using System;
using System.Collections.Generic;
using System.Linq;

namespace FortressFrontier.Runtime.Content
{
    public enum ContentValidationCode
    {
        MissingCatalog,
        InvalidSchemaVersion,
        EmptyId,
        DuplicateId,
        MissingReference,
        InvalidReferenceType,
        InvalidResourceDefinition,
        InvalidUpgradeCost,
        InvalidCardProgression,
        InvalidCampBinding,
        InvalidInitialHand,
        InvalidOfferGuarantee,
        InvalidMapModes,
        InvalidAiConfiguration,
        InvalidCampaignStage,
        InvalidProductionConfiguration,
        InvalidDeploymentConfiguration,
        InvalidCombatConfiguration,
        InvalidLayoutConfiguration,
        InvalidResearchConfiguration,
        InvalidConstructionConfiguration,
        InvalidBossConfiguration
    }

    public sealed class ContentValidationIssue
    {
        public ContentValidationIssue(ContentValidationCode code, string path, string message)
        {
            Code = code;
            Path = path ?? string.Empty;
            Message = message ?? string.Empty;
        }

        public ContentValidationCode Code { get; }
        public string Path { get; }
        public string Message { get; }
        public override string ToString() => $"[{Code}] {Path}: {Message}";
    }

    public sealed class ContentValidationReport
    {
        private readonly List<ContentValidationIssue> _issues = new();
        public bool IsValid => _issues.Count == 0;
        public IReadOnlyList<ContentValidationIssue> Issues => _issues;
        internal void Add(ContentValidationCode code, string path, string message) =>
            _issues.Add(new ContentValidationIssue(code, path, message));
    }

    public sealed class ContentConfigException : InvalidOperationException
    {
        public ContentConfigException(ContentValidationReport report)
            : base(BuildMessage(report))
        {
            Report = report ?? throw new ArgumentNullException(nameof(report));
        }

        public ContentValidationReport Report { get; }

        private static string BuildMessage(ContentValidationReport report)
        {
            if (report == null) return "Content configuration validation failed.";
            return "Content configuration validation failed:\n" + string.Join("\n", report.Issues.Select(issue => issue.ToString()));
        }
    }

    public static class ContentConfigValidator
    {
        public static ContentValidationReport Validate(GameContentConfig config)
        {
            var report = new ContentValidationReport();
            if (config == null)
            {
                report.Add(ContentValidationCode.MissingCatalog, "GameContentConfig", "Root config is missing.");
                return report;
            }

            if (config.SchemaVersion != ContentConstants.ExpectedSchemaVersion)
                report.Add(ContentValidationCode.InvalidSchemaVersion, "GameContentConfig.SchemaVersion",
                    $"Schema v{ContentConstants.ExpectedSchemaVersion} is required; found v{config.SchemaVersion}. Legacy schemas must be explicitly migrated or rejected.");

            ValidateCatalogReferences(config, report);
            if (!HasAllCatalogs(config)) return report;

            var resources = Index(config.ResourceCatalog.Definitions, definition => definition?.Id, "ResourceCatalog", report);
            var cards = Index(config.CardCatalog.Definitions, definition => definition?.Id, "CardCatalog", report);
            var tacticEffects = Index(config.CardCatalog.TacticEffects, definition => definition?.Id, "CardCatalog.TacticEffects", report);
            var buildings = Index(config.BuildingCatalog.Definitions, definition => definition?.Id, "BuildingCatalog", report);
            var researchUpgrades = Index(config.BuildingCatalog.ResearchUpgrades, definition => definition?.Id, "BuildingCatalog.ResearchUpgrades", report);
            var researchBags = Index(config.BuildingCatalog.ResearchBags, definition => definition?.Id, "BuildingCatalog.ResearchBags", report);
            var units = Index(config.UnitCatalog.Definitions, definition => definition?.Id, "UnitCatalog", report);
            var battlefields = Index(config.BattlefieldCatalog.Definitions, definition => definition?.Id, "BattlefieldCatalog", report);
            var bosses = Index(config.BossCatalog.Definitions, definition => definition?.Id, "BossCatalog", report);
            var rewards = Index(config.RewardCatalog.Definitions, definition => definition?.Id, "RewardCatalog", report);
            var stages = Index(config.StageEffectCatalog.CampaignStages, definition => definition?.Id, "StageEffectCatalog.CampaignStages", report);
            var modes = Index(config.StageEffectCatalog.MapModes, definition => definition?.Id, "StageEffectCatalog.MapModes", report);
            var phaseProfiles = Index(config.StageEffectCatalog.AiPhaseProfiles, definition => definition?.Id, "StageEffectCatalog.AiPhaseProfiles", report);
            var utilityProfiles = Index(config.StageEffectCatalog.AiUtilityProfiles, definition => definition?.Id, "StageEffectCatalog.AiUtilityProfiles", report);
            var economyProfiles = Index(config.StageEffectCatalog.EnemyEconomyProfiles, definition => definition?.Id, "StageEffectCatalog.EnemyEconomyProfiles", report);
            var doctrines = Index(config.StageEffectCatalog.AiDoctrines, definition => definition?.Id, "StageEffectCatalog.AiDoctrines", report);
            var difficulties = Index(config.StageEffectCatalog.DifficultyRules, definition => definition?.Id, "StageEffectCatalog.DifficultyRules", report);
            var unitPools = Index(config.StageEffectCatalog.EnemyUnitPools, definition => definition?.Id, "StageEffectCatalog.EnemyUnitPools", report);
            var resourceWaves = Index(config.StageEffectCatalog.ResourceActivationWaves, definition => definition?.Id, "StageEffectCatalog.ResourceActivationWaves", report);
            var scenes = Index(config.SceneKeyCatalog.Definitions, definition => definition?.Id, "SceneKeyCatalog", report);
            var presentations = Index(config.PresentationCatalog.Definitions, definition => definition?.Id, "PresentationCatalog", report);

            ValidateResources(resources, report);
            ValidateUnits(units, resources, presentations, report);
            ValidateCards(cards, tacticEffects, buildings, units, resources, presentations, report);
            ValidateBuildings(buildings, cards, units, resources, researchBags, presentations, report);
            ValidateResearch(researchUpgrades, researchBags, resources, presentations, report);
            ValidateRewards(rewards, cards, units, resources, presentations, report);
            ValidateStages(stages, battlefields, cards, config.ProgressionConfig, report);
            ValidatePhaseProfiles(phaseProfiles, report);
            ValidateUtilityProfiles(utilityProfiles, report);
            ValidateEconomyProfiles(economyProfiles, cards, buildings, units, resources, report);
            ValidateDifficulties(difficulties, report);
            
ValidateModes(modes, phaseProfiles, utilityProfiles, economyProfiles, doctrines, difficulties, unitPools, units, rewards, report);
            ValidateResourceWaves(resourceWaves, modes, resources, report);
            ValidateHeatTiers(config.StageEffectCatalog.HeatTiers, report);
            ValidateBosses(bosses, rewards, presentations, report);
            ValidateBattlefields(battlefields, stages, modes, bosses, rewards, scenes, cards, buildings, units, resources, report);
            ValidateContentReachability(rewards, battlefields, cards, buildings, report);
            ValidateP1PrototypeValues(units, buildings, cards, report);
            return report;
        }

        public static void ThrowIfInvalid(GameContentConfig config)
        {
            var report = Validate(config);
            if (!report.IsValid) throw new ContentConfigException(report);
        }

        private static void ValidateCatalogReferences(GameContentConfig config, ContentValidationReport report)
        {
            RequireCatalog(config.ResourceCatalog, "ResourceCatalog", report);
            RequireCatalog(config.CardCatalog, "CardCatalog", report);
            RequireCatalog(config.BuildingCatalog, "BuildingCatalog", report);
            RequireCatalog(config.UnitCatalog, "UnitCatalog", report);
            RequireCatalog(config.BattlefieldCatalog, "BattlefieldCatalog", report);
            RequireCatalog(config.BossCatalog, "BossCatalog", report);
            RequireCatalog(config.RewardCatalog, "RewardCatalog", report);
            RequireCatalog(config.ProgressionConfig, "ProgressionConfig", report);
            RequireCatalog(config.StageEffectCatalog, "StageEffectCatalog", report);
            RequireCatalog(config.SceneKeyCatalog, "SceneKeyCatalog", report);
            RequireCatalog(config.PresentationCatalog, "PresentationCatalog", report);
        }

        private static bool HasAllCatalogs(GameContentConfig config) =>
            config.ResourceCatalog != null && config.CardCatalog != null && config.BuildingCatalog != null &&
            config.UnitCatalog != null && config.BattlefieldCatalog != null && config.BossCatalog != null &&
            config.RewardCatalog != null && config.ProgressionConfig != null && config.StageEffectCatalog != null &&
            config.SceneKeyCatalog != null && config.PresentationCatalog != null;

        private static void RequireCatalog(object catalog, string path, ContentValidationReport report)
        {
            if (catalog == null) report.Add(ContentValidationCode.MissingCatalog, path, "Catalog reference is missing.");
        }

        private static Dictionary<string, T> Index<T>(IReadOnlyList<T> definitions, Func<T, string> getId, string path, ContentValidationReport report)
            where T : class
        {
            var result = new Dictionary<string, T>(StringComparer.Ordinal);
            if (definitions == null) return result;
            for (var index = 0; index < definitions.Count; index++)
            {
                var definition = definitions[index];
                var id = definition == null ? null : getId(definition);
                var itemPath = $"{path}[{index}]";
                if (string.IsNullOrWhiteSpace(id))
                {
                    report.Add(ContentValidationCode.EmptyId, itemPath, "Stable id cannot be empty.");
                    continue;
                }
                if (!result.TryAdd(id, definition))
                    report.Add(ContentValidationCode.DuplicateId, itemPath, $"Duplicate stable id '{id}'.");
            }
            return result;
        }

