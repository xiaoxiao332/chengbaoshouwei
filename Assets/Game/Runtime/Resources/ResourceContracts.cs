using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using UnityEngine;

namespace FortressFrontier.Runtime.Resources
{
    public interface IAssetLease<out T> : IDisposable where T : UnityEngine.Object
    {
        ResourceKey Key { get; }
        T Asset { get; }
    }

    public interface IInstanceLease : IDisposable
    {
        ResourceKey Key { get; }
        GameObject Instance { get; }
    }

    public interface IResourceService
    {
        Task<IAssetLease<T>> AcquireAsync<T>(ResourceKey key, CancellationToken cancellationToken)
            where T : UnityEngine.Object;

        Task<IInstanceLease> SpawnAsync(
            ResourceKey key,
            Transform parent,
            CancellationToken cancellationToken);

        Task PreloadAsync(
            IReadOnlyCollection<ResourceKey> keys,
            CancellationToken cancellationToken);
    }

    public interface IPoolable
    {
        void OnRent();
        void OnReturn();
    }
}
