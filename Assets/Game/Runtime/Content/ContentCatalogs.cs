using System.Collections.Generic;
using UnityEngine;

namespace FortressFrontier.Runtime.Content
{
    public abstract class ContentCatalogAsset<TDefinition> : ScriptableObject
    {
        [SerializeField] private List<TDefinition> _definitions = new();
        public IReadOnlyList<TDefinition> Definitions => _definitions;
    }

}