        private static void ValidateResources(Dictionary<string, ResourceDefinition> resources, ContentValidationReport report)
        {
            if (!resources.TryGetValue(ContentConstants.GoldResourceId, out var gold) || gold.Scope != ResourceScope.Meta || gold.Group != ResourceGroup.Meta)
                report.Add(ContentValidationCode.InvalidResourceDefinition, "ResourceCatalog", "resource.gold must be a Meta resource in the Meta group.");
            if (!resources.TryGetValue(ContentConstants.PlankResourceId, out var plank) || plank.Scope != ResourceScope.Match || plank.Group != ResourceGroup.Wood)
                report.Add(ContentValidationCode.InvalidResourceDefinition, "ResourceCatalog", "resource.plank must be a Match resource in the Wood group.");

            var matchCount = resources.Values.Count(resource => resource.Scope == ResourceScope.Match);
            if (matchCount != 9)
                report.Add(ContentValidationCode.InvalidResourceDefinition, "ResourceCatalog", $"Exactly 9 match resources are required; found {matchCount}.");
            foreach (var pair in resources)
            {
                var expectedAcquisition = pair.Key == ContentConstants.GoldResourceId
                    ? ResourceAcquisitionKind.Meta
                    : IsWorldRawResource(pair.Key) ? ResourceAcquisitionKind.BattlefieldGathered : ResourceAcquisitionKind.Processed;
                if (pair.Value.AcquisitionKind != expectedAcquisition)
                    report.Add(ContentValidationCode.InvalidResourceDefinition, $"ResourceCatalog.{pair.Key}.AcquisitionKind",
                        $"Resource '{pair.Key}' must use acquisition kind {expectedAcquisition}.");
                if (pair.Value.Scope == ResourceScope.Match && pair.Value.DefaultCapacity <= 0 && !pair.Value.CanOverflow)
                    report.Add(ContentValidationCode.InvalidResourceDefinition, $"ResourceCatalog.{pair.Key}.DefaultCapacity", "A bounded match resource needs a positive capacity.");
                if (pair.Value.Scope == ResourceScope.Meta && pair.Key != ContentConstants.GoldResourceId)
                    report.Add(ContentValidationCode.InvalidResourceDefinition, $"ResourceCatalog.{pair.Key}.Scope", "Gold is the only supported meta resource in v0.4.");
            }
        }

        private static void ValidateUnits(Dictionary<string, UnitDefinition> units, Dictionary<string, ResourceDefinition> resources,
            Dictionary<string, PresentationDefinition> presentations, ContentValidationReport report)
        {
            foreach (var pair in units)
            {
                var unit = pair.Value;
                var path = $"UnitCatalog.{pair.Key}";
                ValidateResourceAmounts(unit.TrainingCosts, resources, path + ".TrainingCosts", report);
                RequireReference(unit.PresentationKey, presentations, path + ".PresentationKey", report);
                if (unit.CanAttack)
                {
                    RequireReference(unit.PlayerWorldPrefabPresentationKey, presentations,
                        path + ".PlayerWorldPrefabPresentationKey", report);
                    RequireReference(unit.EnemyWorldPrefabPresentationKey, presentations,
                        path + ".EnemyWorldPrefabPresentationKey", report);
                }
                if (unit.MaxHealth <= 0 || unit.CollisionRadius <= 0 || unit.MovePerTick < 0 ||
                    unit.WallDamageMultiplierMilli < 0 || unit.AttackIntervalTicks <= 0 ||
                    (unit.CanAttack && unit.AcquireRadius <= unit.AttackRange) ||
                    unit.ChaseRadius < unit.AcquireRadius ||
                    (unit.CanAttack && (unit.AttackDamage <= 0 || unit.AttackRange <= 0)) ||
                    (!unit.CanAttack && (unit.AttackDamage != 0 || unit.ProjectileSpeedPerTick != 0)))
                    report.Add(ContentValidationCode.InvalidCombatConfiguration, path,
                        "Unit combat values are inconsistent: ranges must be ordered and attack-capable units need positive damage/range.");
            }
        }

        private static void ValidateCards(Dictionary<string, CardDefinition> cards,
            Dictionary<string, TacticEffectDefinition> tacticEffects, Dictionary<string, BuildingDefinition> buildings,
            Dictionary<string, UnitDefinition> units, Dictionary<string, ResourceDefinition> resources,
            Dictionary<string, PresentationDefinition> presentations, ContentValidationReport report)
        {
            foreach (var pair in cards)
            {
                var card = pair.Value;
                var path = $"CardCatalog.{pair.Key}";
                if (card.Type == CardType.Soldier)
                {
                    RequireReference(card.LinkedContentId, units, path + ".LinkedContentId", report);
                    RequireReference(card.ActivationCampBuildingId, buildings, path + ".ActivationCampBuildingId", report);
                }
                else if (card.Type == CardType.BuildingItem || card.Type == CardType.BattlefieldItem)
                {
                    RequireReference(card.LinkedContentId, buildings, path + ".LinkedContentId", report);
                    if (card.Type == CardType.BattlefieldItem && buildings.TryGetValue(card.LinkedContentId ?? string.Empty, out var structure) &&
                        structure.Category != BuildingCategory.BattlefieldStructure)
                        report.Add(ContentValidationCode.InvalidReferenceType, path + ".LinkedContentId", "BattlefieldItem must reference a BattlefieldStructure.");
                }
                else if (card.Type == CardType.Tactic)
                {
                    RequireReference(card.LinkedContentId, tacticEffects, path + ".LinkedContentId", report);
                }
                else if (card.Type == CardType.ReinforcementItem)
                {
                    if (card.DefaultUnlocked || card.UnlockGoldCost != 0 || card.MaxMetaLevel != 1 ||
                        card.UpgradeGoldCosts.Count != 0 || card.GrowthRules.Count != 0 ||
                        card.PrerequisiteCardIds.Count != 0 || card.OfferTags.Count != 0 ||
                        !string.IsNullOrWhiteSpace(card.ActivationCampBuildingId))
                        report.Add(ContentValidationCode.InvalidCardProgression, path,
                            "Reward-only reinforcement cards must be locked, level 1, free of progression, prerequisites, camp activation and normal offer tags.");
                }

                if (card.MaxMetaLevel < 1 || card.UpgradeGoldCosts.Count != card.MaxMetaLevel - 1 || card.UpgradeGoldCosts.Any(cost => cost < 0))
                    report.Add(ContentValidationCode.InvalidCardProgression, path, "Upgrade cost count must equal MaxMetaLevel - 1 and costs cannot be negative.");
                foreach (var prerequisiteId in card.PrerequisiteCardIds)
                    RequireReference(prerequisiteId, cards, path + ".PrerequisiteCardIds", report);
                RequireReference(card.PresentationKey, presentations, path + ".PresentationKey", report);
            }

            foreach (var pair in tacticEffects)
            {
                var effect = pair.Value;
                var path = $"CardCatalog.TacticEffects.{pair.Key}";
                ValidateResourceAmounts(effect.ResourceAmounts, resources, path + ".ResourceAmounts", report);
                if (effect.Kind == TacticEffectKind.AddResource && effect.ResourceAmounts.Any(amount => amount != null && IsWorldRawResource(amount.ResourceId)))
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".ResourceAmounts",
                        "Tactics cannot create battlefield-gathered raw resources.");
                if (effect.Kind != TacticEffectKind.AddResource && effect.Magnitude <= 0)
                    report.Add(ContentValidationCode.InvalidReferenceType, path + ".Magnitude", "Non-resource tactic effects need a positive magnitude.");
            }
        }

