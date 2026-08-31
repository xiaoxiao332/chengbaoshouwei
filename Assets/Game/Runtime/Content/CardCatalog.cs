using System.Collections.Generic;
using UnityEngine;

namespace FortressFrontier.Runtime.Content
{
    [CreateAssetMenu(menuName = "Fortress Frontier/Content/Card Catalog", fileName = "CardCatalog")]
    public sealed class CardCatalog : ContentCatalogAsset<CardDefinition>
    {
        [SerializeField] private List<TacticEffectDefinition> _tacticEffects = new();
        public IReadOnlyList<TacticEffectDefinition> TacticEffects => _tacticEffects;
    }
}
