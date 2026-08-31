using System;
using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Core.Identifiers;

namespace FortressFrontier.Core.AI
{
    public enum AiIntentKind { Develop, Hold, Assault, RaidEconomy, BuildTower, Research, Reserve }
    public enum AiGateFailureReason { None, Resource, Card, TrainingSlot, ResearchSlot, PhasePermission, TowerLimit, ForbiddenZone, UnreachablePath, MissingTarget, PacingCooldown, ArmyCap, ProtectedReserve }
    public enum AiCommitmentInterruptReason { None, WallEmergency, LogisticsThreat, TargetLost, PathInvalid, TransactionFailed, BossEvent }
    public enum AiCommandKind { None, Facility, Train, Research, BuildTower }

    public readonly struct AiFeatureValue
    {
        public AiFeatureValue(string id, int valueMilli) { Id = id ?? string.Empty; ValueMilli = valueMilli; }
        public string Id { get; }
        public int ValueMilli { get; }
    }

    public readonly struct AiUtilityCoefficient
    {
        public AiUtilityCoefficient(string featureId, string intentId, int coefficient)
        { FeatureId = featureId ?? string.Empty; IntentId = intentId ?? string.Empty; Coefficient = coefficient; }
        public string FeatureId { get; }
        public string IntentId { get; }
        public int Coefficient { get; }
    }

    public readonly struct AiActionCandidate
    {
        public AiActionCandidate(string candidateId, string intentId, AiCommandKind commandKind, string sourceId,
            string targetId, string routeId, AiGateFailureReason failureReason)
            : this(candidateId, intentId, commandKind, sourceId, targetId, routeId, failureReason,
                Array.Empty<AiResourceCost>(), 0, 0, AiCommandPlan.Empty) { }
        public AiActionCandidate(string candidateId, string intentId, AiCommandKind commandKind, string sourceId,
            string targetId, string routeId, AiGateFailureReason failureReason, IReadOnlyList<AiResourceCost> costs,
            int expectedBenefitMilli, int riskMilli, AiCommandPlan commandPlan)
        { CandidateId = candidateId ?? string.Empty; IntentId = intentId ?? string.Empty; CommandKind = commandKind; SourceId = sourceId ?? string.Empty; TargetId = targetId ?? string.Empty; RouteId = routeId ?? string.Empty; FailureReason = failureReason; Costs = costs ?? Array.Empty<AiResourceCost>(); ExpectedBenefitMilli = expectedBenefitMilli; RiskMilli = riskMilli; CommandPlan = commandPlan ?? AiCommandPlan.Empty; }
        public string CandidateId { get; }
        public string IntentId { get; }
        public AiCommandKind CommandKind { get; }
        public string SourceId { get; }
        public string TargetId { get; }
        public string RouteId { get; }
        public AiGateFailureReason FailureReason { get; }
        public IReadOnlyList<AiResourceCost> Costs { get; }
        public int ExpectedBenefitMilli { get; }
        public int RiskMilli { get; }
        public AiCommandPlan CommandPlan { get; }
        public bool IsLegal => FailureReason == AiGateFailureReason.None;
    }

    public readonly struct AiResourceCost
    {
        public AiResourceCost(ResourceId resourceId, int amount) { ResourceId = resourceId; Amount = Math.Max(0, amount); }
        public ResourceId ResourceId { get; }
        public int Amount { get; }
    }

    public sealed class AiCommandPlan
    {
        public static AiCommandPlan Empty { get; } = new(string.Empty, Array.Empty<string>(), "none");
        public AiCommandPlan(string planId, IReadOnlyList<string> commandIds, string completionPolicy)
        { PlanId = planId ?? string.Empty; CommandIds = commandIds ?? Array.Empty<string>(); CompletionPolicy = completionPolicy ?? string.Empty; }
        public string PlanId { get; }
        public IReadOnlyList<string> CommandIds { get; }
        public string CompletionPolicy { get; }
    }

