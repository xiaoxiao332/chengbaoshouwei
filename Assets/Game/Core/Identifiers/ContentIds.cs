using System;

namespace FortressFrontier.Core.Identifiers
{
    internal static class StableId
    {
        public static string Require(string value, string parameterName, string displayName)
        {
            return string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException($"{displayName} cannot be empty.", parameterName)
                : value;
        }

        public static bool Equals(string left, string right) => string.Equals(left, right, StringComparison.Ordinal);
        public static int GetHashCode(string value) => StringComparer.Ordinal.GetHashCode(value ?? string.Empty);
    }

    public readonly struct ResourceId : IEquatable<ResourceId>
    {
        public ResourceId(string value) => Value = StableId.Require(value, nameof(value), "Resource id");
        public string Value { get; }
        public bool Equals(ResourceId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ResourceId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct BuildingId : IEquatable<BuildingId>
    {
        public BuildingId(string value) => Value = StableId.Require(value, nameof(value), "Building id");
        public string Value { get; }
        public bool Equals(BuildingId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is BuildingId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct UnitId : IEquatable<UnitId>
    {
        public UnitId(string value) => Value = StableId.Require(value, nameof(value), "Unit id");
        public string Value { get; }
        public bool Equals(UnitId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is UnitId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct CampaignStageId : IEquatable<CampaignStageId>
    {
        public CampaignStageId(string value) => Value = StableId.Require(value, nameof(value), "Campaign stage id");
        public string Value { get; }
        public bool Equals(CampaignStageId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is CampaignStageId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct AiDoctrineId : IEquatable<AiDoctrineId>
    {
        public AiDoctrineId(string value) => Value = StableId.Require(value, nameof(value), "AI doctrine id");
        public string Value { get; }
        public bool Equals(AiDoctrineId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is AiDoctrineId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct AiPhaseProfileId : IEquatable<AiPhaseProfileId>
    {
        public AiPhaseProfileId(string value) => Value = StableId.Require(value, nameof(value), "AI phase profile id");
        public string Value { get; }
        public bool Equals(AiPhaseProfileId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is AiPhaseProfileId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct AiUtilityProfileId : IEquatable<AiUtilityProfileId>
    {
        public AiUtilityProfileId(string value) => Value = StableId.Require(value, nameof(value), "AI utility profile id");
        public string Value { get; }
        public bool Equals(AiUtilityProfileId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is AiUtilityProfileId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct EnemyEconomyProfileId : IEquatable<EnemyEconomyProfileId>
    {
        public EnemyEconomyProfileId(string value) => Value = StableId.Require(value, nameof(value), "Enemy economy profile id");
        public string Value { get; }
        public bool Equals(EnemyEconomyProfileId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is EnemyEconomyProfileId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct BossId : IEquatable<BossId>
    {
        public BossId(string value) => Value = StableId.Require(value, nameof(value), "Boss id");
        public string Value { get; }
        public bool Equals(BossId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is BossId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct RewardTableId : IEquatable<RewardTableId>
    {
        public RewardTableId(string value) => Value = StableId.Require(value, nameof(value), "Reward table id");
        public string Value { get; }
        public bool Equals(RewardTableId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is RewardTableId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct RewardChoiceId : IEquatable<RewardChoiceId>
    {
        public RewardChoiceId(string value) => Value = StableId.Require(value, nameof(value), "Reward choice id");
        public string Value { get; }
        public bool Equals(RewardChoiceId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is RewardChoiceId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct ReinforcementTemplateId : IEquatable<ReinforcementTemplateId>
    {
        public ReinforcementTemplateId(string value) => Value = StableId.Require(value, nameof(value), "Reinforcement template id");
        public string Value { get; }
        public bool Equals(ReinforcementTemplateId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ReinforcementTemplateId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct AiIntentId : IEquatable<AiIntentId>
    {
        public AiIntentId(string value) => Value = StableId.Require(value, nameof(value), "AI intent id");
        public string Value { get; }
        public bool Equals(AiIntentId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is AiIntentId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct AiFeatureId : IEquatable<AiFeatureId>
    {
        public AiFeatureId(string value) => Value = StableId.Require(value, nameof(value), "AI feature id");
        public string Value { get; }
        public bool Equals(AiFeatureId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is AiFeatureId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct MatchPhaseId : IEquatable<MatchPhaseId>
    {
        public MatchPhaseId(string value) => Value = StableId.Require(value, nameof(value), "Match phase id");
        public string Value { get; }
        public bool Equals(MatchPhaseId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is MatchPhaseId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct TacticEffectId : IEquatable<TacticEffectId>
    {
        public TacticEffectId(string value) => Value = StableId.Require(value, nameof(value), "Tactic effect id");
        public string Value { get; }
        public bool Equals(TacticEffectId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is TacticEffectId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct ResearchUpgradeId : IEquatable<ResearchUpgradeId>
    {
        public ResearchUpgradeId(string value) => Value = StableId.Require(value, nameof(value), "Research upgrade id");
        public string Value { get; }
        public bool Equals(ResearchUpgradeId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ResearchUpgradeId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct RouteId : IEquatable<RouteId>
    {
        public RouteId(string value) => Value = StableId.Require(value, nameof(value), "Route id");
        public string Value { get; }
        public bool Equals(RouteId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is RouteId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct GathererSourceId : IEquatable<GathererSourceId>
    {
        public GathererSourceId(string value) => Value = StableId.Require(value, nameof(value), "Gatherer source id");
        public string Value { get; }
        public bool Equals(GathererSourceId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is GathererSourceId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct ResourceNodeId : IEquatable<ResourceNodeId>
    {
        public ResourceNodeId(string value) => Value = StableId.Require(value, nameof(value), "Resource node id");
        public string Value { get; }
        public bool Equals(ResourceNodeId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ResourceNodeId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }

    public readonly struct ConstructionSiteId : IEquatable<ConstructionSiteId>
    {
        public ConstructionSiteId(string value) => Value = StableId.Require(value, nameof(value), "Construction site id");
        public string Value { get; }
        public bool Equals(ConstructionSiteId other) => StableId.Equals(Value, other.Value);
        public override bool Equals(object obj) => obj is ConstructionSiteId other && Equals(other);
        public override int GetHashCode() => StableId.GetHashCode(Value);
        public override string ToString() => Value;
    }
}