        private static void ValidateBuildings(Dictionary<string, BuildingDefinition> buildings, Dictionary<string, CardDefinition> cards,
            Dictionary<string, UnitDefinition> units, Dictionary<string, ResourceDefinition> resources,
            Dictionary<string, ResearchBagDefinition> researchBags,
            Dictionary<string, PresentationDefinition> presentations, ContentValidationReport report)
        {
            foreach (var pair in buildings)
            {
                var building = pair.Value;
                var path = $"BuildingCatalog.{pair.Key}";
                if (!cards.TryGetValue(building.SourceCardId ?? string.Empty, out var sourceCard) ||
                    (sourceCard.Type != CardType.BuildingItem && sourceCard.Type != CardType.BattlefieldItem) || sourceCard.LinkedContentId != building.Id)
                    report.Add(ContentValidationCode.InvalidReferenceType, path + ".SourceCardId", "Source card must be a building or battlefield item linked back to this building.");
                ValidateResourceAmounts(building.Inputs, resources, path + ".Inputs", report);
                ValidateResourceAmounts(building.Outputs, resources, path + ".Outputs", report);
                ValidateResourceAmounts(building.InputReserveFloors, resources, path + ".InputReserveFloors", report);
                if (building.Category == BuildingCategory.Processing)
                {
                    if (building.InputReserveFloors.Count != 1 ||
                        building.InputReserveFloors.Any(floor => floor == null ||
                            !building.Inputs.Any(input => input != null && input.ResourceId == floor.ResourceId)))
                        report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".InputReserveFloors",
                            "Each Schema v12 processor needs exactly one positive reserve floor for its input resource.");
                }
                else if (building.InputReserveFloors.Count != 0)
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".InputReserveFloors",
                        "Only processing buildings may reserve an input floor.");
                if (building.Category == BuildingCategory.Gathering &&
                    (string.IsNullOrWhiteSpace(building.WorkerUnitId) || building.Inputs.Count != 0 || building.Outputs.Count != 0 ||
                     building.GathererAllowedResourceIds.Count == 0 ||
                     building.GathererAllowedResourceIds.Any(id => !IsWorldRawResource(id)) ||
                     building.GathererDispatchIntervalTicks <= 0 || building.GathererCarryAmount <= 0))
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path,
                        "A gathering camp needs one worker, explicit raw-resource whitelist, dispatch interval and carry amount; Outputs must stay empty.");
                if (building.Category == BuildingCategory.Gathering)
                    ValidateResourceAmounts(building.GathererDispatchCosts, resources, path + ".GathererDispatchCosts", report);
                if (building.Category != BuildingCategory.Gathering &&
                    building.Outputs.Any(output => output != null && IsWorldRawResource(output.ResourceId)))
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".Outputs",
                        "Buildings may process raw resources but cannot create them.");
                if (building.Category is not (BuildingCategory.Gathering or BuildingCategory.BattlefieldStructure) &&
                    !string.IsNullOrWhiteSpace(building.WorkerUnitId))
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".WorkerUnitId",
                        "Only gathering lodges and battlefield construction may own a worker.");
                if (building.Category is not (BuildingCategory.Gathering or BuildingCategory.SoldierCamp or BuildingCategory.BattlefieldStructure) &&
                    building.ProductionCycleTicks <= 0)
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".ProductionCycleTicks", "A producing building needs a positive production cycle.");
                if (building.Category == BuildingCategory.Gathering && building.WorkerGatherTicks <= 0)
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".WorkerGatherTicks", "A gathering building needs a positive gather duration.");
                if (!string.IsNullOrWhiteSpace(building.WorkerUnitId)) RequireReference(building.WorkerUnitId, units, path + ".WorkerUnitId", report);
                RequireReference(building.PresentationKey, presentations, path + ".PresentationKey", report);

                if (building.Category == BuildingCategory.BattlefieldStructure)
                {
                    ValidateResourceAmounts(building.ConstructionCosts, resources, path + ".ConstructionCosts", report);
                    if (building.MaxHealth <= 0 || building.AttackDamage <= 0 || building.AttackRange <= 0 ||
                        building.AttackIntervalTicks <= 0 || building.ConstructionTicks <= 0)
                        report.Add(ContentValidationCode.InvalidConstructionConfiguration, path,
                            "Battlefield structure needs positive health, attack, range, interval and construction duration.");
                }
                if (building.Category == BuildingCategory.Research)
                    RequireReference(building.ResearchBagId, researchBags, path + ".ResearchBagId", report);

                if (building.Category == BuildingCategory.SoldierCamp)
                {
                    if (!cards.TryGetValue(building.ActivatedSoldierCardId ?? string.Empty, out var soldierCard) ||
                        soldierCard.Type != CardType.Soldier || soldierCard.ActivationCampBuildingId != building.Id)
                        report.Add(ContentValidationCode.InvalidCampBinding, path, "Soldier camp and soldier card must form a one-to-one stable-id binding.");
                }
                else if (!string.IsNullOrWhiteSpace(building.ActivatedSoldierCardId))
                    report.Add(ContentValidationCode.InvalidCampBinding, path, "Only SoldierCamp buildings may activate a soldier card.");

                var expectedLevel = 2;
                foreach (var upgrade in building.UpgradeLevels)
                {
                    if (upgrade == null || upgrade.Level != expectedLevel || upgrade.Cost <= 0 ||
                        upgrade.ProductionMultiplierMilli < 1 || upgrade.TrainingTimeMultiplierMilli < 1 ||
                        !string.Equals(upgrade.PaymentResourceId, ContentConstants.PlankResourceId, StringComparison.Ordinal))
                        report.Add(ContentValidationCode.InvalidUpgradeCost, path + ".UpgradeLevels", "Upgrade levels must be sequential from 2 and pay a positive amount of resource.plank only.");
                    expectedLevel++;
                }
            }

            foreach (var rawId in WorldRawResourceIds)
                if (!buildings.Values.Any(building => building.Category == BuildingCategory.Processing &&
                        building.Inputs.Any(input => input != null && input.ResourceId == rawId)))
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, "BuildingCatalog",
                        $"Raw resource '{rawId}' needs at least one processing sink.");
            foreach (var processedId in new[] { "resource.meat", "resource.wine", "resource.plank", "resource.stone", "resource.iron-ingot" })
                if (!buildings.Values.Any(building => building.Category == BuildingCategory.Processing &&
                        building.Outputs.Any(output => output != null && output.ResourceId == processedId)))
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, "BuildingCatalog",
                        $"Processed resource '{processedId}' needs at least one configured producer.");
        }

        private static void ValidateResearch(Dictionary<string, ResearchUpgradeDefinition> upgrades,
            Dictionary<string, ResearchBagDefinition> bags, Dictionary<string, ResourceDefinition> resources,
            Dictionary<string, PresentationDefinition> presentations, ContentValidationReport report)
        {
            if (upgrades.Count != 8 || Enum.GetValues(typeof(ResearchCategory)).Cast<ResearchCategory>()
                    .Any(category => upgrades.Values.Count(upgrade => upgrade.TargetRole == category) != 2))
                report.Add(ContentValidationCode.InvalidResearchConfiguration, "BuildingCatalog.ResearchUpgrades",
                    "Schema v12 requires exactly eight upgrades: two each for Melee, Ranged, Magic and Siege.");

            foreach (var pair in upgrades)
            {
                if (pair.Value.Modifiers.Count == 0 || pair.Value.MaxRank != 3 ||
                    pair.Value.Modifiers.Any(value => value == null || string.IsNullOrWhiteSpace(value.PropertyKey) ||
                        value.PercentPerRankBasisPoints <= 0))
                    report.Add(ContentValidationCode.InvalidResearchConfiguration, $"BuildingCatalog.ResearchUpgrades.{pair.Key}",
                        "Research upgrade needs at least one positive linear modifier and max rank 3.");
                RequireReference(pair.Value.PresentationKey, presentations,
                    $"BuildingCatalog.ResearchUpgrades.{pair.Key}.PresentationKey", report);
            }

            foreach (var pair in bags)
            {
                var bag = pair.Value;
                var path = $"BuildingCatalog.ResearchBags.{pair.Key}";
                foreach (var id in bag.UpgradeIds) RequireReference(id, upgrades, path + ".UpgradeIds", report);
                ValidateResourceAmounts(bag.Costs, resources, path + ".Costs", report);
                var wine = bag.Costs.FirstOrDefault(cost => cost?.ResourceId == "resource.wine")?.Amount ?? 0;
                var ingot = bag.Costs.FirstOrDefault(cost => cost?.ResourceId == "resource.iron-ingot")?.Amount ?? 0;
                if (bag.UpgradeIds.Distinct(StringComparer.Ordinal).Count() != 8 || bag.ResearchTicks != 250 ||
                    bag.CandidateCount != 3 || wine != 12 || ingot != 8)
                    report.Add(ContentValidationCode.InvalidResearchConfiguration, path,
                        "Schema v12 research bag requires eight unique upgrades, wine 12, iron ingot 8, 250 ticks and three candidates.");
            }
        }

        private static void ValidateRewards(Dictionary<string, RewardDefinition> rewards, Dictionary<string, CardDefinition> cards,
            Dictionary<string, UnitDefinition> units,
            Dictionary<string, ResourceDefinition> resources,
            Dictionary<string, PresentationDefinition> presentations,
            ContentValidationReport report)
        {
            foreach (var pair in rewards)
            {
                var reward = pair.Value;
                var rewardPath = $"RewardCatalog.{pair.Key}";
                if (!reward.TimedCardOffers.Select(offer => offer?.TriggerSeconds ?? -1).SequenceEqual(ContentConstants.P1OfferSeconds) ||
                    reward.TimedCardOffers.Any(offer => offer == null || offer.CandidateCount != 4))
                    report.Add(ContentValidationCode.InvalidOfferGuarantee, rewardPath + ".TimedCardOffers",
                        "Schema v14 requires the automatic reward cycle to start at 60 seconds with four visible choices.");
                for (var index = 0; index < reward.TimedCardOffers.Count; index++)
                {
                    var actual = reward.TimedCardOffers[index]?.FallbackCardIds
                        .Where(value => !string.IsNullOrWhiteSpace(value)).ToHashSet(StringComparer.Ordinal)
                        ?? new HashSet<string>(StringComparer.Ordinal);
                    if (!ContentConstants.P1OfferCardIds[0].All(actual.Contains) ||
                        !actual.Contains("card.battlefield.arrow-tower"))
                        report.Add(ContentValidationCode.InvalidOfferGuarantee, $"{rewardPath}.TimedCardOffers[{index}]",
                            "Schema v14 recurring building-card pool must contain the required early economy/research cards and the arrow-tower battlefield structure.");
                }
                if (reward.HandLimit != 6 || !reward.AllowFullHandDiscard || reward.FullHandExchange == null ||
                    !string.IsNullOrWhiteSpace(reward.FullHandExchange.ResourceId) || reward.FullHandExchange.Amount != 0)
                    report.Add(ContentValidationCode.InvalidOfferGuarantee, rewardPath + ".FullHandExchange",
                        "A full six-card hand must use replace/discard and cannot exchange the offer for resources.");
                if (reward.PlayerBossRewards.Count == 0 || reward.EnemyBossRewards.Count == 0 || reward.BossRewardBudgetMilli <= 0)
                    report.Add(ContentValidationCode.InvalidBossConfiguration, rewardPath,
                        "Both sides need a non-empty Boss contact reward pool and a positive strength budget.");
                for (var index = 0; index < pair.Value.TimedCardOffers.Count; index++)
                {
                    var offer = pair.Value.TimedCardOffers[index];
                    var path = $"RewardCatalog.{pair.Key}.TimedCardOffers[{index}]";
                    if (offer == null || offer.CandidateCount < 1)
                    {
                        report.Add(ContentValidationCode.InvalidOfferGuarantee, path, "Offer must request at least one candidate.");
                        continue;
                    }
                    var fallbacks = offer.FallbackCardIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
                    if (fallbacks.Length < offer.CandidateCount || fallbacks.Any(id => !cards.TryGetValue(id, out var card) || !card.DefaultUnlocked))
                        report.Add(ContentValidationCode.InvalidOfferGuarantee, path, "Fallback pool must contain enough unique, default-unlocked cards.");
                }

                if (reward.RarityWeights.Count != 3 || reward.RarityWeights.Select(value => value.Rarity).Distinct().Count() != 3 ||
                    reward.RarityWeights.Any(value => !value.HeatTierWeights.SequenceEqual(ContentConstants.RewardRarityWeights
                        .Select(weights => weights[(int)value.Rarity]))))
                    report.Add(ContentValidationCode.InvalidOfferGuarantee, rewardPath + ".RarityWeights",
                        "Schema v14 reward rarity weights must be 100/0/0, 75/25/0, 60/40/0, 50/40/10 and 45/40/15.");
                foreach (var key in new[] { reward.BuildingRewardPresentationKey, reward.ResourceRewardPresentationKey, reward.ReinforcementRewardPresentationKey })
                    RequireReference(key, presentations, rewardPath + ".RewardPresentationKey", report);

                if (reward.ProcessedResourceBundles.Count != 9 ||
                    reward.ProcessedResourceBundles.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != 9 ||
                    reward.ProcessedResourceBundles.GroupBy(value => value.Rarity).Any(group => group.Count() != 3))
                    report.Add(ContentValidationCode.InvalidOfferGuarantee, rewardPath + ".ProcessedResourceBundles",
                        "Schema v14 requires three processed-resource bundles in each rarity.");
                foreach (var bundle in reward.ProcessedResourceBundles)
                    ValidateResourceAmounts(bundle.Amounts, resources,
                        rewardPath + ".ProcessedResourceBundles." + bundle.Id, report);
                var expectedAmounts = new Dictionary<RewardRarity, int[]>(3)
                {
                    [RewardRarity.Common] = new[] { 6, 12 }, [RewardRarity.Rare] = new[] { 8, 15 },
                    [RewardRarity.Epic] = new[] { 9, 18 }
                };
                if (reward.ProcessedResourceBundles.Any(bundle => bundle.Amounts.Any(amount =>
                        amount.Amount != (amount.ResourceId is "resource.plank" or "resource.stone"
                            ? expectedAmounts[bundle.Rarity][1] : expectedAmounts[bundle.Rarity][0]))))
                    report.Add(ContentValidationCode.InvalidOfferGuarantee, rewardPath + ".ProcessedResourceBundles",
                        "Schema v14 processed bundles must use explicit 6/8/9 and 12/15/18 quantities.");

                var templates = reward.ReinforcementTemplates;
                if (templates.Count != 10 || templates.Select(value => value.Id).Distinct(StringComparer.Ordinal).Count() != 10 ||
                    !templates.GroupBy(value => value.MinimumHeatTier).OrderBy(value => value.Key)
                        .Select(value => value.Count()).SequenceEqual(new[] { 2, 2, 3, 3 }))
                    report.Add(ContentValidationCode.InvalidOfferGuarantee, rewardPath + ".ReinforcementTemplates",
                        "Schema v14 requires 2/2/3/3 stable reinforcement templates across heat tiers 0-3.");
                var heavyOrSiege = new HashSet<string>(new[]
                    { "unit.siege-ram", "unit.heavy-warrior", "unit.cannon" }, StringComparer.Ordinal);
                foreach (var template in templates)
                {
                    var expectedRarity = template.MinimumHeatTier == 0 ? RewardRarity.Common :
                        template.MinimumHeatTier < 3 ? RewardRarity.Rare : RewardRarity.Epic;
                    if (template.Rarity != expectedRarity || template.Units.Count == 0 || template.Units.Any(value => value.Quantity <= 0 || !units.ContainsKey(value.UnitId)) ||
                        template.Units.Where(value => heavyOrSiege.Contains(value.UnitId)).Sum(value => value.Quantity) > 1)
                        report.Add(ContentValidationCode.InvalidOfferGuarantee,
                            rewardPath + ".ReinforcementTemplates." + template.Id,
                            "Reinforcement templates require legal positive unit references and at most one heavy/siege unit.");
                }
            }

            var reinforcementCards = cards.Values.Where(value => value.Type == CardType.ReinforcementItem).ToArray();
            var templateGroups = rewards.Values.SelectMany(value => value.ReinforcementTemplates)
                .GroupBy(value => value.Id, StringComparer.Ordinal).ToArray();
            foreach (var group in templateGroups)
            {
                var definitions = group.Select(value => $"{value.DisplayName}|{value.MinimumHeatTier}|" + string.Join(",",
                    value.Units.OrderBy(unit => unit.UnitId, StringComparer.Ordinal)
                        .Select(unit => $"{unit.UnitId}:{unit.Quantity}"))).Distinct(StringComparer.Ordinal).ToArray();
                if (definitions.Length != 1)
                    report.Add(ContentValidationCode.InvalidOfferGuarantee,
                        "RewardCatalog.ReinforcementTemplates." + group.Key,
                        "A stable reinforcement template id must keep the same name, heat tier and unit composition in every reward table.");
                var matchingCards = reinforcementCards.Where(card => card.LinkedContentId == group.Key).ToArray();
                if (matchingCards.Length != 1)
                    report.Add(ContentValidationCode.InvalidReferenceType,
                        "CardCatalog.ReinforcementItems." + group.Key,
                        "Every unique reinforcement template must map to exactly one ReinforcementItem card.");
            }
            var templateIds = templateGroups.Select(group => group.Key).ToHashSet(StringComparer.Ordinal);
            foreach (var card in reinforcementCards.Where(card => !templateIds.Contains(card.LinkedContentId)))
                report.Add(ContentValidationCode.InvalidReferenceType, "CardCatalog." + card.Id + ".LinkedContentId",
                    "ReinforcementItem must reference a reinforcement template used by a reward table.");
        }

        private static void ValidateStages(Dictionary<string, CampaignStageDefinition> stages,
            Dictionary<string, BattlefieldDefinition> battlefields, Dictionary<string, CardDefinition> cards,
            ProgressionConfig progression, ContentValidationReport report)
        {
            RequireReference(progression.InitialCampaignStageId, stages, "ProgressionConfig.InitialCampaignStageId", report);
            foreach (var pair in stages)
            {
                var stage = pair.Value;
                var path = $"StageEffectCatalog.CampaignStages.{pair.Key}";
                if (!string.IsNullOrWhiteSpace(stage.PrerequisiteStageId)) RequireReference(stage.PrerequisiteStageId, stages, path + ".PrerequisiteStageId", report);
                foreach (var id in stage.UnlockedBattlefieldIds) RequireReference(id, battlefields, path + ".UnlockedBattlefieldIds", report);
                foreach (var id in stage.PurchasableCardIds)
                {
                    RequireReference(id, cards, path + ".PurchasableCardIds", report);
                    if (cards.TryGetValue(id, out var card) && card.Type == CardType.ReinforcementItem)
                        report.Add(ContentValidationCode.InvalidCardProgression, path + ".PurchasableCardIds",
                            "Reward-only reinforcement cards cannot be purchased in progression.");
                }
            }
        }

        private static void ValidateModes(Dictionary<string, MapModeDefinition> modes,
            Dictionary<string, AiPhaseProfileDefinition> phases, Dictionary<string, AiUtilityProfileDefinition> utilities,
            Dictionary<string, EnemyEconomyProfileDefinition> economies, Dictionary<string, AiDoctrineDefinition> doctrines,
            Dictionary<string, DifficultyRulesDefinition> difficulties, Dictionary<string, EnemyUnitPoolDefinition> unitPools,
            Dictionary<string, UnitDefinition> units, Dictionary<string, RewardDefinition> rewards,
            ContentValidationReport report)
        {
            foreach (var pair in modes)
            {
                var mode = pair.Value;
                var path = $"StageEffectCatalog.MapModes.{pair.Key}";
                RequireReference(mode.AiDoctrineId, doctrines, path + ".AiDoctrineId", report);
                RequireReference(mode.DifficultyRulesId, difficulties, path + ".DifficultyRulesId", report);
                RequireReference(mode.AiPhaseProfileId, phases, path + ".AiPhaseProfileId", report);
                RequireReference(mode.AiUtilityProfileId, utilities, path + ".AiUtilityProfileId", report);
                RequireReference(mode.EnemyEconomyProfileId, economies, path + ".EnemyEconomyProfileId", report);
                RequireReference(mode.EnemyUnitPoolId, unitPools, path + ".EnemyUnitPoolId", report);
                RequireReference(mode.RewardTableId, rewards, path + ".RewardTableId", report);
                if (mode.RewardMultiplierMilli < 1)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".RewardMultiplierMilli", "Reward multiplier must be positive.");
                if (mode.Kind == MapModeKind.Nightmare && economies.TryGetValue(mode.EnemyEconomyProfileId, out var nightmareEconomy) && nightmareEconomy.EconomicEfficiencyMilli > 1100)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".EnemyEconomyProfileId", "Nightmare economic efficiency cannot exceed 1100 milli.");
            }

            foreach (var pair in unitPools)
                foreach (var unitId in pair.Value.UnitIds)
                    RequireReference(unitId, units, $"StageEffectCatalog.EnemyUnitPools.{pair.Key}.UnitIds", report);
        }

