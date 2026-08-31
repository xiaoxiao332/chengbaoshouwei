using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Resources;
using FortressFrontier.Runtime.Progression;

namespace FortressFrontier.Runtime.Content
{
    public sealed class ContentConfigSystem : GameSystemBase, IProgressionContent, IMatchContent, ISelectionContent
    {
        private readonly IResourceService _resources;
        private readonly ResourceKey _configKey;
        private IAssetLease<GameContentConfig> _configLease;
        private IReadOnlyList<ProgressionCardDefinition> _progressionCards;
        private IReadOnlyList<ProgressionStageDefinition> _progressionStages;
        private IReadOnlyList<SelectionBattlefieldDefinition> _selectionBattlefields;
        private IReadOnlyDictionary<CardId, ResourceKey> _selectionCardArt;
        private Dictionary<string, HashSet<string>> _purchasableCardsByStage;

        public ContentConfigSystem(IResourceService resources, ResourceKey configKey)
            : base(SystemLifetime.Global)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _configKey = configKey;
        }

        public int SchemaVersion { get; private set; }
        public ContentValidationReport LastValidationReport { get; private set; }
        public int InitialGold => GetConfig().ProgressionConfig.InitialGold;
        public CampaignStageId InitialCampaignStageId => new(GetConfig().ProgressionConfig.InitialCampaignStageId);
        public IReadOnlyList<ProgressionCardDefinition> Cards => _progressionCards ?? throw new InvalidOperationException("ContentConfigSystem is not initialized.");
        public IReadOnlyList<ProgressionStageDefinition> Stages => _progressionStages ?? throw new InvalidOperationException("ContentConfigSystem is not initialized.");
        public IReadOnlyList<SelectionBattlefieldDefinition> Battlefields => _selectionBattlefields ?? throw new InvalidOperationException("ContentConfigSystem is not initialized.");
        public IReadOnlyDictionary<CardId, ResourceKey> CardArt => _selectionCardArt ?? throw new InvalidOperationException("ContentConfigSystem is not initialized.");

