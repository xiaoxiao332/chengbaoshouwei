using System;
using System.Collections.Generic;
using FortressFrontier.Core.Identifiers;

namespace FortressFrontier.Runtime.Content
{
    internal static class P1SnapshotFreeze
    {
        public static IReadOnlyList<T> Copy<T>(IReadOnlyList<T> source)
        {
            if (source == null || source.Count == 0) return Array.Empty<T>();
            var values = new T[source.Count];
            for (var i = 0; i < values.Length; i++) values[i] = source[i];
            return Array.AsReadOnly(values);
        }
    }

    public readonly struct MatchPoint
    {
        public MatchPoint(string id, int x, int y) { Id = id ?? string.Empty; X = x; Y = y; }
        public string Id { get; }
        public int X { get; }
        public int Y { get; }
    }

    public readonly struct MatchRect
    {
        public MatchRect(string id, ZoneKind kind, int x, int y, int width, int height)
        { Id = id ?? string.Empty; Kind = kind; X = x; Y = y; Width = width; Height = height; }
        public string Id { get; }
        public ZoneKind Kind { get; }
        public int X { get; }
        public int Y { get; }
        public int Width { get; }
        public int Height { get; }
    }

    public sealed class MatchWallConfig
    {
        public MatchWallConfig(string id, int maxHealth, MatchPoint gate) { Id = id; MaxHealth = maxHealth; Gate = gate; }
        public string Id { get; }
        public int MaxHealth { get; }
        public MatchPoint Gate { get; }
    }

    public sealed class MatchCombatConfig
    {
        public static MatchCombatConfig Empty { get; } = new(Array.Empty<MatchUnitConfig>(),
            new MatchWallConfig(string.Empty, 1, default), new MatchWallConfig(string.Empty, 1, default));
        public MatchCombatConfig(IReadOnlyList<MatchUnitConfig> units, MatchWallConfig playerWall, MatchWallConfig enemyWall)
        { Units = P1SnapshotFreeze.Copy(units); PlayerWall = playerWall; EnemyWall = enemyWall; }
        public IReadOnlyList<MatchUnitConfig> Units { get; }
        public MatchWallConfig PlayerWall { get; }
        public MatchWallConfig EnemyWall { get; }
    }

    public sealed class MatchRouteConfig
    {
        public MatchRouteConfig(RouteId id, IReadOnlyList<MatchPoint> points) { Id = id; Points = P1SnapshotFreeze.Copy(points); }
        public RouteId Id { get; }
        public IReadOnlyList<MatchPoint> Points { get; }
    }

    public sealed class MatchResourceNodeConfig
    {
        public MatchResourceNodeConfig(ResourceNodeId id, ResourceId resourceId, MatchPoint position, int capacity)
            : this(id, position, capacity, ResourceNodeSpawnGroup.Central, string.Empty, new[] { resourceId }, 0, 450) { }
        public MatchResourceNodeConfig(ResourceNodeId id, MatchPoint position, int capacity,
            ResourceNodeSpawnGroup spawnGroup, string mirrorNodeId, IReadOnlyList<ResourceId> allowedResourceIds,
            int respawnCapacity = 0, int respawnDelayTicks = 1)
        { Id = id; Position = position; Capacity = capacity; SpawnGroup = spawnGroup; MirrorNodeId = mirrorNodeId ?? string.Empty; AllowedResourceIds = P1SnapshotFreeze.Copy(allowedResourceIds); RespawnCapacity = Math.Max(0, respawnCapacity); RespawnDelayTicks = Math.Max(1, respawnDelayTicks); }
        public ResourceNodeId Id { get; }
        public ResourceId ResourceId => AllowedResourceIds.Count > 0 ? AllowedResourceIds[0] : default;
        public MatchPoint Position { get; }
        public int Capacity { get; }
        public ResourceNodeSpawnGroup SpawnGroup { get; }
        public string MirrorNodeId { get; }
        public IReadOnlyList<ResourceId> AllowedResourceIds { get; }
        public int RespawnCapacity { get; }
        public int RespawnDelayTicks { get; }
    }

    public sealed class MatchResourceActivationWaveConfig
    {
        public MatchResourceActivationWaveConfig(string id, int triggerTick, int nodesPerGroup,
            IReadOnlyList<ResourceNodeSpawnGroup> groups, IReadOnlyList<ResourceId> allowedResourceIds)
        { Id = id ?? string.Empty; TriggerTick = triggerTick; NodesPerGroup = nodesPerGroup; Groups = P1SnapshotFreeze.Copy(groups); AllowedResourceIds = P1SnapshotFreeze.Copy(allowedResourceIds); }
        public string Id { get; }
        public int TriggerTick { get; }
        public int NodesPerGroup { get; }
        public IReadOnlyList<ResourceNodeSpawnGroup> Groups { get; }
        public IReadOnlyList<ResourceId> AllowedResourceIds { get; }
    }

