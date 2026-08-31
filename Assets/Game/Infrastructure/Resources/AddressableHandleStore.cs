using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Resources;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace FortressFrontier.Infrastructure.Resources
{
    internal sealed class AddressableHandleStore : IDisposable
    {
        private sealed class Entry
        {
            public AsyncOperationHandle Handle;
            public Task<UnityEngine.Object> LoadTask;
            public int ReferenceCount;
        }

        private readonly ResourceCatalog _catalog;
        private readonly Dictionary<(ResourceKey Key, Type Type), Entry> _entries = new();
        private readonly object _sync = new();
        private bool _disposed;

        public AddressableHandleStore(ResourceCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        public async Task<IAssetLease<T>> AcquireAsync<T>(ResourceKey key, CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            Entry entry;
            var cacheKey = (key, typeof(T));

            lock (_sync)
            {
                ThrowIfDisposed();
                if (!_entries.TryGetValue(cacheKey, out entry))
                {
                    var handle = Addressables.LoadAssetAsync<T>(_catalog.GetRuntimeKey(key));
                    entry = new Entry
                    {
                        Handle = handle,
                        LoadTask = AwaitHandleAsync(handle),
                        ReferenceCount = 0
                    };
                    _entries.Add(cacheKey, entry);
                }

                entry.ReferenceCount++;
            }

            try
            {
                var asset = await TaskCancellation.WaitAsync(entry.LoadTask, cancellationToken);
                if (asset is not T typedAsset)
                {
                    throw new InvalidCastException(
                        $"Addressable '{key}' loaded as {asset?.GetType().FullName ?? "null"}, expected {typeof(T).FullName}.");
                }

                return new AssetLease<T>(key, typedAsset, () => Release(cacheKey));
            }
            catch
            {
                Release(cacheKey);
                throw;
            }
        }

        public void Dispose()
        {
            List<AsyncOperationHandle> handles;
            lock (_sync)
            {
                if (_disposed)
                {
                    return;
                }

                _disposed = true;
                handles = new List<AsyncOperationHandle>(_entries.Count);
                foreach (var entry in _entries.Values)
                {
                    handles.Add(entry.Handle);
                }

                _entries.Clear();
            }

            foreach (var handle in handles)
            {
                if (handle.IsValid())
                {
                    Addressables.Release(handle);
                }
            }
        }

        private static Task<UnityEngine.Object> AwaitHandleAsync<T>(
            AsyncOperationHandle<T> handle)
            where T : UnityEngine.Object
        {
            var completion = new TaskCompletionSource<UnityEngine.Object>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            void Complete(AsyncOperationHandle<T> completed)
            {
                if (completed.Status == AsyncOperationStatus.Succeeded && completed.Result != null)
                {
                    completion.TrySetResult(completed.Result);
                    return;
                }

                completion.TrySetException(
                    completed.OperationException ?? new InvalidOperationException("Addressable asset load failed."));
            }

            handle.Completed += Complete;
            if (handle.IsDone)
            {
                Complete(handle);
            }

            return completion.Task;
        }

        private void Release((ResourceKey Key, Type Type) cacheKey)
        {
            AsyncOperationHandle? handleToRelease = null;

            lock (_sync)
            {
                if (!_entries.TryGetValue(cacheKey, out var entry))
                {
                    return;
                }

                entry.ReferenceCount--;
                if (entry.ReferenceCount <= 0)
                {
                    _entries.Remove(cacheKey);
                    handleToRelease = entry.Handle;
                }
            }

            if (handleToRelease.HasValue && handleToRelease.Value.IsValid())
            {
                Addressables.Release(handleToRelease.Value);
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(AddressableHandleStore));
            }
        }

        private sealed class AssetLease<T> : IAssetLease<T> where T : UnityEngine.Object
        {
            private Action _release;

            public AssetLease(ResourceKey key, T asset, Action release)
            {
                Key = key;
                Asset = asset;
                _release = release;
            }

            public ResourceKey Key { get; }
            public T Asset { get; }

            public void Dispose()
            {
                Interlocked.Exchange(ref _release, null)?.Invoke();
            }
        }
    }
}
