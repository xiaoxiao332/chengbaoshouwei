using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Resources;
using UnityEngine;

namespace FortressFrontier.Infrastructure.Resources
{
    public sealed class AddressablesResourceSystem : GameSystemBase, IResourceService
    {
        private readonly ResourceCatalog _catalog;
        private readonly Dictionary<ResourceKey, InternalObjectPool> _pools = new();
        private readonly List<IDisposable> _preloadLeases = new();
        private readonly SemaphoreSlim _poolGate = new(1, 1);
        private AddressableHandleStore _handleStore;
        private GameObject _poolRoot;

        public AddressablesResourceSystem(ResourceCatalog catalog)
            : base(SystemLifetime.Global)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _handleStore = new AddressableHandleStore(_catalog);
            _poolRoot = new GameObject("[ResourcePool]");
            _poolRoot.SetActive(false);
            return Task.CompletedTask;
        }

        public Task<IAssetLease<T>> AcquireAsync<T>(ResourceKey key, CancellationToken cancellationToken)
            where T : UnityEngine.Object
        {
            EnsureInitialized();
            return _handleStore.AcquireAsync<T>(key, cancellationToken);
        }

        public async Task<IInstanceLease> SpawnAsync(
            ResourceKey key,
            Transform parent,
            CancellationToken cancellationToken)
        {
            EnsureInitialized();
            await _poolGate.WaitAsync(cancellationToken);
            try
            {
                if (!_pools.TryGetValue(key, out var pool))
                {
                    var prefabLease = await _handleStore.AcquireAsync<GameObject>(key, cancellationToken);
                    try
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (_poolRoot == null) throw new OperationCanceledException(cancellationToken);
                        pool = new InternalObjectPool(key, prefabLease, _poolRoot.transform);
                        _pools.Add(key, pool);
                    }
                    catch
                    {
                        prefabLease.Dispose();
                        throw;
                    }
                }

                cancellationToken.ThrowIfCancellationRequested();
                return pool.Rent(parent);
            }
            finally
            {
                _poolGate.Release();
            }
        }

        public async Task PreloadAsync(
            IReadOnlyCollection<ResourceKey> keys,
            CancellationToken cancellationToken)
        {
            EnsureInitialized();
            var pending = new List<Task<IAssetLease<GameObject>>>(keys.Count);
            try
            {
                foreach (var key in keys)
                {
                    pending.Add(_handleStore.AcquireAsync<GameObject>(key, cancellationToken));
                }

                var acquiredThisCall = await Task.WhenAll(pending);
                _preloadLeases.AddRange(acquiredThisCall);
            }
            catch
            {
                foreach (var task in pending)
                {
                    if (task.Status == TaskStatus.RanToCompletion)
                    {
                        task.Result.Dispose();
                    }
                }

                throw;
            }
        }

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            await _poolGate.WaitAsync(CancellationToken.None);
            try
            {
                foreach (var pool in _pools.Values)
                    pool.Dispose();

                _pools.Clear();
                foreach (var lease in _preloadLeases)
                    lease.Dispose();

                _preloadLeases.Clear();
                _handleStore?.Dispose();
                _handleStore = null;

                if (_poolRoot != null)
                {
                    UnityEngine.Object.Destroy(_poolRoot);
                    _poolRoot = null;
                }
            }
            finally
            {
                _poolGate.Release();
            }
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _handleStore == null)
            {
                throw new InvalidOperationException("AddressablesResourceSystem is not initialized.");
            }
        }
    }
}
