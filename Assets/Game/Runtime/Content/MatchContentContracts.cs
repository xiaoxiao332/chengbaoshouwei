using System;
using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Gameplay;

namespace FortressFrontier.Runtime.Content
{
    public readonly struct ResourceAmount
    {
        public ResourceAmount(ResourceId resourceId, int amount)
        {
            ResourceId = resourceId;
            Amount = amount;
        }

        public ResourceId ResourceId { get; }
        public int Amount { get; }
    }

    public sealed class MatchResourceConfig
    {
        public MatchResourceConfig(ResourceId id, int capacity, bool canOverflow)
            : this(id, capacity, canOverflow, ResourceAcquisitionKind.Processed)
        {
        }

        public MatchResourceConfig(ResourceId id, int capacity, bool canOverflow, ResourceAcquisitionKind acquisitionKind)
        {
            Id = id;
            Capacity = capacity;
            CanOverflow = canOverflow;
            AcquisitionKind = acquisitionKind;
        }

        public ResourceId Id { get; }
        public int Capacity { get; }
        public bool CanOverflow { get; }
        public ResourceAcquisitionKind AcquisitionKind { get; }
    }

    public sealed class MatchUpgradeConfig
    {
        public MatchUpgradeConfig(int level, int requiredWorkCount, MatchPhaseId? requiredPhaseId,
            ResourceAmount payment, int durationTicks, int productionMultiplierMilli, int trainingTimeMultiplierMilli)
        {
            Level = level;
            RequiredWorkCount = requiredWorkCount;
            RequiredPhaseId = requiredPhaseId;
            Payment = payment;
            DurationTicks = durationTicks;
            ProductionMultiplierMilli = productionMultiplierMilli;
            TrainingTimeMultiplierMilli = trainingTimeMultiplierMilli;
        }

        public int Level { get; }
        public int RequiredWorkCount { get; }
        public MatchPhaseId? RequiredPhaseId { get; }
        public ResourceAmount Payment { get; }
        public int DurationTicks { get; }
        public int ProductionMultiplierMilli { get; }
        public int TrainingTimeMultiplierMilli { get; }
    }

    public sealed class MatchBuildingConfig
    {
        public MatchBuildingConfig(BuildingId id, CardId sourceCardId, BuildingCategory category,
            IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs,
            CardId? activatedSoldierCardId, int productionCycleTicks, int workerGatherTicks,
            IReadOnlyList<MatchUpgradeConfig> upgrades)
            : this(id, sourceCardId, category, inputs, outputs, null, activatedSoldierCardId,
                productionCycleTicks, workerGatherTicks, upgrades)
        {
        }

        public MatchBuildingConfig(BuildingId id, CardId sourceCardId, BuildingCategory category,
            IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs,
            UnitId? workerUnitId, CardId? activatedSoldierCardId, int productionCycleTicks,
            int workerGatherTicks, IReadOnlyList<MatchUpgradeConfig> upgrades)
            : this(id, sourceCardId, category, inputs, outputs, workerUnitId, activatedSoldierCardId,
                productionCycleTicks, workerGatherTicks, upgrades, Array.Empty<ResourceId>(),
                Array.Empty<ResourceAmount>(), 250, 3, GathererResourceSelectionPolicy.Fixed,
                Array.Empty<ResourceAmount>())
        {
        }

        public MatchBuildingConfig(BuildingId id, CardId sourceCardId, BuildingCategory category,
            IReadOnlyList<ResourceAmount> inputs, IReadOnlyList<ResourceAmount> outputs,
            UnitId? workerUnitId, CardId? activatedSoldierCardId, int productionCycleTicks,
            int workerGatherTicks, IReadOnlyList<MatchUpgradeConfig> upgrades,
            IReadOnlyList<ResourceId> gathererAllowedResourceIds, IReadOnlyList<ResourceAmount> gathererDispatchCosts,
            int gathererDispatchIntervalTicks, int gathererCarryAmount,
            GathererResourceSelectionPolicy gathererResourceSelectionPolicy,
            IReadOnlyList<ResourceAmount> inputReserveFloors = null)
        {
            Id = id;
            SourceCardId = sourceCardId;
            Category = category;
            Inputs = inputs;
            Outputs = outputs;
            InputReserveFloors = inputReserveFloors ?? Array.Empty<ResourceAmount>();
            WorkerUnitId = workerUnitId;
            ActivatedSoldierCardId = activatedSoldierCardId;
            ProductionCycleTicks = productionCycleTicks;
            WorkerGatherTicks = workerGatherTicks;
            Upgrades = upgrades;
            GathererAllowedResourceIds = gathererAllowedResourceIds ?? Array.Empty<ResourceId>();
            GathererDispatchCosts = gathererDispatchCosts ?? Array.Empty<ResourceAmount>();
            GathererDispatchIntervalTicks = Math.Max(1, gathererDispatchIntervalTicks);
            GathererCarryAmount = Math.Max(1, gathererCarryAmount);
            GathererResourceSelectionPolicy = gathererResourceSelectionPolicy;
        }

