using System;

namespace FortressFrontier.Core.Identifiers
{
    public readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        public ResourceKey(string value)
        {
            Value = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Resource key cannot be empty.", nameof(value))
                : value;
        }

        public string Value { get; }
        public bool Equals(ResourceKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is ResourceKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value;
    }

    public readonly struct SceneKey : IEquatable<SceneKey>
    {
        public SceneKey(string value)
        {
            Value = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Scene key cannot be empty.", nameof(value))
                : value;
        }

        public string Value { get; }
        public bool Equals(SceneKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is SceneKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value;
    }

    public readonly struct PanelKey : IEquatable<PanelKey>
    {
        public PanelKey(string value)
        {
            Value = string.IsNullOrWhiteSpace(value)
                ? throw new ArgumentException("Panel key cannot be empty.", nameof(value))
                : value;
        }

        public string Value { get; }
        public bool Equals(PanelKey other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is PanelKey other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value;
    }

    public readonly struct CardId : IEquatable<CardId>
    {
        public CardId(string value) => Value = Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(CardId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is CardId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value;
        private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Card id cannot be empty.", name) : value;
    }

    public readonly struct BattlefieldId : IEquatable<BattlefieldId>
    {
        public BattlefieldId(string value) => Value = Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(BattlefieldId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is BattlefieldId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value;
        private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Battlefield id cannot be empty.", name) : value;
    }

    public readonly struct MapModeId : IEquatable<MapModeId>
    {
        public MapModeId(string value) => Value = Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(MapModeId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MapModeId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value;
        private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Map mode id cannot be empty.", name) : value;
    }

    public readonly struct MatchId : IEquatable<MatchId>
    {
        public MatchId(string value) => Value = Require(value, nameof(value));
        public string Value { get; }
        public bool Equals(MatchId other) => string.Equals(Value, other.Value, StringComparison.Ordinal);
        public override bool Equals(object obj) => obj is MatchId other && Equals(other);
        public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value ?? string.Empty);
        public override string ToString() => Value;
        private static string Require(string value, string name) => string.IsNullOrWhiteSpace(value) ? throw new ArgumentException("Match id cannot be empty.", name) : value;
    }
}
