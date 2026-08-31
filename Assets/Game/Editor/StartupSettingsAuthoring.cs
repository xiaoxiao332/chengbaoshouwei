#if UNITY_EDITOR
using System;
using FortressFrontier.Infrastructure.Resources;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Content;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    public static class StartupSettingsAuthoring
    {
        public const string MenuPath = "Fortress Frontier/UI/Build Startup And Settings";
        public const string SettingsOnlyMenuPath = "Fortress Frontier/UI/Rebuild Settings Audio Controls";
        private const string PrefabRoot = "Assets/Game/Content/Prefabs/UI";
        private const string SettingsPrefabPath = PrefabRoot + "/Settings.prefab";
        private const string BootPrefabPath = PrefabRoot + "/Boot.prefab";
        private const string SelectionPrefabPath = PrefabRoot + "/Selection.prefab";
        private const string GameplayPrefabPath = PrefabRoot + "/Gameplay.prefab";
        private const string ResourceCatalogPath = "Assets/Game/Content/Config/ResourceCatalog.asset";
        private const string PanelCatalogPath = "Assets/Game/Content/Config/PanelCatalog.asset";
        private const string BattlefieldCatalogPath = "Assets/Game/Content/Config/Battlefields/BattlefieldCatalog.asset";
        private const string ArtRoot = "Assets/Game/Art/Formal/PNG";

        private static readonly Color Ink = Hex("211815");
        private static readonly Color Wood = Hex("4C3224");
        private static readonly Color Parchment = Hex("F2DDA9");
        private static readonly Color Gold = Hex("F6BC4B");
        private static readonly Color Orange = Hex("E96E27");
        private static readonly Color Blue = Hex("327BD1");
        private static Font _font;

        [MenuItem(MenuPath)]
        public static void BuildAssets()
        {
            BuildSettingsPrefab();
            BindBootPrefab();
            BindSelectionPrefab();
            HideGameplaySoldierSlots();
            RegisterResource();
            RegisterPanel();
            RegisterAddressable();
            ApplyGathererDispatchInterval();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FortressFrontier startup menu, settings panel, hidden soldier slots, and 160-tick gatherer interval authored successfully.");
        }

        [MenuItem(SettingsOnlyMenuPath)]
        public static void BuildSettingsAudioControls()
        {
            BuildSettingsPrefab();
            RegisterResource();
            RegisterPanel();
            RegisterAddressable();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("FortressFrontier settings prefab rebuilt with master, music, and sound-effect controls.");
        }

        private static void BuildSettingsPrefab()
        {
            var root = new GameObject("Settings", typeof(RectTransform), typeof(CanvasGroup), typeof(SettingsPanel));
            Stretch(root.GetComponent<RectTransform>());
            var dim = AddImage(root.transform, "Dim", new Color(0.04f, 0.025f, 0.02f, 0.76f), null,
                Vector2.zero, Vector2.one);
            dim.raycastTarget = true;

            var safeArea = AddRect(root.transform, "SafeArea", Vector2.zero, Vector2.one);
            var window = AddImage(safeArea, "Window", Parchment, "ui_panel",
                new Vector2(0.30f, 0.20f), new Vector2(0.70f, 0.80f));
            AddImage(window.transform, "Header", Wood, null, new Vector2(0.02f, 0.83f), new Vector2(0.98f, 0.97f));
            AddText(window.transform, "Title", "设置", 42, TextAnchor.MiddleCenter, Gold,
                new Vector2(0.08f, 0.84f), new Vector2(0.92f, 0.96f));
            AddText(window.transform, "MasterVolumeLabel", "总音量", 26, TextAnchor.MiddleLeft, Ink,
                new Vector2(0.09f, 0.66f), new Vector2(0.30f, 0.75f));
            AddText(window.transform, "MusicVolumeLabel", "音乐", 26, TextAnchor.MiddleLeft, Ink,
                new Vector2(0.09f, 0.51f), new Vector2(0.30f, 0.60f));
            AddText(window.transform, "SfxVolumeLabel", "音效", 26, TextAnchor.MiddleLeft, Ink,
                new Vector2(0.09f, 0.36f), new Vector2(0.30f, 0.45f));

            var masterSlider = AddSlider(window.transform, "MasterVolume",
                new Vector2(0.30f, 0.66f), new Vector2(0.77f, 0.75f), 100);
            var musicSlider = AddSlider(window.transform, "MusicVolume",
                new Vector2(0.30f, 0.51f), new Vector2(0.77f, 0.60f), 70);
            var sfxSlider = AddSlider(window.transform, "SfxVolume",
                new Vector2(0.30f, 0.36f), new Vector2(0.77f, 0.45f), 80);
            var masterValue = AddText(window.transform, "MasterVolumeValue", "100%", 24, TextAnchor.MiddleRight, Ink,
                new Vector2(0.78f, 0.66f), new Vector2(0.91f, 0.75f));
            var musicValue = AddText(window.transform, "MusicVolumeValue", "70%", 24, TextAnchor.MiddleRight, Ink,
                new Vector2(0.78f, 0.51f), new Vector2(0.91f, 0.60f));
            var sfxValue = AddText(window.transform, "SfxVolumeValue", "80%", 24, TextAnchor.MiddleRight, Ink,
                new Vector2(0.78f, 0.36f), new Vector2(0.91f, 0.45f));
            var mute = AddToggle(window.transform);
            var error = AddText(window.transform, "Error", string.Empty, 21, TextAnchor.MiddleCenter,
                Hex("9B2D22"), new Vector2(0.08f, 0.17f), new Vector2(0.92f, 0.23f));
            var cancel = AddButton(window.transform, "Cancel", "取消", 27, Wood,
                new Vector2(0.10f, 0.04f), new Vector2(0.45f, 0.16f), "ui_button_secondary");
            var apply = AddButton(window.transform, "Apply", "应用并关闭", 27, Orange,
                new Vector2(0.55f, 0.04f), new Vector2(0.90f, 0.16f), "ui_button_primary");

            SetReferences(root.GetComponent<SettingsPanel>(),
                ("_masterVolumeSlider", masterSlider), ("_musicVolumeSlider", musicSlider),
                ("_sfxVolumeSlider", sfxSlider), ("_muteToggle", mute),
                ("_masterVolumeValue", masterValue), ("_musicVolumeValue", musicValue),
                ("_sfxVolumeValue", sfxValue), ("_errorText", error),
                ("_applyButton", apply), ("_cancelButton", cancel));
            PrefabUtility.SaveAsPrefabAsset(root, SettingsPrefabPath);
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void BindBootPrefab()
        {
            var root = LoadPrefab(BootPrefabPath);
            try
            {
                var panel = root.GetComponent<BootPanel>() ?? throw new InvalidOperationException("Boot prefab has no BootPanel.");
                var progress = FindRequired(root.transform, "ProgressFrame").gameObject;
                var status = FindRequired(root.transform, "Status").GetComponent<Text>();
                var fill = FindRequired(root.transform, "ProgressFrame/Fill").GetComponent<Image>();
                var readyMenu = root.transform.Find("ReadyMenu");
                if (readyMenu != null) UnityEngine.Object.DestroyImmediate(readyMenu.gameObject);
                var menu = AddRect(root.transform, "ReadyMenu", new Vector2(0.31f, 0.06f), new Vector2(0.69f, 0.22f));
                var start = AddButton(menu, "StartGame", "开始游戏", 36, Orange,
                    new Vector2(0.02f, 0.08f), new Vector2(0.62f, 0.92f), "ui_button_primary");
                var settings = AddButton(menu, "Settings", "设置", 31, Wood,
                    new Vector2(0.66f, 0.08f), new Vector2(0.98f, 0.92f), "ui_button_secondary");
                menu.gameObject.SetActive(false);
                SetReferences(panel, ("_progressFill", fill), ("_statusText", status),
                    ("_progressRoot", progress), ("_readyMenu", menu.gameObject),
                    ("_startButton", start), ("_settingsButton", settings));
                PrefabUtility.SaveAsPrefabAsset(root, BootPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BindSelectionPrefab()
        {
            var root = LoadPrefab(SelectionPrefabPath);
            try
            {
                var panel = root.GetComponent<SelectionPanel>() ?? throw new InvalidOperationException("Selection prefab has no SelectionPanel.");
                var legacy = root.transform.Find("Settings");
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy.gameObject);
                var settings = AddButton(root.transform, "Settings", "⚙", 42, Wood,
                    new Vector2(0.925f, 0.92f), new Vector2(0.985f, 0.99f), "ui_button_secondary");
                SetReferences(panel, ("_settingsButton", settings));
                PrefabUtility.SaveAsPrefabAsset(root, SelectionPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void HideGameplaySoldierSlots()
        {
            var root = LoadPrefab(GameplayPrefabPath);
            try
            {
                for (var index = 0; index < 4; index++)
                    FindRequired(root.transform, $"CardTray/SoldierCards/Soldier{index}").gameObject.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RegisterResource()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(ResourceCatalogPath)
                ?? throw new InvalidOperationException("ResourceCatalog is missing.");
            var serialized = new SerializedObject(catalog);
            var entries = serialized.FindProperty("_entries");
            var index = FindStringEntry(entries, "_id", "ui.settings");
            if (index < 0) { index = entries.arraySize; entries.InsertArrayElementAtIndex(index); }
            var entry = entries.GetArrayElementAtIndex(index);
            entry.FindPropertyRelative("_id").stringValue = "ui.settings";
            entry.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue =
                AssetDatabase.AssetPathToGUID(SettingsPrefabPath);
            entry.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue = false;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void RegisterPanel()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<PanelCatalog>(PanelCatalogPath)
                ?? throw new InvalidOperationException("PanelCatalog is missing.");
            var serialized = new SerializedObject(catalog);
            var panels = serialized.FindProperty("_panels");
            var index = FindStringEntry(panels, "_id", "ui.settings");
            if (index < 0) { index = panels.arraySize; panels.InsertArrayElementAtIndex(index); }
            var panel = panels.GetArrayElementAtIndex(index);
            panel.FindPropertyRelative("_id").stringValue = "ui.settings";
            panel.FindPropertyRelative("_resourceId").stringValue = "ui.settings";
            panel.FindPropertyRelative("_layer").enumValueIndex = (int)UIPanelLayer.Pop;
            panel.FindPropertyRelative("_cacheWhenClosed").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static void RegisterAddressable()
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings
                ?? throw new InvalidOperationException("Addressables settings are missing.");
            var group = settings.FindGroup("Local-UI")
                ?? throw new InvalidOperationException("Addressables Local-UI group is missing.");
            var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(SettingsPrefabPath), group, false, false);
            entry.address = "ui.settings";
            EditorUtility.SetDirty(settings);
        }

        private static void ApplyGathererDispatchInterval()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<BattlefieldCatalog>(BattlefieldCatalogPath)
                ?? throw new InvalidOperationException("BattlefieldCatalog is missing.");
            var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("_definitions");
            for (var index = 0; index < definitions.arraySize; index++)
                definitions.GetArrayElementAtIndex(index).FindPropertyRelative("_gathererDispatchIntervalTicks").intValue = 160;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
        }

        private static Slider AddSlider(Transform parent, string name, Vector2 min, Vector2 max, int value)
        {
            var root = AddImage(parent, name, new Color(0, 0, 0, 0), null, min, max);
            var slider = root.gameObject.AddComponent<Slider>();
            var background = AddImage(root.transform, "Background", Wood, null,
                new Vector2(0, 0.38f), new Vector2(1, 0.62f));
            var fillArea = AddRect(root.transform, "Fill Area", new Vector2(0.02f, 0.30f), new Vector2(0.98f, 0.70f));
            var fill = AddImage(fillArea, "Fill", Blue, null, Vector2.zero, Vector2.one);
            var handleArea = AddRect(root.transform, "Handle Slide Area", new Vector2(0.02f, 0), new Vector2(0.98f, 1));
            var handle = AddImage(handleArea, "Handle", Gold, "ui_button_secondary",
                new Vector2(0, 0.08f), new Vector2(0.08f, 0.92f));
            slider.targetGraphic = handle;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.minValue = 0;
            slider.maxValue = 100;
            slider.wholeNumbers = true;
            slider.value = value;
            background.raycastTarget = false;
            fill.raycastTarget = false;
            return slider;
        }

        private static Toggle AddToggle(Transform parent)
        {
            var root = AddRect(parent, "Mute", new Vector2(0.09f, 0.24f), new Vector2(0.91f, 0.33f));
            var background = AddImage(root, "Background", Wood, "ui_button_secondary",
                new Vector2(0, 0.13f), new Vector2(0.12f, 0.87f));
            var checkmark = AddImage(background.transform, "Checkmark", Orange, null,
                new Vector2(0.22f, 0.22f), new Vector2(0.78f, 0.78f));
            AddText(root, "Label", "静音", 28, TextAnchor.MiddleLeft, Ink,
                new Vector2(0.16f, 0), new Vector2(0.70f, 1));
            var toggle = root.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = background;
            toggle.graphic = checkmark;
            toggle.isOn = false;
            return toggle;
        }

        private static Button AddButton(Transform parent, string name, string label, int size, Color color,
            Vector2 min, Vector2 max, string spriteName)
        {
            var image = AddImage(parent, name, color, spriteName, min, max);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            AddText(image.transform, "Label", label, size, TextAnchor.MiddleCenter,
                color.grayscale > 0.55f ? Ink : Parchment, new Vector2(0.05f, 0.08f), new Vector2(0.95f, 0.92f));
            return button;
        }

        private static Image AddImage(Transform parent, string name, Color color, string spriteName,
            Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            gameObject.transform.SetParent(parent, false);
            var image = gameObject.GetComponent<Image>();
            image.color = color;
            image.sprite = string.IsNullOrWhiteSpace(spriteName)
                ? null
                : AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{spriteName}.png");
            SetRect(image.rectTransform, min, max);
            return image;
        }

        private static Text AddText(Transform parent, string name, string value, int size, TextAnchor alignment,
            Color color, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Text));
            gameObject.transform.SetParent(parent, false);
            var text = gameObject.GetComponent<Text>();
            text.font = _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Math.Max(12, size / 2);
            text.resizeTextMaxSize = size;
            SetRect(text.rectTransform, min, max);
            return text;
        }

        private static RectTransform AddRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            SetRect(rect, min, max);
            return rect;
        }

        private static void SetReferences(UnityEngine.Object target,
            params (string Name, UnityEngine.Object Value)[] references)
        {
            var serialized = new SerializedObject(target);
            foreach (var reference in references)
            {
                var property = serialized.FindProperty(reference.Name)
                    ?? throw new InvalidOperationException($"Missing serialized field {reference.Name} on {target.GetType().Name}.");
                property.objectReferenceValue = reference.Value;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int FindStringEntry(SerializedProperty array, string fieldName, string value)
        {
            for (var index = 0; index < array.arraySize; index++)
                if (array.GetArrayElementAtIndex(index).FindPropertyRelative(fieldName).stringValue == value) return index;
            return -1;
        }

        private static GameObject LoadPrefab(string path) => PrefabUtility.LoadPrefabContents(path)
            ?? throw new InvalidOperationException($"Prefab is missing: {path}");

        private static Transform FindRequired(Transform root, string path) => root.Find(path)
            ?? throw new InvalidOperationException($"Prefab child is missing: {root.name}/{path}");

        private static void Stretch(RectTransform rect) => SetRect(rect, Vector2.zero, Vector2.one);

        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString("#" + value, out var color);
            return color;
        }
    }
}
#endif