        public BuildingId Id { get; }
        public CardId SourceCardId { get; }
        public BuildingCategory Category { get; }
        public IReadOnlyList<ResourceAmount> Inputs { get; }
        public IReadOnlyList<ResourceAmount> Outputs { get; }
        public IReadOnlyList<ResourceAmount> InputReserveFloors { get; }
        public UnitId? WorkerUnitId { get; }
        public CardId? ActivatedSoldierCardId { get; }
        public int ProductionCycleTicks { get; }
        public int WorkerGatherTicks { get; }
        public IReadOnlyList<MatchUpgradeConfig> Upgrades { get; }
        public IReadOnlyList<ResourceId> GathererAllowedResourceIds { get; }
        public IReadOnlyList<ResourceAmount> GathererDispatchCosts { get; }
        public int GathererDispatchIntervalTicks { get; }
        public int GathererCarryAmount { get; }
        public GathererResourceSelectionPolicy GathererResourceSelectionPolicy { get; }
    }

    public sealed class MatchUnitConfig
    {
        public MatchUnitConfig(UnitId id, CardId soldierCardId, IReadOnlyList<ResourceAmount> trainingCosts, int trainingTicks)
            : this(id, soldierCardId, trainingCosts, trainingTicks, 1, 0, 1000, 0, 1, 0, 0, 0, 1, 0,
                UnitTargetPriority.ThreatThenDistance, false)
        {
        }

        public MatchUnitConfig(UnitId id, CardId soldierCardId, IReadOnlyList<ResourceAmount> trainingCosts, int trainingTicks,
            int maxHealth, int attackDamage, int wallDamageMultiplierMilli, int movePerTick, int collisionRadius,
            int acquireRadius, int chaseRadius, int attackRange, int attackIntervalTicks, int projectileSpeedPerTick,
            UnitTargetPriority targetPriority, bool canAttack)
            : this(id, soldierCardId, trainingCosts, trainingTicks, maxHealth, attackDamage,
                wallDamageMultiplierMilli, movePerTick, collisionRadius, acquireRadius, chaseRadius,
                attackRange, attackIntervalTicks, projectileSpeedPerTick, targetPriority, canAttack,
                ResearchCategory.Melee, UnitProjectileKind.None, 0, 0, default)
        {
        }

        public MatchUnitConfig(UnitId id, CardId soldierCardId, IReadOnlyList<ResourceAmount> trainingCosts, int trainingTicks,
            int maxHealth, int attackDamage, int wallDamageMultiplierMilli, int movePerTick, int collisionRadius,
            int acquireRadius, int chaseRadius, int attackRange, int attackIntervalTicks, int projectileSpeedPerTick,
            UnitTargetPriority targetPriority, bool canAttack, ResearchCategory researchCategory,
            UnitProjectileKind projectileKind, int explosionRadius, int explosionSecondaryDamageMilli,
            ResourceKey projectilePresentationKey)
        {
            Id = id;
            SoldierCardId = soldierCardId;
            TrainingCosts = trainingCosts;
            TrainingTicks = trainingTicks;
            MaxHealth = maxHealth;
            AttackDamage = attackDamage;
            WallDamageMultiplierMilli = wallDamageMultiplierMilli;
            MovePerTick = movePerTick;
            CollisionRadius = collisionRadius;
            AcquireRadius = acquireRadius;
            ChaseRadius = chaseRadius;
            AttackRange = attackRange;
            AttackIntervalTicks = attackIntervalTicks;
            ProjectileSpeedPerTick = projectileSpeedPerTick;
            TargetPriority = targetPriority;
            CanAttack = canAttack;
            ResearchCategory = researchCategory;
            ProjectileKind = projectileKind;
            ExplosionRadius = Math.Max(0, explosionRadius);
            ExplosionSecondaryDamageMilli = Math.Clamp(explosionSecondaryDamageMilli, 0, 1000);
            ProjectilePresentationKey = projectilePresentationKey;
        }

