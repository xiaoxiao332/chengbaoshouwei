using UnityEngine;

namespace FortressFrontier.Runtime.Content
{
    [CreateAssetMenu(menuName = "Fortress Frontier/Content/Progression Config", fileName = "ProgressionConfig")]
    public sealed class ProgressionConfig : ScriptableObject
    {
        [SerializeField] private string _initialCampaignStageId;
        [SerializeField, Min(0)] private int _initialGold = 200;
        public string InitialCampaignStageId => _initialCampaignStageId;
        public int InitialGold => _initialGold;
    }
}
