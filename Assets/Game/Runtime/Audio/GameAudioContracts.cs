using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;

namespace FortressFrontier.Runtime.Audio
{
    public enum GameAudioCue
    {
        UnitHit,
        GatherComplete
    }

    public readonly struct AudioVolumeSettings
    {
        public AudioVolumeSettings(int masterVolumePercent, int musicVolumePercent,
            int sfxVolumePercent, bool muted)
        {
            MasterVolumePercent = Math.Clamp(masterVolumePercent, 0, 100);
            MusicVolumePercent = Math.Clamp(musicVolumePercent, 0, 100);
            SfxVolumePercent = Math.Clamp(sfxVolumePercent, 0, 100);
            Muted = muted;
        }

        public int MasterVolumePercent { get; }
        public int MusicVolumePercent { get; }
        public int SfxVolumePercent { get; }
        public bool Muted { get; }
        public float EffectiveMasterVolume => Muted ? 0f : MasterVolumePercent / 100f;
        public float MusicGain => MusicVolumePercent / 100f;
        public float SfxGain => SfxVolumePercent / 100f;
    }

    public interface IAudioPlaybackService
    {
        Task SetMusicAsync(ResourceKey key, float crossFadeSeconds, CancellationToken cancellationToken);
        void RequestSfx(GameAudioCue cue, float normalizedPan);
        void ApplyVolumes(AudioVolumeSettings settings);
    }

    public static class GameAudioKeys
    {
        public static readonly ResourceKey Boot = new("audio.bgm.boot");
        public static readonly ResourceKey Selection = new("audio.bgm.selection");
        public static readonly ResourceKey PrologueDevelopment = new("audio.bgm.prologue.development");
        public static readonly ResourceKey PrologueContest = new("audio.bgm.prologue.contest");
        public static readonly ResourceKey PrologueDecisive = new("audio.bgm.prologue.decisive");
        public static readonly ResourceKey RiverPassDevelopment = new("audio.bgm.river-pass.development");
        public static readonly ResourceKey RiverPassContest = new("audio.bgm.river-pass.contest");
        public static readonly ResourceKey RiverPassDecisive = new("audio.bgm.river-pass.decisive");
        public static readonly ResourceKey StoneGolemBoss = new("audio.bgm.boss.stone-golem");
        public static readonly ResourceKey Victory = new("audio.bgm.result.victory");
        public static readonly ResourceKey Defeat = new("audio.bgm.result.defeat");
        public static readonly ResourceKey UnitHit = new("audio.sfx.unit-hit");
        public static readonly ResourceKey GatherComplete = new("audio.sfx.gather-complete");
    }
}