        public UnitId Id { get; }
        public CardId SoldierCardId { get; }
        public IReadOnlyList<ResourceAmount> TrainingCosts { get; }
        public int TrainingTicks { get; }
        public int MaxHealth { get; }
        public int AttackDamage { get; }
        public int WallDamageMultiplierMilli { get; }
        public int MovePerTick { get; }
        public int CollisionRadius { get; }
        public int AcquireRadius { get; }
        public int ChaseRadius { get; }
        public int AttackRange { get; }
        public int AttackIntervalTicks { get; }
        public int ProjectileSpeedPerTick { get; }
        public UnitTargetPriority TargetPriority { get; }
        public bool CanAttack { get; }
        public ResearchCategory ResearchCategory { get; }
        public UnitProjectileKind ProjectileKind { get; }
        public int ExplosionRadius { get; }
        public int ExplosionSecondaryDamageMilli { get; }
        public ResourceKey ProjectilePresentationKey { get; }
    }

    public readonly struct MatchIntentWeightConfig
    {
        public MatchIntentWeightConfig(string intentId, int weight)
        { IntentId = intentId; Weight = weight; }
        public string IntentId { get; }
        public int Weight { get; }
    }

    public sealed class MatchPhaseConfig
    {
        public MatchPhaseConfig(MatchPhaseId id, int startTick)
            : this(id, startTick, Array.Empty<string>(), Array.Empty<MatchIntentWeightConfig>(), 9000, 2000)
        {
        }

        public MatchPhaseConfig(MatchPhaseId id, int startTick, IReadOnlyList<string> allowedIntentIds,
            IReadOnlyList<MatchIntentWeightConfig> baseIntentWeights)
            : this(id, startTick, allowedIntentIds, baseIntentWeights, 9000, 2000)
        {
        }

        public MatchPhaseConfig(MatchPhaseId id, int startTick, IReadOnlyList<string> allowedIntentIds,
            IReadOnlyList<MatchIntentWeightConfig> baseIntentWeights, int publicAccelerationStartTick,
            int publicProductionMultiplierMilli)
        {
            Id = id;
            StartTick = startTick;
            AllowedIntentIds = allowedIntentIds?.ToArray() ?? Array.Empty<string>();
            BaseIntentWeights = baseIntentWeights?.ToArray() ?? Array.Empty<MatchIntentWeightConfig>();
            PublicAccelerationStartTick = Math.Max(0, publicAccelerationStartTick);
            PublicProductionMultiplierMilli = Math.Max(1000, publicProductionMultiplierMilli);
        }

        public MatchPhaseId Id { get; }
        public int StartTick { get; }
        public IReadOnlyList<string> AllowedIntentIds { get; }
        public IReadOnlyList<MatchIntentWeightConfig> BaseIntentWeights { get; }
        public int PublicAccelerationStartTick { get; }
        public int PublicProductionMultiplierMilli { get; }
    }

    public sealed class MatchRewardConfig
    {
        public MatchRewardConfig(int completionGold, int victoryGold, int firstClearGold, int modeMultiplierMilli)
        {
            CompletionGold = completionGold;
            VictoryGold = victoryGold;
            FirstClearGold = firstClearGold;
            ModeMultiplierMilli = modeMultiplierMilli;
        }

        public int CompletionGold { get; }
        public int VictoryGold { get; }
        public int FirstClearGold { get; }
        public int ModeMultiplierMilli { get; }
    }

    public sealed class MatchPresentationConfig
    {
        public static MatchPresentationConfig Empty { get; } = new(
            new Dictionary<CardId, ResourceKey>(), new Dictionary<BuildingId, ResourceKey>(),
            new Dictionary<UnitId, MatchUnitPresentationConfig>(), default);

        public MatchPresentationConfig(IReadOnlyDictionary<CardId, ResourceKey> cardArt,
            IReadOnlyDictionary<BuildingId, ResourceKey> buildingArt,
            IReadOnlyDictionary<UnitId, MatchUnitPresentationConfig> units, ResourceKey mapArt = default)
        {
            CardArt = cardArt ?? throw new ArgumentNullException(nameof(cardArt));
            BuildingArt = buildingArt ?? throw new ArgumentNullException(nameof(buildingArt));
            Units = units ?? throw new ArgumentNullException(nameof(units));
            MapArt = mapArt;
        }

        public IReadOnlyDictionary<CardId, ResourceKey> CardArt { get; }
        public IReadOnlyDictionary<BuildingId, ResourceKey> BuildingArt { get; }
        public IReadOnlyDictionary<UnitId, MatchUnitPresentationConfig> Units { get; }
        public ResourceKey MapArt { get; }

