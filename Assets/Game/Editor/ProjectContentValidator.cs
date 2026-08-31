#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Infrastructure.Resources;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Runtime.Content;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;

namespace FortressFrontier.Editor
{
    public static class ProjectContentValidator
    {
        private const string RootPath = "Assets/Game/Content/Config/GameContentConfig.asset";
        private const string ResourceCatalogPath = "Assets/Game/Content/Config/ResourceCatalog.asset";

        [MenuItem("Fortress Frontier/Content/Validate Project Content")]
        public static void ValidateMenu()
        {
            var issues = CollectIssues();
            if (issues.Count > 0) throw new InvalidOperationException("Project content validation failed:\n" + string.Join("\n", issues));
            Debug.Log("FortressFrontier project content, presentation resources, and Addressables are valid.");
        }

        public static IReadOnlyList<string> CollectIssues()
        {
            var issues = new List<string>();
            var root = AssetDatabase.LoadAssetAtPath<GameContentConfig>(RootPath);
            var resourceCatalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(ResourceCatalogPath);
            if (root == null) { issues.Add($"Missing root config: {RootPath}"); return issues; }
            if (resourceCatalog == null) { issues.Add($"Missing infrastructure ResourceCatalog: {ResourceCatalogPath}"); return issues; }
            issues.AddRange(ContentConfigValidator.Validate(root).Issues.Select(value => value.ToString()));

            var serialized = new SerializedObject(resourceCatalog);
            var entries = serialized.FindProperty("_entries");
            var guidById = new Dictionary<string, string>(StringComparer.Ordinal);
            for (var index = 0; index < entries.arraySize; index++)
            {
                var item = entries.GetArrayElementAtIndex(index);
                var id = item.FindPropertyRelative("_id").stringValue;
                var guid = item.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue;
                if (!string.IsNullOrWhiteSpace(id) && !guidById.TryAdd(id, guid)) issues.Add($"Duplicate infrastructure ResourceKey: {id}");
            }

            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null) issues.Add("Addressables settings are missing.");
            else
            {
                foreach (var duplicate in settings.groups
                             .Where(group => group != null)
                             .SelectMany(group => group.entries)
                             .Where(entry => entry != null && !string.IsNullOrWhiteSpace(entry.address))
                             .GroupBy(entry => entry.address, StringComparer.Ordinal)
                             .Where(group => group.Count() > 1))
                    issues.Add($"Duplicate Addressables address: {duplicate.Key}");
            }
            foreach (var definition in root.PresentationCatalog.Definitions)
                ValidateKey(definition.ResourceKey,
                    definition.ResourceKey.StartsWith("world.", StringComparison.Ordinal) ? typeof(GameObject) : typeof(Sprite),
                    guidById, settings, issues);
            foreach (var key in GameplayWorldPresentationProfile.RequiredKeys)
                ValidateKey(key.Value, typeof(GameObject), guidById, settings, issues);
            return issues;
        }

        private static void ValidateKey(string key, Type expectedType, IReadOnlyDictionary<string, string> guidById,
            UnityEditor.AddressableAssets.Settings.AddressableAssetSettings settings, ICollection<string> issues)
        {
            if (!guidById.TryGetValue(key, out var guid) || string.IsNullOrWhiteSpace(guid))
            { issues.Add($"Missing infrastructure ResourceKey: {key}"); return; }
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrWhiteSpace(path) || AssetDatabase.LoadAssetAtPath(path, expectedType) == null)
                issues.Add($"ResourceKey '{key}' does not resolve to {expectedType.Name}.");
            if (settings?.FindAssetEntry(guid) == null)
                issues.Add($"ResourceKey '{key}' points to an asset that is not registered in Addressables.");
        }
    }
}
#endif
