using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.StateMachine;
using FortressFrontier.Core.Systems;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Resources;
using FortressFrontier.Runtime.UI;
using UnityEngine;

namespace FortressFrontier.Presentation.UI
{
    public sealed class UIManager : GameSystemBase, IPanelService
    {
        private sealed class PanelRuntime
        {
            public PanelCatalog.PanelDefinition Definition;
            public IInstanceLease Lease;
            public UIPanelBase Panel;
        }

        private sealed class CompositionState : IAsyncState<UIStateId>
        {
            private readonly UIManager _owner;

            public CompositionState(UIManager owner, UIStateId id)
            {
                _owner = owner;
                Id = id;
            }

            public UIStateId Id { get; }
            public Task EnterAsync(UIStateId? previousState, CancellationToken cancellationToken) =>
                _owner.ApplyStateAsync(Id, cancellationToken);

            public Task ExitAsync(UIStateId nextState, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private readonly IResourceService _resourceService;
        private readonly PanelCatalog _catalog;
        private readonly UIRootView _rootView;
        private readonly Dictionary<PanelKey, PanelRuntime> _loaded = new();
        private readonly List<PanelKey> _popupStack = new();
        private readonly HashSet<PanelKey> _openOverlays = new();
        private readonly SemaphoreSlim _panelGate = new(1, 1);
        private readonly AsyncStateMachine<UIStateId> _stateMachine = new();
        private PanelKey? _background;
        private PanelKey? _window;

        public UIManager(IResourceService resourceService, PanelCatalog catalog, UIRootView rootView)
            : base(SystemLifetime.Global)
        {
            _resourceService = resourceService ?? throw new ArgumentNullException(nameof(resourceService));
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            _rootView = rootView ?? throw new ArgumentNullException(nameof(rootView));

            foreach (UIStateId state in Enum.GetValues(typeof(UIStateId)))
            {
                _stateMachine.Register(new CompositionState(this, state));
            }
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task TransitionAsync(UIStateId state, CancellationToken cancellationToken) =>
            _stateMachine.TransitionAsync(state, cancellationToken);

        public async Task<TPanel> OpenAsync<TPanel>(
            PanelKey id,
            object arguments,
            CancellationToken cancellationToken)
            where TPanel : UIPanelBase
        {
            await _panelGate.WaitAsync(cancellationToken);
            try
            {
                var runtime = await OpenInternalAsync(id, arguments, cancellationToken);
                return runtime.Panel as TPanel
                    ?? throw new InvalidCastException($"Panel '{id}' is not {typeof(TPanel).FullName}.");
            }
            finally
            {
                _panelGate.Release();
            }
        }

        async Task<TView> IPanelService.OpenViewAsync<TView>(
            PanelKey id,
            object arguments,
            CancellationToken cancellationToken)
            where TView : class
        {
            await _panelGate.WaitAsync(cancellationToken);
            try
            {
                var runtime = await OpenInternalAsync(id, arguments, cancellationToken);
                return runtime.Panel as TView
                    ?? throw new InvalidCastException($"Panel '{id}' does not implement {typeof(TView).FullName}.");
            }
            finally
            {
                _panelGate.Release();
            }
        }

        public async Task CloseAsync(PanelKey id, CancellationToken cancellationToken)
        {
            await _panelGate.WaitAsync(cancellationToken);
            try
            {
                await CloseInternalAsync(id, cancellationToken);
            }
            finally
            {
                _panelGate.Release();
            }
        }

        public async Task CloseTopPopupAsync(CancellationToken cancellationToken)
        {
            await _panelGate.WaitAsync(cancellationToken);
            try
            {
                if (_popupStack.Count > 0)
                {
                    await CloseInternalAsync(_popupStack[^1], cancellationToken);
                }
            }
            finally
            {
                _panelGate.Release();
            }
        }

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            await _panelGate.WaitAsync(cancellationToken);
            try
            {
                foreach (var runtime in _loaded.Values.ToArray())
                {
                    if (runtime.Panel != null)
                    {
                        await runtime.Panel.CloseAsync(cancellationToken);
                    }
                    runtime.Lease.Dispose();
                }

                _loaded.Clear();
                _popupStack.Clear();
                _openOverlays.Clear();
                _background = null;
                _window = null;
            }
            finally
            {
                _panelGate.Release();
            }
        }

        private async Task ApplyStateAsync(UIStateId state, CancellationToken cancellationToken)
        {
            await _panelGate.WaitAsync(cancellationToken);
            try
            {
                foreach (var popup in _popupStack.ToArray())
                {
                    await CloseInternalAsync(popup, cancellationToken);
                }

                if (_window.HasValue)
                {
                    await CloseInternalAsync(_window.Value, cancellationToken);
                }

                if (_background.HasValue)
                {
                    await CloseInternalAsync(_background.Value, cancellationToken);
                }

                var definition = _catalog.GetState(state);
                if (!string.IsNullOrWhiteSpace(definition.BackgroundPanelId))
                {
                    await OpenInternalAsync(new PanelKey(definition.BackgroundPanelId), null, cancellationToken);
                }

                if (!string.IsNullOrWhiteSpace(definition.WindowPanelId))
                {
                    await OpenInternalAsync(new PanelKey(definition.WindowPanelId), null, cancellationToken);
                }

                foreach (var overlayId in definition.OverlayPanelIds.Where(id => !string.IsNullOrWhiteSpace(id)))
                {
                    await OpenInternalAsync(new PanelKey(overlayId), null, cancellationToken);
                }
            }
            finally
            {
                _panelGate.Release();
            }
        }

        private async Task<PanelRuntime> OpenInternalAsync(
            PanelKey id,
            object arguments,
            CancellationToken cancellationToken)
        {
            if (_loaded.TryGetValue(id, out var staleRuntime) && staleRuntime.Panel == null)
            {
                staleRuntime.Lease.Dispose();
                _loaded.Remove(id);
            }

            if (!_loaded.TryGetValue(id, out var runtime))
            {
                var definition = _catalog.GetPanel(id);
                var lease = await _resourceService.SpawnAsync(
                    definition.ResourceKey,
                    _rootView.GetLayerRoot(definition.Layer),
                    cancellationToken);

                if (!lease.Instance.TryGetComponent<UIPanelBase>(out var panel))
                {
                    lease.Dispose();
                    throw new InvalidOperationException($"Panel prefab '{id}' has no UIPanelBase component.");
                }

                StretchToParent(lease.Instance.transform as RectTransform, _rootView.GetLayerRoot(definition.Layer));
                runtime = new PanelRuntime { Definition = definition, Lease = lease, Panel = panel };
                _loaded.Add(id, runtime);
            }

            else if (runtime.Panel.IsOpen && arguments == null)
            {
                return runtime;
            }

            await EnforceLayerPolicyBeforeOpenAsync(id, runtime.Definition.Layer, cancellationToken);
            await runtime.Panel.OpenAsync(arguments, cancellationToken);

            switch (runtime.Definition.Layer)
            {
                case UIPanelLayer.Bg:
                    _background = id;
                    break;
                case UIPanelLayer.Window:
                    _window = id;
                    break;
                case UIPanelLayer.Pop:
                    _popupStack.Add(id);
                    break;
                case UIPanelLayer.Over:
                    _openOverlays.Add(id);
                    break;
            }

            return runtime;
        }

        private async Task EnforceLayerPolicyBeforeOpenAsync(
            PanelKey id,
            UIPanelLayer layer,
            CancellationToken cancellationToken)
        {
            if (layer == UIPanelLayer.Bg && _background.HasValue && !_background.Value.Equals(id))
            {
                await CloseInternalAsync(_background.Value, cancellationToken);
            }
            else if (layer == UIPanelLayer.Window && _window.HasValue && !_window.Value.Equals(id))
            {
                await CloseInternalAsync(_window.Value, cancellationToken);
            }
            else if (layer == UIPanelLayer.Pop && _popupStack.Count > 0)
            {
                _loaded[_popupStack[^1]].Panel.SetInputEnabled(false);
            }
        }

        private async Task CloseInternalAsync(PanelKey id, CancellationToken cancellationToken)
        {
            if (!_loaded.TryGetValue(id, out var runtime))
            {
                return;
            }

            if (runtime.Panel == null)
            {
                runtime.Lease.Dispose();
                _loaded.Remove(id);
                _popupStack.Remove(id);
                _openOverlays.Remove(id);
                if (_background.HasValue && _background.Value.Equals(id)) _background = null;
                if (_window.HasValue && _window.Value.Equals(id)) _window = null;
                return;
            }

            await runtime.Panel.CloseAsync(cancellationToken);
            _popupStack.Remove(id);
            _openOverlays.Remove(id);
            if (_background.HasValue && _background.Value.Equals(id)) _background = null;
            if (_window.HasValue && _window.Value.Equals(id)) _window = null;

            if (_popupStack.Count > 0)
            {
                _loaded[_popupStack[^1]].Panel.SetInputEnabled(true);
            }

            if (!runtime.Definition.CacheWhenClosed)
            {
                runtime.Lease.Dispose();
                _loaded.Remove(id);
            }
        }

        private static void StretchToParent(RectTransform panel, RectTransform parent)
        {
            if (panel == null)
            {
                throw new InvalidOperationException("Panel prefab root must use RectTransform.");
            }

            panel.SetParent(parent, false);
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.anchoredPosition = Vector2.zero;
            panel.localScale = Vector3.one;
        }

        async Task IPanelService.OpenAsync(PanelKey id, object arguments, CancellationToken cancellationToken)
        {
            await _panelGate.WaitAsync(cancellationToken);
            try
            {
                await OpenInternalAsync(id, arguments, cancellationToken);
            }
            finally
            {
                _panelGate.Release();
            }
        }
    }
}
