#if UNITY_EDITOR
using System;
using FortressFrontier.Infrastructure.Resources;
using FortressFrontier.Runtime.Content;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace FortressFrontier.Editor
{
    public static class GameContentConfigAuthoring
    {
        public const string RootAssetPath = "Assets/Game/Content/Config/GameContentConfig.asset";
        private const string ConfigRoot = "Assets/Game/Content/Config";

        [MenuItem("Fortress Frontier/Content/Build Baseline Config")]
        public static void BuildBaseline()
        {
            EnsureFolders();
            var resources = GetOrCreate<ResourceDefinitionCatalog>(ConfigRoot + "/Resources/ResourceCatalog.asset");
            var cards = GetOrCreate<CardCatalog>(ConfigRoot + "/Cards/CardCatalog.asset");
            var buildings = GetOrCreate<BuildingCatalog>(ConfigRoot + "/Buildings/BuildingCatalog.asset");
            var units = GetOrCreate<UnitCatalog>(ConfigRoot + "/Units/UnitCatalog.asset");
            var battlefields = GetOrCreate<BattlefieldCatalog>(ConfigRoot + "/Battlefields/BattlefieldCatalog.asset");
            var bosses = GetOrCreate<BossCatalog>(ConfigRoot + "/Bosses/BossCatalog.asset");
            var rewards = GetOrCreate<RewardCatalog>(ConfigRoot + "/Rewards/RewardCatalog.asset");
            var progression = GetOrCreate<ProgressionConfig>(ConfigRoot + "/Progression/ProgressionConfig.asset");
            var stages = GetOrCreate<StageEffectCatalog>(ConfigRoot + "/Stages/StageEffectCatalog.asset");
            var scenes = GetOrCreate<SceneKeyCatalog>(ConfigRoot + "/Scenes/SceneKeyCatalog.asset");
            var presentation = GetOrCreate<PresentationCatalog>(ConfigRoot + "/Presentation/PresentationCatalog.asset");
            var root = GetOrCreate<GameContentConfig>(RootAssetPath);

            P1ContentConfigAuthoring.Apply(resources, cards, buildings, units, battlefields, bosses, rewards, stages, presentation);
            ConfigureProgression(progression);
            ConfigureScenes(scenes);
            ConfigureRoot(root, resources, cards, buildings, units, battlefields, bosses, rewards, progression, stages, scenes, presentation);
            RegisterRootAddressable();
            PresentationResourceAuthoring.Configure();
            VerticalSliceAuthoring.BuildWorldPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            var issues = ProjectContentValidator.CollectIssues();
            if (issues.Count > 0) throw new InvalidOperationException("Project content validation failed:\n" + string.Join("\n", issues));
            Debug.Log($"FortressFrontier content baseline built and validated at {RootAssetPath}.");
        }

        [MenuItem("Fortress Frontier/Content/Validate Config")]
        public static void ValidateConfig()
        {
            var root = AssetDatabase.LoadAssetAtPath<GameContentConfig>(RootAssetPath)
                ?? throw new InvalidOperationException($"Missing root content config: {RootAssetPath}");
            var issues = ProjectContentValidator.CollectIssues();
            if (issues.Count > 0) throw new InvalidOperationException("Project content validation failed:\n" + string.Join("\n", issues));
            Debug.Log($"FortressFrontier content config is valid (schema {root.SchemaVersion}).");
        }

        private static void ConfigureProgression(ProgressionConfig config)
        {
            var so = Begin(config);
            so.FindProperty("_initialCampaignStageId").stringValue = "stage.prologue";
            so.FindProperty("_initialGold").intValue = 200;
            End(so);
        }

        private static void ConfigureScenes(SceneKeyCatalog catalog)
        {
            var values = new[] { ("scene.selection", "scene.selection"), ("scene.gameplay", "scene.gameplay") };
            ConfigureList(catalog, "_definitions", values.Length, (item, index) =>
            {
                String(item, "_id", values[index].Item1);
                String(item, "_resourceKey", values[index].Item2);
            });
        }

        private static void ConfigureRoot(GameContentConfig root, ResourceDefinitionCatalog resources, CardCatalog cards,
            BuildingCatalog buildings, UnitCatalog units, BattlefieldCatalog battlefields, BossCatalog bosses,
            RewardCatalog rewards, ProgressionConfig progression, StageEffectCatalog stages, SceneKeyCatalog scenes,
            PresentationCatalog presentation)
        {
            var so = Begin(root);
            so.FindProperty("_schemaVersion").intValue = ContentConstants.ExpectedSchemaVersion;
            so.FindProperty("_resourceCatalog").objectReferenceValue = resources;
            so.FindProperty("_cardCatalog").objectReferenceValue = cards;
            so.FindProperty("_buildingCatalog").objectReferenceValue = buildings;
            so.FindProperty("_unitCatalog").objectReferenceValue = units;
            so.FindProperty("_battlefieldCatalog").objectReferenceValue = battlefields;
            so.FindProperty("_bossCatalog").objectReferenceValue = bosses;
            so.FindProperty("_rewardCatalog").objectReferenceValue = rewards;
            so.FindProperty("_progressionConfig").objectReferenceValue = progression;
            so.FindProperty("_stageEffectCatalog").objectReferenceValue = stages;
            so.FindProperty("_sceneKeyCatalog").objectReferenceValue = scenes;
            so.FindProperty("_presentationCatalog").objectReferenceValue = presentation;
            End(so);
        }

        private static void RegisterRootAddressable()
        {
            const string resourceCatalogPath = ConfigRoot + "/ResourceCatalog.asset";
            var resourceCatalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(resourceCatalogPath)
                ?? throw new InvalidOperationException($"Missing infrastructure resource catalog: {resourceCatalogPath}");
            var so = Begin(resourceCatalog);
            var entries = so.FindProperty("_entries");
            var entryIndex = -1;
            for (var index = 0; index < entries.arraySize; index++)
            {
                if (entries.GetArrayElementAtIndex(index).FindPropertyRelative("_id").stringValue == "config.game-content")
                {
                    entryIndex = index;
                    break;
                }
            }

            if (entryIndex < 0)
            {
                entryIndex = entries.arraySize;
                entries.arraySize++;
            }

            var entry = entries.GetArrayElementAtIndex(entryIndex);
            String(entry, "_id", "config.game-content");
            entry.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(RootAssetPath);
            Bool(entry, "_excludeFromGameObjectPreload", true);
            End(so);

            var settings = AddressableAssetSettingsDefaultObject.Settings
                ?? throw new InvalidOperationException("Addressables settings are missing.");
            var group = settings.FindGroup("Local-Core")
                ?? throw new InvalidOperationException("Addressables group Local-Core is missing.");
            var addressableEntry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(RootAssetPath), group, false, false);
            addressableEntry.address = "config.game-content";
            EditorUtility.SetDirty(settings);
        }

        private static void ConfigureList(UnityEngine.Object target, string propertyName, int count, Action<SerializedProperty, int> configure)
        {
            var so = Begin(target);
            var list = so.FindProperty(propertyName) ?? throw new InvalidOperationException($"Missing property {propertyName} on {target.name}.");
            list.arraySize = count;
            for (var index = 0; index < count; index++) configure(list.GetArrayElementAtIndex(index), index);
            End(so);
        }

        private static SerializedObject Begin(UnityEngine.Object target)
        {
            Undo.RecordObject(target, "Configure FortressFrontier content");
            return new SerializedObject(target);
        }

        private static void End(SerializedObject so)
        {
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(so.targetObject);
        }

        private static void String(SerializedProperty parent, string name, string value) => parent.FindPropertyRelative(name).stringValue = value;
        private static void Bool(SerializedProperty parent, string name, bool value) => parent.FindPropertyRelative(name).boolValue = value;

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;
            asset = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Game", "Content");
            EnsureFolder("Assets/Game/Content", "Config");
            foreach (var folder in new[] { "Resources", "Cards", "Buildings", "Units", "Battlefields", "Bosses", "Rewards", "Progression", "Stages", "Scenes", "Presentation" })
                EnsureFolder(ConfigRoot, folder);
        }

        private static void EnsureFolder(string parent, string name)
        {
            var path = parent + "/" + name;
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, name);
        }
    }
}
#endif
