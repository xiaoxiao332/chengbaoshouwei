using FortressFrontier.Core.Identifiers;

namespace FortressFrontier.Runtime.Audio
{
    public sealed class GameplayMusicState
    {
        private readonly BattlefieldId _battlefieldId;
        private MatchPhaseId _phaseId;
        private bool _bossActive;
        private bool _matchEnded;
        private bool _playerVictory;

        public GameplayMusicState(BattlefieldId battlefieldId, MatchPhaseId phaseId)
        {
            _battlefieldId = battlefieldId;
            _phaseId = phaseId;
        }

        public ResourceKey CurrentKey => _matchEnded
            ? _playerVictory ? GameAudioKeys.Victory : GameAudioKeys.Defeat
            : _bossActive ? GameAudioKeys.StoneGolemBoss : ResolvePhaseMusic(_battlefieldId, _phaseId);

        public bool SetPhase(MatchPhaseId phaseId)
        {
            var previous = CurrentKey;
            _phaseId = phaseId;
            return !previous.Equals(CurrentKey);
        }

        public bool SetBossActive(bool active)
        {
            var previous = CurrentKey;
            _bossActive = active;
            return !previous.Equals(CurrentKey);
        }

        public bool SetMatchResult(bool playerVictory)
        {
            var previous = CurrentKey;
            _matchEnded = true;
            _playerVictory = playerVictory;
            return !previous.Equals(CurrentKey);
        }

        public static ResourceKey ResolvePhaseMusic(BattlefieldId battlefieldId, MatchPhaseId phaseId)
        {
            var riverPass = battlefieldId.Value == "battlefield.river-pass";
            return phaseId.Value switch
            {
                "phase.contest" => riverPass ? GameAudioKeys.RiverPassContest : GameAudioKeys.PrologueContest,
                "phase.decisive" => riverPass ? GameAudioKeys.RiverPassDecisive : GameAudioKeys.PrologueDecisive,
                _ => riverPass ? GameAudioKeys.RiverPassDevelopment : GameAudioKeys.PrologueDevelopment
            };
        }
    }
}
