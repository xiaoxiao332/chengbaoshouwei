using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Runtime.Settings;

namespace FortressFrontier.Runtime.Prototype
{
    public interface ISettingsOverlay
    {
        Task OpenSettingsAsync(CancellationToken cancellationToken);
    }

    public interface ISettingsViewCommands
    {
        Task ApplyAndCloseAsync(int masterVolumePercent, int musicVolumePercent, int sfxVolumePercent,
            bool muted, CancellationToken cancellationToken);
        Task CancelAsync(CancellationToken cancellationToken);
    }

    public interface ISettingsView
    {
        void Bind(ISettingsViewCommands commands, ApplicationSettingsSnapshot snapshot);
        void ShowSaveError();
    }

    public interface IBootMenuCommands
    {
        Task StartGameAsync(CancellationToken cancellationToken);
        Task OpenSettingsAsync(CancellationToken cancellationToken);
    }
}