private static void ValidatePhaseProfiles(Dictionary<string, AiPhaseProfileDefinition> profiles,
            ContentValidationReport report)
        {
            var requiredIds = new[] { "phase.development", "phase.contest", "phase.decisive" };
            var developmentAllowed = new HashSet<string>(new[]
                { "intent.develop", "intent.reserve", "intent.research", "intent.hold", "intent.assault" }, StringComparer.Ordinal);
            var decisiveAllowed = new HashSet<string>(new[]
                { "intent.hold", "intent.assault", "intent.raid-economy", "intent.research", "intent.build-tower", "intent.reserve" }, StringComparer.Ordinal);
            foreach (var pair in profiles)
            {
                var path = $"StageEffectCatalog.AiPhaseProfiles.{pair.Key}";
                var profile = pair.Value;
                var phases = profile.Phases;
                if (phases.Count != ContentConstants.RequiredAiPhaseCount ||
                    !phases.Select(value => value?.Id).SequenceEqual(requiredIds) ||
                    phases.Select(value => value?.StartTick ?? -1).Where(tick => tick >= 0).Distinct().Count() != ContentConstants.RequiredAiPhaseCount ||
                    !phases.Where(value => value != null).Select(value => value.StartTick).OrderBy(tick => tick)
                        .SequenceEqual(phases.Where(value => value != null).Select(value => value.StartTick)))
                {
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".Phases",
                        "Profile must contain ordered development, contest and decisive phases with unique start ticks.");
                    continue;
                }

                if (phases[0].StartTick != 0 || phases.Any(value => value.AllowedIntentIds.Count == 0) ||
                    !developmentAllowed.SetEquals(phases[0].AllowedIntentIds) ||
                    !new HashSet<string>(ContentConstants.P1AiIntentIds, StringComparer.Ordinal).SetEquals(phases[1].AllowedIntentIds) ||
                    !decisiveAllowed.SetEquals(phases[2].AllowedIntentIds))
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".Phases",
                        "Schema v8 phase intent permissions must match development, full contest and decisive sets.");

                if (profile.FirstProbeStartTick != 600 || profile.FirstProbeEndTick != 800 ||
                    profile.FirstProbeEndTick < profile.FirstProbeStartTick)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".FirstProbeWindow",
                        "Schema v8 requires the first probe window to be 600..800 ticks.");

                if (profile.PublicAccelerationStartTick != 9000 ||
                    profile.PublicProductionMultiplierMilli != 2000)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".PublicAcceleration",
                        "Schema v8 requires the public 200% processing acceleration at tick 9000.");
            }
        }

