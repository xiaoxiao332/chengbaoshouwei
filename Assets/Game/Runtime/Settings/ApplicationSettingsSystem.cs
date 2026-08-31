using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Saving;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Audio;

namespace FortressFrontier.Runtime.Settings
{
    [Serializable]
    public sealed class ApplicationSettingsSaveData
    {
        public int Version = 3;
        public int MasterVolumePercent = 100;
        public int MusicVolumePercent = 70;
        public int SfxVolumePercent = 80;
        public bool Muted;
        public bool RewardedAdConsentGranted;
    }

    public readonly struct ApplicationSettingsSnapshot
    {
        public ApplicationSettingsSnapshot(int masterVolumePercent, int musicVolumePercent,
            int sfxVolumePercent, bool muted, bool rewardedAdConsentGranted = false)
        {
            MasterVolumePercent = Math.Clamp(masterVolumePercent, 0, 100);
            MusicVolumePercent = Math.Clamp(musicVolumePercent, 0, 100);
            SfxVolumePercent = Math.Clamp(sfxVolumePercent, 0, 100);
            Muted = muted;
            RewardedAdConsentGranted = rewardedAdConsentGranted;
        }

        public int MasterVolumePercent { get; }
        public int MusicVolumePercent { get; }
        public int SfxVolumePercent { get; }
        public bool Muted { get; }
        public bool RewardedAdConsentGranted { get; }
        public float EffectiveVolume => Muted ? 0f : MasterVolumePercent / 100f;
        public AudioVolumeSettings ToAudioVolumeSettings() => new(
            MasterVolumePercent, MusicVolumePercent, SfxVolumePercent, Muted);
    }

    public interface IApplicationSettingsReader
    {
        ApplicationSettingsSnapshot GetSnapshot();
    }

    public interface IApplicationSettingsCommands
    {
        Task<bool> ApplyAsync(int masterVolumePercent, int musicVolumePercent, int sfxVolumePercent,
            bool muted, CancellationToken cancellationToken);
        Task<bool> SetRewardedAdConsentAsync(bool granted, CancellationToken cancellationToken);
    }

    public sealed class ApplicationSettingsSystem : GameSystemBase, ISaveParticipant,
        IApplicationSettingsReader, IApplicationSettingsCommands
    {
        private readonly Func<CancellationToken, Task> _persistSettingsAsync;
        private readonly Action<AudioVolumeSettings> _applyVolume;
        private readonly SemaphoreSlim _transactionGate = new(1, 1);
        private ApplicationSettingsSaveData _state;

        public ApplicationSettingsSystem(Func<CancellationToken, Task> persistSettingsAsync,
            Action<AudioVolumeSettings> applyVolume)
            : base(SystemLifetime.Global)
        {
            _persistSettingsAsync = persistSettingsAsync ?? throw new ArgumentNullException(nameof(persistSettingsAsync));
            _applyVolume = applyVolume ?? throw new ArgumentNullException(nameof(applyVolume));
        }

        public SaveFileKind FileKind => SaveFileKind.Settings;
        public string SectionKey => "application-settings";
        public int SectionVersion => 3;
        public Type StateType => typeof(ApplicationSettingsSaveData);

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _state ??= CreateDefault();
            Normalize(_state, SectionVersion);
            ApplyVolume();
            return Task.CompletedTask;
        }

        public ApplicationSettingsSnapshot GetSnapshot()
        {
            EnsureInitialized();
            return new ApplicationSettingsSnapshot(_state.MasterVolumePercent, _state.MusicVolumePercent,
                _state.SfxVolumePercent, _state.Muted, _state.RewardedAdConsentGranted);
        }

        public async Task<bool> ApplyAsync(int masterVolumePercent, int musicVolumePercent, int sfxVolumePercent,
            bool muted, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            await _transactionGate.WaitAsync(cancellationToken);
            try
            {
                var previous = Clone(_state);
                _state.MasterVolumePercent = Math.Clamp(masterVolumePercent, 0, 100);
                _state.MusicVolumePercent = Math.Clamp(musicVolumePercent, 0, 100);
                _state.SfxVolumePercent = Math.Clamp(sfxVolumePercent, 0, 100);
                _state.Muted = muted;
                ApplyVolume();
                try
                {
                    await _persistSettingsAsync(cancellationToken);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    _state = previous;
                    ApplyVolume();
                    throw;
                }
                catch
                {
                    _state = previous;
                    ApplyVolume();
                    return false;
                }
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        public async Task<bool> SetRewardedAdConsentAsync(bool granted, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            await _transactionGate.WaitAsync(cancellationToken);
            try
            {
                if (_state.RewardedAdConsentGranted == granted) return true;
                var previous = Clone(_state);
                _state.RewardedAdConsentGranted = granted;
                try
                {
                    await _persistSettingsAsync(cancellationToken);
                    return true;
                }
                catch (OperationCanceledException)
                {
                    _state = previous;
                    throw;
                }
                catch
                {
                    _state = previous;
                    return false;
                }
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        public object CaptureState()
        {
            EnsureInitialized();
            return Clone(_state);
        }

        public object CreateDefaultState() => CreateDefault();

        public void RestoreState(object state, int storedVersion)
        {
            _state = state as ApplicationSettingsSaveData ?? CreateDefault();
            Normalize(_state, storedVersion);
            if (IsInitialized) ApplyVolume();
        }

        private void ApplyVolume() => _applyVolume(new AudioVolumeSettings(
            _state.MasterVolumePercent, _state.MusicVolumePercent, _state.SfxVolumePercent, _state.Muted));

        private static ApplicationSettingsSaveData CreateDefault() => new();

        private static ApplicationSettingsSaveData Clone(ApplicationSettingsSaveData source) => new()
        {
            Version = 3,
            MasterVolumePercent = source.MasterVolumePercent,
            MusicVolumePercent = source.MusicVolumePercent,
            SfxVolumePercent = source.SfxVolumePercent,
            Muted = source.Muted,
            RewardedAdConsentGranted = source.RewardedAdConsentGranted
        };

        private static void Normalize(ApplicationSettingsSaveData state, int storedVersion)
        {
            if (storedVersion < 3)
            {
                state.MusicVolumePercent = 70;
                state.SfxVolumePercent = 80;
            }
            state.Version = 3;
            state.MasterVolumePercent = Math.Clamp(state.MasterVolumePercent, 0, 100);
            state.MusicVolumePercent = Math.Clamp(state.MusicVolumePercent, 0, 100);
            state.SfxVolumePercent = Math.Clamp(state.SfxVolumePercent, 0, 100);
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _state == null)
                throw new InvalidOperationException("ApplicationSettingsSystem is not initialized.");
        }
    }
}
