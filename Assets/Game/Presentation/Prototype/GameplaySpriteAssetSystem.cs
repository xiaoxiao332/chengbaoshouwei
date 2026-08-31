using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Resources;
using UnityEngine;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class GameplaySpriteAssetSystem : GameSystemBase, IGameplaySpriteResolver
    {
        private readonly IResourceService _resources;
        private readonly ResourceKey[] _keys;
        private readonly Dictionary<ResourceKey, IAssetLease<Sprite>> _leases = new();

        public GameplaySpriteAssetSystem(IResourceService resources, MatchPresentationConfig presentation)
            : base(SystemLifetime.Scene)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            _keys = presentation.CardArt.Values.Concat(presentation.BuildingArt.Values)
                .Concat(presentation.Units.Values.Select(value => value.Sprite))
                .Append(presentation.MapArt)
                .Where(value => !string.IsNullOrWhiteSpace(value.Value)).Distinct().ToArray();
        }

        public GameplaySpriteAssetSystem(IResourceService resources, MatchPresentationConfig presentation,
            IEnumerable<ResourceKey> additionalKeys)
            : base(SystemLifetime.Scene)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            if (presentation == null) throw new ArgumentNullException(nameof(presentation));
            _keys = presentation.CardArt.Values.Concat(presentation.BuildingArt.Values)
                .Concat(presentation.Units.Values.Select(value => value.Sprite))
                .Append(presentation.MapArt)
                .Concat(additionalKeys ?? Array.Empty<ResourceKey>())
                .Where(value => !string.IsNullOrWhiteSpace(value.Value)).Distinct().ToArray();
        }

        public GameplaySpriteAssetSystem(IResourceService resources, IEnumerable<ResourceKey> keys)
            : base(SystemLifetime.Scene)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _keys = (keys ?? throw new ArgumentNullException(nameof(keys)))
                .Where(value => !string.IsNullOrWhiteSpace(value.Value)).Distinct().ToArray();
        }

        protected override async Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            foreach (var key in _keys)
                _leases.Add(key, await _resources.AcquireAsync<Sprite>(key, cancellationToken));
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            foreach (var lease in _leases.Values) lease.Dispose();
            _leases.Clear();
            return Task.CompletedTask;
        }

        public Sprite Resolve(ResourceKey key)
        {
            if (string.IsNullOrWhiteSpace(key.Value)) return null;
            return _leases.TryGetValue(key, out var lease)
                ? lease.Asset
                : throw new KeyNotFoundException($"Gameplay sprite was not preloaded: '{key}'.");
        }
    }
}
