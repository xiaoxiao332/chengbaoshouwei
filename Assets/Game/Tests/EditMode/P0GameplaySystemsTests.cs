using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Tests.Shared;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class P0GameplaySystemsTests
    {
        private static readonly GameContext Context = new("tests");

        [Test]
        public async Task Economy_ReservationCommitReleaseAndGoldIsolation_AreAtomic()
        {
            var economy = new EconomySystem(SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 120)));
            await economy.InitializeAsync(Context, CancellationToken.None);
            var food = new ResourceId("resource.food");

            Assert.That(economy.TryReserve(new[] { new ResourceAmount(food, 40) }, out var reservation, out _), Is.True);
            Assert.That(economy.GetSnapshot().Single(value => value.Id.Equals(food)).Available, Is.EqualTo(80));
            Assert.That(economy.TryCommit(reservation, new[] { new ResourceAmount(food, 20) }, out _), Is.True);
            Assert.That(economy.Release(reservation), Is.True);
            Assert.That(economy.GetSnapshot().Single(value => value.Id.Equals(food)).Amount, Is.EqualTo(100));
            Assert.That(economy.TryAdd(new ResourceId("resource.gold"), 1, out var failure), Is.False);
            Assert.That(failure, Is.EqualTo(EconomyFailure.MetaResourceForbidden));
        }

        [Test]
        public async Task LumberReturnsBeforeDeposit_ThenSawmillExchangesAtomically()
        {
            var config = SchemaV5TestSnapshotFactory.Create();
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var nodes = new ResourceNodeSystem(config);
            var gatherers = new PlayerGathererSystem(config.BattlefieldLayout.Gatherers, economy, nodes,
                new MatchPoint("gate", 100, 100));
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await nodes.InitializeAsync(Context, CancellationToken.None);
            await gatherers.InitializeAsync(Context, CancellationToken.None);
            for (var tick = 1; tick <= 40; tick++) gatherers.SimulateTick(tick);
            Assert.That(Amount(economy, "resource.wood"), Is.Zero);
            for (var tick = 41; tick <= 1000 && Amount(economy, "resource.wood") == 0; tick++)
                gatherers.SimulateTick(tick);
            var gatheredWood = Amount(economy, "resource.wood");
            Assert.That(gatheredWood, Is.GreaterThanOrEqualTo(3));

            buildings.TryBuild(1, new BuildingId("building.sawmill"), out _);
            for (var i = 0; i < 5; i++) buildings.SimulateTick();
            Assert.That(Amount(economy, "resource.wood"), Is.EqualTo(gatheredWood - 2));
            Assert.That(Amount(economy, "resource.plank"), Is.EqualTo(2));
        }

