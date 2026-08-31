using System;
using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Tests.Shared
{
    public static class SchemaV5TestSnapshotFactory
    {
        public static MatchConfigSnapshot Create(IReadOnlyList<ResourceAmount> initialInventory = null,
            IReadOnlyList<MatchPhaseConfig> phases = null, MatchBattlefieldLayoutConfig layout = null)
        {
            var resources = new[]
            {
                Resource("resource.food", ResourceAcquisitionKind.BattlefieldGathered),
                Resource("resource.meat", ResourceAcquisitionKind.Processed),
                Resource("resource.wine", ResourceAcquisitionKind.Processed),
                Resource("resource.wood", ResourceAcquisitionKind.BattlefieldGathered),
                Resource("resource.plank", ResourceAcquisitionKind.Processed),
                Resource("resource.raw-stone", ResourceAcquisitionKind.BattlefieldGathered),
                Resource("resource.stone", ResourceAcquisitionKind.Processed),
                Resource("resource.iron-ore", ResourceAcquisitionKind.BattlefieldGathered),
                Resource("resource.iron-ingot", ResourceAcquisitionKind.Processed)
            };
            var upgrade = new MatchUpgradeConfig(2, 0, null,
                new ResourceAmount(new ResourceId("resource.plank"), 2), 2, 1150, 850);
            var buildings = new[]
            {
                new MatchBuildingConfig(new BuildingId("building.sawmill"), new CardId("card.building.sawmill"),
                    BuildingCategory.Processing, new[] { Amount("resource.wood", 2) }, new[] { Amount("resource.plank", 2) },
                    null, 5, 0, new[] { upgrade }),
                new MatchBuildingConfig(new BuildingId("building.shield-camp"), new CardId("card.building.shield-camp"),
                    BuildingCategory.SoldierCamp, Array.Empty<ResourceAmount>(), Array.Empty<ResourceAmount>(),
                    new CardId("card.soldier.shield-guard"), 8, 0, new[] { upgrade })
            };
            var unit = new MatchUnitConfig(new UnitId("unit.shield-guard"), new CardId("card.soldier.shield-guard"),
                new[] { Amount("resource.food", 20) }, 8);
            var phaseValues = phases ?? new[]
            {
                new MatchPhaseConfig(new MatchPhaseId("phase.development"), 0),
                new MatchPhaseConfig(new MatchPhaseId("phase.contest"), 3000),
                new MatchPhaseConfig(new MatchPhaseId("phase.decisive"), 6000)
            };
            var combat = new MatchCombatConfig(new[] { unit },
                new MatchWallConfig("wall.player", 5000, new MatchPoint("gate.player", 52, 100)),
                new MatchWallConfig("wall.enemy", 5000, new MatchPoint("gate.enemy", 1848, 100)));
            return new MatchConfigSnapshot(ContentConstants.ExpectedSchemaVersion,
                new BattlefieldId("battlefield.prologue"), new MapModeId("mode.prologue.peaceful"),
                resources, initialInventory ?? Array.Empty<ResourceAmount>(), buildings, new[] { unit }, phaseValues,
                new MatchRewardConfig(100, 50, 50, 1000), 30, combat, layout ?? CreateBattlefieldLayout(),
                MatchHandAndOffersConfig.Empty, MatchResearchConfig.Empty, MatchBossConfig.Empty,
                MatchConstructionConfig.Empty, MatchEnemyEconomyConfig.Empty, MatchAiStrategyConfig.Empty, 1234);
        }

        public static MatchConfigSnapshot WithInventory(params (string id, int amount)[] inventory) =>
            Create(inventory.Select(value => Amount(value.id, value.amount)).ToArray());

        public static MatchConfigSnapshot WithPhaseTicks(int development, int contest, int decisive) => Create(
            phases: new[]
            {
                new MatchPhaseConfig(new MatchPhaseId("phase.development"), development),
                new MatchPhaseConfig(new MatchPhaseId("phase.contest"), contest),
                new MatchPhaseConfig(new MatchPhaseId("phase.decisive"), decisive)
            });

        private static MatchResourceConfig Resource(string id, ResourceAcquisitionKind acquisitionKind) =>
            new(new ResourceId(id), 999, false, acquisitionKind);

        private static ResourceAmount Amount(string id, int amount) => new(new ResourceId(id), amount);

        private static MatchBattlefieldLayoutConfig CreateBattlefieldLayout()
        {
            var resourceIds = new[] { "resource.food", "resource.wood", "resource.raw-stone", "resource.iron-ore" };
            var nodes = resourceIds.Select((id, index) => new MatchResourceNodeConfig(
                new ResourceNodeId($"node.player.{index}"), new MatchPoint($"node.player.{index}.point", 200 + index * 40, 100),
                100, ResourceNodeSpawnGroup.PlayerSafe, $"node.enemy.{index}", new[] { new ResourceId(id) })).ToArray();
            var wave = new MatchResourceActivationWaveConfig("wave.opening", 0, resourceIds.Length,
                new[] { ResourceNodeSpawnGroup.PlayerSafe }, resourceIds.Select(id => new ResourceId(id)).ToArray());
            var gatherers = new[]
            {
                new MatchGathererConfig(new GathererSourceId("gatherer-source.wall.universal"), new RouteId("route.middle"),
                    new UnitId("unit.gatherer"), resourceIds.Select(id => new ResourceId(id)).ToArray(), 3, 80, 3, 120,
                    Array.Empty<ResourceAmount>(), 250, GathererResourceSelectionPolicy.RoundRobin, default)
            };
            var routes = new[]
            {
                Route("route.upper", 70), Route("route.middle", 100), Route("route.lower", 130)
            };
            var zones = new[]
            {
                new MatchRect("zone.player-deployment", ZoneKind.PlayerDeployment, 548, 80, 272, 920),
                new MatchRect("zone.enemy-deployment", ZoneKind.EnemyDeployment, 1536, 80, 258, 920)
            };
            return new MatchBattlefieldLayoutConfig(1920, 1080, zones, routes,
                nodes, Array.Empty<MatchBossSpawnConfig>(), 54, new[] { wave }, gatherers, 250);
        }

        private static MatchRouteConfig Route(string id, int y) => new(new RouteId(id), new[]
        {
            new MatchPoint(id + ".player", 100, y), new MatchPoint(id + ".middle", 960, y),
            new MatchPoint(id + ".enemy", 1800, y)
        });
    }
}
