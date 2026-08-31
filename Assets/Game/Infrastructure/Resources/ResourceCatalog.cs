using System;
using System.Collections.Generic;
using FortressFrontier.Core.Identifiers;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace FortressFrontier.Infrastructure.Resources
{
    [CreateAssetMenu(menuName = "Fortress Frontier/Resources/Resource Catalog", fileName = "ResourceCatalog")]
    public sealed class ResourceCatalog : ScriptableObject
    {
        [Serializable]
        private sealed class Entry
        {
            [SerializeField] private string _id;
            [SerializeField] private AssetReference _reference;
            [SerializeField] private bool _excludeFromGameObjectPreload;

            public string Id => _id;
            public AssetReference Reference => _reference;
            public bool ExcludeFromGameObjectPreload => _excludeFromGameObjectPreload;
        }

        [SerializeField] private List<Entry> _entries = new();
        private Dictionary<string, AssetReference> _lookup;

        public object GetRuntimeKey(ResourceKey key)
        {
            return GetReference(key.Value).RuntimeKey;
        }

        public object GetRuntimeKey(SceneKey key)
        {
            return GetReference(key.Value).RuntimeKey;
        }

        public IReadOnlyList<ResourceKey> GetPreloadResourceKeys()
        {
            var keys = new List<ResourceKey>(_entries.Count);
            foreach (var entry in _entries)
            {
                if (entry == null || entry.ExcludeFromGameObjectPreload || string.IsNullOrWhiteSpace(entry.Id) ||
                    entry.Id.StartsWith("scene.", StringComparison.Ordinal))
                {
                    continue;
                }

                keys.Add(new ResourceKey(entry.Id));
            }

            return keys;
        }

        private AssetReference GetReference(string id)
        {
            EnsureLookup();
            if (!_lookup.TryGetValue(id, out var reference) || reference == null || !reference.RuntimeKeyIsValid())
            {
                throw new KeyNotFoundException($"Resource catalog entry is missing or invalid: '{id}'.");
            }

            return reference;
        }

        private void EnsureLookup()
        {
            if (_lookup != null)
            {
                return;
            }

            _lookup = new Dictionary<string, AssetReference>(StringComparer.Ordinal);
            foreach (var entry in _entries)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.Id))
                {
                    continue;
                }

                if (!_lookup.TryAdd(entry.Id, entry.Reference))
                {
                    throw new InvalidOperationException($"Duplicate resource catalog id: '{entry.Id}'.");
                }
            }
        }

        private void OnValidate()
        {
            _lookup = null;
        }
    }
}
