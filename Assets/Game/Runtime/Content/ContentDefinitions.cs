using System;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.Serialization;
namespace FortressFrontier.Runtime.Content
{
    public enum ResourceScope { Match, Meta }
    public enum ResourceGroup { Food, Wood, Stone, Iron, Meta }
    public enum ResourceAcquisitionKind { BattlefieldGathered, Processed, Meta }
    public enum CardType { Soldier, BuildingItem, BattlefieldItem, Tactic, ReinforcementItem }
    public enum BuildingCategory { Gathering, Processing, Storage, Research, SoldierCamp, BattlefieldStructure }
    public enum MapModeKind { PeacefulDevelopment, ActiveOffense, Nightmare }
    public enum GathererResourceSelectionPolicy { Fixed, RoundRobin }

    [Serializable]
    public sealed class ResourceAmountDefinition
    {
        [SerializeField] private string _resourceId;
        [SerializeField] private int _amount;

        public string ResourceId => _resourceId;
        public int Amount => _amount;
    }

    [Serializable]
    public sealed class ResourceDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private ResourceScope _scope;
        [SerializeField] private ResourceGroup _group;
        [SerializeField] private ResourceAcquisitionKind _acquisitionKind;
        [SerializeField] private string _displayName;
        [SerializeField] private string _presentationKey;
        [SerializeField] private int _defaultCapacity;
        [SerializeField] private bool _canOverflow;
        [SerializeField, Min(0)] private int _displayPrecision;

