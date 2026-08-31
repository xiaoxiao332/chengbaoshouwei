#if UNITY_EDITOR
using System;
using FortressFrontier.Bootstrap;
using FortressFrontier.Runtime.Scenes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    internal static class P1WorldAuthoring
    {
        private const string GameplayScene = "Assets/Game/Scenes/Gameplay.unity";
        private const string GameplayPrefab = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";

        [MenuItem("Fortress Frontier/P1/Build World Context")]
        public static void Build()
        {
            var scene = EditorSceneManager.OpenScene(GameplayScene, OpenSceneMode.Single);
            var root = FindOrCreate(null, "P1WorldContext");
            var context = root.GetComponent<GameplayWorldContext>() ?? root.AddComponent<GameplayWorldContext>();

            var anchors = FindOrCreate(root.transform, "Anchors").transform;
            var playerWall = Anchor(anchors, "PlayerWall", 470, 540);
            var enemyWall = Anchor(anchors, "EnemyWall", 1872, 540);
            var playerGate = Anchor(anchors, "PlayerGate", 470, 540);
            var enemyGate = Anchor(anchors, "EnemyGate", 1872, 540);
            var playerDeployment = Anchor(anchors, "PlayerDeployment", 684, 540);
            var enemyDeployment = Anchor(anchors, "EnemyDeployment", 1665, 540);
            var upper = Anchor(anchors, "RouteUpper", 960, 270);
            var middle = Anchor(anchors, "RouteMiddle", 960, 540);
            var lower = Anchor(anchors, "RouteLower", 960, 810);
            var tower = Anchor(anchors, "TowerBuildArea", 1171, 540);
            var forbidden = Anchor(anchors, "TowerForbiddenEnemyWall", 1843, 540);

            var resources = new Transform[9];
            var resourceRoot = FindOrCreate(anchors, "ResourcePoints").transform;
            for (var i = 0; i < resources.Length; i++)
            {
                var groupX = i < 3 ? 680 : i < 6 ? 1120 : 1560;
                resources[i] = Anchor(resourceRoot, $"ResourcePoint{i + 1:00}", groupX, 270 + i % 3 * 270);
            }
            var bosses = new[] { Anchor(anchors, "BossPoint01", 960, 370), Anchor(anchors, "BossPoint02", 960, 710) };

            var runtimeRoot = FindOrCreate(root.transform, "RuntimeRoots").transform;
            var units = FindOrCreate(runtimeRoot, "WorldUnits").transform;
            var construction = FindOrCreate(runtimeRoot, "WorldConstruction").transform;
            var effects = FindOrCreate(runtimeRoot, "WorldEffects").transform;
            var unitsOverlay = Overlay(units, "WorldUnitsOverlay", 50);
            var constructionOverlay = Overlay(construction, "WorldConstructionOverlay", 40);
            var effectsOverlay = Overlay(effects, "WorldEffectsOverlay", 60);
            var shells = FindOrCreate(root.transform, "P1PresentationShells");
            FindOrCreate(shells.transform, "Combat"); FindOrCreate(shells.transform, "Construction");
            FindOrCreate(shells.transform, "Research"); FindOrCreate(shells.transform, "Boss");
            shells.SetActive(false);

            var so = new SerializedObject(context);
            Ref(so, "_playerGate", playerGate); Ref(so, "_enemyGate", enemyGate); Ref(so, "_playerWall", playerWall); Ref(so, "_enemyWall", enemyWall);
            Ref(so, "_playerDeployment", playerDeployment); Ref(so, "_enemyDeployment", enemyDeployment);
            Ref(so, "_upperRoute", upper); Ref(so, "_middleRoute", middle); Ref(so, "_lowerRoute", lower);
            Refs(so.FindProperty("_resourcePoints"), resources); Refs(so.FindProperty("_bossPoints"), bosses);
            Ref(so, "_towerBuildArea", tower); Refs(so.FindProperty("_towerForbiddenAreas"), new[] { forbidden });
            Ref(so, "_worldUnitsRoot", units); Ref(so, "_worldConstructionRoot", construction); Ref(so, "_worldEffectsRoot", effects);
            Ref(so, "_worldUnitsOverlay", unitsOverlay); Ref(so, "_worldConstructionOverlay", constructionOverlay); Ref(so, "_worldEffectsOverlay", effectsOverlay);
            so.ApplyModifiedPropertiesWithoutUndo();

            var installer = UnityEngine.Object.FindFirstObjectByType<GameplayInstaller>(FindObjectsInactive.Include)
                ?? throw new InvalidOperationException("Gameplay scene is missing GameplayInstaller.");
            var installerObject = new SerializedObject(installer);
            installerObject.FindProperty("_worldContext").objectReferenceValue = context;
            installerObject.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            BuildPrefabShells();
            AssetDatabase.SaveAssets();
            Debug.Log("P1 World Context scene anchors and disabled prefab shells were built.");
        }

        private static void BuildPrefabShells()
        {
            var root = PrefabUtility.LoadPrefabContents(GameplayPrefab);
            try
            {
                var shell = FindOrCreate(root.transform, "P1BaselineShell");
                FindOrCreate(shell.transform, "SoldierTab"); FindOrCreate(shell.transform, "ItemTab");
                FindOrCreate(shell.transform, "TimedOfferShell"); FindOrCreate(shell.transform, "ResearchShell");
                FindOrCreate(shell.transform, "BossRewardShell"); FindOrCreate(shell.transform, "ConstructionShell");
                shell.SetActive(false);
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static Transform Anchor(Transform parent, string name, int referenceX, int referenceY)
        {
            var value = FindOrCreate(parent, name).transform;
            value.localPosition = new Vector3((referenceX - 960) / 100f, (referenceY - 540) / 100f, 0f);
            value.localRotation = Quaternion.identity; value.localScale = Vector3.one;
            return value;
        }

        private static GameObject FindOrCreate(Transform parent, string name)
        {
            Transform existing = null;
            if (parent != null) existing = parent.Find(name);
            else foreach (var candidate in SceneManager.GetActiveScene().GetRootGameObjects()) if (candidate.name == name) { existing = candidate.transform; break; }
            if (existing != null) return existing.gameObject;
            var value = new GameObject(name); if (parent != null) value.transform.SetParent(parent, false); return value;
        }

        private static RectTransform Overlay(Transform parent, string name, int sortingOrder)
        {
            var existing = parent.Find(name);
            var value = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            value.transform.SetParent(parent, false);
            var canvas = value.GetComponent<Canvas>() ?? value.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
            var scaler = value.GetComponent<CanvasScaler>() ?? value.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            return value.GetComponent<RectTransform>();
        }

        private static void Ref(SerializedObject so, string name, UnityEngine.Object value) => so.FindProperty(name).objectReferenceValue = value;
        private static void Refs(SerializedProperty property, Transform[] values) { property.arraySize = values.Length; for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; }
    }
}
#endif
