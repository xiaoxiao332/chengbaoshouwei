using FortressFrontier.Presentation.Prototype;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Tests
{
    public sealed class UnitAnimationPlayModeTests
    {
        private static readonly string[] AnimatedPrefabPaths =
        {
            "Assets/Game/Content/Prefabs/World/world_gatherer_player.prefab",
            "Assets/Game/Content/Prefabs/World/world_gatherer_enemy.prefab",
            "Assets/Game/Content/Prefabs/World/world_unit_shield_player.prefab",
            "Assets/Game/Content/Prefabs/World/world_unit_shield_enemy.prefab",
            "Assets/Game/Content/Prefabs/World/world_unit_archer_player.prefab",
            "Assets/Game/Content/Prefabs/World/world_unit_archer_enemy.prefab",
            "Assets/Game/Content/Prefabs/World/world_unit_ram_player.prefab",
            "Assets/Game/Content/Prefabs/World/world_unit_ram_enemy.prefab"
        };

        private static readonly string[] CannonPrefabPaths =
        {
            "Assets/Game/Content/Prefabs/World/world_unit_cannon_player.prefab",
            "Assets/Game/Content/Prefabs/World/world_unit_cannon_enemy.prefab"
        };

        [Test]
        public void AnimatedPrefabs_HaveIndependentVisualPivotAndStableLabel()
        {
            foreach (var path in AnimatedPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero, path);

                var pivot = prefab.transform.Find("VisualPivot") as RectTransform;
                var label = prefab.transform.Find("Label") as RectTransform;
                var view = prefab.GetComponent<GameplayWorldEntityView>();
                Assert.That(view, Is.Not.Null, path);
                Assert.That(pivot, Is.Not.Null, path);
                Assert.That(label, Is.Not.Null, path);
                Assert.That(pivot.parent, Is.EqualTo(prefab.transform), path);
                Assert.That(label.parent, Is.EqualTo(prefab.transform), path);
                Assert.That(pivot.GetComponent<Image>()?.sprite, Is.Not.Null, path);

                var serializedView = new SerializedObject(view);
                Assert.That(serializedView.FindProperty("_visualPivot").objectReferenceValue, Is.EqualTo(pivot), path);
                Assert.That(serializedView.FindProperty("_icon").objectReferenceValue, Is.EqualTo(pivot.GetComponent<Image>()), path);
                Assert.That(serializedView.FindProperty("_label").objectReferenceValue, Is.EqualTo(label.GetComponent<Text>()), path);
            }
        }

        [Test]
        public void ProceduralAnimation_FlipsOnlyVisualAndResetsAfterPooling()
        {
            var root = new GameObject("AnimatedUnit", typeof(RectTransform), typeof(GameplayWorldEntityView));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                rootRect.sizeDelta = new Vector2(100f, 100f);

                var pivotObject = new GameObject("VisualPivot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pivotObject.transform.SetParent(root.transform, false);
                var pivot = pivotObject.GetComponent<RectTransform>();
                pivot.sizeDelta = rootRect.sizeDelta;
                var icon = pivotObject.GetComponent<Image>();

                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(root.transform, false);
                var label = labelObject.GetComponent<Text>();

                var view = root.GetComponent<GameplayWorldEntityView>();
                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("_visualPivot").objectReferenceValue = pivot;
                serializedView.FindProperty("_icon").objectReferenceValue = icon;
                serializedView.FindProperty("_label").objectReferenceValue = label;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                view.OnRent();
                view.Present(100, 200, "HP", 0.75f, Color.white, WorldEntityMotionState.Moving, -1, true, true, true, true);
                view.TickVisual(0.05f, false);

                Assert.That(view.FacingDirection, Is.EqualTo(-1));
                Assert.That(pivot.localScale.x, Is.LessThan(0f));
                Assert.That(label.rectTransform.localScale.x, Is.EqualTo(1f));
                Assert.That(rootRect.localScale.x, Is.GreaterThan(0f));
                Assert.That(Mathf.Abs(pivot.localEulerAngles.z), Is.GreaterThan(0.1f));
                Assert.That(icon.color.a, Is.LessThan(1f));
                Assert.That(pivot.anchoredPosition.y, Is.GreaterThan(0f));

                var frozenScale = pivot.localScale;
                var frozenRotation = pivot.localRotation;
                var frozenColor = icon.color;
                var frozenPosition = rootRect.anchoredPosition;
                view.TickVisual(0.2f, true);
                Assert.That(pivot.localScale, Is.EqualTo(frozenScale));
                Assert.That(pivot.localRotation, Is.EqualTo(frozenRotation));
                Assert.That(icon.color, Is.EqualTo(frozenColor));
                Assert.That(rootRect.anchoredPosition, Is.EqualTo(frozenPosition));

                view.TickVisual(0.2f, false);
                Assert.That(icon.color, Is.EqualTo(Color.white));
                view.Present(100, 200, "HP", 1f, Color.white, WorldEntityMotionState.Gathering, -1, false, false, false, true);
                view.TickVisual(0.04f, false);
                Assert.That(Mathf.Abs(pivot.localEulerAngles.z), Is.GreaterThan(0.1f));

                view.OnReturn();
                Assert.That(root.activeSelf, Is.False);
                view.OnRent();
                Assert.That(root.activeSelf, Is.True);
                Assert.That(view.FacingDirection, Is.EqualTo(1));
                Assert.That(view.MotionState, Is.EqualTo(WorldEntityMotionState.Static));
                Assert.That(rootRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(rootRect.localScale, Is.EqualTo(Vector3.one));
                Assert.That(pivot.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(pivot.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(pivot.localScale, Is.EqualTo(Vector3.one));
                Assert.That(icon.color, Is.EqualTo(Color.white));
                Assert.That(label.text, Is.Empty);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void IdleBreathing_UsesSlowerTwoPointFourSecondCycle()
        {
            var root = new GameObject("BreathingUnit", typeof(RectTransform), typeof(GameplayWorldEntityView));
            try
            {
                var pivotObject = new GameObject("VisualPivot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                pivotObject.transform.SetParent(root.transform, false);
                var pivot = pivotObject.GetComponent<RectTransform>();
                var view = root.GetComponent<GameplayWorldEntityView>();
                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("_visualPivot").objectReferenceValue = pivot;
                serializedView.FindProperty("_icon").objectReferenceValue = pivotObject.GetComponent<Image>();
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                view.OnRent();
                view.Present(0, 0, string.Empty, 1f, Color.white, WorldEntityMotionState.Idle,
                    1, false, false, false, false);
                view.TickVisual(0.6f, false);
                Assert.That(pivot.localScale.y, Is.EqualTo(1.01f).Within(0.0001f));
                view.TickVisual(0.6f, false);
                Assert.That(pivot.localScale.y, Is.EqualTo(1f).Within(0.0001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ProjectileVisual_FollowsRenderedDirectionAndResetsAfterPooling()
        {
            var root = new GameObject("Projectile", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(GameplayWorldEntityView));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                var view = root.GetComponent<GameplayWorldEntityView>();
                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("_visualPivot").objectReferenceValue = rootRect;
                serializedView.FindProperty("_icon").objectReferenceValue = root.GetComponent<Image>();
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                view.OnRent();
                view.Present(0, 0, string.Empty, 0.72f, Color.white, WorldEntityMotionState.Projectile,
                    1, false, false, false, true, 45f);
                Assert.That(Mathf.DeltaAngle(45f, rootRect.localEulerAngles.z), Is.EqualTo(0f).Within(0.01f));

                view.Present(100, 100, string.Empty, 0.72f, Color.white, WorldEntityMotionState.Projectile,
                    1, false, false, false, true, 45f);
                view.TickVisual(0.1f, false);
                Assert.That(Mathf.DeltaAngle(45f, rootRect.localEulerAngles.z), Is.EqualTo(0f).Within(0.1f));

                view.Present(200, 50, string.Empty, 0.72f, Color.white, WorldEntityMotionState.Projectile,
                    1, false, false, false, true, -20f);
                view.TickVisual(0.1f, false);
                Assert.That(Mathf.DeltaAngle(0f, rootRect.localEulerAngles.z), Is.LessThan(0f));

                var frozenPosition = rootRect.anchoredPosition;
                var frozenRotation = rootRect.localRotation;
                view.Present(300, 0, string.Empty, 0.72f, Color.white, WorldEntityMotionState.Projectile,
                    1, false, false, false, true, 90f);
                view.TickVisual(0.2f, true);
                Assert.That(rootRect.anchoredPosition, Is.EqualTo(frozenPosition));
                Assert.That(rootRect.localRotation, Is.EqualTo(frozenRotation));

                view.OnReturn();
                view.OnRent();
                Assert.That(rootRect.anchoredPosition, Is.EqualTo(Vector2.zero));
                Assert.That(rootRect.localRotation, Is.EqualTo(Quaternion.identity));
                Assert.That(rootRect.localScale, Is.EqualTo(Vector3.one));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CannonPrefabs_ResolveFacingMirroredProjectileOrigin()
        {
            foreach (var path in CannonPrefabPaths)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(prefab, Is.Not.Null, path);
                Assert.That(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(prefab), Is.Zero, path);

                var instance = Object.Instantiate(prefab);
                var effectsParentObject = new GameObject("Effects", typeof(RectTransform));
                try
                {
                    var root = instance.GetComponent<RectTransform>();
                    var point = instance.transform.Find("point") as RectTransform;
                    var view = instance.GetComponent<GameplayWorldEntityView>();
                    var effectsParent = effectsParentObject.GetComponent<RectTransform>();
                    instance.transform.SetParent(effectsParent, false);

                    Assert.That(point, Is.Not.Null, path);
                    Assert.That(view, Is.Not.Null, path);
                    var facing = path.Contains("_player") ? 1 : -1;
                    root.anchoredPosition = new Vector2(320f, 180f);
                    view.OnRent();
                    view.Present(320, 180, string.Empty, 0.75f, Color.white, WorldEntityMotionState.Idle,
                        facing, false, false, true, false);

                    Assert.That(view.TryGetProjectileOrigin(effectsParent, out var origin), Is.True, path);
                    Assert.That(origin.x, Is.EqualTo(320f + point.anchoredPosition.x * 0.75f * facing).Within(0.01f), path);
                    Assert.That(origin.y, Is.EqualTo(180f + point.anchoredPosition.y * 0.75f).Within(0.01f), path);
                }
                finally
                {
                    Object.DestroyImmediate(instance);
                    Object.DestroyImmediate(effectsParentObject);
                }
            }
        }

        [Test]
        public void ProjectileVisual_StartsAtProvidedOriginBeforeSmoothing()
        {
            var root = new GameObject("Projectile", typeof(RectTransform), typeof(GameplayWorldEntityView));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                var view = root.GetComponent<GameplayWorldEntityView>();
                view.OnRent();
                view.Present(200, 100, string.Empty, 1f, Color.white, WorldEntityMotionState.Projectile,
                    1, false, false, false, true, 0f, new Vector2(40f, 60f));

                Assert.That(rootRect.anchoredPosition, Is.EqualTo(new Vector2(40f, 60f)));
                view.TickVisual(0.1f, false);
                Assert.That(rootRect.anchoredPosition.x, Is.GreaterThan(40f));
                Assert.That(rootRect.anchoredPosition.x, Is.LessThan(200f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

[Test]
        public void StaticRootVisual_PreservesPresentedWorldPositionAndScale()
        {
            var root = new GameObject("StaticResource", typeof(RectTransform), typeof(CanvasRenderer),
                typeof(Image), typeof(GameplayWorldEntityView));
            try
            {
                var rootRect = root.GetComponent<RectTransform>();
                var icon = root.GetComponent<Image>();
                var view = root.GetComponent<GameplayWorldEntityView>();
                var serializedView = new SerializedObject(view);
                serializedView.FindProperty("_visualPivot").objectReferenceValue = rootRect;
                serializedView.FindProperty("_icon").objectReferenceValue = icon;
                serializedView.ApplyModifiedPropertiesWithoutUndo();

                view.OnRent();
                view.Present(790, 597, string.Empty, 0.8f, Color.white,
                    WorldEntityMotionState.Static, 1, false, false, false, false);
                view.TickVisual(0.1f, false);

                Assert.That(rootRect.anchoredPosition, Is.EqualTo(new Vector2(790f, 597f)));
                Assert.That(rootRect.localScale, Is.EqualTo(Vector3.one * 0.8f));
                Assert.That(rootRect.localRotation, Is.EqualTo(Quaternion.identity));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

    }
}
