using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Audio;
using FortressFrontier.Runtime.Resources;
using UnityEngine;

namespace FortressFrontier.Infrastructure.Audio
{
    public sealed class AudioPlaybackSystem : GameSystemBase, IGameTickable, IAudioPlaybackService
    {
        private const int MusicSourceCount = 2;
        private const int HitSourceCount = 4;
        private const int GatherSourceCount = 2;
        private static readonly float[] PitchSequence = { 0.97f, 1.03f, 0.99f, 1.01f, 1f };

        private readonly IResourceService _resources;
        private readonly Transform _owner;
        private GameObject _root;
        private AudioSource[] _musicSources;
        private AudioSource[] _hitSources;
        private AudioSource[] _gatherSources;
        private IAssetLease<AudioClip> _hitLease;
        private IAssetLease<AudioClip> _gatherLease;
        private IAssetLease<AudioClip> _currentMusicLease;
        private IAssetLease<AudioClip> _transitionMusicLease;
        private ResourceKey _currentMusicKey;
        private AudioVolumeSettings _volumes = new(100, 70, 80, false);
        private int _activeMusicIndex;
        private float _crossFadeDuration;
        private float _crossFadeElapsed;
        private bool _crossFading;
        private int _musicRequestVersion;
        private readonly SfxAdmissionBudget _hitBudget = new(HitSourceCount, 6f, 3f, 3);
        private readonly SfxAdmissionBudget _gatherBudget = new(GatherSourceCount, 3f, 2f, 2);
        private int _pitchIndex;

        public AudioPlaybackSystem(IResourceService resources, Transform owner) : base(SystemLifetime.Global)
        {
            _resources = resources ?? throw new ArgumentNullException(nameof(resources));
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
        }

        public int MusicSourceCapacity => MusicSourceCount;
        public int HitSourceCapacity => HitSourceCount;
        public int GatherSourceCapacity => GatherSourceCount;
        public int ActiveHitCount => _hitSources?.Count(value => value.isPlaying) ?? 0;
        public int ActiveGatherCount => _gatherSources?.Count(value => value.isPlaying) ?? 0;

        protected override async Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _root = new GameObject("[AudioPlayback]");
            _root.transform.SetParent(_owner, false);
            _musicSources = CreateSources("Music", MusicSourceCount, true);
            _hitSources = CreateSources("UnitHit", HitSourceCount, false);
            _gatherSources = CreateSources("GatherComplete", GatherSourceCount, false);

