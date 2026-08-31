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
using NUnit.Framework;
using UnityEditor;

namespace FortressFrontier.Tests
{
    public sealed class BalanceProxySimulationTests
    {
        private const string RootPath = "Assets/Game/Content/Config/GameContentConfig.asset";

        private enum ProxyStrategy
        {
            EconomyPriority,
            Melee,
            Ranged,
            MagicSiege,
            Mixed
        }

        private readonly struct ProxyResult
        {
            public ProxyResult(string battlefieldId, MapModeKind mode, ProxyStrategy strategy,
                int seed, int durationTicks, bool ended, bool playerWon, int firstPressureTick,
                int averagePressureIntervalTicks, int pressureMinIntervalTicks,
                int completedPressureCycles, int maximumConsecutiveIntentCount,
                int longestPressureGapTicks, IReadOnlyDictionary<string, int> unitUsage,
                IReadOnlyDictionary<string, int> resources, int researchRanks,
                int freeGathered, int paidGathered, int paidDispatchCost, int paidDeaths,
                int maturePaidSourceCount)
            {
                BattlefieldId = battlefieldId;
                Mode = mode;
                Strategy = strategy;
                Seed = seed;
                DurationTicks = durationTicks;
                Ended = ended;
                PlayerWon = playerWon;
                FirstPressureTick = firstPressureTick;
                
                PressureMinIntervalTicks = pressureMinIntervalTicks;
                CompletedPressureCycles = completedPressureCycles;
                MaximumConsecutiveIntentCount = maximumConsecutiveIntentCount;
                LongestPressureGapTicks = longestPressureGapTicks;
                AveragePressureIntervalTicks = averagePressureIntervalTicks;
                UnitUsage = unitUsage;
                Resources = resources;
                ResearchRanks = researchRanks;
                FreeGathered = freeGathered; PaidGathered = paidGathered;
                PaidDispatchCost = paidDispatchCost; PaidDeaths = paidDeaths;
                MaturePaidSourceCount = maturePaidSourceCount;
            }

            public string BattlefieldId { get; }
            public MapModeKind Mode { get; }
            public ProxyStrategy Strategy { get; }
            public int Seed { get; }
            public int DurationTicks { get; }
            public bool Ended { get; }
            public bool PlayerWon { get; }
            public int FirstPressureTick { get; }
            
            public int PressureMinIntervalTicks { get; }
            public int CompletedPressureCycles { get; }
            public int MaximumConsecutiveIntentCount { get; }
            public int LongestPressureGapTicks { get; }
            public int AveragePressureIntervalTicks { get; }
            public IReadOnlyDictionary<string, int> UnitUsage { get; }
            public IReadOnlyDictionary<string, int> Resources { get; }
            public int ResearchRanks { get; }
            public int FreeGathered { get; }
            public int PaidGathered { get; }
            public int PaidDispatchCost { get; }
            public int PaidDeaths { get; }
            public int MaturePaidSourceCount { get; }
        }

[Test]
        public async Task QuickRegression_SixCombinationsTenSeeds_ProducesCadenceReport()
        {
            var results = await RunBatch(10);
            WriteReport("quick-10", results);
            Assert.That(results.Count, Is.EqualTo(60));
            Assert.That(results.All(value => value.FirstPressureTick >= 0), Is.True,
                "Every observed match must eventually produce a legal enemy pressure.");
            Assert.That(results.All(value => value.AveragePressureIntervalTicks == 0 ||
                                             value.AveragePressureIntervalTicks >= Math.Max(400,
                                                 value.PressureMinIntervalTicks * ContentConstants.AiPressureIntervalMultipliersMilli.Min() / 1000)),
                Is.True, "Repeated pressure must respect the configured breathing interval.");
            Assert.That(results.GroupBy(value => value.Mode)
                .All(group => group.Any(value => value.CompletedPressureCycles > 0)), Is.True,
                "Each mode must demonstrate at least one complete pressure/recovery cycle.");
        }

[TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        [Explicit("Schema v14 formal matrix split batch: prologue peaceful, 50 samples per case.")]
        public Task FormalBalance_ProloguePeaceful_FiftySampleBatch(int batchIndex) =>
            RunFormalCohort("battlefield.prologue", "mode.prologue.peaceful", batchIndex, 50);

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        [Explicit("Schema v14 formal matrix split batch: prologue offensive, 50 samples per case.")]
        public Task FormalBalance_PrologueOffensive_FiftySampleBatch(int batchIndex) =>
            RunFormalCohort("battlefield.prologue", "mode.prologue.offensive", batchIndex, 50);

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        [Explicit("Schema v14 formal matrix split batch: prologue nightmare, 50 samples per case.")]
        public Task FormalBalance_PrologueNightmare_FiftySampleBatch(int batchIndex) =>
            RunFormalCohort("battlefield.prologue", "mode.prologue.nightmare", batchIndex, 50);

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        [Explicit("Schema v14 formal matrix split batch: river pass peaceful, 50 samples per case.")]
        public Task FormalBalance_RiverPassPeaceful_FiftySampleBatch(int batchIndex) =>
            RunFormalCohort("battlefield.river-pass", "mode.river-pass.peaceful", batchIndex, 50);

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        [Explicit("Schema v14 formal matrix split batch: river pass offensive, 50 samples per case.")]
        public Task FormalBalance_RiverPassOffensive_FiftySampleBatch(int batchIndex) =>
            RunFormalCohort("battlefield.river-pass", "mode.river-pass.offensive", batchIndex, 50);

        [TestCase(0), TestCase(1), TestCase(2), TestCase(3), TestCase(4), TestCase(5)]
        [Explicit("Schema v14 formal matrix split batch: river pass nightmare, 50 samples per case.")]
        public Task FormalBalance_RiverPassNightmare_FiftySampleBatch(int batchIndex) =>
            RunFormalCohort("battlefield.river-pass", "mode.river-pass.nightmare", batchIndex, 50);

        private static async Task RunFormalCohort(
            string battlefieldId, string modeId, int batchIndex, int sampleCount)
        {
            Assert.That(batchIndex, Is.InRange(0, 5));
            var results = await RunFormalBatch(battlefieldId, modeId, batchIndex * sampleCount, sampleCount);
            Assert.That(results.Count, Is.EqualTo(sampleCount));
            Assert.That(results.All(value => value.BattlefieldId == battlefieldId), Is.True);
            Assert.That(results.All(value => value.FirstPressureTick >= 0), Is.True);
            Assert.That(results.All(value => value.AveragePressureIntervalTicks == 0 ||
                                             value.AveragePressureIntervalTicks >= Math.Max(400,
                                                 value.PressureMinIntervalTicks * ContentConstants.AiPressureIntervalMultipliersMilli.Min() / 1000)),
                Is.True);
            WriteReport($"formal-v14-{battlefieldId.Replace('.', '-')}-{modeId.Replace('.', '-')}-batch-{batchIndex + 1}-of-6", results);
            var mixedUsage = results.Where(value => value.Strategy == ProxyStrategy.Mixed)
                .SelectMany(value => value.UnitUsage).GroupBy(value => value.Key)
                .ToDictionary(group => group.Key, group => group.Sum(value => value.Value));
            var mixedTotal = Math.Max(1, mixedUsage.Values.Sum());
            var mixedTopShareMilli = mixedUsage.Values.DefaultIfEmpty(0).Max() * 1000 / mixedTotal;
            TestContext.WriteLine($"BALANCE_OBSERVATION mixedTopUnitShareMilli={mixedTopShareMilli}");
            var topUnits = results.GroupBy(value => (value.BattlefieldId, value.Mode, value.Strategy))
                .Select(group => group.SelectMany(value => value.UnitUsage).GroupBy(value => value.Key)
                    .OrderByDescending(value => value.Sum(entry => entry.Value)).ThenBy(value => value.Key).FirstOrDefault()?.Key)
                .Where(value => value != null).Distinct(StringComparer.Ordinal).ToArray();
            TestContext.WriteLine($"BALANCE_OBSERVATION distinctTopUnitsAcrossStrategies={topUnits.Length}");
            var maturePaidSamples = results.Where(value => value.MaturePaidSourceCount >= 2).ToArray();
            var maturePaidAhead = maturePaidSamples.Count(value => value.PaidGathered > value.FreeGathered);
            TestContext.WriteLine(
                $"BALANCE_OBSERVATION maturePaidAhead={maturePaidAhead}/{maturePaidSamples.Length} " +
                "(cumulative totals include the free source's pre-construction head start)");
            Assert.That(results.Sum(value => value.PaidGathered), Is.GreaterThan(results.Sum(value => value.PaidDispatchCost)),
                "Paid specialist gathering must remain net-positive in normalized resource units.");
        }

        private static async Task<IReadOnlyList<ProxyResult>> RunBatch(int seedsPerCombination)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameContentConfig>(RootPath);
            var content = new ContentConfigSystem(new AssetResourceService(root),
                new ResourceKey("config.game-content"));
            await content.InitializeAsync(new GameContext("balance-quick-content"), CancellationToken.None);
            try
            {
                var results = new List<ProxyResult>();
                foreach (var battlefield in root.BattlefieldCatalog.Definitions.OrderBy(value => value.Id))
                    foreach (var modeId in battlefield.MapModeIds.OrderBy(value => value))
                        for (var seed = 1; seed <= seedsPerCombination; seed++)
                        {
                            var snapshot = content.CreateMatchSnapshot(
                                new BattlefieldId(battlefield.Id), new MapModeId(modeId), seed);
                            results.Add(await RunProxy(snapshot,
                                (ProxyStrategy)((seed - 1) % Enum.GetValues(typeof(ProxyStrategy)).Length)));
                        }
                return results;
            }
            finally
            {
                await content.ShutdownAsync(CancellationToken.None);
            }
        }

