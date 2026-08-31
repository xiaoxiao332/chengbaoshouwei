using System;
using System.Collections.Generic;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Gameplay
{
    public sealed class MatchSimulationPipeline
    {
        public MatchSimulationPipeline(EnemyEconomySystem enemyEconomy, ResourceNodeSystem resourceNodes, BuildingSystem buildings,
            EnemyBuildingSystem enemyBuildings, PlayerGathererSystem playerGatherers,
            EnemyGathererSystem enemyGatherers, PlayerResearchSystem playerResearch,
            EnemyResearchSystem enemyResearch, TrainingSystem training,
            AiStrategySystem aiStrategy, EnemyTrainingSystem enemyTraining,
            PlayerTowerConstructionSystem playerConstruction, EnemyTowerConstructionSystem enemyConstruction,
            BossSystem boss, CombatSystem combat, HandAndOfferSystem hand, MatchAnalyticsSystem analytics)
            : this(enemyEconomy, resourceNodes, buildings, enemyBuildings, playerGatherers, enemyGatherers,
                playerResearch, enemyResearch, training, aiStrategy, enemyTraining, playerConstruction,
                enemyConstruction, boss, combat, hand, null, analytics) { }

        public MatchSimulationPipeline(EnemyEconomySystem enemyEconomy, ResourceNodeSystem resourceNodes, BuildingSystem buildings,
            EnemyBuildingSystem enemyBuildings, PlayerGathererSystem playerGatherers,
            EnemyGathererSystem enemyGatherers, PlayerResearchSystem playerResearch,
            EnemyResearchSystem enemyResearch, TrainingSystem training,
            AiStrategySystem aiStrategy, EnemyTrainingSystem enemyTraining,
            PlayerTowerConstructionSystem playerConstruction, EnemyTowerConstructionSystem enemyConstruction,
            BossSystem boss, CombatSystem combat, HandAndOfferSystem hand, HandAndOfferSystem enemyHand,
            MatchAnalyticsSystem analytics)
        {
            var systems = new List<IFixedMatchSimulation>
            {
                enemyEconomy, resourceNodes, buildings, enemyBuildings, playerGatherers, enemyGatherers,
                playerResearch, enemyResearch, training, hand
            };
            if (enemyHand != null) systems.Add(enemyHand);
            systems.Add(aiStrategy);
            systems.Add(enemyTraining);
            systems.Add(playerConstruction);
            systems.Add(enemyConstruction);
            systems.Add(boss);
            systems.Add(combat);
            systems.Add(analytics);
            var seen = new List<IFixedMatchSimulation>();
            foreach (var system in systems)
            {
                if (system == null) throw new ArgumentNullException(nameof(systems), "A match simulation participant is missing.");
                if (seen.Exists(value => ReferenceEquals(value, system)))
                    throw new ArgumentException("A match simulation participant was registered more than once.", nameof(systems));
                seen.Add(system);
            }
            Systems = systems;
        }

        public IReadOnlyList<IFixedMatchSimulation> Systems { get; }
    }

    public sealed class MatchRuntime
    {
        internal MatchRuntime(EconomySystem economy, EnemyEconomySystem enemyEconomy,
            MatchPhaseSystem phases, BuildingSystem buildings, CampSystem camps, TrainingSystem training,
            EnemyBuildingSystem enemyBuildings, EnemyCampSystem enemyCamps, EnemyTrainingSystem enemyTraining,
            PlayerResearchSystem playerResearch, EnemyResearchSystem enemyResearch,
            ResourceNodeSystem resourceNodes, PlayerGathererSystem playerGatherers,
            EnemyGathererSystem enemyGatherers, HandAndOfferSystem hand, AiStrategySystem aiStrategy,
            HandAndOfferSystem enemyHand,
            PlayerTowerConstructionSystem playerConstruction, EnemyTowerConstructionSystem enemyConstruction,
            BossSystem boss, CombatSystem combat, MatchAnalyticsSystem analytics,
            FixedSimulationSystem simulation, IReadOnlyList<GameSystemBase> systems)
        {
            Economy = economy; EnemyEconomy = enemyEconomy; Phases = phases; Buildings = buildings;
            Camps = camps; Training = training; EnemyBuildings = enemyBuildings; EnemyCamps = enemyCamps;
            PlayerResearch = playerResearch; EnemyResearch = enemyResearch;
            EnemyTraining = enemyTraining; ResourceNodes = resourceNodes; PlayerGatherers = playerGatherers;
            EnemyGatherers = enemyGatherers; Hand = hand; AiStrategy = aiStrategy; Combat = combat;
            EnemyHand = enemyHand;
            PlayerConstruction = playerConstruction; EnemyConstruction = enemyConstruction;
            Boss = boss;
            Analytics = analytics;
            Simulation = simulation; Systems = systems;
        }

        public EconomySystem Economy { get; }
        public EnemyEconomySystem EnemyEconomy { get; }
        public MatchPhaseSystem Phases { get; }
        public BuildingSystem Buildings { get; }
        public CampSystem Camps { get; }
        public TrainingSystem Training { get; }
        public EnemyBuildingSystem EnemyBuildings { get; }
        public EnemyCampSystem EnemyCamps { get; }
        public EnemyTrainingSystem EnemyTraining { get; }
        public PlayerResearchSystem PlayerResearch { get; }
        public EnemyResearchSystem EnemyResearch { get; }
        public ResourceNodeSystem ResourceNodes { get; }
        public PlayerGathererSystem PlayerGatherers { get; }
        public EnemyGathererSystem EnemyGatherers { get; }
        public HandAndOfferSystem Hand { get; }
        public HandAndOfferSystem EnemyHand { get; }
        public AiStrategySystem AiStrategy { get; }
        public PlayerTowerConstructionSystem PlayerConstruction { get; }
        public EnemyTowerConstructionSystem EnemyConstruction { get; }
        public BossSystem Boss { get; }
        public MatchAnalyticsSystem Analytics { get; }
        public CombatSystem Combat { get; }
        public FixedSimulationSystem Simulation { get; }
        public IReadOnlyList<GameSystemBase> Systems { get; }
    }

    public static class MatchRuntimeFactory
    {
        public static MatchRuntime Create(MatchConfigSnapshot snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.SchemaVersion != ContentConstants.ExpectedSchemaVersion)
                throw new ArgumentException($"Schema v{ContentConstants.ExpectedSchemaVersion} is required; found v{snapshot.SchemaVersion}.", nameof(snapshot));

            var economy = new EconomySystem(snapshot);
            var enemyEconomy = new EnemyEconomySystem(snapshot);
            var phases = new MatchPhaseSystem(snapshot);
            var buildings = new BuildingSystem(snapshot, economy);
            var camps = new CampSystem(buildings, snapshot);
            var enemyBuildings = new EnemyBuildingSystem(snapshot, enemyEconomy);
            var enemyCamps = new EnemyCampSystem(enemyBuildings, snapshot);
            var playerResearch = new PlayerResearchSystem(snapshot, economy, buildings, camps);
            var enemyResearch = new EnemyResearchSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps);
            var training = new TrainingSystem(snapshot, economy, buildings, camps, playerResearch);
            var enemyTraining = new EnemyTrainingSystem(snapshot, enemyEconomy, enemyBuildings, enemyCamps, enemyResearch);
            var resourceNodes = new ResourceNodeSystem(snapshot);
            var playerGatherers = new PlayerGathererSystem(snapshot.BattlefieldLayout.Gatherers, economy,
                resourceNodes, snapshot.Combat.PlayerWall.Gate, buildings, snapshot.BattlefieldLayout, snapshot.Seed);
            var enemyGatherers = new EnemyGathererSystem(snapshot.BattlefieldLayout.Gatherers, enemyEconomy,
                resourceNodes, snapshot.Combat.EnemyWall.Gate, snapshot.EnemyEconomy.EconomicEfficiencyMilli,
                snapshot.BattlefieldLayout, enemyBuildings, snapshot.Seed);
            var hand = new HandAndOfferSystem(snapshot, economy, buildings);
            var enemyHand = new EnemyHandAndOfferSystem(snapshot, enemyEconomy, enemyBuildings,
                snapshot.HandAndOffers.GuaranteedCards);
            var playerConstruction = new PlayerTowerConstructionSystem(snapshot, economy, hand);
            var enemyConstruction = new EnemyTowerConstructionSystem(snapshot, enemyEconomy, enemyHand);
            var boss = new BossSystem(snapshot, economy, enemyEconomy);
            var combat = new CombatSystem(snapshot, training, enemyTraining, playerConstruction, enemyConstruction,
                playerResearch, enemyResearch, boss, playerGatherers, enemyGatherers);
            boss.BindCombat(combat);
            var aiStrategy = new AiStrategySystem(snapshot, enemyBuildings, enemyTraining, enemyEconomy,
                phases, enemyConstruction, enemyResearch, combat, enemyHand, playerGatherers);
            var analytics = new MatchAnalyticsSystem(economy, buildings, combat, hand, playerResearch,
                playerConstruction, boss, enemyEconomy, aiStrategy);
            var pipeline = new MatchSimulationPipeline(enemyEconomy, resourceNodes, buildings, enemyBuildings, playerGatherers,
                enemyGatherers, playerResearch, enemyResearch, training, aiStrategy, enemyTraining,
                playerConstruction, enemyConstruction, boss, combat, hand, enemyHand, analytics);
            var simulation = new FixedSimulationSystem(phases, pipeline);
            hand.RewardChoiceStateChanged += active =>
                simulation.SetPauseReason(MatchPauseReason.PlayerRewardChoice, active);
            var systems = new GameSystemBase[]
            {
                economy, enemyEconomy, phases, buildings, camps, playerResearch, training, enemyBuildings, enemyCamps,
                enemyResearch, enemyTraining, resourceNodes, playerGatherers, enemyGatherers, hand, enemyHand, aiStrategy,
                playerConstruction, enemyConstruction, boss, combat, analytics, simulation
            };
            return new MatchRuntime(economy, enemyEconomy, phases, buildings, camps, training, enemyBuildings,
                enemyCamps, enemyTraining, playerResearch, enemyResearch, resourceNodes, playerGatherers, enemyGatherers, hand, aiStrategy, enemyHand,
                playerConstruction, enemyConstruction, boss, combat, analytics, simulation, systems);
        }
    }
}
