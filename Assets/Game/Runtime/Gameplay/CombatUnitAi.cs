using System;
using System.Collections.Generic;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Gameplay
{
    public enum CombatUnitState
    {
        Advancing,
        Pursuing,
        Attacking,
        Dead
    }

    public static class CombatUnitStateMachine
    {
        public static CombatUnitState Resolve(bool hasValidTarget, long distanceSquared,
            int attackRange, int chaseRadius)
        {
            if (!hasValidTarget || distanceSquared > (long)chaseRadius * chaseRadius)
                return CombatUnitState.Advancing;

            return distanceSquared <= (long)attackRange * attackRange
                ? CombatUnitState.Attacking
                : CombatUnitState.Pursuing;
        }
    }

    internal readonly struct CombatTargetId : IEquatable<CombatTargetId>, IComparable<CombatTargetId>
    {
        public CombatTargetId(CombatTargetKind kind, int numericId, string stableId = null)
        {
            Kind = kind;
            NumericId = numericId;
            StableId = stableId ?? string.Empty;
        }

        public CombatTargetKind Kind { get; }
        public int NumericId { get; }
        public string StableId { get; }
        public bool IsNone => Kind == CombatTargetKind.None;
        public bool IsDynamic => Kind is CombatTargetKind.Unit or CombatTargetKind.Gatherer or
            CombatTargetKind.Tower or CombatTargetKind.Boss;

        public string ToCompatibilityKey()
        {
            return Kind switch
            {
                CombatTargetKind.Unit => $"unit:{NumericId}",
                CombatTargetKind.Gatherer => $"gatherer:{NumericId}",
                CombatTargetKind.Tower => $"tower:{NumericId}",
                CombatTargetKind.Boss => $"boss:{StableId}",
                CombatTargetKind.Wall => $"wall:{StableId}",
                _ => string.Empty
            };
        }

        public int CompareTo(CombatTargetId other)
        {
            var kind = Kind.CompareTo(other.Kind);
            if (kind != 0) return kind;
            var numeric = NumericId.CompareTo(other.NumericId);
            return numeric != 0 ? numeric : string.Compare(StableId, other.StableId, StringComparison.Ordinal);
        }

        public bool Equals(CombatTargetId other) => Kind == other.Kind && NumericId == other.NumericId &&
            string.Equals(StableId, other.StableId, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CombatTargetId other && Equals(other);
        public override int GetHashCode() => HashCode.Combine((int)Kind, NumericId, StableId);
        public static bool operator ==(CombatTargetId left, CombatTargetId right) => left.Equals(right);
        public static bool operator !=(CombatTargetId left, CombatTargetId right) => !left.Equals(right);
        public static CombatTargetId None => default;
    }

    internal readonly struct CombatTargetCandidate
    {
        public CombatTargetCandidate(CombatTargetId id, MatchFaction faction, int x, int y)
        {
            Id = id;
            Faction = faction;
            X = x;
            Y = y;
        }

        public CombatTargetId Id { get; }
        public MatchFaction Faction { get; }
        public int X { get; }
        public int Y { get; }
    }

    internal sealed class CombatTargetIndex
    {
        private const int CellSize = 128;
        private readonly Dictionary<(MatchFaction Faction, int CellX, int CellY), List<CombatTargetCandidate>> _cells = new();
        private readonly Dictionary<(CombatTargetId Id, MatchFaction Faction), CombatTargetCandidate> _byId = new();
        private readonly Stack<List<CombatTargetCandidate>> _bucketPool = new();

        public void Reset()
        {
            foreach (var values in _cells.Values)
            {
                values.Clear();
                _bucketPool.Push(values);
            }
            _cells.Clear();
            _byId.Clear();
        }

        public void Add(CombatTargetCandidate candidate)
        {
            _byId[(candidate.Id, candidate.Faction)] = candidate;
            var key = (candidate.Faction, FloorCell(candidate.X), FloorCell(candidate.Y));
            if (!_cells.TryGetValue(key, out var values))
            {
                values = _bucketPool.Count > 0 ? _bucketPool.Pop() : new List<CombatTargetCandidate>();
                _cells.Add(key, values);
            }
            values.Add(candidate);
        }

        public void Seal()
        {
            foreach (var values in _cells.Values)
                values.Sort((left, right) => left.Id.CompareTo(right.Id));
        }

        public bool TryGet(CombatTargetId id, MatchFaction faction, out CombatTargetCandidate candidate) =>
            _byId.TryGetValue((id, faction), out candidate);

        public void QueryOpponents(MatchFaction faction, int x, int y, int radius, List<CombatTargetCandidate> results)
        {
            results.Clear();
            var opponent = faction == MatchFaction.Player ? MatchFaction.Enemy : MatchFaction.Player;
            var minimumX = FloorCell(x - radius);
            var maximumX = FloorCell(x + radius);
            var minimumY = FloorCell(y - radius);
            var maximumY = FloorCell(y + radius);
            var radiusSquared = (long)radius * radius;
            for (var cellX = minimumX; cellX <= maximumX; cellX++)
            for (var cellY = minimumY; cellY <= maximumY; cellY++)
            {
                if (!_cells.TryGetValue((opponent, cellX, cellY), out var values)) continue;
                foreach (var candidate in values)
                {
                    if (DistanceSquared(x, y, candidate.X, candidate.Y) <= radiusSquared)
                        results.Add(candidate);
                }
            }
        }

        private static int FloorCell(int value)
        {
            if (value >= 0) return value / CellSize;
            return -((-value + CellSize - 1) / CellSize);
        }

        public static long DistanceSquared(int leftX, int leftY, int rightX, int rightY)
        {
            var deltaX = (long)rightX - leftX;
            var deltaY = (long)rightY - leftY;
            return deltaX * deltaX + deltaY * deltaY;
        }
    }

    internal static class CombatTargetSelector
    {
        public static bool TrySelect(UnitTargetPriority policy, bool structuresOnly, int x, int y,
            int attackRange, IReadOnlyList<CombatTargetCandidate> candidates, out CombatTargetCandidate selected)
        {
            selected = default;
            var found = false;
            var bestCategory = int.MaxValue;
            var bestDistance = long.MaxValue;
            foreach (var candidate in candidates)
            {
                if (!IsAllowed(candidate.Id.Kind, structuresOnly)) continue;
                var distance = CombatTargetIndex.DistanceSquared(x, y, candidate.X, candidate.Y);
                if (candidate.Id.Kind == CombatTargetKind.Wall && distance > (long)attackRange * attackRange) continue;
                var category = Category(policy, candidate.Id.Kind);
                var replace = !found;
                if (found && policy == UnitTargetPriority.DistanceThenThreat)
                    replace = distance < bestDistance || distance == bestDistance &&
                        (category < bestCategory || category == bestCategory && candidate.Id.CompareTo(selected.Id) < 0);
                else if (found)
                    replace = category < bestCategory || category == bestCategory &&
                        (distance < bestDistance || distance == bestDistance && candidate.Id.CompareTo(selected.Id) < 0);
                if (!replace) continue;
                found = true;
                selected = candidate;
                bestCategory = category;
                bestDistance = distance;
            }
            return found;
        }

        private static bool IsAllowed(CombatTargetKind kind, bool structuresOnly)
        {
            return structuresOnly ? kind is CombatTargetKind.Tower or CombatTargetKind.Wall :
                kind is CombatTargetKind.Unit or CombatTargetKind.Gatherer or CombatTargetKind.Tower or
                    CombatTargetKind.Boss or CombatTargetKind.Wall;
        }

        private static int Category(UnitTargetPriority policy, CombatTargetKind kind)
        {
            if (policy == UnitTargetPriority.WallUnlessThreatened && kind == CombatTargetKind.Wall) return 0;
            return kind switch
            {
                CombatTargetKind.Unit => 0,
                CombatTargetKind.Gatherer => 1,
                CombatTargetKind.Tower => 2,
                CombatTargetKind.Boss => 3,
                CombatTargetKind.Wall => 4,
                _ => int.MaxValue
            };
        }
    }

    internal static class CombatUnitMovement
    {
        private const int MaximumSeparationMilli = 350;

        public static bool MoveTowards(ref int x, ref int y, int targetX, int targetY, int speed, int stopRange,
            int minimumX, int maximumX, int minimumY, int maximumY, int separationX, int separationY,
            ref int remainderX, ref int remainderY)
        {
            if (speed <= 0) return false;
            var deltaX = (long)targetX - x;
            var deltaY = (long)targetY - y;
            var distanceSquared = deltaX * deltaX + deltaY * deltaY;
            if (distanceSquared <= (long)stopRange * stopRange) return false;
            var distance = IntegerSqrtCeil(distanceSquared);
            var remaining = Math.Max(0L, distance - Math.Max(0, stopRange));
            var step = Math.Min(speed, remaining);
            if (step <= 0) return false;

            var directionX = deltaX * 1000 / Math.Max(1, distance);
            var directionY = deltaY * 1000 / Math.Max(1, distance);
            var parallel = (separationX * directionX + separationY * directionY) / 1000;
            var perpendicularX = separationX - directionX * parallel / 1000;
            var perpendicularY = separationY - directionY * parallel / 1000;
            var perpendicularMagnitude = IntegerSqrt((long)perpendicularX * perpendicularX + (long)perpendicularY * perpendicularY);
            if (perpendicularMagnitude > MaximumSeparationMilli)
            {
                perpendicularX = (int)(perpendicularX * MaximumSeparationMilli / perpendicularMagnitude);
                perpendicularY = (int)(perpendicularY * MaximumSeparationMilli / perpendicularMagnitude);
            }
            var steeredX = directionX + perpendicularX;
            var steeredY = directionY + perpendicularY;

            var magnitude = Math.Max(1L, IntegerSqrt(steeredX * steeredX + steeredY * steeredY));
            var normalizedX = steeredX * 1000 / magnitude;
            var normalizedY = steeredY * 1000 / magnitude;
            var accumulatedX = normalizedX * step + remainderX;
            var accumulatedY = normalizedY * step + remainderY;
            var moveX = accumulatedX / 1000;
            var moveY = accumulatedY / 1000;
            remainderX = (int)(accumulatedX % 1000);
            remainderY = (int)(accumulatedY % 1000);
            if (moveX == 0 && moveY == 0)
            {
                if (Math.Abs(deltaX) >= Math.Abs(deltaY)) moveX = Math.Sign(deltaX);
                else moveY = Math.Sign(deltaY);
            }

            var nextX = Math.Clamp(x + (int)moveX, minimumX, maximumX);
            var nextY = Math.Clamp(y + (int)moveY, minimumY, maximumY);
            if (nextX == x && nextY == y) return false;
            x = nextX;
            y = nextY;
            return true;
        }

        private static long IntegerSqrt(long value)
        {
            if (value <= 0) return 0;
            var root = (long)Math.Sqrt(value);
            while ((root + 1) <= value / (root + 1)) root++;
            while (root > value / root) root--;
            return root;
        }

        private static long IntegerSqrtCeil(long value)
        {
            var floor = IntegerSqrt(value);
            return floor * floor == value ? floor : floor + 1;
        }
    }
}