        private static async Task<IReadOnlyList<ProxyResult>> RunFormalBatch(
            string battlefieldId, string modeId, int seedOffset, int sampleCount)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameContentConfig>(RootPath);
            var content = new ContentConfigSystem(new AssetResourceService(root),
                new ResourceKey("config.game-content"));
            await content.InitializeAsync(new GameContext("balance-formal-content"), CancellationToken.None);
            try
            {
                var results = new List<ProxyResult>(sampleCount);
                for (var sample = 1; sample <= sampleCount; sample++)
                {
                    var seed = seedOffset + sample;
                    var snapshot = content.CreateMatchSnapshot(
                        new BattlefieldId(battlefieldId), new MapModeId(modeId), 10000 + seed);
                    results.Add(await RunProxy(snapshot,
                        (ProxyStrategy)((seed - 1) % Enum.GetValues(typeof(ProxyStrategy)).Length)));
                }
                return results;
            }
            finally
            {
                await content.ShutdownAsync(CancellationToken.None);
            }
        }

        private static async Task<ProxyResult> RunProxy(MatchConfigSnapshot snapshot, ProxyStrategy strategy)
        {
            var runtime = MatchRuntimeFactory.Create(snapshot);
            var context = new GameContext($"balance-{snapshot.MapModeId.Value}-{snapshot.Seed}");
            foreach (var system in runtime.Systems)
                await system.InitializeAsync(context, CancellationToken.None);

            try
            {
                PlayOpening(runtime, strategy);
                for (var tick = 1; tick <= 36000 && !runtime.Combat.HasEnded; tick++)
                {
                    ResolveOfferAndPlayNewCards(runtime, strategy, snapshot.Seed);
                    IssuePlayerOrders(runtime, strategy, snapshot.Seed, tick);
                    runtime.Simulation.AdvanceTicks(1);
                }

                var analysis = runtime.Analytics.Capture(
                    runtime.Combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy).Health == 0);
                var playerWon = analysis.EnemyWall.Health == 0 && analysis.PlayerWall.Health > 0;
                Assert.That(runtime.Economy.GetSnapshot().All(value =>
                    value.Amount >= 0 && value.Reserved >= 0 && value.Reserved <= value.Amount), Is.True);
                Assert.That(runtime.EnemyEconomy.GetSnapshot().All(value =>
                    value.Amount >= 0 && value.Reserved >= 0 && value.Reserved <= value.Amount), Is.True);
                var gathering = runtime.PlayerGatherers.GetSourceEconomySnapshot();
                return new ProxyResult(snapshot.BattlefieldId.Value, snapshot.MapModeKind, strategy,
                    snapshot.Seed, analysis.DurationTicks, runtime.Combat.HasEnded, playerWon,
                    analysis.FirstEnemyPressureTick, analysis.AverageEnemyPressureIntervalTicks,
                    snapshot.AiStrategy.PressureMinIntervalTicks, analysis.CompletedPressureCycles,
                    analysis.MaximumConsecutiveEnemyIntentCount, analysis.LongestEnemyPressureGapTicks,
                    analysis.CombatCounts.Where(value => value.Faction == MatchFaction.Player)
                        .ToDictionary(value => value.UnitId.Value, value => value.Spawned),
                    runtime.Economy.GetSnapshot().ToDictionary(value => value.Id.Value, value => value.Amount),
                    runtime.PlayerResearch.GetSnapshot().CompletedRanks,
                    gathering.Where(value => value.BuildingInstanceId == 0).Sum(value => value.DeliveredAmount),
                    gathering.Where(value => value.BuildingInstanceId != 0).Sum(value => value.DeliveredAmount),
                    gathering.Where(value => value.BuildingInstanceId != 0).Sum(value => value.DispatchCostAmount),
                    gathering.Where(value => value.BuildingInstanceId != 0).Sum(value => value.DeathCount),
                    gathering.Count(value => value.BuildingInstanceId != 0 && value.CompletedTrips >= 3));
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse())
                    await system.ShutdownAsync(CancellationToken.None);
            }
        }

        private static void PlayOpening(MatchRuntime runtime, ProxyStrategy strategy)
        {
            var cards = new[] { "card.building.gatherer-lodge", "card.building.wood-gatherer-camp",
                "card.building.winery", "card.building.sawmill", "card.building.shield-camp", "card.building.archer-camp" };
            for (var slot = 0; slot < cards.Length; slot++)
                runtime.Hand.TryPlayBuilding(new CardId(cards[slot]), slot);
        }

        private static void ResolveOfferAndPlayNewCards(MatchRuntime runtime, ProxyStrategy strategy, int seed)
        {
            ResumeAffordableGatheringBuildings(runtime);
            ManageFoodProcessor(runtime, "building.winery",
                strategy is ProxyStrategy.Ranged or ProxyStrategy.MagicSiege or ProxyStrategy.Mixed or ProxyStrategy.EconomyPriority);
            ManageFoodProcessor(runtime, "building.pasture",
                strategy is ProxyStrategy.Melee or ProxyStrategy.Mixed);
            ManageProcessor(runtime, "building.sawmill", new ResourceId("resource.wood"),
                strategy is ProxyStrategy.EconomyPriority or ProxyStrategy.Melee or ProxyStrategy.MagicSiege or ProxyStrategy.Mixed,
                18, 35);
            var preferred = StrategyCards(strategy, seed);
            var offer = runtime.Hand.GetOffer();
            if (offer.Active)
            {
                var contentChoices = offer.Choices.Where(value => value.Kind == RewardChoiceKind.ContentCard).ToArray();
                var selected = preferred.Select(id => contentChoices.FirstOrDefault(value => value.CardId?.Value == id))
                    .FirstOrDefault(value => value != null && !HasBuildingForCard(runtime, value.CardId.Value.Value));
                var choice = selected ?? contentChoices.FirstOrDefault() ?? offer.Choices[0];
                if (!runtime.Hand.ChooseOffer(choice) && choice.Kind != RewardChoiceKind.ProcessedResourceBundle)
                    runtime.Hand.TryReplaceAndChoose(choice.Id, runtime.Hand.GetHand().First().Id);
            }
            foreach (var cardId in preferred)
            {
                var card = new CardId(cardId);
                if (!runtime.Hand.Contains(card) || HasBuildingForCard(runtime, cardId)) continue;
                var slot = runtime.Buildings.GetSnapshot().FirstOrDefault(value => !value.BuildingId.HasValue);
                if (slot == null)
                {
                    var replacement = runtime.Buildings.GetSnapshot().Where(value => value.BuildingId.HasValue)
                        .OrderBy(value => ProxyReplacementPriority(value.BuildingId.Value.Value, strategy))
                        .ThenBy(value => value.SlotIndex).First();
                    runtime.Buildings.Demolish(replacement.InstanceId);
                    slot = runtime.Buildings.GetSnapshot()[replacement.SlotIndex];
                }
                runtime.Hand.TryPlayBuilding(card, slot.SlotIndex);
            }
        }

        private static string[] StrategyCards(ProxyStrategy strategy, int seed) => strategy switch
        {
            ProxyStrategy.EconomyPriority => new[] { "card.building.stone-gatherer-camp", "card.building.iron-gatherer-camp", "card.building.research-lab", "card.building.iron-smelter", "card.building.stoneworks", "card.building.sawmill" },
            ProxyStrategy.Melee => new[] { "card.building.iron-gatherer-camp", "card.building.pasture", "card.building.iron-smelter", "card.building.heavy-warrior-camp" },
            ProxyStrategy.Ranged => new[] { "card.building.longbow-camp" },
            ProxyStrategy.MagicSiege when (seed & 1) == 0 => new[] { "card.building.stone-gatherer-camp", "card.building.iron-gatherer-camp", "card.building.pasture", "card.building.iron-smelter", "card.building.stoneworks", "card.building.cannon-camp" },
            ProxyStrategy.MagicSiege => new[] { "card.building.iron-gatherer-camp", "card.building.iron-smelter", "card.building.mage-camp" },
            _ => new[] { "card.building.iron-gatherer-camp", "card.building.pasture", "card.building.iron-smelter", "card.building.heavy-warrior-camp", "card.building.longbow-camp", "card.building.mage-camp", "card.building.cannon-camp", "card.building.research-lab" }
        };

        private static int ProxyReplacementPriority(string buildingId, ProxyStrategy strategy)
        {
            if (buildingId == "building.sawmill") return 0;
            if ((strategy is ProxyStrategy.MagicSiege or ProxyStrategy.Melee) && buildingId == "building.archer-camp") return 1;
            if (strategy == ProxyStrategy.MagicSiege && buildingId == "building.winery") return 2;
            if (buildingId == "building.shield-camp") return 3;
            return 10;
        }

        private static bool HasBuildingForCard(MatchRuntime runtime, string cardId)
        {
            if (!cardId.StartsWith("card.building.", StringComparison.Ordinal)) return false;
            var buildingId = "building." + cardId.Substring("card.building.".Length);
            return runtime.Buildings.GetSnapshot().Any(value => value.BuildingId?.Value == buildingId);
        }

        private static void ResumeAffordableGatheringBuildings(MatchRuntime runtime)
        {
            foreach (var building in runtime.Buildings.GetSnapshot().Where(value => value.Paused))
            {
                var config = runtime.Buildings.GetConfig(building.InstanceId);
                if (config?.Category != BuildingCategory.Gathering ||
                    config.GathererDispatchCosts.Any(cost =>
                        runtime.Economy.GetAvailable(cost.ResourceId) < cost.Amount))
                    continue;
                runtime.Buildings.TryResumeAfterResourceShortage(building.InstanceId);
            }
        }

        private static void ManageFoodProcessor(MatchRuntime runtime, string buildingId, bool enabled)
        {
            var building = runtime.Buildings.GetSnapshot().FirstOrDefault(value => value.BuildingId?.Value == buildingId);
            if (building == null) return;
            var food = runtime.Economy.GetAvailable(new ResourceId("resource.food"));
            if (enabled && building.Paused && food >= 25)
                runtime.Buildings.TryResumeAfterResourceShortage(building.InstanceId);
        }

        private static void ManageProcessor(MatchRuntime runtime, string buildingId, ResourceId inputResourceId,
            bool enabled, int pauseBelow, int resumeAt)
        {
            var building = runtime.Buildings.GetSnapshot().FirstOrDefault(value => value.BuildingId?.Value == buildingId);
            if (building == null) return;
            var available = runtime.Economy.GetAvailable(inputResourceId);
            if (enabled && building.Paused && available >= resumeAt)
                runtime.Buildings.TryResumeAfterResourceShortage(building.InstanceId);
        }

        private static void IssuePlayerOrders(MatchRuntime runtime, ProxyStrategy strategy, int seed, int tick)
        {
            var startTick = strategy switch
            {
                ProxyStrategy.EconomyPriority => 600,
                ProxyStrategy.Melee => 400,
                ProxyStrategy.Ranged => 500,
                ProxyStrategy.MagicSiege => 600,
                _ => 500
            };
            if (tick < startTick)
                return;

            if (tick % 600 == 0 && !runtime.PlayerResearch.GetSnapshot().Active)
            {
                var candidates = runtime.PlayerResearch.GetCandidates();
                if (candidates.Count > 0) runtime.PlayerResearch.TryStart(candidates[0].Id);
            }
            if (strategy == ProxyStrategy.MagicSiege && tick >= 2400 && tick < 3300) return;

            var advancedStart = strategy == ProxyStrategy.MagicSiege ? 3300 : 2700;
            var developingAdvanced = strategy is ProxyStrategy.Melee or ProxyStrategy.Ranged or
                ProxyStrategy.MagicSiege or ProxyStrategy.Mixed;
            var interval = developingAdvanced && tick < advancedStart ? 120 :
                strategy == ProxyStrategy.EconomyPriority ? 100 : 60;
            if (tick % 20 != 0) return;
            var activeAndQueued = runtime.Combat.GetUnits().Count(value => value.Faction == MatchFaction.Player) +
                                  runtime.Training.GetSnapshot().Sum(value => value.Remaining);
            var desiredForce = tick < 3000 ? 8 : tick < 6000 ? 14 : 20;
            if (activeAndQueued >= desiredForce || tick % interval != 0) return;
            var lane = (tick / interval) % 3;
            var offset = tick / interval;
            var point = DeploymentPoint.World(650, 270 + lane * 270, lane);
            var sequence = strategy switch
            {
                ProxyStrategy.Melee when tick >= advancedStart => new[] { "unit.heavy-warrior" },
                ProxyStrategy.Melee => new[] { "unit.shield-guard" },
                ProxyStrategy.Ranged when tick >= advancedStart => new[] { "unit.longbow" },
                ProxyStrategy.Ranged => new[] { "unit.archer", "unit.shield-guard" },
                ProxyStrategy.MagicSiege when tick >= advancedStart && (seed & 1) == 0 => new[] { "unit.cannon" },
                ProxyStrategy.MagicSiege when tick >= advancedStart => new[] { "unit.mage" },
                ProxyStrategy.MagicSiege => new[] { "unit.shield-guard" },
                ProxyStrategy.Mixed => new[] { new[] { "unit.shield-guard", "unit.archer", "unit.longbow", "unit.longbow" }[offset % 4] },
                _ => new[] { "unit.shield-guard", "unit.archer" }
            };
            for (var index = 0; index < sequence.Length; index++)
            {
                var unitId = new UnitId(sequence[(offset + index) % sequence.Length]);
                if (runtime.Training.TryCreateOrder(unitId, 1, point, out _) == TrainingFailure.None) break;
            }

        }

        private sealed class AssetResourceService : IResourceService
        {
            private readonly GameContentConfig _asset;
            public AssetResourceService(GameContentConfig asset) => _asset = asset;
            public Task<IAssetLease<T>> AcquireAsync<T>(ResourceKey key,
                CancellationToken cancellationToken) where T : UnityEngine.Object =>
                Task.FromResult<IAssetLease<T>>(new Lease<T>(key, _asset as T));
            public Task<IInstanceLease> SpawnAsync(ResourceKey key, UnityEngine.Transform parent,
                CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task PreloadAsync(IReadOnlyCollection<ResourceKey> keys,
                CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class Lease<T> : IAssetLease<T> where T : UnityEngine.Object
        {
            public Lease(ResourceKey key, T asset)
            {
                Key = key;
                Asset = asset;
            }

            public ResourceKey Key { get; }
            public T Asset { get; }
            public void Dispose()
            {
            }
        }

        
private static void WriteReport(string label, IReadOnlyList<ProxyResult> results)
        {
            TestContext.WriteLine($"CADENCE_REPORT {label}");
            foreach (var strategyGroup in results.GroupBy(value => (value.Mode, value.Strategy))
                         .OrderBy(value => value.Key.Mode).ThenBy(value => value.Key.Strategy))
            {
                var endedByStrategy = strategyGroup.Where(value => value.Ended).ToArray();
                var unitText = string.Join(",", strategyGroup.SelectMany(value => value.UnitUsage)
                    .GroupBy(value => value.Key).OrderBy(value => value.Key)
                    .Select(group => group.Key.Replace("unit.", string.Empty) + ":" +
                                     group.Sum(value => value.Value)));
                TestContext.WriteLine($"  {strategyGroup.Key.Mode}/{strategyGroup.Key.Strategy}: " +
                    $"observed={strategyGroup.Count()} naturallyEnded={endedByStrategy.Length} " +
                    $"playerWins={strategyGroup.Count(value => value.PlayerWon)} " +
                    $"avgNaturalEndTicks={endedByStrategy.Select(value => value.DurationTicks).DefaultIfEmpty(0).Average():0} " +
                    $"researchAvg={strategyGroup.Average(value => value.ResearchRanks):0.0} " +
                    $"freeRaw={strategyGroup.Sum(value => value.FreeGathered)} paidRaw={strategyGroup.Sum(value => value.PaidGathered)} " +
                    $"paidCost={strategyGroup.Sum(value => value.PaidDispatchCost)} paidDeaths={strategyGroup.Sum(value => value.PaidDeaths)} " +
                    $"units=[{unitText}]");
            }

            foreach (var group in results.GroupBy(value => value.Mode).OrderBy(value => value.Key))
            {
                var pressure = group.Select(value => value.AveragePressureIntervalTicks)
                    .Where(value => value > 0).DefaultIfEmpty(0).Average();
                var cycles = group.Select(value => value.CompletedPressureCycles).Average();
                var longestGap = group.Select(value => value.LongestPressureGapTicks).Max();
                var repetition = group.Select(value => value.MaximumConsecutiveIntentCount).Max();
                TestContext.WriteLine(
                    $"{group.Key}: samples={group.Count()} naturallyEnded={group.Count(value => value.Ended)} " +
                    $"firstPressureTicks={group.Min(value => value.FirstPressureTick)}..{group.Max(value => value.FirstPressureTick)} " +
                    $"avgPressureTicks={pressure:0} avgCompletedCycles={cycles:0.0} " +
                    $"longestObservedGapTicks={longestGap} maxConsecutiveIntent={repetition}");
            }
        }
    }
}