[Test]
        public async Task PublicAcceleration_DoublesProcessingForEitherFactionWithoutFreeInputs()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.wood", 4));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.sawmill"), out _);
            buildings.SetPublicProductionMultiplier(2000);

            for (var i = 0; i < 5; i++)
                buildings.SimulateTick();

            Assert.That(Amount(economy, "resource.wood"), Is.EqualTo(2));
            Assert.That(Amount(economy, "resource.plank"), Is.EqualTo(4));
        }


        [Test]
        public async Task KilledUniversalGatherer_DoesNotResetRoundRobinDispatchSchedule()
        {
            var config = SchemaV5TestSnapshotFactory.Create();
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var nodes = new ResourceNodeSystem(config);
            var gatherers = new PlayerGathererSystem(config.BattlefieldLayout.Gatherers, economy, nodes,
                new MatchPoint("gate", 100, 100));
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await nodes.InitializeAsync(Context, CancellationToken.None);
            await gatherers.InitializeAsync(Context, CancellationToken.None);
            for (var i = 0; i < 3; i++) gatherers.SimulateTick(i + 1);
            var openingGatherer = gatherers.GetSnapshot().Single();
            Assert.That(openingGatherer.ResourceId.Value, Is.EqualTo("resource.food"));
            Assert.That(gatherers.Kill(openingGatherer.Id), Is.True);
            for (var tick = 4; tick < 250; tick++) gatherers.SimulateTick(tick);
            Assert.That(Amount(economy, "resource.wood"), Is.Zero);
            gatherers.SimulateTick(250);
            Assert.That(gatherers.GetSnapshot().Any(value => value.SourceId.Value == "gatherer-source.wall.universal" &&
                                                            value.ResourceId.Value == "resource.wood"), Is.True);
            for (var tick = 251; tick <= 1200 && Amount(economy, "resource.wood") == 0; tick++)
                gatherers.SimulateTick(tick);
            Assert.That(Amount(economy, "resource.wood"), Is.GreaterThanOrEqualTo(3));
        }

        [Test]
        public async Task Upgrade_PaysPlankOnce_AndMovesThroughFiveStateModel()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.plank", 10));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.sawmill"), out var instanceId);
            Assert.That(buildings.GetSnapshot()[0].UpgradeState, Is.EqualTo(BuildingUpgradeState.Ready));
            var changedCount = 0;
            buildings.Changed += () => changedCount++;
            Assert.That(buildings.TryStartUpgrade(instanceId), Is.True);
            Assert.That(Amount(economy, "resource.plank"), Is.EqualTo(8));
            Assert.That(buildings.GetSnapshot()[0].UpgradeState, Is.EqualTo(BuildingUpgradeState.Upgrading));
            Assert.That(buildings.GetSnapshot()[0].UpgradeProgressMilli, Is.Zero);
            buildings.SimulateTick();
            Assert.That(buildings.GetSnapshot()[0].UpgradeProgressMilli, Is.EqualTo(500));
            buildings.SimulateTick();
            Assert.That(buildings.GetSnapshot()[0].Level, Is.EqualTo(2));
            Assert.That(buildings.GetSnapshot()[0].UpgradeState, Is.EqualTo(BuildingUpgradeState.Max));
            Assert.That(buildings.GetSnapshot()[0].UpgradeProgressMilli, Is.Zero);
            Assert.That(changedCount, Is.EqualTo(3), "Upgrade start and both authoritative ticks must refresh presentation.");
        }

        [Test]
        public async Task Upgrade_InsufficientResources_LeavesBuildingAndInventoryUnchanged()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 1));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.sawmill"), out var instanceId);

            Assert.That(buildings.TryStartUpgrade(instanceId), Is.False);
            Assert.That(Amount(economy, "resource.plank"), Is.Zero);
            Assert.That(buildings.GetSnapshot()[0].Level, Is.EqualTo(1));
            Assert.That(buildings.GetSnapshot()[0].UpgradeState, Is.EqualTo(BuildingUpgradeState.Ready));
            Assert.That(buildings.GetSnapshot()[0].UpgradeProgressMilli, Is.Zero);
        }

        [Test]
        public async Task Camps_ZeroOneTwoOneZero_AndTwoCampsTrainInParallel()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 120));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var camps = new CampSystem(buildings);
            var training = new TrainingSystem(config, economy, buildings, camps);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await camps.InitializeAsync(Context, CancellationToken.None);
            await training.InitializeAsync(Context, CancellationToken.None);
            var card = new CardId("card.soldier.shield-guard");
            Assert.That(camps.GetCampInstanceIds(card), Is.Empty);
            buildings.TryBuild(0, new BuildingId("building.shield-camp"), out var first);
            Assert.That(camps.GetCampInstanceIds(card).Count, Is.EqualTo(1));
            buildings.TryBuild(1, new BuildingId("building.shield-camp"), out var second);
            Assert.That(camps.GetCampInstanceIds(card).Count, Is.EqualTo(2));

            var deployed = 0;
            training.UnitDeployed += (_, _) => deployed++;
            Assert.That(training.TryCreateOrder(new UnitId("unit.shield-guard"), 2, new DeploymentPoint(1, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var i = 0; i < 8; i++) training.SimulateTick();
            Assert.That(deployed, Is.EqualTo(2));
            Assert.That(Amount(economy, "resource.food"), Is.EqualTo(80));

            buildings.Demolish(second);
            Assert.That(camps.GetCampInstanceIds(card).Count, Is.EqualTo(1));
            buildings.Demolish(first);
            Assert.That(camps.GetCampInstanceIds(card), Is.Empty);
        }

        [Test]
        public async Task TrainingCancellationAndLastCampRemoval_RefundOnlyUncommittedUnits()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 120));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var camps = new CampSystem(buildings);
            var training = new TrainingSystem(config, economy, buildings, camps);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await camps.InitializeAsync(Context, CancellationToken.None);
            await training.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.shield-camp"), out var camp);
            training.TryCreateOrder(new UnitId("unit.shield-guard"), 2, new DeploymentPoint(0, 0), out _);
            for (var i = 0; i < 8; i++) training.SimulateTick();
            Assert.That(Amount(economy, "resource.food"), Is.EqualTo(100));
            buildings.Demolish(camp);
            var food = economy.GetSnapshot().Single(value => value.Id.Value == "resource.food");
            Assert.That(food.Amount, Is.EqualTo(100));
            Assert.That(food.Reserved, Is.Zero);
        }

        [Test]
        public async Task EmergencyDefenseOrder_WaitsForCurrentUnitThenPrecedesUnstartedNormalSlots()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 120));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var camps = new CampSystem(buildings);
            var training = new TrainingSystem(config, economy, buildings, camps);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await camps.InitializeAsync(Context, CancellationToken.None);
            await training.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.shield-camp"), out _);
            var unit = new UnitId("unit.shield-guard");
            var route = new RouteId("route.middle");
            var point = DeploymentPoint.World(700, 500, 1);

            Assert.That(training.TryCreateOrder(unit, 2, point, route, "source.normal", "intent.assault",
                out var normalOrderId), Is.EqualTo(TrainingFailure.None));
            training.SimulateTick(0);
            Assert.That(training.GetSnapshot().Single(value => value.Id == normalOrderId).AssignedCamps, Is.EqualTo(1));
            Assert.That(training.TryCreateOrder(unit, 1, point, route, "source.ai-logistics-defense", "intent.hold",
                TrainingOrderPriority.EmergencyDefense, "logistics:route.middle:1", out var emergencyOrderId),
                Is.EqualTo(TrainingFailure.None));

            for (var tick = 1; tick < 8; tick++) training.SimulateTick(tick);
            Assert.That(training.GetSnapshot().Single(value => value.Id == normalOrderId).Completed, Is.EqualTo(1));
            training.SimulateTick(8);
            var snapshots = training.GetSnapshot();
            Assert.That(snapshots.Single(value => value.Id == emergencyOrderId).AssignedCamps, Is.EqualTo(1));
            Assert.That(snapshots.Single(value => value.Id == normalOrderId).AssignedCamps, Is.Zero);
        }

        [Test]
        public async Task SoldierSelection_ReservesBeforePlacement_ThenCreatesDeterministicSlots()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 120));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var camps = new CampSystem(buildings);
            var training = new TrainingSystem(config, economy, buildings, camps);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await camps.InitializeAsync(Context, CancellationToken.None);
            await training.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.shield-camp"), out _);

            var unit = new UnitId("unit.shield-guard");
            Assert.That(training.UpdateSelection(unit, 3), Is.EqualTo(TrainingFailure.None));
            Assert.That(training.GetSelectionSnapshot().TotalCount, Is.EqualTo(3));
            Assert.That(economy.GetSnapshot().Single(value => value.Id.Value == "resource.food").Reserved, Is.EqualTo(60));
            Assert.That(training.SubmitSelection(400, 400, out _), Is.EqualTo(TrainingFailure.InvalidDeploymentPoint));
            Assert.That(training.GetSelectionSnapshot().TotalCount, Is.EqualTo(3));

            Assert.That(training.SubmitSelection(680, 500, out var orderIds), Is.EqualTo(TrainingFailure.None));
            Assert.That(orderIds.Count, Is.EqualTo(1));
            Assert.That(training.GetSelectionSnapshot().TotalCount, Is.Zero);
            var slots = training.GetDeploymentSlots();
            Assert.That(slots.Count, Is.EqualTo(3));
            Assert.That(slots.Select(value => value.Id), Is.Ordered);
            Assert.That(slots.All(value => value.Point.HasWorldPosition), Is.True);
            Assert.That(slots.All(value => value.Point.X is >= 548 and <= 820), Is.True);
            Assert.That(slots.All(value => value.Point.Y is >= 80 and <= 1000), Is.True);

            for (var i = 0; i < 8; i++) training.SimulateTick();
            Assert.That(training.GetDeploymentSlots().Count, Is.EqualTo(2));
            Assert.That(Amount(economy, "resource.food"), Is.EqualTo(100));
            Assert.That(economy.GetSnapshot().Single(value => value.Id.Value == "resource.food").Reserved, Is.EqualTo(40));
        }

        [Test]
        public async Task SoldierSelection_AcceptsBothDeploymentAreaBoundaries()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 120));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var camps = new CampSystem(buildings);
            var training = new TrainingSystem(config, economy, buildings, camps);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await camps.InitializeAsync(Context, CancellationToken.None);
            await training.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.shield-camp"), out _);
            var unit = new UnitId("unit.shield-guard");

            Assert.That(training.UpdateSelection(unit, 1), Is.EqualTo(TrainingFailure.None));
            Assert.That(training.SubmitSelection(548, 80, out var lowerOrders), Is.EqualTo(TrainingFailure.None));
            Assert.That(training.GetDeploymentSlots().Single().Point.X, Is.EqualTo(548));
            Assert.That(training.GetDeploymentSlots().Single().Point.Y, Is.EqualTo(80));
            foreach (var orderId in lowerOrders) Assert.That(training.Cancel(orderId), Is.EqualTo(TrainingFailure.None));

            Assert.That(training.UpdateSelection(unit, 1), Is.EqualTo(TrainingFailure.None));
            Assert.That(training.SubmitSelection(820, 1000, out var upperOrders), Is.EqualTo(TrainingFailure.None));
            var upper = training.GetDeploymentSlots().Single().Point;
            Assert.That(upper.X, Is.InRange(548, 820));
            Assert.That(upper.Y, Is.InRange(80, 1000));
            foreach (var orderId in upperOrders) Assert.That(training.Cancel(orderId), Is.EqualTo(TrainingFailure.None));
        }

        [Test]
        public async Task SoldierSelection_TimesOutBeforePlacement_AndLeavesNoReservation()
        {
            var config = SchemaV5TestSnapshotFactory.WithInventory(("resource.food", 120));
            var economy = new EconomySystem(config);
            var buildings = new BuildingSystem(config, economy);
            var camps = new CampSystem(buildings);
            var training = new TrainingSystem(config, economy, buildings, camps);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await camps.InitializeAsync(Context, CancellationToken.None);
            await training.InitializeAsync(Context, CancellationToken.None);
            buildings.TryBuild(0, new BuildingId("building.shield-camp"), out _);

            Assert.That(training.UpdateSelection(new UnitId("unit.shield-guard"), 2), Is.EqualTo(TrainingFailure.None));
            for (var i = 0; i < 8 * ContentConstants.FixedTicksPerSecond; i++) training.SimulateTick();
            Assert.That(training.GetSelectionSnapshot().TotalCount, Is.Zero);
            var food = economy.GetSnapshot().Single(value => value.Id.Value == "resource.food");
            Assert.That(food.Amount, Is.EqualTo(120));
            Assert.That(food.Reserved, Is.Zero);
        }

        [Test]
        public async Task Settlement_IsIdempotent_AndSaveFailureRollsBack()
        {
            var failSave = false;
            var progression = new ProgressionSystem(new EmptyProgressionContent(), _ =>
                failSave ? Task.FromException(new InvalidOperationException("disk")) : Task.CompletedTask);
            await progression.InitializeAsync(Context, CancellationToken.None);
            var result = new MatchResult(new MatchId("match-1"), new BattlefieldId("battlefield.prologue"), new MapModeId("mode.prologue.offensive"), true, true);
            var reward = new MatchRewardConfig(100, 50, 50, 1250);
            var first = await progression.SettleMatchAsync(result, reward, CancellationToken.None);
            var duplicate = await progression.SettleMatchAsync(result, reward, CancellationToken.None);
            Assert.That(first.GoldAwarded, Is.EqualTo(250));
            Assert.That(duplicate.Duplicate, Is.True);
            Assert.That(duplicate.GoldBalance, Is.EqualTo(first.GoldBalance));

            failSave = true;
            var failed = await progression.SettleMatchAsync(
                new MatchResult(new MatchId("match-2"), result.BattlefieldId, result.MapModeId, true, true), reward, CancellationToken.None);
            Assert.That(failed.Status, Is.EqualTo(SettlementStatus.SaveFailed));
            Assert.That(progression.GetSnapshot().Gold, Is.EqualTo(first.GoldBalance));
        }

        [Test]
        public async Task FixedDriver_AdvancesPhasesOnceInStableOrder()
        {
            var config = SchemaV5TestSnapshotFactory.WithPhaseTicks(0, 3, 6);
            var economy = new EconomySystem(config);
            var phases = new MatchPhaseSystem(config);
            var buildings = new BuildingSystem(config, economy);
            var camps = new CampSystem(buildings);
            var training = new TrainingSystem(config, economy, buildings, camps);
            var driver = new FixedSimulationSystem(phases, buildings, training);
            await economy.InitializeAsync(Context, CancellationToken.None);
            await phases.InitializeAsync(Context, CancellationToken.None);
            await buildings.InitializeAsync(Context, CancellationToken.None);
            await camps.InitializeAsync(Context, CancellationToken.None);
            await training.InitializeAsync(Context, CancellationToken.None);
            await driver.InitializeAsync(Context, CancellationToken.None);
            var entered = new List<string>();
            phases.PhaseChanged += value => entered.Add(value.Value);
            driver.Tick(0.61f);
            Assert.That(driver.TickCount, Is.EqualTo(6));
            Assert.That(entered, Is.EqualTo(new[] { "phase.contest", "phase.decisive" }));
            driver.SetPaused(true);
            driver.Tick(1f);
            Assert.That(driver.TickCount, Is.EqualTo(6));
        }

        private static int Amount(EconomySystem economy, string id) =>
            economy.GetSnapshot().Single(value => value.Id.Value == id).Amount;

        private sealed class EmptyProgressionContent : IProgressionContent
        {
            public int InitialGold => 1000;
            public CampaignStageId InitialCampaignStageId => new("stage.prologue");
            public IReadOnlyList<ProgressionCardDefinition> Cards => Array.Empty<ProgressionCardDefinition>();
            public IReadOnlyList<ProgressionStageDefinition> Stages { get; } = new[]
            {
                new ProgressionStageDefinition(new CampaignStageId("stage.prologue"), null,
                    new[] { new BattlefieldId("battlefield.prologue") })
            };
            public bool IsCardPurchasable(CampaignStageId stageId, CardId cardId) => false;
        }
    }
}
