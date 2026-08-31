#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Infrastructure.Resources;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace FortressFrontier.Editor
{
    internal static class PresentationResourceAuthoring
    {
        private const string CatalogPath = "Assets/Game/Content/Config/ResourceCatalog.asset";

        public static void Configure()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(CatalogPath)
                ?? throw new InvalidOperationException($"Missing ResourceCatalog: {CatalogPath}");
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < entries.arraySize; index++)
                indexById[entries.GetArrayElementAtIndex(index).FindPropertyRelative("_id").stringValue] = index;

            var settings = AddressableAssetSettingsDefaultObject.Settings
                ?? throw new InvalidOperationException("Addressables settings are missing.");
            var group = settings.FindGroup("Local-UI")
                ?? throw new InvalidOperationException("Addressables group 'Local-UI' is missing.");

            foreach (var pair in PresentationAssetManifest.Assets)
            {
                if (AssetDatabase.LoadAssetAtPath<Sprite>(pair.Value) == null)
                    throw new InvalidOperationException($"Presentation sprite is missing or has the wrong type: {pair.Value}");
                if (!indexById.TryGetValue(pair.Key, out var index))
                {
                    index = entries.arraySize;
                    entries.InsertArrayElementAtIndex(index);
                    indexById.Add(pair.Key, index);
                }
                var item = entries.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("_id").stringValue = pair.Key;
                var guid = AssetDatabase.AssetPathToGUID(pair.Value);
                item.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue = guid;
                item.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue = true;
                foreach (var existing in settings.groups
                             .Where(value => value != null)
                             .SelectMany(value => value.entries)
                             .Where(value => value != null && value.address == pair.Key && value.guid != guid)
                             .ToArray())
                    settings.RemoveAssetEntry(existing.guid, false);
                settings.CreateOrMoveEntry(guid, group, false, false).address = pair.Key;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(settings);
        }
    }
}
#endif
