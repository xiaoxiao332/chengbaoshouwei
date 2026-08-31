using UnityEngine;

namespace FortressFrontier.Runtime.Content
{
    [CreateAssetMenu(menuName = "Fortress Frontier/Content/Game Content Config", fileName = "GameContentConfig")]
    public sealed class GameContentConfig : ScriptableObject
    {
        [SerializeField, Min(1)] private int _schemaVersion = 1;
        [SerializeField] private ResourceDefinitionCatalog _resourceCatalog;
        [SerializeField] private CardCatalog _cardCatalog;
        [SerializeField] private BuildingCatalog _buildingCatalog;
        [SerializeField] private UnitCatalog _unitCatalog;
        [SerializeField] private BattlefieldCatalog _battlefieldCatalog;
        [SerializeField] private BossCatalog _bossCatalog;
        [SerializeField] private RewardCatalog _rewardCatalog;
        [SerializeField] private ProgressionConfig _progressionConfig;
        [SerializeField] private StageEffectCatalog _stageEffectCatalog;
        [SerializeField] private SceneKeyCatalog _sceneKeyCatalog;
        [SerializeField] private PresentationCatalog _presentationCatalog;

        public int SchemaVersion => _schemaVersion;
        public ResourceDefinitionCatalog ResourceCatalog => _resourceCatalog;
        public CardCatalog CardCatalog => _cardCatalog;
        public BuildingCatalog BuildingCatalog => _buildingCatalog;
        public UnitCatalog UnitCatalog => _unitCatalog;
        public BattlefieldCatalog BattlefieldCatalog => _battlefieldCatalog;
        public BossCatalog BossCatalog => _bossCatalog;
        public RewardCatalog RewardCatalog => _rewardCatalog;
        public ProgressionConfig ProgressionConfig => _progressionConfig;
        public StageEffectCatalog StageEffectCatalog => _stageEffectCatalog;
        public SceneKeyCatalog SceneKeyCatalog => _sceneKeyCatalog;
        public PresentationCatalog PresentationCatalog => _presentationCatalog;
    }
}
