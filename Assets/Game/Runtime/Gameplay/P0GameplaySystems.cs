using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Progression;

namespace FortressFrontier.Runtime.Gameplay
{
    public enum EconomyFailure { None, UnknownResource, MetaResourceForbidden, InvalidAmount, InsufficientAvailable, CapacityExceeded, UnknownReservation }
    public enum EconomyTransactionKind { Add, Exchange, ReservationCommit }
    public enum ProductionBlockReason { None, MissingWorker, MissingInput, OutputFull, ReserveProtected }
    public enum BuildingUpgradeState { Hidden, Locked, Ready, Upgrading, Max }
    public enum TrainingFailure
    {
        None,
        InvalidQuantity,
        InvalidDeploymentPoint,
        CardInactive,
        UnknownUnit,
        InsufficientResources,
        OrderNotFound,
        TooManyUnitTypes,
        TooManyUnits,
        SelectionEmpty
    }
    public interface IFixedMatchSimulation { void SimulateTick(int tick); }
    public readonly struct ReservationId : IEquatable<ReservationId>
    {
        internal ReservationId(int value) => Value = value;
        internal int Value { get; }
        public bool Equals(ReservationId other) => Value == other.Value;
        public override bool Equals(object obj) => obj is ReservationId other && Equals(other);
        public override int GetHashCode() => Value;
    }

    public sealed class ResourceBalanceSnapshot
    {
        public ResourceBalanceSnapshot(ResourceId id, int amount, int reserved, int capacity)
        { Id = id; Amount = amount; Reserved = reserved; Capacity = capacity; }
        public ResourceId Id { get; }
        public int Amount { get; }
        public int Reserved { get; }
        public int Available => Amount - Reserved;
        public int Capacity { get; }
    }

    public readonly struct EconomyResourceDelta
    {
        public EconomyResourceDelta(ResourceId resourceId, int amount)
        { ResourceId = resourceId; Amount = amount; }
        public ResourceId ResourceId { get; }
        public int Amount { get; }
    }

    public class EconomySystem : GameSystemBase
    {
        private sealed class Reservation
        {
            public readonly Dictionary<ResourceId, int> Remaining = new();
        }

        private readonly MatchConfigSnapshot _config;
        private readonly IReadOnlyList<ResourceAmount> _initialInventory;
        private readonly Dictionary<ResourceId, MatchResourceConfig> _definitions = new();
        private readonly Dictionary<ResourceId, int> _balances = new();
        private readonly Dictionary<ResourceId, int> _reserved = new();
        private readonly Dictionary<int, Reservation> _reservations = new();
        private int _nextReservationId = 1;