    public sealed class MatchGathererConfig
    {
        public MatchGathererConfig(GathererSourceId sourceId, RouteId routeId, UnitId unitId,
            IReadOnlyList<ResourceId> allowedResourceIds, int carryAmount, int gatherTicks,
            int movePerTick, int maxHealth)
            : this(sourceId, routeId, unitId, allowedResourceIds, carryAmount, gatherTicks,
                movePerTick, maxHealth, Array.Empty<ResourceAmount>(), 250,
                GathererResourceSelectionPolicy.Fixed, default)
        {
        }

        public MatchGathererConfig(GathererSourceId sourceId, RouteId routeId, UnitId unitId,
            IReadOnlyList<ResourceId> allowedResourceIds, int carryAmount, int gatherTicks,
            int movePerTick, int maxHealth, IReadOnlyList<ResourceAmount> dispatchCosts,
            int dispatchIntervalTicks, GathererResourceSelectionPolicy selectionPolicy,
            BuildingId sourceBuildingId)
            : this(sourceId, routeId, unitId, allowedResourceIds, carryAmount, gatherTicks, movePerTick,
                maxHealth, dispatchCosts, dispatchIntervalTicks, dispatchIntervalTicks, selectionPolicy,
                sourceBuildingId)
        {
        }

        public MatchGathererConfig(GathererSourceId sourceId, RouteId routeId, UnitId unitId,
            IReadOnlyList<ResourceId> allowedResourceIds, int carryAmount, int gatherTicks,
            int movePerTick, int maxHealth, IReadOnlyList<ResourceAmount> dispatchCosts,
            int dispatchIntervalMinTicks, int dispatchIntervalMaxTicks,
            GathererResourceSelectionPolicy selectionPolicy, BuildingId sourceBuildingId)
        {
            SourceId = sourceId;
            RouteId = routeId;
            UnitId = unitId;
            AllowedResourceIds = P1SnapshotFreeze.Copy(allowedResourceIds);
            CarryAmount = carryAmount;
            GatherTicks = gatherTicks;
            MovePerTick = Math.Max(1, movePerTick);
            MaxHealth = Math.Max(1, maxHealth);
            DispatchCosts = P1SnapshotFreeze.Copy(dispatchCosts);
            DispatchIntervalMinTicks = Math.Max(1, dispatchIntervalMinTicks);
            DispatchIntervalMaxTicks = Math.Max(DispatchIntervalMinTicks, dispatchIntervalMaxTicks);
            SelectionPolicy = selectionPolicy;
            SourceBuildingId = sourceBuildingId;
        }

        public GathererSourceId SourceId { get; }
        public RouteId RouteId { get; }
        public UnitId UnitId { get; }
        public IReadOnlyList<ResourceId> AllowedResourceIds { get; }
        public int CarryAmount { get; }
        public int GatherTicks { get; }
        public int MovePerTick { get; }
        public int MaxHealth { get; }
        public IReadOnlyList<ResourceAmount> DispatchCosts { get; }
        public int DispatchIntervalTicks => DispatchIntervalMinTicks;
        public int DispatchIntervalMinTicks { get; }
        public int DispatchIntervalMaxTicks { get; }
        public GathererResourceSelectionPolicy SelectionPolicy { get; }
        public BuildingId SourceBuildingId { get; }
    }

    public sealed class MatchBossSpawnConfig
    {
        public MatchBossSpawnConfig(string id, MatchPoint position, int warningTick, int spawnTick)
        { Id = id; Position = position; WarningTick = warningTick; SpawnTick = spawnTick; }
        public string Id { get; }
        public MatchPoint Position { get; }
        public int WarningTick { get; }
        public int SpawnTick { get; }
    }

