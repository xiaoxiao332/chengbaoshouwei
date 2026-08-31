using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FortressFrontier.Editor
{
    public static class AddressablesProjectTools
    {
        private static readonly string[] LocalGroupNames =
        {
            "Local-Core",
            "Local-UI",
            "Local-Gameplay",
            "Local-Audio",
            "Local-Scenes"
        };

        [MenuItem("Fortress Frontier/Addressables/Configure Local Groups")]
        public static void ConfigureLocalGroups()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                settings = AddressableAssetSettings.Create(
                    "Assets/AddressableAssetsData",
                    "AddressableAssetSettings",
                    true,
                    true);
                AddressableAssetSettingsDefaultObject.Settings = settings;
            }

            settings.BuildRemoteCatalog = false;
            settings.DisableCatalogUpdateOnStartup = true;

            var defaultGroup = settings.DefaultGroup
                ?? throw new InvalidOperationException("Addressables default group was not created.");
            defaultGroup.Name = LocalGroupNames[0];

            for (var index = 1; index < LocalGroupNames.Length; index++)
            {
                var groupName = LocalGroupNames[index];
                if (settings.FindGroup(groupName) == null)
                {
                    settings.CreateGroup(
                        groupName,
                        false,
                        false,
                        true,
                        new List<AddressableAssetGroupSchema>(defaultGroup.Schemas));
                }
            }

            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("FortressFrontier Addressables local groups configured.");
        }

        [MenuItem("Fortress Frontier/Addressables/Build Local Content")]
        public static void BuildLocalContent()
        {
            ConfigureLocalGroups();
            AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult result);
            if (!string.IsNullOrEmpty(result.Error))
            {
                throw new InvalidOperationException($"Addressables build failed: {result.Error}");
            }

            Debug.Log($"FortressFrontier Addressables build succeeded in {result.Duration} seconds.");
        }
    }
}
