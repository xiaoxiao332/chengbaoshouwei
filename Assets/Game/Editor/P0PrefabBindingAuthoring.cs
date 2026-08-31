#if UNITY_EDITOR
using System;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Bootstrap;
using FortressFrontier.Runtime.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    public static class P0PrefabBindingAuthoring
    {
        private const string GameplayPath = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";
        private const string ResultPath = "Assets/Game/Content/Prefabs/UI/Result.prefab";
        private const string SelectionPath = "Assets/Game/Content/Prefabs/UI/Selection.prefab";
        private const string GameplayScenePath = "Assets/Game/Scenes/Gameplay.unity";
        private const string ArtRoot = "Assets/Game/Art/Formal/PNG/";

        [MenuItem("Fortress Frontier/P0/Bind Prefab References")]
        public static void Bind()
        {
            BindGameplay();
            BindResult();
            BindSelectionProgression();
            BindGameplayScene();
            AssetDatabase.SaveAssets();
            Debug.Log("P0 prefab references bound and validated.");
        }

        private static void BindSelectionProgression()
        {
            var root = PrefabUtility.LoadPrefabContents(SelectionPath);
            try
            {
                var panel = root.GetComponent<SelectionPanel>()
                    ?? throw new InvalidOperationException("Selection prefab has no SelectionPanel.");
                var detail = FindByName(root.transform, "CardDetail")
                    ?? throw new InvalidOperationException("Selection prefab has no CardDetail.");
                Button EnsureButton(string name, string label, Vector2 anchoredPosition)
                {
                    var existing = FindByName(detail, name)?.GetComponent<Button>();
                    if (existing != null) return existing;
                    var gameObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
                    gameObject.transform.SetParent(detail, false);
                    var rect = gameObject.GetComponent<RectTransform>();
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    rect.anchoredPosition = anchoredPosition;
                    rect.sizeDelta = new Vector2(130f, 54f);
                    gameObject.GetComponent<Image>().color = new Color(0.18f, 0.42f, 0.72f, 1f);
                    var textObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
                    textObject.transform.SetParent(gameObject.transform, false);
                    var textRect = textObject.GetComponent<RectTransform>();
                    textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
                    textRect.offsetMin = textRect.offsetMax = Vector2.zero;
                    var text = textObject.GetComponent<Text>();
                    text.text = label; text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                    text.fontSize = 22; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white;
                    return gameObject.GetComponent<Button>();
                }
                var unlock = EnsureButton("Unlock", "解锁", new Vector2(-150f, -16f));
                var upgrade = EnsureButton("Upgrade", "升级", new Vector2(-10f, -16f));
                var serialized = new SerializedObject(panel);
                var battlefieldPanel = FindByName(root.transform, "BattlefieldPanel")
                    ?? throw new InvalidOperationException("Selection prefab has no BattlefieldPanel.");
                var mapTitle = FindByName(battlefieldPanel, "MapTitle")?.GetComponent<Text>()
                    ?? throw new InvalidOperationException("Selection prefab has no MapTitle.");
                var previous = EnsureSelectionNavigationButton(battlefieldPanel, "PreviousBattlefield", "‹", new Vector2(-590f, -18f));
                var next = EnsureSelectionNavigationButton(battlefieldPanel, "NextBattlefield", "›", new Vector2(-12f, -18f));
                serialized.FindProperty("_unlockButton").objectReferenceValue = unlock;
                serialized.FindProperty("_upgradeButton").objectReferenceValue = upgrade;
                serialized.FindProperty("_battlefieldName").objectReferenceValue = mapTitle;
                serialized.FindProperty("_previousBattlefieldButton").objectReferenceValue = previous;
                serialized.FindProperty("_nextBattlefieldButton").objectReferenceValue = next;
                SetArray(serialized.FindProperty("_cardImages"), 8,
                    index => Find<Image>(root.transform, $"Card{index}/Art"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, SelectionPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BindGameplayScene()
        {
            var scene = SceneManager.GetSceneByPath(GameplayScenePath);
            var openedHere = !scene.IsValid() || !scene.isLoaded;
            if (openedHere) scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Additive);
            try
            {
                SceneContext context = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    context = root.GetComponentInChildren<SceneContext>(true);
                    if (context != null) break;
                }
                if (context == null) throw new InvalidOperationException("Gameplay scene has no SceneContext.");
                var installer = context.GetComponent<GameplayInstaller>()
                    ?? throw new InvalidOperationException("Gameplay SceneContext has no GameplayInstaller.");
                var serialized = new SerializedObject(context);
                var installers = serialized.FindProperty("_installers");
                installers.arraySize = 1;
                installers.GetArrayElementAtIndex(0).objectReferenceValue = installer;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene, GameplayScenePath);
            }
            finally
            {
                if (openedHere) EditorSceneManager.CloseScene(scene, true);
            }
        }

        private static void BindGameplay()
        {
            var root = PrefabUtility.LoadPrefabContents(GameplayPath);
            try
            {
                var panel = root.GetComponent<GameplayPanel>()
                    ?? throw new InvalidOperationException("Gameplay prefab has no GameplayPanel.");
                var serialized = new SerializedObject(panel);
                SetArray(serialized.FindProperty("_resourceTexts"), 4,
                    index => Find<Text>(root.transform, $"ResourceGroup{index}/Values"));
                SetArray(serialized.FindProperty("_buildingImages"), 9,
                    index => Find<Image>(root.transform, $"Slot{index}/BuildingArt"));
                SetArray(serialized.FindProperty("_deployedUnitImages"), 7,
                    index => Find<Image>(root.transform, $"Unit{index}"));
                var decreaseButtons = new Button[4];
                var increaseButtons = new Button[4];
                var countTexts = new Text[4];
                for (var index = 0; index < 4; index++)
                {
                    var card = FindByName(root.transform, $"Soldier{index}")
                        ?? throw new InvalidOperationException($"Missing soldier card Soldier{index}.");
                    EnsureButton(card);
                    EnsureSoldierCountControls(card, out decreaseButtons[index], out increaseButtons[index], out countTexts[index]);
                }
                SetArray(serialized.FindProperty("_soldierDecreaseButtons"), decreaseButtons.Length, index => decreaseButtons[index]);
                SetArray(serialized.FindProperty("_soldierIncreaseButtons"), increaseButtons.Length, index => increaseButtons[index]);
                SetArray(serialized.FindProperty("_soldierCountTexts"), countTexts.Length, index => countTexts[index]);
                var deploymentObject = serialized.FindProperty("_deploymentGrid")?.objectReferenceValue as GameObject;
                var deployment = deploymentObject != null ? deploymentObject.transform : FindByName(root.transform, "DeploymentGrid");
                if (deployment == null) throw new InvalidOperationException("Gameplay prefab has no deployment area object.");
                var deploymentImage = deployment.GetComponent<Image>() ?? deployment.gameObject.AddComponent<Image>();
                deploymentImage.raycastTarget = true;
                var deploymentInput = deployment.GetComponent<DeploymentAreaInput>() ?? deployment.gameObject.AddComponent<DeploymentAreaInput>();
                serialized.FindProperty("_deploymentAreaInput").objectReferenceValue = deploymentInput;
                var actions = new[] { "PauseAction", "UpgradeAction", "DemolishAction" };
                SetArray(serialized.FindProperty("_buildingActionButtons"), actions.Length,
                    index => Find<Button>(root.transform, $"BuildingMenu/{actions[index]}"));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void BindResult()
        {
            var root = PrefabUtility.LoadPrefabContents(ResultPath);
            try
            {
                var panel = root.GetComponent<ResultPanel>()
                    ?? throw new InvalidOperationException("Result prefab has no ResultPanel.");
                var serialized = new SerializedObject(panel);
                serialized.FindProperty("_retryButton").objectReferenceValue = Find<Button>(root.transform, "Panel/Retry");
                serialized.FindProperty("_rewardedAdButton").objectReferenceValue = Find<Button>(root.transform, "Panel/RewardedAd");
                serialized.FindProperty("_rewardedAdLabel").objectReferenceValue = Find<Text>(root.transform, "Panel/RewardedAd/Label");
                serialized.FindProperty("_rewardedAdStatus").objectReferenceValue = Find<Text>(root.transform, "Panel/RewardedAdStatus");
                serialized.FindProperty("_privacyPolicyButton").objectReferenceValue = Find<Button>(root.transform, "Panel/PrivacyPolicy");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var summary = Find<Text>(root.transform, "Panel/Summary");
                var summaryRect = summary.rectTransform;
                summaryRect.anchorMin = new Vector2(0.08f, 0.20f); summaryRect.anchorMax = new Vector2(0.92f, 0.78f);
                summaryRect.offsetMin = summaryRect.offsetMax = Vector2.zero;
                summary.fontSize = 20; summary.alignment = TextAnchor.UpperLeft;
                summary.horizontalOverflow = HorizontalWrapMode.Wrap;
                summary.verticalOverflow = VerticalWrapMode.Overflow;
                var core = root.transform.Find("Panel/Core") as RectTransform;
                if (core != null)
                {
                    core.anchorMin = new Vector2(0.80f, 0.80f); core.anchorMax = new Vector2(0.94f, 0.95f);
                    core.offsetMin = core.offsetMax = Vector2.zero;
                }
                var title = Find<Text>(root.transform, "Panel/Title");
                title.rectTransform.anchorMax = new Vector2(0.78f, 0.97f);
                PrefabUtility.SaveAsPrefabAsset(root, ResultPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static T Find<T>(Transform root, string path) where T : Component =>
            root.Find(path)?.GetComponent<T>() ?? throw new InvalidOperationException($"Missing {typeof(T).Name} at '{path}'.");

        private static Button EnsureButton(Transform target)
        {
            var button = target.GetComponent<Button>() ?? target.gameObject.AddComponent<Button>();
            button.targetGraphic = target.GetComponent<Graphic>() ?? target.gameObject.AddComponent<Image>();
            return button;
        }

        private static Transform FindByName(Transform root, string name)
        {
            if (root.name == name) return root;
            foreach (Transform child in root)
            {
                var found = FindByName(child, name);
                if (found != null) return found;
            }
            return null;
        }

        private static void EnsureSoldierCountControls(Transform card, out Button decrease, out Button increase, out Text count)
        {
            var controls = card.Find("CountControls");
            if (controls == null)
            {
                var value = new GameObject("CountControls", typeof(RectTransform));
                controls = value.transform;
                controls.SetParent(card, false);
            }
            var controlsRect = (RectTransform)controls;
            controlsRect.anchorMin = new Vector2(0.08f, 0.02f);
            controlsRect.anchorMax = new Vector2(0.92f, 0.30f);
            controlsRect.offsetMin = Vector2.zero;
            controlsRect.offsetMax = Vector2.zero;
            decrease = EnsureCountButton(controls, "Decrease", "−", new Vector2(0f, 0f), new Vector2(0.30f, 1f));
            increase = EnsureCountButton(controls, "Increase", "+", new Vector2(0.70f, 0f), new Vector2(1f, 1f));
            var countTransform = controls.Find("Count");
            if (countTransform == null)
            {
                var value = new GameObject("Count", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                countTransform = value.transform;
                countTransform.SetParent(controls, false);
            }
            var rect = (RectTransform)countTransform;
            rect.anchorMin = new Vector2(0.30f, 0f); rect.anchorMax = new Vector2(0.70f, 1f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            count = countTransform.GetComponent<Text>();
            count.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            count.fontSize = 22; count.alignment = TextAnchor.MiddleCenter; count.color = Color.white;
            count.raycastTarget = false;
        }

        private static Button EnsureCountButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax)
        {
            var child = parent.Find(name);
            if (child == null)
            {
                var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                child = value.transform;
                child.SetParent(parent, false);
            }
            var rect = (RectTransform)child;
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = child.GetComponent<Image>(); image.color = new Color(0.16f, 0.14f, 0.12f, 0.92f);
            var button = child.GetComponent<Button>(); button.targetGraphic = image;
            var textTransform = child.Find("Label");
            if (textTransform == null)
            {
                var value = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textTransform = value.transform; textTransform.SetParent(child, false);
            }
            var textRect = (RectTransform)textTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one; textRect.offsetMin = Vector2.zero; textRect.offsetMax = Vector2.zero;
            var text = textTransform.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 24; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.text = label; text.raycastTarget = false;
            return button;
        }

        private static Button EnsureSelectionNavigationButton(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            var child = FindByName(parent, name);
            if (child == null)
            {
                var value = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
                child = value.transform;
                child.SetParent(parent, false);
            }
            var rect = (RectTransform)child;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = new Vector2(52f, 52f);
            var image = child.GetComponent<Image>(); image.color = new Color(0.18f, 0.42f, 0.72f, 1f);
            var button = child.GetComponent<Button>(); button.targetGraphic = image;
            var textTransform = child.Find("Label");
            if (textTransform == null)
            {
                var value = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                textTransform = value.transform;
                textTransform.SetParent(child, false);
            }
            var textRect = (RectTransform)textTransform;
            textRect.anchorMin = Vector2.zero; textRect.anchorMax = Vector2.one;
            textRect.offsetMin = textRect.offsetMax = Vector2.zero;
            var text = textTransform.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 34; text.alignment = TextAnchor.MiddleCenter; text.color = Color.white; text.text = label;
            return button;
        }

        private static Sprite Sprite(string name) =>
            AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}{name}.png")
            ?? throw new InvalidOperationException($"Missing sprite: {name}");

        private static void SetArray<T>(SerializedProperty property, int count, Func<int, T> resolve) where T : UnityEngine.Object
        {
            if (property == null) throw new InvalidOperationException("Missing serialized array property.");
            property.arraySize = count;
            for (var index = 0; index < count; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = resolve(index);
        }
    }
}
#endif
