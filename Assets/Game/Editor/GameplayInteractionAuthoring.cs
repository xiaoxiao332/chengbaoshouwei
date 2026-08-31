#if UNITY_EDITOR
using System;
using FortressFrontier.Presentation.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    public static class GameplayInteractionAuthoring
    {
        private const string GameplayPrefab = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";
        private static readonly Color Ink = Hex("211815");
        private static readonly Color Wood = Hex("4C3224");
        private static readonly Color Parchment = Hex("F2DDA9");
        private static readonly Color Gold = Hex("C8862D");
        private static readonly Color Blue = Hex("327BD1");
        private static readonly Color Orange = Hex("D96B2B");
        private static readonly Color Red = Hex("9F3F36");

        [MenuItem("Fortress Frontier/Gameplay/Build Hover Interaction Panels")]
        public static void Build()
        {
            var root = PrefabUtility.LoadPrefabContents(GameplayPrefab);
            try
            {
                var panel = root.GetComponent<GameplayPanel>()
                    ?? throw new InvalidOperationException("Gameplay prefab has no GameplayPanel.");
                Configure(root, panel);
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefab);
                AssetDatabase.SaveAssets();
                Debug.Log("Gameplay card hover and selected-building action panels were authored and bound.");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void Configure(GameObject root, GameplayPanel panel)
        {
            var existingMenu = Find(root.transform, "BuildingMenu")
                ?? throw new InvalidOperationException("Gameplay prefab has no BuildingMenu.");
            var menuImage = existingMenu.GetComponent<Image>() ?? existingMenu.gameObject.AddComponent<Image>();
            ConfigurePanelRect(existingMenu.GetComponent<RectTransform>(), new Vector2(336f, 82f));
            menuImage.color = Wood;
            menuImage.raycastTarget = true;

            var oldTitle = existingMenu.Find("Menu");
            if (oldTitle != null) UnityEngine.Object.DestroyImmediate(oldTitle.gameObject);
            var resume = existingMenu.Find("ResumeAction");
            if (resume != null) UnityEngine.Object.DestroyImmediate(resume.gameObject);
            var pause = EnsureButton(existingMenu, "PauseAction", "暂停", Blue, 0);
            var upgrade = EnsureButton(existingMenu, "UpgradeAction", "升级", Orange, 1);
            var demolish = EnsureButton(existingMenu, "DemolishAction", "拆除", Red, 2);
            existingMenu.SetAsLastSibling();
            existingMenu.gameObject.SetActive(false);

            var hover = Find(root.transform, "CardHoverPanel");
            if (hover == null)
            {
                var value = new GameObject("CardHoverPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                value.transform.SetParent(root.transform, false);
                hover = value.transform;
            }
            var hoverRect = hover.GetComponent<RectTransform>();
            ConfigurePanelRect(hoverRect, new Vector2(372f, 158f));
            var hoverImage = hover.GetComponent<Image>();
            hoverImage.sprite = menuImage.sprite;
            hoverImage.type = menuImage.type;
            hoverImage.color = Wood;
            hoverImage.raycastTarget = false;

            var parchment = EnsureImage(hover, "Parchment", Parchment);
            SetStretch(parchment.rectTransform, new Vector2(8f, 8f), new Vector2(-8f, -8f));
            parchment.raycastTarget = false;
            var name = EnsureText(parchment.transform, "Name", 25, FontStyle.Bold, Ink,
                new Vector2(0.055f, 0.68f), new Vector2(0.945f, 0.94f));
            var cost = EnsureText(parchment.transform, "Cost", 19, FontStyle.Bold, Gold,
                new Vector2(0.055f, 0.43f), new Vector2(0.945f, 0.68f));
            var attributes = EnsureText(parchment.transform, "Attributes", 18, FontStyle.Normal, Ink,
                new Vector2(0.055f, 0.08f), new Vector2(0.945f, 0.43f));
            hover.SetAsLastSibling();
            hover.gameObject.SetActive(false);

            var serialized = new SerializedObject(panel);
            serialized.FindProperty("_buildingMenu").objectReferenceValue = existingMenu.gameObject;
            SetArray(serialized.FindProperty("_buildingActionButtons"), new[] { pause, upgrade, demolish });
            serialized.FindProperty("_cardHoverPanel").objectReferenceValue = hover.gameObject;
            serialized.FindProperty("_cardHoverNameText").objectReferenceValue = name;
            serialized.FindProperty("_cardHoverCostText").objectReferenceValue = cost;
            serialized.FindProperty("_cardHoverAttributesText").objectReferenceValue = attributes;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Button EnsureButton(Transform parent, string name, string label, Color color, int index)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                value.transform.SetParent(parent, false);
                child = value.transform;
            }
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(index / 3f + 0.018f, 0.14f);
            rect.anchorMax = new Vector2((index + 1) / 3f - 0.018f, 0.86f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var image = child.GetComponent<Image>() ?? child.gameObject.AddComponent<Image>();
            image.color = color;
            var button = child.GetComponent<Button>() ?? child.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var text = EnsureText(child, "Label", 20, FontStyle.Bold, Color.white, Vector2.zero, Vector2.one);
            text.text = label;
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        private static Image EnsureImage(Transform parent, string name, Color color)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                value.transform.SetParent(parent, false);
                child = value.transform;
            }
            var image = child.GetComponent<Image>();
            image.color = color;
            return image;
        }

        private static Text EnsureText(Transform parent, string name, int size, FontStyle style, Color color,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                value.transform.SetParent(parent, false);
                child = value.transform;
            }
            var text = child.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            var rect = text.rectTransform;
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return text;
        }

        private static void ConfigurePanelRect(RectTransform rect, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static void SetStretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = offsetMin;
            rect.offsetMax = offsetMax;
        }

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++)
                property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }

        private static Transform Find(Transform root, string name)
        {
            foreach (var value in root.GetComponentsInChildren<Transform>(true))
                if (value.name == name) return value;
            return null;
        }

        private static Color Hex(string value) => ColorUtility.TryParseHtmlString("#" + value, out var color)
            ? color : Color.white;
    }
}
#endif