    public sealed class MatchBattlefieldLayoutConfig
    {
        public static MatchBattlefieldLayoutConfig Empty { get; } = new(1920, 1080, Array.Empty<MatchRect>(),
            Array.Empty<MatchRouteConfig>(), Array.Empty<MatchResourceNodeConfig>(), Array.Empty<MatchBossSpawnConfig>(), 1,
            Array.Empty<MatchResourceActivationWaveConfig>(), Array.Empty<MatchGathererConfig>());
        public MatchBattlefieldLayoutConfig(int referenceWidth, int referenceHeight, IReadOnlyList<MatchRect> zones,
            IReadOnlyList<MatchRouteConfig> routes, IReadOnlyList<MatchResourceNodeConfig> resourceNodes,
            IReadOnlyList<MatchBossSpawnConfig> bossSpawns, int minimumRoadWidth)
            : this(referenceWidth, referenceHeight, zones, routes, resourceNodes, bossSpawns, minimumRoadWidth,
                Array.Empty<MatchResourceActivationWaveConfig>(), Array.Empty<MatchGathererConfig>()) { }
        public MatchBattlefieldLayoutConfig(int referenceWidth, int referenceHeight, IReadOnlyList<MatchRect> zones,
            IReadOnlyList<MatchRouteConfig> routes, IReadOnlyList<MatchResourceNodeConfig> resourceNodes,
            IReadOnlyList<MatchBossSpawnConfig> bossSpawns, int minimumRoadWidth,
            IReadOnlyList<MatchResourceActivationWaveConfig> activationWaves)
            : this(referenceWidth, referenceHeight, zones, routes, resourceNodes, bossSpawns, minimumRoadWidth,
                activationWaves, Array.Empty<MatchGathererConfig>()) { }
        public MatchBattlefieldLayoutConfig(int referenceWidth, int referenceHeight, IReadOnlyList<MatchRect> zones,
            IReadOnlyList<MatchRouteConfig> routes, IReadOnlyList<MatchResourceNodeConfig> resourceNodes,
            IReadOnlyList<MatchBossSpawnConfig> bossSpawns, int minimumRoadWidth,
            IReadOnlyList<MatchResourceActivationWaveConfig> activationWaves,
            IReadOnlyList<MatchGathererConfig> gatherers)
            : this(referenceWidth, referenceHeight, zones, routes, resourceNodes, bossSpawns, minimumRoadWidth,
                activationWaves, gatherers, 80) { }
        public MatchBattlefieldLayoutConfig(int referenceWidth, int referenceHeight, IReadOnlyList<MatchRect> zones,
            IReadOnlyList<MatchRouteConfig> routes, IReadOnlyList<MatchResourceNodeConfig> resourceNodes,
            IReadOnlyList<MatchBossSpawnConfig> bossSpawns, int minimumRoadWidth,
            IReadOnlyList<MatchResourceActivationWaveConfig> activationWaves,
            IReadOnlyList<MatchGathererConfig> gatherers, int gathererDispatchIntervalTicks)
            : this(referenceWidth, referenceHeight, zones, routes, resourceNodes, bossSpawns, minimumRoadWidth,
                activationWaves, gatherers, gathererDispatchIntervalTicks, gathererDispatchIntervalTicks) { }
        public MatchBattlefieldLayoutConfig(int referenceWidth, int referenceHeight, IReadOnlyList<MatchRect> zones,
            IReadOnlyList<MatchRouteConfig> routes, IReadOnlyList<MatchResourceNodeConfig> resourceNodes,
            IReadOnlyList<MatchBossSpawnConfig> bossSpawns, int minimumRoadWidth,
            IReadOnlyList<MatchResourceActivationWaveConfig> activationWaves,
            IReadOnlyList<MatchGathererConfig> gatherers, int gathererDispatchIntervalMinTicks,
            int gathererDispatchIntervalMaxTicks)
        { ReferenceWidth = referenceWidth; ReferenceHeight = referenceHeight; Zones = P1SnapshotFreeze.Copy(zones); Routes = P1SnapshotFreeze.Copy(routes); ResourceNodes = P1SnapshotFreeze.Copy(resourceNodes); BossSpawns = P1SnapshotFreeze.Copy(bossSpawns); MinimumRoadWidth = minimumRoadWidth; ActivationWaves = P1SnapshotFreeze.Copy(activationWaves); Gatherers = P1SnapshotFreeze.Copy(gatherers); GathererDispatchIntervalMinTicks = Math.Max(1, gathererDispatchIntervalMinTicks); GathererDispatchIntervalMaxTicks = Math.Max(GathererDispatchIntervalMinTicks, gathererDispatchIntervalMaxTicks); }
        public int ReferenceWidth { get; }
        public int ReferenceHeight { get; }
        public IReadOnlyList<MatchRect> Zones { get; }
        public IReadOnlyList<MatchRouteConfig> Routes { get; }
        public IReadOnlyList<MatchResourceNodeConfig> ResourceNodes { get; }
        public IReadOnlyList<MatchBossSpawnConfig> BossSpawns { get; }
        public int MinimumRoadWidth { get; }
        public IReadOnlyList<MatchResourceActivationWaveConfig> ActivationWaves { get; }
        public IReadOnlyList<MatchGathererConfig> Gatherers { get; }
        public int GathererDispatchIntervalTicks => GathererDispatchIntervalMinTicks;
        public int GathererDispatchIntervalMinTicks { get; }
        public int GathererDispatchIntervalMaxTicks { get; }
    }

    public sealed class MatchTacticEffectConfig
    {
        public MatchTacticEffectConfig(TacticEffectId id, TacticEffectKind kind, TacticTargetKind targetKind,
            IReadOnlyList<ResourceAmount> resourceAmounts, int magnitude, int radius, int durationTicks, int perMatchLimit)
        { Id = id; Kind = kind; TargetKind = targetKind; ResourceAmounts = P1SnapshotFreeze.Copy(resourceAmounts); Magnitude = magnitude; Radius = radius; DurationTicks = durationTicks; PerMatchLimit = perMatchLimit; }
        public TacticEffectId Id { get; }
        public TacticEffectKind Kind { get; }
        public TacticTargetKind TargetKind { get; }
        public IReadOnlyList<ResourceAmount> ResourceAmounts { get; }
        public int Magnitude { get; }
        public int Radius { get; }
        public int DurationTicks { get; }
        public int PerMatchLimit { get; }
    }

