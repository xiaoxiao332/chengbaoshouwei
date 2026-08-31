using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Settings;
using FortressFrontier.Runtime.UI;

namespace FortressFrontier.Runtime.Prototype
{
    public sealed class SettingsVisualSystem : GameSystemBase, ISettingsOverlay, ISettingsViewCommands
    {
        public static readonly PanelKey PanelId = new("ui.settings");
        private readonly IPanelService _panels;
        private readonly IApplicationSettingsReader _reader;
        private readonly IApplicationSettingsCommands _commands;
        private ISettingsView _view;

        public SettingsVisualSystem(IPanelService panels, IApplicationSettingsReader reader,
            IApplicationSettingsCommands commands) : base(SystemLifetime.Global)
        {
            _panels = panels ?? throw new ArgumentNullException(nameof(panels));
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _view = null;
            await _panels.CloseAsync(PanelId, cancellationToken);
        }

        public async Task OpenSettingsAsync(CancellationToken cancellationToken)
        {
            _view = await _panels.OpenViewAsync<ISettingsView>(PanelId, null, cancellationToken);
            _view.Bind(this, _reader.GetSnapshot());
        }

        public async Task ApplyAndCloseAsync(int masterVolumePercent, int musicVolumePercent,
            int sfxVolumePercent, bool muted,
            CancellationToken cancellationToken)
        {
            if (!await _commands.ApplyAsync(masterVolumePercent, musicVolumePercent, sfxVolumePercent,
                    muted, cancellationToken))
            {
                _view?.ShowSaveError();
                return;
            }

            _view = null;
            await _panels.CloseAsync(PanelId, cancellationToken);
        }

        public async Task CancelAsync(CancellationToken cancellationToken)
        {
            _view = null;
            await _panels.CloseAsync(PanelId, cancellationToken);
        }
    }
}
