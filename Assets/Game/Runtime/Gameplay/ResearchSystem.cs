using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Gameplay
{
    public enum ResearchFailure
    {
        None, LabMissing, LabUnavailable, AlreadyResearching, UnknownUpgrade,
        CategoryUnavailable, InsufficientResources, LevelMax
    }

    public interface IUnitResearchModifiers
    {
        int GetMultiplierMilli(UnitId unitId, string propertyKey);
    }

    public readonly struct ResearchCandidateSnapshot
    {
        public ResearchCandidateSnapshot(ResearchUpgradeId id, ResearchCategory targetRole, int rank,
            int maxRank, IReadOnlyList<MatchResearchModifierConfig> modifiers, ResourceKey presentationKey)
        { Id = id; TargetRole = targetRole; Rank = rank; MaxRank = maxRank; Modifiers = modifiers; PresentationKey = presentationKey; }
        public ResearchUpgradeId Id { get; }
        public ResearchCategory TargetRole { get; }
        public int Rank { get; }
        public int MaxRank { get; }
        public IReadOnlyList<MatchResearchModifierConfig> Modifiers { get; }
        public ResourceKey PresentationKey { get; }
    }

    public sealed class ResearchSnapshot
    {
        public ResearchSnapshot(bool active, ResearchUpgradeId activeUpgradeId, ResearchCategory category,
            int progressTicks, int requiredTicks, bool labAvailable,
            IReadOnlyList<ResearchCandidateSnapshot> candidates, int completedRanks)
        { Active = active; ActiveUpgradeId = activeUpgradeId; Category = category; ProgressTicks = progressTicks;
          RequiredTicks = requiredTicks; LabAvailable = labAvailable; Candidates = candidates; CompletedRanks = completedRanks; }
        public bool Active { get; }
        public ResearchUpgradeId ActiveUpgradeId { get; }
        public ResearchCategory Category { get; }
        public int ProgressTicks { get; }
        public int RequiredTicks { get; }
        public bool LabAvailable { get; }
        public IReadOnlyList<ResearchCandidateSnapshot> Candidates { get; }
        public int CompletedRanks { get; }
    }

    public class ResearchSystem : GameSystemBase, IFixedMatchSimulation, IUnitResearchModifiers
    {
        private const string ResearchLabId = "building.research-lab";
        private readonly MatchConfigSnapshot _config;
        private readonly EconomySystem _economy;
        private readonly BuildingSystem _buildings;
        private readonly IResearchCategoryAvailability _categoryAvailability;
        private readonly Dictionary<ResearchUpgradeId, MatchResearchUpgradeConfig> _upgrades;
        private readonly Dictionary<ResearchUpgradeId, int> _ranks = new();
        private readonly Dictionary<UnitId, ResearchCategory> _unitRoles;
        private ResearchUpgradeId _activeUpgradeId;
        private int _progress;
        private int _completedCount;
        private bool _active;

        public ResearchSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings,
            IResearchCategoryAvailability categoryAvailability = null)
            : base(SystemLifetime.Scene)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _categoryAvailability = categoryAvailability;
            _upgrades = config.Research.Upgrades.ToDictionary(value => value.Id);
            _unitRoles = config.Units.ToDictionary(value => value.Id, value => value.ResearchCategory);
        }

        public event Action Changed;

        public ResearchSnapshot GetSnapshot()
        {
            var lab = FindLab();
            var labAvailable = lab != null && !lab.Paused && lab.UpgradeState != BuildingUpgradeState.Upgrading;
            var activeCategory = _active && _upgrades.TryGetValue(_activeUpgradeId, out var upgrade)
                ? upgrade.TargetRole : default;
            return new ResearchSnapshot(_active, _activeUpgradeId, activeCategory, _progress,
                ResolveRequiredTicks(lab), labAvailable, GetCandidates(), _ranks.Values.Sum());
        }

        public IReadOnlyList<ResearchCandidateSnapshot> GetCandidates()
        {
            var available = _upgrades.Values
                .Where(value => GetRank(value.Id) < value.MaxRank &&
                                (_categoryAvailability == null || _categoryAvailability.IsAvailable(value.TargetRole)))
                .OrderBy(value => value.Id.Value, StringComparer.Ordinal)
                .ToArray();
            if (available.Length == 0) return Array.Empty<ResearchCandidateSnapshot>();
            var start = Math.Abs(HashCode.Combine(_config.Seed, _completedCount)) % available.Length;
            var count = Math.Min(_config.Research.CandidateCount, available.Length);
            var result = new ResearchCandidateSnapshot[count];
            for (var i = 0; i < count; i++)
            {
                var value = available[(start + i) % available.Length];
                result[i] = new ResearchCandidateSnapshot(value.Id, value.TargetRole, GetRank(value.Id),
                    value.MaxRank, value.Modifiers, value.PresentationKey);
            }
            return result;
        }

        public ResearchFailure TryStart(ResearchUpgradeId upgradeId)
        {
            var lab = FindLab();
            if (lab == null) return ResearchFailure.LabMissing;
            if (lab.Paused || lab.UpgradeState == BuildingUpgradeState.Upgrading)
                return ResearchFailure.LabUnavailable;
            if (_active) return ResearchFailure.AlreadyResearching;
            if (!_upgrades.TryGetValue(upgradeId, out var upgrade)) return ResearchFailure.UnknownUpgrade;
            if (GetRank(upgradeId) >= upgrade.MaxRank) return ResearchFailure.LevelMax;
            if (!GetCandidates().Any(value => value.Id.Equals(upgradeId))) return ResearchFailure.CategoryUnavailable;
            if (!_economy.TryReserve(_config.Research.Costs, "source.research", "intent.research",
                    out var reservation, out _))
                return ResearchFailure.InsufficientResources;
            if (!_economy.TryCommit(reservation, _config.Research.Costs, out _))
                throw new InvalidOperationException("A validated research reservation could not be committed.");
            _activeUpgradeId = upgradeId;
            _progress = 0;
            _active = true;
            Changed?.Invoke();
            return ResearchFailure.None;
        }

        public int GetRank(ResearchUpgradeId upgradeId) => _ranks.GetValueOrDefault(upgradeId);

        public int GetMultiplierMilli(UnitId unitId, string propertyKey)
        {
            if (!_unitRoles.TryGetValue(unitId, out var role)) return 1000;
            long bonusMilli = 0;
            foreach (var pair in _ranks.OrderBy(value => value.Key.Value, StringComparer.Ordinal))
            {
                var upgrade = _upgrades[pair.Key];
                if (upgrade.TargetRole != role) continue;
                foreach (var modifier in upgrade.Modifiers)
                    if (string.Equals(modifier.PropertyKey, propertyKey, StringComparison.Ordinal))
                        bonusMilli += (long)modifier.PercentPerRankBasisPoints * pair.Value / 10;
            }
            return (int)Math.Clamp(1000 + bonusMilli, 1, int.MaxValue);
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _ranks.Clear();
            _active = false;
            _progress = 0;
            _completedCount = 0;
            return Task.CompletedTask;
        }

        public void SimulateTick(int tick)
        {
            if (!_active) return;
            var lab = FindLab();
            if (lab == null || lab.Paused || lab.UpgradeState == BuildingUpgradeState.Upgrading) return;
            if (++_progress < ResolveRequiredTicks(lab)) return;
            var upgrade = _upgrades[_activeUpgradeId];
            _ranks[_activeUpgradeId] = Math.Min(upgrade.MaxRank, GetRank(_activeUpgradeId) + 1);
            _active = false;
            _progress = 0;
            _completedCount++;
            Changed?.Invoke();
        }

        private BuildingSlotSnapshot FindLab()
        {
            foreach (var slot in _buildings.GetSnapshot())
                if (slot.BuildingId?.Value == ResearchLabId) return slot;
            return null;
        }

        private int ResolveRequiredTicks(BuildingSlotSnapshot lab)
        {
            var multiplier = lab?.Level switch { 2 => 852, >= 3 => 752, _ => 1000 };
            return Math.Max(1, (_config.Research.ResearchTicks * multiplier + 999) / 1000);
        }
    }

    public sealed class PlayerResearchSystem : ResearchSystem
    {
        public PlayerResearchSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings,
            IResearchCategoryAvailability categoryAvailability = null)
            : base(config, economy, buildings, categoryAvailability) { }
    }

    public sealed class EnemyResearchSystem : ResearchSystem
    {
        public EnemyResearchSystem(MatchConfigSnapshot config, EconomySystem economy, BuildingSystem buildings,
            IResearchCategoryAvailability categoryAvailability = null)
            : base(config, economy, buildings, categoryAvailability) { }
    }
}