    public sealed class MatchTimedOfferConfig
    {
        public MatchTimedOfferConfig(int triggerSeconds, int candidateCount, IReadOnlyList<CardId> fallbackCardIds)
        { TriggerSeconds = triggerSeconds; CandidateCount = candidateCount; FallbackCardIds = P1SnapshotFreeze.Copy(fallbackCardIds); }
        public int TriggerSeconds { get; }
        public int CandidateCount { get; }
        public IReadOnlyList<CardId> FallbackCardIds { get; }
    }

    public sealed class MatchProcessedResourceBundleConfig
    {
        public MatchProcessedResourceBundleConfig(string id, string displayName, IReadOnlyList<ResourceAmount> amounts,
            RewardRarity rarity = RewardRarity.Common)
        { Id = id ?? string.Empty; DisplayName = displayName ?? string.Empty; Amounts = P1SnapshotFreeze.Copy(amounts); Rarity = rarity; }
        public string Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<ResourceAmount> Amounts { get; }
        public RewardRarity Rarity { get; }
    }

    public sealed class MatchReinforcementTemplateConfig
    {
        public MatchReinforcementTemplateConfig(ReinforcementTemplateId id, CardId cardId, string displayName,
            int minimumHeatTier, IReadOnlyList<UnitId> units, RewardRarity rarity = RewardRarity.Common)
        { Id = id; CardId = cardId; DisplayName = displayName ?? string.Empty; MinimumHeatTier = Math.Clamp(minimumHeatTier, 0, 3); Units = P1SnapshotFreeze.Copy(units); Rarity = rarity; }
        public ReinforcementTemplateId Id { get; }
        public CardId CardId { get; }
        public string DisplayName { get; }
        public int MinimumHeatTier { get; }
        public IReadOnlyList<UnitId> Units { get; }
        public RewardRarity Rarity { get; }
    }

    public sealed class MatchRewardRarityWeights
    {
        public MatchRewardRarityWeights(IReadOnlyList<int> common, IReadOnlyList<int> rare, IReadOnlyList<int> epic)
        { Common = P1SnapshotFreeze.Copy(common); Rare = P1SnapshotFreeze.Copy(rare); Epic = P1SnapshotFreeze.Copy(epic); }
        public IReadOnlyList<int> Common { get; }
        public IReadOnlyList<int> Rare { get; }
        public IReadOnlyList<int> Epic { get; }
        public int GetWeight(int heatTier, RewardRarity rarity)
        {
            var source = rarity switch { RewardRarity.Common => Common, RewardRarity.Rare => Rare, _ => Epic };
            return source.Count == 0 ? 0 : Math.Max(0, source[Math.Clamp(heatTier, 0, source.Count - 1)]);
        }
    }

    public sealed class MatchHandAndOffersConfig
    {
        public static MatchHandAndOffersConfig Empty { get; } = new(6, Array.Empty<CardId>(), Array.Empty<CardId>(),
            Array.Empty<MatchTimedOfferConfig>(), true, Array.Empty<MatchTacticEffectConfig>());
        public MatchHandAndOffersConfig(int handLimit, IReadOnlyList<CardId> guaranteedCards, IReadOnlyList<CardId> fillerCards,
            IReadOnlyList<MatchTimedOfferConfig> offers, ResourceAmount fullHandExchange,
            IReadOnlyList<MatchTacticEffectConfig> tacticEffects)
            : this(handLimit, guaranteedCards, fillerCards, offers, true, tacticEffects) { FullHandExchange = fullHandExchange; }
        public MatchHandAndOffersConfig(int handLimit, IReadOnlyList<CardId> guaranteedCards, IReadOnlyList<CardId> fillerCards,
            IReadOnlyList<MatchTimedOfferConfig> offers, bool allowFullHandDiscard,
            IReadOnlyList<MatchTacticEffectConfig> tacticEffects)
            : this(handLimit, guaranteedCards, fillerCards, offers, allowFullHandDiscard, tacticEffects,
                Array.Empty<MatchProcessedResourceBundleConfig>(), Array.Empty<MatchReinforcementTemplateConfig>()) { }
        public MatchHandAndOffersConfig(int handLimit, IReadOnlyList<CardId> guaranteedCards, IReadOnlyList<CardId> fillerCards,
            IReadOnlyList<MatchTimedOfferConfig> offers, bool allowFullHandDiscard,
            IReadOnlyList<MatchTacticEffectConfig> tacticEffects,
            IReadOnlyList<MatchProcessedResourceBundleConfig> processedResourceBundles,
            IReadOnlyList<MatchReinforcementTemplateConfig> reinforcementTemplates,
            MatchRewardRarityWeights rarityWeights = null, ResourceKey buildingRewardArt = default,
            ResourceKey resourceRewardArt = default, ResourceKey reinforcementRewardArt = default)
        { HandLimit = handLimit; GuaranteedCards = P1SnapshotFreeze.Copy(guaranteedCards); FillerCards = P1SnapshotFreeze.Copy(fillerCards); Offers = P1SnapshotFreeze.Copy(offers); AllowFullHandDiscard = allowFullHandDiscard; TacticEffects = P1SnapshotFreeze.Copy(tacticEffects); ProcessedResourceBundles = P1SnapshotFreeze.Copy(processedResourceBundles); ReinforcementTemplates = P1SnapshotFreeze.Copy(reinforcementTemplates); RarityWeights = rarityWeights ?? new MatchRewardRarityWeights(new[] { 100 }, new[] { 0 }, new[] { 0 }); BuildingRewardArt = buildingRewardArt; ResourceRewardArt = resourceRewardArt; ReinforcementRewardArt = reinforcementRewardArt; }
        public int HandLimit { get; }
        public IReadOnlyList<CardId> GuaranteedCards { get; }
        public IReadOnlyList<CardId> FillerCards { get; }
        public IReadOnlyList<MatchTimedOfferConfig> Offers { get; }
        public ResourceAmount FullHandExchange { get; }
        public bool AllowFullHandDiscard { get; }
        public IReadOnlyList<MatchTacticEffectConfig> TacticEffects { get; }
        public IReadOnlyList<MatchProcessedResourceBundleConfig> ProcessedResourceBundles { get; }
        public IReadOnlyList<MatchReinforcementTemplateConfig> ReinforcementTemplates { get; }
        public MatchRewardRarityWeights RarityWeights { get; }
        public ResourceKey BuildingRewardArt { get; }
        public ResourceKey ResourceRewardArt { get; }
        public ResourceKey ReinforcementRewardArt { get; }
    }