            try
            {
                var hitTask = _resources.AcquireAsync<AudioClip>(GameAudioKeys.UnitHit, cancellationToken);
                var gatherTask = _resources.AcquireAsync<AudioClip>(GameAudioKeys.GatherComplete, cancellationToken);
                await Task.WhenAll(hitTask, gatherTask);
                _hitLease = await hitTask;
                _gatherLease = await gatherTask;
                AssignClip(_hitSources, _hitLease.Asset);
                AssignClip(_gatherSources, _gatherLease.Asset);
                ApplyVolumes(_volumes);
            }
            catch
            {
                DestroyRoot();
                throw;
            }
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _musicRequestVersion);
            _currentMusicLease?.Dispose();
            _transitionMusicLease?.Dispose();
            _hitLease?.Dispose();
            _gatherLease?.Dispose();
            _currentMusicLease = null;
            _transitionMusicLease = null;
            _hitLease = null;
            _gatherLease = null;
            DestroyRoot();
            return Task.CompletedTask;
        }

        public async Task SetMusicAsync(ResourceKey key, float crossFadeSeconds,
            CancellationToken cancellationToken)
        {
            if (!IsInitialized) throw new InvalidOperationException("AudioPlaybackSystem is not initialized.");
            if (_currentMusicLease != null && _currentMusicKey.Equals(key) && !_crossFading) return;

            var requestVersion = Interlocked.Increment(ref _musicRequestVersion);
            IAssetLease<AudioClip> loaded = null;
            try
            {
                loaded = await _resources.AcquireAsync<AudioClip>(key, cancellationToken);
                if (requestVersion != Volatile.Read(ref _musicRequestVersion) || cancellationToken.IsCancellationRequested)
                {
                    loaded.Dispose();
                    return;
                }

                BeginMusicTransition(key, loaded, Math.Max(0f, crossFadeSeconds));
                loaded = null;
            }
            catch (OperationCanceledException)
            {
                loaded?.Dispose();
                throw;
            }
            catch (Exception exception)
            {
                loaded?.Dispose();
                Debug.LogError($"Unable to load music '{key}'; current music will continue. {exception}", _root);
            }
        }

        public void RequestSfx(GameAudioCue cue, float normalizedPan)
        {
            if (!IsInitialized) return;
            switch (cue)
            {
                case GameAudioCue.UnitHit:
                    TryPlay(_hitSources, _hitBudget, 0.55f, normalizedPan);
                    break;
                case GameAudioCue.GatherComplete:
                    TryPlay(_gatherSources, _gatherBudget, 1f, normalizedPan);
                    break;
            }
        }

        public void ApplyVolumes(AudioVolumeSettings settings)
        {
            _volumes = settings;
            AudioListener.volume = settings.EffectiveMasterVolume;
            UpdateMusicVolumes();
            UpdateSfxVolumes(_hitSources, 0.55f);
            UpdateSfxVolumes(_gatherSources, 1f);
        }

        public void Tick(float deltaTime)
        {
            _hitBudget.Tick(deltaTime);
            _gatherBudget.Tick(deltaTime);
            if (_crossFading)
            {
                _crossFadeElapsed += Math.Max(0f, deltaTime);
                if (_crossFadeElapsed >= _crossFadeDuration) CompleteMusicTransition();
                else UpdateMusicVolumes();
            }
            UpdateSfxVolumes(_hitSources, 0.55f);
            UpdateSfxVolumes(_gatherSources, 1f);
        }

        private AudioSource[] CreateSources(string prefix, int count, bool loop)
        {
            var sources = new AudioSource[count];
            for (var index = 0; index < count; index++)
            {
                var child = new GameObject($"{prefix}-{index + 1}");
                child.transform.SetParent(_root.transform, false);
                var source = child.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = loop;
                source.spatialBlend = 0f;
                sources[index] = source;
            }
            return sources;
        }

        private void BeginMusicTransition(ResourceKey key, IAssetLease<AudioClip> lease, float duration)
        {
            if (_crossFading) CompleteMusicTransition();
            var nextIndex = _currentMusicLease == null ? _activeMusicIndex : 1 - _activeMusicIndex;
            var next = _musicSources[nextIndex];
            next.Stop();
            next.clip = lease.Asset;
            next.volume = 0f;
            next.Play();

            _transitionMusicLease = lease;
            _currentMusicKey = key;
            if (_currentMusicLease == null || duration <= 0f)
            {
                _musicSources[_activeMusicIndex].Stop();
                _currentMusicLease?.Dispose();
                _currentMusicLease = _transitionMusicLease;
                _transitionMusicLease = null;
                _activeMusicIndex = nextIndex;
                _crossFading = false;
                UpdateMusicVolumes();
                return;
            }

            _crossFadeDuration = duration;
            _crossFadeElapsed = 0f;
            _crossFading = true;
            UpdateMusicVolumes();
        }

        private void CompleteMusicTransition()
        {
            if (!_crossFading) return;
            var oldIndex = _activeMusicIndex;
            _activeMusicIndex = 1 - _activeMusicIndex;
            _musicSources[oldIndex].Stop();
            _musicSources[oldIndex].clip = null;
            _currentMusicLease?.Dispose();
            _currentMusicLease = _transitionMusicLease;
            _transitionMusicLease = null;
            _crossFading = false;
            UpdateMusicVolumes();
        }

        private void UpdateMusicVolumes()
        {
            if (_musicSources == null) return;
            var gain = _volumes.MusicGain;
            if (!_crossFading)
            {
                for (var index = 0; index < _musicSources.Length; index++)
                    _musicSources[index].volume = index == _activeMusicIndex ? gain : 0f;
                return;
            }

            var t = Mathf.Clamp01(_crossFadeElapsed / Math.Max(0.001f, _crossFadeDuration));
            _musicSources[_activeMusicIndex].volume = gain * (1f - t);
            _musicSources[1 - _activeMusicIndex].volume = gain * t;
        }

        private void TryPlay(AudioSource[] sources, SfxAdmissionBudget budget,
            float baseVolume, float normalizedPan)
        {
            if (sources == null) return;
            var source = sources.FirstOrDefault(value => !value.isPlaying);
            var activeCount = sources.Count(value => value.isPlaying);
            if (source == null || source.clip == null || !budget.TryAdmit(activeCount, Time.frameCount)) return;
            source.panStereo = Mathf.Clamp(normalizedPan, -0.35f, 0.35f);
            source.pitch = PitchSequence[_pitchIndex++ % PitchSequence.Length];
            source.Play();
            UpdateSfxVolumes(sources, baseVolume);
        }

        private void UpdateSfxVolumes(AudioSource[] sources, float baseVolume)
        {
            if (sources == null) return;
            var active = Math.Max(1, sources.Count(value => value.isPlaying));
            var gain = baseVolume * _volumes.SfxGain / Mathf.Sqrt(active);
            foreach (var source in sources) source.volume = gain;
        }

        private static void AssignClip(AudioSource[] sources, AudioClip clip)
        {
            foreach (var source in sources) source.clip = clip;
        }

        private void DestroyRoot()
        {
            if (_root != null) UnityEngine.Object.Destroy(_root);
            _root = null;
            _musicSources = null;
            _hitSources = null;
            _gatherSources = null;
        }
    }
}