        public EconomySystem(MatchConfigSnapshot config) : this(config, config?.InitialInventory) { }
        public EconomySystem(MatchConfigSnapshot config, IReadOnlyList<ResourceAmount> initialInventory) : base(SystemLifetime.Scene)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _initialInventory = initialInventory ?? Array.Empty<ResourceAmount>(); }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            foreach (var definition in _config.Resources)
            {
                if (definition.Id.Value == ContentConstants.GoldResourceId)
                    throw new InvalidOperationException("Meta gold cannot enter match economy.");
                _definitions.Add(definition.Id, definition);
                _balances.Add(definition.Id, 0);
                _reserved.Add(definition.Id, 0);
            }
            foreach (var amount in _initialInventory)
            {
                if (!TryAdd(amount.ResourceId, amount.Amount, out var failure))
                    throw new InvalidOperationException($"Invalid initial inventory '{amount.ResourceId}': {failure}.");
            }
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _definitions.Clear(); _balances.Clear(); _reserved.Clear(); _reservations.Clear();
            return Task.CompletedTask;
        }

        public IReadOnlyList<ResourceBalanceSnapshot> GetSnapshot() => _definitions.Keys
            .OrderBy(id => id.Value, StringComparer.Ordinal)
            .Select(id => new ResourceBalanceSnapshot(id, _balances[id], _reserved[id], _definitions[id].Capacity)).ToArray();

        public int GetAvailable(ResourceId id) => _balances.TryGetValue(id, out var amount) ? amount - _reserved[id] : 0;

        public bool TryAdd(ResourceId id, int amount, out EconomyFailure failure)
        {
            if (id.Value == ContentConstants.GoldResourceId) { failure = EconomyFailure.MetaResourceForbidden; return false; }
            if (!_definitions.TryGetValue(id, out var definition)) { failure = EconomyFailure.UnknownResource; return false; }
            if (amount <= 0) { failure = EconomyFailure.InvalidAmount; return false; }
            if (!definition.CanOverflow && _balances[id] + amount > definition.Capacity) { failure = EconomyFailure.CapacityExceeded; return false; }
            _balances[id] += amount;
            OnTransaction(EconomyTransactionKind.Add, default, new[] { new EconomyResourceDelta(id, amount) });
            failure = EconomyFailure.None; return true;
        }

        public bool TryExchange(IEnumerable<ResourceAmount> inputs, IEnumerable<ResourceAmount> outputs, out EconomyFailure failure)
        {
            var debit = Aggregate(inputs); var credit = Aggregate(outputs);
            if (!ValidateAmounts(debit, out failure) || !ValidateAmounts(credit, out failure)) return false;
            foreach (var pair in debit)
                if (_balances[pair.Key] - _reserved[pair.Key] < pair.Value) { failure = EconomyFailure.InsufficientAvailable; return false; }
            foreach (var pair in credit)
            {
                var definition = _definitions[pair.Key];
                var final = _balances[pair.Key] - debit.GetValueOrDefault(pair.Key) + pair.Value;
                if (!definition.CanOverflow && final > definition.Capacity) { failure = EconomyFailure.CapacityExceeded; return false; }
            }
            foreach (var pair in debit) _balances[pair.Key] -= pair.Value;
            foreach (var pair in credit) _balances[pair.Key] += pair.Value;
            OnTransaction(EconomyTransactionKind.Exchange, default,
                debit.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).Select(pair => new EconomyResourceDelta(pair.Key, -pair.Value))
                    .Concat(credit.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal).Select(pair => new EconomyResourceDelta(pair.Key, pair.Value))).ToArray());
            failure = EconomyFailure.None; return true;
        }

        public bool TryReserve(IEnumerable<ResourceAmount> amounts, out ReservationId reservationId, out EconomyFailure failure)
            => TryReserve(amounts, "source.reservation", "intent.unknown", out reservationId, out failure);

        public bool TryReserve(IEnumerable<ResourceAmount> amounts, string sourceId, string intentId,
            out ReservationId reservationId, out EconomyFailure failure)
        {
            reservationId = default;
            var costs = Aggregate(amounts);
            if (costs.Count == 0) { failure = EconomyFailure.InvalidAmount; return false; }
            if (!ValidateAmounts(costs, out failure)) return false;
            foreach (var pair in costs)
                if (_balances[pair.Key] - _reserved[pair.Key] < pair.Value) { failure = EconomyFailure.InsufficientAvailable; return false; }
            var reservation = new Reservation();
            foreach (var pair in costs) { reservation.Remaining.Add(pair.Key, pair.Value); _reserved[pair.Key] += pair.Value; }
            reservationId = new ReservationId(_nextReservationId++);
            _reservations.Add(reservationId.Value, reservation);
            OnReservationCreated(reservationId, sourceId, intentId);
            failure = EconomyFailure.None; return true;
        }

        public bool TryCommit(ReservationId id, IEnumerable<ResourceAmount> amounts, out EconomyFailure failure)
        {
            if (!_reservations.TryGetValue(id.Value, out var reservation)) { failure = EconomyFailure.UnknownReservation; return false; }
            var costs = Aggregate(amounts);
            if (!ValidateAmounts(costs, out failure)) return false;
            foreach (var pair in costs)
                if (!reservation.Remaining.TryGetValue(pair.Key, out var remaining) || remaining < pair.Value)
                { failure = EconomyFailure.InsufficientAvailable; return false; }
            foreach (var pair in costs)
            {
                reservation.Remaining[pair.Key] -= pair.Value;
                _reserved[pair.Key] -= pair.Value;
                _balances[pair.Key] -= pair.Value;
                if (reservation.Remaining[pair.Key] == 0) reservation.Remaining.Remove(pair.Key);
            }
            var completed = reservation.Remaining.Count == 0;
            if (completed) _reservations.Remove(id.Value);
            OnTransaction(EconomyTransactionKind.ReservationCommit, id,
                costs.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal)
                    .Select(pair => new EconomyResourceDelta(pair.Key, -pair.Value)).ToArray());
            if (completed) OnReservationRemoved(id);
            failure = EconomyFailure.None; return true;
        }

        public bool Release(ReservationId id)
        {
            if (!_reservations.Remove(id.Value, out var reservation)) return false;
            foreach (var pair in reservation.Remaining) _reserved[pair.Key] -= pair.Value;
            OnReservationRemoved(id);
            return true;
        }

        private bool ValidateAmounts(Dictionary<ResourceId, int> amounts, out EconomyFailure failure)
        {
            foreach (var pair in amounts)
            {
                if (pair.Key.Value == ContentConstants.GoldResourceId) { failure = EconomyFailure.MetaResourceForbidden; return false; }
                if (!_definitions.ContainsKey(pair.Key)) { failure = EconomyFailure.UnknownResource; return false; }
                if (pair.Value <= 0) { failure = EconomyFailure.InvalidAmount; return false; }
            }
            failure = EconomyFailure.None; return true;
        }

        protected virtual void OnTransaction(EconomyTransactionKind kind, ReservationId reservationId,
            IReadOnlyList<EconomyResourceDelta> deltas) { }
        protected virtual void OnReservationCreated(ReservationId reservationId, string sourceId, string intentId) { }
        protected virtual void OnReservationRemoved(ReservationId reservationId) { }

        private static Dictionary<ResourceId, int> Aggregate(IEnumerable<ResourceAmount> amounts)
        {
            var result = new Dictionary<ResourceId, int>();
            if (amounts == null) return result;
            foreach (var amount in amounts)
                result[amount.ResourceId] = result.GetValueOrDefault(amount.ResourceId) + amount.Amount;
            return result;
        }
    }

    public sealed class BuildingSlotSnapshot
    {
        public BuildingSlotSnapshot(int slotIndex, int instanceId, BuildingId? buildingId, bool paused, int level,
            int effectiveWorkCount, BuildingUpgradeState upgradeState, ProductionBlockReason blockReason,
            int upgradeProgressMilli = 0)
        { SlotIndex = slotIndex; InstanceId = instanceId; BuildingId = buildingId; Paused = paused; Level = level;
          EffectiveWorkCount = effectiveWorkCount; UpgradeState = upgradeState; BlockReason = blockReason;
          UpgradeProgressMilli = Math.Clamp(upgradeProgressMilli, 0, 1000); }
        public int SlotIndex { get; }
        public int InstanceId { get; }
        public BuildingId? BuildingId { get; }
        public bool Paused { get; }
        public int Level { get; }
        public int EffectiveWorkCount { get; }
        public BuildingUpgradeState UpgradeState { get; }
        public ProductionBlockReason BlockReason { get; }
        public int UpgradeProgressMilli { get; }
    }

    public class BuildingSystem : GameSystemBase, IFixedMatchSimulation
    {
        private sealed class Slot
        {
            public int InstanceId; public MatchBuildingConfig Config; public bool Paused; public int Level = 1;
            public int WorkTicks; public int EffectiveWorkCount; public int UpgradeTicksRemaining;
            public ProductionBlockReason BlockReason;
        }

        private readonly MatchConfigSnapshot _config;
        private readonly EconomySystem _economy;
        private readonly Slot[] _slots = new Slot[9];
        private readonly Dictionary<BuildingId, MatchBuildingConfig> _definitions;
        private int _nextInstanceId = 1;
        
        private int _publicProductionMultiplierMilli = 1000;
private MatchPhaseId _phaseId = new("phase.development");

        public BuildingSystem(MatchConfigSnapshot config, EconomySystem economy) : base(SystemLifetime.Scene)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _definitions = config.Buildings.ToDictionary(value => value.Id);
        }

        public event Action Changed;
        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public IReadOnlyList<BuildingSlotSnapshot> GetSnapshot() => Enumerable.Range(0, _slots.Length).Select(index =>
        {
            var slot = _slots[index];
            return slot == null
                ? new BuildingSlotSnapshot(index, 0, null, false, 0, 0, BuildingUpgradeState.Hidden, ProductionBlockReason.None)
                : new BuildingSlotSnapshot(index, slot.InstanceId, slot.Config.Id, slot.Paused, slot.Level,
                    slot.EffectiveWorkCount, ResolveUpgradeState(slot), slot.BlockReason,
                    ResolveUpgradeProgressMilli(slot));
        }).ToArray();

        public bool TryBuild(int slotIndex, BuildingId buildingId, out int instanceId)
        {
            instanceId = 0;
            if (slotIndex < 0 || slotIndex >= _slots.Length || _slots[slotIndex] != null ||
                !_definitions.TryGetValue(buildingId, out var config) ||
                config.Category == BuildingCategory.BattlefieldStructure) return false;
            var slot = new Slot { InstanceId = _nextInstanceId++, Config = config };
            _slots[slotIndex] = slot; instanceId = slot.InstanceId; Changed?.Invoke(); return true;
        }

        public bool TryResumeAfterResourceShortage(int instanceId)
        {
            var slot = Find(instanceId);
            if (slot == null || !slot.Paused ||
                slot.BlockReason is not (ProductionBlockReason.MissingInput or ProductionBlockReason.ReserveProtected))
                return false;
            slot.Paused = false;
            slot.BlockReason = ProductionBlockReason.None;
            Changed?.Invoke();
            return true;
        }

        public bool Demolish(int instanceId)
        {
            for (var i = 0; i < _slots.Length; i++)
                if (_slots[i]?.InstanceId == instanceId) { _slots[i] = null; Changed?.Invoke(); return true; }
            return false;
        }

        public bool TryStartUpgrade(int instanceId)
        {
            var slot = Find(instanceId);
            if (slot == null || ResolveUpgradeState(slot) != BuildingUpgradeState.Ready) return false;
            var upgrade = slot.Config.Upgrades.Single(value => value.Level == slot.Level + 1);
            if (!_economy.TryExchange(new[] { upgrade.Payment }, null, out _)) return false;
            slot.UpgradeTicksRemaining = Math.Max(1, upgrade.DurationTicks); Changed?.Invoke(); return true;
        }

        
        public void SetPublicProductionMultiplier(int multiplierMilli) =>
            _publicProductionMultiplierMilli = Math.Max(1000, multiplierMilli);
