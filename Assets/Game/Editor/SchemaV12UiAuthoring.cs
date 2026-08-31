#if UNITY_EDITOR
using System;
using FortressFrontier.Presentation.Prototype;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    internal static class SchemaV12UiAuthoring
    {
        private const string GameplayPath = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";
        private const string SelectionPath = "Assets/Game/Content/Prefabs/UI/Selection.prefab";
        private static readonly Color Ink = new(0.16f, 0.12f, 0.09f, 0.98f);
        private static readonly Color Parchment = new(0.88f, 0.76f, 0.55f, 0.98f);
        private static readonly Color Blue = new(0.20f, 0.48f, 0.82f, 1f);
        private static readonly Color Orange = new(0.91f, 0.44f, 0.14f, 1f);

        [MenuItem("Fortress Frontier/Schema v12/Rebuild Map Pagination And Research UI")]
        public static void Rebuild()
        {
            RebuildSelection();
            RebuildGameplay();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Schema v12 map bindings, pagination, and ResearchPanel were rebuilt idempotently.");
        }

        private static void RebuildSelection()
        {
            var root = PrefabUtility.LoadPrefabContents(SelectionPath);
            try
            {
                var panel = root.GetComponent<SelectionPanel>() ?? throw new InvalidOperationException("SelectionPanel missing.");
                var preview = root.transform.Find("BattlefieldPanel/MapPreview")?.GetComponent<Image>()
                    ?? throw new InvalidOperationException("Selection/BattlefieldPanel/MapPreview missing.");
                var pager = EnsureRect(root.transform, "CardPager", new Vector2(0.29f, 0.315f), new Vector2(0.49f, 0.355f));
                ClearChildren(pager);
                var previous = CreateButton(pager, "Previous", "‹", new Vector2(0f, 0f), new Vector2(0.25f, 1f), Blue);
                var page = CreateText(pager, "Page", "1/2", 22, new Vector2(0.27f, 0f), new Vector2(0.73f, 1f));
                var next = CreateButton(pager, "Next", "›", new Vector2(0.75f, 0f), new Vector2(1f, 1f), Blue);
                SetRefs(panel, ("_mapPreview", preview), ("_previousCardPageButton", previous),
                    ("_nextCardPageButton", next), ("_cardPageText", page));
                PrefabUtility.SaveAsPrefabAsset(root, SelectionPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void RebuildGameplay()
        {
            var root = PrefabUtility.LoadPrefabContents(GameplayPath);
            try
            {
                var panel = root.GetComponent<GameplayPanel>() ?? throw new InvalidOperationException("GameplayPanel missing.");
                var world = root.transform.Find("World")?.GetComponent<Image>()
                    ?? throw new InvalidOperationException("Gameplay/World missing.");
                var tray = root.transform.Find("CardTray") as RectTransform
                    ?? throw new InvalidOperationException("Gameplay/CardTray missing.");
                tray.anchorMin = new Vector2(0.28f, 0.005f);
                tray.anchorMax = new Vector2(0.80f, 0.14f);
                tray.offsetMin = tray.offsetMax = Vector2.zero;
                var pager = EnsureRect(tray, "SoldierPager", new Vector2(0.79f, 0.18f), new Vector2(0.88f, 0.82f));
                ClearChildren(pager);
                var previous = CreateButton(pager, "Previous", "‹", new Vector2(0f, 0.55f), new Vector2(1f, 1f), Blue);
                var page = CreateText(pager, "Page", "1/2", 16, new Vector2(0f, 0.34f), new Vector2(1f, 0.56f));
                var next = CreateButton(pager, "Next", "›", new Vector2(0f, 0f), new Vector2(1f, 0.45f), Blue);

                var research = root.transform.Find("ResearchPanel") as RectTransform
                    ?? throw new InvalidOperationException("Gameplay/ResearchPanel missing.");
                research.anchorMin = new Vector2(0.31f, 0.24f);
                research.anchorMax = new Vector2(0.75f, 0.76f);
                research.offsetMin = research.offsetMax = Vector2.zero;
                ClearChildren(research);
                CreateText(research, "Title", "类别研究", 34, new Vector2(0.06f, 0.84f), new Vector2(0.78f, 0.97f));
                var close = CreateButton(research, "Close", "×", new Vector2(0.87f, 0.87f), new Vector2(0.96f, 0.96f), Ink);
                var status = CreateText(research, "Status", "需要研究院", 20, new Vector2(0.08f, 0.10f), new Vector2(0.92f, 0.22f));
                var progressBack = CreateImage(research, "ProgressBack", Ink, new Vector2(0.08f, 0.05f), new Vector2(0.92f, 0.09f));
                var progress = CreateImage(progressBack.rectTransform, "Progress", Orange, new Vector2(0.02f, 0.15f), new Vector2(0.98f, 0.85f));
                progress.type = Image.Type.Filled; progress.fillMethod = Image.FillMethod.Horizontal; progress.fillAmount = 0f;

                var buttons = new Button[3];
                var images = new Image[3];
                var texts = new Text[3];
                for (var i = 0; i < 3; i++)
                {
                    var x0 = 0.05f + i * 0.315f;
                    var button = CreateButton(research, "Option" + i, string.Empty,
                        new Vector2(x0, 0.25f), new Vector2(x0 + 0.285f, 0.80f), i == 0 ? Blue : Parchment);
                    buttons[i] = button;
                    images[i] = CreateImage(button.transform, "Art", Color.white,
                        new Vector2(0.12f, 0.42f), new Vector2(0.88f, 0.92f));
                    images[i].raycastTarget = false;
                    texts[i] = CreateText(button.transform, "Label", "研究候选", 18,
                        new Vector2(0.05f, 0.04f), new Vector2(0.95f, 0.42f));
                    texts[i].raycastTarget = false;
                }

                SetRefs(panel, ("_worldBackground", world), ("_previousSoldierPageButton", previous),
                    ("_nextSoldierPageButton", next), ("_soldierPageText", page),
                    ("_researchCloseButton", close), ("_researchProgressFill", progress), ("_researchStatusText", status));
                SetArray(panel, "_researchOptionButtons", buttons);
                SetArray(panel, "_researchOptionImages", images);
                SetArray(panel, "_researchOptionTexts", texts);
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static RectTransform EnsureRect(Transform parent, string name, Vector2 min, Vector2 max)
        {
            var child = parent.Find(name);
            var rect = child as RectTransform;
            if (rect == null)
            {
                var go = new GameObject(name, typeof(RectTransform));
                rect = go.GetComponent<RectTransform>();
                rect.SetParent(parent, false);
            }
            rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = rect.offsetMax = Vector2.zero;
            return rect;
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
                UnityEngine.Object.DestroyImmediate(parent.GetChild(i).gameObject);
        }

        private static Image CreateImage(Transform parent, string name, Color color, Vector2 min, Vector2 max)
        {
            var rect = EnsureRect(parent, name, min, max);
            var image = rect.GetComponent<Image>() ?? rect.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private static Button CreateButton(Transform parent, string name, string label, Vector2 min, Vector2 max, Color color)
        {
            var image = CreateImage(parent, name, color, min, max);
            var button = image.GetComponent<Button>() ?? image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            if (!string.IsNullOrEmpty(label))
            {
                var text = CreateText(image.transform, "Label", label, 22, Vector2.zero, Vector2.one);
                text.raycastTarget = false;
            }
            return button;
        }

        private static Text CreateText(Transform parent, string name, string value, int size, Vector2 min, Vector2 max)
        {
            var rect = EnsureRect(parent, name, min, max);
            var text = rect.GetComponent<Text>() ?? rect.gameObject.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = value; text.fontSize = size; text.alignment = TextAnchor.MiddleCenter;
            text.color = Ink; text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            return text;
        }

        private static void SetRefs(UnityEngine.Object target, params (string, UnityEngine.Object)[] refs)
        {
            var serialized = new SerializedObject(target);
            foreach (var pair in refs)
            {
                var property = serialized.FindProperty(pair.Item1)
                    ?? throw new InvalidOperationException($"Missing property {pair.Item1} on {target.GetType().Name}.");
                property.objectReferenceValue = pair.Item2;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetArray<T>(UnityEngine.Object target, string name, T[] values) where T : UnityEngine.Object
        {
            var serialized = new SerializedObject(target);
            var property = serialized.FindProperty(name) ?? throw new InvalidOperationException($"Missing property {name}.");
            property.arraySize = values.Length;
            for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
#endif