    public interface IAiActionCandidateProvider
    {
        string ProviderId { get; }
        IReadOnlyList<AiActionCandidate> BuildCandidates();
    }

    public readonly struct AiScoredCandidate
    {
        public AiScoredCandidate(AiActionCandidate candidate, int score) { Candidate = candidate; Score = score; }
        public AiActionCandidate Candidate { get; }
        public int Score { get; }
    }

    public readonly struct AiDecision
    {
        public AiDecision(AiActionCandidate candidate, int score, bool suboptimal)
        { Candidate = candidate; Score = score; WasSuboptimal = suboptimal; }
        public AiActionCandidate Candidate { get; }
        public int Score { get; }
        public bool WasSuboptimal { get; }
    }

    public readonly struct AiEconomyForecast
    {
        public AiEconomyForecast(ResourceId resourceId, int horizonTicks, int currentInventory, int reservedSpending,
            int gatheredIncome, int facilityConsumption, int processingOutput, int netFlow, int projectedAvailable,
            int earliestAffordableTick)
        { ResourceId = resourceId; HorizonTicks = horizonTicks; CurrentInventory = currentInventory; ReservedSpending = reservedSpending;
          GatheredIncome = gatheredIncome; FacilityConsumption = facilityConsumption; ProcessingOutput = processingOutput;
          NetFlow = netFlow; ProjectedAvailable = projectedAvailable; EarliestAffordableTick = earliestAffordableTick; }
        public AiEconomyForecast(int horizonTicks, int projectedAvailable)
            : this(default, horizonTicks, projectedAvailable, 0, 0, 0, 0, 0, projectedAvailable, 0) { }
        public ResourceId ResourceId { get; }
        public int HorizonTicks { get; }
        public int CurrentInventory { get; }
        public int ReservedSpending { get; }
        public int GatheredIncome { get; }
        public int FacilityConsumption { get; }
        public int ProcessingOutput { get; }
        public int NetFlow { get; }
        public int ProjectedAvailable { get; }
        public int EarliestAffordableTick { get; }
    }

    public readonly struct AiResourceFlow
    {
        public AiResourceFlow(ResourceId resourceId, int currentInventory, int reservedSpending, int gatherPerTickMilli,
            int facilityConsumptionPerTickMilli, int processingOutputPerTickMilli, int requiredBudget)
        { ResourceId = resourceId; CurrentInventory = Math.Max(0, currentInventory); ReservedSpending = Math.Max(0, reservedSpending);
          GatherPerTickMilli = gatherPerTickMilli; FacilityConsumptionPerTickMilli = facilityConsumptionPerTickMilli;
          ProcessingOutputPerTickMilli = processingOutputPerTickMilli; RequiredBudget = Math.Max(0, requiredBudget); }
        public ResourceId ResourceId { get; }
        public int CurrentInventory { get; }
        public int ReservedSpending { get; }
        public int GatherPerTickMilli { get; }
        public int FacilityConsumptionPerTickMilli { get; }
        public int ProcessingOutputPerTickMilli { get; }
        public int RequiredBudget { get; }
    }

