using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Resources;
using UnityEngine;

namespace FortressFrontier.Infrastructure.Resources
{
    internal sealed class InternalObjectPool : IDisposable
    {
        private readonly ResourceKey _key;
        private readonly IAssetLease<GameObject> _prefabLease;
        private readonly Transform _inactiveRoot;
        private readonly Stack<GameObject> _available = new();
        private readonly HashSet<GameObject> _instances = new();
        private bool _disposed;

        public InternalObjectPool(
            ResourceKey key,
            IAssetLease<GameObject> prefabLease,
            Transform inactiveRoot)
        {
            _key = key;
            _prefabLease = prefabLease ?? throw new ArgumentNullException(nameof(prefabLease));
            _inactiveRoot = inactiveRoot;
        }

        public IInstanceLease Rent(Transform parent)
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(InternalObjectPool));
            }

            GameObject instance;
            do
            {
                instance = _available.Count > 0 ? _available.Pop() : null;
            }
            while (instance == null && _available.Count > 0);

            if (instance == null)
            {
                instance = UnityEngine.Object.Instantiate(_prefabLease.Asset);
                _instances.Add(instance);
            }

            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            instance.SetActive(true);

            foreach (var poolable in instance.GetComponentsInChildren<MonoBehaviour>(true).OfType<IPoolable>())
            {
                poolable.OnRent();
            }

            return new InstanceLease(_key, instance, Return);
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            foreach (var instance in _instances)
            {
                if (instance != null)
                {
                    UnityEngine.Object.Destroy(instance);
                }
            }

            _instances.Clear();
            _available.Clear();
            _prefabLease.Dispose();
        }

        private void Return(GameObject instance)
        {
            if (instance == null || !_instances.Contains(instance))
            {
                return;
            }

            if (_disposed)
            {
                UnityEngine.Object.Destroy(instance);
                return;
            }

            foreach (var poolable in instance.GetComponentsInChildren<MonoBehaviour>(true).OfType<IPoolable>())
            {
                poolable.OnReturn();
            }

            instance.SetActive(false);
            instance.transform.SetParent(_inactiveRoot, false);
            _available.Push(instance);
        }

        private sealed class InstanceLease : IInstanceLease
        {
            private Action<GameObject> _return;

            public InstanceLease(ResourceKey key, GameObject instance, Action<GameObject> returnAction)
            {
                Key = key;
                Instance = instance;
                _return = returnAction;
            }

            public ResourceKey Key { get; }
            public GameObject Instance { get; }

            public void Dispose()
            {
                Interlocked.Exchange(ref _return, null)?.Invoke(Instance);
            }
        }
    }
}
