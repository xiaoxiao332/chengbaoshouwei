using System;
using System.Collections.Generic;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Gameplay;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class GameplayWorldPresentationProfile
    {
        private static readonly IReadOnlyDictionary<string, ResourceKey> ResourceNodes =
            new Dictionary<string, ResourceKey>(StringComparer.Ordinal)
            {
                ["resource.food"] = new("world.resource.food"),
                ["resource.wood"] = new("world.resource.wood"),
                ["resource.raw-stone"] = new("world.resource.raw-stone"),
                ["resource.iron-ore"] = new("world.resource.iron-ore")
            };

        private readonly MatchPresentationConfig _presentation;

        public GameplayWorldPresentationProfile(MatchPresentationConfig presentation) =>
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

        public static IReadOnlyList<ResourceKey> RequiredKeys { get; } = new[]
        {
            new ResourceKey("world.resource.food"), new ResourceKey("world.resource.wood"),
            new ResourceKey("world.resource.raw-stone"), new ResourceKey("world.resource.iron-ore"),
            new ResourceKey("world.gatherer.player"), new ResourceKey("world.gatherer.enemy"),
            new ResourceKey("world.worker.food.player"), new ResourceKey("world.worker.food.enemy"),
            new ResourceKey("world.worker.wood.player"), new ResourceKey("world.worker.wood.enemy"),
            new ResourceKey("world.worker.stone.player"), new ResourceKey("world.worker.stone.enemy"),
            new ResourceKey("world.worker.iron.player"), new ResourceKey("world.worker.iron.enemy"),
            new ResourceKey("world.builder.player"), new ResourceKey("world.builder.enemy"),
            new ResourceKey("world.tower-site.player"), new ResourceKey("world.tower-site.enemy"),
            new ResourceKey("world.tower.player"), new ResourceKey("world.tower.enemy"),
            new ResourceKey("world.boss"), new ResourceKey("world.boss-core"),
            new ResourceKey("world.projectile.arrow"), new ResourceKey("world.projectile.fireball"),
            new ResourceKey("world.projectile.cannonball"), new ResourceKey("world.boss-warning-zone"),
            new ResourceKey("world.boss-meteor"),
            new ResourceKey("world.enemy-order-route")
        };

        public ResourceKey ResourceNode(ResourceId id) => ResourceNodes.TryGetValue(id.Value, out var key)
            ? key
            : throw new KeyNotFoundException($"No world presentation is configured for resource '{id}'.");

        public ResourceKey Gatherer(MatchFaction faction) =>
            new(faction == MatchFaction.Player ? "world.gatherer.player" : "world.gatherer.enemy");

        public ResourceKey Gatherer(UnitId unitId, MatchFaction faction)
        {
            var profession = unitId.Value switch
            {
                "unit.lumberjack" => "wood", "unit.stonecutter" => "stone", "unit.iron-miner" => "iron",
                "unit.gatherer" => "food", _ => string.Empty
            };
            return string.IsNullOrEmpty(profession) ? Gatherer(faction) : new ResourceKey($"world.worker.{profession}.{Side(faction)}");
        }

        public ResourceKey Unit(UnitId id, MatchFaction faction) =>
            _presentation.GetUnit(id).WorldPrefab(faction);

        public ResourceKey Builder(MatchFaction faction) => new($"world.builder.{Side(faction)}");
        public ResourceKey TowerSite(MatchFaction faction) => new($"world.tower-site.{Side(faction)}");
        public ResourceKey Tower(MatchFaction faction) => new($"world.tower.{Side(faction)}");
        public ResourceKey Boss(bool rewardCore) => new(rewardCore ? "world.boss-core" : "world.boss");
        public ResourceKey Arrow() => new("world.projectile.arrow");
        public ResourceKey Projectile(UnitProjectileKind kind) => kind switch
        {
            UnitProjectileKind.Fireball => new ResourceKey("world.projectile.fireball"),
            UnitProjectileKind.Cannonball => new ResourceKey("world.projectile.cannonball"),
            _ => Arrow()
        };
        public ResourceKey BossWarningZone() => new("world.boss-warning-zone");
        public ResourceKey BossMeteor() => new("world.boss-meteor");
        public ResourceKey EnemyOrderRoute() => new("world.enemy-order-route");
        private static string Side(MatchFaction faction) => faction == MatchFaction.Player ? "player" : "enemy";
    }
}
