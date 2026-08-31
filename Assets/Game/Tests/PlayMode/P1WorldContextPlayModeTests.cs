using System.Collections;
using FortressFrontier.Runtime.Scenes;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace FortressFrontier.Tests
{
    public sealed class P1WorldContextPlayModeTests
    {
        [UnityTest]
        public IEnumerator GameplayScene_ContextReferencesAreCompleteAndLifecycleIsIdempotent()
        {
            var operation = EditorSceneManager.LoadSceneAsyncInPlayMode("Assets/Game/Scenes/Gameplay.unity", new LoadSceneParameters(LoadSceneMode.Single));
            yield return operation;
            var context = Object.FindFirstObjectByType<GameplayWorldContext>(FindObjectsInactive.Include);
            Assert.That(context, Is.Not.Null);
            Assert.That(context.TryValidate(out var reason), Is.True, reason);
            Assert.That(context.WorldUnitsOverlay, Is.Not.Null);
            Assert.That(context.WorldConstructionOverlay, Is.Not.Null);
            Assert.That(context.WorldEffectsOverlay, Is.Not.Null);
            context.Initialize(); context.Initialize(); Assert.That(context.IsInitialized, Is.True);
            context.Shutdown(); context.Shutdown(); Assert.That(context.IsInitialized, Is.False);
        }

        [Test]
        public void WorldPrefabs_HaveNoMissingScripts()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Game/Content/Prefabs/World" }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                Assert.That(CountMissing(prefab), Is.Zero, path);
            }
        }

        [Test]
        public void GameplayPrefab_P1ShellIsDisabledAndHasNoMissingScripts()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Content/Prefabs/UI/Gameplay.prefab");
            Assert.That(prefab, Is.Not.Null);
            var shell = prefab.transform.Find("P1BaselineShell");
            Assert.That(shell, Is.Not.Null);
            Assert.That(shell.gameObject.activeSelf, Is.False);
            Assert.That(CountMissing(prefab), Is.Zero);
        }

        private static int CountMissing(GameObject root)
        {
            var count = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root);
            foreach (Transform child in root.transform) count += CountMissing(child.gameObject);
            return count;
        }
    }
}
