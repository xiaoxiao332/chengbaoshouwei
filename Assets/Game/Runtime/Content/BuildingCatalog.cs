using System.Collections.Generic;
using UnityEngine;

namespace FortressFrontier.Runtime.Content
{
    [CreateAssetMenu(menuName = "Fortress Frontier/Content/Building Catalog", fileName = "BuildingCatalog")]
    public sealed class BuildingCatalog : ContentCatalogAsset<BuildingDefinition>
    {
        [SerializeField] private List<ResearchUpgradeDefinition> _researchUpgrades = new();
        [SerializeField] private List<ResearchBagDefinition> _researchBags = new();
        public IReadOnlyList<ResearchUpgradeDefinition> ResearchUpgrades => _researchUpgrades;
        public IReadOnlyList<ResearchBagDefinition> ResearchBags => _researchBags;
    }
}
