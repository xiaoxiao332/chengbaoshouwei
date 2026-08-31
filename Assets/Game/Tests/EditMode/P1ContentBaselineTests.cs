using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Resources;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Audio;
using FortressFrontier.Tests.Shared;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace FortressFrontier.Tests
{
    public sealed class P1ContentBaselineTests
    {
        private const string RootPath = "Assets/Game/Content/Config/GameContentConfig.asset";

        [Test]
        public void SchemaV14Asset_ContainsThreeGateEconomyAndCategoryResearchBaselines()
        {
            var root = LoadRoot();
            Assert.That(root.SchemaVersion, Is.EqualTo(14));
            var validation = ContentConfigValidator.Validate(root);
            Assert.That(validation.IsValid, Is.True,
                string.Join(" | ", validation.Issues.Select(value => $"{value.Path}: {value.Message}")));
            Assert.That(root.BattlefieldCatalog.Definitions.Count, Is.EqualTo(2));
            Assert.That(root.BattlefieldCatalog.Definitions.All(value => value.MapModeIds.Count == 3), Is.True);
            Assert.That(root.BattlefieldCatalog.Definitions.All(value => value.Gatherers.Count == 3 &&
                value.GathererDispatchIntervalMinTicks == 150 && value.GathererDispatchIntervalMaxTicks == 200 &&
                value.Gatherers.Select(gatherer => gatherer.RouteId).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(new[] { "route.upper", "route.middle", "route.lower" }) &&
                value.Gatherers.All(gatherer => gatherer.CarryAmount == 3) &&
                value.Gatherers.SelectMany(gatherer => gatherer.AllowedResourceIds).ToHashSet(StringComparer.Ordinal)
                    .SetEquals(new[] { "resource.food", "resource.wood", "resource.raw-stone" })), Is.True);
            var gathererUnits = root.BattlefieldCatalog.Definitions.SelectMany(value => value.Gatherers)
                .Select(value => root.UnitCatalog.Definitions.Single(unit => unit.Id == value.UnitId)).ToArray();
            Assert.That(gathererUnits.All(value => value.RoleTags.Contains("Worker") && value.MovePerTick == 3), Is.True);
            var gathering = root.BuildingCatalog.Definitions.Where(value => value.Category == BuildingCategory.Gathering)
                .OrderBy(value => value.Id, StringComparer.Ordinal).ToArray();
            Assert.That(gathering.Select(value => value.Id), Is.EquivalentTo(new[]
                { "building.gatherer-lodge", "building.wood-gatherer-camp", "building.stone-gatherer-camp", "building.iron-gatherer-camp" }));
            Assert.That(gathering.All(value => value.Outputs.Count == 0 && value.WorkerUnitId.Length > 0), Is.True);
            Assert.That(gathering.ToDictionary(value => value.Id, value => (value.GathererDispatchIntervalTicks, value.GathererCarryAmount)),
                Is.EquivalentTo(new Dictionary<string, (int, int)>
                {
                    ["building.gatherer-lodge"] = (180, 8), ["building.wood-gatherer-camp"] = (200, 7),
                    ["building.stone-gatherer-camp"] = (220, 6), ["building.iron-gatherer-camp"] = (240, 5)
                }));
            var shield = root.UnitCatalog.Definitions.Single(value => value.Id == "unit.shield-guard");
            var archer = root.UnitCatalog.Definitions.Single(value => value.Id == "unit.archer");
            var ram = root.UnitCatalog.Definitions.Single(value => value.Id == "unit.siege-ram");
            var heavy = root.UnitCatalog.Definitions.Single(value => value.Id == "unit.heavy-warrior");
            var mage = root.UnitCatalog.Definitions.Single(value => value.Id == "unit.mage");
            var longbow = root.UnitCatalog.Definitions.Single(value => value.Id == "unit.longbow");
            var cannon = root.UnitCatalog.Definitions.Single(value => value.Id == "unit.cannon");
            Assert.That((shield.MaxHealth, shield.AttackDamage, shield.MovePerTick, shield.AttackIntervalTicks, shield.AttackRange), Is.EqualTo((360, 24, 4, 10, 28)));
            Assert.That((archer.MaxHealth, archer.AttackDamage, archer.AttackRange, archer.ProjectileSpeedPerTick), Is.EqualTo((160, 28, 180, 16)));
            Assert.That((ram.MaxHealth, ram.WallDamageMultiplierMilli, ram.MovePerTick, ram.AttackIntervalTicks), Is.EqualTo((480, 4000, 3, 16)));
            Assert.That(ram.TargetPriority, Is.EqualTo(UnitTargetPriority.StructuresOnly));
            Assert.That((heavy.MaxHealth, heavy.AttackDamage, heavy.AttackIntervalTicks, heavy.AttackRange),
                Is.EqualTo((520, 36, 14, 28)));
            Assert.That((mage.MaxHealth, mage.AttackDamage, mage.AttackIntervalTicks, mage.AttackRange,
                mage.ProjectileKind, mage.ExplosionRadius, mage.ExplosionSecondaryDamageMilli),
                Is.EqualTo((140, 40, 18, 170, UnitProjectileKind.Fireball, 60, 600)));
            Assert.That(mage.ProjectilePresentationKey, Is.EqualTo("presentation.world.projectile.fireball"));
            Assert.That((longbow.MaxHealth, longbow.AttackDamage, longbow.AttackIntervalTicks, longbow.AttackRange),
                Is.EqualTo((115, 38, 18, 280)));
            Assert.That((cannon.MaxHealth, cannon.AttackDamage, cannon.AttackIntervalTicks, cannon.AttackRange,
                cannon.ProjectileKind, cannon.ExplosionRadius, cannon.ExplosionSecondaryDamageMilli,
                cannon.WallDamageMultiplierMilli),
                Is.EqualTo((340, 54, 25, 240, UnitProjectileKind.Cannonball, 80, 650, 1500)));
            Assert.That(cannon.ProjectilePresentationKey, Is.EqualTo("presentation.world.projectile.cannonball"));
            Assert.That(shield.PlayerWorldPrefabPresentationKey, Is.EqualTo("presentation.world.unit.shield-guard.player"));
            Assert.That(archer.EnemyWorldPrefabPresentationKey, Is.EqualTo("presentation.world.unit.archer.enemy"));
            Assert.That(root.CardCatalog.TacticEffects.Count, Is.EqualTo(3));
            Assert.That(root.BuildingCatalog.ResearchUpgrades.Count, Is.EqualTo(8));
            Assert.That(root.StageEffectCatalog.AiPhaseProfiles.Single().FirstProbeStartTick, Is.EqualTo(600));
            Assert.That(root.StageEffectCatalog.AiPhaseProfiles.Single().FirstProbeEndTick, Is.EqualTo(800));
            Assert.That(root.StageEffectCatalog.AiUtilityProfiles.All(value =>
                value.LogisticsThreatMemoryTicks == 300 && value.MaxConcurrentLogisticsResponses == 2 &&
                value.EmergencyDefenseOverflowUnits == 2 && value.TowerEscalationKillCount == 2), Is.True,
                "Schema v12 utility defaults were not authored for every mode.");
            Assert.That(root.StageEffectCatalog.EnemyEconomyProfiles.All(value =>
                value.DefenseReserveFormationId == "formation.logistics-guard" &&
                value.Formations.Any(formation => formation.Id == "formation.logistics-guard" &&
                    formation.UnitIds.SequenceEqual(new[] { "unit.shield-guard" }) &&
                    formation.Quantities.SequenceEqual(new[] { 1 }) &&
                    formation.AllowedIntentIds.SequenceEqual(new[] { "intent.hold" }))), Is.True,
                "Schema v12 logistics guard formation was not authored for every enemy economy profile.");
            Assert.That(root.StageEffectCatalog.EnemyEconomyProfiles.Select(value => value.EconomicEfficiencyMilli),
                Is.EqualTo(new[] { 1000, 1050, 1100 }));
            Assert.That(root.StageEffectCatalog.EnemyEconomyProfiles.All(value => value.Camps.Any(camp => camp.UnitId == "unit.siege-ram")), Is.True);
            Assert.That(root.StageEffectCatalog.EnemyEconomyProfiles.All(value => value.Formations.Select(formation => formation.Id)
                .SequenceEqual(new[] { "formation.probe", "formation.shield-archer", "formation.economy-raid", "formation.siege-cover", "formation.magic", "formation.longbow", "formation.cannon", "formation.logistics-guard" })), Is.True);
            Assert.That(root.StageEffectCatalog.HeatTiers.Select(value => value.StartTick),
                Is.EqualTo(ContentConstants.HeatTierStartTicks));
            Assert.That(root.StageEffectCatalog.HeatTiers.Select(value => value.RewardCooldownSeconds),
                Is.EqualTo(ContentConstants.OfferCooldownSeconds));
        }

[Test]
        public void SchemaV8_PacingProfilesExposeOnlyPublicAndLegalControls()
        {
            var root = LoadRoot();
            var phaseProfile = root.StageEffectCatalog.AiPhaseProfiles.Single();
            Assert.That(phaseProfile.PublicAccelerationStartTick, Is.EqualTo(9000));
            Assert.That(phaseProfile.PublicProductionMultiplierMilli, Is.EqualTo(2000));
            Assert.That(phaseProfile.Phases[0].AllowedIntentIds,
                Is.EquivalentTo(new[] { "intent.develop", "intent.reserve", "intent.research", "intent.hold", "intent.assault" }));
            Assert.That(phaseProfile.Phases[2].AllowedIntentIds, Does.Not.Contain("intent.develop"));

            var utilities = root.StageEffectCatalog.AiUtilityProfiles.OrderBy(value => value.Id).ToArray();
            Assert.That(utilities.Select(value => value.PressureMinIntervalTicks),
                Is.EqualTo(new[] { 550, 350, 300 }));
            Assert.That(utilities.Select(value => value.PressureTargetIntervalTicks),
                Is.EqualTo(new[] { 650, 450, 375 }));
            Assert.That(utilities.Select(value => value.PressureMaxIntervalTicks),
                Is.EqualTo(new[] { 750, 550, 450 }));
            Assert.That(utilities.All(value => value.ActiveUnitSoftCap + value.QueuedUnitSoftCap <= 36), Is.True);

            var difficulties = root.StageEffectCatalog.DifficultyRules.ToDictionary(value => value.Id);
            Assert.That((difficulties["difficulty.standard"].SuboptimalIntervalMinTicks,
                difficulties["difficulty.standard"].SuboptimalIntervalMaxTicks), Is.EqualTo((600, 900)));
            Assert.That((difficulties["difficulty.standard-fast"].SuboptimalIntervalMinTicks,
                difficulties["difficulty.standard-fast"].SuboptimalIntervalMaxTicks), Is.EqualTo((900, 1200)));
            Assert.That(difficulties["difficulty.nightmare"].SuboptimalIntervalMaxTicks, Is.Zero);

            foreach (var economy in root.StageEffectCatalog.EnemyEconomyProfiles)
            {
                Assert.That(economy.Formations.All(value => value.AllowedIntentIds.Count > 0), Is.True);
                Assert.That(economy.Formations.Single(value => value.Id == "formation.economy-raid").AllowedIntentIds,
                    Is.EqualTo(new[] { "intent.raid-economy" }));
            }

            foreach (var mode in root.StageEffectCatalog.MapModes)
            {
                var waves = root.StageEffectCatalog.ResourceActivationWaves
                    .Where(value => value.MapModeId == mode.Id).OrderBy(value => value.TriggerSeconds).ToArray();
                Assert.That(waves.Select(value => value.TriggerSeconds), Is.EqualTo(new[] { 0, 60, 120, 180, 240, 300 }));
                Assert.That(waves[0].AllowedResourceIds,
                    Is.EquivalentTo(new[] { "resource.food", "resource.wood", "resource.raw-stone" }));
                Assert.That(waves[1].AllowedResourceIds, Is.EquivalentTo(new[] { "resource.food", "resource.wood" }));
                Assert.That(waves[2].AllowedResourceIds, Is.EqualTo(new[] { "resource.raw-stone" }));
                Assert.That(waves[3].AllowedResourceIds, Is.EqualTo(new[] { "resource.iron-ore" }));
                Assert.That(waves[4].AllowedResourceIds, Is.EquivalentTo(new[] { "resource.food", "resource.wood", "resource.raw-stone", "resource.iron-ore" }));
                Assert.That(waves[5].AllowedResourceIds, Is.EquivalentTo(waves[4].AllowedResourceIds));
            }
        }


        [Test]
        public void SchemaV2_IsExplicitlyRejected()
        {
            var clone = UnityEngine.Object.Instantiate(LoadRoot());
            try
            {
                var serialized = new SerializedObject(clone);
                serialized.FindProperty("_schemaVersion").intValue = 2;
                serialized.ApplyModifiedPropertiesWithoutUndo();
                var report = ContentConfigValidator.Validate(clone);
                Assert.That(report.Issues.Any(issue => issue.Code == ContentValidationCode.InvalidSchemaVersion), Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(clone); }
        }

        [Test]
        public async Task MatchSnapshot_DeepCopiesEveryP1Subconfiguration()
        {
            var root = LoadRoot();
            var service = new AssetResourceService(root);
            var system = new ContentConfigSystem(service, new ResourceKey("config.game-content"));
            await system.InitializeAsync(new GameContext("p1-snapshot-test"), CancellationToken.None);
            try
            {
                var snapshot = system.CreateMatchSnapshot(new BattlefieldId("battlefield.prologue"), new MapModeId("mode.prologue.peaceful"), 9876);
                Assert.That(snapshot.Seed, Is.EqualTo(9876));
                Assert.That(snapshot.Presentation.CardArt.Count, Is.EqualTo(root.CardCatalog.Definitions.Count));
                Assert.That(snapshot.Presentation.BuildingArt.Count, Is.EqualTo(root.BuildingCatalog.Definitions.Count));
                Assert.That(snapshot.Presentation.Units.Count, Is.EqualTo(7));
                Assert.That(snapshot.Presentation.MapArt.Value, Is.EqualTo("art.map.prologue"));
                var archerPresentation = snapshot.Presentation.GetUnit(new UnitId("unit.archer"));
                Assert.That(archerPresentation.Sprite.Value, Is.EqualTo("art.unit.archer"));
                Assert.That(archerPresentation.PlayerWorldPrefab.Value, Is.EqualTo("world.unit.archer.player"));
                Assert.That(archerPresentation.EnemyWorldPrefab.Value, Is.EqualTo("world.unit.archer.enemy"));
                Assert.That(snapshot.Combat.PlayerWall.MaxHealth, Is.EqualTo(5000));
                Assert.That(snapshot.BattlefieldLayout.Routes.Count, Is.EqualTo(3));
                Assert.That(snapshot.BattlefieldLayout.ResourceNodes.Count, Is.EqualTo(12));
                Assert.That(snapshot.BattlefieldLayout.ResourceNodes.SelectMany(value => value.AllowedResourceIds)
                    .All(value => value.Value is "resource.food" or "resource.wood" or "resource.raw-stone" or "resource.iron-ore"), Is.True);
                Assert.That(snapshot.BattlefieldLayout.ActivationWaves.Select(value => value.TriggerTick),
                    Is.EqualTo(new[] { 0, 600, 1200, 1800, 2400, 3000 }));
                Assert.That(snapshot.HandAndOffers.Offers.Select(value => value.TriggerSeconds), Is.EqualTo(ContentConstants.P1OfferSeconds));
                Assert.That(snapshot.Research.Upgrades.Count, Is.EqualTo(8));
                Assert.That(snapshot.Boss.MaxHealth, Is.EqualTo(3200));
                Assert.That(snapshot.Construction.MaxSites, Is.EqualTo(2));
                Assert.That(snapshot.EnemyEconomy.InitialHand.Count, Is.EqualTo(6));
                Assert.That(snapshot.AiStrategy.SoftmaxLookupVersion, Is.EqualTo(1));
                Assert.That(ContainsUnityObject(snapshot, new HashSet<object>(ReferenceEqualityComparer.Instance)), Is.False);
            }
            finally { await system.ShutdownAsync(CancellationToken.None); }
        }

        [Test]
        public async Task EveryStandardBattlefield_ResolvesExactlyThreeConfiguredModes()
        {
            var root = LoadRoot();
            var system = new ContentConfigSystem(new AssetResourceService(root), new ResourceKey("config.game-content"));
            await system.InitializeAsync(new GameContext("all-battlefields"), CancellationToken.None);
            try
            {
                foreach (var battlefield in root.BattlefieldCatalog.Definitions)
                {
                    Assert.That(battlefield.MapModeIds.Count, Is.EqualTo(3));
                    foreach (var modeId in battlefield.MapModeIds)
                    {
                        var snapshot = system.CreateMatchSnapshot(new BattlefieldId(battlefield.Id), new MapModeId(modeId), 8080);
                        Assert.That(snapshot.BattlefieldId.Value, Is.EqualTo(battlefield.Id));
                        Assert.That(snapshot.MapModeId.Value, Is.EqualTo(modeId));
                    Assert.That(snapshot.BattlefieldLayout.ActivationWaves.Count, Is.EqualTo(6));
                    }
                }
                var prologue = system.CreateMatchSnapshot(new BattlefieldId("battlefield.prologue"), new MapModeId("mode.prologue.peaceful"), 1);
                var river = system.CreateMatchSnapshot(new BattlefieldId("battlefield.river-pass"), new MapModeId("mode.river-pass.peaceful"), 1);
                Assert.That(river.BattlefieldLayout.BossSpawns[0].Position.X, Is.Not.EqualTo(prologue.BattlefieldLayout.BossSpawns[0].Position.X));
                Assert.That(river.Reward.FirstClearGold, Is.GreaterThan(prologue.Reward.FirstClearGold));
            }
            finally { await system.ShutdownAsync(CancellationToken.None); }
        }

        [Test]
        public async Task SchemaV13_GateShuffleIsMirroredUniqueReproducibleAndCoversSixPermutations()
        {
            var permutations = new HashSet<string>(StringComparer.Ordinal);
            for (var seed = 1; seed <= 128; seed++)
            {
                var first = await CreateSnapshot(seed);
                var second = await CreateSnapshot(seed);
                var routeOrder = new[] { "route.upper", "route.middle", "route.lower" };
                var ordered = routeOrder.Select(routeId => first.BattlefieldLayout.Gatherers
                    .Single(value => value.RouteId.Value == routeId)).ToArray();
                var resources = ordered.Select(value => value.AllowedResourceIds.Single().Value).ToArray();
                Assert.That(resources.Distinct(StringComparer.Ordinal).Count(), Is.EqualTo(3));
                Assert.That(resources, Is.EquivalentTo(new[]
                    { "resource.food", "resource.wood", "resource.raw-stone" }));
                Assert.That(routeOrder.Select(routeId => second.BattlefieldLayout.Gatherers
                    .Single(value => value.RouteId.Value == routeId).AllowedResourceIds.Single().Value), Is.EqualTo(resources));
                permutations.Add(string.Join("|", resources));

                for (var lane = 0; lane < 3; lane++)
                {
                    var player = first.BattlefieldLayout.ResourceNodes.Single(value => value.Id.Value == $"resource-node.player-{lane}");
                    var enemy = first.BattlefieldLayout.ResourceNodes.Single(value => value.Id.Value == $"resource-node.enemy-{lane}");
                    Assert.That(player.AllowedResourceIds.Single(), Is.EqualTo(enemy.AllowedResourceIds.Single()));
                    Assert.That(player.AllowedResourceIds.Single().Value, Is.EqualTo(resources[lane]));
                }
            }
            Assert.That(permutations.Count, Is.EqualTo(6));
        }

        [Test]
        public async Task SchemaV14_HeatChangesOnlyAiAndRewardParameters()
        {
            var snapshot = await CreateSnapshot(13013);
            var expected = new[]
            {
                (0, 60, 1000, 1000), (1800, 55, 950, 1100), (3600, 50, 900, 1250),
                (5400, 45, 850, 1450), (7200, 45, 800, 1650)
            };
            Assert.That(snapshot.Heat.Tiers.Select(value => (value.StartTick, value.RewardCooldownSeconds,
                value.AiPressureIntervalMultiplierMilli, value.AdvancedUnitWeightMultiplierMilli)), Is.EqualTo(expected));
            var unitCosts = snapshot.Units.ToDictionary(value => value.Id,
                value => (value.TrainingTicks, costs: string.Join("|", value.TrainingCosts.Select(cost => $"{cost.ResourceId.Value}:{cost.Amount}"))));
            var buildingCycles = snapshot.Buildings.ToDictionary(value => value.Id,
                value => (value.ProductionCycleTicks, value.GathererDispatchIntervalTicks));
            foreach (var tick in ContentConstants.HeatTierStartTicks)
            {
                _ = snapshot.Heat.GetTier(tick);
                Assert.That(snapshot.Units.ToDictionary(value => value.Id,
                    value => (value.TrainingTicks, costs: string.Join("|", value.TrainingCosts.Select(cost => $"{cost.ResourceId.Value}:{cost.Amount}")))),
                    Is.EqualTo(unitCosts));
                Assert.That(snapshot.Buildings.ToDictionary(value => value.Id,
                    value => (value.ProductionCycleTicks, value.GathererDispatchIntervalTicks)), Is.EqualTo(buildingCycles));
            }
        }

        [Test]
        public async Task SchemaV14_OfferHasFourFixedSlotsAndReinforcementConsumesOnlyAfterLegalDeployment()
        {
            var snapshot = await CreateSnapshot(31337);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("schema-v13-reward");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            try
            {
                runtime.Hand.SimulateTick(600);
                var offer = runtime.Hand.GetOffer();
                Assert.That(offer.Choices.Select(value => value.Kind), Is.EqualTo(new[]
                    { RewardChoiceKind.ContentCard, RewardChoiceKind.ContentCard, RewardChoiceKind.ProcessedResourceBundle, RewardChoiceKind.ReinforcementItem }));
                Assert.That(offer.Choices.Take(2).Select(value => value.CardId), Is.Unique);
                var reinforcement = offer.Choices.Single(value => value.Kind == RewardChoiceKind.ReinforcementItem);
                Assert.That(runtime.Hand.TryReplaceAndChoose(reinforcement.Id, runtime.Hand.GetHand().First().Id), Is.True);
                var item = runtime.Hand.GetHand().Single(value => value.ReinforcementTemplateId.HasValue);
                var count = item.Count;
                Assert.That(runtime.Hand.TryDeployReinforcement(item.Id, runtime.Training, 0, 0), Is.False);
                Assert.That(runtime.Hand.GetHand().Single(value => value.Id.Equals(item.Id)).Count, Is.EqualTo(count));
                var area = runtime.Training.PlayerDeploymentArea;
                Assert.That(runtime.Hand.TryDeployReinforcement(item.Id, runtime.Training,
                    area.X + area.Width / 2, area.Y + area.Height / 2), Is.True);
                Assert.That(runtime.Combat.GetUnits().Count(value => value.Faction == MatchFaction.Player),
                    Is.EqualTo(reinforcement.Units.Count));
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task SchemaV14_ArrowTowerUsesDeterministicWeightedBuildingSlotsForBothFactions()
        {
            const string towerCardValue = "card.battlefield.arrow-tower";
            var playerTowerOffers = 0;
            var enemyTowerOffers = 0;
            var verifiedTowerHandBoundary = false;

            for (var seed = 1; seed <= 64; seed++)
            {
                var snapshot = await CreateSnapshot(seed);
                var playerEconomy = new EconomySystem(snapshot);
                var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
                var playerHand = new HandAndOfferSystem(snapshot, playerEconomy, playerBuildings);
                var enemyEconomy = new EnemyEconomySystem(snapshot);
                var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
                var enemyHand = new EnemyHandAndOfferSystem(snapshot, enemyEconomy, enemyBuildings,
                    snapshot.HandAndOffers.GuaranteedCards);
                var systems = new GameSystemBase[]
                    { playerEconomy, playerBuildings, playerHand, enemyEconomy, enemyBuildings, enemyHand };
                foreach (var system in systems)
                    await system.InitializeAsync(new GameContext($"tower-offer-{seed}"), CancellationToken.None);
                try
                {
                    playerHand.SimulateTick(600);
                    enemyHand.SimulateTick(600);
                    var playerTower = playerHand.GetOffer().Choices.FirstOrDefault(value => value.CardId?.Value == towerCardValue);
                    var enemyTower = enemyHand.GetOffer().Choices.FirstOrDefault(value => value.CardId?.Value == towerCardValue);
                    if (playerTower != null)
                    {
                        playerTowerOffers++;
                        Assert.That(playerTower.Kind, Is.EqualTo(RewardChoiceKind.ContentCard));
                        if (!verifiedTowerHandBoundary)
                        {
                            var replaced = playerHand.GetHand().First().Id;
                            Assert.That(playerHand.TryReplaceAndChoose(playerTower.Id, replaced), Is.True);
                            var towerCard = playerHand.GetHand().Single(value => value.Id.Value == towerCardValue);
                            Assert.That(towerCard.Type, Is.EqualTo(CardType.BattlefieldItem));
                            Assert.That(playerHand.TryPlayBuilding(towerCard.Id, 0), Is.False,
                                "A battlefield structure must never enter the nine-grid BuildingSystem.");
                            Assert.That(playerBuildings.TryBuild(0, new BuildingId("building.arrow-tower"), out _), Is.False,
                                "The BuildingSystem business boundary must reject battlefield structures directly.");
                            Assert.That(playerHand.Contains(towerCard.Id), Is.True,
                                "A rejected nine-grid placement must not consume the tower card.");
                            verifiedTowerHandBoundary = true;
                        }
                    }
                    if (enemyTower != null) enemyTowerOffers++;
                }
                finally
                {
                    foreach (var system in systems.Reverse())
                        await system.ShutdownAsync(CancellationToken.None);
                }
            }

            Assert.That(playerTowerOffers, Is.InRange(1, 63),
                "The player tower must be reachable by weight without becoming guaranteed.");
            Assert.That(enemyTowerOffers, Is.InRange(1, 63),
                "The enemy tower must be reachable by weight without becoming guaranteed.");
            Assert.That(verifiedTowerHandBoundary, Is.True);

            var deterministicSnapshot = await CreateSnapshot(17);
            var signatures = new List<string>();
            for (var pass = 0; pass < 2; pass++)
            {
                var economy = new EconomySystem(deterministicSnapshot);
                var buildings = new BuildingSystem(deterministicSnapshot, economy);
                var hand = new HandAndOfferSystem(deterministicSnapshot, economy, buildings);
                var systems = new GameSystemBase[] { economy, buildings, hand };
                foreach (var system in systems)
                    await system.InitializeAsync(new GameContext($"tower-determinism-{pass}"), CancellationToken.None);
                hand.SimulateTick(600);
                signatures.Add(string.Join("|", hand.GetOffer().Choices.Select(value => value.CardId?.Value ?? value.Id.Value)));
                foreach (var system in systems.Reverse())
                    await system.ShutdownAsync(CancellationToken.None);
            }
            Assert.That(signatures[1], Is.EqualTo(signatures[0]));
        }

        [Test]
        public void SchemaV13_PauseReasonsDoNotReleaseEachOther()
        {
            var phases = new MatchPhaseSystem(new MatchConfigSnapshot(13, new BattlefieldId("battlefield.test"),
                new MapModeId("mode.test"), Array.Empty<MatchResourceConfig>(), Array.Empty<ResourceAmount>(),
                Array.Empty<MatchBuildingConfig>(), Array.Empty<MatchUnitConfig>(), Array.Empty<MatchPhaseConfig>(),
                new MatchRewardConfig(0, 0, 0, 1000), 1));
            var simulation = new FixedSimulationSystem(phases, Array.Empty<IFixedMatchSimulation>());
            simulation.SetPauseReason(MatchPauseReason.Application, true);
            simulation.SetPauseReason(MatchPauseReason.PlayerRewardChoice, true);
            simulation.SetPauseReason(MatchPauseReason.Application, false);
            Assert.That(simulation.IsPaused, Is.True);
            simulation.SetPauseReason(MatchPauseReason.PlayerRewardChoice, false);
            Assert.That(simulation.IsPaused, Is.False);
        }

        [Test]
        public async Task SeededResourceActivation_IsReproducibleAndMirrored()
        {
            var snapshot = await CreateSnapshot(424242);
            var first = new ResourceNodeSystem(snapshot);
            var second = new ResourceNodeSystem(snapshot);
            await first.InitializeAsync(new GameContext("resource-seed-a"), CancellationToken.None);
            await second.InitializeAsync(new GameContext("resource-seed-b"), CancellationToken.None);
            var a = first.GetSnapshot().Where(value => value.Active).Select(value => $"{value.Id.Value}:{value.ResourceId?.Value}:{value.GridColumn},{value.GridRow}").ToArray();
            var b = second.GetSnapshot().Where(value => value.Active).Select(value => $"{value.Id.Value}:{value.ResourceId?.Value}:{value.GridColumn},{value.GridRow}").ToArray();
            Assert.That(a, Is.EqualTo(b));
            
            Assert.That(first.GetSnapshot().Where(value => value.Active).All(value => value.SpawnRevision == 1), Is.True);
Assert.That(a.Length, Is.EqualTo(6));
            var active = first.GetSnapshot().Where(value => value.Active).ToDictionary(value => value.Id.Value);
            Assert.That(active.Values.Select(value => (value.GridColumn, value.GridRow)).Distinct().Count(), Is.EqualTo(active.Count));
            Assert.That(active.Values.Select(value => value.ResourceId?.Value).Distinct(),
                Is.EquivalentTo(new[] { "resource.food", "resource.raw-stone", "resource.wood" }));
            foreach (var player in active.Values.Where(value => value.Group == ResourceNodeSpawnGroup.PlayerSafe))
            {
                var enemyId = player.Id.Value.Replace("player", "enemy");
                Assert.That(active.ContainsKey(enemyId), Is.True);
                Assert.That(active[enemyId].ResourceId, Is.EqualTo(player.ResourceId));
                Assert.That(active[enemyId].GridColumn + player.GridColumn, Is.EqualTo(11));
                Assert.That(active[enemyId].GridRow, Is.EqualTo(player.GridRow));
            }
            first.SimulateTick(600);
            Assert.That(first.GetSnapshot().Count(value => value.Active && value.Group == ResourceNodeSpawnGroup.Central), Is.EqualTo(2));
        }

        [TestCase("battlefield.prologue", "mode.prologue.peaceful", 680, 1662, 270, 540, 810)]
        [TestCase("battlefield.river-pass", "mode.river-pass.peaceful", 720, 1622, 220, 540, 860)]
        public async Task SchemaV14_ResourceLayoutHasEqualWallDistanceAndSixUniqueCentralSlots(
            string battlefieldId, string modeId, int playerX, int enemyX, int upperY, int middleY, int lowerY)
        {
            var snapshot = await CreateSnapshot(14001, battlefieldId, modeId);
            var nodes = snapshot.BattlefieldLayout.ResourceNodes;
            Assert.That(nodes.Count(value => value.SpawnGroup == ResourceNodeSpawnGroup.PlayerSafe), Is.EqualTo(3));
            Assert.That(nodes.Count(value => value.SpawnGroup == ResourceNodeSpawnGroup.Central), Is.EqualTo(6));
            Assert.That(nodes.Count(value => value.SpawnGroup == ResourceNodeSpawnGroup.EnemySafe), Is.EqualTo(3));
            Assert.That(nodes.Where(value => value.SpawnGroup == ResourceNodeSpawnGroup.Central)
                .Select(value => (value.Position.X, value.Position.Y)), Is.EquivalentTo(new[]
                { (1040, upperY), (1300, upperY), (1040, middleY), (1300, middleY), (1040, lowerY), (1300, lowerY) }));
            foreach (var player in nodes.Where(value => value.SpawnGroup == ResourceNodeSpawnGroup.PlayerSafe))
            {
                var enemy = nodes.Single(value => value.Id.Value == player.MirrorNodeId);
                Assert.That(player.Position.X, Is.EqualTo(playerX));
                Assert.That(enemy.Position.X, Is.EqualTo(enemyX));
                Assert.That(player.Position.X - snapshot.Combat.PlayerWall.Gate.X,
                    Is.EqualTo(snapshot.Combat.EnemyWall.Gate.X - enemy.Position.X));
                Assert.That(player.RespawnCapacity, Is.EqualTo(30));
                Assert.That(player.RespawnDelayTicks, Is.EqualTo(1800));
            }
        }

        [Test]
        public async Task SchemaV14_WaveRandomnessIsReproducibleAndIndependentFromRespawnTiming()
        {
            var snapshot = await CreateSnapshot(14002);
            var a = new ResourceNodeSystem(snapshot); var b = new ResourceNodeSystem(snapshot);
            await a.InitializeAsync(new GameContext("v14-wave-a"), CancellationToken.None);
            await b.InitializeAsync(new GameContext("v14-wave-b"), CancellationToken.None);
            a.SimulateTick(600); b.SimulateTick(600);
            var depleted = a.GetSnapshot().First(value => value.Active && value.Group == ResourceNodeSpawnGroup.Central);
            a.Harvest(depleted.Id, depleted.SpawnRevision, depleted.ResourceId.Value, depleted.Remaining);
            a.SimulateTick(1050);
            var beforeA = a.GetSnapshot().Where(value => value.Active).Select(value => value.Id).ToHashSet();
            var beforeB = b.GetSnapshot().Where(value => value.Active).Select(value => value.Id).ToHashSet();
            a.SimulateTick(1200); b.SimulateTick(1200);
            var newA = a.GetSnapshot().Where(value => value.Active && !beforeA.Contains(value.Id)).Select(value => value.Id).Single();
            var newB = b.GetSnapshot().Where(value => value.Active && !beforeB.Contains(value.Id)).Select(value => value.Id).Single();
            Assert.That(newA, Is.EqualTo(newB));

            var different = await CreateSnapshot(14003);
            var c = new ResourceNodeSystem(different);
            await c.InitializeAsync(new GameContext("v14-wave-c"), CancellationToken.None);
            c.SimulateTick(3000); a.SimulateTick(3000);
            Assert.That(c.GetSnapshot().Where(value => value.Group == ResourceNodeSpawnGroup.Central)
                .OrderBy(value => value.Id.Value).Select(value => value.ResourceId?.Value),
                Is.Not.EqualTo(a.GetSnapshot().Where(value => value.Group == ResourceNodeSpawnGroup.Central)
                    .OrderBy(value => value.Id.Value).Select(value => value.ResourceId?.Value)));
        }

        [Test]
        public void SchemaV14_RewardRarityConfigUsesExplicitBudgetsWithoutUnitStatChanges()
        {
            var reward = LoadRoot().RewardCatalog.Definitions.First();
            Assert.That(reward.RarityWeights.OrderBy(value => value.Rarity)
                .Select(value => value.HeatTierWeights.ToArray()), Is.EqualTo(new[]
                {
                    new[] { 100, 75, 60, 50, 45 }, new[] { 0, 25, 40, 40, 40 }, new[] { 0, 0, 0, 10, 15 }
                }));
            Assert.That(reward.ProcessedResourceBundles.Count, Is.EqualTo(9));
            Assert.That(reward.ProcessedResourceBundles.GroupBy(value => value.Rarity).All(value => value.Count() == 3), Is.True);
            Assert.That(reward.ReinforcementTemplates.All(value => value.Rarity ==
                (value.MinimumHeatTier == 0 ? RewardRarity.Common : value.MinimumHeatTier < 3 ? RewardRarity.Rare : RewardRarity.Epic)), Is.True);
        }

        [Test]
        public async Task DepletedResource_ReleasesGridCellAndRespawnsDeterministically()
        {
            var snapshot = await CreateSnapshot(9123);
            var nodes = new ResourceNodeSystem(snapshot);
            await nodes.InitializeAsync(new GameContext("resource-respawn"), CancellationToken.None);
            var target = nodes.GetSnapshot().First(value => value.Active && value.Group == ResourceNodeSpawnGroup.PlayerSafe);
            Assert.That(nodes.Harvest(target.Id, target.SpawnRevision, target.ResourceId.Value, target.Remaining), Is.EqualTo(target.Remaining));
            Assert.That(nodes.GetSnapshot().Single(value => value.Id.Equals(target.Id)).Active, Is.False);
            nodes.SimulateTick(1799);
            Assert.That(nodes.GetSnapshot().Single(value => value.Id.Equals(target.Id)).Active, Is.False);
            nodes.SimulateTick(1800);
            var respawned = nodes.GetSnapshot().Single(value => value.Id.Equals(target.Id));
            Assert.That(respawned.Active, Is.True);
            
            
            Assert.That(respawned.ResourceId, Is.EqualTo(target.ResourceId),
                "A depleted node must respawn from its activated wave resource pool without changing identity.");
Assert.That(respawned.SpawnRevision, Is.EqualTo(target.SpawnRevision + 1));
            Assert.That(nodes.Harvest(target.Id, target.SpawnRevision, target.ResourceId.Value, 1), Is.Zero);
            Assert.That(nodes.TryGetNode(target.Id, out var queriedRespawn), Is.True);
            Assert.That(queriedRespawn.X, Is.EqualTo(respawned.X));
            Assert.That(queriedRespawn.Y, Is.EqualTo(respawned.Y));
Assert.That(respawned.Remaining, Is.EqualTo(respawned.Capacity));
            Assert.That(respawned.Capacity, Is.EqualTo(30));
            Assert.That(nodes.GetSnapshot().Where(value => value.Active)
                .Select(value => (value.GridColumn, value.GridRow)).Distinct().Count(),
                Is.EqualTo(nodes.GetSnapshot().Count(value => value.Active)));
        }

[Test]
        public async Task GathererTarget_UsesRecordedNodeCoordinatesAndGeneration()
        {
            var snapshot = await CreateSnapshot(24680);
            var context = new GameContext("gatherer-target-coordinate");
            var economy = new EconomySystem(snapshot);
            var nodes = new ResourceNodeSystem(snapshot);
            var gatherers = new PlayerGathererSystem(snapshot.BattlefieldLayout.Gatherers, economy, nodes,
                snapshot.Combat.PlayerWall.Gate);
            await economy.InitializeAsync(context, CancellationToken.None);
            await nodes.InitializeAsync(context, CancellationToken.None);
            await gatherers.InitializeAsync(context, CancellationToken.None);

            gatherers.SimulateTick(0);
            var worker = gatherers.GetSnapshot().First(value => value.TargetNodeId.Value != null);
            Assert.That(nodes.TryGetNode(worker.TargetNodeId, out var node), Is.True);
            Assert.That(worker.TargetX, Is.EqualTo(node.X));
            Assert.That(worker.TargetY, Is.EqualTo(node.Y));
            Assert.That(worker.TargetSpawnRevision, Is.EqualTo(node.SpawnRevision));

            Assert.That(nodes.Harvest(node.Id, node.SpawnRevision, node.ResourceId.Value, node.Remaining),
                Is.EqualTo(node.Remaining));
            nodes.SimulateTick(1800);
            var respawned = nodes.GetSnapshot().Single(value => value.Id.Equals(node.Id));
            Assert.That(respawned.SpawnRevision, Is.EqualTo(node.SpawnRevision + 1));
            Assert.That(nodes.Harvest(node.Id, node.SpawnRevision, node.ResourceId.Value, 1), Is.Zero);

            gatherers.SimulateTick(1);
            var refreshedWorker = gatherers.GetSnapshot().Single(value => value.Id == worker.Id);
            if (refreshedWorker.TargetNodeId.Value != null)
            {
                Assert.That(nodes.TryGetNode(refreshedWorker.TargetNodeId, out var refreshedNode), Is.True);
                Assert.That(refreshedWorker.TargetX, Is.EqualTo(refreshedNode.X));
                Assert.That(refreshedWorker.TargetY, Is.EqualTo(refreshedNode.Y));
                Assert.That(refreshedWorker.TargetSpawnRevision, Is.EqualTo(refreshedNode.SpawnRevision));
            }
        }


        [Test]
        public async Task BattlefieldStoneGatherer_CollectsWithoutGatheringBuilding()
        {
            var snapshot = await CreateSnapshot(42);
            var context = new GameContext("stone-gathering");
            var economy = new EconomySystem(snapshot);
            var buildings = new BuildingSystem(snapshot, economy);
            var nodes = new ResourceNodeSystem(snapshot);
            var gatherers = new PlayerGathererSystem(snapshot.BattlefieldLayout.Gatherers, economy, nodes,
                snapshot.Combat.PlayerWall.Gate);
            foreach (var system in new GameSystemBase[] { economy, buildings, nodes, gatherers })
                await system.InitializeAsync(context, CancellationToken.None);
            Assert.That(snapshot.Buildings.Count(value => value.Category == BuildingCategory.Gathering), Is.EqualTo(4));
            Assert.That(snapshot.BattlefieldLayout.Gatherers.Count, Is.EqualTo(3));
            Assert.That(snapshot.BattlefieldLayout.Gatherers.All(value =>
                value.SelectionPolicy == GathererResourceSelectionPolicy.Fixed), Is.True);
            Assert.That(snapshot.BattlefieldLayout.Gatherers.Select(value => value.AllowedResourceIds.Single().Value),
                Is.EquivalentTo(new[] { "resource.food", "resource.wood", "resource.raw-stone" }));
            var before = economy.GetAvailable(new ResourceId("resource.raw-stone"));
            for (var tick = 0; tick < 1000; tick++)
            {
                nodes.SimulateTick(tick);
                gatherers.SimulateTick(tick);
            }
            Assert.That(economy.GetAvailable(new ResourceId("resource.raw-stone")), Is.GreaterThan(before));
        }

        [Test]
        public async Task CentralResource_BecomesSharedContestTargetAfterSafeNodesDeplete()
        {
            var snapshot = await CreateSnapshot(31415);
            var nodes = new ResourceNodeSystem(snapshot);
            await nodes.InitializeAsync(new GameContext("central-contest"), CancellationToken.None);
            nodes.SimulateTick(600);
            var central = nodes.GetSnapshot().First(value => value.Active && value.Group == ResourceNodeSpawnGroup.Central);
            foreach (var safe in nodes.GetSnapshot().Where(value => value.Active && value.Group != ResourceNodeSpawnGroup.Central && value.ResourceId.Equals(central.ResourceId)))
                nodes.Harvest(safe.Id, safe.SpawnRevision, safe.ResourceId.Value, safe.Remaining);

            Assert.That(nodes.TryFindNode(MatchFaction.Player, central.ResourceId.Value,
                snapshot.Combat.PlayerWall.Gate.X, snapshot.Combat.PlayerWall.Gate.Y, out var playerTarget), Is.True);
            Assert.That(nodes.TryFindNode(MatchFaction.Enemy, central.ResourceId.Value,
                snapshot.Combat.EnemyWall.Gate.X, snapshot.Combat.EnemyWall.Gate.Y, out var enemyTarget), Is.True);
            Assert.That(playerTarget.Id, Is.EqualTo(central.Id));
            Assert.That(enemyTarget.Id, Is.EqualTo(central.Id));
            var before = central.Remaining;
            Assert.That(nodes.Harvest(playerTarget.Id, playerTarget.SpawnRevision, central.ResourceId.Value, 3), Is.EqualTo(3));
            Assert.That(nodes.Harvest(enemyTarget.Id, enemyTarget.SpawnRevision, central.ResourceId.Value, 4), Is.EqualTo(4));
            Assert.That(nodes.GetSnapshot().Single(value => value.Id.Equals(central.Id)).Remaining, Is.EqualTo(before - 7));
        }

        [Test]
        public async Task StableRewardCommands_RequireAtomicReplacementWithoutCreatingRawFood()
        {
            var snapshot = await CreateSnapshot(777);
            var economy = new EconomySystem(snapshot);
            var buildings = new BuildingSystem(snapshot, economy);
            var hand = new HandAndOfferSystem(snapshot, economy, buildings);
            var context = new GameContext("hand-test");
            await economy.InitializeAsync(context, CancellationToken.None);
            await buildings.InitializeAsync(context, CancellationToken.None);
            await hand.InitializeAsync(context, CancellationToken.None);
            Assert.That(hand.TotalCount, Is.EqualTo(6));
            var foodBeforeOffer = economy.GetAvailable(new ResourceId("resource.food"));
            hand.SimulateTick(600);
            var offer = hand.GetOffer();
            Assert.That(offer.Active, Is.True);
            var contentChoice = offer.Choices.First(value => value.Kind == RewardChoiceKind.ContentCard);
            var replaced = hand.GetHand().First().Id;
            Assert.That(hand.ChooseOffer(contentChoice), Is.False);
            Assert.That(hand.TryReplaceAndChoose(contentChoice.Id, replaced), Is.True);
            Assert.That(hand.TotalCount, Is.EqualTo(6));
            Assert.That(economy.GetAvailable(new ResourceId("resource.food")), Is.EqualTo(foodBeforeOffer));
            Assert.That(hand.TryPlayBuilding(new CardId("card.building.gatherer-lodge"), 0), Is.True);
            Assert.That(hand.TotalCount, Is.EqualTo(5));
        }

        [Test]
        public async Task EnemyEconomyLedger_RecordsDeterministicIncomeAndExpenseWithConservation()
        {
            var snapshot = await CreateSnapshot(701);
            var economy = new EnemyEconomySystem(snapshot);
            await economy.InitializeAsync(new GameContext("enemy-ledger"), CancellationToken.None);

            economy.SimulateTick(42);
            Assert.That(economy.TryAdd(new ResourceId("resource.wood"), 10, out _), Is.True);
            Assert.That(economy.TryExchange(
                new[] { new ResourceAmount(new ResourceId("resource.wood"), 4) },
                new[] { new ResourceAmount(new ResourceId("resource.plank"), 2) }, out _), Is.True);

            var ledger = economy.GetLedger();
            var delivery = ledger.Single(value => value.Tick == 42 && value.ResourceId.Value == "resource.wood" && value.Amount == 10);
            Assert.That(delivery.SourceId, Is.EqualTo("source.resource-delivery"));
            Assert.That(delivery.IntentId, Is.EqualTo("intent.develop"));
            var exchange = ledger.Where(value => value.Tick == 42 && value.TransactionId != delivery.TransactionId).ToArray();
            Assert.That(exchange.Select(value => value.Amount).ToArray(), Is.EqualTo(new[] { -4, 2 }));
            Assert.That(exchange.Select(value => value.SourceId).Distinct().Single(), Is.EqualTo("source.virtual-facility"));

            foreach (var balance in economy.GetSnapshot())
                Assert.That(ledger.Where(value => value.ResourceId.Equals(balance.Id)).Sum(value => value.Amount),
                    Is.EqualTo(balance.Amount), balance.Id.Value);
            await economy.ShutdownAsync(CancellationToken.None);
        }

        [Test]
        public async Task TowerConstruction_ConsumesCardAndStone_ThenBuilderCompletesVisibleSite()
        {
            var snapshot = await CreateSnapshot(702);
            var economy = new EnemyEconomySystem(snapshot);
            var towerCard = snapshot.Buildings.Single(value => value.Id.Equals(snapshot.Construction.TowerBuildingId)).SourceCardId;
            var cards = new FixedMatchCardInventory(new[] { towerCard });
            var construction = new TowerConstructionSystem(MatchFaction.Enemy, snapshot, economy, cards);
            var context = new GameContext("tower-construction");
            await economy.InitializeAsync(context, CancellationToken.None);
            await construction.InitializeAsync(context, CancellationToken.None);
            foreach (var cost in snapshot.Construction.Costs)
                Assert.That(economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

            var buildable = snapshot.BattlefieldLayout.Zones.Single(value => value.Kind == ZoneKind.TowerBuildable);
            var x = buildable.X + buildable.Width - 200;
            var y = buildable.Y + 20;
            Assert.That(construction.TryStartSite(x, y, out var siteId), Is.EqualTo(TowerConstructionFailure.None));
            Assert.That(cards.Contains(towerCard), Is.False);
            Assert.That(construction.GetSites().Single().State, Is.EqualTo(TowerSiteState.Blueprint));
            Assert.That(economy.GetLedger().Any(value => value.Amount < 0 && value.SourceId == "source.tower-construction" &&
                value.IntentId == "intent.build-tower"), Is.True);

            for (var tick = 0; tick < 20; tick++) construction.SimulateTick(tick);
            var progressBeforeDeath = construction.GetSites().Single().ProgressTicks;
            Assert.That(progressBeforeDeath, Is.GreaterThan(0));
            Assert.That(construction.KillActiveBuilder(), Is.True);
            Assert.That(construction.GetSites().Single().ProgressTicks,
                Is.EqualTo(progressBeforeDeath * snapshot.Construction.RetainedProgressMilli / 1000));
            for (var tick = 20; tick < 400; tick++) construction.SimulateTick(tick);
            Assert.That(construction.GetSites(), Is.Empty);
            Assert.That(construction.GetTowers().Single().Id, Is.EqualTo(siteId));
            Assert.That(construction.GetTowers().Single().Health, Is.EqualTo(snapshot.Construction.MaxHealth));
        }

        [Test]
        public async Task Research_RequiresLab_PaysOnce_CompletesAndAppliesToFutureRole()
        {
            var snapshot = await CreateSnapshot(703);
            var economy = new EconomySystem(snapshot);
            var buildings = new BuildingSystem(snapshot, economy);
            var research = new PlayerResearchSystem(snapshot, economy, buildings);
            var context = new GameContext("research");
            await economy.InitializeAsync(context, CancellationToken.None);
            await buildings.InitializeAsync(context, CancellationToken.None);
            await research.InitializeAsync(context, CancellationToken.None);
            var candidate = research.GetCandidates().First();
            Assert.That(research.TryStart(candidate.Id), Is.EqualTo(ResearchFailure.LabMissing));
            Assert.That(buildings.TryBuild(0, new BuildingId("building.research-lab"), out var labId), Is.True);
            foreach (var cost in snapshot.Research.Costs)
                Assert.That(economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

            Assert.That(research.TryStart(candidate.Id), Is.EqualTo(ResearchFailure.None));
            Assert.That(research.TryStart(candidate.Id), Is.EqualTo(ResearchFailure.AlreadyResearching));
            var requiredTicks = research.GetSnapshot().RequiredTicks;
            for (var tick = 0; tick < requiredTicks; tick++) research.SimulateTick(tick);
            var upgrade = snapshot.Research.Upgrades.Single(value => value.Id.Equals(candidate.Id));
            Assert.That(research.GetRank(candidate.Id), Is.EqualTo(1));
            var unitId = new UnitId(candidate.TargetRole switch
            {
                ResearchCategory.Ranged => "unit.archer",
                ResearchCategory.Magic => "unit.mage",
                ResearchCategory.Siege => "unit.siege-ram",
                _ => "unit.shield-guard"
            });
            var modifier = upgrade.Modifiers.Single();
            Assert.That(research.GetMultiplierMilli(unitId, modifier.PropertyKey),
                Is.EqualTo(1000 + modifier.PercentPerRankBasisPoints / 10));
            Assert.That(research.GetSnapshot().CompletedRanks, Is.EqualTo(1));
        }

        [Test]
        public async Task CategoryHealthResearch_OnlyChangesUnitsDeployedAfterCompletion()
        {
            var snapshot = await CreateSnapshot(1703);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("future-deployment-research");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            try
            {
                Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.research-lab"), out _), Is.True);
                Assert.That(runtime.Buildings.TryBuild(1, new BuildingId("building.shield-camp"), out _), Is.True);
                ResearchCandidateSnapshot target = default;
                for (var attempt = 0; attempt < 24 && target.Id.Value == null; attempt++)
                {
                    target = runtime.PlayerResearch.GetCandidates().FirstOrDefault(value =>
                        value.Modifiers.Any(modifier => modifier.PropertyKey == "health"));
                    if (target.Id.Value != null) break;
                    var filler = runtime.PlayerResearch.GetCandidates().First();
                    foreach (var cost in snapshot.Research.Costs)
                        Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                    Assert.That(runtime.PlayerResearch.TryStart(filler.Id), Is.EqualTo(ResearchFailure.None));
                    var ticks = runtime.PlayerResearch.GetSnapshot().RequiredTicks;
                    for (var tick = 0; tick < ticks; tick++) runtime.PlayerResearch.SimulateTick(tick);
                }
                Assert.That(target.Id.Value, Is.Not.Null, "A health preset must remain reachable through deterministic candidates.");

                Assert.That(target.TargetRole, Is.EqualTo(ResearchCategory.Melee));
                var unitId = new UnitId("unit.shield-guard");
                var unit = snapshot.Units.Single(value => value.Id.Equals(unitId));
                foreach (var cost in unit.TrainingCosts)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount * 2, out _), Is.True);
                Assert.That(runtime.Training.TryCreateOrder(unitId, 1, DeploymentPoint.World(760, 540, 1), out _),
                    Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= unit.TrainingTicks; tick++) runtime.Training.SimulateTick(tick);
                var before = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Player);

                foreach (var cost in snapshot.Research.Costs)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                Assert.That(runtime.PlayerResearch.TryStart(target.Id), Is.EqualTo(ResearchFailure.None));
                var required = runtime.PlayerResearch.GetSnapshot().RequiredTicks;
                for (var tick = 0; tick < required; tick++) runtime.PlayerResearch.SimulateTick(tick);
                Assert.That(before.MaxHealth, Is.EqualTo(unit.MaxHealth), "An already deployed unit must not mutate.");
                Assert.That(runtime.Combat.GetUnits().Single(value => value.Id == before.Id).MaxHealth,
                    Is.EqualTo(before.MaxHealth));

                Assert.That(runtime.Training.TryCreateOrder(unitId, 1, DeploymentPoint.World(780, 540, 1), out _),
                    Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= unit.TrainingTicks; tick++) runtime.Training.SimulateTick(tick + 1000);
                var after = runtime.Combat.GetUnits().Where(value => value.Faction == MatchFaction.Player)
                    .OrderBy(value => value.Id).Last();
                Assert.That(after.MaxHealth, Is.GreaterThan(before.MaxHealth));
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task UtilityAi_IsDeterministic_UsesHardGatesAndHonorsCommitment()
        {
            var snapshot = await CreateSnapshot(704);
            var left = MatchRuntimeFactory.Create(snapshot);
            var right = MatchRuntimeFactory.Create(snapshot);
            var leftContext = new GameContext("ai-left");
            var rightContext = new GameContext("ai-right");
            foreach (var system in left.Systems) await system.InitializeAsync(leftContext, CancellationToken.None);
            foreach (var system in right.Systems) await system.InitializeAsync(rightContext, CancellationToken.None);

            left.Simulation.AdvanceTicks(1800);
            right.Simulation.AdvanceTicks(1800);
            var leftDecisions = left.AiStrategy.GetDecisions();
            var rightDecisions = right.AiStrategy.GetDecisions();
            Assert.That(leftDecisions.Count, Is.GreaterThan(2));
            Assert.That(leftDecisions.Select(value => $"{value.Tick}|{value.IntentId}|{value.Utility}|{value.Result}"),
                Is.EqualTo(rightDecisions.Select(value => $"{value.Tick}|{value.IntentId}|{value.Utility}|{value.Result}")));
            Assert.That(leftDecisions.Where(value => value.IntentId == "intent.build-tower")
                .All(value => !value.Result.Contains("failed", StringComparison.Ordinal)), Is.True,
                "A tower candidate must pass the same position/path/card/resource preflight used by command execution.");
            Assert.That(leftDecisions.All(value => value.CommitmentUntilTick - value.Tick >= 80), Is.True);
            for (var index = 1; index < leftDecisions.Count; index++)
                Assert.That(leftDecisions[index].Tick, Is.GreaterThanOrEqualTo(leftDecisions[index - 1].CommitmentUntilTick));

            foreach (var system in left.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            foreach (var system in right.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [TestCase("battlefield.prologue", "mode.prologue.peaceful")]
        [TestCase("battlefield.prologue", "mode.prologue.offensive")]
        [TestCase("battlefield.prologue", "mode.prologue.nightmare")]
        [TestCase("battlefield.river-pass", "mode.river-pass.peaceful")]
        [TestCase("battlefield.river-pass", "mode.river-pass.offensive")]
        [TestCase("battlefield.river-pass", "mode.river-pass.nightmare")]
        public async Task EnemyFactory_FirstFormationClosesRoutePreviewAndWorldSpawn(string battlefieldId, string modeId)
        {
            var snapshot = await CreateSnapshot(1701, battlefieldId, modeId);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            foreach (var system in runtime.Systems) await system.InitializeAsync(new GameContext("enemy-first-formation"), CancellationToken.None);
            try
            {
                DeploymentSlotSnapshot preview = null;
                var previewTicks = 0;
                CombatUnitSnapshot spawned = null;
                for (var tick = 1; tick <= 900 && spawned == null; tick++)
                {
                    runtime.Simulation.AdvanceTicks(1);
                    var playerOffer = runtime.Hand.GetOffer();
                    if (playerOffer.Active)
                    {
                        var claimed = playerOffer.Choices.Any(value => runtime.Hand.ChooseOffer(value));
                        if (!claimed)
                            runtime.Hand.TryReplaceAndChoose(playerOffer.Choices[0].Id, runtime.Hand.GetHand().First().Id);
                    }
                    var current = runtime.EnemyTraining.GetDeploymentSlots().FirstOrDefault();
                    if (current != null)
                    {
                        preview ??= current;
                        if (current.RouteId.Equals(preview.RouteId) && current.Point.X == preview.Point.X && current.Point.Y == preview.Point.Y)
                            previewTicks++;
                    }
                    spawned = runtime.Combat.GetUnits().FirstOrDefault(value =>
                        value.Faction == MatchFaction.Enemy && value.RouteId.Value != null);
                }
                var firstTrain = runtime.AiStrategy.GetDecisions().FirstOrDefault(value => value.Result.StartsWith("train:", StringComparison.Ordinal));
                var diagnostics = string.Join(";", runtime.AiStrategy.GetDecisions().Select(value => $"{value.Tick}:{value.CandidateId}:{value.Result}:{value.GateFailure}")) +
                    " inventory=" + string.Join(",", runtime.EnemyEconomy.GetSnapshot().Select(value => $"{value.Id.Value}={value.Amount}/{value.Available}")) +
                    " health=" + runtime.AiStrategy.GetHealth().DefectId;
                Assert.That(firstTrain, Is.Not.Null, diagnostics);
                Assert.That(firstTrain.Tick, Is.LessThanOrEqualTo(800));
                Assert.That(preview, Is.Not.Null);
                Assert.That(preview.Point.HasWorldPosition, Is.True);
                Assert.That(preview.RouteId.Value, Is.Not.Empty);
                Assert.That(previewTicks, Is.GreaterThanOrEqualTo(10));
                Assert.That(spawned, Is.Not.Null);
                Assert.That(spawned.RouteId, Is.EqualTo(preview.RouteId));
                Assert.That((spawned.SpawnX, spawned.SpawnY, spawned.Lane), Is.EqualTo((preview.Point.X, preview.Point.Y, preview.Point.Lane)));
            }
            finally { foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None); }
        }

        [TestCase("battlefield.prologue", "mode.prologue.peaceful")]
        [TestCase("battlefield.prologue", "mode.prologue.offensive")]
        [TestCase("battlefield.prologue", "mode.prologue.nightmare")]
        [TestCase("battlefield.river-pass", "mode.river-pass.peaceful")]
        [TestCase("battlefield.river-pass", "mode.river-pass.offensive")]
        [TestCase("battlefield.river-pass", "mode.river-pass.nightmare")]
        public async Task EnemyFactory_NineThousandTickRunPreservesEconomyAndAllPhaseBoundaries(string battlefieldId, string modeId)
        {
            var snapshot = await CreateSnapshot(3001, battlefieldId, modeId);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            foreach (var system in runtime.Systems) await system.InitializeAsync(new GameContext("enemy-long-run"), CancellationToken.None);
            try
            {
                runtime.Simulation.AdvanceTicks(2999);
                Assert.That(runtime.Phases.CurrentPhaseId.Value, Is.EqualTo("phase.development"));
                runtime.Simulation.AdvanceTicks(1);
                Assert.That(runtime.Phases.CurrentPhaseId.Value, Is.EqualTo("phase.contest"));
                runtime.Simulation.AdvanceTicks(1);
                Assert.That(runtime.Phases.CurrentPhaseId.Value, Is.EqualTo("phase.contest"));
                runtime.Simulation.AdvanceTicks(2998);
                Assert.That(runtime.Simulation.TickCount, Is.EqualTo(5999));
                Assert.That(runtime.Phases.CurrentPhaseId.Value, Is.EqualTo("phase.contest"));
                runtime.Simulation.AdvanceTicks(1);
                Assert.That(runtime.Phases.CurrentPhaseId.Value, Is.EqualTo("phase.decisive"));
                Assert.That(runtime.Phases.IsPublicAccelerationActive, Is.False);
                runtime.Simulation.AdvanceTicks(2999);
                Assert.That(runtime.Simulation.TickCount, Is.EqualTo(8999));
                Assert.That(runtime.Phases.IsPublicAccelerationActive, Is.False);
                runtime.Simulation.AdvanceTicks(1);
                Assert.That(runtime.Phases.PublicProductionMultiplierMilli, Is.EqualTo(2000));
                Assert.That(runtime.EnemyEconomy.GetSnapshot().All(value => value.Amount >= 0 && value.Reserved >= 0 && value.Reserved <= value.Amount), Is.True);
                Assert.That(runtime.EnemyEconomy.GetLedger().All(value => value.TransactionId > 0 && !string.IsNullOrWhiteSpace(value.SourceId)), Is.True);
                Assert.That(runtime.AiStrategy.GetDecisions().Any(value => value.Result.StartsWith("train:", StringComparison.Ordinal) && value.Tick <= 800), Is.True);
            }
            finally { foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None); }
        }

        [Test]
        public async Task NeutralBoss_WarnsSpawnsFightsAndAwardsCoreOnContact()
        {
            var snapshot = await CreateSnapshot(705);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("boss");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            var spawn = snapshot.BattlefieldLayout.BossSpawns.First();
            runtime.Boss.SimulateTick(spawn.WarningTick);
            Assert.That(runtime.Boss.GetSnapshot().First(value => value.SpawnId == spawn.Id).State,
                Is.EqualTo(BossRuntimeState.Warning));
            runtime.Boss.SimulateTick(spawn.SpawnTick);
            Assert.That(runtime.Boss.GetSnapshot().First(value => value.SpawnId == spawn.Id).State,
                Is.EqualTo(BossRuntimeState.Active));

            Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True);
            var unit = snapshot.Units.Single(value => value.Id.Value == "unit.shield-guard");
            foreach (var cost in unit.TrainingCosts)
                Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
            Assert.That(runtime.Training.TryCreateOrder(unit.Id, 1,
                DeploymentPoint.World(spawn.Position.X, spawn.Position.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick <= unit.TrainingTicks; tick++) runtime.Training.SimulateTick(tick);
            Assert.That(runtime.Combat.GetUnits().Any(value => value.Faction == MatchFaction.Player), Is.True);

            Assert.That(runtime.Boss.TryDamage(spawn.Id, MatchFaction.Player,
                snapshot.Boss.MaxHealth + snapshot.Boss.Armor), Is.True);
            runtime.Boss.SimulateTick(spawn.SpawnTick + 1);
            var resolved = runtime.Boss.GetSnapshot().First(value => value.SpawnId == spawn.Id);
            Assert.That(resolved.State, Is.EqualTo(BossRuntimeState.Resolved));
            Assert.That(resolved.Winner, Is.EqualTo(MatchFaction.Player));
            var claim = runtime.Boss.GetClaims().Single();
            Assert.That(claim.Faction, Is.EqualTo(MatchFaction.Player));
            Assert.That(claim.Kind, Is.EqualTo(BossRewardKind.ResourceBundle));
            Assert.That(claim.ResourceId.HasValue, Is.True);
            Assert.That(claim.GrantedAmount, Is.GreaterThan(0));
            foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [Test]
        public async Task GathererSources_DispatchEveryTwoHundredFiftyTicksWithoutInflightOrGlobalCap()
        {
            var baseSnapshot = SchemaV5TestSnapshotFactory.Create();
            var longTripConfigs = baseSnapshot.BattlefieldLayout.Gatherers.Select(value =>
                new MatchGathererConfig(value.SourceId, value.RouteId, value.UnitId, value.AllowedResourceIds,
                    value.CarryAmount, 1000, value.MovePerTick, value.MaxHealth, value.DispatchCosts,
                    value.DispatchIntervalTicks, value.SelectionPolicy, value.SourceBuildingId)).ToArray();
            var sourceLayout = baseSnapshot.BattlefieldLayout;
            var longTripLayout = new MatchBattlefieldLayoutConfig(sourceLayout.ReferenceWidth, sourceLayout.ReferenceHeight,
                sourceLayout.Zones, sourceLayout.Routes, sourceLayout.ResourceNodes, sourceLayout.BossSpawns,
                sourceLayout.MinimumRoadWidth, sourceLayout.ActivationWaves, longTripConfigs, 250);
            var snapshot = SchemaV5TestSnapshotFactory.Create(layout: longTripLayout);
            var economy = new EconomySystem(snapshot);
            var nodes = new ResourceNodeSystem(snapshot);
            var gatherers = new PlayerGathererSystem(snapshot.BattlefieldLayout.Gatherers, economy, nodes,
                snapshot.Combat.PlayerWall.Gate, null, snapshot.BattlefieldLayout);
            foreach (var system in new GameSystemBase[] { economy, nodes, gatherers })
                await system.InitializeAsync(new GameContext("overlapping-gatherer-sources"), CancellationToken.None);

            Assert.That(gatherers.GetSnapshot().Count, Is.EqualTo(1));
            for (var tick = 1; tick <= 1000; tick++) gatherers.SimulateTick(tick);
            Assert.That(gatherers.GetSnapshot().Count(value => value.SourceId.Value == "gatherer-source.wall.universal"), Is.EqualTo(5));
            Assert.That(gatherers.GetSnapshot().Count, Is.EqualTo(5), "No per-source, in-flight or global cap may suppress dispatch.");
        }

        [Test]
        public async Task GatheringLodge_ResourceShortageRequiresManualResumeAndFreezesOnlyItsClock()
        {
            var snapshot = await CreateSnapshot(9001);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("gathering-lodge");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            var food = new ResourceId("resource.food");
            Assert.That(runtime.Economy.TryAdd(food, 3, out _), Is.True,
                "Specialist gatherers are paid sources and need an atomic dispatch budget.");
            Assert.That(runtime.Hand.TryPlayBuilding(new CardId("card.building.gatherer-lodge"), 0), Is.True);
            var instanceId = runtime.Buildings.GetSnapshot().Single(value => value.BuildingId?.Value == "building.gatherer-lodge").InstanceId;

            runtime.ResourceNodes.SimulateTick(0);
            runtime.Buildings.SimulateTick(0);
            runtime.PlayerGatherers.SimulateTick(0);
            var first = runtime.PlayerGatherers.GetSnapshot().Single(value => value.BuildingInstanceId == instanceId);
            var upperY = snapshot.BattlefieldLayout.Routes.Single(value => value.Id.Value == "route.upper").Points[0].Y;
            Assert.That(first.SourceId.Value, Is.EqualTo($"gatherer-source.building.{instanceId}"));
            Assert.That(Math.Abs(first.Y - upperY), Is.LessThanOrEqualTo(3),
                "Top-row lodge must dispatch through the upper route gate before its first movement step.");

            for (var tick = 1; tick <= 180; tick++)
            {
                runtime.ResourceNodes.SimulateTick(tick);
                runtime.Buildings.SimulateTick(tick);
                runtime.PlayerGatherers.SimulateTick(tick);
            }
            Assert.That(runtime.PlayerGatherers.GetSnapshot().Count(value => value.BuildingInstanceId == instanceId),
                Is.GreaterThanOrEqualTo(2), "The second worker must leave without waiting for the first trip to complete.");

            for (var tick = 181; tick < 360; tick++)
            {
                runtime.ResourceNodes.SimulateTick(tick);
                runtime.Buildings.SimulateTick(tick);
                runtime.PlayerGatherers.SimulateTick(tick);
            }
            var availableBeforeShortage = runtime.Economy.GetAvailable(food);
            if (availableBeforeShortage > 0)
                Assert.That(runtime.Economy.TryExchange(new[] { new ResourceAmount(food, availableBeforeShortage) }, null, out _), Is.True);
            runtime.Buildings.SimulateTick(360);
            runtime.PlayerGatherers.SimulateTick(360);
            const int pauseTick = 361;
            var paused = runtime.Buildings.GetSnapshot().Single(value => value.InstanceId == instanceId);
            Assert.That(paused.Paused, Is.True);
            Assert.That(paused.BlockReason, Is.EqualTo(ProductionBlockReason.MissingInput));
            var sourceId = new GathererSourceId($"gatherer-source.building.{instanceId}");
            var dispatchesAtPause = runtime.PlayerGatherers.GetSourceEconomySnapshot().Single(value => value.SourceId.Equals(sourceId)).DispatchCount;
            Assert.That(runtime.Economy.TryAdd(food, 3, out _), Is.True);
            for (var tick = pauseTick; tick < pauseTick + 100; tick++) runtime.PlayerGatherers.SimulateTick(tick);
            Assert.That(runtime.Buildings.GetSnapshot().Single(value => value.InstanceId == instanceId).Paused, Is.True,
                "Inventory recovery must not resume a resource-shortage latch.");
            Assert.That(runtime.PlayerGatherers.GetSourceEconomySnapshot().Single(value => value.SourceId.Equals(sourceId)).DispatchCount,
                Is.EqualTo(dispatchesAtPause));
            Assert.That(runtime.Buildings.TryResumeAfterResourceShortage(instanceId), Is.True);
            for (var tick = pauseTick + 100; tick < pauseTick + 220; tick++) runtime.PlayerGatherers.SimulateTick(tick);
            Assert.That(runtime.PlayerGatherers.GetSourceEconomySnapshot().Single(value => value.SourceId.Equals(sourceId)).DispatchCount,
                Is.GreaterThan(dispatchesAtPause), "Manual resume must preserve the frozen dispatch remainder without catch-up dispatches.");
            Assert.That(runtime.Buildings.Demolish(instanceId), Is.True);
            runtime.PlayerGatherers.SimulateTick(pauseTick + 221);
            Assert.That(runtime.PlayerGatherers.GetSnapshot().Any(value => value.BuildingInstanceId == instanceId), Is.False);
            foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [Test]
        public async Task Gatherer_IsDamageableAndDeathDoesNotResetSourceDispatchClock()
        {
            var snapshot = await CreateSnapshot(9004);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("gatherer-damage");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            var victim = runtime.EnemyGatherers.GetSnapshot().First();
            Assert.That(runtime.EnemyGatherers.TryDamage(victim.Id, 1), Is.True);
            var damaged = runtime.EnemyGatherers.GetSnapshot().Single(value => value.Id == victim.Id);
            Assert.That(damaged.Health, Is.EqualTo(victim.Health - 1));
            Assert.That(damaged.DamageRevision, Is.EqualTo(victim.DamageRevision + 1));
            Assert.That(runtime.EnemyGatherers.Kill(victim.Id), Is.True);
            Assert.That(runtime.EnemyGatherers.GetSnapshot().Any(value => value.Id == victim.Id), Is.False);
            runtime.EnemyGatherers.SimulateTick(149);
            Assert.That(runtime.EnemyGatherers.GetSnapshot().Any(value => value.SourceId.Equals(victim.SourceId)), Is.False);
            for (var tick = 150; tick <= 200 && !runtime.EnemyGatherers.GetSnapshot().Any(value => value.SourceId.Equals(victim.SourceId)); tick++)
                runtime.EnemyGatherers.SimulateTick(tick);
            var replacement = runtime.EnemyGatherers.GetSnapshot().Single(value => value.SourceId.Equals(victim.SourceId));
            Assert.That(replacement.Id, Is.Not.EqualTo(victim.Id));
            foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [Test]
        public async Task Archer_FiresTravelingArrowThatDamagesAndRecyclesOnHit()
        {
            var snapshot = await CreateSnapshot(9002);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("archer-projectile");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.archer-camp"), out _), Is.True);
            Assert.That(runtime.EnemyBuildings.GetSnapshot().Any(value => value.BuildingId?.Value == "building.shield-camp"), Is.True);
            var archer = snapshot.Units.Single(value => value.Id.Value == "unit.archer");
            var shield = snapshot.Units.Single(value => value.Id.Value == "unit.shield-guard");
            foreach (var cost in archer.TrainingCosts) Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
            foreach (var cost in shield.TrainingCosts) Assert.That(runtime.EnemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
            Assert.That(runtime.Training.TryCreateOrder(archer.Id, 1, DeploymentPoint.World(820, 540, 1), out _), Is.EqualTo(TrainingFailure.None));
            Assert.That(runtime.EnemyTraining.TryCreateOrder(shield.Id, 1, DeploymentPoint.World(1536, 540, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick < 120 && runtime.Combat.GetUnits().Count < 2; tick++)
            { runtime.Training.SimulateTick(tick); runtime.EnemyTraining.SimulateTick(tick); }
            Assert.That(runtime.Combat.GetUnits().Count, Is.EqualTo(2));

            var projectileId = 0;
            var lastProgress = 0;
            var sawProgress = false;
            var enemyId = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy).Id;
            for (var tick = 0; tick < 160; tick++)
            {
                runtime.Combat.SimulateTick(tick);
                if (projectileId == 0) projectileId = runtime.Combat.GetProjectiles().FirstOrDefault()?.Id ?? 0;
                var projectile = runtime.Combat.GetProjectiles().FirstOrDefault(value => value.Id == projectileId);
                if (projectile != null)
                {
                    Assert.That(projectile.FlightProgressMilli, Is.GreaterThanOrEqualTo(lastProgress));
                    Assert.That((projectile.OriginX, projectile.OriginY), Is.Not.EqualTo((projectile.TargetX, projectile.TargetY)));
                    lastProgress = projectile.FlightProgressMilli;
                    sawProgress |= lastProgress > 0;
                }
                var enemy = runtime.Combat.GetUnits().FirstOrDefault(value => value.Id == enemyId);
                if (projectileId != 0 && enemy != null && enemy.DamageRevision > 0)
                {
                    Assert.That(runtime.Combat.GetProjectiles().Any(value => value.Id == projectileId), Is.False);
                    Assert.That(enemy.Health, Is.LessThan(enemy.MaxHealth));
                    break;
                }
            }
            Assert.That(projectileId, Is.GreaterThan(0), "The archer never created a traveling arrow.");
            Assert.That(sawProgress, Is.True, "The arrow never published monotonic in-flight progress.");
            Assert.That(runtime.Combat.GetUnits().Single(value => value.Id == enemyId).DamageRevision, Is.GreaterThan(0));
            foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [Test]
        public async Task Fireball_ExplodesAtLastKnownTargetWithoutFriendlyFireOrDuplicateImpact()
        {
            var snapshot = await CreateSnapshot(9012);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("fireball-explosion");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            try
            {
                Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.mage-camp"), out _), Is.True);
                if (!runtime.EnemyBuildings.GetSnapshot().Any(value => value.BuildingId?.Value == "building.ram-camp"))
                {
                    var replacement = runtime.EnemyBuildings.GetSnapshot().First(value =>
                        value.BuildingId.HasValue && value.BuildingId.Value.Value is not
                            ("building.shield-camp" or "building.archer-camp"));
                    Assert.That(runtime.EnemyBuildings.Demolish(replacement.InstanceId), Is.True);
                    Assert.That(runtime.EnemyBuildings.TryBuild(replacement.SlotIndex,
                        new BuildingId("building.ram-camp"), out _), Is.True);
                }

                var mage = snapshot.Units.Single(value => value.Id.Value == "unit.mage");
                var shield = snapshot.Units.Single(value => value.Id.Value == "unit.shield-guard");
                var ram = snapshot.Units.Single(value => value.Id.Value == "unit.siege-ram");
                foreach (var cost in mage.TrainingCosts)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                foreach (var cost in shield.TrainingCosts.Concat(ram.TrainingCosts))
                    Assert.That(runtime.EnemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

                Assert.That(runtime.Training.TryCreateOrder(mage.Id, 1,
                    DeploymentPoint.World(1310, 540, 1), out _), Is.EqualTo(TrainingFailure.None));
                Assert.That(runtime.EnemyTraining.TryCreateOrder(shield.Id, 1,
                    DeploymentPoint.World(1360, 540, 1), out _), Is.EqualTo(TrainingFailure.None));
                Assert.That(runtime.EnemyTraining.TryCreateOrder(ram.Id, 1,
                    DeploymentPoint.World(1370, 540, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick < 260 && runtime.Combat.GetUnits().Count < 3; tick++)
                {
                    runtime.Training.SimulateTick(tick);
                    runtime.EnemyTraining.SimulateTick(tick);
                }
                Assert.That(runtime.Combat.GetUnits().Count, Is.EqualTo(3));

                runtime.Combat.SimulateTick(0);
                var projectile = runtime.Combat.GetProjectiles().Single(value => value.ProjectileKind == UnitProjectileKind.Fireball);
                Assert.That(projectile.PresentationKey.Value, Is.EqualTo("world.projectile.fireball"));
                var target = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy &&
                    value.UnitId.Equals(shield.Id));
                var secondary = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy &&
                    value.UnitId.Equals(ram.Id));
                var friendly = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Player);
                Assert.That(runtime.Combat.TryDamageUnit(target.Id, target.Health), Is.True,
                    "The primary target is deliberately removed while the fireball is in flight.");
                var friendlyHealth = friendly.Health;
                var friendlyRevision = friendly.DamageRevision;
                var secondaryRevision = secondary.DamageRevision;
                for (var tick = 1; tick < 40 && runtime.Combat.GetProjectiles().Any(value => value.Id == projectile.Id); tick++)
                    runtime.Combat.SimulateTick(tick);

                Assert.That(runtime.Combat.GetProjectiles().Any(value => value.Id == projectile.Id), Is.False);
                var secondaryAfter = runtime.Combat.GetUnits().Single(value => value.Id == secondary.Id);
                Assert.That(secondaryAfter.DamageRevision, Is.EqualTo(secondaryRevision + 1),
                    "The last-known-position explosion must apply secondary damage exactly once.");
                var friendlyAfter = runtime.Combat.GetUnits().Single(value => value.Id == friendly.Id);
                Assert.That((friendlyAfter.Health, friendlyAfter.DamageRevision),
                    Is.EqualTo((friendlyHealth, friendlyRevision)), "Explosions must not damage friendly units.");
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task BossFsm_RetaliatesTelegraphsAndAppliesMeteorKnockback()
        {
            var snapshot = await CreateSnapshot(9003);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("boss-fsm");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            var spawn = snapshot.BattlefieldLayout.BossSpawns.First();
            runtime.Boss.SimulateTick(spawn.WarningTick);
            runtime.Boss.SimulateTick(spawn.SpawnTick);
            Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True);
            var shield = snapshot.Units.Single(value => value.Id.Value == "unit.shield-guard");
            foreach (var cost in shield.TrainingCosts) Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
            Assert.That(runtime.Training.TryCreateOrder(shield.Id, 1,
                DeploymentPoint.World(spawn.Position.X, spawn.Position.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick <= shield.TrainingTicks; tick++) runtime.Training.SimulateTick(tick);
            var before = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Player);
            Assert.That(runtime.Boss.TryDamage(spawn.Id, MatchFaction.Player, snapshot.Boss.Armor + 1, before.Id), Is.True);
            Assert.That(runtime.Boss.GetSnapshot().First(value => value.SpawnId == spawn.Id).CombatState,
                Is.EqualTo(BossCombatState.Retaliating));

            var sawTelegraph = false;
            var sawImpact = false;
            for (var tick = spawn.SpawnTick + 1; tick < spawn.SpawnTick + 70; tick++)
            {
                runtime.Boss.SimulateTick(tick);
                sawTelegraph |= runtime.Boss.GetHazards().Any(value => value.State == BossHazardState.Telegraph);
                sawImpact |= runtime.Boss.GetHazards().Any(value => value.State == BossHazardState.Impact);
                if (sawImpact) break;
            }
            var after = runtime.Combat.GetUnits().Single(value => value.Id == before.Id);
            Assert.That(sawTelegraph, Is.True);
            Assert.That(sawImpact, Is.True);
            Assert.That(after.DamageRevision, Is.GreaterThan(before.DamageRevision));
            Assert.That((after.X, after.Y), Is.Not.EqualTo((before.X, before.Y)));
            foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [Test]
        public void ResultReport_ContainsAllProductRequiredSettlementFacts()
        {
            var config = SchemaV5TestSnapshotFactory.Create();
            var analysis = new MatchAnalysisSnapshot(3725,
                new WallSnapshot(MatchFaction.Player, 3210, 5000),
                new WallSnapshot(MatchFaction.Enemy, 0, 5000),
                new[]
                {
                    new UnitCombatCountSnapshot(MatchFaction.Player, new UnitId("unit.shield-guard"), 5, 2),
                    new UnitCombatCountSnapshot(MatchFaction.Enemy, new UnitId("unit.shield-guard"), 4, 4)
                },
                new[] { new WallDamageSourceSnapshot(MatchFaction.Player, new UnitId("unit.shield-guard"), 5000) },
                new ResourceId("resource.food"), 420, 80, 610, 1, 0, 1, 2, 0, 17,
                MatchFailureCause.None);
            var receipt = new FortressFrontier.Runtime.Progression.SettlementReceipt(new MatchId("report"),
                200, 1400, true, false, FortressFrontier.Runtime.Progression.SettlementStatus.Success);

            var report = MatchResultReportFormatter.Format(config, receipt, true, analysis);

            foreach (var label in new[] { "战场：", "局时：", "Boss归属", "城墙：", "交换：", "城墙伤害主力：",
                         "首次入库：", "首次敌军压力：", "平均压力间隔：", "断点：", "最长部署空窗", "敌方意图占比：", "储备保护延迟", "压力循环：", "连续同意图峰值", "最长无压力", "平均恢复", "战况分析：", "金币明细：", "总金币" })
                Assert.That(report, Does.Contain(label), label);
            Assert.That(report, Does.Contain("完成 100 + 胜利 50 + 首通 50"));
        }

        [Test]
        public async Task MatchAnalytics_SamplesAuthoritativeRuntimeWithoutMutatingIt()
        {
            var snapshot = await CreateSnapshot(706);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("analytics");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            foreach (var id in new[] { "resource.food", "resource.wood", "resource.raw-stone", "resource.iron-ore" })
                Assert.That(runtime.Economy.TryAdd(new ResourceId(id), 100, out _), Is.True);

            runtime.Simulation.AdvanceTicks(610);
            var report = runtime.Analytics.Capture(false);

            Assert.That(report.DurationTicks, Is.EqualTo(610));
            Assert.That(report.MaximumDeploymentGapTicks, Is.EqualTo(610));
            Assert.That(report.FailureCause, Is.EqualTo(MatchFailureCause.DeploymentGap));
            Assert.That(report.EnemyLedgerEntryCount, Is.GreaterThan(0));
            Assert.That(runtime.Simulation.TickCount, Is.EqualTo(610), "Capturing analytics must not advance simulation.");
            foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [Test]
        public async Task TrainedUnit_AdvancesAndDamagesEnemyWall()
        {
            var snapshot = await CreateSnapshot(9);
            var context = new GameContext("combat-test");
            var playerEconomy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var playerCamps = new CampSystem(playerBuildings);
            var enemyCamps = new EnemyCampSystem(enemyBuildings);
            var playerTraining = new TrainingSystem(snapshot, playerEconomy, playerBuildings, playerCamps);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var nodes = new ResourceNodeSystem(snapshot);
            var gatherers = new PlayerGathererSystem(snapshot.BattlefieldLayout.Gatherers, playerEconomy, nodes,
                snapshot.Combat.PlayerWall.Gate);
            var combat = new CombatSystem(snapshot, playerTraining, enemyTraining);
            foreach (var system in new GameSystemBase[] { playerEconomy, enemyEconomy, playerBuildings, enemyBuildings,
                         playerCamps, enemyCamps, playerTraining, enemyTraining, nodes, gatherers, combat })
                await system.InitializeAsync(context, CancellationToken.None);
            Assert.That(playerBuildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True);
            for (var tick = 0; tick < 1000; tick++)
            {
                nodes.SimulateTick(tick);
                gatherers.SimulateTick(tick);
            }
            var shield = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
            foreach (var cost in shield.TrainingCosts)
                Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
            
Assert.That(playerTraining.TryCreateOrder(new UnitId("unit.shield-guard"), 1, new DeploymentPoint(1, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick < 80; tick++) playerTraining.SimulateTick();
            Assert.That(combat.GetUnits().Count, Is.EqualTo(1));
            for (var tick = 0; tick < 400; tick++) combat.SimulateTick(tick);
            Assert.That(combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy).Health, Is.LessThan(5000));
            Assert.That(combat.GetCombatCounts().Single(value => value.Faction == MatchFaction.Player).Spawned, Is.EqualTo(1));
            
            Assert.That(combat.GetUnits().Single().AttackRevision, Is.GreaterThan(0),
                "Wall attacks must publish presentation-only attack revisions.");
Assert.That(combat.GetWallDamageSources().Single(value => value.Attacker == MatchFaction.Player).Damage, Is.GreaterThan(0));
        }

        [Test]
        public async Task CrowdedOpposingUnits_SeparateAlliesAndAcquireEnemiesWithoutPhysics()
        {
            var snapshot = await CreateSnapshot(10);
            var context = new GameContext("crowd-combat-test");
            var playerEconomy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var playerCamps = new CampSystem(playerBuildings);
            var enemyCamps = new EnemyCampSystem(enemyBuildings);
            var playerTraining = new TrainingSystem(snapshot, playerEconomy, playerBuildings, playerCamps);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var combat = new CombatSystem(snapshot, playerTraining, enemyTraining);
            var systems = new GameSystemBase[] { playerEconomy, enemyEconomy, playerBuildings, enemyBuildings,
                playerCamps, enemyCamps, playerTraining, enemyTraining, combat };
            foreach (var system in systems) await system.InitializeAsync(context, CancellationToken.None);

            var campId = new BuildingId("building.shield-camp");
            var unitId = new UnitId("unit.shield-guard");
            Assert.That(playerBuildings.TryBuild(0, campId, out _), Is.True);
            Assert.That(enemyBuildings.TryBuild(0, campId, out _), Is.True);
            var unit = snapshot.Combat.Units.Single(value => value.Id.Equals(unitId));
            foreach (var cost in unit.TrainingCosts)
            {
                Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount * 3, out _), Is.True);
                Assert.That(enemyEconomy.TryAdd(cost.ResourceId, cost.Amount * 3, out _), Is.True);
            }
            Assert.That(playerTraining.TryCreateOrder(unitId, 3, DeploymentPoint.World(720, 540, 1), out _), Is.EqualTo(TrainingFailure.None));
            Assert.That(enemyTraining.TryCreateOrder(unitId, 3, DeploymentPoint.World(790, 540, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick < 400 && combat.GetUnits().Count < 6; tick++)
            { playerTraining.SimulateTick(tick); enemyTraining.SimulateTick(tick); }
            Assert.That(combat.GetUnits().Count, Is.EqualTo(6));

            combat.SimulateTick(0);
            var initiallyLocked = combat.GetUnits().Where(value => value.LockedTargetId != 0)
                .ToDictionary(value => value.Id, value => value.LockedTargetId);
            Assert.That(initiallyLocked, Is.Not.Empty, "Units in acquisition range did not lock a combat target.");
            Assert.That(combat.GetUnits().Where(value => value.LockedTargetId != 0)
                .All(value => value.LockedTargetKind == CombatTargetKind.Unit &&
                              value.LockedTargetKey == $"unit:{value.LockedTargetId}"), Is.True);
            for (var tick = 1; tick < 8; tick++)
            {
                combat.SimulateTick(tick);
                var current = combat.GetUnits().ToDictionary(value => value.Id);
                foreach (var pair in initiallyLocked.ToArray())
                {
                    if (!current.TryGetValue(pair.Key, out var source) || !current.ContainsKey(pair.Value))
                    { initiallyLocked.Remove(pair.Key); continue; }
                    Assert.That(source.LockedTargetId, Is.EqualTo(pair.Value),
                        $"Unit {pair.Key} switched away from a still-valid locked target {pair.Value}.");
                }
            }
            for (var tick = 8; tick < 30; tick++) combat.SimulateTick(tick);
            var units = combat.GetUnits();
            foreach (var faction in new[] { MatchFaction.Player, MatchFaction.Enemy })
            {
                var allies = units.Where(value => value.Faction == faction).OrderBy(value => value.Id).ToArray();
                Assert.That(allies.Select(value => (value.X, value.Y)).Distinct().Count(), Is.EqualTo(allies.Length),
                    $"{faction} units remained stacked after deterministic separation.");
                Assert.That(allies.All(value => value.Lane == 1), Is.True);
            }
            
            Assert.That(units.Any(value => value.AttackRevision > 0), Is.True,
                string.Join("; ", units.Select(value => $"{value.Id}:{value.State}@({value.X},{value.Y})->{value.LockedTargetKey}")));
            Assert.That(units.Any(value => value.Health < value.MaxHealth && value.DamageRevision > 0), Is.True,
                string.Join("; ", units.Select(value => $"{value.Id}:{value.Health}/{value.MaxHealth}:{value.State}")));

            var hitAudioEvents = new List<UnitHitAudioEvent>();
            combat.UnitHit += hitAudioEvents.Add;
            var directTarget = units.First(value => value.Health > 1);
            var directRevision = directTarget.DamageRevision;
            var eventCountBeforeDirectHit = hitAudioEvents.Count;
            Assert.That(combat.TryDamageUnit(directTarget.Id, 1), Is.True);
            Assert.That(combat.GetUnits().Single(value => value.Id == directTarget.Id).DamageRevision, Is.EqualTo(directRevision + 1));
            Assert.That(hitAudioEvents.Count, Is.EqualTo(eventCountBeforeDirectHit + 1));
            Assert.That(hitAudioEvents[^1].Killed, Is.False);

            var enemyBeforeArea = combat.GetUnits().Where(value => value.Faction == MatchFaction.Enemy)
                .ToDictionary(value => value.Id, value => value.DamageRevision);
            var eventCountBeforeArea = hitAudioEvents.Count;
            var areaHits = combat.ApplyAreaDamage(MatchFaction.Enemy, 1);
            Assert.That(areaHits, Is.EqualTo(enemyBeforeArea.Count));
            Assert.That(hitAudioEvents.Count, Is.EqualTo(eventCountBeforeArea + areaHits));
            foreach (var target in combat.GetUnits().Where(value => value.Faction == MatchFaction.Enemy))
                Assert.That(target.DamageRevision, Is.EqualTo(enemyBeforeArea[target.Id] + 1));
Assert.That(units.Any(value => value.Health < value.MaxHealth), Is.True,
                "Opposing units in the same lane never acquired or damaged one another.");

            var lethalTarget = combat.GetUnits().First();
            var eventCountBeforeLethalHit = hitAudioEvents.Count;
            Assert.That(combat.TryDamageUnit(lethalTarget.Id, int.MaxValue), Is.True);
            Assert.That(hitAudioEvents.Count, Is.EqualTo(eventCountBeforeLethalHit + 1));
            Assert.That(hitAudioEvents[^1].Killed, Is.True);

            foreach (var system in systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
        }

        [TestCase(false, 0L, 28, 360, CombatUnitState.Advancing)]
        [TestCase(true, 784L, 28, 360, CombatUnitState.Attacking)]
        [TestCase(true, 785L, 28, 360, CombatUnitState.Pursuing)]
        [TestCase(true, 129600L, 28, 360, CombatUnitState.Pursuing)]
        [TestCase(true, 129601L, 28, 360, CombatUnitState.Advancing)]
        public void CombatUnitStateMachine_UsesExclusiveDistanceBands(bool hasValidTarget,
            long distanceSquared, int attackRange, int chaseRadius, CombatUnitState expected)
        {
            Assert.That(CombatUnitStateMachine.Resolve(hasValidTarget, distanceSquared, attackRange, chaseRadius),
                Is.EqualTo(expected));
        }

        [Test]
        public async Task SameXDifferentYAndDifferentRoutes_UnitsCloseInTwoDimensionsAndFight()
        {
            var snapshot = await CreateSnapshot(20260821);
            var context = new GameContext("two-dimensional-unit-ai-test");
            var playerEconomy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var playerCamps = new CampSystem(playerBuildings);
            var enemyCamps = new EnemyCampSystem(enemyBuildings);
            var playerTraining = new TrainingSystem(snapshot, playerEconomy, playerBuildings, playerCamps);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var combat = new CombatSystem(snapshot, playerTraining, enemyTraining);
            var systems = new GameSystemBase[] { playerEconomy, enemyEconomy, playerBuildings, enemyBuildings,
                playerCamps, enemyCamps, playerTraining, enemyTraining, combat };
            foreach (var system in systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                var unitId = new UnitId("unit.shield-guard");
                var unit = snapshot.Combat.Units.Single(value => value.Id.Equals(unitId));
                Assert.That(playerBuildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True,
                    "Player shield camp setup failed.");
                Assert.That(enemyBuildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True,
                    "Enemy shield camp setup failed.");
                foreach (var cost in unit.TrainingCosts)
                {
                    Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True,
                        $"Player resource setup failed: {cost.ResourceId.Value}.");
                    Assert.That(enemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True,
                        $"Enemy resource setup failed: {cost.ResourceId.Value}.");
                }

                Assert.That(playerTraining.TryCreateOrder(unitId, 1,
                    DeploymentPoint.World(1000, 810, 2), out _), Is.EqualTo(TrainingFailure.None));
                Assert.That(enemyTraining.TryCreateOrder(unitId, 1,
                    DeploymentPoint.World(1000, 717, 0), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= unit.TrainingTicks; tick++)
                {
                    playerTraining.SimulateTick(tick);
                    enemyTraining.SimulateTick(tick);
                }

                var before = combat.GetUnits().OrderBy(value => value.Id).ToArray();
                Assert.That(before, Has.Length.EqualTo(2));
                Assert.That(before[0].X, Is.EqualTo(before[1].X));
                Assert.That(before[0].Lane, Is.Not.EqualTo(before[1].Lane));
                var initialDistanceSquared = (long)(before[0].X - before[1].X) * (before[0].X - before[1].X) +
                                             (long)(before[0].Y - before[1].Y) * (before[0].Y - before[1].Y);

                combat.SimulateTick(0);
                var locked = combat.GetUnits().OrderBy(value => value.Id).ToArray();
                Assert.That(locked.All(value => value.LockedTargetKind == CombatTargetKind.Unit), Is.True,
                    string.Join("; ", locked.Select(value => $"{value.Id}:{value.LockedTargetKey}")));
                Assert.That(locked.All(value => value.State == CombatUnitState.Pursuing), Is.True,
                    string.Join("; ", locked.Select(value => $"{value.Id}:{value.State}@({value.X},{value.Y})")));

                for (var tick = 1; tick < 60; tick++) combat.SimulateTick(tick);
                var after = combat.GetUnits().OrderBy(value => value.Id).ToArray();
                Assert.That(after, Has.Length.EqualTo(2));
                var finalDistanceSquared = (long)(after[0].X - after[1].X) * (after[0].X - after[1].X) +
                                           (long)(after[0].Y - after[1].Y) * (after[0].Y - after[1].Y);
                Assert.That(finalDistanceSquared, Is.LessThan(initialDistanceSquared));
                Assert.That(after.Any(value => value.AttackRevision > 0), Is.True,
                    string.Join("; ", after.Select(value => $"{value.Id}:{value.State}:attack={value.AttackRevision}")));
                Assert.That(after.Any(value => value.Health < value.MaxHealth), Is.True,
                    string.Join("; ", after.Select(value => $"{value.Id}:{value.Health}/{value.MaxHealth}")));

                foreach (var deployed in after) Assert.That(combat.TryDamageUnit(deployed.Id, int.MaxValue), Is.True);
                combat.SimulateTick(60);
                Assert.That(combat.GetUnits(), Is.Empty);
                foreach (var cost in unit.TrainingCosts)
                {
                    Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                    Assert.That(enemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                }
                Assert.That(playerTraining.TryCreateOrder(unitId, 1,
                    DeploymentPoint.World(1000, 700, 0), out _), Is.EqualTo(TrainingFailure.None));
                Assert.That(enemyTraining.TryCreateOrder(unitId, 1,
                    DeploymentPoint.World(1028, 704, 2), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 61; tick <= 61 + unit.TrainingTicks; tick++)
                {
                    playerTraining.SimulateTick(tick);
                    enemyTraining.SimulateTick(tick);
                }

                var boundaryBefore = combat.GetUnits().OrderBy(value => value.Id).ToArray();
                Assert.That(boundaryBefore, Has.Length.EqualTo(2));
                Assert.That((long)(boundaryBefore[0].X - boundaryBefore[1].X) * (boundaryBefore[0].X - boundaryBefore[1].X) +
                            (long)(boundaryBefore[0].Y - boundaryBefore[1].Y) * (boundaryBefore[0].Y - boundaryBefore[1].Y),
                    Is.EqualTo(800), "The regression requires distance sqrt(800)=28.28, just outside AttackRange 28.");
                combat.SimulateTick(100);
                var boundaryMoved = combat.GetUnits().OrderBy(value => value.Id).ToArray();
                Assert.That(boundaryMoved.Select(value => (value.X, value.Y)),
                    Is.Not.EquivalentTo(boundaryBefore.Select(value => (value.X, value.Y))),
                    "A non-square distance just outside AttackRange must still produce pursuit movement.");
                combat.SimulateTick(101);
                var boundaryAttacked = combat.GetUnits().OrderBy(value => value.Id).ToArray();
                Assert.That(boundaryAttacked.Any(value => value.AttackRevision > 0), Is.True);
                Assert.That(boundaryAttacked.Any(value => value.Health < value.MaxHealth), Is.True);
            }
            finally
            {
                foreach (var system in systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task UnitAttackingWall_DoesNotSwitchToLaterEnemyUnit()
        {
            var snapshot = await CreateSnapshot(711);
            var context = new GameContext("wall-lock-test");
            var playerEconomy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var playerCamps = new CampSystem(playerBuildings);
            var enemyCamps = new EnemyCampSystem(enemyBuildings);
            var playerTraining = new TrainingSystem(snapshot, playerEconomy, playerBuildings, playerCamps);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var combat = new CombatSystem(snapshot, playerTraining, enemyTraining);
            var systems = new GameSystemBase[] { playerEconomy, enemyEconomy, playerBuildings, enemyBuildings,
                playerCamps, enemyCamps, playerTraining, enemyTraining, combat };
            foreach (var system in systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                var campId = new BuildingId("building.shield-camp");
                var unitId = new UnitId("unit.shield-guard");
                var unit = snapshot.Combat.Units.Single(value => value.Id.Equals(unitId));
                Assert.That(playerBuildings.TryBuild(0, campId, out _), Is.True);
                Assert.That(enemyBuildings.TryBuild(0, campId, out _), Is.True);
                foreach (var cost in unit.TrainingCosts)
                {
                    Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                    Assert.That(enemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                }

                var wall = snapshot.Combat.PlayerWall.Gate;
                Assert.That(enemyTraining.TryCreateOrder(unitId, 1,
                    DeploymentPoint.World(wall.X + 10, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= unit.TrainingTicks; tick++) enemyTraining.SimulateTick(tick);
                combat.SimulateTick(0);

                var wallAfterFirstAttack = combat.GetWalls().Single(value => value.Faction == MatchFaction.Player).Health;
                Assert.That(wallAfterFirstAttack, Is.LessThan(snapshot.Combat.PlayerWall.MaxHealth));
                var wallAttacker = combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy);
                Assert.That(wallAttacker.LockedTargetId, Is.Zero);
                Assert.That(wallAttacker.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall));
                Assert.That(wallAttacker.LockedTargetKey, Does.StartWith("wall:"));

                Assert.That(playerTraining.TryCreateOrder(unitId, 1,
                    DeploymentPoint.World(wall.X + 200, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= unit.TrainingTicks; tick++) playerTraining.SimulateTick(tick);
                var playerBefore = combat.GetUnits().Single(value => value.Faction == MatchFaction.Player);

                for (var tick = 1; tick <= unit.AttackIntervalTicks; tick++) combat.SimulateTick(tick);

                var enemyAfter = combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy);
                var playerAfter = combat.GetUnits().Single(value => value.Id == playerBefore.Id);
                var wallAfterSecondAttack = combat.GetWalls().Single(value => value.Faction == MatchFaction.Player).Health;
                Assert.That(enemyAfter.LockedTargetId, Is.Zero,
                    "An existing wall attack must not be replaced by a later unit target.");
                Assert.That(playerAfter.Health, Is.EqualTo(playerBefore.Health));
                Assert.That(wallAfterSecondAttack, Is.LessThan(wallAfterFirstAttack));
            }
            finally
            {
                foreach (var system in systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task ArcherHit_InterruptsWallLockAndVictimRetaliatesAgainstAttacker()
        {
            var snapshot = await CreateSnapshot(713);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("archer-retaliation-test");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                var archer = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.archer");
                var shield = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
                foreach (var gatherer in runtime.PlayerGatherers.GetSnapshot().ToArray())
                    Assert.That(runtime.PlayerGatherers.Kill(gatherer.Id), Is.True);
                Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.archer-camp"), out _), Is.True);
                Assert.That(runtime.EnemyBuildings.GetSnapshot()
                    .Any(value => value.BuildingId?.Value == "building.shield-camp"), Is.True);
                foreach (var cost in archer.TrainingCosts)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                foreach (var cost in shield.TrainingCosts)
                    Assert.That(runtime.EnemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

                var wall = snapshot.Combat.PlayerWall.Gate;
                Assert.That(runtime.EnemyTraining.TryCreateOrder(shield.Id, 1,
                    DeploymentPoint.World(wall.X + 10, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= shield.TrainingTicks; tick++) runtime.EnemyTraining.SimulateTick(tick);
                runtime.Combat.SimulateTick(0);
                var victim = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy);
                Assert.That(victim.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall));

                Assert.That(runtime.Training.TryCreateOrder(archer.Id, 1,
                    DeploymentPoint.World(wall.X + 150, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= archer.TrainingTicks; tick++) runtime.Training.SimulateTick(tick);
                var attacker = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Player);

                var sawRetaliationLock = false;
                var sawReturnDamage = false;
                for (var tick = 1; tick < 100; tick++)
                {
                    runtime.Combat.SimulateTick(tick);
                    var units = runtime.Combat.GetUnits();
                    var currentVictim = units.FirstOrDefault(value => value.Id == victim.Id);
                    var currentAttacker = units.FirstOrDefault(value => value.Id == attacker.Id);
                    if (currentVictim != null && currentVictim.DamageRevision > victim.DamageRevision)
                    {
                        sawRetaliationLock |= currentVictim.LockedTargetKind == CombatTargetKind.Unit &&
                                              currentVictim.LockedTargetId == attacker.Id;
                    }
                    sawReturnDamage |= currentAttacker != null && currentAttacker.DamageRevision > attacker.DamageRevision;
                    if (sawRetaliationLock && sawReturnDamage) break;
                }

                Assert.That(sawRetaliationLock, Is.True,
                    "A direct archer hit did not replace the victim's wall lock with the attacking unit.");
                Assert.That(sawReturnDamage, Is.True, "The wall attacker locked the archer but never returned damage.");
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task TowerHit_InterruptsWallApproachAndUnitDamagesTower()
        {
            var snapshot = await CreateSnapshot(714);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("tower-retaliation-test");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                foreach (var gatherer in runtime.EnemyGatherers.GetSnapshot().ToArray())
                    Assert.That(runtime.EnemyGatherers.Kill(gatherer.Id), Is.True);
                var archer = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
                Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True);
                foreach (var cost in archer.TrainingCosts)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

                var buildable = snapshot.BattlefieldLayout.Zones.Single(value => value.Kind == ZoneKind.TowerBuildable);
                var towerX = buildable.X + buildable.Width - 200;
                var towerY = buildable.Y + 20;
                var wall = snapshot.Combat.EnemyWall.Gate;
                Assert.That(runtime.Training.TryCreateOrder(archer.Id, 1,
                    DeploymentPoint.World(wall.X - 48, towerY, 0), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= archer.TrainingTicks; tick++) runtime.Training.SimulateTick(tick);
                runtime.Combat.SimulateTick(0);
                var unitBeforeHit = runtime.Combat.GetUnits().Single();
                Assert.That(unitBeforeHit.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall),
                    "A wall target is a vertical surface, so gate-center Y must not affect wall range.");
                Assert.That(unitBeforeHit.State, Is.EqualTo(CombatUnitState.Attacking));

                foreach (var cost in snapshot.Construction.Costs)
                    Assert.That(runtime.EnemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                Assert.That(runtime.EnemyHand.TryGrantPublicCard(new CardId("card.battlefield.arrow-tower")), Is.True);
                Assert.That(runtime.EnemyConstruction.TryStartSite(towerX, towerY, out var towerId),
                    Is.EqualTo(TowerConstructionFailure.None));
                for (var tick = 0; tick < 400 && runtime.EnemyConstruction.GetTowers().Count == 0; tick++)
                    runtime.EnemyConstruction.SimulateTick(tick);
                Assert.That(runtime.EnemyConstruction.GetTowers().Single().Id, Is.EqualTo(towerId));

                var sawTowerLock = false;
                var sawTowerDamage = false;
                var lastSeen = unitBeforeHit;
                for (var tick = 1; tick < 120; tick++)
                {
                    runtime.Combat.SimulateTick(tick);
                    var current = runtime.Combat.GetUnits().FirstOrDefault(value => value.Id == unitBeforeHit.Id);
                    if (current != null) lastSeen = current;
                    sawTowerLock |= current != null && current.DamageRevision > unitBeforeHit.DamageRevision &&
                                    current.LockedTargetKind == CombatTargetKind.Tower &&
                                    current.LockedTargetKey == $"tower:{towerId}";
                    var tower = runtime.EnemyConstruction.GetTowers().FirstOrDefault(value => value.Id == towerId);
                    sawTowerDamage |= tower != null && tower.Health < tower.MaxHealth;
                    if (sawTowerLock && sawTowerDamage) break;
                }

                Assert.That(sawTowerLock, Is.True,
                    "An enemy tower hit did not replace the archer's wall lock with the attacking tower.");
                var finalUnit = runtime.Combat.GetUnits().FirstOrDefault(value => value.Id == unitBeforeHit.Id);
                var finalTower = runtime.EnemyConstruction.GetTowers().FirstOrDefault(value => value.Id == towerId);
                Assert.That(sawTowerDamage, Is.True,
                    $"The unit locked the attacking tower but never damaged it. " +
                    $"Unit={finalUnit?.State}@({finalUnit?.X},{finalUnit?.Y})->{finalUnit?.LockedTargetKey}; " +
                    $"Last={lastSeen.State}@({lastSeen.X},{lastSeen.Y})->{lastSeen.LockedTargetKey}, attack={lastSeen.AttackRevision}; " +
                    $"Tower=({finalTower?.X},{finalTower?.Y}) {finalTower?.Health}/{finalTower?.MaxHealth}.");
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task SameTickArcherHits_KeepFirstStableRetaliationTarget()
        {
            var snapshot = await CreateSnapshot(715);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("stable-retaliation-source-test");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                foreach (var gatherer in runtime.PlayerGatherers.GetSnapshot().ToArray())
                    Assert.That(runtime.PlayerGatherers.Kill(gatherer.Id), Is.True);
                var archer = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.archer");
                var shield = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
                Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.archer-camp"), out _), Is.True);
                foreach (var cost in archer.TrainingCosts)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount * 2, out _), Is.True);
                foreach (var cost in shield.TrainingCosts)
                    Assert.That(runtime.EnemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

                var wall = snapshot.Combat.PlayerWall.Gate;
                Assert.That(runtime.EnemyTraining.TryCreateOrder(shield.Id, 1,
                    DeploymentPoint.World(wall.X + 10, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= shield.TrainingTicks; tick++) runtime.EnemyTraining.SimulateTick(tick);
                runtime.Combat.SimulateTick(0);
                var victim = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy);
                Assert.That(victim.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall));

                Assert.That(runtime.Training.TryCreateOrder(archer.Id, 2,
                    DeploymentPoint.World(wall.X + 150, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick < 400 && runtime.Combat.GetUnits().Count(value => value.Faction == MatchFaction.Player) < 2; tick++)
                    runtime.Training.SimulateTick(tick);
                var attackers = runtime.Combat.GetUnits().Where(value => value.Faction == MatchFaction.Player)
                    .OrderBy(value => value.Id).ToArray();
                Assert.That(attackers.Length, Is.EqualTo(2));

                CombatUnitSnapshot damaged = null;
                for (var tick = 1; tick < 80; tick++)
                {
                    runtime.Combat.SimulateTick(tick);
                    damaged = runtime.Combat.GetUnits().FirstOrDefault(value => value.Id == victim.Id);
                    if (damaged != null && damaged.DamageRevision >= victim.DamageRevision + 2) break;
                }

                Assert.That(damaged, Is.Not.Null);
                Assert.That(damaged.DamageRevision, Is.GreaterThanOrEqualTo(victim.DamageRevision + 2));
                Assert.That(damaged.LockedTargetKind, Is.EqualTo(CombatTargetKind.Unit));
                Assert.That(damaged.LockedTargetId, Is.EqualTo(attackers[0].Id),
                    "A later same-tick projectile replaced the first stable retaliation target.");
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task ProjectileWhoseSourceDiesBeforeImpact_DoesNotCreateRetaliationLock()
        {
            var snapshot = await CreateSnapshot(716);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("invalid-retaliation-source-test");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                foreach (var gatherer in runtime.PlayerGatherers.GetSnapshot().ToArray())
                    Assert.That(runtime.PlayerGatherers.Kill(gatherer.Id), Is.True);
                var archer = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.archer");
                var shield = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
                Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.archer-camp"), out _), Is.True);
                foreach (var cost in archer.TrainingCosts)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                foreach (var cost in shield.TrainingCosts)
                    Assert.That(runtime.EnemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

                var wall = snapshot.Combat.PlayerWall.Gate;
                Assert.That(runtime.EnemyTraining.TryCreateOrder(shield.Id, 1,
                    DeploymentPoint.World(wall.X + 10, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= shield.TrainingTicks; tick++) runtime.EnemyTraining.SimulateTick(tick);
                runtime.Combat.SimulateTick(0);
                var victim = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy);
                Assert.That(victim.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall));

                Assert.That(runtime.Training.TryCreateOrder(archer.Id, 1,
                    DeploymentPoint.World(wall.X + 150, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= archer.TrainingTicks; tick++) runtime.Training.SimulateTick(tick);
                var attacker = runtime.Combat.GetUnits().Single(value => value.Faction == MatchFaction.Player);
                runtime.Combat.SimulateTick(1);
                Assert.That(runtime.Combat.GetProjectiles(), Is.Not.Empty);
                Assert.That(runtime.Combat.TryDamageUnit(attacker.Id, int.MaxValue), Is.True);

                CombatUnitSnapshot damaged = null;
                for (var tick = 2; tick < 80; tick++)
                {
                    runtime.Combat.SimulateTick(tick);
                    damaged = runtime.Combat.GetUnits().FirstOrDefault(value => value.Id == victim.Id);
                    if (damaged != null && damaged.DamageRevision > victim.DamageRevision) break;
                }

                Assert.That(damaged, Is.Not.Null);
                Assert.That(damaged.DamageRevision, Is.GreaterThan(victim.DamageRevision));
                Assert.That(damaged.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall));
                Assert.That(damaged.LockedTargetId, Is.Zero);
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task SiegeRam_IgnoresUnitsAndLocksWallAsStructureTarget()
        {
            var snapshot = await CreateSnapshot(712);
            var context = new GameContext("ram-wall-lock-test");
            var playerEconomy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var playerCamps = new CampSystem(playerBuildings);
            var enemyCamps = new EnemyCampSystem(enemyBuildings);
            var playerTraining = new TrainingSystem(snapshot, playerEconomy, playerBuildings, playerCamps);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var combat = new CombatSystem(snapshot, playerTraining, enemyTraining);
            var systems = new GameSystemBase[] { playerEconomy, enemyEconomy, playerBuildings, enemyBuildings,
                playerCamps, enemyCamps, playerTraining, enemyTraining, combat };
            foreach (var system in systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                var ram = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.siege-ram");
                var shield = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
                Assert.That(playerBuildings.TryBuild(0, new BuildingId("building.ram-camp"), out _), Is.True);
                Assert.That(enemyBuildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True);
                foreach (var cost in ram.TrainingCosts)
                    Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                foreach (var cost in shield.TrainingCosts)
                    Assert.That(enemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

                var wall = snapshot.Combat.EnemyWall.Gate;
                var enemySurface = snapshot.BattlefieldLayout.Routes[0].Points[^1].X;
                Assert.That(playerTraining.TryCreateOrder(ram.Id, 1,
                    DeploymentPoint.World(enemySurface - ram.CollisionRadius - ram.AttackRange, wall.Y, 1), out _),
                    Is.EqualTo(TrainingFailure.None));
                Assert.That(enemyTraining.TryCreateOrder(shield.Id, 1,
                    DeploymentPoint.World(wall.X - 20, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= Math.Max(ram.TrainingTicks, shield.TrainingTicks); tick++)
                { playerTraining.SimulateTick(tick); enemyTraining.SimulateTick(tick); }

                var enemyBefore = combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy);
                combat.SimulateTick(0);

                var ramAfter = combat.GetUnits().Single(value => value.UnitId.Equals(ram.Id));
                var enemyAfter = combat.GetUnits().Single(value => value.Id == enemyBefore.Id);
                Assert.That(ramAfter.X + ram.CollisionRadius + ram.AttackRange, Is.EqualTo(enemySurface));
                Assert.That(ramAfter.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall));
                Assert.That(ramAfter.LockedTargetId, Is.Zero);
                Assert.That(enemyAfter.Health, Is.EqualTo(enemyBefore.Health));
                Assert.That(combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy).Health,
                    Is.LessThan(snapshot.Combat.EnemyWall.MaxHealth));
            }
            finally
            {
                foreach (var system in systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task EnemyGathererHit_BecomesDelayedPaidLogisticsDefenseOnTheThreatenedRoute()
        {
            var snapshot = await CreateSnapshot(20260822);
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext("logistics-defense");
            foreach (var system in runtime.Systems) await system.InitializeAsync(context, CancellationToken.None);
            try
            {
                Assert.That(runtime.Hand.TryPlayBuilding(new CardId("card.building.archer-camp"), 0), Is.True);
                var gatherer = runtime.EnemyGatherers.GetSnapshot().OrderBy(value => value.Id).First();
                var gathererHitAudio = new List<UnitHitAudioEvent>();
                runtime.Combat.UnitHit += gathererHitAudio.Add;
                var archer = snapshot.Units.Single(value => value.Id.Equals(new UnitId("unit.archer")));
                foreach (var cost in archer.TrainingCosts)
                    Assert.That(runtime.Economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                var routes = snapshot.BattlefieldLayout.Routes
                    .OrderBy(value => value.Points.Count == 0 ? int.MaxValue : value.Points[^1].Y)
                    .ThenBy(value => value.Id.Value, StringComparer.Ordinal).ToArray();
                var lane = Array.FindIndex(routes, value => value.Id.Equals(gatherer.RouteId));
                var point = DeploymentPoint.World(gatherer.X - 40, gatherer.Y, lane);
                Assert.That(runtime.Training.TryCreateOrder(archer.Id, 1, point,
                    gatherer.RouteId, "source.test-raid", "intent.raid-economy", out _), Is.EqualTo(TrainingFailure.None));

                for (var tick = 0; tick <= 120; tick++) runtime.Training.SimulateTick(tick);
                for (var tick = 0; tick <= 30 && runtime.Combat.GetGathererThreatIncidents().Count == 0; tick++)
                    runtime.Combat.SimulateTick(tick);

                var incident = runtime.Combat.GetGathererThreatIncidents().FirstOrDefault();
                Assert.That(incident.Sequence, Is.GreaterThan(0));
                Assert.That(incident.RouteId, Is.EqualTo(gatherer.RouteId));
                Assert.That(incident.AttackerHandle, Is.GreaterThan(0));
                Assert.That(gathererHitAudio.Any(value => value.Faction == MatchFaction.Enemy &&
                    value.X == gatherer.X && value.Y == gatherer.Y), Is.True);

                var responseTick = incident.Tick + snapshot.AiStrategy.ReactionDelayTicks + 1;
                for (var tick = 0; tick <= responseTick; tick++) runtime.AiStrategy.SimulateTick(tick);
                var decision = runtime.AiStrategy.GetDecisions().FirstOrDefault(value =>
                    value.DefenseTriggerKind == AiDefenseTriggerKind.LogisticsDefense);
                Assert.That(decision, Is.Not.Null);
                Assert.That(decision.Tick, Is.InRange(responseTick - 1, responseTick));
                Assert.That(decision.IntentId, Is.EqualTo("intent.hold"));
                Assert.That(decision.ThreatRouteId, Is.EqualTo(gatherer.RouteId.Value));
                Assert.That(runtime.EnemyTraining.GetSnapshot().Any(value =>
                    value.Priority == TrainingOrderPriority.EmergencyDefense &&
                    !string.IsNullOrWhiteSpace(value.DefenseTriggerId)), Is.True,
                    $"decision={decision.Result}/{decision.GateFailure}; orders=" +
                    string.Join(",", runtime.EnemyTraining.GetSnapshot().Select(value =>
                        $"{value.Id}:{value.Priority}:{value.DefenseTriggerId}:{value.Remaining}")));
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse())
                    await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task SixMinuteSlice_ClosesEconomyAndFirstPressureAsSmokeTest()
        {
            var snapshot = await CreateSnapshot(20260809);
            var context = new GameContext("six-minute-slice");
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var playerBuildings = runtime.Buildings;
            var enemyBuildings = runtime.EnemyBuildings;
            var playerTraining = runtime.Training;
            var enemyTraining = runtime.EnemyTraining;
            var nodes = runtime.ResourceNodes;
            var playerGatherers = runtime.PlayerGatherers;
            var enemyGatherers = runtime.EnemyGatherers;
            var hand = runtime.Hand;
            var aiStrategy = runtime.AiStrategy;
            var combat = runtime.Combat;
            var systems = runtime.Systems;
            foreach (var system in systems) await system.InitializeAsync(context, CancellationToken.None);

            var initialCards = new[] { "card.building.winery", "card.building.gatherer-lodge", "card.building.sawmill",
                "card.building.shield-camp", "card.building.archer-camp" };
            for (var slot = 0; slot < initialCards.Length; slot++)
                Assert.That(hand.TryPlayBuilding(new CardId(initialCards[slot]), slot), Is.True, initialCards[slot]);

            var endTick = -1;
            for (var tick = 0; tick <= 3600 && !combat.HasEnded; tick++)
            {
                nodes.SimulateTick(tick);
                playerBuildings.SimulateTick(tick);
                enemyBuildings.SimulateTick(tick);
                playerGatherers.SimulateTick(tick);
                enemyGatherers.SimulateTick(tick);
                if (tick % 60 == 0)
                    playerTraining.TryCreateOrder(new UnitId("unit.shield-guard"), 1, new DeploymentPoint(1, 1), out _);
                if (tick % 80 == 40)
                    playerTraining.TryCreateOrder(new UnitId("unit.archer"), 1, new DeploymentPoint(1, 1), out _);
                playerTraining.SimulateTick(tick);
                aiStrategy.SimulateTick(tick);
                enemyTraining.SimulateTick(tick);
                combat.SimulateTick(tick);
                hand.SimulateTick(tick);
                var offer = hand.GetOffer();
                if (offer.Active)
                {
                    var choice = offer.Choices[0];
                    var claimed = hand.ChooseOffer(choice);
                    if (!claimed)
                        claimed = hand.TryReplaceAndChoose(choice.Id, hand.GetHand().First().Id);
                    Assert.That(claimed, Is.True, "Headless smoke test must resolve full-hand offers through replacement.");
                    hand.TryConsumeTactic(choice, out _);
                }
                if (combat.HasEnded) endTick = tick;
            }

            var wallState = string.Join(", ", combat.GetWalls().Select(value => $"{value.Faction}={value.Health}"));
            var inventoryState = string.Join(", ", runtime.Economy.GetSnapshot()
                .Select(value => $"{value.Id.Value}={value.Amount}/{value.Available}"));
            var firstPressure = aiStrategy.GetDecisions().FirstOrDefault(value =>
                value.Result.StartsWith("train:", StringComparison.Ordinal));
            Assert.That(firstPressure, Is.Not.Null,
                $"No legal first pressure was produced. Walls: {wallState}; inventory: {inventoryState}");
            Assert.That(firstPressure.Tick, Is.InRange(600, 800));
            Assert.That(runtime.Economy.GetSnapshot().All(value =>
                value.Amount >= 0 && value.Reserved >= 0 && value.Reserved <= value.Amount), Is.True);
            Assert.That(runtime.EnemyEconomy.GetSnapshot().All(value =>
                value.Amount >= 0 && value.Reserved >= 0 && value.Reserved <= value.Amount), Is.True);
            if (combat.HasEnded)
                Assert.That(combat.GetWalls().Count(value => value.Health == 0), Is.EqualTo(1),
                    $"A smoke-test result at tick {endTick} must have exactly one defeated wall.");
        }

        [Test]
        public async Task ProcessingReserveFloor_ProtectsMilitaryInputsWithoutBlockingOtherSpending()
        {
            var snapshot = await CreateSnapshot(1801);
            var economy = new EconomySystem(snapshot);
            var buildings = new BuildingSystem(snapshot, economy);
            var systems = new GameSystemBase[] { economy, buildings };
            foreach (var system in systems) await system.InitializeAsync(new GameContext("processing-reserve"), CancellationToken.None);
            try
            {
                var food = new ResourceId("resource.food");
                var wine = new ResourceId("resource.wine");
                Assert.That(economy.TryAdd(food, 21, out _), Is.True);
                Assert.That(buildings.TryBuild(0, new BuildingId("building.winery"), out var wineryId), Is.True);
                Assert.That(buildings.TryResumeAfterResourceShortage(wineryId), Is.False,
                    "A running building cannot execute the resource-shortage resume command.");
                for (var tick = 0; tick < 50; tick++) buildings.SimulateTick(tick);
                Assert.That(economy.GetAvailable(food), Is.EqualTo(21));
                Assert.That(economy.GetAvailable(wine), Is.Zero);
                Assert.That(buildings.GetSnapshot().Single(value => value.InstanceId == wineryId).BlockReason,
                    Is.EqualTo(ProductionBlockReason.ReserveProtected));
                Assert.That(buildings.GetSnapshot().Single(value => value.InstanceId == wineryId).Paused, Is.True);

                Assert.That(economy.TryExchange(new[] { new ResourceAmount(food, 1) }, null, out _), Is.True,
                    "The reserve floor must not lock player-directed spending.");
                Assert.That(economy.TryAdd(food, 2, out _), Is.True);
                for (var tick = 50; tick < 100; tick++) buildings.SimulateTick(tick);
                Assert.That(economy.GetAvailable(food), Is.EqualTo(22));
                Assert.That(economy.GetAvailable(wine), Is.Zero);
                Assert.That(buildings.GetSnapshot().Single(value => value.InstanceId == wineryId).Paused, Is.True,
                    "Adding inventory must not resume a resource-shortage latch.");
                Assert.That(buildings.TryResumeAfterResourceShortage(wineryId), Is.True);
                for (var tick = 100; tick < 150; tick++) buildings.SimulateTick(tick);
                Assert.That(economy.GetAvailable(food), Is.EqualTo(20));
                Assert.That(economy.GetAvailable(wine), Is.EqualTo(1));
                for (var tick = 150; tick < 200; tick++) buildings.SimulateTick(tick);
                Assert.That(buildings.GetSnapshot().Single(value => value.InstanceId == wineryId).Paused, Is.True,
                    "The next unaffordable production attempt must latch the building again.");
            }
            finally { foreach (var system in systems.Reverse()) await system.ShutdownAsync(CancellationToken.None); }
        }

        [Test]
        public async Task EnemyOpening_ConsumesSameSixCards_AndSpecialistCampsDispatchRealWorkers()
        {
            var runtime = MatchRuntimeFactory.Create(await CreateSnapshot(1802));
            foreach (var system in runtime.Systems) await system.InitializeAsync(new GameContext("enemy-symmetric-opening"), CancellationToken.None);
            try
            {
                Assert.That(runtime.EnemyHand.TotalCount, Is.Zero, "All six opening cards must be consumed into legal slots.");
                var built = runtime.EnemyBuildings.GetSnapshot().Where(value => value.BuildingId.HasValue)
                    .Select(value => value.BuildingId.Value.Value).OrderBy(value => value, StringComparer.Ordinal).ToArray();
                var expected = ContentConstants.P1InitialBuildingCardIds
                    .Select(cardId => cardId.Replace("card.", string.Empty, StringComparison.Ordinal))
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                Assert.That(built, Is.EqualTo(expected));

                runtime.EnemyGatherers.SimulateTick(0);
                var paidWorkers = runtime.EnemyGatherers.GetSnapshot().Where(value => value.BuildingInstanceId != 0).ToArray();
                Assert.That(paidWorkers.Length, Is.GreaterThanOrEqualTo(1));
                Assert.That(paidWorkers.All(value => built.Contains(runtime.EnemyBuildings.GetConfig(value.BuildingInstanceId).Id.Value)), Is.True);
                Assert.That(runtime.EnemyEconomy.GetLedger().Any(value => value.IntentId == "intent.gather" && value.Amount < 0), Is.True);
            }
            finally { foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None); }
        }

        [Test]
        public async Task ResearchCandidates_OnlyUseCategoriesWithActiveCamps()
        {
            var runtime = MatchRuntimeFactory.Create(await CreateSnapshot(1803));
            foreach (var system in runtime.Systems) await system.InitializeAsync(new GameContext("research-category-availability"), CancellationToken.None);
            try
            {
                Assert.That(runtime.Buildings.TryBuild(0, new BuildingId("building.research-lab"), out _), Is.True);
                Assert.That(runtime.PlayerResearch.GetCandidates(), Is.Empty);
                Assert.That(runtime.Buildings.TryBuild(1, new BuildingId("building.shield-camp"), out _), Is.True);
                Assert.That(runtime.PlayerResearch.GetCandidates(), Is.Not.Empty);
                Assert.That(runtime.PlayerResearch.GetCandidates().All(value => value.TargetRole == ResearchCategory.Melee), Is.True);
                Assert.That(runtime.Buildings.TryBuild(2, new BuildingId("building.archer-camp"), out _), Is.True);
                var categories = runtime.PlayerResearch.GetCandidates().Select(value => value.TargetRole).Distinct().ToArray();
                Assert.That(categories, Does.Contain(ResearchCategory.Melee));
                Assert.That(categories, Does.Contain(ResearchCategory.Ranged));
                Assert.That(categories, Has.None.EqualTo(ResearchCategory.Magic));
                Assert.That(categories, Has.None.EqualTo(ResearchCategory.Siege));
            }
            finally { foreach (var system in runtime.Systems.Reverse()) await system.ShutdownAsync(CancellationToken.None); }
        }
        [TestCase("battlefield.prologue", "mode.prologue.peaceful")]
        [TestCase("battlefield.river-pass", "mode.river-pass.peaceful")]
        public async Task CombatRoutes_DefineSharedWallSurfacesBehindBothGates(string battlefieldId, string modeId)
        {
            var snapshot = await CreateSnapshot(20260831, battlefieldId, modeId);
            var playerSurface = snapshot.BattlefieldLayout.Routes.Select(route => route.Points[0].X).Distinct().ToArray();
            var enemySurface = snapshot.BattlefieldLayout.Routes.Select(route => route.Points[route.Points.Count - 1].X).Distinct().ToArray();

            Assert.That(playerSurface, Is.EqualTo(new[] { 518 }));
            Assert.That(enemySurface, Is.EqualTo(new[] { 1824 }));
            Assert.That(snapshot.Combat.PlayerWall.Gate.X, Is.LessThan(playerSurface.Single()));
            Assert.That(snapshot.Combat.EnemyWall.Gate.X, Is.GreaterThan(enemySurface.Single()));
        }

        [Test]
        public async Task RangedUnits_OnEveryLaneAttackVerticalWallSurfacesWithoutChangingLane()
        {
            var snapshot = await CreateSnapshot(20260832);
            var context = new GameContext("wall-surface-lanes");
            var playerEconomy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var playerCamps = new CampSystem(playerBuildings);
            var enemyCamps = new EnemyCampSystem(enemyBuildings);
            var playerTraining = new TrainingSystem(snapshot, playerEconomy, playerBuildings, playerCamps);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var combat = new CombatSystem(snapshot, playerTraining, enemyTraining);
            var systems = new GameSystemBase[] { playerEconomy, enemyEconomy, playerBuildings, enemyBuildings,
                playerCamps, enemyCamps, playerTraining, enemyTraining, combat };
            foreach (var system in systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                var campId = new BuildingId("building.archer-camp");
                var archer = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.archer");
                Assert.That(playerBuildings.TryBuild(0, campId, out _), Is.True);
                Assert.That(enemyBuildings.TryBuild(0, campId, out _), Is.True);
                foreach (var cost in archer.TrainingCosts)
                {
                    Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount * 3, out _), Is.True);
                    Assert.That(enemyEconomy.TryAdd(cost.ResourceId, cost.Amount * 3, out _), Is.True);
                }

                var playerSurface = snapshot.BattlefieldLayout.Routes.Min(route => route.Points.Min(point => point.X));
                var enemySurface = snapshot.BattlefieldLayout.Routes.Max(route => route.Points.Max(point => point.X));
                var routeYs = snapshot.BattlefieldLayout.Routes
                    .Select(route => route.Points.Sum(point => point.Y) / route.Points.Count).OrderBy(value => value).ToArray();
                var playerX = enemySurface - archer.CollisionRadius - archer.AttackRange;
                var enemyX = playerSurface + archer.CollisionRadius + archer.AttackRange;
                for (var lane = 0; lane < routeYs.Length; lane++)
                {
                    Assert.That(playerTraining.TryCreateOrder(archer.Id, 1,
                        DeploymentPoint.World(playerX, routeYs[lane], lane), out _), Is.EqualTo(TrainingFailure.None));
                    Assert.That(enemyTraining.TryCreateOrder(archer.Id, 1,
                        DeploymentPoint.World(enemyX, routeYs[lane], lane), out _), Is.EqualTo(TrainingFailure.None));
                }

                for (var tick = 0; tick <= archer.TrainingTicks * 4 && combat.GetUnits().Count < 6; tick++)
                {
                    playerTraining.SimulateTick(tick);
                    enemyTraining.SimulateTick(tick);
                }
                Assert.That(combat.GetUnits().Count, Is.EqualTo(6));

                combat.SimulateTick(0);

                var units = combat.GetUnits();
                Assert.That(units.All(value => value.AttackRevision == 1 &&
                    value.LockedTargetKind == CombatTargetKind.Wall), Is.True);
                Assert.That(units.Where(value => value.Faction == MatchFaction.Player)
                    .Select(value => (value.X, value.Y)), Is.EquivalentTo(routeYs.Select(y => (playerX, y))));
                Assert.That(units.Where(value => value.Faction == MatchFaction.Enemy)
                    .Select(value => (value.X, value.Y)), Is.EquivalentTo(routeYs.Select(y => (enemyX, y))));

                var projectiles = combat.GetProjectiles();
                Assert.That(projectiles.Count, Is.EqualTo(6));
                Assert.That(projectiles.All(value => value.TargetKind == CombatProjectileTargetKind.Wall), Is.True);
                Assert.That(projectiles.Where(value => value.Faction == MatchFaction.Player)
                    .Select(value => (value.TargetX, value.TargetY)),
                    Is.EquivalentTo(routeYs.Select(y => (enemySurface, y))));
                Assert.That(projectiles.Where(value => value.Faction == MatchFaction.Enemy)
                    .Select(value => (value.TargetX, value.TargetY)),
                    Is.EquivalentTo(routeYs.Select(y => (playerSurface, y))));
            }
            finally
            {
                foreach (var system in systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task MeleeUnit_EntersExactEdgeRangeAndBossKnockbackCannotCrossWallSurface()
        {
            var snapshot = await CreateSnapshot(20260833);
            var context = new GameContext("wall-edge-range");
            var playerEconomy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var playerBuildings = new BuildingSystem(snapshot, playerEconomy);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var playerCamps = new CampSystem(playerBuildings);
            var enemyCamps = new EnemyCampSystem(enemyBuildings);
            var playerTraining = new TrainingSystem(snapshot, playerEconomy, playerBuildings, playerCamps);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var combat = new CombatSystem(snapshot, playerTraining, enemyTraining);
            var systems = new GameSystemBase[] { playerEconomy, enemyEconomy, playerBuildings, enemyBuildings,
                playerCamps, enemyCamps, playerTraining, enemyTraining, combat };
            foreach (var system in systems) await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                var shield = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
                Assert.That(playerBuildings.TryBuild(0, new BuildingId("building.shield-camp"), out _), Is.True);
                foreach (var cost in shield.TrainingCosts)
                    Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

                var enemySurface = snapshot.BattlefieldLayout.Routes.Max(route => route.Points.Max(point => point.X));
                var upperY = snapshot.BattlefieldLayout.Routes
                    .Select(route => route.Points.Sum(point => point.Y) / route.Points.Count).Min();
                var startX = enemySurface - shield.CollisionRadius - shield.AttackRange - shield.MovePerTick - 1;
                Assert.That(playerTraining.TryCreateOrder(shield.Id, 1,
                    DeploymentPoint.World(startX, upperY, 0), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= shield.TrainingTicks; tick++) playerTraining.SimulateTick(tick);

                combat.SimulateTick(0);
                var outside = combat.GetUnits().Single();
                Assert.That(outside.X, Is.EqualTo(startX + shield.MovePerTick));
                Assert.That(outside.Y, Is.EqualTo(upperY));
                Assert.That(outside.AttackRevision, Is.Zero);
                Assert.That(combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy).Health,
                    Is.EqualTo(snapshot.Combat.EnemyWall.MaxHealth));

                combat.SimulateTick(1);
                var attacking = combat.GetUnits().Single();
                Assert.That(attacking.X + shield.CollisionRadius + shield.AttackRange, Is.EqualTo(enemySurface));
                Assert.That(attacking.Y, Is.EqualTo(upperY));
                Assert.That(attacking.AttackRevision, Is.EqualTo(1));
                Assert.That(combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy).Health,
                    Is.LessThan(snapshot.Combat.EnemyWall.MaxHealth));

                Assert.That(combat.ApplyBossMeteor(attacking.X - 1, attacking.Y, 10, 1, 1000), Is.EqualTo(1));
                var knocked = combat.GetUnits().Single();
                Assert.That(knocked.X + shield.CollisionRadius, Is.LessThanOrEqualTo(enemySurface));

                foreach (var cost in shield.TrainingCosts)
                    Assert.That(playerEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
                Assert.That(playerTraining.TryCreateOrder(shield.Id, 1,
                    DeploymentPoint.World(enemySurface, upperY, 0), out _), Is.EqualTo(TrainingFailure.None));
                for (var tick = 0; tick <= shield.TrainingTicks; tick++) playerTraining.SimulateTick(tick + 1000);
                combat.SimulateTick(2);
                Assert.That(combat.GetUnits().All(value =>
                    value.X + shield.CollisionRadius <= enemySurface), Is.True,
                    "Friendly separation must not push a unit body through the enemy wall surface.");
            }
            finally
            {
                foreach (var system in systems.Reverse()) await system.ShutdownAsync(CancellationToken.None);
            }
        }



        private static async Task<MatchConfigSnapshot> CreateSnapshot(int seed)
            => await CreateSnapshot(seed, "battlefield.prologue", "mode.prologue.peaceful");

        private static async Task<MatchConfigSnapshot> CreateSnapshot(int seed, string battlefieldId, string modeId)
        {
            var system = new ContentConfigSystem(new AssetResourceService(LoadRoot()), new ResourceKey("config.game-content"));
            await system.InitializeAsync(new GameContext("snapshot-helper"), CancellationToken.None);
            try { return system.CreateMatchSnapshot(new BattlefieldId(battlefieldId), new MapModeId(modeId), seed); }
            finally { await system.ShutdownAsync(CancellationToken.None); }
        }

        private static bool ContainsUnityObject(object value, HashSet<object> visited)
        {
            if (value == null || value is string || value.GetType().IsPrimitive || value.GetType().IsEnum || value.GetType().IsValueType) return false;
            if (value is UnityEngine.Object) return true;
            if (!visited.Add(value)) return false;
            if (value is System.Collections.IEnumerable sequence)
                foreach (var item in sequence) if (ContainsUnityObject(item, visited)) return true;
            foreach (var property in value.GetType().GetProperties().Where(property => property.GetIndexParameters().Length == 0))
                if (ContainsUnityObject(property.GetValue(value), visited)) return true;
            return false;
        }

        private static GameContentConfig LoadRoot() => AssetDatabase.LoadAssetAtPath<GameContentConfig>(RootPath)
            ?? throw new AssertionException("Missing P1 GameContentConfig asset.");

        private sealed class AssetResourceService : IResourceService
        {
            private readonly GameContentConfig _asset;
            public AssetResourceService(GameContentConfig asset) => _asset = asset;
            public Task<IAssetLease<T>> AcquireAsync<T>(ResourceKey key, CancellationToken cancellationToken) where T : UnityEngine.Object =>
                Task.FromResult<IAssetLease<T>>(new Lease<T>(key, _asset as T));
            public Task<IInstanceLease> SpawnAsync(ResourceKey key, Transform parent, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task PreloadAsync(IReadOnlyCollection<ResourceKey> keys, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class Lease<T> : IAssetLease<T> where T : UnityEngine.Object
        {
            public Lease(ResourceKey key, T asset) { Key = key; Asset = asset; }
            public ResourceKey Key { get; }
            public T Asset { get; }
            public void Dispose() { }
        }

        private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
        {
            public static readonly ReferenceEqualityComparer Instance = new();
            public new bool Equals(object x, object y) => ReferenceEquals(x, y);
            public int GetHashCode(object obj) => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