        public ResourceKey GetCardArt(CardId id) => CardArt.TryGetValue(id, out var key)
            ? key
            : throw new KeyNotFoundException($"Missing card presentation for '{id}'.");

        public ResourceKey GetBuildingArt(BuildingId id) => BuildingArt.TryGetValue(id, out var key)
            ? key
            : throw new KeyNotFoundException($"Missing building presentation for '{id}'.");

        public MatchUnitPresentationConfig GetUnit(UnitId id) => Units.TryGetValue(id, out var value)
            ? value
            : throw new KeyNotFoundException($"Missing unit presentation for '{id}'.");
    }

    public sealed class MatchUnitPresentationConfig
    {
        public MatchUnitPresentationConfig(UnitId unitId, ResourceKey sprite,
            ResourceKey playerWorldPrefab, ResourceKey enemyWorldPrefab)
        {
            UnitId = unitId;
            Sprite = sprite;
            PlayerWorldPrefab = playerWorldPrefab;
            EnemyWorldPrefab = enemyWorldPrefab;
        }

        public UnitId UnitId { get; }
        public ResourceKey Sprite { get; }
        public ResourceKey PlayerWorldPrefab { get; }
        public ResourceKey EnemyWorldPrefab { get; }

        public ResourceKey WorldPrefab(MatchFaction faction) => faction == MatchFaction.Player
            ? PlayerWorldPrefab
            : EnemyWorldPrefab;
    }

    public sealed class MatchConfigSnapshot
    {
        public MatchConfigSnapshot(int schemaVersion, BattlefieldId battlefieldId, MapModeId mapModeId,
            IReadOnlyList<MatchResourceConfig> resources, IReadOnlyList<ResourceAmount> initialInventory,
            IReadOnlyList<MatchBuildingConfig> buildings, IReadOnlyList<MatchUnitConfig> units,
            IReadOnlyList<MatchPhaseConfig> phases, MatchRewardConfig reward, int deploymentOrderTimeoutTicks)
            : this(schemaVersion, battlefieldId, mapModeId, resources, initialInventory, buildings, units, phases, reward,
                deploymentOrderTimeoutTicks, MatchCombatConfig.Empty, MatchBattlefieldLayoutConfig.Empty,
                MatchHandAndOffersConfig.Empty, MatchResearchConfig.Empty, MatchBossConfig.Empty,
                MatchConstructionConfig.Empty, MatchEnemyEconomyConfig.Empty, MatchAiStrategyConfig.Empty,
                1, MatchPresentationConfig.Empty, null, MapModeKind.PeacefulDevelopment, MatchHeatConfig.Default)
        {
        }

        public MatchConfigSnapshot(int schemaVersion, BattlefieldId battlefieldId, MapModeId mapModeId,
            IReadOnlyList<MatchResourceConfig> resources, IReadOnlyList<ResourceAmount> initialInventory,
            IReadOnlyList<MatchBuildingConfig> buildings, IReadOnlyList<MatchUnitConfig> units,
            IReadOnlyList<MatchPhaseConfig> phases, MatchRewardConfig reward, int deploymentOrderTimeoutTicks,
            MatchCombatConfig combat, MatchBattlefieldLayoutConfig battlefieldLayout,
            MatchHandAndOffersConfig handAndOffers, MatchResearchConfig research, MatchBossConfig boss,
            MatchConstructionConfig construction, MatchEnemyEconomyConfig enemyEconomy, MatchAiStrategyConfig aiStrategy,
            int seed = 1, MatchPresentationConfig presentation = null, string battlefieldDisplayName = null,
            MapModeKind mapModeKind = MapModeKind.PeacefulDevelopment, MatchHeatConfig heat = null)
        {
            SchemaVersion = schemaVersion;
            BattlefieldId = battlefieldId;
            MapModeId = mapModeId;
            BattlefieldDisplayName = string.IsNullOrWhiteSpace(battlefieldDisplayName) ? battlefieldId.Value : battlefieldDisplayName;
            MapModeKind = mapModeKind;
            Resources = resources;
            InitialInventory = initialInventory;
            Buildings = buildings;
            Units = units;
            Phases = phases;
            Reward = reward;
            DeploymentOrderTimeoutTicks = deploymentOrderTimeoutTicks;
            Combat = combat ?? throw new ArgumentNullException(nameof(combat));
            BattlefieldLayout = battlefieldLayout ?? throw new ArgumentNullException(nameof(battlefieldLayout));
            HandAndOffers = handAndOffers ?? throw new ArgumentNullException(nameof(handAndOffers));
            Research = research ?? throw new ArgumentNullException(nameof(research));
            Boss = boss ?? throw new ArgumentNullException(nameof(boss));
            Construction = construction ?? throw new ArgumentNullException(nameof(construction));
            EnemyEconomy = enemyEconomy ?? throw new ArgumentNullException(nameof(enemyEconomy));
            AiStrategy = aiStrategy ?? throw new ArgumentNullException(nameof(aiStrategy));
            Heat = heat ?? MatchHeatConfig.Default;
            Presentation = presentation ?? MatchPresentationConfig.Empty;
            Seed = seed == 0 ? 1 : seed;
        }

