using System.Linq;
using FortressFrontier.Core.Systems;
using FortressFrontier.Editor;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Tests.Shared;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class SchemaV5MigrationChainTests
    {
        [Test]
        public void SharedSnapshot_DefaultsToSchemaV12BattlefieldGatheringAndPhases()
        {
            var snapshot = SchemaV5TestSnapshotFactory.Create();
            Assert.That(snapshot.SchemaVersion, Is.EqualTo(ContentConstants.ExpectedSchemaVersion));
            Assert.That(snapshot.InitialInventory, Is.Empty);
            Assert.That(snapshot.BattlefieldLayout.GathererDispatchIntervalTicks, Is.EqualTo(250));
            Assert.That(snapshot.BattlefieldLayout.Gatherers, Has.Count.EqualTo(1));
            Assert.That(snapshot.BattlefieldLayout.Gatherers[0].SourceId.Value, Is.EqualTo("gatherer-source.wall.universal"));
            Assert.That(snapshot.BattlefieldLayout.Gatherers[0].CarryAmount, Is.EqualTo(3));
            Assert.That(snapshot.BattlefieldLayout.Gatherers.SelectMany(value => value.AllowedResourceIds).Select(value => value.Value),
                Is.EquivalentTo(new[] { "resource.food", "resource.wood", "resource.raw-stone", "resource.iron-ore" }));
            Assert.That(snapshot.Phases.Select(value => value.StartTick), Is.EqualTo(new[] { 0, 3000, 6000 }));
            Assert.That(snapshot.Buildings.Any(value => value.Category == BuildingCategory.Gathering), Is.False);
        }

        [Test]
        public void MatchRuntimeFactory_CreatesOneCompleteOrderedRuntime()
        {
            var runtime = MatchRuntimeFactory.Create(SchemaV5TestSnapshotFactory.Create());
            var expected = new GameSystemBase[]
            {
                runtime.Economy, runtime.EnemyEconomy, runtime.Phases, runtime.Buildings, runtime.Camps,
                runtime.PlayerResearch, runtime.Training, runtime.EnemyBuildings, runtime.EnemyCamps,
                runtime.EnemyResearch, runtime.EnemyTraining, runtime.ResourceNodes, runtime.PlayerGatherers,
                runtime.EnemyGatherers, runtime.Hand, runtime.EnemyHand, runtime.AiStrategy,
                runtime.PlayerConstruction, runtime.EnemyConstruction, runtime.Boss, runtime.Combat,
                runtime.Analytics, runtime.Simulation
            };
            Assert.That(runtime.Systems, Is.EqualTo(expected));
            Assert.That(runtime.Systems.Distinct().Count(), Is.EqualTo(runtime.Systems.Count));
            Assert.That(runtime.Hand.GetType(), Is.Not.EqualTo(runtime.EnemyHand.GetType()));
            Assert.That(runtime.Systems[^1], Is.SameAs(runtime.Simulation));
            Assert.That(runtime.PlayerGatherers, Is.Not.Null);
            Assert.That(runtime.EnemyGatherers, Is.Not.Null);
            Assert.That(runtime.PlayerConstruction, Is.Not.Null);
            Assert.That(runtime.EnemyConstruction, Is.Not.Null);
            Assert.That(runtime.PlayerResearch, Is.Not.Null);
            Assert.That(runtime.EnemyResearch, Is.Not.Null);
            Assert.That(runtime.Boss, Is.Not.Null);
        }

        [Test]
        public void ProjectContent_HasNoCrossCatalogOrAddressablesIssues()
        {
            Assert.That(ProjectContentValidator.CollectIssues(), Is.Empty);
        }
    }
}