private static void ValidateUtilityProfiles(Dictionary<string, AiUtilityProfileDefinition> profiles,
            ContentValidationReport report)
        {
            var expected = new HashSet<string>(ContentConstants.P1AiIntentIds, StringComparer.Ordinal);
            foreach (var pair in profiles)
            {
                var profile = pair.Value;
                var path = $"StageEffectCatalog.AiUtilityProfiles.{pair.Key}";
                var commitmentIds = profile.Commitments.Select(value => value?.IntentId)
                    .Where(id => !string.IsNullOrWhiteSpace(id)).ToArray();
                if (commitmentIds.Length != expected.Count ||
                    commitmentIds.Distinct(StringComparer.Ordinal).Count() != expected.Count ||
                    commitmentIds.Any(id => !expected.Contains(id)) ||
                    profile.Commitments.Any(value => value == null || value.MinimumTicks < 80))
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".Commitments",
                        "Utility profile must define all seven unique intents with at least 80 commitment ticks.");

                if (profile.FeatureCoefficients.Count == 0 ||
                    profile.FeatureCoefficients.Any(value => value == null ||
                        string.IsNullOrWhiteSpace(value.FeatureId) || !expected.Contains(value.IntentId)))
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".FeatureCoefficients",
                        "Feature coefficients must reference a named feature and one of the seven intents.");

                if (profile.SwitchCost != 120 || profile.RepetitionPenalty != 180 ||
                    profile.SoftmaxLookupVersion != 1 || profile.TemperatureMilli <= 0 ||
                    profile.DecisionIntervalTicks <= 0 ||
                    profile.PressureMinIntervalTicks <= 0 ||
                    profile.PressureTargetIntervalTicks < profile.PressureMinIntervalTicks ||
                    profile.PressureMaxIntervalTicks < profile.PressureTargetIntervalTicks ||
                    profile.ActiveUnitSoftCap <= 0 || profile.QueuedUnitSoftCap <= 0 ||
                    profile.LogisticsThreatMemoryTicks != 300 ||
                    profile.MaxConcurrentLogisticsResponses != 2 ||
                    profile.EmergencyDefenseOverflowUnits != 2 ||
                    profile.TowerEscalationKillCount != 2 ||
                    profile.ActiveUnitSoftCap + profile.QueuedUnitSoftCap > 36)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path,
                        "Schema v11 requires ordered pressure intervals, bounded army/queue caps, 300-tick logistics memory, two responses, two overflow units and two-kill tower escalation.");
            }
        }

        private static void ValidateEconomyProfiles(Dictionary<string, EnemyEconomyProfileDefinition> profiles,
            Dictionary<string, CardDefinition> cards, Dictionary<string, BuildingDefinition> buildings,
            Dictionary<string, UnitDefinition> units, Dictionary<string, ResourceDefinition> resources,
            ContentValidationReport report)
        {
            foreach (var pair in profiles)
            {
                var profile = pair.Value;
                var path = $"StageEffectCatalog.EnemyEconomyProfiles.{pair.Key}";
                var configuredDefenseFormation = profile.Formations.FirstOrDefault(value =>
                    value != null && string.Equals(value.Id, profile.DefenseReserveFormationId, StringComparison.Ordinal));
                var defenseReserveCosts = configuredDefenseFormation == null
                    ? new Dictionary<string, int>(StringComparer.Ordinal)
                    : configuredDefenseFormation.UnitIds.Select((unitId, index) =>
                            (unitId, quantity: index < configuredDefenseFormation.Quantities.Count
                                ? configuredDefenseFormation.Quantities[index] : 0))
                        .Where(value => units.ContainsKey(value.unitId))
                        .SelectMany(value => units[value.unitId].TrainingCosts.Select(cost =>
                            (cost.ResourceId, amount: cost.Amount * value.quantity)))
                        .GroupBy(value => value.ResourceId, StringComparer.Ordinal)
                        .ToDictionary(group => group.Key, group => group.Sum(value => value.amount), StringComparer.Ordinal);
                ValidateResourceAmounts(profile.InitialInventory, resources, path + ".InitialInventory", report);
                if (profile.EconomicEfficiencyMilli < 1000 || profile.EconomicEfficiencyMilli > 1100)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".EconomicEfficiencyMilli",
                        "Enemy economic efficiency must stay between parity and the public 1100 milli cap.");
                if (profile.InitialInventory.Any(amount => amount != null && IsWorldRawResource(amount.ResourceId) &&
                        amount.Amount > defenseReserveCosts.GetValueOrDefault(amount.ResourceId)) ||
                    defenseReserveCosts.Any(cost => IsWorldRawResource(cost.Key) &&
                        profile.InitialInventory.Where(value => value != null && value.ResourceId == cost.Key)
                            .Sum(value => value.Amount) < cost.Value))
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".InitialInventory",
                        "Enemy raw-resource opening inventory may contain exactly one paid logistics-reserve formation and no surplus.");
                foreach (var facility in profile.Facilities)
                {
                    if (facility == null || facility.Level < 1) report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".Facilities", "Facility level must be positive.");
                    else
                    {
                        RequireReference(facility.BuildingId, buildings, path + ".Facilities", report);
                        if (buildings.TryGetValue(facility.BuildingId, out var facilityBuilding) &&
                            facilityBuilding.Category == BuildingCategory.BattlefieldStructure)
                            report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".Facilities", "Enemy background facilities cannot include world-space battlefield structures.");
                    }
                }
                foreach (var camp in profile.Camps)
                {
                    if (camp == null || camp.SlotCount < 1) report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".Camps", "Camp slots must be positive.");
                    else RequireReference(camp.UnitId, units, path + ".Camps", report);
                }
                if (profile.InitialHandCardIds.Count != 6 || profile.InitialHandCardIds.Distinct(StringComparer.Ordinal).Count() != 6 ||
                    profile.InitialHandCardIds.Any(id => !cards.ContainsKey(id)) ||
                    !profile.InitialHandCardIds.OrderBy(value => value, StringComparer.Ordinal)
                        .SequenceEqual(ContentConstants.P1InitialBuildingCardIds.OrderBy(value => value, StringComparer.Ordinal)))
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".InitialHandCardIds",
                        "Enemy must start from the same six building cards as the player and consume them through normal building commands.");
                foreach (var formation in profile.Formations)
                {
                    if (formation == null || string.IsNullOrWhiteSpace(formation.Id) ||
                        formation.UnitIds.Count == 0 ||
                        formation.UnitIds.Count != formation.Quantities.Count ||
                        formation.Quantities.Any(quantity => quantity <= 0))
                    {
                        report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".Formations",
                            "Formation needs an id and aligned positive unit quantities.");
                        continue;
                    }

                    foreach (var id in formation.UnitIds)
                        RequireReference(id, units, path + ".Formations.UnitIds", report);
                    if (formation.AllowedIntentIds.Count == 0 ||
                        formation.AllowedIntentIds.Any(id =>
                            !ContentConstants.P1AiIntentIds.Contains(id, StringComparer.Ordinal)))
                        report.Add(ContentValidationCode.InvalidAiConfiguration,
                            path + ".Formations.AllowedIntentIds",
                            "Each formation requires at least one valid allowed intent in Schema v8.");
                }
                var defenseFormation = configuredDefenseFormation;
                if (defenseFormation == null || profile.DefenseReserveFormationId != "formation.logistics-guard" ||
                    defenseFormation.UnitIds.Count != 1 || defenseFormation.Quantities.Count != 1 ||
                    defenseFormation.Quantities[0] != 1 || defenseFormation.AllowedIntentIds.Count != 1 ||
                    defenseFormation.AllowedIntentIds[0] != "intent.hold" ||
                    !units.TryGetValue(defenseFormation.UnitIds[0], out var defenseUnit) || !defenseUnit.CanAttack ||
                    !profile.Camps.Any(value => value != null && value.UnitId == defenseFormation.UnitIds[0]))
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path + ".DefenseReserveFormationId",
                        "Schema v11 requires formation.logistics-guard with one attack-capable unit, Hold-only permission and an available camp.");
                if (profile.ReserveRatioMilli < 0 || profile.ReserveRatioMilli > 1000 || profile.GatherCycleTicks <= 0 ||
                    profile.ProcessingCycleTicks <= 0 || profile.BuilderRespawnTicks != 80)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path, "Enemy economy needs bounded reserves, positive cycles and an 80 tick builder respawn.");
            }
        }

        private static void ValidateBosses(Dictionary<string, BossDefinition> bosses, Dictionary<string, RewardDefinition> rewards,
            Dictionary<string, PresentationDefinition> presentations, ContentValidationReport report)
        {
            foreach (var pair in bosses)
            {
                RequireReference(pair.Value.RewardTableId, rewards, $"BossCatalog.{pair.Key}.RewardTableId", report);
                RequireReference(pair.Value.PresentationKey, presentations, $"BossCatalog.{pair.Key}.PresentationKey", report);
                var boss = pair.Value;
                if (boss.MaxHealth != 3200 || boss.AttackDamage != 45 || boss.AttackIntervalTicks != 14 ||
                    boss.MovePerTick <= 0 || boss.CollisionRadius <= 0 || boss.AcquireRadius <= 0 ||
                    boss.LeashRadius < boss.AcquireRadius || boss.ReturnArmorPerTick < 0 || boss.RewardCoreLifetimeTicks != 250)
                    report.Add(ContentValidationCode.InvalidBossConfiguration, $"BossCatalog.{pair.Key}",
                        "Boss must satisfy the P1 prototype combat and 250-tick reward-core baseline.");
            }
        }

        private static void ValidateBattlefields(Dictionary<string, BattlefieldDefinition> battlefields,
            Dictionary<string, CampaignStageDefinition> stages, Dictionary<string, MapModeDefinition> modes,
            Dictionary<string, BossDefinition> bosses, Dictionary<string, RewardDefinition> rewards,
            Dictionary<string, SceneKeyDefinition> scenes, Dictionary<string, CardDefinition> cards,
            Dictionary<string, BuildingDefinition> buildings, Dictionary<string, UnitDefinition> units,
            Dictionary<string, ResourceDefinition> resources,
            ContentValidationReport report)
        {
            foreach (var pair in battlefields)
            {
                var battlefield = pair.Value;
                var path = $"BattlefieldCatalog.{pair.Key}";
                RequireReference(battlefield.SceneKey, scenes, path + ".SceneKey", report);
                RequireReference(battlefield.CampaignStageId, stages, path + ".CampaignStageId", report);
                RequireReference(battlefield.BossId, bosses, path + ".BossId", report);
                RequireReference(battlefield.RewardTableId, rewards, path + ".RewardTableId", report);
                ValidateResourceAmounts(battlefield.InitialPlayerInventory, resources, path + ".InitialPlayerInventory", report);
                if (battlefield.InitialPlayerInventory.Any(amount => amount?.ResourceId == ContentConstants.GoldResourceId))
                    report.Add(ContentValidationCode.InvalidResourceDefinition, path + ".InitialPlayerInventory", "Meta gold cannot be part of match inventory.");
                if (battlefield.InitialPlayerInventory.Any(amount => amount != null && IsWorldRawResource(amount.ResourceId)))
                    report.Add(ContentValidationCode.InvalidResourceDefinition, path + ".InitialPlayerInventory",
                        "Player raw-resource inventory must start at zero and be filled only by gatherer return.");
                ValidateGatherers(battlefield, units, resources, path, report);
                if (battlefield.DeploymentOrderTimeoutTicks <= 0)
                    report.Add(ContentValidationCode.InvalidDeploymentConfiguration, path + ".DeploymentOrderTimeoutTicks", "Deployment order timeout must be positive.");

                ValidateBattlefieldLayout(battlefield, resources, path, report);

                var resolvedModes = battlefield.MapModeIds.Where(id => modes.ContainsKey(id)).Select(id => modes[id]).ToArray();
                foreach (var id in battlefield.MapModeIds) RequireReference(id, modes, path + ".MapModeIds", report);
                if (battlefield.MapModeIds.Count != ContentConstants.RequiredMapModeCount ||
                    battlefield.MapModeIds.Distinct(StringComparer.Ordinal).Count() != ContentConstants.RequiredMapModeCount ||
                    resolvedModes.Select(mode => mode.Kind).Distinct().Count() != ContentConstants.RequiredMapModeCount)
                    report.Add(ContentValidationCode.InvalidMapModes, path + ".MapModeIds", "Battlefield must reference exactly one peaceful, offensive and nightmare mode.");

                ValidateInitialHand(battlefield.InitialHand, path + ".InitialHand", cards, buildings, report);
            }
        }

        private static void ValidateGatherers(BattlefieldDefinition battlefield,
            Dictionary<string, UnitDefinition> units, Dictionary<string, ResourceDefinition> resources,
            string path, ContentValidationReport report)
        {
            var gatherers = battlefield.Gatherers.Where(value => value != null).ToArray();
            var routeIds = battlefield.Routes.Where(value => value != null).Select(value => value.Id)
                .ToHashSet(StringComparer.Ordinal);
            if (gatherers.Length != 3 || gatherers.Select(value => value.SourceId)
                    .Distinct(StringComparer.Ordinal).Count() != 3 ||
                gatherers.Select(value => value.RouteId).Distinct(StringComparer.Ordinal).Count() != 3)
                report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".Gatherers",
                    "Schema v13 battlefield logistics require one stable free source on each of the three routes.");
            if (battlefield.GathererDispatchIntervalMinTicks != 150 ||
                battlefield.GathererDispatchIntervalMaxTicks != 200)
                report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".GathererDispatchIntervalTicks",
                    "Free gate gatherer dispatch interval must be deterministically sampled from 150-200 ticks.");
            if (gatherers.Any(value => value.CarryAmount != 3 ||
                    !new HashSet<string>(value.AllowedResourceIds, StringComparer.Ordinal).SetEquals(new[]
                    { ContentConstants.FoodResourceId, ContentConstants.WoodResourceId, ContentConstants.RawStoneResourceId })))
                report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".Gatherers",
                    "Each free gate source must carry 3 and allow the three no-duplicate opening resources.");

            var workerSpeeds = units.Values.Where(value => value.RoleTags.Contains("Worker"))
                .Select(value => value.MovePerTick).Distinct().ToArray();
            if (workerSpeeds.Length != 1 || workerSpeeds[0] <= 0)
                report.Add(ContentValidationCode.InvalidProductionConfiguration, "UnitCatalog.Worker.MovePerTick",
                    "All gatherer Worker units must share one positive MovePerTick value.");
            foreach (var gatherer in gatherers)
            {
                RequireReference(gatherer.UnitId, units, path + ".Gatherers.UnitId", report);
                if (string.IsNullOrWhiteSpace(gatherer.SourceId) || string.IsNullOrWhiteSpace(gatherer.RouteId) ||
                    !routeIds.Contains(gatherer.RouteId) || gatherer.AllowedResourceIds.Count == 0 ||
                    gatherer.AllowedResourceIds.Any(id => !IsWorldRawResource(id)) ||
                    gatherer.AllowedResourceIds.Distinct(StringComparer.Ordinal).Count() != gatherer.AllowedResourceIds.Count ||
                    gatherer.CarryAmount <= 0 || gatherer.GatherTicks <= 0)
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".Gatherers",
                        "Each gatherer source requires stable source/route ids, raw-resource permissions and positive carry/gather values.");
                foreach (var resourceId in gatherer.AllowedResourceIds)
                    RequireReference(resourceId, resources, path + ".Gatherers.AllowedResourceIds", report);
                if (!units.TryGetValue(gatherer.UnitId ?? string.Empty, out var unit)) continue;
                if (!unit.RoleTags.Contains("Worker") || unit.MovePerTick <= 0)
                    report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".Gatherers.UnitId",
                        "Gatherer sources must reference a Worker unit with positive movement speed.");
            }
            var configuredRaw = gatherers.SelectMany(value => value.AllowedResourceIds).Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (!configuredRaw.SequenceEqual(new[] { ContentConstants.FoodResourceId, ContentConstants.RawStoneResourceId,
                    ContentConstants.WoodResourceId }.OrderBy(value => value, StringComparer.Ordinal)))
                report.Add(ContentValidationCode.InvalidProductionConfiguration, path + ".Gatherers",
                    "The three free gate sources must collectively cover food, wood and raw stone without iron ore.");
        }

        private static void ValidateBattlefieldLayout(BattlefieldDefinition battlefield,
            Dictionary<string, ResourceDefinition> resources, string path, ContentValidationReport report)
        {
            if (battlefield.ReferenceWidth != 1920 || battlefield.ReferenceHeight != 1080 ||
                battlefield.PlayerWall == null || battlefield.EnemyWall == null ||
                battlefield.PlayerWall.MaxHealth != 5000 || battlefield.EnemyWall.MaxHealth != 5000)
                report.Add(ContentValidationCode.InvalidLayoutConfiguration, path,
                    "P1 battlefield uses 1920x1080 reference coordinates and two 5000-health walls.");

            var zones = battlefield.Zones.Where(zone => zone != null).ToArray();
            foreach (var zone in zones)
                if (string.IsNullOrWhiteSpace(zone.Id) || zone.Width <= 0 || zone.Height <= 0 || zone.X < 0 || zone.Y < 0 ||
                    zone.X + zone.Width > battlefield.ReferenceWidth || zone.Y + zone.Height > battlefield.ReferenceHeight)
                    report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".Zones", "Every uniquely identified zone must stay inside reference bounds.");
            if (zones.Select(zone => zone.Id).Distinct(StringComparer.Ordinal).Count() != zones.Length ||
                !zones.Any(zone => zone.Kind == ZoneKind.PlayerDeployment) || !zones.Any(zone => zone.Kind == ZoneKind.EnemyDeployment) ||
                !zones.Any(zone => zone.Kind == ZoneKind.TowerBuildable) || !zones.Any(zone => zone.Kind == ZoneKind.TowerForbidden) ||
                zones.Count(zone => zone.Kind == ZoneKind.MainGate) != 2)
                report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".Zones", "Deployment, tower and both main-gate zones are required with unique ids.");
            var enemyEightPercentStart = battlefield.ReferenceWidth * 92 / 100;
            if (!zones.Any(zone => zone.Kind == ZoneKind.TowerForbidden && zone.X <= enemyEightPercentStart && zone.X + zone.Width >= battlefield.ReferenceWidth))
                report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".Zones", "The enemy-wall-side 8% strip must be tower-forbidden.");

            if (battlefield.Routes.Count != 3 || battlefield.Routes.Any(route => route == null || string.IsNullOrWhiteSpace(route.Id) ||
                    route.Points.Count < 2 || route.Points.Any(point => !PointInBounds(point, battlefield))) ||
                battlefield.Routes.Select(route => route?.Id).Distinct(StringComparer.Ordinal).Count() != 3 || battlefield.MinimumRoadWidth < 54)
                report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".Routes", "Exactly three bounded soft routes and a minimum road width of 54 are required.");

            foreach (var node in battlefield.ResourceNodes)
            {
                if (node == null || string.IsNullOrWhiteSpace(node.Id) || node.Capacity <= 0 || !PointInBounds(node.Position, battlefield))
                    report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".ResourceNodes", "Resource nodes need ids, capacity and bounded positions.");
                else
                {
                    if (node.AllowedResourceIds.Count == 0)
                        report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".ResourceNodes.AllowedResourceIds", "Every candidate node needs at least one allowed raw resource.");
                    foreach (var resourceId in node.AllowedResourceIds)
                    {
                        RequireReference(resourceId, resources, path + ".ResourceNodes.AllowedResourceIds", report);
                        if (!IsWorldRawResource(resourceId))
                            report.Add(ContentValidationCode.InvalidResourceDefinition, path + ".ResourceNodes.AllowedResourceIds", $"Processed resource '{resourceId}' cannot be a world node.");
                    }
                    if (node.SpawnGroup != ResourceNodeSpawnGroup.Central && string.IsNullOrWhiteSpace(node.MirrorNodeId))
                        report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".ResourceNodes.MirrorNodeId", "Safe-side candidates require a stable mirror id.");
                }
            }
            if (battlefield.ResourceNodes.Select(node => node?.Id).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).Count() != battlefield.ResourceNodes.Count)
                report.Add(ContentValidationCode.DuplicateId, path + ".ResourceNodes", "Resource node ids must be unique.");
            var playerSafe = battlefield.ResourceNodes.Where(node => node?.SpawnGroup == ResourceNodeSpawnGroup.PlayerSafe).OrderBy(node => node.Id, StringComparer.Ordinal).ToArray();
            var central = battlefield.ResourceNodes.Where(node => node?.SpawnGroup == ResourceNodeSpawnGroup.Central).ToArray();
            var enemySafe = battlefield.ResourceNodes.Where(node => node?.SpawnGroup == ResourceNodeSpawnGroup.EnemySafe).ToDictionary(node => node.Id, StringComparer.Ordinal);
            if (playerSafe.Length != 3 || central.Length != 6 || enemySafe.Count != 3 ||
                central.Select(node => (node.Position.X, node.Position.Y)).Distinct().Count() != 6)
                report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".ResourceNodes",
                    "Schema v14 requires 3 player-safe, 6 unique central and 3 enemy-safe nodes.");
            foreach (var player in playerSafe)
                if (!enemySafe.TryGetValue(player.MirrorNodeId, out var enemy) ||
                    battlefield.EnemyWall.Gate.X - enemy.Position.X != player.Position.X - battlefield.PlayerWall.Gate.X)
                    report.Add(ContentValidationCode.InvalidLayoutConfiguration, path + ".ResourceNodes.MirrorDistance",
                        $"Safe node '{player.Id}' must have the same wall distance as its enemy mirror.");

            var spawns = battlefield.BossSpawns.Where(spawn => spawn != null).ToArray();
            if (spawns.Length != 2 || !spawns.Select(spawn => spawn.SpawnTick).SequenceEqual(new[] { 2700, 6300 }) ||
                spawns.Any(spawn => spawn.WarningTick != spawn.SpawnTick - 120 || !PointInBounds(spawn.Position, battlefield)))
                report.Add(ContentValidationCode.InvalidBossConfiguration, path + ".BossSpawns", "Bosses spawn at 2700/6300 ticks with a 120 tick warning at bounded points.");

            if (battlefield.MaxConstructionSites != 2 || battlefield.MaxCompletedTowers != 3 || battlefield.MaxActiveBuilders != 1 ||
                battlefield.BuilderRespawnTicks != 80 || battlefield.RetainedConstructionProgressMilli != 500)
                report.Add(ContentValidationCode.InvalidConstructionConfiguration, path,
                    "P1 construction limits are 2 sites, 3 towers, 1 builder, 80 respawn ticks and 50% retained progress.");
        }

