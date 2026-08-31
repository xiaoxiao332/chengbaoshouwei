using System;
using FortressFrontier.Presentation.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    public static class BuildingUpgradeVisualAuthoring
    {
        private const string GameplayPrefabPath = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";
        private const string UpgradeIconPath = "Assets/Game/Art/Formal/PNG/state_building_upgraded_chevron.png";

        private static readonly Color ConstructionFill = new(0.31f, 0.61f, 0.82f, 1f);
        private static readonly Color UpgradeFill = new(0.94f, 0.51f, 0.13f, 1f);
        private static readonly Color SliderTrack = new(0.12f, 0.09f, 0.07f, 0.82f);
        private static readonly Color UpgradeButton = new(0.851f, 0.42f, 0.169f, 1f);
        private static readonly Color DisabledButton = new(0.38f, 0.32f, 0.28f, 0.72f);

        [MenuItem("Fortress Frontier/UI/Reconcile Building Upgrade Visuals")]
        public static void Reconcile()
        {
            ConfigureUpgradeIconImporter();
            var icon = AssetDatabase.LoadAssetAtPath<Sprite>(UpgradeIconPath)
                ?? throw new InvalidOperationException($"Upgrade icon was not imported as a Sprite: {UpgradeIconPath}");
            var root = PrefabUtility.LoadPrefabContents(GameplayPrefabPath);
            try
            {
                var panel = root.GetComponent<GameplayPanel>()
                    ?? throw new InvalidOperationException("Gameplay prefab has no GameplayPanel.");
                var progressViews = new BuildingSlotProgressView[9];
                for (var index = 0; index < progressViews.Length; index++)
                {
                    var art = root.transform.Find($"Slot{index}/BuildingArt") as RectTransform
                        ?? throw new InvalidOperationException($"Gameplay prefab has no Slot{index}/BuildingArt.");
                    progressViews[index] = ConfigureBuildingArt(art, icon);
                }

                var feedback = ConfigureUpgradeButton(root.transform);
                var panelSerialized = new SerializedObject(panel);
                SetArray(panelSerialized.FindProperty("_buildingProgressViews"), progressViews);
                panelSerialized.FindProperty("_upgradeButtonFeedback").objectReferenceValue = feedback;
                panelSerialized.ApplyModifiedPropertiesWithoutUndo();

                EditorUtility.SetDirty(panel);
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefabPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
            AssetDatabase.SaveAssets();
        }

        private static BuildingSlotProgressView ConfigureBuildingArt(RectTransform art, Sprite icon)
        {
            var construction = EnsureSlider(art, "ConstructionSlider", ConstructionFill);
            var upgrade = EnsureSlider(art, "UpgradeSlider", UpgradeFill);
            var upgradeIcon = EnsureImage(art, "UpgradeIcon");
            ConfigureFixedRect(upgradeIcon.rectTransform, Vector2.one, new Vector2(30f, 30f), new Vector2(-10f, -10f));
            upgradeIcon.sprite = icon;
            upgradeIcon.color = Color.white;
            upgradeIcon.preserveAspect = true;
            upgradeIcon.raycastTarget = false;
            upgradeIcon.gameObject.SetActive(false);
            upgradeIcon.transform.SetAsLastSibling();

            var view = art.GetComponent<BuildingSlotProgressView>() ?? art.gameObject.AddComponent<BuildingSlotProgressView>();
            var serialized = new SerializedObject(view);
            serialized.FindProperty("_constructionSlider").objectReferenceValue = construction;
            serialized.FindProperty("_upgradeSlider").objectReferenceValue = upgrade;
            serialized.FindProperty("_upgradeIcon").objectReferenceValue = upgradeIcon;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(view);
            return view;
        }

        private static Slider EnsureSlider(RectTransform parent, string name, Color fillColor)
        {
            var existing = parent.Find(name);
            var root = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Slider));
            if (existing == null) root.transform.SetParent(parent, false);
            var rect = root.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.12f, 0.91f);
            rect.anchorMax = new Vector2(0.88f, 0.975f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;

            var background = root.GetComponent<Image>() ?? root.AddComponent<Image>();
            background.color = SliderTrack;
            background.raycastTarget = false;

            var fillArea = EnsureRect(rect, "Fill Area");
            fillArea.anchorMin = new Vector2(0.035f, 0.2f);
            fillArea.anchorMax = new Vector2(0.965f, 0.8f);
            fillArea.offsetMin = Vector2.zero;
            fillArea.offsetMax = Vector2.zero;
            var fill = EnsureImage(fillArea, "Fill");
            SetStretch(fill.rectTransform);
            fill.color = fillColor;
            fill.raycastTarget = false;

            var slider = root.GetComponent<Slider>() ?? root.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1000f;
            slider.wholeNumbers = true;
            slider.direction = Slider.Direction.LeftToRight;
            slider.fillRect = fill.rectTransform;
            slider.handleRect = null;
            slider.targetGraphic = null;
            slider.interactable = false;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.SetValueWithoutNotify(0f);
            root.SetActive(false);
            return slider;
        }

        private static UpgradeButtonFeedback ConfigureUpgradeButton(Transform root)
        {
            var buttonTransform = root.Find("BuildingMenu/UpgradeAction") as RectTransform
                ?? throw new InvalidOperationException("Gameplay prefab has no BuildingMenu/UpgradeAction.");
            var button = buttonTransform.GetComponent<Button>()
                ?? throw new InvalidOperationException("UpgradeAction has no Button.");
            var rootImage = buttonTransform.GetComponent<Image>() ?? buttonTransform.gameObject.AddComponent<Image>();
            var pivot = EnsureRect(buttonTransform, "FeedbackPivot");
            SetStretch(pivot);
            pivot.localScale = Vector3.one;
            var pivotImage = pivot.GetComponent<Image>() ?? pivot.gameObject.AddComponent<Image>();
            pivotImage.sprite = rootImage.sprite;
            pivotImage.type = rootImage.type;
            pivotImage.preserveAspect = rootImage.preserveAspect;
            pivotImage.color = UpgradeButton;
            pivotImage.raycastTarget = false;

            var label = buttonTransform.Find("Label");
            if (label != null) label.SetParent(pivot, false);
            rootImage.color = Color.clear;
            rootImage.raycastTarget = true;
            button.targetGraphic = pivotImage;
            button.transition = Selectable.Transition.None;

            var feedback = buttonTransform.GetComponent<UpgradeButtonFeedback>()
                ?? buttonTransform.gameObject.AddComponent<UpgradeButtonFeedback>();
            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("_visualPivot").objectReferenceValue = pivot;
            serialized.FindProperty("_visualImage").objectReferenceValue = pivotImage;
            serialized.FindProperty("_baseColor").colorValue = UpgradeButton;
            serialized.FindProperty("_disabledColor").colorValue = DisabledButton;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(feedback);
            return feedback;
        }

        private static Image EnsureImage(Transform parent, string name)
        {
            var existing = parent.Find(name);
            var gameObject = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            if (existing == null) gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<Image>() ?? gameObject.AddComponent<Image>();
        }

        private static RectTransform EnsureRect(Transform parent, string name)
        {
            var existing = parent.Find(name) as RectTransform;
            if (existing != null) return existing;
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            return gameObject.GetComponent<RectTransform>();
        }

        private static void SetStretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void ConfigureFixedRect(RectTransform rect, Vector2 anchor, Vector2 size, Vector2 position)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            rect.localScale = Vector3.one;
        }

        private static void ConfigureUpgradeIconImporter()
        {
            AssetDatabase.ImportAsset(UpgradeIconPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(UpgradeIconPath) as TextureImporter
                ?? throw new InvalidOperationException($"Upgrade icon has no TextureImporter: {UpgradeIconPath}");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 128;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }
    }
}
