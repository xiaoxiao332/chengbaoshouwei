using FortressFrontier.Runtime.Gameplay;

namespace FortressFrontier.Runtime.Audio
{
    public readonly struct UnitHitAudioEvent
    {
        public UnitHitAudioEvent(MatchFaction faction, int x, int y, bool killed)
        {
            Faction = faction;
            X = x;
            Y = y;
            Killed = killed;
        }

        public MatchFaction Faction { get; }
        public int X { get; }
        public int Y { get; }
        public bool Killed { get; }
    }

    public readonly struct GatherCompleteAudioEvent
    {
        public GatherCompleteAudioEvent(MatchFaction faction, int x, int y, int amount)
        {
            Faction = faction;
            X = x;
            Y = y;
            Amount = amount;
        }

        public MatchFaction Faction { get; }
        public int X { get; }
        public int Y { get; }
        public int Amount { get; }
    }
}
