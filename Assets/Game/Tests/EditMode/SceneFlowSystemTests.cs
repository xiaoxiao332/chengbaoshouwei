using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Scenes;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class SceneFlowSystemTests
    {
        [Test]
        public async Task Transition_WhenPendingSceneIsInvalid_PreservesCurrentScene()
        {
            var current = CreateScene(true);
            var currentLease = new TestSceneLease("scene.current", current);
            var invalidLease = new TestSceneLease("scene.invalid", current);
            var service = new QueueSceneService(
                _ => Task.FromResult<ISceneLease>(currentLease),
                _ => Task.FromResult<ISceneLease>(invalidLease));
            var system = CreateSystem(service);

            try
            {
                await system.InitializeAsync(new GameContext("scene-flow-test"), CancellationToken.None);
                await system.TransitionAsync(new SceneKey("scene.current"), CancellationToken.None);
                foreach (var root in current.GetRootGameObjects())
                {
                    if (root.name == "SceneContext" && root.GetComponent<SceneContext>() != null)
                    {
                        UnityEngine.Object.DestroyImmediate(root);
                        break;
                    }
                }

                Assert.ThrowsAsync<InvalidOperationException>(() =>
                    system.TransitionAsync(new SceneKey("scene.invalid"), CancellationToken.None));

                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(current));
                Assert.That(currentLease.DisposeCount, Is.Zero);
                Assert.That(invalidLease.DisposeCount, Is.EqualTo(1));
            }
            finally
            {
                await system.ShutdownAsync(CancellationToken.None);
                CloseIfLoaded(current);
            }
        }

        [Test]
        public async Task Transition_WhenPendingLoadIsCancelled_PreservesCurrentScene()
        {
            var current = CreateScene(true);
            var currentLease = new TestSceneLease("scene.current", current);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var service = new QueueSceneService(
                _ => Task.FromResult<ISceneLease>(currentLease),
                token => Task.FromCanceled<ISceneLease>(token));
            var system = CreateSystem(service);

            try
            {
                await system.InitializeAsync(new GameContext("scene-flow-test"), CancellationToken.None);
                await system.TransitionAsync(new SceneKey("scene.current"), CancellationToken.None);

                Assert.CatchAsync<OperationCanceledException>(() =>
                    system.TransitionAsync(new SceneKey("scene.cancelled"), cancellation.Token));

                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(current));
                Assert.That(currentLease.DisposeCount, Is.Zero);
            }
            finally
            {
                await system.ShutdownAsync(CancellationToken.None);
                CloseIfLoaded(current);
            }
        }

        private static SceneFlowSystem CreateSystem(ISceneService service)
        {
#pragma warning disable SYSLIB0050
            var dependencies = (SceneSystemDependencies)FormatterServices.GetUninitializedObject(
                typeof(SceneSystemDependencies));
#pragma warning restore SYSLIB0050
            return new SceneFlowSystem(service, dependencies);
        }

        private static Scene CreateScene(bool withContext)
        {
            var scene = SceneManager.GetActiveScene();
            if (withContext)
            {
                var root = new GameObject("SceneContext");
                root.AddComponent<SceneContext>();
                SceneManager.MoveGameObjectToScene(root, scene);
            }

            return scene;
        }

        private static void CloseIfLoaded(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.name == "SceneContext" && root.GetComponent<SceneContext>() != null)
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        private sealed class QueueSceneService : ISceneService
        {
            private readonly Queue<Func<CancellationToken, Task<ISceneLease>>> _loads;

            public QueueSceneService(params Func<CancellationToken, Task<ISceneLease>>[] loads)
            {
                _loads = new Queue<Func<CancellationToken, Task<ISceneLease>>>(loads);
            }

            public Task<ISceneLease> LoadAdditiveAsync(SceneKey key, CancellationToken cancellationToken)
            {
                return _loads.Dequeue()(cancellationToken);
            }
        }

        private sealed class TestSceneLease : ISceneLease
        {
            public TestSceneLease(string key, Scene scene)
            {
                Key = new SceneKey(key);
                Scene = scene;
            }

            public SceneKey Key { get; }
            public Scene Scene { get; }
            public int DisposeCount { get; private set; }

            public ValueTask DisposeAsync()
            {
                DisposeCount++;
                return default;
            }
        }
    }
}
