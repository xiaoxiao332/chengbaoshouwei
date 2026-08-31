#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FortressFrontier.Presentation.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    internal static class ReinforcementCardAuthoring
    {
        private const string VisualPrefabPath = "Assets/Game/Content/Prefabs/UI/ReinforcementCardVisual.prefab";
        private const string GameplayPrefabPath = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";

        [MenuItem("Fortress Frontier/Schema v14/Reconcile Reward Choice Visuals")]
        private static void Rebuild()
        {
            if (AreAuthoredAssetsCurrent())
            {
                Debug.Log("Schema v14 reward choice visuals are already reconciled and valid.");
                return;
            }

            BuildVisualPrefab();
            EmbedIntoGameplay();
            ValidateAuthoredAssets();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Schema v14 reward choice visuals reconciled and validated.");
        }

        private static bool AreAuthoredAssetsCurrent()
        {
            try
            {
                ValidateAuthoredAssets();
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        private static void BuildVisualPrefab()
        {
            var root = new GameObject("ReinforcementCardVisual", typeof(RectTransform), typeof(ReinforcementCardVisual));
            try
            {
                Stretch(root.GetComponent<RectTransform>());
                var backdropObject = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
                backdropObject.transform.SetParent(root.transform, false); Stretch(backdropObject.GetComponent<RectTransform>());
                var backdrop = backdropObject.GetComponent<Image>(); backdrop.color = new Color(0.08f, 0.055f, 0.035f, 1f); backdrop.raycastTarget = false;
                var label = CreateText(root.transform, "ReinforcementLabel", "援军", 15, FontStyle.Bold,
                    new Color(1f, 0.78f, 0.28f, 1f), new Vector2(0.03f, 0.80f), new Vector2(0.42f, 0.98f), TextAnchor.MiddleLeft);
                var title = CreateText(root.transform, "Title", "援军卡", 14, FontStyle.Bold, Color.white,
                    new Vector2(0.03f, 0.01f), new Vector2(0.97f, 0.21f), TextAnchor.MiddleCenter);
                var icons = new Image[3];
                var quantities = new Text[3];
                var min = new[] { 0.02f, 0.35f, 0.68f };
                var max = new[] { 0.32f, 0.65f, 0.98f };
                for (var index = 0; index < 3; index++)
                {
                    var slot = new GameObject("UnitSlot" + index, typeof(RectTransform), typeof(Image));
                    slot.transform.SetParent(root.transform, false);
                    var rect = slot.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(min[index], 0.21f); rect.anchorMax = new Vector2(max[index], 0.82f);
                    rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                    icons[index] = slot.GetComponent<Image>();
                    icons[index].preserveAspect = true; icons[index].raycastTarget = false; icons[index].color = Color.white;
                    quantities[index] = CreateText(slot.transform, "Quantity", "×1", 16, FontStyle.Bold, Color.white,
                        new Vector2(0.40f, 0f), Vector2.one, TextAnchor.LowerRight);
                }

                var serialized = new SerializedObject(root.GetComponent<ReinforcementCardVisual>());
                SetArray(serialized.FindProperty("_unitIcons"), icons);
                SetArray(serialized.FindProperty("_quantityTexts"), quantities);
                serialized.FindProperty("_reinforcementLabel").objectReferenceValue = label;
                serialized.FindProperty("_titleText").objectReferenceValue = title;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, VisualPrefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void EmbedIntoGameplay()
        {
            var visualPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath)
                ?? throw new InvalidOperationException("Reinforcement visual prefab was not created.");
            var root = PrefabUtility.LoadPrefabContents(GameplayPrefabPath);
            try
            {
                var itemVisuals = new ReinforcementCardVisual[6];
                for (var index = 0; index < itemVisuals.Length; index++)
                    itemVisuals[index] = Embed(visualPrefab, FindTransform(root.transform, "Item" + index),
                        Vector2.zero, Vector2.one);
                var choiceVisuals = new ReinforcementCardVisual[4];
                var choiceButtons = new Button[4];
                var choiceImages = new Image[4];
                var choicePanel = FindTransform(root.transform, "ChoicePanel");
                EnsureChoicePopCanvas(choicePanel);
                var choiceTitle = choicePanel.Find("Title")?.GetComponent<Text>()
                    ?? throw new InvalidOperationException("ChoicePanel/Title is missing Text.");
                choiceTitle.text = "战后整备 · 四选一";
                for (var index = 0; index < choiceVisuals.Length; index++)
                {
                    var choice = FindTransform(choicePanel, "Choice" + index);
                    choiceButtons[index] = choice.GetComponent<Button>()
                        ?? throw new InvalidOperationException($"Choice{index} is missing Button.");
                    choiceImages[index] = choice.Find("img")?.GetComponent<Image>()
                        ?? throw new InvalidOperationException($"Choice{index}/img is missing Image.");
                    var existing = choice.Find("ReinforcementVisual");
                    if (index < 3)
                    {
                        if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
                        continue;
                    }
                    choiceVisuals[index] = existing != null
                        ? existing.GetComponent<ReinforcementCardVisual>()
                        : Embed(visualPrefab, choice, new Vector2(0.06f, 0.30f), new Vector2(0.94f, 0.98f));
                }

                var panel = root.GetComponent<GameplayPanel>()
                    ?? throw new InvalidOperationException("Gameplay prefab is missing GameplayPanel.");
                var serialized = new SerializedObject(panel);
                SetArray(serialized.FindProperty("_itemReinforcementVisuals"), itemVisuals);
                SetArray(serialized.FindProperty("_choiceOptions"), choiceButtons);
                SetArray(serialized.FindProperty("_choiceArtImages"), choiceImages);
                SetArray(serialized.FindProperty("_choiceReinforcementVisuals"), choiceVisuals);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static ReinforcementCardVisual Embed(GameObject prefab, Transform parent, Vector2 anchorMin, Vector2 anchorMax)
        {
            var existing = parent.Find("ReinforcementVisual");
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing.gameObject);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = "ReinforcementVisual";
            var rect = instance.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            return instance.GetComponent<ReinforcementCardVisual>();
        }

        private static void EnsureChoicePopCanvas(Transform choicePanel)
        {
            if (choicePanel.parent != null && choicePanel.parent.name == "ChoicePopCanvas")
            {
                ConfigureChoiceCanvas(choicePanel.parent.gameObject);
                return;
            }

            var originalParent = choicePanel.parent;
            var siblingIndex = choicePanel.GetSiblingIndex();
            var wrapper = new GameObject("ChoicePopCanvas", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
            wrapper.transform.SetParent(originalParent, false);
            wrapper.transform.SetSiblingIndex(siblingIndex);
            Stretch(wrapper.GetComponent<RectTransform>());
            ConfigureChoiceCanvas(wrapper);
            choicePanel.SetParent(wrapper.transform, false);
        }

        private static void ConfigureChoiceCanvas(GameObject wrapper)
        {
            var canvas = wrapper.GetComponent<Canvas>() ?? wrapper.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 200;
            var serializedCanvas = new SerializedObject(canvas);
            serializedCanvas.FindProperty("m_OverrideSorting").boolValue = true;
            serializedCanvas.FindProperty("m_SortingOrder").intValue = 200;
            serializedCanvas.ApplyModifiedPropertiesWithoutUndo();
            if (wrapper.GetComponent<GraphicRaycaster>() == null) wrapper.AddComponent<GraphicRaycaster>();
        }

        private static void ValidateAuthoredAssets()
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath)?.GetComponent<ReinforcementCardVisual>()
                ?? throw new InvalidOperationException("ReinforcementCardVisual prefab/component is missing.");
            if (visual.UnitIcons.Count != 3 || visual.QuantityTexts.Count != 3 || visual.ReinforcementLabel == null || visual.TitleText == null)
                throw new InvalidOperationException("ReinforcementCardVisual requires three icon slots, three quantities, a label and a title.");

            var gameplay = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPrefabPath);
            var panel = gameplay?.GetComponent<GameplayPanel>() ?? throw new InvalidOperationException("GameplayPanel is missing.");
            var nested = gameplay.GetComponentsInChildren<ReinforcementCardVisual>(true);
            if (nested.Length != 7) throw new InvalidOperationException($"Gameplay prefab requires six item and one Choice3 reinforcement visuals; found {nested.Length}.");
            var choicePanel = FindTransform(gameplay.transform, "ChoicePanel");
            if (choicePanel.Find("Title")?.GetComponent<Text>()?.text != "战后整备 · 四选一")
                throw new InvalidOperationException("ChoicePanel title must identify the four-choice reward.");
            var choiceCanvas = choicePanel.parent != null && choicePanel.parent.name == "ChoicePopCanvas"
                ? choicePanel.parent.GetComponent<Canvas>()
                : null;
            if (choiceCanvas == null || !choiceCanvas.overrideSorting || choiceCanvas.sortingOrder != 200 ||
                choiceCanvas.GetComponent<GraphicRaycaster>() == null)
                throw new InvalidOperationException("ChoicePanel must render and receive input on the Pop sorting layer.");
            var serialized = new SerializedObject(panel);
            ValidateArray(serialized.FindProperty("_itemReinforcementVisuals"), 6);
            ValidateArray(serialized.FindProperty("_choiceOptions"), 4);
            ValidateArray(serialized.FindProperty("_choiceArtImages"), 4);
            var choiceVisuals = serialized.FindProperty("_choiceReinforcementVisuals");
            if (choiceVisuals == null || choiceVisuals.arraySize != 4)
                throw new InvalidOperationException("GameplayPanel choice reinforcement visual array must have four entries.");
            for (var index = 0; index < 4; index++)
                if ((index == 3) != (choiceVisuals.GetArrayElementAtIndex(index).objectReferenceValue != null))
                    throw new InvalidOperationException("Only Choice3 may reference a reinforcement composition visual.");
        }

        private static Text CreateText(Transform parent, string name, string value, int size, FontStyle style,
            Color color, Vector2 anchorMin, Vector2 anchorMax, TextAnchor alignment)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Text));
            child.transform.SetParent(parent, false);
            var text = child.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value;
            text.fontSize = size; text.fontStyle = style; text.alignment = alignment; text.color = color;
            text.raycastTarget = false; text.resizeTextForBestFit = true; text.resizeTextMinSize = 11; text.resizeTextMaxSize = size;
            var rect = text.rectTransform; rect.anchorMin = anchorMin; rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            return text;
        }

        private static void Stretch(RectTransform rect)
        { rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; }

        private static Transform FindTransform(Transform root, string name)
        {
            var found = FindTransformOrNull(root, name);
            return found ?? throw new InvalidOperationException($"Gameplay prefab is missing '{name}'.");
        }

        private static Transform FindTransformOrNull(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            { var found = FindTransformOrNull(child, name); if (found != null) return found; }
            return null;
        }

        private static void SetArray<T>(SerializedProperty property, IReadOnlyList<T> values) where T : UnityEngine.Object
        {
            property.arraySize = values.Count;
            for (var index = 0; index < values.Count; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static void ValidateArray(SerializedProperty property, int expected)
        {
            if (property == null || property.arraySize != expected) throw new InvalidOperationException("GameplayPanel reinforcement visual array is incomplete.");
            for (var index = 0; index < expected; index++)
                if (property.GetArrayElementAtIndex(index).objectReferenceValue == null)
                    throw new InvalidOperationException("GameplayPanel reinforcement visual reference is missing.");
        }
    }
}
#endif