public void SetPhase(MatchPhaseId phaseId) => _phaseId = phaseId;

        public void SimulateTick()
        {
            var changed = false;
            foreach (var slot in _slots.Where(value => value != null))
            {
                if (slot.UpgradeTicksRemaining > 0)
                {
                    slot.UpgradeTicksRemaining--;
                    if (slot.UpgradeTicksRemaining == 0) slot.Level++;
                    changed = true;
                    continue;
                }
                if (slot.Paused) continue;
                if (slot.Config.Category == BuildingCategory.Gathering)
                {
                    slot.BlockReason = ProductionBlockReason.None;
                    continue;
                }
                if (++slot.WorkTicks < ResolveCycleTicks(slot)) continue;
                slot.WorkTicks = 0;
                var outputs = ScaleOutputs(slot);
                if (slot.Config.Category == BuildingCategory.Processing && !PreservesInputReserve(slot.Config))
                {
                    changed |= PauseForResourceShortage(slot, ProductionBlockReason.ReserveProtected);
                    continue;
                }
                if (_economy.TryExchange(slot.Config.Inputs, outputs, out var failure))
                { slot.EffectiveWorkCount++; slot.BlockReason = ProductionBlockReason.None; changed = true; }
                else if (failure == EconomyFailure.CapacityExceeded)
                    slot.BlockReason = ProductionBlockReason.OutputFull;
                else
                {
                    changed |= PauseForResourceShortage(slot, ProductionBlockReason.MissingInput);
                }
            }
            if (changed) Changed?.Invoke();
        }

        public void SimulateTick(int tick) => SimulateTick();

        public bool RecordExternalWork(int instanceId)
        {
            var slot = Find(instanceId);
            if (slot == null || slot.Config.Category != BuildingCategory.Gathering)
                return false;
            slot.EffectiveWorkCount++;
            slot.BlockReason = ProductionBlockReason.None;
            Changed?.Invoke();
            return true;
        }

        internal bool SetExternalBlockReason(int instanceId, ProductionBlockReason reason)
        {
            var slot = Find(instanceId);
            if (slot == null || slot.Config.Category != BuildingCategory.Gathering)
                return false;
            var pauseForMissingResource = reason is ProductionBlockReason.MissingInput or ProductionBlockReason.ReserveProtected;
            if (slot.BlockReason == reason && (!pauseForMissingResource || slot.Paused)) return false;
            if (slot.Paused && !pauseForMissingResource) return false;
            if (pauseForMissingResource) slot.Paused = true;
            slot.BlockReason = reason;
            Changed?.Invoke();
            return true;
        }

        private static bool PauseForResourceShortage(Slot slot, ProductionBlockReason reason)
        {
            if (slot.Paused && slot.BlockReason == reason) return false;
            slot.Paused = true;
            slot.BlockReason = reason;
            return true;
        }

        public MatchBuildingConfig GetConfig(int instanceId) => Find(instanceId)?.Config;
        public int GetLevel(int instanceId) => Find(instanceId)?.Level ?? 0;

        private int ResolveCycleTicks(Slot slot) => Math.Max(1, slot.Config.ProductionCycleTicks);
        private bool PreservesInputReserve(MatchBuildingConfig config)
        {
            foreach (var floor in config.InputReserveFloors)
            {
                var debit = config.Inputs.Where(value => value.ResourceId.Equals(floor.ResourceId)).Sum(value => value.Amount);
                if (_economy.GetAvailable(floor.ResourceId) - debit < floor.Amount)
                    return false;
            }
            return true;
        }
        private ResourceAmount[] ScaleOutputs(Slot slot)
        {
            var levelMultiplier = slot.Level <= 1
                ? 1000
                : slot.Config.Upgrades.Single(value => value.Level == slot.Level).ProductionMultiplierMilli;
            var combinedMultiplier = (long)levelMultiplier * _publicProductionMultiplierMilli / 1000;
            return slot.Config.Outputs.Select(value => new ResourceAmount(value.ResourceId,
                Math.Max(1, (int)Math.Min(int.MaxValue, value.Amount * combinedMultiplier / 1000)))).ToArray();
        }
        private Slot Find(int instanceId) => _slots.FirstOrDefault(value => value?.InstanceId == instanceId);
        private BuildingUpgradeState ResolveUpgradeState(Slot slot)
        {
            if (slot.UpgradeTicksRemaining > 0) return BuildingUpgradeState.Upgrading;
            var upgrade = slot.Config.Upgrades.FirstOrDefault(value => value.Level == slot.Level + 1);
            if (upgrade == null) return BuildingUpgradeState.Max;
            if (slot.EffectiveWorkCount < upgrade.RequiredWorkCount) return BuildingUpgradeState.Locked;
            if (upgrade.RequiredPhaseId.HasValue && !upgrade.RequiredPhaseId.Value.Equals(_phaseId)) return BuildingUpgradeState.Locked;
            return BuildingUpgradeState.Ready;
        }

        private static int ResolveUpgradeProgressMilli(Slot slot)
        {
            if (slot.UpgradeTicksRemaining <= 0) return 0;
            var upgrade = slot.Config.Upgrades.FirstOrDefault(value => value.Level == slot.Level + 1);
            if (upgrade == null) return 0;
            var duration = Math.Max(1, upgrade.DurationTicks);
            return Math.Clamp((duration - slot.UpgradeTicksRemaining) * 1000 / duration, 0, 1000);
        }
    }

    public interface IResearchCategoryAvailability
    {
        bool IsAvailable(ResearchCategory category);
        IReadOnlyList<ResearchCategory> GetAvailableCategories();
    }

    public class CampSystem : GameSystemBase, IResearchCategoryAvailability
    {
        private readonly BuildingSystem _buildings;
        private readonly IReadOnlyDictionary<CardId, ResearchCategory> _researchCategories;
        private Dictionary<CardId, int[]> _activeCamps = new();
        public CampSystem(BuildingSystem buildings, MatchConfigSnapshot config = null) : base(SystemLifetime.Scene)
        {
            _buildings = buildings;
            _researchCategories = (config?.Units ?? Array.Empty<MatchUnitConfig>())
                .Where(value => value.SoldierCardId.Value != null)
                .GroupBy(value => value.SoldierCardId)
                .ToDictionary(value => value.Key, value => value.First().ResearchCategory);
        }
        public event Action Changed;
        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        { _buildings.Changed += Refresh; Refresh(); return Task.CompletedTask; }
        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        { _buildings.Changed -= Refresh; _activeCamps.Clear(); return Task.CompletedTask; }
        public IReadOnlyList<int> GetCampInstanceIds(CardId soldierCardId) => _activeCamps.TryGetValue(soldierCardId, out var ids) ? ids : Array.Empty<int>();
        public bool IsActive(CardId soldierCardId) => GetCampInstanceIds(soldierCardId).Count > 0;
        public bool IsAvailable(ResearchCategory category) => _activeCamps.Keys.Any(cardId =>
            _researchCategories.TryGetValue(cardId, out var activeCategory) && activeCategory == category);
        public IReadOnlyList<ResearchCategory> GetAvailableCategories() => _activeCamps.Keys
            .Where(_researchCategories.ContainsKey)
            .Select(cardId => _researchCategories[cardId])
            .Distinct().OrderBy(value => value).ToArray();
        private void Refresh()
        {
            _activeCamps = _buildings.GetSnapshot().Where(value => value.BuildingId.HasValue)
                .Select(value => (snapshot: value, config: _buildings.GetConfig(value.InstanceId)))
                .Where(value => value.config?.Category == BuildingCategory.SoldierCamp && value.config.ActivatedSoldierCardId.HasValue)
                .GroupBy(value => value.config.ActivatedSoldierCardId.Value)
                .ToDictionary(group => group.Key, group => group.Select(value => value.snapshot.InstanceId).OrderBy(value => value).ToArray());
            Changed?.Invoke();
        }
    }

    public readonly struct DeploymentPoint
    {
        public DeploymentPoint(int lane, int cell) : this(lane, cell, 0, 0, false) { }
        private DeploymentPoint(int lane, int cell, int x, int y, bool hasWorldPosition)
        { Lane = lane; Cell = cell; X = x; Y = y; HasWorldPosition = hasWorldPosition; }
        public static DeploymentPoint World(int x, int y, int lane) => new(lane, 0, x, y, true);
        public int Lane { get; }
        public int Cell { get; }
        public int X { get; }
        public int Y { get; }
        public bool HasWorldPosition { get; }
        public bool IsValid => Lane is >= 0 and < 3 && (HasWorldPosition || Cell is >= 0 and < 3);
    }

    public enum DeploymentSlotState { WaitingForCamp, Training }

    public sealed class SoldierSelectionSnapshot
    {
        public SoldierSelectionSnapshot(IReadOnlyDictionary<UnitId, int> quantities, int idleTicks)
        { Quantities = quantities; IdleTicks = idleTicks; }
        public IReadOnlyDictionary<UnitId, int> Quantities { get; }
        public int IdleTicks { get; }
        public int TotalCount => Quantities.Values.Sum();
    }

    public sealed class DeploymentSlotSnapshot
    {
        public DeploymentSlotSnapshot(int id, int orderId, UnitId unitId, DeploymentPoint point,
            DeploymentSlotState state, int assignedCampId, int progressTicks, int requiredTicks, RouteId routeId = default)
        { Id = id; OrderId = orderId; UnitId = unitId; Point = point; RouteId = routeId; State = state; AssignedCampId = assignedCampId;
          ProgressTicks = progressTicks; RequiredTicks = requiredTicks; }
        public int Id { get; }
        public int OrderId { get; }
        public UnitId UnitId { get; }
        public DeploymentPoint Point { get; }
        public RouteId RouteId { get; }
        public DeploymentSlotState State { get; }
        public int AssignedCampId { get; }
        public int ProgressTicks { get; }
        public int RequiredTicks { get; }
    }

    public readonly struct UnitDeployment
    {
        public UnitDeployment(UnitId unitId, DeploymentPoint point, RouteId routeId)
        { UnitId = unitId; Point = point; RouteId = routeId; }
        public UnitId UnitId { get; }
        public DeploymentPoint Point { get; }
        public RouteId RouteId { get; }
    }

    public enum TrainingOrderPriority
    {
        Normal = 0,
        EmergencyDefense = 100
    }

    public sealed class TrainingOrderSnapshot
    {
        public TrainingOrderSnapshot(int id, UnitId unitId, int requested, int completed, int assignedCamps,
            TrainingOrderPriority priority = TrainingOrderPriority.Normal, string defenseTriggerId = "")
        { Id = id; UnitId = unitId; Requested = requested; Completed = completed; AssignedCamps = assignedCamps; Priority = priority; DefenseTriggerId = defenseTriggerId ?? string.Empty; }
        public int Id { get; }
        public UnitId UnitId { get; }
        public int Requested { get; }
        public int Completed { get; }
        public int Remaining => Requested - Completed;
        public int AssignedCamps { get; }
        public TrainingOrderPriority Priority { get; }
        public string DefenseTriggerId { get; }
    }

    public class TrainingSystem : GameSystemBase, IFixedMatchSimulation
    {
        private const int MaxSelectedUnitTypes = 3;
        private const int MaxSelectedPerUnit = 5;
        private const int MaxSelectedUnits = 8;
        private const int SelectionTimeoutTicks = 8 * ContentConstants.FixedTicksPerSecond;
        private sealed class Order
        {
            public int Id; public MatchUnitConfig Unit; public int Requested; public int Completed;
            public ReservationId Reservation; public readonly List<Slot> Slots = new();
            public TrainingOrderPriority Priority; public string DefenseTriggerId;
        }
        private sealed class Slot { public int Id; public DeploymentPoint Point; public RouteId RouteId; public bool Completed; }
        private sealed class Assignment { public int CampId; public Order Order; public Slot Slot; public int ProgressTicks; public int RequiredTicks; }
        private readonly MatchConfigSnapshot _config;
        private readonly EconomySystem _economy;
        private readonly BuildingSystem _buildings;
        private readonly CampSystem _camps;
        private readonly IUnitResearchModifiers _research;
        private readonly Dictionary<UnitId, MatchUnitConfig> _units;
        private readonly List<Order> _orders = new();
        private readonly Dictionary<int, Assignment> _assignments = new();
        private readonly Dictionary<UnitId, int> _selection = new();
        private ReservationId _selectionReservation;
        private bool _hasSelectionReservation;
        private int _selectionIdleTicks;
        private int _nextOrderId = 1;
        private int _nextSlotId = 1;

        public TrainingSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings, CampSystem camps)
            : this(config, economy, buildings, camps, null) { }
        public TrainingSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings, CampSystem camps,
            IUnitResearchModifiers research)
            : base(SystemLifetime.Scene)
        { _config = config; _economy = economy; _buildings = buildings; _camps = camps; _research = research; _units = config.Units.ToDictionary(value => value.Id); }
        public event Action Changed;
        public event Action<UnitId, DeploymentPoint> UnitDeployed;
        public event Action<UnitDeployment> UnitDeploymentCompleted;
        protected virtual int TrainingTimeMultiplierMilli => 1000;
        protected virtual int MinimumTrainingTicks => 1;
        protected virtual ZoneKind ReinforcementDeploymentZoneKind => ZoneKind.PlayerDeployment;
        public MatchRect PlayerDeploymentArea => _config.BattlefieldLayout.Zones.SingleOrDefault(value => value.Kind == ZoneKind.PlayerDeployment);
        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        { _camps.Changed += HandleCampChange; return Task.CompletedTask; }
        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _camps.Changed -= HandleCampChange;
            ReleaseSelection();
            foreach (var order in _orders) _economy.Release(order.Reservation);
            _orders.Clear(); _assignments.Clear();
            return Task.CompletedTask;
        }

        public IReadOnlyList<TrainingOrderSnapshot> GetSnapshot() => _orders.Select(order => new TrainingOrderSnapshot(
            order.Id, order.Unit.Id, order.Requested, order.Completed, _assignments.Values.Count(value => value.Order == order),
            order.Priority, order.DefenseTriggerId)).ToArray();

        public SoldierSelectionSnapshot GetSelectionSnapshot() => new(
            new System.Collections.ObjectModel.ReadOnlyDictionary<UnitId, int>(new Dictionary<UnitId, int>(_selection)),
            _selectionIdleTicks);

        public IReadOnlyList<DeploymentSlotSnapshot> GetDeploymentSlots() => _orders
            .SelectMany(order => order.Slots.Where(slot => !slot.Completed).Select(slot =>
            {
                var assignment = _assignments.Values.FirstOrDefault(value => value.Slot == slot);
                return new DeploymentSlotSnapshot(slot.Id, order.Id, order.Unit.Id, slot.Point,
                    assignment == null ? DeploymentSlotState.WaitingForCamp : DeploymentSlotState.Training,
                    assignment?.CampId ?? 0, assignment?.ProgressTicks ?? 0, assignment?.RequiredTicks ?? 0, slot.RouteId);
            }))
            .OrderBy(value => value.Id).ToArray();

        public TrainingFailure UpdateSelection(UnitId unitId, int quantity)
        {
            if (quantity < 0 || quantity > MaxSelectedPerUnit) return TrainingFailure.InvalidQuantity;
            if (!_units.TryGetValue(unitId, out var unit)) return TrainingFailure.UnknownUnit;
            if (quantity > 0 && !_camps.IsActive(unit.SoldierCardId)) return TrainingFailure.CardInactive;

            var candidate = new Dictionary<UnitId, int>(_selection);
            if (quantity == 0) candidate.Remove(unitId); else candidate[unitId] = quantity;
            if (candidate.Count > MaxSelectedUnitTypes) return TrainingFailure.TooManyUnitTypes;
            if (candidate.Values.Sum() > MaxSelectedUnits) return TrainingFailure.TooManyUnits;
            if (!TryReplaceSelectionReservation(candidate)) return TrainingFailure.InsufficientResources;
            _selection.Clear();
            foreach (var pair in candidate.OrderBy(value => value.Key.Value, StringComparer.Ordinal)) _selection.Add(pair.Key, pair.Value);
            _selectionIdleTicks = 0;
            Changed?.Invoke();
            return TrainingFailure.None;
        }

        public TrainingFailure CancelSelection()
        {
            if (_selection.Count == 0) return TrainingFailure.SelectionEmpty;
            ReleaseSelection();
            Changed?.Invoke();
            return TrainingFailure.None;
        }

        public TrainingFailure SubmitSelection(int worldX, int worldY, out IReadOnlyList<int> orderIds)
        {
            orderIds = Array.Empty<int>();
            if (_selection.Count == 0) return TrainingFailure.SelectionEmpty;
            var deployment = _config.BattlefieldLayout.Zones.SingleOrDefault(value => value.Kind == ZoneKind.PlayerDeployment);
            if (deployment.Width <= 0 || deployment.Height <= 0 || worldX < deployment.X || worldX > deployment.X + deployment.Width ||
                worldY < deployment.Y || worldY > deployment.Y + deployment.Height)
                return TrainingFailure.InvalidDeploymentPoint;

            var selected = _selection.OrderBy(value => value.Key.Value, StringComparer.Ordinal).ToArray();
            ReleaseSelection();
            var created = new List<Order>();
            var slotSequence = 0;
            foreach (var pair in selected)
            {
                var points = new DeploymentPoint[pair.Value];
                for (var index = 0; index < points.Length; index++)
                {
                    var offsetIndex = slotSequence++;
                    var x = Math.Clamp(worldX + ((offsetIndex % 3) - 1) * 28, deployment.X, deployment.X + deployment.Width);
                    var y = Math.Clamp(worldY + ((offsetIndex / 3) - 1) * 34, deployment.Y, deployment.Y + deployment.Height);
                    points[index] = DeploymentPoint.World(x, y, ResolveLane(deployment, y));
                }
                var failure = TryCreateOrderInternal(pair.Key, points, out var order);
                if (failure != TrainingFailure.None)
                {
                    foreach (var value in created.ToArray()) Cancel(value.Id);
                    RestoreSelection(selected);
                    return failure;
                }
                created.Add(order);
            }
            _selection.Clear();
            _selectionIdleTicks = 0;
            orderIds = created.Select(value => value.Id).ToArray();
            Changed?.Invoke();
            return TrainingFailure.None;
        }

        public TrainingFailure TryDeployReinforcements(IReadOnlyList<UnitId> unitIds, int worldX, int worldY)
        {
            if (unitIds == null || unitIds.Count == 0 || unitIds.Count > MaxSelectedUnits)
                return TrainingFailure.InvalidQuantity;
            if (unitIds.Any(value => !_units.ContainsKey(value))) return TrainingFailure.UnknownUnit;
            var area = _config.BattlefieldLayout.Zones.SingleOrDefault(value => value.Kind == ReinforcementDeploymentZoneKind);
            if (area.Width <= 0 || area.Height <= 0 || worldX < area.X || worldX > area.X + area.Width ||
                worldY < area.Y || worldY > area.Y + area.Height)
                return TrainingFailure.InvalidDeploymentPoint;

            var deployments = unitIds.Select((unitId, index) =>
            {
                var x = Math.Clamp(worldX + ((index % 3) - 1) * 28, area.X, area.X + area.Width);
                var y = Math.Clamp(worldY + ((index / 3) - 1) * 34, area.Y, area.Y + area.Height);
                var point = DeploymentPoint.World(x, y, ResolveLane(area, y));
                var route = _config.BattlefieldLayout.Routes
                    .OrderBy(value => value.Points.Count == 0 ? int.MaxValue :
                        Math.Abs(value.Points[0].Y - y))
                    .ThenBy(value => value.Id.Value, StringComparer.Ordinal)
                    .Select(value => value.Id).FirstOrDefault();
                return new UnitDeployment(unitId, point, route);
            }).ToArray();

            foreach (var deployment in deployments)
            {
                UnitDeployed?.Invoke(deployment.UnitId, deployment.Point);
                UnitDeploymentCompleted?.Invoke(deployment);
            }
            Changed?.Invoke();
            return TrainingFailure.None;
        }

        public TrainingFailure TryCreateOrder(UnitId unitId, int quantity, DeploymentPoint point, out int orderId)
            => TryCreateOrder(unitId, quantity, point, "source.training", "intent.unknown", out orderId);

        public TrainingFailure TryCreateOrder(UnitId unitId, int quantity, DeploymentPoint point,
            string sourceId, string intentId, out int orderId)
            => TryCreateOrder(unitId, quantity, point, default, sourceId, intentId, out orderId);

        public TrainingFailure TryCreateOrder(UnitId unitId, int quantity, DeploymentPoint point, RouteId routeId,
            string sourceId, string intentId, out int orderId)
            => TryCreateOrder(unitId, quantity, point, routeId, sourceId, intentId,
                TrainingOrderPriority.Normal, string.Empty, out orderId);

        public TrainingFailure TryCreateOrder(UnitId unitId, int quantity, DeploymentPoint point, RouteId routeId,
            string sourceId, string intentId, TrainingOrderPriority priority, string defenseTriggerId, out int orderId)
        {
            orderId = 0;
            if (quantity <= 0) return TrainingFailure.InvalidQuantity;
            if (!point.IsValid) return TrainingFailure.InvalidDeploymentPoint;
            var points = Enumerable.Repeat(point, quantity).ToArray();
            var routes = Enumerable.Repeat(routeId, quantity).ToArray();
            var failure = TryCreateOrderInternal(unitId, points, out var order, sourceId, intentId, routes,
                priority, defenseTriggerId);
            if (failure != TrainingFailure.None) return failure;
            orderId = order.Id; Changed?.Invoke(); return TrainingFailure.None;
        }

        public TrainingFailure Cancel(int orderId)
        {
            var order = _orders.FirstOrDefault(value => value.Id == orderId);
            if (order == null) return TrainingFailure.OrderNotFound;
            foreach (var campId in _assignments.Where(value => value.Value.Order == order).Select(value => value.Key).ToArray()) _assignments.Remove(campId);
            _economy.Release(order.Reservation); _orders.Remove(order); Changed?.Invoke(); return TrainingFailure.None;
        }

        public void SimulateTick()
        {
            var changed = false;
            if (_selection.Count > 0 && ++_selectionIdleTicks >= SelectionTimeoutTicks)
            { ReleaseSelection(); changed = true; }
            foreach (var order in _orders.OrderByDescending(value => value.Priority).ThenBy(value => value.Id).ToArray())
            {
                var camps = _camps.GetCampInstanceIds(order.Unit.SoldierCardId);
                var alreadyAssigned = _assignments.Values.Count(value => value.Order == order);
                var unstarted = order.Requested - order.Completed - alreadyAssigned;
                var waitingSlots = order.Slots.Where(value => !value.Completed && _assignments.Values.All(assignment => assignment.Slot != value)).OrderBy(value => value.Id).ToArray();
                foreach (var campId in camps.Where(value => !_assignments.ContainsKey(value)).Take(Math.Max(0, unstarted)))
                {
                    var slot = waitingSlots.First();
                    waitingSlots = waitingSlots.Skip(1).ToArray();
                    var level = _buildings.GetLevel(campId);
                    var config = _buildings.GetConfig(campId);
                    var multiplier = level <= 1 ? 1000 : config.Upgrades.Single(value => value.Level == level).TrainingTimeMultiplierMilli;
                    var requiredTicks = Math.Max(1, order.Unit.TrainingTicks * multiplier / 1000);
                    var researchSpeed = _research?.GetMultiplierMilli(order.Unit.Id, "training") ?? 1000;
                    requiredTicks = Math.Max(MinimumTrainingTicks,
                        requiredTicks * TrainingTimeMultiplierMilli / Math.Max(1, researchSpeed));
                    _assignments.Add(campId, new Assignment { CampId = campId, Order = order, Slot = slot, RequiredTicks = requiredTicks });
                    changed = true;
                }
            }

            foreach (var pair in _assignments.ToArray())
            {
                var assignment = pair.Value;
                if (!_camps.GetCampInstanceIds(assignment.Order.Unit.SoldierCardId).Contains(assignment.CampId))
                { _assignments.Remove(pair.Key); changed = true; continue; }
                if (++assignment.ProgressTicks < assignment.RequiredTicks) continue;
                if (!_economy.TryCommit(assignment.Order.Reservation, assignment.Order.Unit.TrainingCosts, out _))
                    throw new InvalidOperationException("A valid training reservation could not be committed.");
                assignment.Order.Completed++;
                assignment.Slot.Completed = true;
                _assignments.Remove(pair.Key);
                UnitDeployed?.Invoke(assignment.Order.Unit.Id, assignment.Slot.Point);
                UnitDeploymentCompleted?.Invoke(new UnitDeployment(assignment.Order.Unit.Id, assignment.Slot.Point, assignment.Slot.RouteId));
                if (assignment.Order.Completed >= assignment.Order.Requested) _orders.Remove(assignment.Order);
                changed = true;
            }
            if (changed) Changed?.Invoke();
        }

        public void SimulateTick(int tick) => SimulateTick();

        private void HandleCampChange()
        {
            foreach (var unitId in _selection.Keys.ToArray())
                if (!_camps.IsActive(_units[unitId].SoldierCardId))
                { ReleaseSelection(); Changed?.Invoke(); break; }
            foreach (var order in _orders.ToArray())
                if (!_camps.IsActive(order.Unit.SoldierCardId)) Cancel(order.Id);
        }

        private TrainingFailure TryCreateOrderInternal(UnitId unitId, IReadOnlyList<DeploymentPoint> points, out Order order,
            string sourceId = "source.training", string intentId = "intent.unknown", IReadOnlyList<RouteId> routeIds = null,
            TrainingOrderPriority priority = TrainingOrderPriority.Normal, string defenseTriggerId = "")
        {
            order = null;
            if (points == null || points.Count == 0) return TrainingFailure.InvalidQuantity;
            if (points.Any(value => !value.IsValid)) return TrainingFailure.InvalidDeploymentPoint;
            if (!_units.TryGetValue(unitId, out var unit)) return TrainingFailure.UnknownUnit;
            if (!_camps.IsActive(unit.SoldierCardId)) return TrainingFailure.CardInactive;
            var fullCost = ScaleCosts(unit, points.Count);
            if (!_economy.TryReserve(fullCost, sourceId, intentId, out var reservation, out _)) return TrainingFailure.InsufficientResources;
            order = new Order { Id = _nextOrderId++, Unit = unit, Requested = points.Count, Reservation = reservation,
                Priority = priority, DefenseTriggerId = defenseTriggerId ?? string.Empty };
            for (var index = 0; index < points.Count; index++) order.Slots.Add(new Slot { Id = _nextSlotId++, Point = points[index],
                RouteId = routeIds != null && index < routeIds.Count ? routeIds[index] : default });
            _orders.Add(order);
            return TrainingFailure.None;
        }

        private bool TryReplaceSelectionReservation(IReadOnlyDictionary<UnitId, int> candidate)
        {
            var previous = _selection.OrderBy(value => value.Key.Value, StringComparer.Ordinal).ToArray();
            ReleaseSelectionReservationOnly();
            if (candidate.Count == 0) return true;
            var costs = AggregateSelectionCosts(candidate);
            if (_economy.TryReserve(costs, out _selectionReservation, out _))
            { _hasSelectionReservation = true; return true; }
            if (previous.Length > 0)
            {
                if (!_economy.TryReserve(AggregateSelectionCosts(previous.ToDictionary(value => value.Key, value => value.Value)),
                    out _selectionReservation, out _))
                    throw new InvalidOperationException("The previous soldier selection reservation could not be restored.");
                _hasSelectionReservation = true;
            }
            return false;
        }

        private ResourceAmount[] AggregateSelectionCosts(IReadOnlyDictionary<UnitId, int> quantities) => quantities
            .SelectMany(pair => ScaleCosts(_units[pair.Key], pair.Value))
            .GroupBy(value => value.ResourceId)
            .Select(group => new ResourceAmount(group.Key, group.Sum(value => value.Amount)))
            .OrderBy(value => value.ResourceId.Value, StringComparer.Ordinal).ToArray();

        private static ResourceAmount[] ScaleCosts(MatchUnitConfig unit, int quantity) => unit.TrainingCosts
            .Select(value => new ResourceAmount(value.ResourceId, checked(value.Amount * quantity))).ToArray();

        private void ReleaseSelection()
        {
            ReleaseSelectionReservationOnly();
            _selection.Clear();
            _selectionIdleTicks = 0;
        }

        private void ReleaseSelectionReservationOnly()
        {
            if (_hasSelectionReservation) _economy.Release(_selectionReservation);
            _hasSelectionReservation = false;
            _selectionReservation = default;
        }

        private void RestoreSelection(IEnumerable<KeyValuePair<UnitId, int>> selected)
        {
            _selection.Clear();
            foreach (var pair in selected) _selection.Add(pair.Key, pair.Value);
            if (!_economy.TryReserve(AggregateSelectionCosts(_selection), out _selectionReservation, out _))
                throw new InvalidOperationException("The submitted soldier selection could not be restored after rollback.");
            _hasSelectionReservation = true;
            _selectionIdleTicks = 0;
        }

        private static int ResolveLane(MatchRect deployment, int y)
        {
            var relative = Math.Clamp(y - deployment.Y, 0, Math.Max(1, deployment.Height - 1));
            return Math.Clamp(relative * 3 / Math.Max(1, deployment.Height), 0, 2);
        }
    }

    public sealed class MatchPhaseSystem : GameSystemBase
    {
        private readonly MatchPhaseConfig[] _phases;
        private int _index;
        private int _publicProductionMultiplierMilli = 1000;

        public MatchPhaseSystem(MatchConfigSnapshot config) : base(SystemLifetime.Scene)
        {
            _phases = config.Phases.OrderBy(value => value.StartTick).ToArray();
        }

        public MatchPhaseId CurrentPhaseId => _phases[_index].Id;
        public int PublicProductionMultiplierMilli => _publicProductionMultiplierMilli;
        public bool IsPublicAccelerationActive => _publicProductionMultiplierMilli > 1000;
        public event Action<MatchPhaseId> PhaseChanged;

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _index = 0;
            _publicProductionMultiplierMilli = 1000;
            return Task.CompletedTask;
        }

        public void SimulateTick(int tick)
        {
            if (_index + 1 < _phases.Length && tick >= _phases[_index + 1].StartTick)
            {
                _index++;
                PhaseChanged?.Invoke(CurrentPhaseId);
            }

            var profile = _phases[0];
            _publicProductionMultiplierMilli = tick >= profile.PublicAccelerationStartTick
                ? profile.PublicProductionMultiplierMilli
                : 1000;
        }
    }

    public enum MatchPauseReason { Application, PlayerRewardChoice, Settlement }

    public sealed class FixedSimulationSystem : GameSystemBase, IGameTickable, IApplicationPauseHandler
    {
        private const double TickSeconds = 1d / ContentConstants.FixedTicksPerSecond;
        private readonly MatchPhaseSystem _phases;
        private readonly IFixedMatchSimulation[] _systems;
        private readonly HashSet<MatchPauseReason> _pauseReasons = new();
        private double _accumulator;
        public FixedSimulationSystem(MatchPhaseSystem phases, BuildingSystem buildings, TrainingSystem training)
            : this(phases, new IFixedMatchSimulation[] { buildings, training }) { }
        public FixedSimulationSystem(MatchPhaseSystem phases, MatchSimulationPipeline pipeline)
            : this(phases, pipeline?.Systems ?? throw new ArgumentNullException(nameof(pipeline))) { }
        public FixedSimulationSystem(MatchPhaseSystem phases, IReadOnlyList<IFixedMatchSimulation> systems) : base(SystemLifetime.Scene)
        { _phases = phases ?? throw new ArgumentNullException(nameof(phases)); _systems = systems?.ToArray() ?? throw new ArgumentNullException(nameof(systems)); }
        public int TickCount { get; private set; }
        public bool IsPaused => _pauseReasons.Count > 0;
        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _phases.PhaseChanged += SetBuildingPhase;
            SetBuildingPhase(_phases.CurrentPhaseId);
            return Task.CompletedTask;
        }
        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        { _phases.PhaseChanged -= SetBuildingPhase; _accumulator = 0; _pauseReasons.Clear(); return Task.CompletedTask; }
        public void Tick(float deltaTime)
        {
            if (IsPaused || deltaTime <= 0f) return;
            _accumulator += deltaTime;
            while (!IsPaused && _accumulator + 1e-9 >= TickSeconds)
            {
                _accumulator -= TickSeconds;
                AdvanceTicks(1);
            }
        }