        public string Id => _id;
        public ResourceScope Scope => _scope;
        public ResourceGroup Group => _group;
        public ResourceAcquisitionKind AcquisitionKind => _acquisitionKind;
        public string DisplayName => _displayName;
        public string PresentationKey => _presentationKey;
        public int DefaultCapacity => _defaultCapacity;
        public bool CanOverflow => _canOverflow;
        public int DisplayPrecision => _displayPrecision;
    }

    [Serializable]
    public sealed class UpgradeLevelDefinition
    {
        [SerializeField, Min(2)] private int _level = 2;
        [SerializeField, Min(0)] private int _requiredEffectiveWorkCount;
        [SerializeField] private string _requiredMatchPhaseId;
        [SerializeField] private string _paymentResourceId = ContentConstants.PlankResourceId;
        [SerializeField, Min(1)] private int _cost = 1;
        [SerializeField, Min(0f)] private float _durationSeconds;
        [SerializeField, Min(1)] private int _productionMultiplierMilli = 1000;
        [SerializeField, Min(1)] private int _trainingTimeMultiplierMilli = 1000;

        public int Level => _level;
        public int RequiredEffectiveWorkCount => _requiredEffectiveWorkCount;
        public string RequiredMatchPhaseId => _requiredMatchPhaseId;
        public string PaymentResourceId => _paymentResourceId;
        public int Cost => _cost;
        public float DurationSeconds => _durationSeconds;
        public int ProductionMultiplierMilli => _productionMultiplierMilli;
        public int TrainingTimeMultiplierMilli => _trainingTimeMultiplierMilli;
    }

    [Serializable]
    public sealed partial class BuildingDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _sourceCardId;
        [SerializeField] private BuildingCategory _category;
        [SerializeField] private List<ResourceAmountDefinition> _inputs = new();
        [SerializeField] private List<ResourceAmountDefinition> _outputs = new();
        [SerializeField] private List<ResourceAmountDefinition> _inputReserveFloors = new();
        [SerializeField] private string _workerUnitId;
        [SerializeField] private string _activatedSoldierCardId;
        [SerializeField, Min(1)] private int _productionCycleTicks = 50;
        [SerializeField, Min(0)] private int _workerGatherTicks = 30;
        [SerializeField] private List<UpgradeLevelDefinition> _upgradeLevels = new();
        [SerializeField] private string _presentationKey;

        public string Id => _id;
        public string SourceCardId => _sourceCardId;
        public BuildingCategory Category => _category;
        public IReadOnlyList<ResourceAmountDefinition> Inputs => _inputs;
        public IReadOnlyList<ResourceAmountDefinition> Outputs => _outputs;
        public IReadOnlyList<ResourceAmountDefinition> InputReserveFloors => _inputReserveFloors;
        public string WorkerUnitId => _workerUnitId;
        public string ActivatedSoldierCardId => _activatedSoldierCardId;
        public int ProductionCycleTicks => _productionCycleTicks;
        public int WorkerGatherTicks => _workerGatherTicks;
        public IReadOnlyList<UpgradeLevelDefinition> UpgradeLevels => _upgradeLevels;
        public string PresentationKey => _presentationKey;
    }

    [Serializable]
    public sealed class GrowthRuleDefinition
    {
        [SerializeField] private string _propertyKey;
        [SerializeField, Min(0)] private int _percentPerLevelBasisPoints = 400;

        public string PropertyKey => _propertyKey;
        public int PercentPerLevelBasisPoints => _percentPerLevelBasisPoints;
    }

    [Serializable]
    public sealed partial class CardDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private CardType _type;
        [SerializeField] private string _linkedContentId;
        [SerializeField] private string _activationCampBuildingId;
        [SerializeField] private bool _defaultUnlocked;
        [SerializeField, Min(0)] private int _unlockGoldCost;
        [SerializeField] private List<string> _prerequisiteCardIds = new();
        [SerializeField, Min(1)] private int _maxMetaLevel = 10;
        [SerializeField] private List<int> _upgradeGoldCosts = new();
        [SerializeField] private List<GrowthRuleDefinition> _growthRules = new();
        [SerializeField] private List<string> _offerTags = new();
        [SerializeField] private string _presentationKey;

        public string Id => _id;
        public CardType Type => _type;
        public string LinkedContentId => _linkedContentId;
        public string ActivationCampBuildingId => _activationCampBuildingId;
        public bool DefaultUnlocked => _defaultUnlocked;
        public int UnlockGoldCost => _unlockGoldCost;
        public IReadOnlyList<string> PrerequisiteCardIds => _prerequisiteCardIds;
        public int MaxMetaLevel => _maxMetaLevel;
        public IReadOnlyList<int> UpgradeGoldCosts => _upgradeGoldCosts;
        public IReadOnlyList<GrowthRuleDefinition> GrowthRules => _growthRules;
        public IReadOnlyList<string> OfferTags => _offerTags;
        public string PresentationKey => _presentationKey;
    }

    [Serializable]
    public sealed partial class UnitDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private List<ResourceAmountDefinition> _trainingCosts = new();
        [SerializeField, Min(0f)] private float _baseTrainingSeconds;
        [SerializeField] private List<string> _roleTags = new();
        [SerializeField] private string _presentationKey;
        [SerializeField] private string _playerWorldPrefabPresentationKey;
        [SerializeField] private string _enemyWorldPrefabPresentationKey;

        public string Id => _id;
        public string DisplayName => _displayName;
        public IReadOnlyList<ResourceAmountDefinition> TrainingCosts => _trainingCosts;
        public float BaseTrainingSeconds => _baseTrainingSeconds;
        public IReadOnlyList<string> RoleTags => _roleTags;
        public string PresentationKey => _presentationKey;
        public string PlayerWorldPrefabPresentationKey => _playerWorldPrefabPresentationKey;
        public string EnemyWorldPrefabPresentationKey => _enemyWorldPrefabPresentationKey;
    }

    [Serializable]
    public sealed class InitialHandRuleDefinition
    {
        [SerializeField, Min(1)] private int _handSize = 6;
        [SerializeField] private List<string> _guaranteedCardIds = new();
        [SerializeField] private List<string> _fillerCardIds = new();

        public int HandSize => _handSize;
        public IReadOnlyList<string> GuaranteedCardIds => _guaranteedCardIds;
        public IReadOnlyList<string> FillerCardIds => _fillerCardIds;
    }

    [Serializable]
    public sealed partial class BattlefieldDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private string _sceneKey;
        [SerializeField] private string _mapPresentationKey;
        [SerializeField] private string _campaignStageId;
        [SerializeField] private List<string> _mapModeIds = new();
        [SerializeField] private string _bossId;
        [SerializeField] private string _rewardTableId;
        [SerializeField] private InitialHandRuleDefinition _initialHand = new();
        [SerializeField] private List<ResourceAmountDefinition> _initialPlayerInventory = new();
        [SerializeField, Min(1)] private int _deploymentOrderTimeoutTicks = 300;

        public string Id => _id;
        public string DisplayName => _displayName;
        public string SceneKey => _sceneKey;
        public string MapPresentationKey => _mapPresentationKey;
        public string CampaignStageId => _campaignStageId;
        public IReadOnlyList<string> MapModeIds => _mapModeIds;
        public string BossId => _bossId;
        public string RewardTableId => _rewardTableId;
        public InitialHandRuleDefinition InitialHand => _initialHand;
        public IReadOnlyList<ResourceAmountDefinition> InitialPlayerInventory => _initialPlayerInventory;
        public int DeploymentOrderTimeoutTicks => _deploymentOrderTimeoutTicks;
    }

    [Serializable]
    public sealed partial class BossDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _rewardTableId;
        [SerializeField] private string _presentationKey;

        public string Id => _id;
        public string RewardTableId => _rewardTableId;
        public string PresentationKey => _presentationKey;
    }

    [Serializable]
    public sealed class TimedCardOfferRule
    {
        [SerializeField, Min(0)] private int _triggerSeconds;
        [SerializeField, Min(1)] private int _candidateCount = 3;
        [SerializeField] private List<string> _fallbackCardIds = new();

        public int TriggerSeconds => _triggerSeconds;
        public int CandidateCount => _candidateCount;
        public IReadOnlyList<string> FallbackCardIds => _fallbackCardIds;
    }

    [Serializable]
    public sealed partial class RewardDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private List<TimedCardOfferRule> _timedCardOffers = new();
        [SerializeField, Min(0)] private int _completionGold;
        [SerializeField, Min(0)] private int _victoryGold;
        [SerializeField, Min(0)] private int _firstClearGold;

        public string Id => _id;
        public IReadOnlyList<TimedCardOfferRule> TimedCardOffers => _timedCardOffers;
        public int CompletionGold => _completionGold;
        public int VictoryGold => _victoryGold;
        public int FirstClearGold => _firstClearGold;
    }

    [Serializable]
    public sealed class CampaignStageDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _prerequisiteStageId;
        [SerializeField] private List<string> _unlockedBattlefieldIds = new();
        [SerializeField] private List<string> _purchasableCardIds = new();

        public string Id => _id;
        public string PrerequisiteStageId => _prerequisiteStageId;
        public IReadOnlyList<string> UnlockedBattlefieldIds => _unlockedBattlefieldIds;
        public IReadOnlyList<string> PurchasableCardIds => _purchasableCardIds;
    }

    [Serializable]
    public sealed class MapModeDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private MapModeKind _kind;
        [SerializeField] private string _aiDoctrineId;
        [SerializeField] private string _difficultyRulesId;
        [SerializeField] private string _aiPhaseProfileId;
        [SerializeField] private string _aiUtilityProfileId;
        [SerializeField] private string _enemyEconomyProfileId;
        [SerializeField] private string _enemyUnitPoolId;
        [SerializeField] private string _rewardTableId;
        [SerializeField, Min(1)] private int _rewardMultiplierMilli = 1000;

        public string Id => _id;
        public MapModeKind Kind => _kind;
        public string AiDoctrineId => _aiDoctrineId;
        public string DifficultyRulesId => _difficultyRulesId;
        public string AiPhaseProfileId => _aiPhaseProfileId;
        public string AiUtilityProfileId => _aiUtilityProfileId;
        public string EnemyEconomyProfileId => _enemyEconomyProfileId;
        public string EnemyUnitPoolId => _enemyUnitPoolId;
        public string RewardTableId => _rewardTableId;
        public int RewardMultiplierMilli => _rewardMultiplierMilli;
    }

    [Serializable]
    public sealed class HeatTierDefinition
    {
        [SerializeField, Min(0)] private int _startTick;
        [SerializeField, Min(1)] private int _rewardCooldownSeconds = 90;
        [SerializeField, Min(1)] private int _aiPressureIntervalMultiplierMilli = 1000;
        [SerializeField, Min(1)] private int _advancedUnitWeightMultiplierMilli = 1000;

        public int StartTick => _startTick;
        public int RewardCooldownSeconds => _rewardCooldownSeconds;
        public int AiPressureIntervalMultiplierMilli => _aiPressureIntervalMultiplierMilli;
        public int AdvancedUnitWeightMultiplierMilli => _advancedUnitWeightMultiplierMilli;
    }

    [Serializable]
    public sealed class AiIntentWeightDefinition
    {
        [SerializeField] private string _intentId;
        [SerializeField] private int _weight;
        public string IntentId => _intentId;
        public int Weight => _weight;
    }

    [Serializable]
    public sealed class AiPhaseDefinition
    {
        [SerializeField] private string _id;
        [SerializeField, Min(0)] private int _startTick;
        [SerializeField] private List<string> _allowedIntentIds = new();
        [SerializeField] private List<AiIntentWeightDefinition> _baseIntentWeights = new();
        [SerializeField] private List<string> _publicEventIds = new();
        public string Id => _id;
        public int StartTick => _startTick;
        public IReadOnlyList<string> AllowedIntentIds => _allowedIntentIds;
        public IReadOnlyList<AiIntentWeightDefinition> BaseIntentWeights => _baseIntentWeights;
        public IReadOnlyList<string> PublicEventIds => _publicEventIds;
    }

    [Serializable]
    public sealed class AiPhaseProfileDefinition
    {
        [SerializeField] private string _id;
        [SerializeField, Min(0)] private int _firstProbeStartTick = 600;
        [SerializeField, Min(0)] private int _firstProbeEndTick = 800;
        [SerializeField] private List<int> _phaseStartTicks = new();
        
        [SerializeField, Min(0)] private int _publicAccelerationStartTick = 9000;
        [SerializeField, Min(1000)] private int _publicProductionMultiplierMilli = 2000;
[SerializeField] private List<AiPhaseDefinition> _phases = new();
        public string Id => _id;
        public int FirstProbeStartTick => _firstProbeStartTick;
        public int FirstProbeEndTick => _firstProbeEndTick;
        public IReadOnlyList<int> PhaseStartTicks => _phaseStartTicks;
        
        public int PublicAccelerationStartTick => _publicAccelerationStartTick;
        public int PublicProductionMultiplierMilli => _publicProductionMultiplierMilli;
public IReadOnlyList<AiPhaseDefinition> Phases => _phases;
    }

    [Serializable]
    public sealed partial class AiUtilityProfileDefinition
    {
        [SerializeField] private string _id;
        [SerializeField, Min(1)] private int _temperatureMilli = 800;
        [SerializeField, Min(1)] private int _decisionIntervalTicks = 80;
        public string Id => _id;
        public int TemperatureMilli => _temperatureMilli;
        public int DecisionIntervalTicks => _decisionIntervalTicks;
    }

    [Serializable]
    public sealed partial class EnemyEconomyProfileDefinition
    {
        [SerializeField] private string _id;
        [SerializeField, Range(900, 1100)] private int _trainingTimeMultiplierMilli = 1000;
        [SerializeField, Range(1000, 1100)] private int _economicEfficiencyMilli = 1000;
        public string Id => _id;
        public int TrainingTimeMultiplierMilli => _trainingTimeMultiplierMilli;
        public int EconomicEfficiencyMilli => _economicEfficiencyMilli;
    }

    [Serializable]
    public sealed class AiDoctrineDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private List<AiIntentWeightDefinition> _intentBiases = new();
        public string Id => _id;
        public string DisplayName => _displayName;
        public IReadOnlyList<AiIntentWeightDefinition> IntentBiases => _intentBiases;
    }

    [Serializable]
    public sealed class DifficultyRulesDefinition
    {
        [SerializeField] private string _id;
        [SerializeField, Min(1)] private int _decisionQualityMilli = 1000;
        [SerializeField, Min(15)] private int _reactionDelayTicks = 15;
        [FormerlySerializedAs("_suboptimalWindowStartTick")]
        [SerializeField, Min(0)] private int _suboptimalIntervalMinTicks = 600;
        [FormerlySerializedAs("_suboptimalWindowEndTick")]
        [SerializeField, Min(0)] private int _suboptimalIntervalMaxTicks = 900;
        [SerializeField, Min(1)] private int _trainingTimeMultiplierMilli = 1000;
        public string Id => _id;
        public int DecisionQualityMilli => _decisionQualityMilli;
        public int ReactionDelayTicks => _reactionDelayTicks;
        public int SuboptimalIntervalMinTicks => _suboptimalIntervalMinTicks;
        public int SuboptimalIntervalMaxTicks => _suboptimalIntervalMaxTicks;
        public int TrainingTimeMultiplierMilli => _trainingTimeMultiplierMilli;
    }

    [Serializable]
    public sealed class EnemyUnitPoolDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private List<string> _unitIds = new();
        public string Id => _id;
        public IReadOnlyList<string> UnitIds => _unitIds;
    }

    [Serializable]
    public sealed class SceneKeyDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _resourceKey;
        public string Id => _id;
        public string ResourceKey => _resourceKey;
    }

    [Serializable]
    public sealed class PresentationDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _resourceKey;
        public string Id => _id;
        public string ResourceKey => _resourceKey;
    }

    public static class ContentConstants
    {
        public const int ExpectedSchemaVersion = 14;
        public const string FoodResourceId = "resource.food";
        public const string WoodResourceId = "resource.wood";
        public const string GoldResourceId = "resource.gold";
        public const string PlankResourceId = "resource.plank";
        public const string RawStoneResourceId = "resource.raw-stone";
        public const string IronOreResourceId = "resource.iron-ore";
        public const string ShieldGuardUnitId = "unit.shield-guard";
        public const string ArcherUnitId = "unit.archer";
        public const int RequiredInitialHandSize = 6;
        public const int RequiredMapModeCount = 3;
        public const int FixedTicksPerSecond = 10;
        public const int RequiredAiPhaseCount = 3;
        public static readonly int[] P1OfferSeconds = { 60 };
        public static readonly int[] HeatTierStartTicks = { 0, 1800, 3600, 5400, 7200 };
        public static readonly int[] AiPressureIntervalMultipliersMilli = { 1000, 950, 900, 850, 800 };
        public static readonly int[] AdvancedUnitWeightMultipliersMilli = { 1000, 1100, 1250, 1450, 1650 };
        public static readonly int[] OfferCooldownSeconds = { 60, 55, 50, 45, 45 };
        public static readonly int[][] RewardRarityWeights =
        {
            new[] { 100, 0, 0 }, new[] { 75, 25, 0 }, new[] { 60, 40, 0 },
            new[] { 50, 40, 10 }, new[] { 45, 40, 15 }
        };
        public static readonly string[] P1InitialBuildingCardIds =
        {
            "card.building.gatherer-lodge", "card.building.wood-gatherer-camp",
            "card.building.winery", "card.building.sawmill",
            "card.building.shield-camp", "card.building.archer-camp"
        };
        public static readonly string[][] P1OfferCardIds =
        {
            new[] { "card.building.stone-gatherer-camp", "card.building.research-lab", "card.building.longbow-camp" },
            new[] { "card.building.iron-gatherer-camp", "card.building.ram-camp", "card.building.research-lab" },
            new[] { "card.building.pasture", "card.building.mage-camp", "card.building.heavy-warrior-camp" },
            new[] { "card.building.iron-smelter", "card.building.longbow-camp", "card.building.research-lab" },
            new[] { "card.building.stoneworks", "card.building.heavy-warrior-camp", "card.building.mage-camp" },
            new[] { "card.building.cannon-camp", "card.building.heavy-warrior-camp", "card.building.mage-camp" }
        };
        public static readonly string[] P1AiIntentIds =
        {
            "intent.develop", "intent.hold", "intent.assault", "intent.raid-economy",
            "intent.build-tower", "intent.research", "intent.reserve"
        };
    }
}