    public sealed class MatchResearchUpgradeConfig
    {
        public MatchResearchUpgradeConfig(ResearchUpgradeId id, ResearchCategory targetRole,
            IReadOnlyList<MatchResearchModifierConfig> modifiers, int maxRank, ResourceKey presentationKey)
        { Id = id; TargetRole = targetRole; Modifiers = P1SnapshotFreeze.Copy(modifiers); MaxRank = Math.Max(1, maxRank); PresentationKey = presentationKey; }
        public ResearchUpgradeId Id { get; }
        public ResearchCategory TargetRole { get; }
        public IReadOnlyList<MatchResearchModifierConfig> Modifiers { get; }
        public int MaxRank { get; }
        public ResourceKey PresentationKey { get; }
    }

    public readonly struct MatchResearchModifierConfig
    {
        public MatchResearchModifierConfig(string propertyKey, int percentPerRankBasisPoints)
        { PropertyKey = propertyKey ?? string.Empty; PercentPerRankBasisPoints = Math.Max(0, percentPerRankBasisPoints); }
        public string PropertyKey { get; }
        public int PercentPerRankBasisPoints { get; }
    }

    public sealed class MatchResearchConfig
    {
        public static MatchResearchConfig Empty { get; } = new(string.Empty, Array.Empty<MatchResearchUpgradeConfig>(), Array.Empty<ResourceAmount>(), 1, 3);
        public MatchResearchConfig(string bagId, IReadOnlyList<MatchResearchUpgradeConfig> upgrades, IReadOnlyList<ResourceAmount> costs,
            int researchTicks, int candidateCount)
        { BagId = bagId; Upgrades = P1SnapshotFreeze.Copy(upgrades); Costs = P1SnapshotFreeze.Copy(costs); ResearchTicks = Math.Max(1, researchTicks); CandidateCount = Math.Clamp(candidateCount, 1, 3); }
        public string BagId { get; }
        public IReadOnlyList<MatchResearchUpgradeConfig> Upgrades { get; }
        public IReadOnlyList<ResourceAmount> Costs { get; }
        public int ResearchTicks { get; }
        public int CandidateCount { get; }
    }