    public sealed class AiEconomyForecaster
    {
        public IReadOnlyList<AiEconomyForecast> Forecast(int available, int netPerTick)
        {
            var horizons = new[] { 300, 600, 900 };
            return horizons.Select(value => new AiEconomyForecast(value,
                Saturate((long)Math.Max(0, available) + (long)netPerTick * value))).ToArray();
        }
        public IReadOnlyList<AiEconomyForecast> Forecast(IReadOnlyList<AiResourceFlow> flows, int currentTick)
        {
            if (flows == null) return Array.Empty<AiEconomyForecast>();
            var result = new List<AiEconomyForecast>();
            foreach (var flow in flows.OrderBy(value => value.ResourceId.Value, StringComparer.Ordinal))
            {
                var netMilli = (long)flow.GatherPerTickMilli - flow.FacilityConsumptionPerTickMilli + flow.ProcessingOutputPerTickMilli;
                var baseAvailable = Math.Max(0, flow.CurrentInventory - flow.ReservedSpending);
                var earliest = baseAvailable >= flow.RequiredBudget ? currentTick : netMilli <= 0 ? -1 :
                    checked(currentTick + (int)Math.Min(int.MaxValue - (long)currentTick,
                        ((long)(flow.RequiredBudget - baseAvailable) * 1000 + netMilli - 1) / netMilli));
                foreach (var horizon in new[] { 300, 600, 900 })
                {
                    var gathered = Saturate((long)flow.GatherPerTickMilli * horizon / 1000);
                    var consumed = Saturate((long)flow.FacilityConsumptionPerTickMilli * horizon / 1000);
                    var output = Saturate((long)flow.ProcessingOutputPerTickMilli * horizon / 1000);
                    var projected = Saturate((long)baseAvailable + gathered - consumed + output);
                    result.Add(new AiEconomyForecast(flow.ResourceId, horizon, flow.CurrentInventory, flow.ReservedSpending,
                        gathered, consumed, output, SaturateSigned(netMilli * horizon / 1000), projected, earliest));
                }
            }
            return result;
        }
        private static int Saturate(long value) => value > int.MaxValue ? int.MaxValue : value < 0 ? 0 : (int)value;
        private static int SaturateSigned(long value) => value > int.MaxValue ? int.MaxValue : value < int.MinValue ? int.MinValue : (int)value;
    }

    public sealed class AiUtilityScorer
    {
        private readonly AiPortfolioPlanner _portfolioPlanner = new();
        public IReadOnlyList<AiScoredCandidate> Score(IReadOnlyList<AiActionCandidate> candidates,
            IReadOnlyList<AiFeatureValue> features, IReadOnlyList<AiUtilityCoefficient> coefficients,
            IReadOnlyDictionary<string, int> baseWeights, string committedIntentId, int switchCost,
            IReadOnlyDictionary<string, int> repetitions, int repetitionPenalty)
        {
            var featureMap = features.ToDictionary(value => value.Id, value => value.ValueMilli, StringComparer.Ordinal);
            var result = new List<AiScoredCandidate>();
            foreach (var candidate in candidates.Where(value => value.IsLegal).OrderBy(value => value.CandidateId, StringComparer.Ordinal))
            {
                var score = baseWeights.TryGetValue(candidate.IntentId, out var weight) ? weight : 0;
                foreach (var coefficient in coefficients)
                    if (coefficient.IntentId == candidate.IntentId && featureMap.TryGetValue(coefficient.FeatureId, out var feature))
                        score = SaturatingAdd(score, (long)coefficient.Coefficient * feature / 1000L);
                if (!string.IsNullOrEmpty(committedIntentId) && committedIntentId != candidate.IntentId) score -= Math.Max(0, switchCost);
                if (repetitions != null && repetitions.TryGetValue(candidate.IntentId, out var count)) score -= Math.Min(3, Math.Max(0, count)) * Math.Max(0, repetitionPenalty);
                score = SaturatingAdd(score, _portfolioPlanner.EvaluatePrimary(candidate, candidates));
                result.Add(new AiScoredCandidate(candidate, score));
            }
            return result;
        }

        private static int SaturatingAdd(int left, long right)
        { var total = (long)left + right; return total > int.MaxValue ? int.MaxValue : total < int.MinValue ? int.MinValue : (int)total; }
    }

    public sealed class AiForwardSimulator
    {
        public int Evaluate(AiActionCandidate candidate, int horizonTicks)
        {
            var boundedHorizon = Math.Clamp(horizonTicks, 1, 300);
            var benefit = (long)candidate.ExpectedBenefitMilli * boundedHorizon / 300;
            var exposure = (long)candidate.RiskMilli * boundedHorizon / 300;
            var liquidityPenalty = candidate.Costs.Sum(value => (long)value.Amount) * boundedHorizon / 3000;
            var result = benefit - exposure - liquidityPenalty;
            return result > int.MaxValue ? int.MaxValue : result < int.MinValue ? int.MinValue : (int)result;
        }
    }