private static void ValidateResourceWaves(
            Dictionary<string, ResourceActivationWaveDefinition> waves,
            Dictionary<string, MapModeDefinition> modes,
            Dictionary<string, ResourceDefinition> resources,
            ContentValidationReport report)
        {
            foreach (var pair in waves)
            {
                var wave = pair.Value;
                var path = "StageEffectCatalog.ResourceActivationWaves." + pair.Key;
                RequireReference(wave.MapModeId, modes, path + ".MapModeId", report);
                if (wave.TriggerSeconds < 0 || wave.NodesPerGroup <= 0 ||
                    wave.Groups.Count == 0 || wave.AllowedResourceIds.Count == 0)
                    report.Add(ContentValidationCode.InvalidLayoutConfiguration, path,
                        "Resource activation waves require a trigger, groups, count and raw resources.");
                foreach (var resourceId in wave.AllowedResourceIds)
                {
                    RequireReference(resourceId, resources, path + ".AllowedResourceIds", report);
                    if (!IsWorldRawResource(resourceId))
                        report.Add(ContentValidationCode.InvalidResourceDefinition,
                            path + ".AllowedResourceIds",
                            $"Processed resource '{resourceId}' cannot be activated as a world node.");
                }
            }

            foreach (var modeId in modes.Keys)
            {
                var modeWaves = waves.Values.Where(value => value.MapModeId == modeId)
                    .OrderBy(value => value.TriggerSeconds).ToArray();
                var path = $"StageEffectCatalog.ResourceActivationWaves.{modeId}";
                if (modeWaves.Length != 6 ||
                    !modeWaves.Select(value => value.TriggerSeconds)
                        .SequenceEqual(new[] { 0, 60, 120, 180, 240, 300 }))
                {
                    report.Add(ContentValidationCode.InvalidLayoutConfiguration, path,
                        "Schema v14 requires safe nodes and six central slots at 0/60/120/180/240/300 seconds.");
                    continue;
                }

                if (!new HashSet<string>(modeWaves[0].AllowedResourceIds, StringComparer.Ordinal)
                        .SetEquals(new[] { ContentConstants.FoodResourceId, ContentConstants.WoodResourceId, ContentConstants.RawStoneResourceId }) ||
                    !new HashSet<string>(modeWaves[1].AllowedResourceIds, StringComparer.Ordinal)
                        .SetEquals(new[] { ContentConstants.FoodResourceId, ContentConstants.WoodResourceId }) ||
                    !modeWaves[2].AllowedResourceIds.SequenceEqual(new[] { ContentConstants.RawStoneResourceId }) ||
                    !modeWaves[3].AllowedResourceIds.SequenceEqual(new[] { ContentConstants.IronOreResourceId }) ||
                    modeWaves.Skip(4).Any(wave => !new HashSet<string>(wave.AllowedResourceIds, StringComparer.Ordinal)
                        .SetEquals(WorldRawResourceIds)))
                    report.Add(ContentValidationCode.InvalidLayoutConfiguration, path,
                        "Schema v14 resource wave pools must end with two weighted all-resource waves.");
            }
        }

        private static readonly string[] WorldRawResourceIds =
        {
            ContentConstants.FoodResourceId, ContentConstants.WoodResourceId,
            ContentConstants.RawStoneResourceId, ContentConstants.IronOreResourceId
        };

        private static void ValidateHeatTiers(IReadOnlyList<HeatTierDefinition> tiers, ContentValidationReport report)
        {
            if (tiers == null || tiers.Count != ContentConstants.HeatTierStartTicks.Length ||
                !tiers.Select(value => value?.StartTick ?? -1).SequenceEqual(ContentConstants.HeatTierStartTicks) ||
                !tiers.Select(value => value?.RewardCooldownSeconds ?? -1).SequenceEqual(ContentConstants.OfferCooldownSeconds) ||
                !tiers.Select(value => value?.AiPressureIntervalMultiplierMilli ?? -1).SequenceEqual(ContentConstants.AiPressureIntervalMultipliersMilli) ||
                !tiers.Select(value => value?.AdvancedUnitWeightMultiplierMilli ?? -1).SequenceEqual(ContentConstants.AdvancedUnitWeightMultipliersMilli))
                report.Add(ContentValidationCode.InvalidAiConfiguration, "StageEffectCatalog.HeatTiers",
                    "Schema v14 requires the five fixed heat tiers for reward cooldown, AI pressure interval and advanced-unit weight.");
        }

        private static bool IsWorldRawResource(string resourceId) => WorldRawResourceIds.Contains(resourceId, StringComparer.Ordinal);

        private static bool PointInBounds(ReferencePointDefinition point, BattlefieldDefinition battlefield) =>
            point != null && point.X >= 0 && point.Y >= 0 && point.X <= battlefield.ReferenceWidth && point.Y <= battlefield.ReferenceHeight;

        private static void ValidateP1PrototypeValues(Dictionary<string, UnitDefinition> units,
            Dictionary<string, BuildingDefinition> buildings, Dictionary<string, CardDefinition> cards,
            ContentValidationReport report)
        {
            var requiredUnitIds = new[] { "unit.gatherer", "unit.lumberjack", "unit.stonecutter", "unit.iron-miner", "unit.builder", "unit.shield-guard", "unit.archer", "unit.siege-ram" };
            foreach (var id in requiredUnitIds) RequireReference(id, units, "UnitCatalog.P1Prototype", report);
            foreach (var id in new[] { "card.soldier.shield-guard", "card.soldier.archer", "card.soldier.siege-ram", "card.battlefield.arrow-tower", "card.tactic.arrow-rain", "card.tactic.field-rations", "card.tactic.emergency-supplies" })
                RequireReference(id, cards, "CardCatalog.P1Prototype", report);
            if (units.TryGetValue("unit.shield-guard", out var shield) &&
                (shield.MaxHealth != 360 || shield.AttackDamage != 24 || shield.MovePerTick != 4 || shield.AttackIntervalTicks != 10 || shield.AttackRange != 28))
                report.Add(ContentValidationCode.InvalidCombatConfiguration, "UnitCatalog.unit.shield-guard", "Shield Guard does not match the P1 Prototype table.");
            if (units.TryGetValue("unit.archer", out var archer) &&
                (archer.MaxHealth != 160 || archer.AttackDamage != 28 || archer.MovePerTick != 4 || archer.AttackIntervalTicks != 12 || archer.AttackRange != 180 || archer.ProjectileSpeedPerTick != 16))
                report.Add(ContentValidationCode.InvalidCombatConfiguration, "UnitCatalog.unit.archer", "Archer does not match the P1 Prototype table.");
            if (units.TryGetValue("unit.siege-ram", out var ram) &&
                (ram.MaxHealth != 480 || ram.AttackDamage != 16 || ram.WallDamageMultiplierMilli != 4000 || ram.MovePerTick != 3 || ram.AttackIntervalTicks != 16))
                report.Add(ContentValidationCode.InvalidCombatConfiguration, "UnitCatalog.unit.siege-ram", "Siege Ram does not match the P1 Prototype table.");
            if (!buildings.TryGetValue("building.arrow-tower", out var tower) || tower.Category != BuildingCategory.BattlefieldStructure ||
                tower.MaxHealth != 650 || tower.AttackDamage != 30 || tower.AttackIntervalTicks != 10 || tower.AttackRange != 220 ||
                tower.ConstructionTicks != 120 || tower.ConstructionCosts.Count != 1 || tower.ConstructionCosts[0].ResourceId != "resource.stone" || tower.ConstructionCosts[0].Amount != 60)
                report.Add(ContentValidationCode.InvalidConstructionConfiguration, "BuildingCatalog.building.arrow-tower", "Arrow Tower does not match the P1 Prototype table.");
        }

        private static void ValidateInitialHand(InitialHandRuleDefinition hand, string path,
            Dictionary<string, CardDefinition> cards, Dictionary<string, BuildingDefinition> buildings,
            ContentValidationReport report)
        {
            if (hand == null)
            {
                report.Add(ContentValidationCode.InvalidInitialHand, path, "Initial hand rule is missing.");
                return;
            }
            var guaranteed = hand.GuaranteedCardIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            var pool = guaranteed.Concat(hand.FillerCardIds).Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
            var allDefaultUnlocked = pool.All(id => cards.TryGetValue(id, out var card) && card.DefaultUnlocked);
            var hasFoodProcessing = guaranteed.Any(id => IsBuildingInputCard(id, ContentConstants.FoodResourceId, cards, buildings));
            var hasWoodProcessing = guaranteed.Any(id => IsBuildingInputCard(id, ContentConstants.WoodResourceId, cards, buildings));
            var hasPlank = guaranteed.Any(id => IsBuildingOutputCard(id, ContentConstants.PlankResourceId, cards, buildings));
            var hasCamp = guaranteed.Any(id => IsSoldierCampCard(id, cards, buildings));
            if (hand.HandSize != ContentConstants.RequiredInitialHandSize || guaranteed.Length > hand.HandSize ||
                pool.Length < hand.HandSize || !allDefaultUnlocked || !hasFoodProcessing || !hasWoodProcessing || !hasPlank || !hasCamp)
                report.Add(ContentValidationCode.InvalidInitialHand, path,
                    "Initial hand must be 6 default-unlocked cards and contain food/wood processing plus a soldier camp.");
        }

        private static bool IsBuildingInputCard(string cardId, string resourceId,
            Dictionary<string, CardDefinition> cards, Dictionary<string, BuildingDefinition> buildings)
        {
            return cards.TryGetValue(cardId, out var card) && card.Type == CardType.BuildingItem &&
                   buildings.TryGetValue(card.LinkedContentId ?? string.Empty, out var building) &&
                   building.Inputs.Any(input => input != null && input.ResourceId == resourceId && input.Amount > 0);
        }

        private static bool IsBuildingOutputCard(string cardId, string resourceId,
            Dictionary<string, CardDefinition> cards, Dictionary<string, BuildingDefinition> buildings)
        {
            return cards.TryGetValue(cardId, out var card) && card.Type == CardType.BuildingItem &&
                   buildings.TryGetValue(card.LinkedContentId ?? string.Empty, out var building) &&
                   building.Outputs.Any(output => output != null && output.ResourceId == resourceId && output.Amount > 0);
        }

        private static bool IsSoldierCampCard(string cardId, Dictionary<string, CardDefinition> cards,
            Dictionary<string, BuildingDefinition> buildings)
        {
            return cards.TryGetValue(cardId, out var card) && card.Type == CardType.BuildingItem &&
                   buildings.TryGetValue(card.LinkedContentId ?? string.Empty, out var building) &&
                   building.Category == BuildingCategory.SoldierCamp && !string.IsNullOrWhiteSpace(building.ActivatedSoldierCardId);
        }

        private static void ValidateContentReachability(Dictionary<string, RewardDefinition> rewards,
            Dictionary<string, BattlefieldDefinition> battlefields, Dictionary<string, CardDefinition> cards,
            Dictionary<string, BuildingDefinition> buildings, ContentValidationReport report)
        {
            var requiredPaths = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                ["heavy-warrior"] = new[] { "building.gatherer-lodge", "building.wood-gatherer-camp", "building.pasture", "building.iron-gatherer-camp", "building.iron-smelter", "building.heavy-warrior-camp" },
                ["mage"] = new[] { "building.gatherer-lodge", "building.wood-gatherer-camp", "building.winery", "building.iron-gatherer-camp", "building.iron-smelter", "building.mage-camp" },
                ["longbow"] = new[] { "building.gatherer-lodge", "building.winery", "building.longbow-camp" },
                ["cannon"] = new[] { "building.gatherer-lodge", "building.wood-gatherer-camp", "building.pasture", "building.stone-gatherer-camp", "building.iron-gatherer-camp", "building.stoneworks", "building.iron-smelter", "building.cannon-camp" },
                ["research"] = new[] { "building.shield-camp", "building.archer-camp", "building.research-lab" },
                ["specialist-gathering"] = new[] { "building.gatherer-lodge", "building.wood-gatherer-camp", "building.stone-gatherer-camp", "building.iron-gatherer-camp" }
            };

            foreach (var battlefield in battlefields.Values)
            {
                if (!rewards.TryGetValue(battlefield.RewardTableId ?? string.Empty, out var reward)) continue;
                var initial = battlefield.InitialHand.GuaranteedCardIds
                    .Where(cards.ContainsKey).Select(cardId => cards[cardId].LinkedContentId)
                    .Where(buildings.ContainsKey).ToHashSet(StringComparer.Ordinal);
                var recurringPool = reward.TimedCardOffers.SelectMany(value => value.FallbackCardIds)
                    .Where(cards.ContainsKey).Select(cardId => cards[cardId].LinkedContentId)
                    .Where(buildings.ContainsKey).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);

                foreach (var path in requiredPaths)
                {
                    if (path.Value.Length > 9 || path.Value.Any(value => !initial.Contains(value) && !recurringPool.Contains(value)))
                        report.Add(ContentValidationCode.InvalidOfferGuarantee,
                            $"BattlefieldCatalog.{battlefield.Id}.Reachability.{path.Key}",
                            $"No legal recurring-reward, nine-slot construction path reaches '{path.Key}'.");
                }
            }
        }

        private static void ValidateResourceAmounts(IReadOnlyList<ResourceAmountDefinition> amounts,
            Dictionary<string, ResourceDefinition> resources, string path, ContentValidationReport report)
        {
            if (amounts == null) return;
            for (var index = 0; index < amounts.Count; index++)
            {
                var amount = amounts[index];
                if (amount == null || amount.Amount <= 0 || !resources.ContainsKey(amount.ResourceId ?? string.Empty))
                    report.Add(ContentValidationCode.MissingReference, $"{path}[{index}]", "Resource amount must reference an existing resource with a positive amount.");
            }
        }

        private static void RequireReference<T>(string id, Dictionary<string, T> index, string path, ContentValidationReport report)
        {
            if (string.IsNullOrWhiteSpace(id) || !index.ContainsKey(id))
                report.Add(ContentValidationCode.MissingReference, path, $"Missing referenced id '{id ?? "<null>"}'.");
        }
    

private static void ValidateDifficulties(
            Dictionary<string, DifficultyRulesDefinition> difficulties,
            ContentValidationReport report)
        {
            foreach (var pair in difficulties)
            {
                var value = pair.Value;
                var path = $"StageEffectCatalog.DifficultyRules.{pair.Key}";
                if (value.ReactionDelayTicks < 15 || value.DecisionQualityMilli <= 0 ||
                    value.SuboptimalIntervalMinTicks < 0 ||
                    value.SuboptimalIntervalMaxTicks < value.SuboptimalIntervalMinTicks ||
                    value.TrainingTimeMultiplierMilli <= 0)
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path,
                        "Difficulty requires delayed perception, ordered recurring mistake intervals and positive quality/training values.");

                if (pair.Key == "difficulty.nightmare" &&
                    (value.SuboptimalIntervalMinTicks != 0 || value.SuboptimalIntervalMaxTicks != 0))
                    report.Add(ContentValidationCode.InvalidAiConfiguration, path,
                        "Nightmare mode cannot force suboptimal decisions.");
            }
        }
}
}
