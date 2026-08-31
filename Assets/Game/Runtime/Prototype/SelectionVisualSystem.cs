using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Flow;
using FortressFrontier.Runtime.UI;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Prototype
{
    public sealed class SelectionVisualSystem : GameSystemBase
    {
        public static readonly PanelKey PanelId = new("ui.selection");
        private readonly IPanelService _panels;
        private readonly SelectionPrototypeProvider _provider;
        private readonly IGameplaySpriteResolver _sprites;
        private ISelectionView _view;

        public SelectionVisualSystem(IPanelService panels, IApplicationFlow applicationFlow, IProgressionReader progression,
            IProgressionCommands progressionCommands, ISelectionContent selectionContent,
            IGameplaySpriteResolver sprites, ISettingsOverlay settingsOverlay = null) : base(SystemLifetime.Scene)
        {
            _panels = panels;
            _provider = new SelectionPrototypeProvider(applicationFlow, progression, progressionCommands,
                selectionContent, settingsOverlay);
            _sprites = sprites;
        }

        protected override async Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _view = await _panels.OpenViewAsync<ISelectionView>(PanelId, null, cancellationToken);
            _provider.Changed += OnChanged;
            _view.Bind(_provider, _sprites, _provider.Snapshot);
        }

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _provider.Changed -= OnChanged;
            _view = null;
            await _panels.CloseAsync(PanelId, cancellationToken);
        }

        private void OnChanged(SelectionViewModel viewModel) => _view?.Render(viewModel);
    }
}
