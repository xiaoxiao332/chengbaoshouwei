using System.Collections.Generic;
using UnityEngine;

namespace FortressFrontier.Runtime.Content
{
    [CreateAssetMenu(menuName = "Fortress Frontier/Content/Stage Effect Catalog", fileName = "StageEffectCatalog")]
    public sealed class StageEffectCatalog : ScriptableObject
    {
        [SerializeField] private List<CampaignStageDefinition> _campaignStages = new();
        [SerializeField] private List<MapModeDefinition> _mapModes = new();
        [SerializeField] private List<AiPhaseProfileDefinition> _aiPhaseProfiles = new();
        [SerializeField] private List<AiUtilityProfileDefinition> _aiUtilityProfiles = new();
        [SerializeField] private List<EnemyEconomyProfileDefinition> _enemyEconomyProfiles = new();
        [SerializeField] private List<AiDoctrineDefinition> _aiDoctrines = new();
        [SerializeField] private List<DifficultyRulesDefinition> _difficultyRules = new();
        [SerializeField] private List<EnemyUnitPoolDefinition> _enemyUnitPools = new();
        [SerializeField] private List<ResourceActivationWaveDefinition> _resourceActivationWaves = new();
        [SerializeField] private List<HeatTierDefinition> _heatTiers = new();

        public IReadOnlyList<CampaignStageDefinition> CampaignStages => _campaignStages;
        public IReadOnlyList<MapModeDefinition> MapModes => _mapModes;
        public IReadOnlyList<AiPhaseProfileDefinition> AiPhaseProfiles => _aiPhaseProfiles;
        public IReadOnlyList<AiUtilityProfileDefinition> AiUtilityProfiles => _aiUtilityProfiles;
        public IReadOnlyList<EnemyEconomyProfileDefinition> EnemyEconomyProfiles => _enemyEconomyProfiles;
        public IReadOnlyList<AiDoctrineDefinition> AiDoctrines => _aiDoctrines;
        public IReadOnlyList<DifficultyRulesDefinition> DifficultyRules => _difficultyRules;
        public IReadOnlyList<EnemyUnitPoolDefinition> EnemyUnitPools => _enemyUnitPools;
        public IReadOnlyList<ResourceActivationWaveDefinition> ResourceActivationWaves => _resourceActivationWaves;
        public IReadOnlyList<HeatTierDefinition> HeatTiers => _heatTiers;
    }
}