public void AdvanceTicks(int count)
        {
            if (count < 0)
                throw new ArgumentOutOfRangeException(nameof(count));

            for (var index = 0; index < count; index++)
            {
                TickCount++;
                _phases.SimulateTick(TickCount);
                foreach (var building in _systems.OfType<BuildingSystem>())
                    building.SetPublicProductionMultiplier(_phases.PublicProductionMultiplierMilli);
                foreach (var system in _systems)
                    system.SimulateTick(TickCount);
            }
        }
        private void SetBuildingPhase(MatchPhaseId phaseId)
        {
            foreach (var building in _systems.OfType<BuildingSystem>()) building.SetPhase(phaseId);
        }
        public void SetPauseReason(MatchPauseReason reason, bool active)
        { if (active) _pauseReasons.Add(reason); else _pauseReasons.Remove(reason); }
        public void SetPaused(bool paused) => SetPauseReason(MatchPauseReason.Application, paused);
        public Task OnApplicationPauseAsync(bool isPaused, CancellationToken cancellationToken)
        { SetPauseReason(MatchPauseReason.Application, isPaused); return Task.CompletedTask; }
    }

    public sealed class MatchSettlementSystem : GameSystemBase
    {
        private readonly MatchConfigSnapshot _config; private readonly MatchId _matchId;
        private readonly IMatchSettlementService _settlement; private readonly FixedSimulationSystem _simulation;
        private MatchResult? _pendingResult;
        public MatchSettlementSystem(MatchConfigSnapshot config, MatchId matchId, IMatchSettlementService settlement, FixedSimulationSystem simulation)
            : base(SystemLifetime.Scene) { _config = config; _matchId = matchId; _settlement = settlement; _simulation = simulation; }
        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        public SettlementReceipt? LastReceipt { get; private set; }
        public async Task<SettlementReceipt> SettleAsync(bool completed, bool victory, CancellationToken cancellationToken)
        {
            _simulation.SetPauseReason(MatchPauseReason.Settlement, true);
            _pendingResult ??= new MatchResult(_matchId, _config.BattlefieldId, _config.MapModeId, completed, victory);
            var receipt = await _settlement.SettleMatchAsync(_pendingResult.Value, _config.Reward, cancellationToken);
            LastReceipt = receipt;
            return receipt;
        }
    }
}