    public sealed class MatchBossConfig
    {
        public static MatchBossConfig Empty { get; } = new(default, 1, 0, 0, 1, 0, 1, 0, 0, 0, 250,
            Array.Empty<MatchBossRewardConfig>(), Array.Empty<MatchBossRewardConfig>(), 700);
        public MatchBossConfig(BossId id, int maxHealth, int armor, int attackDamage, int attackIntervalTicks, int movePerTick,
            int collisionRadius, int acquireRadius, int leashRadius, int returnArmorPerTick, int rewardCoreLifetimeTicks,
            IReadOnlyList<MatchBossRewardConfig> playerRewards, IReadOnlyList<MatchBossRewardConfig> enemyRewards, int rewardBudgetMilli)
        { Id = id; MaxHealth = maxHealth; Armor = armor; AttackDamage = attackDamage; AttackIntervalTicks = attackIntervalTicks; MovePerTick = movePerTick; CollisionRadius = collisionRadius; AcquireRadius = acquireRadius; LeashRadius = leashRadius; ReturnArmorPerTick = returnArmorPerTick; RewardCoreLifetimeTicks = rewardCoreLifetimeTicks; PlayerRewards = P1SnapshotFreeze.Copy(playerRewards); EnemyRewards = P1SnapshotFreeze.Copy(enemyRewards); RewardBudgetMilli = rewardBudgetMilli; }
        public BossId Id { get; }
        public int MaxHealth { get; }
        public int Armor { get; }
        public int AttackDamage { get; }
        public int AttackIntervalTicks { get; }
        public int MovePerTick { get; }
        public int CollisionRadius { get; }
        public int AcquireRadius { get; }
        public int LeashRadius { get; }
        public int ReturnArmorPerTick { get; }
        public int RewardCoreLifetimeTicks { get; }
        public IReadOnlyList<MatchBossRewardConfig> PlayerRewards { get; }
        public IReadOnlyList<MatchBossRewardConfig> EnemyRewards { get; }
        public int RewardBudgetMilli { get; }
    }

    public sealed class MatchBossRewardConfig
    {
        public MatchBossRewardConfig(string id, BossRewardKind kind, int weight, int magnitude, int durationTicks)
        { Id = id; Kind = kind; Weight = weight; Magnitude = magnitude; DurationTicks = durationTicks; }
        public string Id { get; }
        public BossRewardKind Kind { get; }
        public int Weight { get; }
        public int Magnitude { get; }
        public int DurationTicks { get; }
    }

    public sealed class MatchConstructionConfig
    {
        public static MatchConstructionConfig Empty { get; } = new(default, 0, 0, 1, 0, 1, 0, Array.Empty<ResourceAmount>(), 2, 3, 1, 80, 500);
        public MatchConstructionConfig(BuildingId towerBuildingId, int maxHealth, int attackDamage, int attackIntervalTicks,
            int attackRange, int projectileSpeedPerTick, int constructionTicks, IReadOnlyList<ResourceAmount> costs,
            int maxSites, int maxTowers, int maxBuilders, int builderRespawnTicks, int retainedProgressMilli)
        { TowerBuildingId = towerBuildingId; MaxHealth = maxHealth; AttackDamage = attackDamage; AttackIntervalTicks = attackIntervalTicks; AttackRange = attackRange; ProjectileSpeedPerTick = projectileSpeedPerTick; ConstructionTicks = constructionTicks; Costs = P1SnapshotFreeze.Copy(costs); MaxSites = maxSites; MaxTowers = maxTowers; MaxBuilders = maxBuilders; BuilderRespawnTicks = builderRespawnTicks; RetainedProgressMilli = retainedProgressMilli; }
        public BuildingId TowerBuildingId { get; }
        public int MaxHealth { get; }
        public int AttackDamage { get; }
        public int AttackIntervalTicks { get; }
        public int AttackRange { get; }
        public int ProjectileSpeedPerTick { get; }
        public int ConstructionTicks { get; }
        public IReadOnlyList<ResourceAmount> Costs { get; }
        public int MaxSites { get; }
        public int MaxTowers { get; }
        public int MaxBuilders { get; }
        public int BuilderRespawnTicks { get; }
        public int RetainedProgressMilli { get; }
    }

    public readonly struct MatchVirtualFacilityConfig
    {
        public MatchVirtualFacilityConfig(BuildingId buildingId, int level) { BuildingId = buildingId; Level = level; }
        public BuildingId BuildingId { get; }
        public int Level { get; }
    }

    public readonly struct MatchVirtualCampConfig
    {
        public MatchVirtualCampConfig(UnitId unitId, int slotCount) { UnitId = unitId; SlotCount = slotCount; }
        public UnitId UnitId { get; }
        public int SlotCount { get; }
    }

    public sealed class MatchEnemyFormationConfig
    {
        public MatchEnemyFormationConfig(string id, IReadOnlyList<UnitId> unitIds, IReadOnlyList<int> quantities)
            : this(id, unitIds, quantities, Array.Empty<string>())
        {
        }

        public MatchEnemyFormationConfig(string id, IReadOnlyList<UnitId> unitIds, IReadOnlyList<int> quantities,
            IReadOnlyList<string> allowedIntentIds)
        {
            Id = id ?? string.Empty;
            UnitIds = P1SnapshotFreeze.Copy(unitIds);
            Quantities = P1SnapshotFreeze.Copy(quantities);
            AllowedIntentIds = P1SnapshotFreeze.Copy(allowedIntentIds);
        }

        public string Id { get; }
        public IReadOnlyList<UnitId> UnitIds { get; }
        public IReadOnlyList<int> Quantities { get; }
        public IReadOnlyList<string> AllowedIntentIds { get; }
    }

