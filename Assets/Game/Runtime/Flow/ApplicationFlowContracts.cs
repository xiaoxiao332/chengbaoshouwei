using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Flow
{
    public readonly struct MatchLaunchRequest : IEquatable<MatchLaunchRequest>
    {
        public MatchLaunchRequest(BattlefieldId battlefieldId, MapModeId mapModeId, MatchId matchId)
            : this(battlefieldId, mapModeId, matchId, 1)
        {
        }

        public MatchLaunchRequest(BattlefieldId battlefieldId, MapModeId mapModeId, MatchId matchId, int seed)
        {
            BattlefieldId = battlefieldId;
            MapModeId = mapModeId;
            MatchId = matchId;
            Seed = seed == 0 ? 1 : seed;
        }

        public BattlefieldId BattlefieldId { get; }
        public MapModeId MapModeId { get; }
        public MatchId MatchId { get; }
        public int Seed { get; }
        public bool Equals(MatchLaunchRequest other) => BattlefieldId.Equals(other.BattlefieldId) && MapModeId.Equals(other.MapModeId) && MatchId.Equals(other.MatchId) && Seed == other.Seed;
        public override bool Equals(object obj) => obj is MatchLaunchRequest other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(BattlefieldId, MapModeId, MatchId, Seed);
    }

    public interface IApplicationFlow
    {
        MatchLaunchRequest? CurrentMatch { get; }
        Task StartMatchAsync(MatchLaunchRequest request, CancellationToken cancellationToken);
        Task ReturnToSelectionAsync(CancellationToken cancellationToken);
    }

    public interface IMatchSessionContext
    {
        MatchConfigSnapshot CurrentMatchSnapshot { get; }
    }
}
