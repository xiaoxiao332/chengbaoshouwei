using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Core.AI;
using FortressFrontier.Core.Identifiers;
using NUnit.Framework;

namespace FortressFrontier.Tests
{
    public sealed class AiDecisionPipelineTests
    {
        [Test]
        public void HardGateFailures_AreStableAndExcludedBeforeScoring()
        {
            var candidates = new[]
            {
                new AiActionCandidate("a.resource", "intent.assault", AiCommandKind.Train, "s", "t", "route.middle", AiGateFailureReason.Resource),
                new AiActionCandidate("b.card", "intent.build-tower", AiCommandKind.BuildTower, "s", "t", "route.middle", AiGateFailureReason.Card),
                new AiActionCandidate("c.legal", "intent.reserve", AiCommandKind.None, "s", "t", "route.middle", AiGateFailureReason.None)
            };
            Assert.That(candidates.Select(value => value.FailureReason), Is.EqualTo(new[]
                { AiGateFailureReason.Resource, AiGateFailureReason.Card, AiGateFailureReason.None }));
            var scored = new AiUtilityScorer().Score(candidates, new AiFeatureValue[0], new AiUtilityCoefficient[0],
                new Dictionary<string, int> { ["intent.reserve"] = 10 }, string.Empty, 0, null, 0);
            Assert.That(scored.Select(value => value.Candidate.CandidateId), Is.EqualTo(new[] { "c.legal" }));
        }

[Test]
        public void TempoController_ProducesPublicRallyPressureAndRecoveryStates()
        {
            var controller = new AiTempoController();
            var config = new AiTempoConfig(550, 650, 750, 22, 8);

            var rallying = controller.Evaluate(config, 600, 0, 8, 2);
            Assert.That(rallying.State, Is.EqualTo(AiTempoState.Rallying));
            Assert.That(rallying.PressureDueMilli, Is.Zero);

            var due = controller.Evaluate(config, 700, 0, 8, 2);
            Assert.That(due.State, Is.EqualTo(AiTempoState.PressureDue));
            Assert.That(due.PressureDueMilli, Is.EqualTo(500));

            var postPressureRecovery = controller.Evaluate(config, 900, 600, 8, 2);
            Assert.That(postPressureRecovery.State, Is.EqualTo(AiTempoState.Recovering));
            Assert.That(postPressureRecovery.RecoveryNeededMilli, Is.GreaterThan(0));

            
var recovery = controller.Evaluate(config, 700, 0, 23, 2);
            Assert.That(recovery.State, Is.EqualTo(AiTempoState.Recovering));
            Assert.That(recovery.OverextensionMilli, Is.GreaterThan(0));
        }

        [Test]
        public void TempoController_UsesCooldownAndArmyCapWithoutBypassingLegalCosts()
        {
            var controller = new AiTempoController();
            var config = new AiTempoConfig(350, 450, 550, 24, 10);
            Assert.That(controller.GetOffensiveGateFailure(config, 349, 0, 5, 0, false),
                Is.EqualTo(AiGateFailureReason.PacingCooldown));
            Assert.That(controller.GetOffensiveGateFailure(config, 500, 0, 24, 0, false),
                Is.EqualTo(AiGateFailureReason.ArmyCap));
            Assert.That(controller.GetOffensiveGateFailure(config, 500, 0, 5, 0, false),
                Is.EqualTo(AiGateFailureReason.None));
        }

        [Test]
        public void RepetitionPenalty_IsCappedAtThreeConsecutiveSelections()
        {
            var candidate = new AiActionCandidate("candidate.reserve", "intent.reserve",
                AiCommandKind.None, "", "", "", AiGateFailureReason.None);
            var scorer = new AiUtilityScorer();
            var three = scorer.Score(new[] { candidate }, new AiFeatureValue[0],
                new AiUtilityCoefficient[0], new Dictionary<string, int> { ["intent.reserve"] = 1000 },
                string.Empty, 0, new Dictionary<string, int> { ["intent.reserve"] = 3 }, 100).Single();
            var excessive = scorer.Score(new[] { candidate }, new AiFeatureValue[0],
                new AiUtilityCoefficient[0], new Dictionary<string, int> { ["intent.reserve"] = 1000 },
                string.Empty, 0, new Dictionary<string, int> { ["intent.reserve"] = 99 }, 100).Single();
            Assert.That(excessive.Score, Is.EqualTo(three.Score));
        }