        public int SchemaVersion { get; }
        public BattlefieldId BattlefieldId { get; }
        public MapModeId MapModeId { get; }
        public string BattlefieldDisplayName { get; }
        public MapModeKind MapModeKind { get; }
        public IReadOnlyList<MatchResourceConfig> Resources { get; }
        public IReadOnlyList<ResourceAmount> InitialInventory { get; }
        public IReadOnlyList<MatchBuildingConfig> Buildings { get; }
        public IReadOnlyList<MatchUnitConfig> Units { get; }
        public IReadOnlyList<MatchPhaseConfig> Phases { get; }
        public MatchRewardConfig Reward { get; }
        public int DeploymentOrderTimeoutTicks { get; }
        public MatchCombatConfig Combat { get; }
        public MatchBattlefieldLayoutConfig BattlefieldLayout { get; }
        public MatchHandAndOffersConfig HandAndOffers { get; }
        public MatchResearchConfig Research { get; }
        public MatchBossConfig Boss { get; }
        public MatchConstructionConfig Construction { get; }
        public MatchEnemyEconomyConfig EnemyEconomy { get; }
        public MatchAiStrategyConfig AiStrategy { get; }
        public MatchHeatConfig Heat { get; }
        public MatchPresentationConfig Presentation { get; }
        public int Seed { get; }
    }

    public readonly struct MatchHeatTier
    {
        public MatchHeatTier(int startTick, int rewardCooldownSeconds, int aiPressureIntervalMultiplierMilli,
            int advancedUnitWeightMultiplierMilli)
        { StartTick = startTick; RewardCooldownSeconds = rewardCooldownSeconds; AiPressureIntervalMultiplierMilli = aiPressureIntervalMultiplierMilli; AdvancedUnitWeightMultiplierMilli = advancedUnitWeightMultiplierMilli; }
        public int StartTick { get; }
        public int RewardCooldownSeconds { get; }
        public int AiPressureIntervalMultiplierMilli { get; }
        public int AdvancedUnitWeightMultiplierMilli { get; }
    }

    public interface IMatchHeatSnapshot
    {
        MatchHeatTier GetTier(int tick);
    }

    public sealed class MatchHeatConfig : IMatchHeatSnapshot
    {
        public static MatchHeatConfig Default { get; } = new(ContentConstants.HeatTierStartTicks.Select((start, index) =>
            new MatchHeatTier(start, ContentConstants.OfferCooldownSeconds[index],
                ContentConstants.AiPressureIntervalMultipliersMilli[index],
                ContentConstants.AdvancedUnitWeightMultipliersMilli[index])).ToArray());

        public MatchHeatConfig(IReadOnlyList<MatchHeatTier> tiers)
        { Tiers = Array.AsReadOnly((tiers ?? Array.Empty<MatchHeatTier>()).OrderBy(value => value.StartTick).ToArray()); }
        public IReadOnlyList<MatchHeatTier> Tiers { get; }
        public MatchHeatTier GetTier(int tick)
        {
            if (Tiers.Count == 0) return new MatchHeatTier(0, 90, 1000, 1000);
            var selected = Tiers[0];
            foreach (var tier in Tiers)
            { if (tier.StartTick > tick) break; selected = tier; }
            return selected;
        }
    }

    public interface IMatchContent
    {
        MatchConfigSnapshot CreateMatchSnapshot(BattlefieldId battlefieldId, MapModeId mapModeId);
        MatchConfigSnapshot CreateMatchSnapshot(BattlefieldId battlefieldId, MapModeId mapModeId, int seed);
    }

    internal static class MatchContentConversion
    {
        public static ResourceAmount[] ToAmounts(IEnumerable<ResourceAmountDefinition> source) =>
            source.Select(value => new ResourceAmount(new ResourceId(value.ResourceId), value.Amount)).ToArray();
    }
}
