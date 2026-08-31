using System;
using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Core.Identifiers;
using UnityEngine;

namespace FortressFrontier.Presentation.UI
{
    [CreateAssetMenu(menuName = "Fortress Frontier/UI/Panel Catalog", fileName = "PanelCatalog")]
    public sealed class PanelCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class PanelDefinition
        {
            [SerializeField] private string _id;
            [SerializeField] private string _resourceId;
            [SerializeField] private UIPanelLayer _layer;
            [SerializeField] private bool _cacheWhenClosed = true;

            public PanelKey Id => new(_id);
            public ResourceKey ResourceKey => new(_resourceId);
            public UIPanelLayer Layer => _layer;
            public bool CacheWhenClosed => _cacheWhenClosed;
        }

        [Serializable]
        public sealed class UIStateDefinition
        {
            [SerializeField] private UIStateId _state;
            [SerializeField] private string _backgroundPanelId;
            [SerializeField] private string _windowPanelId;
            [SerializeField] private List<string> _overlayPanelIds = new();

            public UIStateId State => _state;
            public string BackgroundPanelId => _backgroundPanelId;
            public string WindowPanelId => _windowPanelId;
            public IReadOnlyList<string> OverlayPanelIds => _overlayPanelIds;
        }

        [SerializeField] private List<PanelDefinition> _panels = new();
        [SerializeField] private List<UIStateDefinition> _states = new();

        public PanelDefinition GetPanel(PanelKey id)
        {
            var definition = _panels.FirstOrDefault(panel => panel != null && panel.Id.Equals(id));
            return definition ?? throw new KeyNotFoundException($"Panel is not configured: '{id}'.");
        }

        public UIStateDefinition GetState(UIStateId state)
        {
            var definition = _states.FirstOrDefault(item => item != null && item.State == state);
            return definition ?? throw new KeyNotFoundException($"UI state is not configured: '{state}'.");
        }
    }
}
