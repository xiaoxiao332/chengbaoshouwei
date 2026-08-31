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
    public static class AudioAssetAuthoring
    {
        public const string MenuPath = "Fortress Frontier/Audio/Configure Formal Audio";
        private const string CatalogPath = "Assets/Game/Content/Config/ResourceCatalog.asset";
        private const string GroupName = "Local-Audio";

        private static readonly (string Id, string Path, bool Music)[] Definitions =
        {
            ("audio.bgm.boot", "Assets/Game/Art/Formal/Audio/BGM/bgm_boot_dawn_at_ramparts.mp3", true),
            ("audio.bgm.selection", "Assets/Game/Art/Formal/Audio/BGM/bgm_selection_fortress_war_table.mp3", true),
            ("audio.bgm.prologue.development", "Assets/Game/Art/Formal/Audio/BGM/bgm_prologue_development_border_smoke.mp3", true),
            ("audio.bgm.prologue.contest", "Assets/Game/Art/Formal/Audio/BGM/bgm_prologue_contest_open_field_clash.mp3", true),
            ("audio.bgm.prologue.decisive", "Assets/Game/Art/Formal/Audio/BGM/bgm_prologue_decisive_before_the_wall.mp3", true),
            ("audio.bgm.river-pass.development", "Assets/Game/Art/Formal/Audio/BGM/bgm_river_pass_development_river_around_pass.mp3", true),
            ("audio.bgm.river-pass.contest", "Assets/Game/Art/Formal/Audio/BGM/bgm_river_pass_contest_underflow.mp3", true),
            ("audio.bgm.river-pass.decisive", "Assets/Game/Art/Formal/Audio/BGM/bgm_river_pass_decisive_canyon_siege.mp3", true),
            ("audio.bgm.boss.stone-golem", "Assets/Game/Art/Formal/Audio/BGM/bgm_boss_stone_golem_awakens.mp3", true),
            ("audio.bgm.result.victory", "Assets/Game/Art/Formal/Audio/BGM/bgm_result_victory_rampart_triumph.mp3", true),
            ("audio.bgm.result.defeat", "Assets/Game/Art/Formal/Audio/BGM/bgm_result_defeat_embers_remain.mp3", true),
            ("audio.sfx.unit-hit", "Assets/Game/Art/Formal/Audio/SFX/sfx_unit_hit_shared.wav", false),
            ("audio.sfx.gather-complete", "Assets/Game/Art/Formal/Audio/SFX/sfx_gather_complete_shared.wav", false)
        };

        [MenuItem(MenuPath)]
        public static void Configure()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(CatalogPath)
                ?? throw new InvalidOperationException($"ResourceCatalog is missing: {CatalogPath}");
            var settings = AddressableAssetSettingsDefaultObject.Settings
                ?? throw new InvalidOperationException("Addressables settings are missing.");
            var group = settings.FindGroup(GroupName)
                ?? throw new InvalidOperationException($"Addressables group is missing: {GroupName}");
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");

            foreach (var definition in Definitions)
            {
                ConfigureImporter(definition.Path, definition.Music);
                var guid = AssetDatabase.AssetPathToGUID(definition.Path);
                if (string.IsNullOrEmpty(guid) || AssetDatabase.LoadAssetAtPath<AudioClip>(definition.Path) == null)
                    throw new InvalidOperationException($"AudioClip is missing or invalid: {definition.Path}");

                var index = FindEntry(entries, definition.Id);
                if (index < 0)
                {
                    index = entries.arraySize;
                    entries.InsertArrayElementAtIndex(index);
                }
                var item = entries.GetArrayElementAtIndex(index);
                item.FindPropertyRelative("_id").stringValue = definition.Id;
                item.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue = guid;
                item.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue = true;

                var addressable = settings.CreateOrMoveEntry(guid, group, false, false);
                addressable.address = definition.Id;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            settings.SetDirty(AddressableAssetSettings.ModificationEvent.EntryModified, null, true, true);
            AssetDatabase.SaveAssets();
            Validate(catalog, settings, group);
            Debug.Log($"Configured and validated {Definitions.Length} formal audio assets in {GroupName}.");
        }

        private static void ConfigureImporter(string path, bool music)
        {
            var importer = AssetImporter.GetAtPath(path) as AudioImporter
                ?? throw new InvalidOperationException($"AudioImporter is missing: {path}");
            importer.forceToMono = !music;
            importer.loadInBackground = music;
            var settings = importer.defaultSampleSettings;
            settings.loadType = music ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad;
            settings.compressionFormat = music ? AudioCompressionFormat.Vorbis : AudioCompressionFormat.PCM;
            settings.quality = music ? 0.7f : 1f;
            settings.sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate;
            settings.preloadAudioData = !music;
            importer.defaultSampleSettings = settings;
            importer.SaveAndReimport();
        }

        private static void Validate(ResourceCatalog catalog, AddressableAssetSettings settings,
            AddressableAssetGroup expectedGroup)
        {
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var definition in Definitions)
            {
                if (!seen.Add(definition.Id) || FindEntry(entries, definition.Id) < 0)
                    throw new InvalidOperationException($"Catalog validation failed: {definition.Id}");
                var guid = AssetDatabase.AssetPathToGUID(definition.Path);
                var addressable = settings.FindAssetEntry(guid);
                if (addressable == null || addressable.parentGroup != expectedGroup || addressable.address != definition.Id)
                    throw new InvalidOperationException($"Addressables validation failed: {definition.Id}");
                var item = entries.GetArrayElementAtIndex(FindEntry(entries, definition.Id));
                if (!item.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue)
                    throw new InvalidOperationException($"Audio must be excluded from GameObject preload: {definition.Id}");
            }
        }

        private static int FindEntry(SerializedProperty entries, string id)
        {
            for (var index = 0; index < entries.arraySize; index++)
                if (entries.GetArrayElementAtIndex(index).FindPropertyRelative("_id").stringValue == id) return index;
            return -1;
        }
    }
}
#endif
