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
    public static class P0PresentationAuthoring
    {
        private const string CatalogPath = "Assets/Game/Content/Config/ResourceCatalog.asset";
        private const string ArtRoot = "Assets/Game/Art/Formal/PNG/";

        private static readonly IReadOnlyDictionary<string, string> Assets = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["art.state.worker-outbound"] = ArtRoot + "state_worker_outbound.png",
            ["art.state.worker-gathering"] = ArtRoot + "state_worker_gathering.png",
            ["art.state.worker-returning"] = ArtRoot + "state_worker_returning.png",
            ["art.state.missing-input"] = ArtRoot + "state_missing_input.png",
            ["art.state.paused"] = ArtRoot + "state_paused.png",
            ["art.state.upgrade-hidden"] = ArtRoot + "state_upgrade_hidden.png",
            ["art.state.upgrade-locked"] = ArtRoot + "state_upgrade_locked.png",
            ["art.state.upgrade-ready"] = ArtRoot + "state_upgrade_ready.png",
            ["art.state.upgrading"] = ArtRoot + "state_upgrade_upgrading.png",
            ["art.state.upgrade-max"] = ArtRoot + "state_upgrade_max.png",
            ["art.state.training-waiting"] = ArtRoot + "state_training_waiting.png",
            ["art.state.training-active"] = ArtRoot + "state_training_active.png",
            ["art.state.training-deployed"] = ArtRoot + "state_training_deployed.png"
        };

        [MenuItem("Fortress Frontier/P0/Configure Presentation Assets")]
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

            foreach (var pair in Assets)
            {
                if (AssetDatabase.LoadMainAssetAtPath(pair.Value) == null)
                    throw new InvalidOperationException($"Missing generated P0 art: {pair.Value}");
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
                var addressableEntry = settings.CreateOrMoveEntry(guid, group, false, false);
                addressableEntry.address = pair.Key;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log($"Configured {Assets.Count} P0 presentation assets.");
        }
    }
}
#endif