        protected override async Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _configLease = await _resources.AcquireAsync<GameContentConfig>(_configKey, cancellationToken);
            LastValidationReport = ContentConfigValidator.Validate(_configLease.Asset);
            if (!LastValidationReport.IsValid) throw new ContentConfigException(LastValidationReport);
            SchemaVersion = _configLease.Asset.SchemaVersion;
            BuildProgressionView(_configLease.Asset);
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _configLease?.Dispose();
            _configLease = null;
            SchemaVersion = 0;
            LastValidationReport = null;
            _progressionCards = null;
            _progressionStages = null;
            _selectionBattlefields = null;
            _selectionCardArt = null;
            _purchasableCardsByStage = null;
            return Task.CompletedTask;
        }

        public bool IsCardPurchasable(CampaignStageId stageId, CardId cardId)
        {
            if (_purchasableCardsByStage == null) throw new InvalidOperationException("ContentConfigSystem is not initialized.");
            return _purchasableCardsByStage.TryGetValue(stageId.Value, out var cards) && cards.Contains(cardId.Value);
        }

        public MatchConfigSnapshot CreateMatchSnapshot(BattlefieldId battlefieldId, MapModeId mapModeId)
            => CreateMatchSnapshot(battlefieldId, mapModeId, 1);

        public MatchConfigSnapshot CreateMatchSnapshot(BattlefieldId battlefieldId, MapModeId mapModeId, int seed)
        {
            var config = GetConfig();
            var battlefield = config.BattlefieldCatalog.Definitions.SingleOrDefault(value => value.Id == battlefieldId.Value)
                ?? throw new KeyNotFoundException($"Unknown battlefield: '{battlefieldId}'.");
            if (!battlefield.MapModeIds.Contains(mapModeId.Value))
                throw new InvalidOperationException($"Map mode '{mapModeId}' is not available for battlefield '{battlefieldId}'.");

            var mode = config.StageEffectCatalog.MapModes.Single(value => value.Id == mapModeId.Value);
            var presentationById = config.PresentationCatalog.Definitions.ToDictionary(
                value => value.Id, value => new ResourceKey(value.ResourceKey), StringComparer.Ordinal);
            var phaseProfile = config.StageEffectCatalog.AiPhaseProfiles.Single(value => value.Id == mode.AiPhaseProfileId);
            var reward = config.RewardCatalog.Definitions.Single(value => value.Id == mode.RewardTableId);
            var soldierCards = config.CardCatalog.Definitions
                .Where(value => value.Type == CardType.Soldier)
                .ToDictionary(value => value.LinkedContentId, value => new CardId(value.Id), StringComparer.Ordinal);

            var resources = config.ResourceCatalog.Definitions
                .Where(value => value.Scope == ResourceScope.Match)
                .Select(value => new MatchResourceConfig(new ResourceId(value.Id), value.DefaultCapacity, value.CanOverflow,
                    value.AcquisitionKind))
                .ToArray();
            var buildings = config.BuildingCatalog.Definitions.Select(value => new MatchBuildingConfig(
                new BuildingId(value.Id), new CardId(value.SourceCardId), value.Category,
                MatchContentConversion.ToAmounts(value.Inputs), MatchContentConversion.ToAmounts(value.Outputs),
                string.IsNullOrWhiteSpace(value.WorkerUnitId) ? null : new UnitId(value.WorkerUnitId),
                string.IsNullOrWhiteSpace(value.ActivatedSoldierCardId) ? null : new CardId(value.ActivatedSoldierCardId),
                value.ProductionCycleTicks, value.WorkerGatherTicks,
                value.UpgradeLevels.Select(upgrade => new MatchUpgradeConfig(
                    upgrade.Level, upgrade.RequiredEffectiveWorkCount,
                    string.IsNullOrWhiteSpace(upgrade.RequiredMatchPhaseId) ? null : new MatchPhaseId(upgrade.RequiredMatchPhaseId),
                    new ResourceAmount(new ResourceId(upgrade.PaymentResourceId), upgrade.Cost),
                    Math.Max(0, (int)Math.Ceiling(upgrade.DurationSeconds * ContentConstants.FixedTicksPerSecond)),
                    upgrade.ProductionMultiplierMilli, upgrade.TrainingTimeMultiplierMilli)).ToArray(),
                value.GathererAllowedResourceIds.Select(id => new ResourceId(id)).ToArray(),
                MatchContentConversion.ToAmounts(value.GathererDispatchCosts),
                value.GathererDispatchIntervalTicks, value.GathererCarryAmount,
                value.GathererResourceSelectionPolicy,
                MatchContentConversion.ToAmounts(value.InputReserveFloors))).ToArray();
            var units = config.UnitCatalog.Definitions
                .Select(value => new MatchUnitConfig(new UnitId(value.Id),
                    soldierCards.TryGetValue(value.Id, out var soldierCard) ? soldierCard : default,
                    MatchContentConversion.ToAmounts(value.TrainingCosts),
                    Math.Max(1, (int)Math.Ceiling(value.BaseTrainingSeconds * ContentConstants.FixedTicksPerSecond)),
                    value.MaxHealth, value.AttackDamage, value.WallDamageMultiplierMilli, value.MovePerTick,
                    value.CollisionRadius, value.AcquireRadius, value.ChaseRadius, value.AttackRange,
                    value.AttackIntervalTicks, value.ProjectileSpeedPerTick, value.TargetPriority, value.CanAttack,
                    value.ResearchCategory, value.ProjectileKind, value.ExplosionRadius,
                    value.ExplosionSecondaryDamageMilli,
                    string.IsNullOrWhiteSpace(value.ProjectilePresentationKey)
                        ? default : presentationById[value.ProjectilePresentationKey]))
                .ToArray();
            var phases = phaseProfile.Phases.OrderBy(value => value.StartTick)
                .Select(value => new MatchPhaseConfig(new MatchPhaseId(value.Id), value.StartTick,
                    value.AllowedIntentIds, value.BaseIntentWeights.Select(weight =>
                        new MatchIntentWeightConfig(weight.IntentId, weight.Weight)).ToArray(),
                    phaseProfile.PublicAccelerationStartTick,
                    phaseProfile.PublicProductionMultiplierMilli)).ToArray();

            MatchPoint Point(ReferencePointDefinition value) => value == null
                ? default
                : new MatchPoint(value.Id, value.X, value.Y);
            var resourceWaves = config.StageEffectCatalog.ResourceActivationWaves
                .Where(value => value.MapModeId == mapModeId.Value)
                .OrderBy(value => value.TriggerSeconds)
                .Select(value => new MatchResourceActivationWaveConfig(value.Id,
                    value.TriggerSeconds * ContentConstants.FixedTicksPerSecond, value.NodesPerGroup,
                    value.Groups.ToArray(), value.AllowedResourceIds.Select(id => new ResourceId(id)).ToArray()))
                .ToArray();
            var unitsById = units.ToDictionary(value => value.Id);
            var gateResources = new[]
            {
                new ResourceId(ContentConstants.FoodResourceId),
                new ResourceId(ContentConstants.WoodResourceId),
                new ResourceId(ContentConstants.RawStoneResourceId)
            };
            ShuffleGateResources(gateResources, seed);
            var gathererDefinitions = battlefield.Gatherers.OrderBy(value => RouteOrdinal(value.RouteId))
                .ThenBy(value => value.RouteId, StringComparer.Ordinal).ToArray();
            var gateResourceByOrdinal = gateResources.Select((resourceId, index) => (resourceId, index))
                .ToDictionary(value => value.index, value => value.resourceId);
            MatchResourceNodeConfig ResourceNode(ResourceNodeDefinition value)
            {
                var allowed = value.AllowedResourceIds.Select(id => new ResourceId(id)).ToArray();
                if (value.SpawnGroup != ResourceNodeSpawnGroup.Central)
                {
                    var suffix = value.Id.LastIndexOf('-');
                    if (suffix >= 0 && int.TryParse(value.Id[(suffix + 1)..], out var lane) &&
                        gateResourceByOrdinal.TryGetValue(lane, out var assigned))
                        allowed = new[] { assigned };
                }
                return new MatchResourceNodeConfig(new ResourceNodeId(value.Id), Point(value.Position), value.Capacity,
                    value.SpawnGroup, value.MirrorNodeId, allowed, value.RespawnCapacity, value.RespawnDelayTicks);
            }
            var layout = new MatchBattlefieldLayoutConfig(battlefield.ReferenceWidth, battlefield.ReferenceHeight,
                battlefield.Zones.Select(value => new MatchRect(value.Id, value.Kind, value.X, value.Y, value.Width, value.Height)).ToArray(),
                battlefield.Routes.Select(value => new MatchRouteConfig(new RouteId(value.Id), value.Points.Select(Point).ToArray())).ToArray(),
                battlefield.ResourceNodes.Select(ResourceNode).ToArray(),
                battlefield.BossSpawns.Select(value => new MatchBossSpawnConfig(value.Id, Point(value.Position), value.WarningTick, value.SpawnTick)).ToArray(),
                battlefield.MinimumRoadWidth, resourceWaves,
                gathererDefinitions.Select((value, index) =>
                {
                    var resourceId = gateResources[index];
                    var workerId = resourceId.Value switch
                    {
                        ContentConstants.WoodResourceId => new UnitId("unit.lumberjack"),
                        ContentConstants.RawStoneResourceId => new UnitId("unit.stonecutter"),
                        _ => new UnitId("unit.gatherer")
                    };
                    var unit = unitsById[workerId];
                    return new MatchGathererConfig(new GathererSourceId(value.SourceId), new RouteId(value.RouteId),
                        unit.Id, new[] { resourceId },
                        value.CarryAmount, value.GatherTicks, unit.MovePerTick, unit.MaxHealth,
                        Array.Empty<ResourceAmount>(), battlefield.GathererDispatchIntervalMinTicks,
                        battlefield.GathererDispatchIntervalMaxTicks,
                        GathererResourceSelectionPolicy.Fixed, default);
                }).ToArray(), battlefield.GathererDispatchIntervalMinTicks,
                battlefield.GathererDispatchIntervalMaxTicks);
            var tacticEffects = config.CardCatalog.TacticEffects.Select(value => new MatchTacticEffectConfig(
                new TacticEffectId(value.Id), value.Kind, value.TargetKind, MatchContentConversion.ToAmounts(value.ResourceAmounts),
                value.Magnitude, value.Radius, value.DurationTicks, value.PerMatchLimit)).ToArray();
            var reinforcementCardsByTemplate = config.CardCatalog.Definitions
                .Where(value => value.Type == CardType.ReinforcementItem)
                .ToDictionary(value => value.LinkedContentId, value => new CardId(value.Id), StringComparer.Ordinal);
            var hand = new MatchHandAndOffersConfig(reward.HandLimit,
                battlefield.InitialHand.GuaranteedCardIds.Select(value => new CardId(value)).ToArray(),
                battlefield.InitialHand.FillerCardIds.Select(value => new CardId(value)).ToArray(),
                reward.TimedCardOffers.Select(value => new MatchTimedOfferConfig(value.TriggerSeconds, value.CandidateCount,
                    value.FallbackCardIds.Select(cardId => new CardId(cardId)).ToArray())).ToArray(),
                reward.AllowFullHandDiscard, tacticEffects,
                reward.ProcessedResourceBundles.Select(value => new MatchProcessedResourceBundleConfig(
                    value.Id, value.DisplayName, MatchContentConversion.ToAmounts(value.Amounts), value.Rarity)).ToArray(),
                reward.ReinforcementTemplates.Select(value => new MatchReinforcementTemplateConfig(
                    new ReinforcementTemplateId(value.Id), reinforcementCardsByTemplate[value.Id], value.DisplayName, value.MinimumHeatTier,
                    value.Units.SelectMany(unit => Enumerable.Repeat(new UnitId(unit.UnitId), unit.Quantity)).ToArray(), value.Rarity)).ToArray(),
                new MatchRewardRarityWeights(
                    reward.RarityWeights.Single(value => value.Rarity == RewardRarity.Common).HeatTierWeights.ToArray(),
                    reward.RarityWeights.Single(value => value.Rarity == RewardRarity.Rare).HeatTierWeights.ToArray(),
                    reward.RarityWeights.Single(value => value.Rarity == RewardRarity.Epic).HeatTierWeights.ToArray()),
                presentationById[reward.BuildingRewardPresentationKey],
                presentationById[reward.ResourceRewardPresentationKey],
                presentationById[reward.ReinforcementRewardPresentationKey]);
            var researchBuilding = config.BuildingCatalog.Definitions.First(value => value.Category == BuildingCategory.Research);
            var researchBag = config.BuildingCatalog.ResearchBags.Single(value => value.Id == researchBuilding.ResearchBagId);
            var researchIds = new HashSet<string>(researchBag.UpgradeIds, StringComparer.Ordinal);
            var research = new MatchResearchConfig(researchBag.Id, config.BuildingCatalog.ResearchUpgrades
                .Where(value => researchIds.Contains(value.Id))
                .Select(value => new MatchResearchUpgradeConfig(new ResearchUpgradeId(value.Id), value.TargetRole,
                    value.Modifiers.Select(modifier => new MatchResearchModifierConfig(modifier.PropertyKey,
                        modifier.PercentPerRankBasisPoints)).ToArray(), value.MaxRank,
                    presentationById[value.PresentationKey])).ToArray(), MatchContentConversion.ToAmounts(researchBag.Costs),
                researchBag.ResearchTicks, researchBag.CandidateCount);
            var bossDefinition = config.BossCatalog.Definitions.Single(value => value.Id == battlefield.BossId);
            MatchBossRewardConfig BossReward(BossRewardEntryDefinition value) =>
                new(value.Id, value.Kind, value.Weight, value.Magnitude, value.DurationTicks);
            var boss = new MatchBossConfig(new BossId(bossDefinition.Id), bossDefinition.MaxHealth, bossDefinition.Armor,
                bossDefinition.AttackDamage, bossDefinition.AttackIntervalTicks, bossDefinition.MovePerTick,
                bossDefinition.CollisionRadius, bossDefinition.AcquireRadius, bossDefinition.LeashRadius,
                bossDefinition.ReturnArmorPerTick, bossDefinition.RewardCoreLifetimeTicks,
                reward.PlayerBossRewards.Select(BossReward).ToArray(), reward.EnemyBossRewards.Select(BossReward).ToArray(),
                reward.BossRewardBudgetMilli);
            var tower = config.BuildingCatalog.Definitions.Single(value => value.Category == BuildingCategory.BattlefieldStructure);
            var construction = new MatchConstructionConfig(new BuildingId(tower.Id), tower.MaxHealth, tower.AttackDamage,
                tower.AttackIntervalTicks, tower.AttackRange, tower.ProjectileSpeedPerTick, tower.ConstructionTicks,
                MatchContentConversion.ToAmounts(tower.ConstructionCosts), battlefield.MaxConstructionSites,
                battlefield.MaxCompletedTowers, battlefield.MaxActiveBuilders, battlefield.BuilderRespawnTicks,
                battlefield.RetainedConstructionProgressMilli);
            var enemyDefinition = config.StageEffectCatalog.EnemyEconomyProfiles.Single(value => value.Id == mode.EnemyEconomyProfileId);
            var enemyEconomy = new MatchEnemyEconomyConfig(MatchContentConversion.ToAmounts(enemyDefinition.InitialInventory),
                enemyDefinition.Facilities.Select(value => new MatchVirtualFacilityConfig(new BuildingId(value.BuildingId), value.Level)).ToArray(),
                enemyDefinition.Camps.Select(value => new MatchVirtualCampConfig(new UnitId(value.UnitId), value.SlotCount)).ToArray(),
                enemyDefinition.InitialHandCardIds.Select(value => new CardId(value)).ToArray(),
                enemyDefinition.Formations.Select(value => new MatchEnemyFormationConfig(value.Id,
                    value.UnitIds.Select(id => new UnitId(id)).ToArray(), value.Quantities.ToArray(),
                    value.AllowedIntentIds.ToArray())).ToArray(), enemyDefinition.DefenseReserveFormationId, enemyDefinition.ReserveRatioMilli,
                enemyDefinition.GatherCycleTicks, enemyDefinition.ProcessingCycleTicks, enemyDefinition.BuilderRespawnTicks,
                enemyDefinition.TrainingTimeMultiplierMilli, enemyDefinition.EconomicEfficiencyMilli);
            var utility = config.StageEffectCatalog.AiUtilityProfiles.Single(value => value.Id == mode.AiUtilityProfileId);
            var doctrine = config.StageEffectCatalog.AiDoctrines.Single(value => value.Id == mode.AiDoctrineId);
            var difficulty = config.StageEffectCatalog.DifficultyRules.Single(value => value.Id == mode.DifficultyRulesId);
            var aiStrategy = new MatchAiStrategyConfig(doctrine.Id, difficulty.Id, difficulty.DecisionQualityMilli,
                difficulty.ReactionDelayTicks, difficulty.SuboptimalIntervalMinTicks, difficulty.SuboptimalIntervalMaxTicks,
                phaseProfile.FirstProbeStartTick, phaseProfile.FirstProbeEndTick, difficulty.TrainingTimeMultiplierMilli, utility.TemperatureMilli, utility.DecisionIntervalTicks,
                utility.SwitchCost, utility.RepetitionPenalty, utility.SoftmaxLookupVersion,
                utility.PressureMinIntervalTicks, utility.PressureTargetIntervalTicks, utility.PressureMaxIntervalTicks,
                utility.ActiveUnitSoftCap, utility.QueuedUnitSoftCap, phaseProfile.PublicAccelerationStartTick,
                phaseProfile.PublicProductionMultiplierMilli, utility.LogisticsThreatMemoryTicks,
                utility.MaxConcurrentLogisticsResponses, utility.EmergencyDefenseOverflowUnits,
                utility.TowerEscalationKillCount,
                doctrine.IntentBiases.Select(value => new MatchIntentWeightConfig(value.IntentId, value.Weight)).ToArray(),
                utility.FeatureCoefficients.Select(value => new MatchAiFeatureCoefficient(value.FeatureId, value.IntentId, value.Coefficient)).ToArray(),
                utility.Commitments.Select(value => new MatchAiCommitmentConfig(value.IntentId, value.MinimumTicks, value.Policy)).ToArray());
            var presentation = new MatchPresentationConfig(
                config.CardCatalog.Definitions.ToDictionary(value => new CardId(value.Id),
                    value => presentationById[value.PresentationKey]),
                config.BuildingCatalog.Definitions.ToDictionary(value => new BuildingId(value.Id),
                    value => presentationById[value.PresentationKey]),
                config.UnitCatalog.Definitions.Where(value => value.CanAttack).ToDictionary(
                    value => new UnitId(value.Id),
                    value => new MatchUnitPresentationConfig(new UnitId(value.Id),
                        presentationById[value.PresentationKey],
                        presentationById[value.PlayerWorldPrefabPresentationKey],
                        presentationById[value.EnemyWorldPrefabPresentationKey])),
                presentationById[battlefield.MapPresentationKey]);
            var heat = new MatchHeatConfig(config.StageEffectCatalog.HeatTiers.Select(value =>
                new MatchHeatTier(value.StartTick, value.RewardCooldownSeconds,
                    value.AiPressureIntervalMultiplierMilli, value.AdvancedUnitWeightMultiplierMilli)).ToArray());

            return new MatchConfigSnapshot(config.SchemaVersion, battlefieldId, mapModeId, resources,
                MatchContentConversion.ToAmounts(battlefield.InitialPlayerInventory), buildings, units, phases,
                new MatchRewardConfig(reward.CompletionGold, reward.VictoryGold, reward.FirstClearGold, mode.RewardMultiplierMilli),
                battlefield.DeploymentOrderTimeoutTicks,
                new MatchCombatConfig(units, new MatchWallConfig(battlefield.PlayerWall.Id, battlefield.PlayerWall.MaxHealth, Point(battlefield.PlayerWall.Gate)),
                    new MatchWallConfig(battlefield.EnemyWall.Id, battlefield.EnemyWall.MaxHealth, Point(battlefield.EnemyWall.Gate))),
                layout, hand, research, boss, construction, enemyEconomy, aiStrategy, seed, presentation,
                battlefield.DisplayName, mode.Kind, heat);
        }

        private static void ShuffleGateResources(ResourceId[] values, int seed)
        {
            unchecked
            {
                var state = (uint)(seed == 0 ? 1 : seed) ^ 0xA341316Cu;
                for (var index = values.Length - 1; index > 0; index--)
                {
                    state ^= state << 13;
                    state ^= state >> 17;
                    state ^= state << 5;
                    var selected = (int)(state % (uint)(index + 1));
                    (values[index], values[selected]) = (values[selected], values[index]);
                }
            }
        }

        private static int RouteOrdinal(string routeId) => routeId switch
        {
            "route.upper" => 0,
            "route.middle" => 1,
            "route.lower" => 2,
            _ => int.MaxValue
        };

        private void BuildProgressionView(GameContentConfig config)
        {
            var presentationById = config.PresentationCatalog.Definitions.ToDictionary(
                value => value.Id, value => new ResourceKey(value.ResourceKey), StringComparer.Ordinal);
            _progressionCards = config.CardCatalog.Definitions.Where(card => card.Type != CardType.ReinforcementItem).Select(card =>
            {
                var growth = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var rule in card.GrowthRules)
                {
                    if (rule != null && !string.IsNullOrWhiteSpace(rule.PropertyKey)) growth[rule.PropertyKey] = rule.PercentPerLevelBasisPoints;
                }
                return new ProgressionCardDefinition(
                    new CardId(card.Id), card.DefaultUnlocked, card.UnlockGoldCost, card.MaxMetaLevel,
                    card.UpgradeGoldCosts.ToArray(),
                    card.PrerequisiteCardIds.Select(id => new CardId(id)).ToArray(),
                    growth);
            }).ToArray();
            _selectionCardArt = config.CardCatalog.Definitions
                .Where(card => card.Type != CardType.ReinforcementItem)
                .ToDictionary(card => new CardId(card.Id), card => presentationById[card.PresentationKey]);

            _purchasableCardsByStage = config.StageEffectCatalog.CampaignStages.ToDictionary(
                stage => stage.Id,
                stage => new HashSet<string>(stage.PurchasableCardIds, StringComparer.Ordinal),
                StringComparer.Ordinal);
            _progressionStages = config.StageEffectCatalog.CampaignStages.Select(stage =>
                new ProgressionStageDefinition(new CampaignStageId(stage.Id),
                    string.IsNullOrWhiteSpace(stage.PrerequisiteStageId)
                        ? null
                        : new CampaignStageId(stage.PrerequisiteStageId),
                    stage.UnlockedBattlefieldIds.Select(id => new BattlefieldId(id)).ToArray())).ToArray();
            _selectionBattlefields = config.BattlefieldCatalog.Definitions.Select(battlefield =>
                new SelectionBattlefieldDefinition(new BattlefieldId(battlefield.Id), battlefield.DisplayName,
                    battlefield.MapModeIds.Select(id => new MapModeId(id)).ToArray(),
                    presentationById[battlefield.MapPresentationKey])).ToArray();
        }

        private GameContentConfig GetConfig()
        {
            return _configLease?.Asset ?? throw new InvalidOperationException("ContentConfigSystem is not initialized.");
        }
    }
}