        [Test]
        public void FixedPointSoftmax_IsDeterministicAtIntegerExtremes()
        {
            var values = new[]
            {
                new AiScoredCandidate(new AiActionCandidate("a", "intent.assault", AiCommandKind.Train, "", "", "", AiGateFailureReason.None), int.MaxValue),
                new AiScoredCandidate(new AiActionCandidate("b", "intent.reserve", AiCommandKind.None, "", "", "", AiGateFailureReason.None), int.MinValue)
            };
            uint leftState = 77, rightState = 77;
            var selector = new AiIntentSelector();
            var left = Enumerable.Range(0, 64).Select(_ => selector.Select(values, 1, ref leftState, false).Candidate.CandidateId).ToArray();
            var right = Enumerable.Range(0, 64).Select(_ => selector.Select(values, 1, ref rightState, false).Candidate.CandidateId).ToArray();
            Assert.That(left, Is.EqualTo(right));
            Assert.That(left, Has.All.EqualTo("a"));
        }

        [Test]
        public void Commitment_IsAtLeastEightSeconds_AndOnlyExplicitInterruptsSwitch()
        {
            var commitment = new AiCommitmentController();
            commitment.Commit("intent.assault", 100, 1);
            Assert.That(commitment.UntilTick, Is.EqualTo(180));
            Assert.That(commitment.CanSwitch(120, AiCommitmentInterruptReason.None), Is.False);
            foreach (var reason in new[] { AiCommitmentInterruptReason.WallEmergency, AiCommitmentInterruptReason.TargetLost,
                         AiCommitmentInterruptReason.PathInvalid, AiCommitmentInterruptReason.TransactionFailed,
                         AiCommitmentInterruptReason.BossEvent })
                Assert.That(commitment.CanSwitch(120, reason), Is.True, reason.ToString());
        }

        [Test]
        public void EconomyForecast_UsesThirtySixtyNinetySecondHorizonsWithoutOverflow()
        {
            var forecasts = new AiEconomyForecaster().Forecast(int.MaxValue - 10, int.MaxValue);
            Assert.That(forecasts.Select(value => value.HorizonTicks), Is.EqualTo(new[] { 300, 600, 900 }));
            Assert.That(forecasts.Select(value => value.ProjectedAvailable), Has.All.EqualTo(int.MaxValue));
        }

        [Test]
        public void ResourceForecast_AccountsForReservationProcessingAndNegativeFoodFlow()
        {
            var food = new ResourceId("resource.food");
            var forecasts = new AiEconomyForecaster().Forecast(new[]
            {
                new AiResourceFlow(food, 20, 5, 100, 300, 0, 30)
            }, 100);
            Assert.That(forecasts.Select(value => value.HorizonTicks), Is.EqualTo(new[] { 300, 600, 900 }));
            Assert.That(forecasts[0].CurrentInventory, Is.EqualTo(20));
            Assert.That(forecasts[0].ReservedSpending, Is.EqualTo(5));
            Assert.That(forecasts[0].GatheredIncome, Is.EqualTo(30));
            Assert.That(forecasts[0].FacilityConsumption, Is.EqualTo(90));
            Assert.That(forecasts[0].NetFlow, Is.LessThan(0));
            Assert.That(forecasts[0].EarliestAffordableTick, Is.EqualTo(-1));
        }

        [Test]
        public void CandidateForwardSimulation_IsBoundedToThirtySeconds()
        {
            var candidate = new AiActionCandidate("candidate", "intent.assault", AiCommandKind.Train, "source", "wall",
                "route.middle", AiGateFailureReason.None, new[] { new AiResourceCost(new ResourceId("resource.food"), 20) },
                400, 100, new AiCommandPlan("formation.probe", new[] { "train" }, "all-orders-enqueued"));
            var simulator = new AiForwardSimulator();
            Assert.That(simulator.Evaluate(candidate, 300), Is.EqualTo(simulator.Evaluate(candidate, 900)));
            Assert.That(simulator.Evaluate(candidate, 150), Is.LessThan(simulator.Evaluate(candidate, 300)));
        }
    }
}
