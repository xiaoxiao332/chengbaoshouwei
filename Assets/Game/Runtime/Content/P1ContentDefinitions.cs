using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace FortressFrontier.Runtime.Content
{
    public enum ResourceNodeSpawnGroup { PlayerSafe, Central, EnemySafe }

    public enum UnitTargetPriority { ThreatThenDistance, DistanceThenThreat, WallUnlessThreatened, StructuresOnly }
    public enum TacticTargetKind { None, FriendlyWall, BattlefieldPoint, BattlefieldArea }
    public enum TacticEffectKind { AddResource, AreaDamage, RepairWall, TimedUnitBuff }
    public enum ResearchCategory { Melee, Ranged, Magic, Siege }
    public enum UnitProjectileKind { None, Arrow, Fireball, Cannonball }
    public enum ZoneKind { PlayerDeployment, EnemyDeployment, TowerBuildable, TowerForbidden, BossForbidden, MainGate }
    public enum BossRewardKind { ExtraOffer, ResearchUpgrade, ResourceBundle, TrainingBoost, EnemyUnitLevel, EnemyWallArmor }
    public enum AiCommitmentPolicy { Duration, OrderComplete, PaymentCommitted, ConstructionSiteCreated }
    public enum RewardRarity { Common, Rare, Epic }

    [Serializable]
    public sealed class ReferencePointDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private int _x;
        [SerializeField] private int _y;
        public string Id => _id;
        public int X => _x;
        public int Y => _y;
    }

    [Serializable]
    public sealed class ReferenceRectDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private ZoneKind _kind;
        [SerializeField] private int _x;
        [SerializeField] private int _y;
        [SerializeField, Min(1)] private int _width = 1;
        [SerializeField, Min(1)] private int _height = 1;
        public string Id => _id;
        public ZoneKind Kind => _kind;
        public int X => _x;
        public int Y => _y;
        public int Width => _width;
        public int Height => _height;
    }

    public sealed partial class UnitDefinition
    {
        [SerializeField, Min(1)] private int _maxHealth = 1;
        [SerializeField, Min(0)] private int _attackDamage;
        [SerializeField, Min(0)] private int _wallDamageMultiplierMilli = 1000;
        [SerializeField, Min(0)] private int _movePerTick;
        [SerializeField, Min(1)] private int _collisionRadius = 1;
        [SerializeField, Min(0)] private int _acquireRadius;
        [SerializeField, Min(0)] private int _chaseRadius;
        [SerializeField, Min(0)] private int _attackRange;
        [SerializeField, Min(1)] private int _attackIntervalTicks = 1;
        [SerializeField, Min(0)] private int _projectileSpeedPerTick;
        [SerializeField] private UnitTargetPriority _targetPriority;
        [SerializeField] private bool _canAttack;
        [SerializeField] private ResearchCategory _researchCategory;
        [SerializeField] private UnitProjectileKind _projectileKind;
        [SerializeField, Min(0)] private int _explosionRadius;
        [SerializeField, Range(0, 1000)] private int _explosionSecondaryDamageMilli;
        [SerializeField] private string _projectilePresentationKey;
        public int MaxHealth => _maxHealth;
        public int AttackDamage => _attackDamage;
        public int WallDamageMultiplierMilli => _wallDamageMultiplierMilli;
        public int MovePerTick => _movePerTick;
        public int CollisionRadius => _collisionRadius;
        public int AcquireRadius => _acquireRadius;
        public int ChaseRadius => _chaseRadius;
        public int AttackRange => _attackRange;
        public int AttackIntervalTicks => _attackIntervalTicks;
        public int ProjectileSpeedPerTick => _projectileSpeedPerTick;
        public UnitTargetPriority TargetPriority => _targetPriority;
        public bool CanAttack => _canAttack;
        public ResearchCategory ResearchCategory => _researchCategory;
        public UnitProjectileKind ProjectileKind => _projectileKind;
        public int ExplosionRadius => _explosionRadius;
        public int ExplosionSecondaryDamageMilli => _explosionSecondaryDamageMilli;
        public string ProjectilePresentationKey => _projectilePresentationKey;
    }

    [Serializable]
    public sealed class TacticEffectDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private TacticEffectKind _kind;
        [SerializeField] private TacticTargetKind _targetKind;
        [SerializeField] private List<ResourceAmountDefinition> _resourceAmounts = new();
        [SerializeField] private int _magnitude;
        [SerializeField, Min(0)] private int _radius;
        [SerializeField, Min(0)] private int _durationTicks;
        [SerializeField, Min(0)] private int _perMatchLimit;
        public string Id => _id;
        public TacticEffectKind Kind => _kind;
        public TacticTargetKind TargetKind => _targetKind;
        public IReadOnlyList<ResourceAmountDefinition> ResourceAmounts => _resourceAmounts;
        public int Magnitude => _magnitude;
        public int Radius => _radius;
        public int DurationTicks => _durationTicks;
        public int PerMatchLimit => _perMatchLimit;
    }

    public sealed partial class BuildingDefinition
    {
        [SerializeField, Min(0)] private int _maxHealth;
        [SerializeField, Min(0)] private int _attackDamage;
        [SerializeField, Min(1)] private int _attackIntervalTicks = 1;
        [SerializeField, Min(0)] private int _attackRange;
        [SerializeField, Min(0)] private int _projectileSpeedPerTick;
        [SerializeField, Min(0)] private int _constructionTicks;
        [SerializeField] private List<ResourceAmountDefinition> _constructionCosts = new();
        [SerializeField] private string _researchBagId;
        [SerializeField] private List<string> _gathererAllowedResourceIds = new();
        [SerializeField] private List<ResourceAmountDefinition> _gathererDispatchCosts = new();
        [SerializeField, Min(1)] private int _gathererDispatchIntervalTicks = 250;
        [SerializeField, Min(1)] private int _gathererCarryAmount = 3;
        [SerializeField] private GathererResourceSelectionPolicy _gathererResourceSelectionPolicy;
        public int MaxHealth => _maxHealth;
        public int AttackDamage => _attackDamage;
        public int AttackIntervalTicks => _attackIntervalTicks;
        public int AttackRange => _attackRange;
        public int ProjectileSpeedPerTick => _projectileSpeedPerTick;
        public int ConstructionTicks => _constructionTicks;
        public IReadOnlyList<ResourceAmountDefinition> ConstructionCosts => _constructionCosts;
        public string ResearchBagId => _researchBagId;
        public IReadOnlyList<string> GathererAllowedResourceIds => _gathererAllowedResourceIds;
        public IReadOnlyList<ResourceAmountDefinition> GathererDispatchCosts => _gathererDispatchCosts;
        public int GathererDispatchIntervalTicks => _gathererDispatchIntervalTicks;
        public int GathererCarryAmount => _gathererCarryAmount;
        public GathererResourceSelectionPolicy GathererResourceSelectionPolicy => _gathererResourceSelectionPolicy;
    }

    [Serializable]
    public sealed class ResearchModifierDefinition
    {
        [SerializeField] private string _propertyKey;
        [SerializeField, Min(0)] private int _percentPerRankBasisPoints;
        public string PropertyKey => _propertyKey;
        public int PercentPerRankBasisPoints => _percentPerRankBasisPoints;
    }

    [Serializable]
    public sealed class ResearchUpgradeDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private ResearchCategory _targetRole;
        [SerializeField] private List<ResearchModifierDefinition> _modifiers = new();
        [SerializeField, Min(1)] private int _maxRank = 3;
        [SerializeField] private string _presentationKey;
        public string Id => _id;
        public ResearchCategory TargetRole => _targetRole;
        public IReadOnlyList<ResearchModifierDefinition> Modifiers => _modifiers;
        public int MaxRank => _maxRank;
        public string PresentationKey => _presentationKey;
    }

    [Serializable]
    public sealed class ResearchBagDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private List<string> _upgradeIds = new();
        [SerializeField] private List<ResourceAmountDefinition> _costs = new();
        [SerializeField, Min(1)] private int _researchTicks = 1;
        [SerializeField, Range(1, 3)] private int _candidateCount = 3;
        public string Id => _id;
        public IReadOnlyList<string> UpgradeIds => _upgradeIds;
        public IReadOnlyList<ResourceAmountDefinition> Costs => _costs;
        public int ResearchTicks => _researchTicks;
        public int CandidateCount => _candidateCount;
    }

    [Serializable]
    public sealed class WallDefinition
    {
        [SerializeField] private string _id;
        [SerializeField, Min(1)] private int _maxHealth = 1;
        [SerializeField] private ReferencePointDefinition _gate = new();
        public string Id => _id;
        public int MaxHealth => _maxHealth;
        public ReferencePointDefinition Gate => _gate;
    }

    [Serializable]
    public sealed class RouteDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private List<ReferencePointDefinition> _points = new();
        public string Id => _id;
        public IReadOnlyList<ReferencePointDefinition> Points => _points;
    }

    [Serializable]
    public sealed class ResourceNodeDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _resourceId;
        [SerializeField] private ResourceNodeSpawnGroup _spawnGroup;
        [SerializeField] private string _mirrorNodeId;
        [SerializeField] private List<string> _allowedResourceIds = new();
        [SerializeField] private ReferencePointDefinition _position = new();
        [SerializeField, Min(1)] private int _capacity = 1;
        [SerializeField, Min(0)] private int _respawnCapacity;
        [SerializeField, Min(1)] private int _respawnDelayTicks = 1;
        public string Id => _id;
        public string ResourceId => _resourceId;
        public ResourceNodeSpawnGroup SpawnGroup => _spawnGroup;
        public string MirrorNodeId => _mirrorNodeId;
        public IReadOnlyList<string> AllowedResourceIds => _allowedResourceIds;
        public ReferencePointDefinition Position => _position;
        public int Capacity => _capacity;
        public int RespawnCapacity => _respawnCapacity;
        public int RespawnDelayTicks => _respawnDelayTicks;
    }

    [Serializable]
    public sealed class ResourceActivationWaveDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _mapModeId;
        [SerializeField, Min(0)] private int _triggerSeconds;
        [SerializeField, Min(1)] private int _nodesPerGroup = 1;
        [SerializeField] private List<ResourceNodeSpawnGroup> _groups = new();
        [SerializeField] private List<string> _allowedResourceIds = new();

        public string Id => _id;
        public string MapModeId => _mapModeId;
        public int TriggerSeconds => _triggerSeconds;
        public int NodesPerGroup => _nodesPerGroup;
        public IReadOnlyList<ResourceNodeSpawnGroup> Groups => _groups;
        public IReadOnlyList<string> AllowedResourceIds => _allowedResourceIds;
    }

    [Serializable]
    public sealed class BossSpawnDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private ReferencePointDefinition _position = new();
        [SerializeField, Min(0)] private int _warningTick;
        [SerializeField, Min(0)] private int _spawnTick;
        public string Id => _id;
        public ReferencePointDefinition Position => _position;
        public int WarningTick => _warningTick;
        public int SpawnTick => _spawnTick;
    }

    [Serializable]
    public sealed class BattlefieldGathererDefinition
    {
        [SerializeField] private string _sourceId;
        [SerializeField] private string _routeId;
        [SerializeField] private string _unitId;
        [SerializeField] private List<string> _allowedResourceIds = new();
        [SerializeField, Min(1)] private int _carryAmount = 4;
        [SerializeField, Min(1)] private int _gatherTicks = 30;

        public string SourceId => _sourceId;
        public string RouteId => _routeId;
        public string UnitId => _unitId;
        public IReadOnlyList<string> AllowedResourceIds => _allowedResourceIds;
        public int CarryAmount => _carryAmount;
        public int GatherTicks => _gatherTicks;
    }

    public sealed partial class BattlefieldDefinition
    {
        [SerializeField, Min(1)] private int _referenceWidth = 1920;
        [SerializeField, Min(1)] private int _referenceHeight = 1080;
        [SerializeField] private WallDefinition _playerWall = new();
        [SerializeField] private WallDefinition _enemyWall = new();
        [SerializeField] private List<ReferenceRectDefinition> _zones = new();
        [SerializeField] private List<RouteDefinition> _routes = new();
        [SerializeField] private List<ResourceNodeDefinition> _resourceNodes = new();
        [SerializeField] private List<BattlefieldGathererDefinition> _gatherers = new();
        [FormerlySerializedAs("_gathererDispatchIntervalTicks")]
        [SerializeField, Min(1)] private int _gathererDispatchIntervalMinTicks = 150;
        [SerializeField, Min(1)] private int _gathererDispatchIntervalMaxTicks = 200;
        [SerializeField] private List<BossSpawnDefinition> _bossSpawns = new();
        [SerializeField, Min(1)] private int _minimumRoadWidth = 54;
        [SerializeField, Min(1)] private int _maxConstructionSites = 2;
        [SerializeField, Min(1)] private int _maxCompletedTowers = 3;
        [SerializeField, Min(1)] private int _maxActiveBuilders = 1;
        [SerializeField, Min(1)] private int _builderRespawnTicks = 80;
        [SerializeField, Range(0, 1000)] private int _retainedConstructionProgressMilli = 500;
        public int ReferenceWidth => _referenceWidth;
        public int ReferenceHeight => _referenceHeight;
        public WallDefinition PlayerWall => _playerWall;
        public WallDefinition EnemyWall => _enemyWall;
        public IReadOnlyList<ReferenceRectDefinition> Zones => _zones;
        public IReadOnlyList<RouteDefinition> Routes => _routes;
        public IReadOnlyList<ResourceNodeDefinition> ResourceNodes => _resourceNodes;
        public IReadOnlyList<BattlefieldGathererDefinition> Gatherers => _gatherers;
        public int GathererDispatchIntervalTicks => _gathererDispatchIntervalMinTicks;
        public int GathererDispatchIntervalMinTicks => _gathererDispatchIntervalMinTicks;
        public int GathererDispatchIntervalMaxTicks => _gathererDispatchIntervalMaxTicks;
        public IReadOnlyList<BossSpawnDefinition> BossSpawns => _bossSpawns;
        public int MinimumRoadWidth => _minimumRoadWidth;
        public int MaxConstructionSites => _maxConstructionSites;
        public int MaxCompletedTowers => _maxCompletedTowers;
        public int MaxActiveBuilders => _maxActiveBuilders;
        public int BuilderRespawnTicks => _builderRespawnTicks;
        public int RetainedConstructionProgressMilli => _retainedConstructionProgressMilli;
    }

    public sealed partial class BossDefinition
    {
        [SerializeField, Min(1)] private int _maxHealth = 1;
        [SerializeField, Min(0)] private int _armor;
        [SerializeField, Min(0)] private int _attackDamage;
        [SerializeField, Min(1)] private int _attackIntervalTicks = 1;
        [SerializeField, Min(0)] private int _movePerTick;
        [SerializeField, Min(1)] private int _collisionRadius = 1;
        [SerializeField, Min(0)] private int _acquireRadius;
        [SerializeField, Min(0)] private int _leashRadius;
        [SerializeField, Min(0)] private int _returnArmorPerTick;
        [SerializeField, Min(1)] private int _rewardCoreLifetimeTicks = 250;
        public int MaxHealth => _maxHealth;
        public int Armor => _armor;
        public int AttackDamage => _attackDamage;
        public int AttackIntervalTicks => _attackIntervalTicks;
        public int MovePerTick => _movePerTick;
        public int CollisionRadius => _collisionRadius;
        public int AcquireRadius => _acquireRadius;
        public int LeashRadius => _leashRadius;
        public int ReturnArmorPerTick => _returnArmorPerTick;
        public int RewardCoreLifetimeTicks => _rewardCoreLifetimeTicks;
    }

    [Serializable]
    public sealed class BossRewardEntryDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private BossRewardKind _kind;
        [SerializeField] private int _weight = 1;
        [SerializeField] private int _magnitude;
        [SerializeField, Min(0)] private int _durationTicks;
        public string Id => _id;
        public BossRewardKind Kind => _kind;
        public int Weight => _weight;
        public int Magnitude => _magnitude;
        public int DurationTicks => _durationTicks;
    }

    public sealed partial class RewardDefinition
    {
        [SerializeField, Min(1)] private int _handLimit = 6;
        [SerializeField] private bool _allowFullHandDiscard = true;
        [SerializeField] private ResourceAmountDefinition _fullHandExchange = new();
        [SerializeField] private List<BossRewardEntryDefinition> _playerBossRewards = new();
        [SerializeField] private List<BossRewardEntryDefinition> _enemyBossRewards = new();
        [SerializeField, Min(1)] private int _bossRewardBudgetMilli = 700;
        [SerializeField] private List<RewardRarityWeightDefinition> _rarityWeights = new();
        [SerializeField] private List<ProcessedResourceBundleDefinition> _processedResourceBundles = new();
        [SerializeField] private List<ReinforcementTemplateDefinition> _reinforcementTemplates = new();
        [SerializeField] private string _buildingRewardPresentationKey;
        [SerializeField] private string _resourceRewardPresentationKey;
        [SerializeField] private string _reinforcementRewardPresentationKey;
        public int HandLimit => _handLimit;
        public bool AllowFullHandDiscard => _allowFullHandDiscard;
        public ResourceAmountDefinition FullHandExchange => _fullHandExchange;
        public IReadOnlyList<BossRewardEntryDefinition> PlayerBossRewards => _playerBossRewards;
        public IReadOnlyList<BossRewardEntryDefinition> EnemyBossRewards => _enemyBossRewards;
        public int BossRewardBudgetMilli => _bossRewardBudgetMilli;
        public IReadOnlyList<RewardRarityWeightDefinition> RarityWeights => _rarityWeights;
        public IReadOnlyList<ProcessedResourceBundleDefinition> ProcessedResourceBundles => _processedResourceBundles;
        public IReadOnlyList<ReinforcementTemplateDefinition> ReinforcementTemplates => _reinforcementTemplates;
        public string BuildingRewardPresentationKey => _buildingRewardPresentationKey;
        public string ResourceRewardPresentationKey => _resourceRewardPresentationKey;
        public string ReinforcementRewardPresentationKey => _reinforcementRewardPresentationKey;
    }

    [Serializable]
    public sealed class RewardRarityWeightDefinition
    {
        [SerializeField] private RewardRarity _rarity;
        [SerializeField] private List<int> _heatTierWeights = new();
        public RewardRarity Rarity => _rarity;
        public IReadOnlyList<int> HeatTierWeights => _heatTierWeights;
    }

    [Serializable]
    public sealed class ProcessedResourceBundleDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField] private RewardRarity _rarity;
        [SerializeField] private List<ResourceAmountDefinition> _amounts = new();
        public string Id => _id;
        public string DisplayName => _displayName;
        public RewardRarity Rarity => _rarity;
        public IReadOnlyList<ResourceAmountDefinition> Amounts => _amounts;
    }

    [Serializable]
    public sealed class ReinforcementUnitDefinition
    {
        [SerializeField] private string _unitId;
        [SerializeField, Min(1)] private int _quantity = 1;
        public string UnitId => _unitId;
        public int Quantity => _quantity;
    }

    [Serializable]
    public sealed class ReinforcementTemplateDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private string _displayName;
        [SerializeField, Range(0, 3)] private int _minimumHeatTier;
        [SerializeField] private RewardRarity _rarity;
        [SerializeField] private List<ReinforcementUnitDefinition> _units = new();
        public string Id => _id;
        public string DisplayName => _displayName;
        public int MinimumHeatTier => _minimumHeatTier;
        public RewardRarity Rarity => _rarity;
        public IReadOnlyList<ReinforcementUnitDefinition> Units => _units;
    }

    [Serializable]
    public sealed class AiFeatureCoefficientDefinition
    {
        [SerializeField] private string _featureId;
        [SerializeField] private string _intentId;
        [SerializeField] private int _coefficient;
        public string FeatureId => _featureId;
        public string IntentId => _intentId;
        public int Coefficient => _coefficient;
    }

    [Serializable]
    public sealed class AiCommitmentDefinition
    {
        [SerializeField] private string _intentId;
        [SerializeField, Min(80)] private int _minimumTicks = 80;
        [SerializeField] private AiCommitmentPolicy _policy;
        public string IntentId => _intentId;
        public int MinimumTicks => _minimumTicks;
        public AiCommitmentPolicy Policy => _policy;
    }

    public sealed partial class AiUtilityProfileDefinition
    {
        [SerializeField] private List<AiFeatureCoefficientDefinition> _featureCoefficients = new();
        [SerializeField] private List<AiCommitmentDefinition> _commitments = new();
        [SerializeField, Min(0)] private int _switchCost = 120;
        [SerializeField, Min(0)] private int _repetitionPenalty = 180;
        
        [SerializeField, Min(1)] private int _pressureMinIntervalTicks = 550;
        [SerializeField, Min(1)] private int _pressureTargetIntervalTicks = 650;
        [SerializeField, Min(1)] private int _pressureMaxIntervalTicks = 750;
        [SerializeField, Min(1)] private int _activeUnitSoftCap = 22;
        [SerializeField, Min(1)] private int _queuedUnitSoftCap = 8;
[SerializeField, Min(1)] private int _logisticsThreatMemoryTicks = 300;
[SerializeField, Range(1, 2)] private int _maxConcurrentLogisticsResponses = 2;
[SerializeField, Min(0)] private int _emergencyDefenseOverflowUnits = 2;
[SerializeField, Min(1)] private int _towerEscalationKillCount = 2;
[SerializeField, Min(1)] private int _softmaxLookupVersion = 1;
        public IReadOnlyList<AiFeatureCoefficientDefinition> FeatureCoefficients => _featureCoefficients;
        public IReadOnlyList<AiCommitmentDefinition> Commitments => _commitments;
        public int SwitchCost => _switchCost;
        public int RepetitionPenalty => _repetitionPenalty;
        
        public int PressureMinIntervalTicks => _pressureMinIntervalTicks;
        public int PressureTargetIntervalTicks => _pressureTargetIntervalTicks;
        public int PressureMaxIntervalTicks => _pressureMaxIntervalTicks;
        public int ActiveUnitSoftCap => _activeUnitSoftCap;
        public int QueuedUnitSoftCap => _queuedUnitSoftCap;
        public int LogisticsThreatMemoryTicks => _logisticsThreatMemoryTicks;
        public int MaxConcurrentLogisticsResponses => _maxConcurrentLogisticsResponses;
        public int EmergencyDefenseOverflowUnits => _emergencyDefenseOverflowUnits;
        public int TowerEscalationKillCount => _towerEscalationKillCount;
public int SoftmaxLookupVersion => _softmaxLookupVersion;
    }

    [Serializable]
    public sealed class VirtualFacilityDefinition
    {
        [SerializeField] private string _buildingId;
        [SerializeField, Min(1)] private int _level = 1;
        public string BuildingId => _buildingId;
        public int Level => _level;
    }

    [Serializable]
    public sealed class VirtualCampDefinition
    {
        [SerializeField] private string _unitId;
        [SerializeField, Min(1)] private int _slotCount = 1;
        public string UnitId => _unitId;
        public int SlotCount => _slotCount;
    }

    [Serializable]
    public sealed class EnemyFormationDefinition
    {
        [SerializeField] private string _id;
        [SerializeField] private List<string> _unitIds = new();
        
        [SerializeField] private List<string> _allowedIntentIds = new();
[SerializeField] private List<int> _quantities = new();
        public string Id => _id;
        public IReadOnlyList<string> UnitIds => _unitIds;
        
        public IReadOnlyList<string> AllowedIntentIds => _allowedIntentIds;
public IReadOnlyList<int> Quantities => _quantities;
    }

    public sealed partial class EnemyEconomyProfileDefinition
    {
        [SerializeField] private List<ResourceAmountDefinition> _initialInventory = new();
        [SerializeField] private List<VirtualFacilityDefinition> _facilities = new();
        [SerializeField] private List<VirtualCampDefinition> _camps = new();
        [SerializeField] private List<string> _initialHandCardIds = new();
        [SerializeField] private List<EnemyFormationDefinition> _formations = new();
        [SerializeField] private string _defenseReserveFormationId = "formation.logistics-guard";
        [SerializeField, Range(0, 1000)] private int _reserveRatioMilli = 300;
        [SerializeField, Min(1)] private int _gatherCycleTicks = 70;
        [SerializeField, Min(1)] private int _processingCycleTicks = 50;
        [SerializeField, Min(1)] private int _builderRespawnTicks = 80;
        public IReadOnlyList<ResourceAmountDefinition> InitialInventory => _initialInventory;
        public IReadOnlyList<VirtualFacilityDefinition> Facilities => _facilities;
        public IReadOnlyList<VirtualCampDefinition> Camps => _camps;
        public IReadOnlyList<string> InitialHandCardIds => _initialHandCardIds;
        public IReadOnlyList<EnemyFormationDefinition> Formations => _formations;
        public string DefenseReserveFormationId => _defenseReserveFormationId;
        public int ReserveRatioMilli => _reserveRatioMilli;
        public int GatherCycleTicks => _gatherCycleTicks;
        public int ProcessingCycleTicks => _processingCycleTicks;
        public int BuilderRespawnTicks => _builderRespawnTicks;
    }
}
