using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Audio;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Gameplay;
using UnityEngine;

namespace FortressFrontier.Presentation.Audio
{
    public sealed class GameplayAudioSystem : GameSystemBase
    {
        public const float MusicCrossFadeSeconds = 0.75f;
        private readonly IAudioPlaybackService _audio;
        private readonly MatchConfigSnapshot _config;
        private readonly MatchPhaseSystem _phases;
        private readonly BossSystem _boss;
        private readonly CombatSystem _combat;
        private readonly GathererSystem _playerGatherers;
        private readonly GathererSystem _enemyGatherers;
        private CancellationTokenSource _lifetime;
        private GameplayMusicState _musicState;

        public GameplayAudioSystem(IAudioPlaybackService audio, MatchConfigSnapshot config,
            MatchPhaseSystem phases, BossSystem boss, CombatSystem combat,
            GathererSystem playerGatherers, GathererSystem enemyGatherers) : base(SystemLifetime.Scene)
        {
            _audio = audio ?? throw new ArgumentNullException(nameof(audio));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _phases = phases ?? throw new ArgumentNullException(nameof(phases));
            _boss = boss ?? throw new ArgumentNullException(nameof(boss));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _playerGatherers = playerGatherers ?? throw new ArgumentNullException(nameof(playerGatherers));
            _enemyGatherers = enemyGatherers ?? throw new ArgumentNullException(nameof(enemyGatherers));
        }

        protected override async Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _musicState = new GameplayMusicState(_config.BattlefieldId, _phases.CurrentPhaseId);
            _phases.PhaseChanged += OnPhaseChanged;
            _boss.Changed += OnBossChanged;
            _combat.MatchEnded += OnMatchEnded;
            _combat.UnitHit += OnUnitHit;
            _playerGatherers.HarvestCompleted += OnHarvestCompleted;
            _enemyGatherers.HarvestCompleted += OnHarvestCompleted;
            await _audio.SetMusicAsync(_musicState.CurrentKey,
                MusicCrossFadeSeconds, _lifetime.Token);
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _phases.PhaseChanged -= OnPhaseChanged;
            _boss.Changed -= OnBossChanged;
            _combat.MatchEnded -= OnMatchEnded;
            _combat.UnitHit -= OnUnitHit;
            _playerGatherers.HarvestCompleted -= OnHarvestCompleted;
            _enemyGatherers.HarvestCompleted -= OnHarvestCompleted;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _musicState = null;
            return Task.CompletedTask;
        }

        public static ResourceKey ResolvePhaseMusic(BattlefieldId battlefieldId, MatchPhaseId phaseId)
            => GameplayMusicState.ResolvePhaseMusic(battlefieldId, phaseId);

        private void OnPhaseChanged(MatchPhaseId phaseId)
        {
            if (_musicState.SetPhase(phaseId)) ChangeMusic(_musicState.CurrentKey);
        }

        private void OnBossChanged()
        {
            var active = _boss.GetSnapshot().Any(value => value.State == BossRuntimeState.Active);
            if (_musicState.SetBossActive(active)) ChangeMusic(_musicState.CurrentKey);
        }

        private void OnMatchEnded(bool playerVictory)
        {
            if (_musicState.SetMatchResult(playerVictory)) ChangeMusic(_musicState.CurrentKey);
        }

        private void OnUnitHit(UnitHitAudioEvent audioEvent) =>
            _audio.RequestSfx(GameAudioCue.UnitHit, ResolvePan(audioEvent.X));

        private void OnHarvestCompleted(GatherCompleteAudioEvent audioEvent) =>
            _audio.RequestSfx(GameAudioCue.GatherComplete, ResolvePan(audioEvent.X));

        private float ResolvePan(int x)
        {
            var width = Math.Max(1, _config.BattlefieldLayout.ReferenceWidth);
            return Mathf.Clamp(((x / (float)width) * 2f - 1f) * 0.35f, -0.35f, 0.35f);
        }

        private async void ChangeMusic(ResourceKey key)
        {
            if (_lifetime == null || _lifetime.IsCancellationRequested) return;
            try
            {
                await _audio.SetMusicAsync(key, MusicCrossFadeSeconds, _lifetime.Token);
            }
            catch (OperationCanceledException) when (_lifetime == null || _lifetime.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }
}
