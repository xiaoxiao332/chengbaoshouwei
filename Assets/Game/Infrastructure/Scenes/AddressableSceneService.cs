using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Scenes;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;

namespace FortressFrontier.Infrastructure.Scenes
{
    public sealed class AddressableSceneService : ISceneService
    {
        private readonly Resources.ResourceCatalog _catalog;

        public AddressableSceneService(Resources.ResourceCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public async Task<ISceneLease> LoadAdditiveAsync(SceneKey key, CancellationToken cancellationToken)
        {
            var handle = Addressables.LoadSceneAsync(
                _catalog.GetRuntimeKey(key),
                LoadSceneMode.Additive,
                true);

            try
            {
                var sceneInstance = await WaitUntilLoadedAsync(handle, cancellationToken);
                if (!sceneInstance.Scene.IsValid() || !sceneInstance.Scene.isLoaded)
                {
                    throw handle.OperationException ?? new InvalidOperationException($"Failed to load scene '{key}'.");
                }

                return new SceneLease(key, handle);
            }
            catch (OperationCanceledException)
            {
                if (handle.IsDone)
                {
                    if (handle.Status == AsyncOperationStatus.Succeeded)
                    {
                        await Addressables.UnloadSceneAsync(handle, true).Task;
                    }
                    else if (handle.IsValid())
                    {
                        Addressables.Release(handle);
                    }
                }
                else
                {
                    handle.Completed += completed =>
                    {
                        if (completed.Status == AsyncOperationStatus.Succeeded)
                        {
                            Addressables.UnloadSceneAsync(completed, true);
                        }
                        else if (completed.IsValid())
                        {
                            Addressables.Release(completed);
                        }
                    };
                }

                throw;
            }
            catch
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }

                throw;
            }
        }

        private static async Task<SceneInstance> WaitUntilLoadedAsync(
            AsyncOperationHandle<SceneInstance> handle,
            CancellationToken cancellationToken)
        {
            var completion = new TaskCompletionSource<SceneInstance>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void CompleteLoad(AsyncOperationHandle<SceneInstance> completed)
            {
                if (completed.Status != AsyncOperationStatus.Succeeded || !completed.Result.Scene.IsValid())
                {
                    completion.TrySetException(
                        completed.OperationException ?? new InvalidOperationException("Addressable scene load failed."));
                    return;
                }

                completion.TrySetResult(completed.Result);
            }

            handle.Completed += CompleteLoad;
            if (handle.IsDone)
            {
                CompleteLoad(handle);
            }

            return await Resources.TaskCancellation.WaitAsync(completion.Task, cancellationToken);
        }

        private sealed class SceneLease : ISceneLease
        {
            private AsyncOperationHandle<SceneInstance>? _handle;

            public SceneLease(SceneKey key, AsyncOperationHandle<SceneInstance> handle)
            {
                Key = key;
                _handle = handle;
            }

            public SceneKey Key { get; }
            public Scene Scene => _handle?.Result.Scene ?? default;

            public async ValueTask DisposeAsync()
            {
                var handle = _handle;
                _handle = null;
                if (handle.HasValue && handle.Value.IsValid())
                {
                    await Addressables.UnloadSceneAsync(handle.Value, true).Task;
                }
            }
        }
    }
}