    public sealed class MatchEnemyEconomyConfig
    {
        public static MatchEnemyEconomyConfig Empty { get; } = new(Array.Empty<ResourceAmount>(), Array.Empty<MatchVirtualFacilityConfig>(), Array.Empty<MatchVirtualCampConfig>(), Array.Empty<CardId>(), Array.Empty<MatchEnemyFormationConfig>(), "", 0, 1, 1, 80, 1000, 1000);
        public MatchEnemyEconomyConfig(IReadOnlyList<ResourceAmount> initialInventory, IReadOnlyList<MatchVirtualFacilityConfig> facilities,
            IReadOnlyList<MatchVirtualCampConfig> camps, IReadOnlyList<CardId> initialHand, IReadOnlyList<MatchEnemyFormationConfig> formations,
            string defenseReserveFormationId, int reserveRatioMilli, int gatherCycleTicks, int processingCycleTicks, int builderRespawnTicks,
            int trainingTimeMultiplierMilli, int economicEfficiencyMilli)
        { InitialInventory = P1SnapshotFreeze.Copy(initialInventory); Facilities = P1SnapshotFreeze.Copy(facilities); Camps = P1SnapshotFreeze.Copy(camps); InitialHand = P1SnapshotFreeze.Copy(initialHand); Formations = P1SnapshotFreeze.Copy(formations); DefenseReserveFormationId = defenseReserveFormationId ?? string.Empty; ReserveRatioMilli = reserveRatioMilli; GatherCycleTicks = gatherCycleTicks; ProcessingCycleTicks = processingCycleTicks; BuilderRespawnTicks = builderRespawnTicks; TrainingTimeMultiplierMilli = trainingTimeMultiplierMilli; EconomicEfficiencyMilli = economicEfficiencyMilli; }
        public IReadOnlyList<ResourceAmount> InitialInventory { get; }
        public IReadOnlyList<MatchVirtualFacilityConfig> Facilities { get; }
        public IReadOnlyList<MatchVirtualCampConfig> Camps { get; }
        public IReadOnlyList<CardId> InitialHand { get; }
        public IReadOnlyList<MatchEnemyFormationConfig> Formations { get; }
        public string DefenseReserveFormationId { get; }
        public int ReserveRatioMilli { get; }
        public int GatherCycleTicks { get; }
        public int ProcessingCycleTicks { get; }
        public int BuilderRespawnTicks { get; }
        public int TrainingTimeMultiplierMilli { get; }
        public int EconomicEfficiencyMilli { get; }
    }

    public readonly struct MatchAiFeatureCoefficient
    {
        public MatchAiFeatureCoefficient(string featureId, string intentId, int coefficient) { FeatureId = featureId ?? string.Empty; IntentId = intentId ?? string.Empty; Coefficient = coefficient; }
        public string FeatureId { get; }
        public string IntentId { get; }
        public int Coefficient { get; }
    }

    public readonly struct MatchAiCommitmentConfig
    {
        public MatchAiCommitmentConfig(string intentId, int minimumTicks, AiCommitmentPolicy policy) { IntentId = intentId ?? string.Empty; MinimumTicks = minimumTicks; Policy = policy; }
        public string IntentId { get; }
        public int MinimumTicks { get; }
        public AiCommitmentPolicy Policy { get; }
    }

    public sealed class MatchAiStrategyConfig
    {
        public static MatchAiStrategyConfig Empty { get; } = new(
            "", "", 1000, 15, 0, 0, 600, 800, 1000, 800, 80, 120, 180, 1,
            Array.Empty<MatchIntentWeightConfig>(), Array.Empty<MatchAiFeatureCoefficient>(),
            Array.Empty<MatchAiCommitmentConfig>());

        public MatchAiStrategyConfig(string doctrineId, string difficultyId, int decisionQualityMilli,
            int reactionDelayTicks, int suboptimalIntervalMinTicks, int suboptimalIntervalMaxTicks,
            int firstProbeStartTick, int firstProbeEndTick, int trainingTimeMultiplierMilli,
            int temperatureMilli, int decisionIntervalTicks, int switchCost, int repetitionPenalty,
            int softmaxLookupVersion, IReadOnlyList<MatchIntentWeightConfig> doctrineBiases,
            IReadOnlyList<MatchAiFeatureCoefficient> featureCoefficients,
            IReadOnlyList<MatchAiCommitmentConfig> commitments)
            : this(doctrineId, difficultyId, decisionQualityMilli, reactionDelayTicks,
                suboptimalIntervalMinTicks, suboptimalIntervalMaxTicks, firstProbeStartTick, firstProbeEndTick,
                trainingTimeMultiplierMilli, temperatureMilli, decisionIntervalTicks, switchCost,
                repetitionPenalty, softmaxLookupVersion, 550, 650, 750, 22, 8, 9000, 2000,
                300, 2, 2, 2,
                doctrineBiases, featureCoefficients, commitments)
        {
        }