    public sealed class AiPortfolioPlanner
    {
        private readonly AiForwardSimulator _simulator = new();
        public int EvaluatePrimary(AiActionCandidate primary, IReadOnlyList<AiActionCandidate> candidates)
        {
            var primaryValue = _simulator.Evaluate(primary, 150);
            var followup = candidates.Where(value => value.IsLegal && value.CandidateId != primary.CandidateId)
                .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                .Select(value => _simulator.Evaluate(value, 150)).DefaultIfEmpty(0).Max();
            var combined = (long)primaryValue + followup / 2L;
            return combined > int.MaxValue ? int.MaxValue : combined < int.MinValue ? int.MinValue : (int)combined;
        }
    }

    public sealed class AiIntentSelector
    {
        private static readonly int[] ExpLookup = { 1000000, 904837, 818731, 740818, 670320, 606531, 548812, 496585, 449329, 406570, 367879, 332871, 301194, 272532, 246597, 223130, 201897, 182684, 165299, 149569, 135335, 122456, 110803, 100259, 90718, 82085, 74274, 67199, 60810, 55023, 49787, 45049, 40762, 36883, 33373, 30197, 27324, 24714, 22313, 20190, 18268 };

        public AiDecision Select(IReadOnlyList<AiScoredCandidate> scored, int temperatureMilli, ref uint randomState, bool forceSuboptimal)
        {
            if (scored == null || scored.Count == 0) return default;
            var ordered = scored.OrderByDescending(value => value.Score).ThenBy(value => value.Candidate.CandidateId, StringComparer.Ordinal).ToArray();
            if (forceSuboptimal && ordered.Length > 1) return new AiDecision(ordered[1].Candidate, ordered[1].Score, true);
            var best = ordered[0].Score;
            long total = 0;
            var weights = new int[ordered.Length];
            for (var index = 0; index < ordered.Length; index++)
            {
                var delta = Math.Max(0L, (long)best - ordered[index].Score);
                var bucket = Math.Min(ExpLookup.Length - 1, (int)Math.Min(int.MaxValue, delta * 1000 / Math.Max(1, temperatureMilli)));
                weights[index] = ExpLookup[bucket]; total += weights[index];
            }
            randomState = unchecked(randomState * 1664525u + 1013904223u);
            var roll = (long)(randomState & 0x7fffffff) * total / 0x80000000L;
            for (var index = 0; index < ordered.Length; index++) { roll -= weights[index]; if (roll < 0) return new AiDecision(ordered[index].Candidate, ordered[index].Score, index != 0); }
            return new AiDecision(ordered[ordered.Length - 1].Candidate, ordered[ordered.Length - 1].Score, true);
        }
    }

    public sealed class AiCommitmentController
    {
        public string IntentId { get; private set; } = string.Empty;
        public string CandidateId { get; private set; } = string.Empty;
        public string TargetId { get; private set; } = string.Empty;
        public string CompletionPolicy { get; private set; } = string.Empty;
        public int UntilTick { get; private set; }
        public bool IsCommitted(int tick) => !string.IsNullOrEmpty(IntentId) && tick < UntilTick;
        public bool CanSwitch(int tick, AiCommitmentInterruptReason reason) => !IsCommitted(tick) || reason != AiCommitmentInterruptReason.None;
        public void Commit(string intentId, int tick, int minimumTicks) { IntentId = intentId ?? string.Empty; UntilTick = tick + Math.Max(80, minimumTicks); }
        public void Commit(string intentId, string candidateId, string targetId, string completionPolicy, int tick, int minimumTicks)
        { Commit(intentId, tick, minimumTicks); CandidateId = candidateId ?? string.Empty; TargetId = targetId ?? string.Empty; CompletionPolicy = completionPolicy ?? string.Empty; }
        public void Clear() { IntentId = string.Empty; CandidateId = string.Empty; TargetId = string.Empty; CompletionPolicy = string.Empty; UntilTick = 0; }
    }
}
