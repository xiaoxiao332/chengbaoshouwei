using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Audio;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Gameplay
{
    public enum MatchFaction { Player, Enemy }
    public enum GathererState { Outbound, Gathering, Returning }

    public sealed class DeterministicRandomStream
    {
        private uint _state;

        public DeterministicRandomStream(int seed)
        {
            _state = unchecked((uint)(seed == 0 ? 1 : seed));
        }

        public static int DeriveSeed(int seed, string streamId)
        {
            unchecked
            {
                var hash = 2166136261u ^ (uint)seed;
                foreach (var character in streamId ?? string.Empty)
                {
                    hash ^= character;
                    hash *= 16777619u;
                }
                return (int)(hash == 0 ? 1 : hash);
            }
        }

        public int Next(int exclusiveMaximum)
        {
            if (exclusiveMaximum <= 0) throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
            var value = NextUInt();
            return (int)(value % (uint)exclusiveMaximum);
        }

        private uint NextUInt()
        {
            var value = _state;
            value ^= value << 13;
            value ^= value >> 17;
            value ^= value << 5;
            _state = value == 0 ? 0x9E3779B9u : value;
            return _state;
        }
    }

    public sealed class ResourceNodeSnapshot
    {
        public ResourceNodeSnapshot(ResourceNodeId id, ResourceId? resourceId, ResourceNodeSpawnGroup group,
            int x, int y, int remaining, int capacity, bool active)
            : this(id, resourceId, group, x, y, -1, -1, remaining, capacity, active, 0) { }

        public ResourceNodeSnapshot(ResourceNodeId id, ResourceId? resourceId, ResourceNodeSpawnGroup group,
            int x, int y, int gridColumn, int gridRow, int remaining, int capacity, bool active, int spawnRevision = 0)
        {
            Id = id;
            ResourceId = resourceId;
            Group = group;
            X = x;
            Y = y;
            GridColumn = gridColumn;
            GridRow = gridRow;
            Remaining = remaining;
            Capacity = capacity;
            Active = active;
            SpawnRevision = spawnRevision;
        }

        public ResourceNodeId Id { get; }
        public ResourceId? ResourceId { get; }
        public ResourceNodeSpawnGroup Group { get; }
        public int X { get; }
        public int Y { get; }
        public int GridColumn { get; }
        public int GridRow { get; }
        public int Remaining { get; }
        public int Capacity { get; }
        public bool Active { get; }
        public int SpawnRevision { get; }
        public bool IsDepleted => Remaining <= 0;
    }

    public readonly struct ResourceGridCell : IEquatable<ResourceGridCell>
    {
        public ResourceGridCell(int column, int row, int x, int y)
        { Column = column; Row = row; X = x; Y = y; }
        public int Column { get; }
        public int Row { get; }
        public int X { get; }
        public int Y { get; }
        public bool Equals(ResourceGridCell other) => Column == other.Column && Row == other.Row;
        public override bool Equals(object obj) => obj is ResourceGridCell other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Column, Row);
    }

    /// <summary>Deterministic runtime occupancy grid for battlefield resource nodes.</summary>
    public sealed class BattlefieldResourceGrid
    {
        private const int ColumnCount = 12;
        private const int RowCount = 8;
        private readonly Dictionary<ResourceNodeSpawnGroup, ResourceGridCell[]> _cells;
        private readonly Dictionary<ResourceGridCell, ResourceNodeId> _occupied = new();

        public BattlefieldResourceGrid(MatchBattlefieldLayoutConfig layout)
        {
            if (layout == null) throw new ArgumentNullException(nameof(layout));
            var bounds = layout.Zones.FirstOrDefault(value => value.Kind == ZoneKind.TowerBuildable);
            if (bounds.Width <= 0 || bounds.Height <= 0)
                bounds = new MatchRect("resource-grid", ZoneKind.TowerBuildable, 0, 0, layout.ReferenceWidth, layout.ReferenceHeight);
            var forbidden = layout.Zones.Where(value => value.Kind is ZoneKind.TowerForbidden or ZoneKind.BossForbidden or ZoneKind.MainGate).ToArray();
            var minimumResourceY = layout.ResourceNodes.Count == 0 ? bounds.Y : layout.ResourceNodes.Min(value => value.Position.Y);
            var maximumResourceY = layout.ResourceNodes.Count == 0 ? bounds.Y + bounds.Height : layout.ResourceNodes.Max(value => value.Position.Y);
            if (maximumResourceY <= minimumResourceY)
            {
                minimumResourceY = bounds.Y;
                maximumResourceY = bounds.Y + bounds.Height;
            }
            var groups = new Dictionary<ResourceNodeSpawnGroup, List<ResourceGridCell>>
            {
                [ResourceNodeSpawnGroup.PlayerSafe] = new(),
                [ResourceNodeSpawnGroup.Central] = new(),
                [ResourceNodeSpawnGroup.EnemySafe] = new()
            };
            for (var column = 0; column < ColumnCount; column++)
            for (var row = 0; row < RowCount; row++)
            {
                var x = bounds.X + (2 * column + 1) * bounds.Width / (2 * ColumnCount);
                var y = bounds.Y + (2 * row + 1) * bounds.Height / (2 * RowCount);
                if (y < minimumResourceY || y > maximumResourceY || forbidden.Any(value => Contains(value, x, y))) continue;
                var group = column < 3 ? ResourceNodeSpawnGroup.PlayerSafe :
                    column >= ColumnCount - 3 ? ResourceNodeSpawnGroup.EnemySafe : ResourceNodeSpawnGroup.Central;
                groups[group].Add(new ResourceGridCell(column, row, x, y));
            }
            _cells = groups.ToDictionary(pair => pair.Key, pair => pair.Value.OrderBy(value => value.Column).ThenBy(value => value.Row).ToArray());
        }

        public int OccupiedCount => _occupied.Count;

        public bool TryReserveRandom(ResourceNodeSpawnGroup group, ResourceNodeId nodeId,
            DeterministicRandomStream random, out ResourceGridCell cell)
        {
            var available = _cells[group].Where(value => !_occupied.ContainsKey(value)).ToArray();
            if (available.Length == 0) { cell = default; return false; }
            cell = available[random.Next(available.Length)];
            _occupied.Add(cell, nodeId);
            return true;
        }

        public bool TryReserveMirrored(ResourceNodeId playerNodeId, ResourceNodeId enemyNodeId,
            DeterministicRandomStream random, out ResourceGridCell playerCell, out ResourceGridCell enemyCell)
        {
            var available = _cells[ResourceNodeSpawnGroup.PlayerSafe]
                .Select(value => (player: value, enemy: Mirror(value)))
                .Where(value => !_occupied.ContainsKey(value.player) && !_occupied.ContainsKey(value.enemy))
                .ToArray();
            if (available.Length == 0) { playerCell = default; enemyCell = default; return false; }
            var picked = available[random.Next(available.Length)];
            playerCell = picked.player; enemyCell = picked.enemy;
            _occupied.Add(playerCell, playerNodeId);
            _occupied.Add(enemyCell, enemyNodeId);
            return true;
        }

        public void Release(ResourceGridCell cell) => _occupied.Remove(cell);

        private static ResourceGridCell Mirror(ResourceGridCell cell) =>
            new(ColumnCount - 1 - cell.Column, cell.Row, 0, 0);
        private static bool Contains(MatchRect rect, int x, int y) =>
            x >= rect.X && x <= rect.X + rect.Width && y >= rect.Y && y <= rect.Y + rect.Height;
    }

    public sealed class ResourceNodeSystem : GameSystemBase, IFixedMatchSimulation
    {
        private sealed class NodeState
        {
            public MatchResourceNodeConfig Config;
            public ResourceId? ResourceId;
            public int Remaining;
            public int ActiveCapacity;
            public bool Active;
            public ResourceGridCell Cell;
            public int SpawnRevision;
            public bool EverActivated;
            
            public ResourceId[] RespawnResourcePool = Array.Empty<ResourceId>();
            public int RespawnTick = -1;
        }

        private readonly MatchConfigSnapshot _config;
        private readonly Dictionary<ResourceNodeId, NodeState> _nodes = new();
        private MatchResourceActivationWaveConfig[] _waves;
        private int _nextWave;
        private int _currentTick;
        private BattlefieldResourceGrid _grid;
        private const int CentralRespawnStartTick = 1800;

        public ResourceNodeSystem(MatchConfigSnapshot config) : base(SystemLifetime.Scene) =>
            _config = config ?? throw new ArgumentNullException(nameof(config));

        public event Action Changed;

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _grid = new BattlefieldResourceGrid(_config.BattlefieldLayout);
            _waves = _config.BattlefieldLayout.ActivationWaves.OrderBy(value => value.TriggerTick).ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
            foreach (var node in _config.BattlefieldLayout.ResourceNodes)
                _nodes.Add(node.Id, new NodeState { Config = node });
            ActivateDueWaves(0);
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _nodes.Clear();
            _waves = Array.Empty<MatchResourceActivationWaveConfig>();
            _nextWave = 0;
            _currentTick = 0;
            _grid = null;
            return Task.CompletedTask;
        }

        public IReadOnlyList<ResourceNodeSnapshot> GetSnapshot() => _nodes.Values
            .OrderBy(value => value.Config.Id.Value, StringComparer.Ordinal)
            .Select(ToSnapshot).ToArray();

        public bool TryGetNode(ResourceNodeId nodeId, out ResourceNodeSnapshot node)
        {
            if (_nodes.TryGetValue(nodeId, out var state))
            {
                node = ToSnapshot(state);
                return true;
            }

            node = null;
            return false;
        }


        public void SimulateTick(int tick)
        {
            _currentTick = tick;
            ActivateDueWaves(tick);
            ReactivateDueNodes(tick);
        }

        public bool TryFindNode(MatchFaction faction, ResourceId resourceId, int fromX, int fromY, out ResourceNodeSnapshot node)
        {
            var safeGroup = faction == MatchFaction.Player ? ResourceNodeSpawnGroup.PlayerSafe : ResourceNodeSpawnGroup.EnemySafe;
            var candidate = _nodes.Values.Where(value => value.Active && value.Remaining > 0 &&
                    value.ResourceId.HasValue && value.ResourceId.Value.Equals(resourceId) &&
                    value.Config.SpawnGroup == safeGroup)
                .OrderBy(value => DistanceSquared(fromX, fromY, value.Cell.X, value.Cell.Y))
                .ThenBy(value => value.Config.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            candidate ??= _nodes.Values.Where(value => value.Active && value.Remaining > 0 &&
                    value.ResourceId.HasValue && value.ResourceId.Value.Equals(resourceId) &&
                    value.Config.SpawnGroup == ResourceNodeSpawnGroup.Central)
                .OrderBy(value => DistanceSquared(fromX, fromY, value.Cell.X, value.Cell.Y))
                .ThenBy(value => value.Config.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            node = candidate == null ? null : ToSnapshot(candidate);
            return node != null;
        }

        public int Harvest(ResourceNodeId nodeId, int expectedSpawnRevision, ResourceId resourceId, int requested)
        {
            if (requested <= 0 || !_nodes.TryGetValue(nodeId, out var node) || !node.Active ||
                node.SpawnRevision != expectedSpawnRevision || !node.ResourceId.HasValue ||
                !node.ResourceId.Value.Equals(resourceId) || node.Remaining <= 0)
                return 0;

            var harvested = Math.Min(requested, node.Remaining);
            node.Remaining -= harvested;
            if (node.Remaining <= 0)
            {
                node.Active = false;
                node.RespawnTick = node.Config.SpawnGroup == ResourceNodeSpawnGroup.Central
                    ? Math.Max(CentralRespawnStartTick, _currentTick + node.Config.RespawnDelayTicks)
                    : _currentTick + node.Config.RespawnDelayTicks;
            }

            Changed?.Invoke();
            return harvested;
        }

        private void ActivateDueWaves(int tick)
        {
            var changed = false;
            while (_nextWave < _waves.Length && tick >= _waves[_nextWave].TriggerTick)
            {
                ActivateWave(_waves[_nextWave++]);
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        private void ActivateWave(MatchResourceActivationWaveConfig wave)
        {
            var waveRandom = new DeterministicRandomStream(
                DeterministicRandomStream.DeriveSeed(_config.Seed, "resource-wave." + wave.Id));
            var groups = new HashSet<ResourceNodeSpawnGroup>(wave.Groups);
            if (groups.Contains(ResourceNodeSpawnGroup.PlayerSafe) && groups.Contains(ResourceNodeSpawnGroup.EnemySafe))
            {
                var candidates = _nodes.Values.Where(value => !value.EverActivated && value.Config.SpawnGroup == ResourceNodeSpawnGroup.PlayerSafe)
                    .OrderBy(value => value.Config.Id.Value, StringComparer.Ordinal).ToList();
                for (var index = 0; index < wave.NodesPerGroup && candidates.Count > 0; index++)
                {
                    var player = candidates[0];
                    candidates.RemoveAt(0);
                    var mirror = _nodes.Values.FirstOrDefault(value => value.Config.Id.Value == player.Config.MirrorNodeId);
                    if (mirror == null) continue;
                    var resource = ChooseResource(wave, player.Config, index);
                    Activate(player, resource, ConfiguredCell(player), GetRespawnPool(wave, player.Config), false);
                    Activate(mirror, resource, ConfiguredCell(mirror), GetRespawnPool(wave, mirror.Config), false);
                }
                groups.Remove(ResourceNodeSpawnGroup.PlayerSafe);
                groups.Remove(ResourceNodeSpawnGroup.EnemySafe);
            }

            foreach (var group in groups.OrderBy(value => value))
            {
                var candidates = _nodes.Values.Where(value => !value.EverActivated && value.Config.SpawnGroup == group)
                    .OrderBy(value => value.Config.Id.Value, StringComparer.Ordinal).ToList();
                for (var index = 0; index < wave.NodesPerGroup && candidates.Count > 0; index++)
                {
                    var candidateIndex = group == ResourceNodeSpawnGroup.Central ? waveRandom.Next(candidates.Count) : 0;
                    var node = candidates[candidateIndex];
                    candidates.RemoveAt(candidateIndex);
                    Activate(node, ChooseResource(wave, node.Config, index, waveRandom), ConfiguredCell(node),
                        GetRespawnPool(wave, node.Config), false);
                }
            }
        }

        private static ResourceId[] GetResourcePool(MatchResourceActivationWaveConfig wave,
            MatchResourceNodeConfig node)
        {
            var allowed = wave.AllowedResourceIds
                .Where(id => node.AllowedResourceIds.Contains(id))
                .Distinct()
                .OrderBy(id => id.Value, StringComparer.Ordinal)
                .ToArray();
            if (allowed.Length == 0)
                throw new InvalidOperationException(
                    $"Resource node '{node.Id}' has no resource allowed by wave '{wave.Id}'.");
            return allowed;
        }

        private static ResourceId[] GetRespawnPool(MatchResourceActivationWaveConfig wave,
            MatchResourceNodeConfig node)
        {
            if (node.SpawnGroup != ResourceNodeSpawnGroup.Central)
                return GetResourcePool(wave, node);
            return new[]
            {
                new ResourceId(ContentConstants.FoodResourceId),
                new ResourceId(ContentConstants.WoodResourceId),
                new ResourceId(ContentConstants.RawStoneResourceId),
                new ResourceId(ContentConstants.IronOreResourceId)
            }.Where(node.AllowedResourceIds.Contains).ToArray();
        }

        private ResourceId ChooseResource(MatchResourceActivationWaveConfig wave, MatchResourceNodeConfig node, int ordinal,
            DeterministicRandomStream random = null)
        {
            var allowed = wave.AllowedResourceIds.Where(id => node.AllowedResourceIds.Contains(id)).OrderBy(id => id.Value, StringComparer.Ordinal).ToArray();
            if (allowed.Length == 0) throw new InvalidOperationException($"Resource node '{node.Id}' has no resource allowed by wave '{wave.Id}'.");
            if (node.SpawnGroup == ResourceNodeSpawnGroup.Central && allowed.Length == 4 && random != null)
                return ChooseWeightedCentralResource(allowed, random);
            return allowed[ordinal % allowed.Length];
        }

        private void ReactivateDueNodes(int tick)
        {
            var changed = false;
            foreach (var node in _nodes.Values.Where(value => !value.Active && value.RespawnTick >= 0 && tick >= value.RespawnTick)
                         .OrderBy(value => value.Config.Id.Value, StringComparer.Ordinal))
            {
                var allowed = node.RespawnResourcePool ?? Array.Empty<ResourceId>();
                if (allowed.Length == 0)
                    continue;

                var central = node.Config.SpawnGroup == ResourceNodeSpawnGroup.Central;
                var respawnRandom = new DeterministicRandomStream(DeterministicRandomStream.DeriveSeed(_config.Seed,
                    $"resource-respawn.{node.Config.Id.Value}.{node.SpawnRevision + 1}"));
                var resourceId = central ? ChooseWeightedCentralResource(allowed, respawnRandom) : allowed[0];
                Activate(node, resourceId, ConfiguredCell(node), allowed, !central);
                changed = true;
            }

            if (changed)
                Changed?.Invoke();
        }

        private void Activate(NodeState node, ResourceId resourceId, ResourceGridCell cell,
            IReadOnlyList<ResourceId> respawnResourcePool, bool safeRespawn)
        {
            node.ResourceId = resourceId;
            node.ActiveCapacity = ResolveCapacity(resourceId, node.Config, safeRespawn);
            node.Remaining = node.ActiveCapacity;
            node.Active = true;
            node.EverActivated = true;
            node.Cell = cell;
            node.RespawnResourcePool = respawnResourcePool == null
                ? Array.Empty<ResourceId>()
                : respawnResourcePool.Distinct()
                    .OrderBy(value => value.Value, StringComparer.Ordinal)
                    .ToArray();
            node.SpawnRevision = checked(node.SpawnRevision + 1);
            node.RespawnTick = -1;
        }

        private static ResourceId ChooseWeightedCentralResource(IReadOnlyList<ResourceId> allowed,
            DeterministicRandomStream random)
        {
            var roll = random.Next(100);
            var desired = roll < 25 ? ContentConstants.FoodResourceId :
                roll < 50 ? ContentConstants.WoodResourceId :
                roll < 80 ? ContentConstants.RawStoneResourceId : ContentConstants.IronOreResourceId;
            var match = allowed.FirstOrDefault(value => value.Value == desired);
            return match.Value != null ? match : allowed[random.Next(allowed.Count)];
        }

        private static int ResolveCapacity(ResourceId resourceId, MatchResourceNodeConfig config, bool isRespawn)
        {
            if (isRespawn && config.RespawnCapacity > 0) return config.RespawnCapacity;
            if (config.SpawnGroup != ResourceNodeSpawnGroup.Central) return config.Capacity;
            return resourceId.Value switch
            {
                ContentConstants.FoodResourceId => 160,
                ContentConstants.WoodResourceId => 160,
                ContentConstants.RawStoneResourceId => 140,
                ContentConstants.IronOreResourceId => 100,
                _ => config.Capacity
            };
        }

        private static ResourceGridCell ConfiguredCell(NodeState node)
        {
            var id = node.Config.Id.Value ?? string.Empty;
            var suffix = id.Length > 0 && char.IsDigit(id[^1]) ? id[^1] - '0' : 0;
            var column = node.Config.SpawnGroup switch
            {
                ResourceNodeSpawnGroup.PlayerSafe => 0,
                ResourceNodeSpawnGroup.Central => 4 + suffix,
                ResourceNodeSpawnGroup.EnemySafe => 11,
                _ => -1
            };
            return new ResourceGridCell(column, suffix, node.Config.Position.X, node.Config.Position.Y);
        }

        private static ResourceNodeSnapshot ToSnapshot(NodeState value) => new(value.Config.Id, value.ResourceId,
            value.Config.SpawnGroup, value.Cell.X, value.Cell.Y, value.Cell.Column, value.Cell.Row,
            value.Remaining, value.ActiveCapacity, value.Active, value.SpawnRevision);
        private static long DistanceSquared(int x1, int y1, int x2, int y2)
        { var x = (long)x2 - x1; var y = (long)y2 - y1; return x * x + y * y; }
    }

    public sealed class GathererSnapshot
    {
        public GathererSnapshot(int id, GathererSourceId sourceId, int buildingInstanceId, MatchFaction faction, GathererState state,
            int x, int y, ResourceId resourceId, int carriedAmount, ResourceNodeId targetNodeId = default,
            int targetX = 0, int targetY = 0, int targetSpawnRevision = 0, RouteId routeId = default,
            int health = 1, int maxHealth = 1, int damageRevision = 0, UnitId unitId = default)
        {
            Id = id;
            SourceId = sourceId;
            BuildingInstanceId = buildingInstanceId;
            Faction = faction;
            State = state;
            X = x;
            Y = y;
            ResourceId = resourceId;
            CarriedAmount = carriedAmount;
            TargetNodeId = targetNodeId;
            TargetX = targetX;
            TargetY = targetY;
            TargetSpawnRevision = targetSpawnRevision;
            RouteId = routeId;
            Health = health;
            MaxHealth = maxHealth;
            DamageRevision = damageRevision;
            UnitId = unitId;
        }

        public int Id { get; }
        public GathererSourceId SourceId { get; }
        public int BuildingInstanceId { get; }
        public MatchFaction Faction { get; }
        public GathererState State { get; }
        public int X { get; }
        public int Y { get; }
        public ResourceId ResourceId { get; }
        public int CarriedAmount { get; }
        public ResourceNodeId TargetNodeId { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public int TargetSpawnRevision { get; }
        public RouteId RouteId { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public int DamageRevision { get; }
        public UnitId UnitId { get; }
    }

    public class GathererSystem : GameSystemBase, IFixedMatchSimulation
    {
        private sealed class Source
        {
            public GathererSourceId Id;
            public int BuildingInstanceId;
            public MatchGathererConfig Config;
            public MatchPoint Gate;
            public int NextDispatchTick;
            public int EfficiencyRemainderMilli;
            public bool Paused;
            public int Level = 1;
            public int ResourceIndex;
            public int DispatchCount;
            public int CompletedTrips;
            public int DeliveredAmount;
            public int DispatchCostAmount;
            public int DeathCount;
            public DeterministicRandomStream DispatchRandom;
        }

        private sealed class Worker
        {
            public int Id;
            public Source Source;
            public ResourceNodeId TargetNodeId;
            public int TargetSpawnRevision;
            public int TargetNodeX;
            public int TargetNodeY;
            public ResourceId ResourceId;
            public GathererState State;
            public int StateTick;
            public int X;
            public int Y;
            public int FromX;
            public int FromY;
            public int TargetX;
            public int TargetY;
            public int Carried;
            public int MovementRemainderX;
            public int MovementRemainderY;
            public int Health;
            public int DamageRevision;
        }

        private readonly MatchFaction _faction;
        private readonly EconomySystem _economy;
        private readonly ResourceNodeSystem _nodes;
        private readonly MatchPoint _gate;
        private readonly IReadOnlyList<MatchGathererConfig> _configs;
        private readonly BuildingSystem _buildings;
        private readonly MatchBattlefieldLayoutConfig _layout;
        private readonly int _matchSeed;
        private readonly Dictionary<GathererSourceId, Source> _sources = new();
        private readonly Dictionary<int, Worker> _workers = new();
        private int _nextWorkerId = 1;
        private int _currentTick;

        protected virtual int EconomicEfficiencyMilli => 1000;

        public GathererSystem(MatchFaction faction, IReadOnlyList<MatchGathererConfig> configs, EconomySystem economy,
            ResourceNodeSystem nodes, MatchPoint gate, BuildingSystem buildings = null,
            MatchBattlefieldLayoutConfig layout = null, int matchSeed = 1) : base(SystemLifetime.Scene)
        {
            _faction = faction; _configs = configs ?? Array.Empty<MatchGathererConfig>(); _economy = economy;
            _nodes = nodes; _gate = gate; _buildings = buildings; _layout = layout; _matchSeed = matchSeed;
        }

        public event Action Changed;
        public event Action<GatherCompleteAudioEvent> HarvestCompleted;
        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            foreach (var config in _configs)
            {
                var source = new Source
                {
                    Id = config.SourceId,
                    Config = config,
                    Gate = ResolveRouteGate(config.RouteId),
                    NextDispatchTick = 0,
                    DispatchRandom = CreateDispatchRandom(config.SourceId)
                };
                _sources.Add(source.Id, source);
                TryDispatch(source);
                source.NextDispatchTick = ResolveDispatchInterval(source);
            }
            return Task.CompletedTask;
        }
        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        { _workers.Clear(); _sources.Clear(); return Task.CompletedTask; }

        public IReadOnlyList<GathererSnapshot> GetSnapshot() => _workers.Values
            .OrderBy(value => value.Id)
            .Select(value => new GathererSnapshot(value.Id, value.Source.Id, value.Source.BuildingInstanceId, _faction, value.State,
                value.X, value.Y, value.ResourceId, value.Carried, value.TargetNodeId,
                value.TargetNodeX, value.TargetNodeY, value.TargetSpawnRevision,
                value.Source.Config.RouteId,
                value.Health, value.Source.Config.MaxHealth, value.DamageRevision, value.Source.Config.UnitId))
            .ToArray();

        public IReadOnlyList<GathererSourceEconomySnapshot> GetSourceEconomySnapshot() => _sources.Values
            .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
            .Select(value => new GathererSourceEconomySnapshot(value.Id, value.BuildingInstanceId,
                value.DispatchCount, value.CompletedTrips, value.DeliveredAmount,
                value.DispatchCostAmount, value.DeathCount)).ToArray();

        public bool Kill(int workerId) => TryDamage(workerId, int.MaxValue);

        public bool TryDamage(int workerId, int damage)
        {
            if (damage <= 0 || !_workers.TryGetValue(workerId, out var worker))
                return false;
            worker.Health = Math.Max(0, worker.Health - damage);
            worker.DamageRevision++;
            if (worker.Health == 0) Recycle(worker, false);
            Changed?.Invoke();
            return true;
        }

        public void SimulateTick(int tick)
        {
            _currentTick = tick;
            var changed = SyncBuildingWorkers();
            foreach (var source in _sources.Values.OrderBy(value => value.Id.Value, StringComparer.Ordinal).ToArray())
            {
                if (source.Paused)
                {
                    source.NextDispatchTick++;
                    continue;
                }
                if (tick >= source.NextDispatchTick)
                {
                    var dispatched = TryDispatch(source);
                    changed |= dispatched;
                    if (dispatched) source.NextDispatchTick = tick + ResolveDispatchInterval(source);
                }
            }
            foreach (var worker in _workers.Values.OrderBy(value => value.Id).ToArray())
            {
                switch (worker.State)
                {
                    case GathererState.Outbound: changed |= TickOutbound(worker); break;
                    case GathererState.Gathering: changed |= TickGathering(worker); break;
                    case GathererState.Returning: changed |= TickReturning(worker); break;
                }
            }
            if (changed) Changed?.Invoke();
        }

        private bool TickOutbound(Worker worker)
        {
            if (worker.TargetNodeId.Value != null && !IsCurrentTargetValid(worker))
                ClearTargetForOutbound(worker);

            if (worker.TargetNodeId.Value == null)
            {
                if (!TryFindNode(worker, out var node))
                    return false;

                worker.TargetNodeId = node.Id;
                worker.ResourceId = node.ResourceId ?? worker.ResourceId;
                worker.TargetNodeX = node.X;
                worker.TargetNodeY = node.Y;
            worker.TargetSpawnRevision = node.SpawnRevision;
                worker.FromX = worker.X;
                worker.FromY = worker.Y;
                worker.TargetX = node.X;
                worker.TargetY = node.Y;
                worker.StateTick = 0;
            }

            Move(worker);
            if (worker.X != worker.TargetX || worker.Y != worker.TargetY)
                return true;

            worker.State = GathererState.Gathering;
            worker.StateTick = 0;
            return true;
        }

        private bool TickGathering(Worker worker)
        {
            if (!IsCurrentTargetValid(worker))
            {
                ClearTargetForOutbound(worker);
                return true;
            }

            worker.StateTick++;
            if (worker.StateTick < Math.Max(1, worker.Source.Config.GatherTicks))
                return true;

            var baseCarry = Math.Max(1, worker.Source.Config.CarryAmount);
            if (worker.Source.BuildingInstanceId != 0 && worker.Source.Level >= 3)
                baseCarry = Math.Max(1, (baseCarry * 120 + 99) / 100);
            var scaled = checked(baseCarry * EconomicEfficiencyMilli + worker.Source.EfficiencyRemainderMilli);
            var visibleCarry = Math.Max(1, scaled / 1000);
            worker.Source.EfficiencyRemainderMilli = scaled % 1000;
            worker.Carried = _nodes.Harvest(worker.TargetNodeId, worker.TargetSpawnRevision, worker.ResourceId, visibleCarry);
            if (worker.Carried <= 0)
            {
                ClearTargetForOutbound(worker);
                return true;
            }

            HarvestCompleted?.Invoke(new GatherCompleteAudioEvent(_faction, worker.X, worker.Y, worker.Carried));

            worker.State = GathererState.Returning;
            worker.StateTick = 0;
            worker.FromX = worker.X;
            worker.FromY = worker.Y;
            worker.TargetX = worker.Source.Gate.X;
            worker.TargetY = worker.Source.Gate.Y;
            worker.MovementRemainderX = 0;
            worker.MovementRemainderY = 0;
            return true;
        }

        private bool TickReturning(Worker worker)
        {
            Move(worker);
            if (worker.X != worker.TargetX || worker.Y != worker.TargetY)
                return true;
            if (!_economy.TryAdd(worker.ResourceId, worker.Carried, out _))
                return false;

            worker.Source.CompletedTrips++;
            worker.Source.DeliveredAmount += worker.Carried;

            Recycle(worker, true);
            return true;
        }

        private bool TryDispatch(Source source)
        {
            if (source.Config.DispatchCosts.Count > 0)
            {
                if (!_economy.TryReserve(source.Config.DispatchCosts, source.Id.Value, "intent.gather",
                        out var reservation, out _))
                {
                    if (source.BuildingInstanceId != 0)
                        _buildings?.SetExternalBlockReason(source.BuildingInstanceId, ProductionBlockReason.MissingInput);
                    return false;
                }
                if (!_economy.TryCommit(reservation, source.Config.DispatchCosts, out _))
                    throw new InvalidOperationException("A validated gatherer dispatch reservation could not be committed.");
                source.DispatchCostAmount += source.Config.DispatchCosts.Sum(value => value.Amount);
            }
            if (source.BuildingInstanceId != 0)
                _buildings?.SetExternalBlockReason(source.BuildingInstanceId, ProductionBlockReason.None);
            var resourceIndex = source.Config.SelectionPolicy == GathererResourceSelectionPolicy.RoundRobin
                ? source.ResourceIndex++ % source.Config.AllowedResourceIds.Count : 0;
            var worker = new Worker
            {
                Id = _nextWorkerId++, Source = source,
                ResourceId = source.Config.AllowedResourceIds[resourceIndex]
            };
            ResetWorker(worker);
            _workers.Add(worker.Id, worker);
            source.DispatchCount++;
            return true;
        }

        private void ResetWorker(Worker worker)
        {
            worker.Health = worker.Source.Config.MaxHealth;
            worker.State = GathererState.Outbound;
            worker.StateTick = 0;
            worker.X = worker.Source.Gate.X;
            worker.Y = worker.Source.Gate.Y;
            worker.FromX = worker.Source.Gate.X;
            worker.FromY = worker.Source.Gate.Y;
            worker.TargetX = worker.Source.Gate.X;
            worker.TargetY = worker.Source.Gate.Y;
            worker.TargetNodeId = default;
            worker.TargetNodeX = 0;
            worker.TargetNodeY = 0;
            worker.TargetSpawnRevision = 0;
            worker.Carried = 0;
            worker.MovementRemainderX = 0;
            worker.MovementRemainderY = 0;
        }

        private bool SyncBuildingWorkers()
        {
            if (_buildings == null) return false;
            var sources = _buildings.GetSnapshot().Where(value => value.BuildingId.HasValue &&
                _buildings.GetConfig(value.InstanceId)?.Category == BuildingCategory.Gathering)
                .ToDictionary(value => value.InstanceId);
            var changed = false;
            foreach (var staleSource in _sources.Values.Where(value => value.BuildingInstanceId != 0 &&
                         !sources.ContainsKey(value.BuildingInstanceId)).ToArray())
            {
                foreach (var workerId in _workers.Values.Where(value => value.Source == staleSource)
                             .Select(value => value.Id).ToArray())
                    _workers.Remove(workerId);
                _sources.Remove(staleSource.Id);
                changed = true;
            }

            foreach (var buildingSnapshot in sources.Values.OrderBy(value => value.InstanceId))
            {
                var sourceId = new GathererSourceId($"gatherer-source.building.{buildingSnapshot.InstanceId}");
                if (_sources.TryGetValue(sourceId, out var existing))
                {
                    existing.Paused = buildingSnapshot.Paused;
                    existing.Level = Math.Max(1, buildingSnapshot.Level);
                    continue;
                }
                var building = _buildings.GetConfig(buildingSnapshot.InstanceId);
                if (building?.WorkerUnitId.HasValue != true) continue;
                var baseline = _configs.FirstOrDefault(value => value.UnitId.Equals(building.WorkerUnitId.Value));
                if (baseline == null) continue;
                var allowed = building.GathererAllowedResourceIds.Distinct().OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
                if (allowed.Length == 0) continue;
                var route = ResolveBuildingRoute(buildingSnapshot.SlotIndex);
                var config = new MatchGathererConfig(sourceId, route.Id, baseline.UnitId, allowed,
                    building.GathererCarryAmount, Math.Max(1, building.WorkerGatherTicks), baseline.MovePerTick,
                    baseline.MaxHealth, building.GathererDispatchCosts, building.GathererDispatchIntervalTicks,
                    building.GathererResourceSelectionPolicy, building.Id);
                var source = new Source
                {
                    Id = sourceId,
                    BuildingInstanceId = buildingSnapshot.InstanceId,
                    Config = config,
                    Gate = ResolveRouteGate(route.Id),
                    NextDispatchTick = _currentTick,
                    DispatchRandom = CreateDispatchRandom(sourceId),
                    Paused = buildingSnapshot.Paused,
                    Level = Math.Max(1, buildingSnapshot.Level)
                };
                _sources.Add(source.Id, source);
                changed |= TryDispatch(source);
                source.NextDispatchTick = _currentTick + ResolveDispatchInterval(source);
            }
            return changed;
        }

        private MatchRouteConfig ResolveBuildingRoute(int slotIndex)
        {
            if (_layout?.Routes == null || _layout.Routes.Count == 0)
                throw new InvalidOperationException("Gatherer buildings require battlefield routes.");
            var zoneKind = _faction == MatchFaction.Player ? ZoneKind.PlayerDeployment : ZoneKind.EnemyDeployment;
            var zone = _layout.Zones.FirstOrDefault(value => value.Kind == zoneKind);
            var column = Math.Clamp(slotIndex % 3, 0, 2);
            var row = Math.Clamp(slotIndex / 3, 0, 2);
            var slotX = zone.Width > 0 ? zone.X + (2 * column + 1) * zone.Width / 6 : _gate.X;
            var slotY = zone.Height > 0 ? zone.Y + (2 * row + 1) * zone.Height / 6 : _gate.Y;
            return _layout.Routes
                .Select(route => new { Route = route, Gate = ResolveRouteGate(route.Id) })
                .OrderBy(value => Math.Abs(value.Gate.X - slotX) + Math.Abs(value.Gate.Y - slotY))
                .ThenBy(value => value.Route.Id.Value, StringComparer.Ordinal)
                .Select(value => value.Route)
                .First();
        }

        private MatchPoint ResolveRouteGate(RouteId routeId)
        {
            var route = _layout?.Routes?.FirstOrDefault(value => value.Id.Equals(routeId));
            if (route == null || route.Points.Count == 0) return _gate;
            return route.Points.OrderBy(value => Math.Abs(value.X - _gate.X) + Math.Abs(value.Y - _gate.Y))
                .ThenBy(value => value.Id, StringComparer.Ordinal).First();
        }

        private bool TryFindNode(Worker worker, out ResourceNodeSnapshot selected)
        {
            selected = null;
            var bestDistance = long.MaxValue;
            if (_nodes.TryFindNode(_faction, worker.ResourceId, worker.X, worker.Y, out var candidate))
            {
                var distance = DistanceSquared(worker.X, worker.Y, candidate.X, candidate.Y);
                if (distance < bestDistance) { selected = candidate; bestDistance = distance; }
            }
            return selected != null;
        }

        private void Recycle(Worker worker, bool completedTrip)
        {
            _workers.Remove(worker.Id);
            if (!completedTrip) worker.Source.DeathCount++;
            if (completedTrip && worker.Source.BuildingInstanceId != 0)
                _buildings?.RecordExternalWork(worker.Source.BuildingInstanceId);
        }

        private void Move(Worker worker)
        {
            CombatUnitMovement.MoveTowards(ref worker.X, ref worker.Y, worker.TargetX, worker.TargetY,
                worker.Source.Config.MovePerTick, 0, 0, Math.Max(1, _layout?.ReferenceWidth ?? 1920),
                0, Math.Max(1, _layout?.ReferenceHeight ?? 1080), 0, 0,
                ref worker.MovementRemainderX, ref worker.MovementRemainderY);
        }

        private int ResolveDispatchInterval(Source source)
        {
            var minimum = Math.Max(1, source.Config.DispatchIntervalMinTicks);
            var maximum = Math.Max(minimum, source.Config.DispatchIntervalMaxTicks);
            var interval = minimum == maximum ? minimum : minimum + source.DispatchRandom.Next(maximum - minimum + 1);
            return source.BuildingInstanceId != 0 && source.Level >= 2
                ? Math.Max(1, (interval * 85 + 99) / 100)
                : interval;
        }

        private DeterministicRandomStream CreateDispatchRandom(GathererSourceId sourceId) =>
            new(DeterministicRandomStream.DeriveSeed(_matchSeed,
                $"gatherer-dispatch.{_faction.ToString().ToLowerInvariant()}.{sourceId.Value}"));
    

        private static void ClearTargetForOutbound(Worker worker)
        {
            worker.TargetNodeId = default;
            
            worker.TargetNodeX = 0;
            worker.TargetNodeY = 0;
            worker.TargetSpawnRevision = 0;
            worker.State = GathererState.Outbound;
            worker.StateTick = 0;
            worker.FromX = worker.X;
            worker.FromY = worker.Y;
            worker.TargetX = worker.X;
            worker.TargetY = worker.Y;
        }


        private bool IsCurrentTargetValid(Worker worker)
        {
            return worker.TargetNodeId.Value != null &&
                   _nodes.TryGetNode(worker.TargetNodeId, out var node) &&
                   node.Active &&
                   node.Remaining > 0 &&
                   node.ResourceId.HasValue &&
                   node.ResourceId.Value.Equals(worker.ResourceId) &&
                   node.SpawnRevision == worker.TargetSpawnRevision &&
                   node.X == worker.TargetNodeX &&
                   node.Y == worker.TargetNodeY;
        }

        private static long DistanceSquared(int x1, int y1, int x2, int y2)
        { var x = (long)x2 - x1; var y = (long)y2 - y1; return x * x + y * y; }
}

    public sealed class PlayerGathererSystem : GathererSystem
    {
        public PlayerGathererSystem(IReadOnlyList<MatchGathererConfig> configs, EconomySystem economy, ResourceNodeSystem nodes, MatchPoint gate,
            BuildingSystem buildings = null, MatchBattlefieldLayoutConfig layout = null, int matchSeed = 1)
            : base(MatchFaction.Player, configs, economy, nodes, gate, buildings, layout, matchSeed) { }
    }

    public sealed class GathererSourceEconomySnapshot
    {
        public GathererSourceEconomySnapshot(GathererSourceId sourceId, int buildingInstanceId,
            int dispatchCount, int completedTrips, int deliveredAmount, int dispatchCostAmount, int deathCount)
        {
            SourceId = sourceId; BuildingInstanceId = buildingInstanceId; DispatchCount = dispatchCount;
            CompletedTrips = completedTrips; DeliveredAmount = deliveredAmount;
            DispatchCostAmount = dispatchCostAmount; DeathCount = deathCount;
        }
        public GathererSourceId SourceId { get; }
        public int BuildingInstanceId { get; }
        public int DispatchCount { get; }
        public int CompletedTrips { get; }
        public int DeliveredAmount { get; }
        public int DispatchCostAmount { get; }
        public int DeathCount { get; }
        public int NetResourceUnits => DeliveredAmount - DispatchCostAmount;
    }

    public sealed class EnemyGathererSystem : GathererSystem
    {
        private readonly int _economicEfficiencyMilli;
        public EnemyGathererSystem(IReadOnlyList<MatchGathererConfig> configs, EconomySystem economy, ResourceNodeSystem nodes, MatchPoint gate,
            int economicEfficiencyMilli = 1000, MatchBattlefieldLayoutConfig layout = null,
            BuildingSystem buildings = null, int matchSeed = 1)
            : base(MatchFaction.Enemy, configs, economy, nodes, gate, buildings, layout, matchSeed)
        { _economicEfficiencyMilli = Math.Clamp(economicEfficiencyMilli, 1000, 1100); }
        protected override int EconomicEfficiencyMilli => _economicEfficiencyMilli;
    }

    public sealed class EnemyEconomyTransactionSnapshot
    {
        public EnemyEconomyTransactionSnapshot(long transactionId, int tick, ResourceId resourceId, int amount,
            string sourceId, string intentId, string targetId, string result)
        { TransactionId = transactionId; Tick = tick; ResourceId = resourceId; Amount = amount; SourceId = sourceId; IntentId = intentId; TargetId = targetId; Result = result; }
        public long TransactionId { get; }
        public int Tick { get; }
        public ResourceId ResourceId { get; }
        public int Amount { get; }
        public string SourceId { get; }
        public string IntentId { get; }
        public string TargetId { get; }
        public string Result { get; }
    }

    public sealed class EnemyEconomySystem : EconomySystem, IFixedMatchSimulation
    {
        private readonly List<EnemyEconomyTransactionSnapshot> _ledger = new();
        private readonly Dictionary<int, (string SourceId, string IntentId)> _reservationContexts = new();
        private long _nextTransactionId = 1;
        private int _currentTick;

        public EnemyEconomySystem(MatchConfigSnapshot config) : base(config, config.EnemyEconomy.InitialInventory) { }

        public IReadOnlyList<EnemyEconomyTransactionSnapshot> GetLedger() => _ledger.ToArray();
        public void SimulateTick(int tick) => _currentTick = tick;

        protected override void OnTransaction(EconomyTransactionKind kind, ReservationId reservationId,
            IReadOnlyList<EconomyResourceDelta> deltas)
        {
            var transactionId = _nextTransactionId++;
            var reservationContext = _reservationContexts.GetValueOrDefault(reservationId.Value);
            var sourceId = kind switch
            {
                EconomyTransactionKind.Add when _currentTick == 0 => "source.initial-inventory",
                EconomyTransactionKind.Add => "source.resource-delivery",
                EconomyTransactionKind.Exchange => "source.virtual-facility",
                EconomyTransactionKind.ReservationCommit when !string.IsNullOrWhiteSpace(reservationContext.SourceId) => reservationContext.SourceId,
                EconomyTransactionKind.ReservationCommit => $"source.training.reservation-{reservationId.Value}",
                _ => "source.unknown"
            };
            var intentId = kind == EconomyTransactionKind.ReservationCommit
                ? (string.IsNullOrWhiteSpace(reservationContext.IntentId) ? "intent.assault" : reservationContext.IntentId)
                : "intent.develop";
            foreach (var delta in deltas)
                _ledger.Add(new EnemyEconomyTransactionSnapshot(transactionId, _currentTick, delta.ResourceId,
                    delta.Amount, sourceId, intentId, $"reservation.{reservationId.Value}", "committed"));
        }

        protected override void OnReservationCreated(ReservationId reservationId, string sourceId, string intentId) =>
            _reservationContexts[reservationId.Value] = (sourceId, intentId);
        protected override void OnReservationRemoved(ReservationId reservationId) => _reservationContexts.Remove(reservationId.Value);
    }

    public sealed class EnemyBuildingSystem : BuildingSystem
    {
        public EnemyBuildingSystem(MatchConfigSnapshot config, EconomySystem economy) : base(config, economy) { }
    }

    public sealed class EnemyCampSystem : CampSystem
    {
        public EnemyCampSystem(BuildingSystem buildings, MatchConfigSnapshot config = null) : base(buildings, config) { }
    }

    public sealed class EnemyTrainingSystem : TrainingSystem
    {
        private readonly int _trainingTimeMultiplierMilli;
        public EnemyTrainingSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings, CampSystem camps)
            : base(config, economy, buildings, camps) { _trainingTimeMultiplierMilli = config.EnemyEconomy.TrainingTimeMultiplierMilli; }
        public EnemyTrainingSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings, CampSystem camps,
            IUnitResearchModifiers research) : base(config, economy, buildings, camps, research)
        { _trainingTimeMultiplierMilli = config.EnemyEconomy.TrainingTimeMultiplierMilli; }
        protected override int TrainingTimeMultiplierMilli => _trainingTimeMultiplierMilli;
        protected override int MinimumTrainingTicks => 10;
        protected override ZoneKind ReinforcementDeploymentZoneKind => ZoneKind.EnemyDeployment;
    }

    public enum CombatTargetKind { None, Unit, Gatherer, Tower, Boss, Wall }

    public sealed class CombatTargetSnapshot
    {
        public CombatTargetSnapshot(CombatTargetKind kind, int numericId, string key)
        { Kind = kind; NumericId = numericId; Key = key ?? string.Empty; }

        public CombatTargetKind Kind { get; }
        public int NumericId { get; }
        public string Key { get; }
        public bool HasTarget => Kind != CombatTargetKind.None;
    }

    public sealed class CombatUnitSnapshot
    {
        public CombatUnitSnapshot(int id, MatchFaction faction, UnitId unitId, int lane, int x, int y, int health, int maxHealth,
            RouteId routeId = default, int? spawnX = null, int? spawnY = null, int attackRevision = 0, int damageRevision = 0,
            int lockedTargetId = 0, CombatTargetKind lockedTargetKind = CombatTargetKind.None, string lockedTargetKey = null,
            CombatUnitState state = CombatUnitState.Advancing)
        {
            Id = id; Faction = faction; UnitId = unitId; Lane = lane; X = x; Y = y;
            SpawnX = spawnX ?? x; SpawnY = spawnY ?? y; Health = health; MaxHealth = maxHealth; RouteId = routeId;
            AttackRevision = attackRevision; DamageRevision = damageRevision; LockedTargetId = lockedTargetId;
            LockedTargetKind = lockedTargetKind == CombatTargetKind.None && lockedTargetId != 0
                ? CombatTargetKind.Unit : lockedTargetKind;
            LockedTargetKey = lockedTargetKey ?? (LockedTargetKind == CombatTargetKind.Unit && lockedTargetId != 0
                ? $"unit:{lockedTargetId}" : string.Empty);
            Target = new CombatTargetSnapshot(LockedTargetKind, lockedTargetId, LockedTargetKey);
            State = state;
        }
        public int Id { get; }
        public MatchFaction Faction { get; }
        public UnitId UnitId { get; }
        public int Lane { get; }
        public int X { get; }
        public int Y { get; }
        public int SpawnX { get; }
        public int SpawnY { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        
        public int AttackRevision { get; }
        public int DamageRevision { get; }
        public int LockedTargetId { get; }
        public CombatTargetKind LockedTargetKind { get; }
        public string LockedTargetKey { get; }
        public CombatTargetSnapshot Target { get; }
        public CombatUnitState State { get; }
        public RouteId RouteId { get; }
    }

    public sealed class WallSnapshot
    {
        public WallSnapshot(MatchFaction faction, int health, int maxHealth) { Faction = faction; Health = health; MaxHealth = maxHealth; }
        public MatchFaction Faction { get; }
        public int Health { get; }
        public int MaxHealth { get; }
    }

    public sealed class UnitCombatCountSnapshot
    {
        public UnitCombatCountSnapshot(MatchFaction faction, UnitId unitId, int spawned, int casualties)
        { Faction = faction; UnitId = unitId; Spawned = spawned; Casualties = casualties; }
        public MatchFaction Faction { get; }
        public UnitId UnitId { get; }
        public int Spawned { get; }
        public int Casualties { get; }
    }

    public sealed class WallDamageSourceSnapshot
    {
        public WallDamageSourceSnapshot(MatchFaction attacker, UnitId unitId, int damage)
        { Attacker = attacker; UnitId = unitId; Damage = damage; }
        public MatchFaction Attacker { get; }
        public UnitId UnitId { get; }
        public int Damage { get; }
    }

    public enum CombatProjectileTargetKind { Unit, Gatherer, Boss, Tower, Wall }

    public sealed class CombatProjectileSnapshot
    {
        public CombatProjectileSnapshot(int id, MatchFaction faction, int x, int y, int targetX, int targetY,
            CombatProjectileTargetKind targetKind, int? originX = null, int? originY = null, int flightProgressMilli = 0,
            UnitProjectileKind projectileKind = UnitProjectileKind.Arrow, ResourceKey presentationKey = default,
            int sourceUnitHandle = 0, UnitId sourceUnitId = default)
        {
            Id = id; Faction = faction; X = x; Y = y; TargetX = targetX; TargetY = targetY; TargetKind = targetKind;
            OriginX = originX ?? x; OriginY = originY ?? y; FlightProgressMilli = Math.Clamp(flightProgressMilli, 0, 1000);
            ProjectileKind = projectileKind; PresentationKey = presentationKey;
            SourceUnitHandle = sourceUnitHandle; SourceUnitId = sourceUnitId;
        }
        public int Id { get; }
        public MatchFaction Faction { get; }
        public int X { get; }
        public int Y { get; }
        public int TargetX { get; }
        public int TargetY { get; }
        public CombatProjectileTargetKind TargetKind { get; }
        public int OriginX { get; }
        public int OriginY { get; }
        public int FlightProgressMilli { get; }
        public UnitProjectileKind ProjectileKind { get; }
        public ResourceKey PresentationKey { get; }
        public int SourceUnitHandle { get; }
        public UnitId SourceUnitId { get; }
    }

    public readonly struct GathererThreatIncident
    {
        public GathererThreatIncident(int sequence, int tick, int attackerHandle, UnitId attackerUnitId,
            int gathererId, GathererSourceId sourceId, RouteId routeId, int x, int y, int damage,
            bool wasKilled, ResourceId lostResourceId, int lostCarriedAmount)
        {
            Sequence = sequence; Tick = tick; AttackerHandle = attackerHandle; AttackerUnitId = attackerUnitId;
            GathererId = gathererId; SourceId = sourceId; RouteId = routeId; X = x; Y = y;
            Damage = damage; WasKilled = wasKilled; LostResourceId = lostResourceId;
            LostCarriedAmount = lostCarriedAmount;
        }

        public int Sequence { get; }
        public int Tick { get; }
        public int AttackerHandle { get; }
        public UnitId AttackerUnitId { get; }
        public int GathererId { get; }
        public GathererSourceId SourceId { get; }
        public RouteId RouteId { get; }
        public int X { get; }
        public int Y { get; }
        public int Damage { get; }
        public bool WasKilled { get; }
        public ResourceId LostResourceId { get; }
        public int LostCarriedAmount { get; }
    }

    public sealed class CombatSystem : GameSystemBase, IFixedMatchSimulation
    {
        private const int SeparationPadding = 12;

        private sealed class UnitState
        {
            public int Id; public MatchFaction Faction; public MatchUnitConfig Config; public int Lane; public RouteId RouteId;
            public int X; public int Y; public int SpawnX; public int SpawnY; public int Health; public int MaxHealth; public int AttackDamage;
            public int MovePerTick; public int AttackRange; public int Cooldown; public int AttackRevision; public int DamageRevision;
            public CombatTargetId LockedTarget;
            public CombatUnitState State = CombatUnitState.Advancing;
            public long PreviousTargetDistanceSquared = long.MaxValue;
            public int StalledTicks;
            public CombatTargetId SuppressedTarget;
            public int SuppressedUntilTick;
            public int MoveRemainderX;
            public int MoveRemainderY;
        }

        private sealed class ProjectileState
        {
            public int Id; public MatchFaction Faction; public int X; public int Y; public int OriginX; public int OriginY;
            public int Speed; public int Damage; public int TravelledDistance; public int FlightProgressMilli;
            public CombatTargetId Target;
            public CombatTargetId Source;
            public UnitId SourceUnitType;
            public UnitProjectileKind Kind;
            public ResourceKey PresentationKey;
            public int ExplosionRadius;
            public int ExplosionSecondaryDamageMilli;
            public int LastTargetX;
            public int LastTargetY;
        }

        private readonly struct DamageSourceReference
        {
            public DamageSourceReference(CombatTargetKind kind, MatchFaction faction, int numericId)
            { Kind = kind; Faction = faction; NumericId = numericId; }

            public CombatTargetKind Kind { get; }
            public MatchFaction Faction { get; }
            public int NumericId { get; }
        }

        private readonly struct IndexedUnit
        {
            public IndexedUnit(UnitState unit) { Unit = unit; X = unit.X; Y = unit.Y; }
            public UnitState Unit { get; }
            public int X { get; }
            public int Y { get; }
        }

        private sealed class LaneSpatialIndex
        {
            private readonly Dictionary<(MatchFaction Faction, int Lane), IndexedUnit[]> _lanes = new();
            private readonly Dictionary<int, IndexedUnit> _byId = new();
            private readonly int _maximumCollisionRadius;

            public LaneSpatialIndex(IEnumerable<UnitState> units, int maximumCollisionRadius)
            {
                _maximumCollisionRadius = maximumCollisionRadius;
                var grouped = new Dictionary<(MatchFaction, int), List<IndexedUnit>>();
                foreach (var unit in units)
                {
                    var indexed = new IndexedUnit(unit);
                    _byId.Add(unit.Id, indexed);
                    var key = (unit.Faction, unit.Lane);
                    if (!grouped.TryGetValue(key, out var values))
                    { values = new List<IndexedUnit>(); grouped.Add(key, values); }
                    values.Add(indexed);
                }
                foreach (var pair in grouped)
                {
                    pair.Value.Sort((left, right) =>
                    {
                        var x = left.X.CompareTo(right.X);
                        return x != 0 ? x : left.Unit.Id.CompareTo(right.Unit.Id);
                    });
                    _lanes.Add(pair.Key, pair.Value.ToArray());
                }
            }

            public IndexedUnit[] GetLane(MatchFaction faction, int lane) =>
                _lanes.TryGetValue((faction, lane), out var values) ? values : Array.Empty<IndexedUnit>();

            public void GetSeparation(UnitState source, out int xMilli, out int yMilli)
            {
                xMilli = 0; yMilli = 0;
                if (!_byId.TryGetValue(source.Id, out var origin)) return;
                var allies = GetLane(source.Faction, source.Lane);
                var searchRadius = source.Config.CollisionRadius + _maximumCollisionRadius + SeparationPadding;
                var index = LowerBound(allies, origin.X - searchRadius);
                for (; index < allies.Length && allies[index].X <= origin.X + searchRadius; index++)
                {
                    var other = allies[index];
                    if (other.Unit.Id == source.Id) continue;
                    var deltaX = origin.X - other.X;
                    var deltaY = origin.Y - other.Y;
                    var distance = Math.Abs(deltaX) + Math.Abs(deltaY);
                    var combinedRadius = source.Config.CollisionRadius + other.Unit.Config.CollisionRadius;
                    var outerRadius = combinedRadius + Math.Max(SeparationPadding, combinedRadius / 2);
                    if (distance >= outerRadius) continue;
                    var strengthMilli = (outerRadius - distance) * 1000 / Math.Max(1, outerRadius);
                    var directionX = deltaX == 0 ? (source.Id < other.Unit.Id ? -1 : 1) : Math.Sign(deltaX);
                    var directionY = deltaY == 0 ? (source.Id < other.Unit.Id ? -1 : 1) : Math.Sign(deltaY);
                    xMilli += directionX * strengthMilli;
                    yMilli += directionY * strengthMilli;
                }
                xMilli = Math.Clamp(xMilli, -1000, 1000);
                yMilli = Math.Clamp(yMilli, -1000, 1000);
            }

            private static int LowerBound(IReadOnlyList<IndexedUnit> values, int x)
            {
                var low = 0; var high = values.Count;
                while (low < high)
                {
                    var middle = low + (high - low) / 2;
                    if (values[middle].X < x) low = middle + 1;
                    else high = middle;
                }
                return low;
            }
        }

        private readonly MatchConfigSnapshot _config;
        private readonly TrainingSystem _playerTraining;
        private readonly TrainingSystem _enemyTraining;
        private readonly TowerConstructionSystem _playerConstruction;
        private readonly TowerConstructionSystem _enemyConstruction;
        private readonly IUnitResearchModifiers _playerResearch;
        private readonly IUnitResearchModifiers _enemyResearch;
        private readonly BossSystem _boss;
        private readonly GathererSystem _playerGatherers;
        private readonly GathererSystem _enemyGatherers;
        private readonly Dictionary<UnitId, MatchUnitConfig> _units;
        private readonly List<UnitState> _active = new();
        private readonly Dictionary<int, UnitState> _activeById = new();
        private readonly List<ProjectileState> _projectiles = new();
        private readonly List<CombatTargetCandidate> _targetCandidates = new();
        private readonly CombatTargetIndex _targetIndex = new();
        private readonly Dictionary<(MatchFaction Faction, int TowerId), int> _towerCooldowns = new();
        private readonly Dictionary<(MatchFaction Faction, UnitId UnitId), int> _spawned = new();
        private readonly Dictionary<(MatchFaction Faction, UnitId UnitId), int> _casualties = new();
        private readonly Dictionary<(MatchFaction Faction, UnitId UnitId), int> _wallDamage = new();
        private readonly List<GathererThreatIncident> _gathererThreatIncidents = new();
        private readonly int[] _laneMinimumY = new int[3];
        private readonly int[] _laneMaximumY = new int[3];
        private readonly int _maximumCollisionRadius;
        private readonly int _movementMinimumX;
        private readonly int _movementMaximumX;
        private readonly int _movementMinimumY;
        private readonly int _movementMaximumY;
        private int _nextId = 1;
        private int _nextProjectileId = 1;
        private int _nextThreatIncidentSequence = 1;
        private int _currentTick;
        private int _playerWallHealth;
        private int _enemyWallHealth;

        public CombatSystem(MatchConfigSnapshot config, TrainingSystem playerTraining, TrainingSystem enemyTraining)
            : this(config, playerTraining, enemyTraining, null, null) { }
        public CombatSystem(MatchConfigSnapshot config, TrainingSystem playerTraining, TrainingSystem enemyTraining,
            TowerConstructionSystem playerConstruction, TowerConstructionSystem enemyConstruction)
            : this(config, playerTraining, enemyTraining, playerConstruction, enemyConstruction, null, null) { }
        public CombatSystem(MatchConfigSnapshot config, TrainingSystem playerTraining, TrainingSystem enemyTraining,
            TowerConstructionSystem playerConstruction, TowerConstructionSystem enemyConstruction,
            IUnitResearchModifiers playerResearch, IUnitResearchModifiers enemyResearch)
            : this(config, playerTraining, enemyTraining, playerConstruction, enemyConstruction, playerResearch, enemyResearch, null) { }
        public CombatSystem(MatchConfigSnapshot config, TrainingSystem playerTraining, TrainingSystem enemyTraining,
            TowerConstructionSystem playerConstruction, TowerConstructionSystem enemyConstruction,
            IUnitResearchModifiers playerResearch, IUnitResearchModifiers enemyResearch, BossSystem boss)
            : this(config, playerTraining, enemyTraining, playerConstruction, enemyConstruction,
                playerResearch, enemyResearch, boss, null, null) { }
        public CombatSystem(MatchConfigSnapshot config, TrainingSystem playerTraining, TrainingSystem enemyTraining,
            TowerConstructionSystem playerConstruction, TowerConstructionSystem enemyConstruction,
            IUnitResearchModifiers playerResearch, IUnitResearchModifiers enemyResearch, BossSystem boss,
            GathererSystem playerGatherers, GathererSystem enemyGatherers)
            : base(SystemLifetime.Scene)
        {
            _config = config; _playerTraining = playerTraining; _enemyTraining = enemyTraining;
            _playerConstruction = playerConstruction; _enemyConstruction = enemyConstruction;
            _playerResearch = playerResearch; _enemyResearch = enemyResearch; _boss = boss;
            _playerGatherers = playerGatherers; _enemyGatherers = enemyGatherers;
            _units = config.Combat.Units.ToDictionary(value => value.Id);
            _maximumCollisionRadius = Math.Max(1, config.Combat.Units.Select(value => value.CollisionRadius).DefaultIfEmpty(1).Max());
            (_movementMinimumX, _movementMaximumX) = ResolveWallSurfaceBounds(config);
            ConfigureLaneBounds(config.BattlefieldLayout);
            _movementMinimumY = _laneMinimumY.Min();
            _movementMaximumY = _laneMaximumY.Max();
        }

        public event Action Changed;
        public event Action<bool> MatchEnded;
        public event Action<UnitHitAudioEvent> UnitHit;
        public bool HasEnded { get; private set; }
        public bool PlayerVictory { get; private set; }
        public IReadOnlyList<GathererThreatIncident> GetGathererThreatIncidents(int afterSequence = 0)
        {
            if (_gathererThreatIncidents.Count == 0) return Array.Empty<GathererThreatIncident>();
            if (afterSequence <= 0) return _gathererThreatIncidents.ToArray();
            return _gathererThreatIncidents.Where(value => value.Sequence > afterSequence).ToArray();
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _playerWallHealth = _config.Combat.PlayerWall.MaxHealth;
            _enemyWallHealth = _config.Combat.EnemyWall.MaxHealth;
            _playerTraining.UnitDeploymentCompleted += SpawnPlayer;
            _enemyTraining.UnitDeploymentCompleted += SpawnEnemy;
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _playerTraining.UnitDeploymentCompleted -= SpawnPlayer;
            _enemyTraining.UnitDeploymentCompleted -= SpawnEnemy;
            _active.Clear(); _activeById.Clear(); _projectiles.Clear(); _targetCandidates.Clear(); _targetIndex.Reset();
            _towerCooldowns.Clear(); _spawned.Clear(); _casualties.Clear(); _wallDamage.Clear();
            _gathererThreatIncidents.Clear();
            return Task.CompletedTask;
        }

        public IReadOnlyList<CombatUnitSnapshot> GetUnits() => _active.Where(value => value.Health > 0).OrderBy(value => value.Id)
            .Select(value => new CombatUnitSnapshot(value.Id, value.Faction, value.Config.Id, value.Lane, value.X, value.Y,
                value.Health, value.MaxHealth, value.RouteId, value.SpawnX, value.SpawnY, value.AttackRevision,
                value.DamageRevision, value.LockedTarget.Kind == CombatTargetKind.Unit ? value.LockedTarget.NumericId : 0,
                value.LockedTarget.Kind, value.LockedTarget.ToCompatibilityKey(), value.State)).ToArray();
        public IReadOnlyList<CombatProjectileSnapshot> GetProjectiles()
        {
            var targets = BuildTargetIndex();
            return _projectiles.OrderBy(value => value.Id)
            .Select(value =>
            {
                TryResolveProjectileTarget(value, targets, out var x, out var y);
                var sourceUnitHandle = value.Source.Kind == CombatTargetKind.Unit ? value.Source.NumericId : 0;
                return new CombatProjectileSnapshot(value.Id, value.Faction, value.X, value.Y, x, y,
                    ToProjectileTargetKind(value.Target.Kind), value.OriginX, value.OriginY, value.FlightProgressMilli,
                    value.Kind, value.PresentationKey, sourceUnitHandle, value.SourceUnitType);
            }).ToArray();
        }
        public IReadOnlyList<WallSnapshot> GetWalls() => new[]
        {
            new WallSnapshot(MatchFaction.Player, _playerWallHealth, _config.Combat.PlayerWall.MaxHealth),
            new WallSnapshot(MatchFaction.Enemy, _enemyWallHealth, _config.Combat.EnemyWall.MaxHealth)
        };
        public IReadOnlyList<UnitCombatCountSnapshot> GetCombatCounts() => _spawned.Keys.Union(_casualties.Keys)
            .OrderBy(value => value.Faction).ThenBy(value => value.UnitId.Value, StringComparer.Ordinal)
            .Select(value => new UnitCombatCountSnapshot(value.Faction, value.UnitId,
                _spawned.GetValueOrDefault(value), _casualties.GetValueOrDefault(value))).ToArray();
        public IReadOnlyList<WallDamageSourceSnapshot> GetWallDamageSources() => _wallDamage
            .OrderBy(value => value.Key.Faction).ThenByDescending(value => value.Value)
            .ThenBy(value => value.Key.UnitId.Value, StringComparer.Ordinal)
            .Select(value => new WallDamageSourceSnapshot(value.Key.Faction, value.Key.UnitId, value.Value)).ToArray();

        public void SimulateTick(int tick)
        {
            if (HasEnded) return;
            _currentTick = tick;
            var targets = BuildTargetIndex();
            var changed = SimulateProjectiles(targets);
            if (RemoveAndRecordCasualties() > 0) changed = true;
            targets = BuildTargetIndex();
            var spatial = new LaneSpatialIndex(_active.Where(value => value.Health > 0), _maximumCollisionRadius);
            foreach (var unit in _active.Where(value => value.Health > 0).OrderBy(value => value.Id).ToArray())
            {
                if (unit.Health <= 0) continue;
                if (unit.Cooldown > 0) unit.Cooldown--;
                var structuresOnly = unit.Config.TargetPriority == UnitTargetPriority.StructuresOnly;
                if (TryHandleLockedTarget(unit, targets, spatial, ref changed)) continue;

                targets.QueryOpponents(unit.Faction, unit.X, unit.Y, unit.Config.AcquireRadius, _targetCandidates);
                if (IsWallAlive(unit.Faction) && WallSurfaceGap(unit) <= unit.AttackRange)
                    _targetCandidates.Add(CreateWallTarget(unit));
                if (unit.SuppressedUntilTick > tick)
                    _targetCandidates.RemoveAll(value => value.Id == unit.SuppressedTarget);
                if (CombatTargetSelector.TrySelect(unit.Config.TargetPriority, structuresOnly, unit.X, unit.Y,
                        WallCenterAttackRange(unit), _targetCandidates, out var target))
                {
                    LockTarget(unit, target.Id);
                    HandleTarget(unit, target, spatial, ref changed);
                    continue;
                }

                unit.State = CombatUnitState.Advancing;
                var wallTarget = CreateWallTarget(unit);
                changed |= MoveTowards(unit, wallTarget.X, unit.Y, WallCenterAttackRange(unit), spatial,
                    trackProgress: false);
                if (!IsWallAlive(unit.Faction) || WallSurfaceGap(unit) > unit.AttackRange) continue;
                wallTarget = CreateWallTarget(unit);
                LockTarget(unit, wallTarget.Id);
                HandleTarget(unit, wallTarget, spatial, ref changed);
            }
            changed |= ResolveFriendlyPenetrations();
            changed |= SimulateTowerAttacks(BuildTargetIndex());
            if (RemoveAndRecordCasualties() > 0) changed = true;
            if (_playerWallHealth <= 0 || _enemyWallHealth <= 0)
            {
                HasEnded = true; PlayerVictory = _enemyWallHealth <= 0 && _playerWallHealth > 0;
                MatchEnded?.Invoke(PlayerVictory); changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        private bool TryAttackUnit(UnitState source, UnitState target)
        {
            if (source.Cooldown > 0 || target.Health <= 0) return false;
            if (source.Config.ProjectileSpeedPerTick > 0)
                SpawnProjectile(source, new CombatTargetId(CombatTargetKind.Unit, target.Id), source.AttackDamage);
            else
                ApplyDamageToUnit(target, source.AttackDamage,
                    new DamageSourceReference(CombatTargetKind.Unit, source.Faction, source.Id));
            CommitAttack(source);
            return true;
        }

        private bool TryHandleLockedTarget(UnitState source, CombatTargetIndex targets, LaneSpatialIndex spatial, ref bool changed)
        {
            if (source.LockedTarget.IsNone) return false;
            if (TryResolveTarget(source, source.LockedTarget, targets, out var target))
            {
                var distanceSquared = CombatTargetIndex.DistanceSquared(source.X, source.Y, target.X, target.Y);
                var state = CombatUnitStateMachine.Resolve(true, distanceSquared,
                    AttackRangeFor(source, target.Id.Kind), source.Config.ChaseRadius);
                if (!source.LockedTarget.IsDynamic || state != CombatUnitState.Advancing)
                {
                    HandleTarget(source, target, distanceSquared, spatial, ref changed);
                    return true;
                }
            }
            if (source.LockedTarget.IsDynamic)
            {
                source.SuppressedTarget = source.LockedTarget;
                source.SuppressedUntilTick = _currentTick + 30;
            }
            ClearTarget(source);
            return false;
        }

        private void HandleTarget(UnitState source, CombatTargetCandidate target, LaneSpatialIndex spatial, ref bool changed)
        {
            HandleTarget(source, target,
                CombatTargetIndex.DistanceSquared(source.X, source.Y, target.X, target.Y), spatial, ref changed);
        }

        private void HandleTarget(UnitState source, CombatTargetCandidate target, long distanceSquared,
            LaneSpatialIndex spatial, ref bool changed)
        {
            var chaseRadius = target.Id.IsDynamic ? source.Config.ChaseRadius : int.MaxValue;
            var attackRange = AttackRangeFor(source, target.Id.Kind);
            var state = CombatUnitStateMachine.Resolve(true, distanceSquared, attackRange, chaseRadius);
            if (state == CombatUnitState.Advancing)
            {
                ClearTarget(source);
                return;
            }

            source.State = state;
            if (state == CombatUnitState.Pursuing)
            {
                changed |= MoveTowards(source, target.X, target.Y, attackRange, spatial, trackProgress: true);
                return;
            }

            source.StalledTicks = 0;
            source.PreviousTargetDistanceSquared = long.MaxValue;
            switch (target.Id.Kind)
            {
                case CombatTargetKind.Unit:
                    if (_activeById.TryGetValue(target.Id.NumericId, out var unit)) changed |= TryAttackUnit(source, unit);
                    break;
                case CombatTargetKind.Gatherer:
                    changed |= TryAttackGatherer(source, target.Id.NumericId);
                    break;
                case CombatTargetKind.Tower:
                    changed |= TryAttackTower(source, target.Id.NumericId);
                    break;
                case CombatTargetKind.Boss:
                    changed |= TryAttackBoss(source, target.Id.StableId);
                    break;
                case CombatTargetKind.Wall:
                    changed |= TryAttackWall(source);
                    break;
            }
        }

        private static void LockTarget(UnitState source, CombatTargetId target)
        {
            source.LockedTarget = target;
            source.State = CombatUnitState.Pursuing;
            source.StalledTicks = 0;
            source.PreviousTargetDistanceSquared = long.MaxValue;
        }

        private static void ClearTarget(UnitState source)
        {
            source.LockedTarget = CombatTargetId.None;
            source.State = CombatUnitState.Advancing;
            source.StalledTicks = 0;
            source.PreviousTargetDistanceSquared = long.MaxValue;
        }

        private bool TryAttackGatherer(UnitState source, int targetId)
        {
            if (source.Cooldown > 0) return false;
            if (source.Config.ProjectileSpeedPerTick > 0)
                SpawnProjectile(source, new CombatTargetId(CombatTargetKind.Gatherer, targetId), source.AttackDamage);
            else if (!TryDamageGatherer(source.Faction, source.Id, source.Config.Id, targetId, source.AttackDamage)) return false;
            CommitAttack(source);
            return true;
        }

        private bool TryAttackTower(UnitState source, int targetId)
        {
            if (source.Cooldown > 0) return false;
            if (source.Config.ProjectileSpeedPerTick > 0)
                SpawnProjectile(source, new CombatTargetId(CombatTargetKind.Tower, targetId), source.AttackDamage);
            else if (OpposingConstruction(source.Faction)?.TryDamageTower(targetId, source.AttackDamage) != true) return false;
            CommitAttack(source);
            return true;
        }

        private bool TryAttackBoss(UnitState source, string targetId)
        {
            if (source.Cooldown > 0 || string.IsNullOrEmpty(targetId)) return false;
            if (source.Config.ProjectileSpeedPerTick > 0)
                SpawnProjectile(source, new CombatTargetId(CombatTargetKind.Boss, 0, targetId), source.AttackDamage);
            else if (_boss?.TryDamage(targetId, source.Faction, source.AttackDamage, source.Id) != true) return false;
            CommitAttack(source);
            return true;
        }

        private bool TryAttackWall(UnitState source)
        {
            if (source.Cooldown > 0) return false;
            var damage = Math.Max(1, source.AttackDamage * source.Config.WallDamageMultiplierMilli / 1000);
            if (source.Config.ProjectileSpeedPerTick > 0)
            {
                var wallId = source.Faction == MatchFaction.Player ? _config.Combat.EnemyWall.Id : _config.Combat.PlayerWall.Id;
                SpawnProjectile(source, new CombatTargetId(CombatTargetKind.Wall, 0, wallId), damage);
            }
            else ApplyWallDamage(source.Faction, source.Config.Id, damage);
            CommitAttack(source);
            return true;
        }

        private void CommitAttack(UnitState source)
        {
            source.AttackRevision++;
            source.Cooldown = Math.Max(1, source.Config.AttackIntervalTicks);
        }

        private void SpawnProjectile(UnitState source, CombatTargetId target, int damage)
        {
            ResolveInitialProjectileTarget(source.Faction, target, source.X, source.Y, out var targetX, out var targetY);
            _projectiles.Add(new ProjectileState
            {
                Id = _nextProjectileId++, Faction = source.Faction, X = source.X, Y = source.Y,
                OriginX = source.X, OriginY = source.Y,
                Speed = Math.Max(1, source.Config.ProjectileSpeedPerTick), Damage = Math.Max(1, damage),
                Target = target, Source = new CombatTargetId(CombatTargetKind.Unit, source.Id), SourceUnitType = source.Config.Id,
                Kind = source.Config.ProjectileKind == UnitProjectileKind.None ? UnitProjectileKind.Arrow : source.Config.ProjectileKind,
                PresentationKey = source.Config.ProjectilePresentationKey,
                ExplosionRadius = source.Config.ExplosionRadius,
                ExplosionSecondaryDamageMilli = source.Config.ExplosionSecondaryDamageMilli,
                LastTargetX = targetX, LastTargetY = targetY
            });
        }

        private void SpawnTowerProjectile(TowerSnapshot tower, UnitState target)
        {
            _projectiles.Add(new ProjectileState
            {
                Id = _nextProjectileId++, Faction = tower.Faction, X = tower.X, Y = tower.Y,
                OriginX = tower.X, OriginY = tower.Y,
                Speed = Math.Max(1, _config.Construction.ProjectileSpeedPerTick),
                Damage = Math.Max(1, _config.Construction.AttackDamage),
                Target = new CombatTargetId(CombatTargetKind.Unit, target.Id),
                Source = new CombatTargetId(CombatTargetKind.Tower, tower.Id),
                Kind = UnitProjectileKind.Arrow,
                PresentationKey = new ResourceKey("sprite.projectile.arrow"),
                LastTargetX = target.X,
                LastTargetY = target.Y
            });
        }

        private bool SimulateProjectiles(CombatTargetIndex targets)
        {
            var changed = false;
            foreach (var projectile in _projectiles.OrderBy(value => value.Id).ToArray())
            {
                if (!TryResolveProjectileTarget(projectile, targets, out var targetX, out var targetY))
                { _projectiles.Remove(projectile); changed = true; continue; }
                var distance = Math.Abs(targetX - projectile.X) + Math.Abs(targetY - projectile.Y);
                if (distance <= projectile.Speed)
                {
                    ApplyProjectileImpact(projectile, targetX, targetY);
                    _projectiles.Remove(projectile);
                    changed = true;
                    continue;
                }
                var deltaX = targetX - projectile.X;
                var deltaY = targetY - projectile.Y;
                var previousX = projectile.X;
                var previousY = projectile.Y;
                projectile.X += deltaX * projectile.Speed / Math.Max(1, distance);
                projectile.Y += deltaY * projectile.Speed / Math.Max(1, distance);
                projectile.TravelledDistance += Math.Abs(projectile.X - previousX) + Math.Abs(projectile.Y - previousY);
                var remaining = Math.Abs(targetX - projectile.X) + Math.Abs(targetY - projectile.Y);
                var progress = projectile.TravelledDistance * 1000 /
                    Math.Max(1, projectile.TravelledDistance + remaining);
                projectile.FlightProgressMilli = Math.Max(projectile.FlightProgressMilli, Math.Clamp(progress, 0, 999));
                if (projectile.X == targetX && projectile.Y == targetY)
                { ApplyProjectileImpact(projectile, targetX, targetY); _projectiles.Remove(projectile); }
                changed = true;
            }
            return changed;
        }

        private bool TryResolveProjectileTarget(ProjectileState projectile, CombatTargetIndex targets, out int x, out int y)
        {
            x = projectile.X; y = projectile.Y;
            if (projectile.Target.Kind == CombatTargetKind.Wall)
            {
                if (!IsWallAlive(projectile.Faction)) return false;
                x = projectile.LastTargetX;
                y = projectile.LastTargetY;
                return true;
            }
            if (projectile.Target.Kind == CombatTargetKind.Unit &&
                _activeById.TryGetValue(projectile.Target.NumericId, out var liveUnit) && liveUnit.Health > 0)
            {
                x = liveUnit.X;
                y = liveUnit.Y;
                projectile.LastTargetX = x;
                projectile.LastTargetY = y;
                return true;
            }
            if (!targets.TryGet(projectile.Target, Opponent(projectile.Faction), out var target))
            {
                if (projectile.ExplosionRadius <= 0) return false;
                x = projectile.LastTargetX;
                y = projectile.LastTargetY;
                return true;
            }
            x = target.X;
            y = target.Y;
            projectile.LastTargetX = x;
            projectile.LastTargetY = y;
            return true;
        }

        private void ResolveInitialProjectileTarget(MatchFaction faction, CombatTargetId targetId, int fallbackX, int fallbackY,
            out int x, out int y)
        {
            x = fallbackX; y = fallbackY;
            if (targetId.Kind == CombatTargetKind.Wall)
            {
                x = faction == MatchFaction.Player ? _movementMaximumX : _movementMinimumX;
                return;
            }
            var targets = BuildTargetIndex();
            if (targets.TryGet(targetId, Opponent(faction), out var target)) { x = target.X; y = target.Y; }
        }

        private void ApplyProjectileImpact(ProjectileState projectile, int impactX, int impactY)
        {
            ApplyProjectileDamage(projectile);
            if (projectile.ExplosionRadius <= 0 || projectile.ExplosionSecondaryDamageMilli <= 0) return;
            var secondaryDamage = Math.Max(1, projectile.Damage * projectile.ExplosionSecondaryDamageMilli / 1000);
            var radiusSquared = (long)projectile.ExplosionRadius * projectile.ExplosionRadius;
            foreach (var unit in _active.Where(value => value.Health > 0 && value.Faction != projectile.Faction)
                         .OrderBy(value => value.Id).ToArray())
            {
                if (projectile.Target.Kind == CombatTargetKind.Unit && projectile.Target.NumericId == unit.Id) continue;
                if (CombatTargetIndex.DistanceSquared(unit.X, unit.Y, impactX, impactY) > radiusSquared) continue;
                ApplyDamageToUnit(unit, secondaryDamage,
                    new DamageSourceReference(projectile.Source.Kind, projectile.Faction, projectile.Source.NumericId));
            }
            var gatherers = OpposingGatherers(projectile.Faction)?.GetSnapshot() ?? Array.Empty<GathererSnapshot>();
            foreach (var gatherer in gatherers.OrderBy(value => value.Id))
            {
                if (projectile.Target.Kind == CombatTargetKind.Gatherer && projectile.Target.NumericId == gatherer.Id) continue;
                if (CombatTargetIndex.DistanceSquared(gatherer.X, gatherer.Y, impactX, impactY) > radiusSquared) continue;
                TryDamageGatherer(projectile.Faction, projectile.Source.NumericId, projectile.SourceUnitType,
                    gatherer.Id, secondaryDamage);
            }
        }

        private void ApplyProjectileDamage(ProjectileState projectile)
        {
            switch (projectile.Target.Kind)
            {
                case CombatTargetKind.Unit:
                    if (_activeById.TryGetValue(projectile.Target.NumericId, out var target) && target.Health > 0)
                        ApplyDamageToUnit(target, projectile.Damage, new DamageSourceReference(
                            projectile.Source.Kind, projectile.Faction, projectile.Source.NumericId));
                    break;
                case CombatTargetKind.Gatherer:
                    TryDamageGatherer(projectile.Faction, projectile.Source.NumericId, projectile.SourceUnitType,
                        projectile.Target.NumericId, projectile.Damage);
                    break;
                case CombatTargetKind.Boss:
                    _boss?.TryDamage(projectile.Target.StableId, projectile.Faction, projectile.Damage, projectile.Source.NumericId);
                    break;
                case CombatTargetKind.Tower:
                    OpposingConstruction(projectile.Faction)?.TryDamageTower(projectile.Target.NumericId, projectile.Damage);
                    break;
                case CombatTargetKind.Wall:
                    ApplyWallDamage(projectile.Faction, projectile.SourceUnitType, projectile.Damage);
                    break;
            }
        }

        private bool TryDamageGatherer(MatchFaction attackerFaction, int attackerHandle, UnitId attackerUnitId,
            int gathererId, int damage)
        {
            var gatherers = OpposingGatherers(attackerFaction);
            var before = gatherers?.GetSnapshot().FirstOrDefault(value => value.Id == gathererId);
            if (before == null || !gatherers.TryDamage(gathererId, damage)) return false;
            UnitHit?.Invoke(new UnitHitAudioEvent(before.Faction, before.X, before.Y,
                Math.Max(1, damage) >= before.Health));
            if (attackerFaction != MatchFaction.Player || before.Faction != MatchFaction.Enemy) return true;

            var appliedDamage = Math.Min(Math.Max(0, damage), before.Health);
            var killed = appliedDamage >= before.Health;
            _gathererThreatIncidents.Add(new GathererThreatIncident(
                _nextThreatIncidentSequence++, _currentTick, attackerHandle, attackerUnitId,
                before.Id, before.SourceId, before.RouteId, before.X, before.Y, appliedDamage, killed,
                before.ResourceId, killed ? before.CarriedAmount : 0));
            if (_gathererThreatIncidents.Count > 512)
                _gathererThreatIncidents.RemoveRange(0, _gathererThreatIncidents.Count - 512);
            return true;
        }

        private void ApplyWallDamage(MatchFaction faction, UnitId sourceUnitId, int damage)
        {
            var applied = faction == MatchFaction.Player ? Math.Min(damage, _enemyWallHealth) : Math.Min(damage, _playerWallHealth);
            if (faction == MatchFaction.Player) _enemyWallHealth = Math.Max(0, _enemyWallHealth - damage);
            else _playerWallHealth = Math.Max(0, _playerWallHealth - damage);
            var damageKey = (faction, sourceUnitId);
            _wallDamage[damageKey] = _wallDamage.GetValueOrDefault(damageKey) + applied;
        }

        private GathererSystem OpposingGatherers(MatchFaction faction) =>
            faction == MatchFaction.Player ? _enemyGatherers : _playerGatherers;

        private TowerConstructionSystem OpposingConstruction(MatchFaction faction) =>
            faction == MatchFaction.Player ? _enemyConstruction : _playerConstruction;

        public int ApplyAreaDamage(MatchFaction targetFaction, int damage)
        {
            if (damage <= 0 || HasEnded) return 0;
            var hit = 0;
            foreach (var unit in _active.Where(value => value.Faction == targetFaction && value.Health > 0))
            {
                ApplyDamageToUnit(unit, damage, default);
                hit++;
            }
            if (hit > 0) { RemoveAndRecordCasualties(); Changed?.Invoke(); }
            return hit;
        }

        public bool TryDamageUnit(int unitId, int damage)
        {
            if (damage <= 0) return false;
            if (!_activeById.TryGetValue(unitId, out var unit) || unit.Health <= 0) return false;
            ApplyDamageToUnit(unit, damage, default); Changed?.Invoke(); return true;
        }

        private void ApplyDamageToUnit(UnitState target, int damage, DamageSourceReference source)
        {
            target.Health = Math.Max(0, target.Health - Math.Max(1, damage));
            target.DamageRevision++;
            UnitHit?.Invoke(new UnitHitAudioEvent(target.Faction, target.X, target.Y, target.Health == 0));
            if (target.Health > 0) TryLockRetaliationTarget(target, source);
        }

        private void TryLockRetaliationTarget(UnitState target, DamageSourceReference source)
        {
            if (source.Kind is not (CombatTargetKind.Unit or CombatTargetKind.Tower) ||
                source.Faction == target.Faction || HasValidDynamicLock(target)) return;

            var chaseRadius = target.Config.ChaseRadius;
            switch (source.Kind)
            {
                case CombatTargetKind.Unit:
                    if (target.Config.TargetPriority == UnitTargetPriority.StructuresOnly) return;
                    _activeById.TryGetValue(source.NumericId, out var attacker);
                    if (attacker == null || attacker.Faction != source.Faction || attacker.Faction == target.Faction ||
                        attacker.Health <= 0 || CombatTargetIndex.DistanceSquared(target.X, target.Y, attacker.X, attacker.Y) >
                        (long)chaseRadius * chaseRadius) return;
                    LockTarget(target, new CombatTargetId(CombatTargetKind.Unit, attacker.Id));
                    break;
                case CombatTargetKind.Tower:
                    var towerId = new CombatTargetId(CombatTargetKind.Tower, source.NumericId);
                    if (!_targetIndex.TryGet(towerId, source.Faction, out var tower) ||
                        CombatTargetIndex.DistanceSquared(target.X, target.Y, tower.X, tower.Y) >
                        (long)chaseRadius * chaseRadius) return;
                    LockTarget(target, towerId);
                    break;
            }
        }

        private bool HasValidDynamicLock(UnitState target)
        {
            var chaseRadius = target.Config.ChaseRadius;
            switch (target.LockedTarget.Kind)
            {
                case CombatTargetKind.Unit:
                    return _activeById.TryGetValue(target.LockedTarget.NumericId, out var unit) && unit.Health > 0 &&
                           unit.Faction != target.Faction && CombatTargetIndex.DistanceSquared(target.X, target.Y, unit.X, unit.Y) <=
                           (long)chaseRadius * chaseRadius;
                case CombatTargetKind.Gatherer:
                    return _targetIndex.TryGet(target.LockedTarget, Opponent(target.Faction), out var gatherer) &&
                           CombatTargetIndex.DistanceSquared(target.X, target.Y, gatherer.X, gatherer.Y) <=
                           (long)chaseRadius * chaseRadius;
                case CombatTargetKind.Tower:
                    return _targetIndex.TryGet(target.LockedTarget, Opponent(target.Faction), out var tower) &&
                           CombatTargetIndex.DistanceSquared(target.X, target.Y, tower.X, tower.Y) <=
                           (long)chaseRadius * chaseRadius;
                case CombatTargetKind.Boss:
                    return _targetIndex.TryGet(target.LockedTarget, Opponent(target.Faction), out var boss) &&
                           CombatTargetIndex.DistanceSquared(target.X, target.Y, boss.X, boss.Y) <=
                           (long)chaseRadius * chaseRadius;
                default:
                    return false;
            }
        }

        public int ApplyBossMeteor(int x, int y, int radius, int damage, int knockbackDistance)
        {
            if (radius <= 0 || damage <= 0 || HasEnded) return 0;
            var hit = 0;
            foreach (var unit in _active.Where(value => value.Health > 0)
                         .OrderBy(value => value.Id).ToArray())
            {
                var deltaX = unit.X - x;
                var deltaY = unit.Y - y;
                var distance = Math.Abs(deltaX) + Math.Abs(deltaY);
                if (distance > radius) continue;
                unit.Health = Math.Max(0, unit.Health - damage);
                unit.DamageRevision++;
                if (unit.Health > 0 && knockbackDistance > 0)
                {
                    var directionX = deltaX == 0 ? (unit.Faction == MatchFaction.Player ? -1 : 1) : Math.Sign(deltaX);
                    var directionY = deltaY == 0 ? (unit.Id % 2 == 0 ? -1 : 1) : Math.Sign(deltaY);
                    var magnitude = Math.Max(1, Math.Abs(deltaX) + Math.Abs(deltaY));
                    unit.X = ClampMovementX(unit, unit.X + directionX * Math.Max(1, Math.Abs(deltaX) * knockbackDistance / magnitude));
                    unit.Y = ClampLaneY(unit, unit.Y + directionY * Math.Max(1, Math.Abs(deltaY) * knockbackDistance / magnitude));
                }
                hit++;
            }
            if (hit > 0) { RemoveAndRecordCasualties(); Changed?.Invoke(); }
            return hit;
        }

        private void SpawnPlayer(UnitDeployment deployment) => Spawn(MatchFaction.Player, deployment);
        private void SpawnEnemy(UnitDeployment deployment) => Spawn(MatchFaction.Enemy, deployment);
        private void Spawn(MatchFaction faction, UnitDeployment deployment)
        {
            var unitId = deployment.UnitId;
            var point = deployment.Point;
            if (!_units.TryGetValue(unitId, out var config) || !config.CanAttack || HasEnded) return;
            var lane = Math.Clamp(point.Lane, 0, 2);
            var research = faction == MatchFaction.Player ? _playerResearch : _enemyResearch;
            var maxHealth = ApplyMultiplier(config.MaxHealth, research, unitId, "health");
            var spawnX = point.HasWorldPosition ? point.X : faction == MatchFaction.Player ? 570 + point.Cell * 20 : 1770 - point.Cell * 20;
            var spawnY = point.HasWorldPosition ? point.Y : 270 + lane * 270;
            spawnX = ClampMovementX(config, spawnX);
            var unit = new UnitState { Id = _nextId++, Faction = faction, Config = config, Lane = lane, RouteId = deployment.RouteId,
                X = spawnX, Y = spawnY, SpawnX = spawnX, SpawnY = spawnY, Health = maxHealth, MaxHealth = maxHealth,
                AttackDamage = ApplyMultiplier(config.AttackDamage, research, unitId, "damage"),
                MovePerTick = ApplyMultiplier(config.MovePerTick, research, unitId, "speed"),
                AttackRange = ApplyMultiplier(config.AttackRange, research, unitId, "range") };
            _active.Add(unit);
            _activeById.Add(unit.Id, unit);
            var spawnKey = (faction, unitId);
            _spawned[spawnKey] = _spawned.GetValueOrDefault(spawnKey) + 1;
            Changed?.Invoke();
        }

        private int RemoveAndRecordCasualties()
        {
            var defeated = _active.Where(value => value.Health <= 0).ToArray();
            foreach (var unit in defeated)
            {
                unit.State = CombatUnitState.Dead;
                var key = (unit.Faction, unit.Config.Id);
                _casualties[key] = _casualties.GetValueOrDefault(key) + 1;
                _activeById.Remove(unit.Id);
            }
            return _active.RemoveAll(value => value.Health <= 0);
        }

        private CombatTargetIndex BuildTargetIndex()
        {
            var targets = _targetIndex;
            targets.Reset();
            foreach (var unit in _active.Where(value => value.Health > 0))
                targets.Add(new CombatTargetCandidate(new CombatTargetId(CombatTargetKind.Unit, unit.Id),
                    unit.Faction, unit.X, unit.Y));
            AddGatherers(targets, _playerGatherers, MatchFaction.Player);
            AddGatherers(targets, _enemyGatherers, MatchFaction.Enemy);
            AddTowers(targets, _playerConstruction);
            AddTowers(targets, _enemyConstruction);
            if (_boss != null)
            {
                foreach (var boss in _boss.GetSnapshot().Where(value => value.State == BossRuntimeState.Active))
                {
                    var id = new CombatTargetId(CombatTargetKind.Boss, 0, boss.SpawnId);
                    targets.Add(new CombatTargetCandidate(id, MatchFaction.Player, boss.X, boss.Y));
                    targets.Add(new CombatTargetCandidate(id, MatchFaction.Enemy, boss.X, boss.Y));
                }
            }
            targets.Seal();
            return targets;
        }

        private static void AddGatherers(CombatTargetIndex targets, GathererSystem gatherers, MatchFaction faction)
        {
            if (gatherers == null) return;
            foreach (var gatherer in gatherers.GetSnapshot())
                targets.Add(new CombatTargetCandidate(new CombatTargetId(CombatTargetKind.Gatherer, gatherer.Id),
                    faction, gatherer.X, gatherer.Y));
        }

        private static void AddTowers(CombatTargetIndex targets, TowerConstructionSystem construction)
        {
            if (construction == null) return;
            foreach (var tower in construction.GetTowers())
                targets.Add(new CombatTargetCandidate(new CombatTargetId(CombatTargetKind.Tower, tower.Id),
                    tower.Faction, tower.X, tower.Y));
        }

        private bool TryResolveTarget(UnitState source, CombatTargetId id, CombatTargetIndex targets,
            out CombatTargetCandidate target)
        {
            if (id.Kind == CombatTargetKind.Unit && _activeById.TryGetValue(id.NumericId, out var unit) &&
                unit.Health > 0 && unit.Faction != source.Faction)
            {
                target = new CombatTargetCandidate(id, unit.Faction, unit.X, unit.Y);
                return true;
            }
            if (id.Kind == CombatTargetKind.Wall)
            {
                target = CreateWallTarget(source);
                return IsWallAlive(source.Faction) && id == target.Id;
            }
            if (!targets.TryGet(id, Opponent(source.Faction), out target)) return false;
            if (id.Kind == CombatTargetKind.Boss) return true;
            return target.Faction != source.Faction;
        }

        private bool SimulateTowerAttacks(CombatTargetIndex targets)
        {
            var changed = false;
            foreach (var construction in new[] { _playerConstruction, _enemyConstruction }.Where(value => value != null))
            foreach (var tower in construction.GetTowers())
            {
                var key = (tower.Faction, tower.Id);
                var cooldown = Math.Max(0, _towerCooldowns.GetValueOrDefault(key) - 1);
                _towerCooldowns[key] = cooldown;
                if (cooldown > 0) continue;
                targets.QueryOpponents(tower.Faction, tower.X, tower.Y, _config.Construction.AttackRange, _targetCandidates);
                UnitState target = null;
                var bestDistance = long.MaxValue;
                foreach (var candidate in _targetCandidates)
                {
                    if (candidate.Id.Kind != CombatTargetKind.Unit ||
                        !_activeById.TryGetValue(candidate.Id.NumericId, out var unit) || unit.Health <= 0) continue;
                    var distance = CombatTargetIndex.DistanceSquared(tower.X, tower.Y, unit.X, unit.Y);
                    if (distance > bestDistance || distance == bestDistance && target != null && unit.Id >= target.Id) continue;
                    target = unit;
                    bestDistance = distance;
                }
                if (target == null) continue;
                if (_config.Construction.ProjectileSpeedPerTick > 0) SpawnTowerProjectile(tower, target);
                else ApplyDamageToUnit(target, _config.Construction.AttackDamage,
                    new DamageSourceReference(CombatTargetKind.Tower, tower.Faction, tower.Id));
                _towerCooldowns[key] = Math.Max(1, _config.Construction.AttackIntervalTicks);
                changed = true;
            }
            var valid = new HashSet<(MatchFaction, int)>(new[] { _playerConstruction, _enemyConstruction }
                .Where(value => value != null).SelectMany(value => value.GetTowers()).Select(value => (value.Faction, value.Id)));
            foreach (var key in _towerCooldowns.Keys.Where(value => !valid.Contains(value)).ToArray()) _towerCooldowns.Remove(key);
            return changed;
        }

        private bool MoveTowards(UnitState unit, int targetX, int targetY, int stopRange, LaneSpatialIndex spatial,
            bool trackProgress)
        {
            spatial.GetSeparation(unit, out var separationX, out var separationY);
            var moved = CombatUnitMovement.MoveTowards(ref unit.X, ref unit.Y, targetX, targetY, unit.MovePerTick,
                stopRange, _movementMinimumX + unit.Config.CollisionRadius,
                _movementMaximumX - unit.Config.CollisionRadius,
                _movementMinimumY + unit.Config.CollisionRadius,
                _movementMaximumY - unit.Config.CollisionRadius, separationX, separationY,
                ref unit.MoveRemainderX, ref unit.MoveRemainderY);
            if (!trackProgress) return moved;

            var distance = CombatTargetIndex.DistanceSquared(unit.X, unit.Y, targetX, targetY);
            if (distance < unit.PreviousTargetDistanceSquared)
            {
                unit.StalledTicks = 0;
                unit.PreviousTargetDistanceSquared = distance;
            }
            else unit.StalledTicks++;
            if (unit.StalledTicks < 20) return moved;

            unit.SuppressedTarget = unit.LockedTarget;
            unit.SuppressedUntilTick = _currentTick + 10;
            ClearTarget(unit);
            return moved;
        }

        private bool ResolveFriendlyPenetrations()
        {
            var changed = false;
            var spatial = new LaneSpatialIndex(_active.Where(value => value.Health > 0), _maximumCollisionRadius);
            foreach (var faction in new[] { MatchFaction.Player, MatchFaction.Enemy })
            for (var lane = 0; lane < 3; lane++)
            {
                var values = spatial.GetLane(faction, lane);
                for (var leftIndex = 0; leftIndex < values.Length; leftIndex++)
                for (var rightIndex = leftIndex + 1; rightIndex < values.Length; rightIndex++)
                {
                    var left = values[leftIndex]; var right = values[rightIndex];
                    var combinedRadius = left.Unit.Config.CollisionRadius + right.Unit.Config.CollisionRadius;
                    if (right.X - left.X > combinedRadius) break;
                    var distance = Math.Abs(left.X - right.X) + Math.Abs(left.Y - right.Y);
                    if (distance >= combinedRadius) continue;
                    var mover = left.Unit.Id > right.Unit.Id ? left.Unit : right.Unit;
                    var anchor = ReferenceEquals(mover, left.Unit) ? right.Unit : left.Unit;
                    var originalX = mover.X; var originalY = mover.Y;
                    var correction = Math.Min(Math.Max(1, combinedRadius - distance), Math.Max(1, mover.MovePerTick));
                    var directionY = mover.Y == anchor.Y ? (mover.Id % 2 == 0 ? -1 : 1) : Math.Sign(mover.Y - anchor.Y);
                    mover.Y = ClampLaneY(mover, mover.Y + directionY * correction);
                    if (mover.Y == originalY)
                    {
                        var directionX = mover.X == anchor.X ? (mover.Faction == MatchFaction.Player ? -1 : 1) : Math.Sign(mover.X - anchor.X);
                        mover.X = ClampMovementX(mover, mover.X + directionX * correction);
                    }
                    changed |= mover.X != originalX || mover.Y != originalY;
                }
            }
            return changed;
        }

        private int ClampLaneY(UnitState unit, int y)
        {
            var minimum = _movementMinimumY + unit.Config.CollisionRadius;
            var maximum = _movementMaximumY - unit.Config.CollisionRadius;
            return minimum <= maximum ? Math.Clamp(y, minimum, maximum) : (_movementMinimumY + _movementMaximumY) / 2;
        }

        private void ConfigureLaneBounds(MatchBattlefieldLayoutConfig layout)
        {
            var centers = layout.Routes.Where(value => value.Points.Count > 0)
                .Select(value => value.Points.Sum(point => point.Y) / value.Points.Count)
                .OrderBy(value => value).Take(3).ToArray();
            if (centers.Length != 3)
                centers = new[] { layout.ReferenceHeight / 4, layout.ReferenceHeight / 2, layout.ReferenceHeight * 3 / 4 };
            for (var lane = 0; lane < 3; lane++)
            {
                _laneMinimumY[lane] = lane == 0 ? 0 : (centers[lane - 1] + centers[lane]) / 2;
                _laneMaximumY[lane] = lane == 2 ? layout.ReferenceHeight : (centers[lane] + centers[lane + 1]) / 2;
            }
        }
        private static (int PlayerSurfaceX, int EnemySurfaceX) ResolveWallSurfaceBounds(MatchConfigSnapshot config)
        {
            var routes = config.BattlefieldLayout.Routes;
            if (routes == null || routes.Count == 0)
                throw new InvalidOperationException("Combat requires at least one battlefield route to resolve wall surfaces.");

            int? playerSurfaceX = null;
            int? enemySurfaceX = null;
            foreach (var route in routes.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                if (route.Points == null || route.Points.Count < 2)
                    throw new InvalidOperationException($"Combat route '{route.Id}' requires at least two points.");
                var routeMinimumX = route.Points[0].X;
                var routeMaximumX = route.Points[route.Points.Count - 1].X;
                if (routeMinimumX >= routeMaximumX)
                    throw new InvalidOperationException($"Combat route '{route.Id}' must run from the player surface to the enemy surface.");
                if (playerSurfaceX.HasValue &&
                    (playerSurfaceX.Value != routeMinimumX || enemySurfaceX.Value != routeMaximumX))
                    throw new InvalidOperationException("All combat routes must share the same player and enemy wall surfaces.");
                playerSurfaceX = routeMinimumX;
                enemySurfaceX = routeMaximumX;
            }

            var player = playerSurfaceX.Value;
            var enemy = enemySurfaceX.Value;
            if (config.Combat.PlayerWall.Gate.X >= player || player >= enemy ||
                enemy >= config.Combat.EnemyWall.Gate.X)
                throw new InvalidOperationException("Wall gates must remain behind the shared combat route surfaces.");
            return (player, enemy);
        }

        private CombatTargetCandidate CreateWallTarget(UnitState source)
        {
            var wall = source.Faction == MatchFaction.Player ? _config.Combat.EnemyWall : _config.Combat.PlayerWall;
            var targetFaction = Opponent(source.Faction);
            var surfaceX = source.Faction == MatchFaction.Player ? _movementMaximumX : _movementMinimumX;
            return new CombatTargetCandidate(new CombatTargetId(CombatTargetKind.Wall, 0, wall.Id),
                targetFaction, surfaceX, source.Y);
        }

        private bool IsWallAlive(MatchFaction attacker) =>
            attacker == MatchFaction.Player ? _enemyWallHealth > 0 : _playerWallHealth > 0;

        private int WallSurfaceGap(UnitState source)
        {
            return source.Faction == MatchFaction.Player
                ? Math.Max(0, _movementMaximumX - (source.X + source.Config.CollisionRadius))
                : Math.Max(0, source.X - source.Config.CollisionRadius - _movementMinimumX);
        }

        private static int WallCenterAttackRange(UnitState source) =>
            Math.Max(0, source.AttackRange) + Math.Max(0, source.Config.CollisionRadius);

        private static int AttackRangeFor(UnitState source, CombatTargetKind kind) =>
            kind == CombatTargetKind.Wall ? WallCenterAttackRange(source) : source.AttackRange;

        private int ClampMovementX(UnitState unit, int x) => ClampMovementX(unit.Config, x);

        private int ClampMovementX(MatchUnitConfig config, int x)
        {
            var minimum = _movementMinimumX + config.CollisionRadius;
            var maximum = _movementMaximumX - config.CollisionRadius;
            return minimum <= maximum ? Math.Clamp(x, minimum, maximum) : (_movementMinimumX + _movementMaximumX) / 2;
        }


        private static CombatProjectileTargetKind ToProjectileTargetKind(CombatTargetKind kind) => kind switch
        {
            CombatTargetKind.Unit => CombatProjectileTargetKind.Unit,
            CombatTargetKind.Gatherer => CombatProjectileTargetKind.Gatherer,
            CombatTargetKind.Boss => CombatProjectileTargetKind.Boss,
            CombatTargetKind.Tower => CombatProjectileTargetKind.Tower,
            CombatTargetKind.Wall => CombatProjectileTargetKind.Wall,
            _ => CombatProjectileTargetKind.Unit
        };

        private static MatchFaction Opponent(MatchFaction faction) =>
            faction == MatchFaction.Player ? MatchFaction.Enemy : MatchFaction.Player;
        private static int ApplyMultiplier(int value, IUnitResearchModifiers research, UnitId unitId, string propertyKey) =>
            Math.Max(1, value * (research?.GetMultiplierMilli(unitId, propertyKey) ?? 1000) / 1000);
    }

    public sealed class ItemCardSnapshot
    {
        public ItemCardSnapshot(CardId id, CardType type, int count, ReinforcementTemplateId? reinforcementTemplateId = null,
            IReadOnlyList<UnitId> reinforcementUnits = null)
        { Id = id; Type = type; Count = count; ReinforcementTemplateId = reinforcementTemplateId; ReinforcementUnits = reinforcementUnits ?? Array.Empty<UnitId>(); }
        public CardId Id { get; }
        public CardType Type { get; }
        public int Count { get; }
        public ReinforcementTemplateId? ReinforcementTemplateId { get; }
        public IReadOnlyList<UnitId> ReinforcementUnits { get; }
    }

    public enum RewardChoiceKind { ContentCard, ProcessedResourceBundle, ReinforcementItem }

    public sealed class RewardChoiceSnapshot
    {
        public RewardChoiceSnapshot(RewardChoiceId id, RewardChoiceKind kind, string displayName,
            CardId? cardId = null, IReadOnlyList<ResourceAmount> resources = null,
            ReinforcementTemplateId? reinforcementTemplateId = null, IReadOnlyList<UnitId> units = null,
            RewardRarity rarity = RewardRarity.Common)
        {
            Id = id; Kind = kind; DisplayName = displayName ?? string.Empty; CardId = cardId;
            Resources = resources ?? Array.Empty<ResourceAmount>(); ReinforcementTemplateId = reinforcementTemplateId;
            Units = units ?? Array.Empty<UnitId>(); Rarity = rarity;
        }
        public RewardChoiceId Id { get; }
        public RewardChoiceKind Kind { get; }
        public string DisplayName { get; }
        public CardId? CardId { get; }
        public IReadOnlyList<ResourceAmount> Resources { get; }
        public ReinforcementTemplateId? ReinforcementTemplateId { get; }
        public IReadOnlyList<UnitId> Units { get; }
        public RewardRarity Rarity { get; }
        public string Value => CardId?.Value ?? Id.Value;
        public static implicit operator CardId(RewardChoiceSnapshot value) =>
            value?.CardId ?? new CardId("card.reward." + (value?.Id.Value ?? "invalid"));
    }

    public sealed class OfferSnapshot
    {
        public OfferSnapshot(bool active, IReadOnlyList<RewardChoiceSnapshot> choices)
        {
            Active = active;
            Choices = choices ?? Array.Empty<RewardChoiceSnapshot>();
        }

        public bool Active { get; }
        public IReadOnlyList<RewardChoiceSnapshot> Choices { get; }
    }

    public class HandAndOfferSystem : GameSystemBase, IFixedMatchSimulation, IMatchCardInventory
    {
        private readonly MatchConfigSnapshot _config;
        private readonly EconomySystem _economy;
        private readonly BuildingSystem _buildings;
        private readonly MatchFaction _faction;
        private readonly IReadOnlyList<CardId> _initialCards;
        private readonly Dictionary<CardId, int> _hand = new();
        private readonly Dictionary<CardId, ReinforcementTemplateId> _reinforcementCards = new();
        private readonly Dictionary<ReinforcementTemplateId, UnitId[]> _reinforcementUnits = new();
        private DeterministicRandomStream _random;
        private MatchTimedOfferConfig[] _offers;
        private int _currentTick;
        private int _nextRewardTick = 60 * ContentConstants.FixedTicksPerSecond;
        private int _offerSequence;
        private RewardChoiceSnapshot[] _activeChoices = Array.Empty<RewardChoiceSnapshot>();

        public HandAndOfferSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings)
            : this(config, economy, buildings, MatchFaction.Player, null) { }

        public HandAndOfferSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings,
            MatchFaction faction, IReadOnlyList<CardId> initialCards)
            : base(SystemLifetime.Scene)
        {
            _config = config; _economy = economy; _buildings = buildings; _faction = faction;
            _initialCards = initialCards ?? config.HandAndOffers.GuaranteedCards;
        }
        public event Action Changed;
        public event Action<bool> RewardChoiceStateChanged;

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _random = new DeterministicRandomStream(DeterministicRandomStream.DeriveSeed(_config.Seed,
                _faction == MatchFaction.Player ? "card-offers.player" : "card-offers.enemy"));
            _offers = _config.HandAndOffers.Offers.OrderBy(value => value.TriggerSeconds).ToArray();
            foreach (var card in _initialCards.Take(_config.HandAndOffers.HandLimit)) Add(card);
            foreach (var card in _faction == MatchFaction.Player ? _config.HandAndOffers.FillerCards : Array.Empty<CardId>())
                if (TotalCount < _config.HandAndOffers.HandLimit) Add(card);
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        { _hand.Clear(); _reinforcementCards.Clear(); _reinforcementUnits.Clear(); _activeChoices = Array.Empty<RewardChoiceSnapshot>(); return Task.CompletedTask; }

        public int TotalCount => _hand.Values.Sum();
        public IReadOnlyList<ItemCardSnapshot> GetHand() => _hand.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
            .Select(pair =>
            {
                var hasTemplate = _reinforcementCards.TryGetValue(pair.Key, out var templateId);
                return new ItemCardSnapshot(pair.Key, ResolveType(pair.Key), pair.Value, hasTemplate ? templateId : null,
                    hasTemplate && _reinforcementUnits.TryGetValue(templateId, out var units) ? units : Array.Empty<UnitId>());
            }).ToArray();
        public OfferSnapshot GetOffer() => new(_activeChoices.Length > 0, _activeChoices);
        public bool Contains(CardId cardId) => Has(cardId);
        public bool TryConsume(CardId cardId)
        {
            if (!Has(cardId)) return false;
            Consume(cardId);
            return true;
        }

        public bool TryGrantPublicCard(CardId cardId)
        {
            if (cardId.Value == null || TotalCount >= _config.HandAndOffers.HandLimit) return false;
            Add(cardId);
            Changed?.Invoke();
            return true;
        }

        public void SimulateTick(int tick)
        {
            _currentTick = tick;
            if (_activeChoices.Length > 0 || tick < _nextRewardTick) return;
            _activeChoices = BuildRewardChoices(tick);
            if (_activeChoices.Length != 4) return;
            _offerSequence++;
            RewardChoiceStateChanged?.Invoke(true);
            Changed?.Invoke();
        }

        public bool ChooseOffer(RewardChoiceId choiceId)
        {
            var choice = _activeChoices.FirstOrDefault(value => value.Id.Equals(choiceId));
            if (choice == null) return false;
            var granted = choice.Kind switch
            {
                RewardChoiceKind.ContentCard => TryGrantToHand(choice.CardId.Value),
                RewardChoiceKind.ProcessedResourceBundle => _economy.TryExchange(null, choice.Resources, out _),
                RewardChoiceKind.ReinforcementItem => TryGrantReinforcement(choice),
                _ => false
            };
            if (!granted) return false;
            CompleteOffer();
            return true;
        }

        public bool ChooseOffer(RewardChoiceSnapshot choice) => choice != null && ChooseOffer(choice.Id);
        public bool ChooseOffer(CardId cardId)
        { var choice = _activeChoices.FirstOrDefault(value => value.CardId.HasValue && value.CardId.Value.Equals(cardId)); return choice != null && ChooseOffer(choice.Id); }

        public bool TryReplaceAndChoose(RewardChoiceId choiceId, CardId replacedCardId)
        {
            if (!Has(replacedCardId) || !_activeChoices.Any(value => value.Id.Equals(choiceId))) return false;
            var template = _reinforcementCards.GetValueOrDefault(replacedCardId);
            Consume(replacedCardId);
            if (ChooseOffer(choiceId)) return true;
            Add(replacedCardId);
            if (template.Value != null) _reinforcementCards[replacedCardId] = template;
            return false;
        }

        public bool TryDeployReinforcement(CardId cardId, TrainingSystem training, int worldX, int worldY)
        {
            if (training == null || !Has(cardId) || !_reinforcementCards.TryGetValue(cardId, out var templateId) ||
                !_reinforcementUnits.TryGetValue(templateId, out var units)) return false;
            if (training.TryDeployReinforcements(units, worldX, worldY) != TrainingFailure.None) return false;
            Consume(cardId);
            if (!Has(cardId)) _reinforcementCards.Remove(cardId);
            return true;
        }

        public bool TryPlayBuilding(CardId cardId, int slotIndex)
        {
            if (!Has(cardId)) return false;
            var building = _config.Buildings.FirstOrDefault(value => value.SourceCardId.Equals(cardId));
            if (building == null || building.Category == BuildingCategory.BattlefieldStructure ||
                !_buildings.TryBuild(slotIndex, building.Id, out _)) return false;
            Consume(cardId); return true;
        }

        public bool TryConsumeTactic(CardId cardId, out MatchTacticEffectConfig effect)
        {
            effect = null;
            if (!Has(cardId) || ResolveType(cardId) != CardType.Tactic) return false;
            var effectId = cardId.Value switch
            {
                "card.tactic.field-rations" => "effect.field-rations",
                "card.tactic.emergency-supplies" => "effect.emergency-supplies",
                "card.tactic.arrow-rain" => "effect.arrow-rain",
                _ => string.Empty
            };
            effect = _config.HandAndOffers.TacticEffects.FirstOrDefault(value => value.Id.Value == effectId);
            if (effect == null) return false;
            if (effect.Kind == TacticEffectKind.AddResource && !_economy.TryExchange(null, effect.ResourceAmounts, out _)) return false;
            Consume(cardId);
            return true;
        }

        private bool Has(CardId id) => _hand.GetValueOrDefault(id) > 0;
        private void Add(CardId id) { _hand[id] = _hand.GetValueOrDefault(id) + 1; }
        private void Consume(CardId id) { if (--_hand[id] <= 0) _hand.Remove(id); Changed?.Invoke(); }
        private CardType ResolveType(CardId id)
        {
            if (_reinforcementCards.ContainsKey(id)) return CardType.ReinforcementItem;
            if (id.Value.StartsWith("card.tactic.", StringComparison.Ordinal)) return CardType.Tactic;
            if (id.Value.StartsWith("card.battlefield.", StringComparison.Ordinal)) return CardType.BattlefieldItem;
            if (id.Value.StartsWith("card.soldier.", StringComparison.Ordinal)) return CardType.Soldier;
            return CardType.BuildingItem;
        }

        private bool TryGrantToHand(CardId cardId)
        { if (TotalCount >= _config.HandAndOffers.HandLimit) return false; Add(cardId); return true; }

        private bool TryGrantReinforcement(RewardChoiceSnapshot choice)
        {
            if (TotalCount >= _config.HandAndOffers.HandLimit || !choice.ReinforcementTemplateId.HasValue || !choice.CardId.HasValue) return false;
            var templateId = choice.ReinforcementTemplateId.Value;
            var cardId = choice.CardId.Value;
            _reinforcementCards[cardId] = templateId;
            _reinforcementUnits[templateId] = choice.Units.ToArray();
            Add(cardId);
            return true;
        }

        private void CompleteOffer()
        {
            _activeChoices = Array.Empty<RewardChoiceSnapshot>();
            _nextRewardTick = checked(_currentTick + _config.Heat.GetTier(_currentTick).RewardCooldownSeconds * ContentConstants.FixedTicksPerSecond);
            RewardChoiceStateChanged?.Invoke(false);
            Changed?.Invoke();
        }

        private RewardChoiceSnapshot[] BuildRewardChoices(int tick)
        {
            var sequence = _offerSequence + 1;
            var contentCardA = ChooseContentCard(null);
            var contentCardB = ChooseContentCard(new HashSet<CardId> { contentCardA });
            var bundle = ChooseResourceBundle(tick, sequence);
            var reinforcement = ChooseReinforcement(tick, sequence);
            return new[]
            {
                new RewardChoiceSnapshot(new RewardChoiceId($"reward-choice.{sequence}.building-a"), RewardChoiceKind.ContentCard,
                    contentCardA.Value, contentCardA),
                new RewardChoiceSnapshot(new RewardChoiceId($"reward-choice.{sequence}.building-b"), RewardChoiceKind.ContentCard,
                    contentCardB.Value, contentCardB),
                new RewardChoiceSnapshot(new RewardChoiceId($"reward-choice.{sequence}.resources"), RewardChoiceKind.ProcessedResourceBundle,
                    bundle.name, resources: bundle.amounts, rarity: bundle.rarity),
                reinforcement
            };
        }

        private CardId ChooseContentCard(ISet<CardId> excluded)
        {
            var pool = _offers.SelectMany(value => value.FallbackCardIds)
                .Where(value => _config.Buildings.Any(building => building.SourceCardId.Equals(value)))
                .Where(value => excluded == null || !excluded.Contains(value))
                .Distinct().OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
            if (pool.Length == 0) pool = _config.Buildings.Select(value => value.SourceCardId).Distinct()
                .Where(value => excluded == null || !excluded.Contains(value))
                .OrderBy(value => value.Value, StringComparer.Ordinal).ToArray();
            if (pool.Length == 0) throw new InvalidOperationException("Schema v14 reward offer requires two distinct building cards.");
            var missingNineGrid = pool.Where(card =>
            {
                var building = _config.Buildings.First(value => value.SourceCardId.Equals(card));
                return building.Category != BuildingCategory.BattlefieldStructure &&
                    !_buildings.GetSnapshot().Any(value => value.BuildingId.HasValue && value.BuildingId.Value.Equals(building.Id));
            }).ToArray();
            var battlefieldStructures = pool.Where(card => _config.Buildings.First(value =>
                value.SourceCardId.Equals(card)).Category == BuildingCategory.BattlefieldStructure).ToArray();
            var candidates = missingNineGrid.Length > 0
                ? missingNineGrid.Concat(battlefieldStructures).Distinct().OrderBy(value => value.Value, StringComparer.Ordinal).ToArray()
                : pool;
            var weighted = candidates.Select(card => (card, weight: ContentCardPriority(
                    _config.Buildings.First(value => value.SourceCardId.Equals(card)))))
                .OrderBy(value => value.card.Value, StringComparer.Ordinal).ToArray();
            var roll = _random.Next(weighted.Sum(value => value.weight));
            foreach (var candidate in weighted)
            {
                if (roll < candidate.weight) return candidate.card;
                roll -= candidate.weight;
            }
            return weighted[^1].card;
        }

        private static int ContentCardPriority(MatchBuildingConfig building) => building.Category switch
        { BuildingCategory.Gathering => 500, BuildingCategory.Processing => 400, BuildingCategory.SoldierCamp => 300,
          BuildingCategory.Research => 250, BuildingCategory.BattlefieldStructure => 200, _ => 150 };

        private (string name, ResourceAmount[] amounts, RewardRarity rarity) ChooseResourceBundle(int tick, int sequence)
        {
            var bundles = _config.HandAndOffers.ProcessedResourceBundles.Count > 0
                ? _config.HandAndOffers.ProcessedResourceBundles.Select(value =>
                    (name: value.DisplayName, amounts: value.Amounts.ToArray(), rarity: value.Rarity)).ToArray()
                : new[]
                {
                    (name: "肉与酒", amounts: new[] { Amount("resource.meat", 6), Amount("resource.wine", 6) }, rarity: RewardRarity.Common),
                    (name: "木板与石料", amounts: new[] { Amount("resource.plank", 12), Amount("resource.stone", 12) }, rarity: RewardRarity.Common),
                    (name: "酒与铁锭", amounts: new[] { Amount("resource.wine", 6), Amount("resource.iron-ingot", 6) }, rarity: RewardRarity.Common)
                };
            var rarity = ChooseRarity(tick, bundles.Select(value => value.rarity).Distinct().ToArray());
            bundles = bundles.Where(value => value.rarity == rarity).ToArray();
            var balances = _economy.GetSnapshot().ToDictionary(value => value.Id.Value, StringComparer.Ordinal);
            return bundles.Select((value, index) => (value, index, coverage: value.Item2.Sum(amount =>
                    balances.TryGetValue(amount.ResourceId.Value, out var balance) && balance.Capacity > 0
                        ? balance.Amount * 1000 / balance.Capacity : 0)))
                .OrderBy(value => value.coverage).ThenBy(value => (value.index + sequence) % bundles.Length)
                .Select(value => value.value).First();
        }

        private RewardChoiceSnapshot ChooseReinforcement(int tick, int sequence)
        {
            var tier = Array.FindLastIndex(ContentConstants.HeatTierStartTicks, value => tick >= value);
            var effectiveTier = Math.Min(3, Math.Max(0, tier));
            var templates = _config.HandAndOffers.ReinforcementTemplates.Select(value =>
                (id: value.Id.Value, cardId: value.CardId, name: value.DisplayName, tier: value.MinimumHeatTier,
                    rarity: value.Rarity, units: value.Units.ToArray())).ToArray();
            var legal = templates.Where(value => value.tier <= effectiveTier &&
                    value.units.All(id => _config.Units.Any(unit => unit.Id.Equals(id))))
                .OrderBy(value => value.id, StringComparer.Ordinal).ToArray();
            if (legal.Length == 0) throw new InvalidOperationException("No legal reinforcement reward template exists.");
            var rarity = ChooseRarity(tick, legal.Select(value => value.rarity).Distinct().ToArray());
            legal = legal.Where(value => value.rarity == rarity).ToArray();
            var picked = legal[_random.Next(legal.Length)];
            var templateId = new ReinforcementTemplateId(picked.id);
            return new RewardChoiceSnapshot(new RewardChoiceId($"reward-choice.{sequence}.reinforcement"),
                RewardChoiceKind.ReinforcementItem, picked.name, picked.cardId, reinforcementTemplateId: templateId,
                units: picked.units, rarity: picked.rarity);
        }

        private RewardRarity ChooseRarity(int tick, IReadOnlyCollection<RewardRarity> legalRarities)
        {
            var heatTier = Math.Max(0, Array.FindLastIndex(ContentConstants.HeatTierStartTicks, value => tick >= value));
            var weighted = legalRarities.OrderBy(value => value)
                .Select(value => (rarity: value, weight: _config.HandAndOffers.RarityWeights.GetWeight(heatTier, value)))
                .Where(value => value.weight > 0).ToArray();
            if (weighted.Length == 0) throw new InvalidOperationException($"No legal reward rarity at heat tier {heatTier}.");
            var roll = _random.Next(weighted.Sum(value => value.weight));
            foreach (var entry in weighted)
            {
                if (roll < entry.weight) return entry.rarity;
                roll -= entry.weight;
            }
            return weighted[^1].rarity;
        }

        private static ResourceAmount Amount(string id, int amount) => new(new ResourceId(id), amount);
    }

    public sealed class EnemyHandAndOfferSystem : HandAndOfferSystem
    {
        public EnemyHandAndOfferSystem(MatchConfigSnapshot config, EnemyEconomySystem economy,
            EnemyBuildingSystem buildings, IReadOnlyList<CardId> initialCards)
            : base(config, economy, buildings, MatchFaction.Enemy, initialCards)
        {
        }
    }

}
