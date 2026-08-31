using System;

namespace FortressFrontier.Core.Systems
{
    public sealed class GameContext
    {
        public GameContext(string gameVersion)
        {
            GameVersion = string.IsNullOrWhiteSpace(gameVersion) ? "0.0.0" : gameVersion;
        }

        public string GameVersion { get; }
        public DateTimeOffset StartedAtUtc { get; } = DateTimeOffset.UtcNow;
    }
}
