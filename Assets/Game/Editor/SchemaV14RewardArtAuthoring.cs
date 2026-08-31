#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FortressFrontier.Infrastructure.Resources;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FortressFrontier.Editor
{
    internal static class SchemaV14RewardArtAuthoring
    {
        private static readonly IReadOnlyDictionary<string, string> Assets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["art.reward.building"] = "Assets/Game/Art/Formal/PNG/SchemaV14/icon_reward_building.png",
            ["art.reward.resource"] = "Assets/Game/Art/Formal/PNG/SchemaV14/icon_reward_resource.png",
            ["art.reward.reinforcement"] = "Assets/Game/Art/Formal/PNG/SchemaV14/icon_reward_reinforcement.png"
        };

        [MenuItem("Fortress Frontier/Schema v14/Import Reward Sprites")]
        private static void ImportRewardSprites()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings
                ?? throw new InvalidOperationException("Addressables settings are missing.");
            var group = settings.FindGroup("Local-UI")
                ?? throw new InvalidOperationException("Addressables group 'Local-UI' is missing.");
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>("Assets/Game/Content/Config/ResourceCatalog.asset")
                ?? throw new InvalidOperationException("Infrastructure ResourceCatalog is missing.");
            var serializedCatalog = new SerializedObject(catalog);
            var entries = serializedCatalog.FindProperty("_entries");
            var indexById = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < entries.arraySize; index++)
                indexById[entries.GetArrayElementAtIndex(index).FindPropertyRelative("_id").stringValue] = index;
            foreach (var pair in Assets)
            {
                AssetDatabase.ImportAsset(pair.Value, ImportAssetOptions.ForceUpdate);
                var importer = AssetImporter.GetAtPath(pair.Value) as TextureImporter
                    ?? throw new InvalidOperationException($"Texture importer is missing for '{pair.Value}'.");
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.mipmapEnabled = false;
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.maxTextureSize = 256;
                importer.SaveAndReimport();
                var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(pair.Value), group, false, false);
                entry.address = pair.Key;
                if (!indexById.TryGetValue(pair.Key, out var catalogIndex))
                {
                    catalogIndex = entries.arraySize;
                    entries.InsertArrayElementAtIndex(catalogIndex);
                    indexById.Add(pair.Key, catalogIndex);
                }
                var catalogEntry = entries.GetArrayElementAtIndex(catalogIndex);
                catalogEntry.FindPropertyRelative("_id").stringValue = pair.Key;
                catalogEntry.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue =
                    AssetDatabase.AssetPathToGUID(pair.Value);
                catalogEntry.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue = true;
            }
            serializedCatalog.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Schema v14 reward sprites imported and registered in Local-UI.");
        }
    }
}
#endif