        public MatchAiStrategyConfig(string doctrineId, string difficultyId, int decisionQualityMilli,
            int reactionDelayTicks, int suboptimalIntervalMinTicks, int suboptimalIntervalMaxTicks,
            int firstProbeStartTick, int firstProbeEndTick, int trainingTimeMultiplierMilli,
            int temperatureMilli, int decisionIntervalTicks, int switchCost, int repetitionPenalty,
            int softmaxLookupVersion, int pressureMinIntervalTicks, int pressureTargetIntervalTicks,
            int pressureMaxIntervalTicks, int activeUnitSoftCap, int queuedUnitSoftCap,
            int publicAccelerationStartTick, int publicProductionMultiplierMilli,
            int logisticsThreatMemoryTicks, int maxConcurrentLogisticsResponses,
            int emergencyDefenseOverflowUnits, int towerEscalationKillCount,
            IReadOnlyList<MatchIntentWeightConfig> doctrineBiases,
            IReadOnlyList<MatchAiFeatureCoefficient> featureCoefficients,
            IReadOnlyList<MatchAiCommitmentConfig> commitments)
        {
            DoctrineId = doctrineId ?? string.Empty;
            DifficultyId = difficultyId ?? string.Empty;
            DecisionQualityMilli = decisionQualityMilli;
            ReactionDelayTicks = Math.Max(15, reactionDelayTicks);
            SuboptimalIntervalMinTicks = Math.Max(0, suboptimalIntervalMinTicks);
            SuboptimalIntervalMaxTicks = Math.Max(SuboptimalIntervalMinTicks, suboptimalIntervalMaxTicks);
            FirstProbeStartTick = firstProbeStartTick;
            FirstProbeEndTick = firstProbeEndTick;
            TrainingTimeMultiplierMilli = trainingTimeMultiplierMilli;
            TemperatureMilli = temperatureMilli;
            DecisionIntervalTicks = decisionIntervalTicks;
            SwitchCost = switchCost;
            RepetitionPenalty = repetitionPenalty;
            SoftmaxLookupVersion = softmaxLookupVersion;
            PressureMinIntervalTicks = Math.Max(1, pressureMinIntervalTicks);
            PressureTargetIntervalTicks = Math.Max(PressureMinIntervalTicks, pressureTargetIntervalTicks);
            PressureMaxIntervalTicks = Math.Max(PressureTargetIntervalTicks, pressureMaxIntervalTicks);
            ActiveUnitSoftCap = Math.Max(1, activeUnitSoftCap);
            QueuedUnitSoftCap = Math.Max(1, queuedUnitSoftCap);
            PublicAccelerationStartTick = Math.Max(0, publicAccelerationStartTick);
            PublicProductionMultiplierMilli = Math.Max(1000, publicProductionMultiplierMilli);
            LogisticsThreatMemoryTicks = Math.Max(1, logisticsThreatMemoryTicks);
            MaxConcurrentLogisticsResponses = Math.Clamp(maxConcurrentLogisticsResponses, 1, 2);
            EmergencyDefenseOverflowUnits = Math.Max(0, emergencyDefenseOverflowUnits);
            TowerEscalationKillCount = Math.Max(1, towerEscalationKillCount);
            DoctrineBiases = P1SnapshotFreeze.Copy(doctrineBiases);
            FeatureCoefficients = P1SnapshotFreeze.Copy(featureCoefficients);
            Commitments = P1SnapshotFreeze.Copy(commitments);
        }

        public string DoctrineId { get; }
        public string DifficultyId { get; }
        public int DecisionQualityMilli { get; }
        public int ReactionDelayTicks { get; }
        public int SuboptimalIntervalMinTicks { get; }
        public int SuboptimalIntervalMaxTicks { get; }
        public int FirstProbeStartTick { get; }
        public int FirstProbeEndTick { get; }
        public int TrainingTimeMultiplierMilli { get; }
        public int TemperatureMilli { get; }
        public int DecisionIntervalTicks { get; }
        public int SwitchCost { get; }
        public int RepetitionPenalty { get; }
        public int SoftmaxLookupVersion { get; }
        public int PressureMinIntervalTicks { get; }
        public int PressureTargetIntervalTicks { get; }
        public int PressureMaxIntervalTicks { get; }
        public int ActiveUnitSoftCap { get; }
        public int QueuedUnitSoftCap { get; }
        public int PublicAccelerationStartTick { get; }
        public int PublicProductionMultiplierMilli { get; }
        public int LogisticsThreatMemoryTicks { get; }
        public int MaxConcurrentLogisticsResponses { get; }
        public int EmergencyDefenseOverflowUnits { get; }
        public int TowerEscalationKillCount { get; }
        public IReadOnlyList<MatchIntentWeightConfig> DoctrineBiases { get; }
        public IReadOnlyList<MatchAiFeatureCoefficient> FeatureCoefficients { get; }
        public IReadOnlyList<MatchAiCommitmentConfig> Commitments { get; }
    }
}
