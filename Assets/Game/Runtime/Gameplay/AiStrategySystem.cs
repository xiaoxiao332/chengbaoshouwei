using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.AI;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Gameplay
{
    public enum AiDefenseTriggerKind { None, WallDefense, LogisticsDefense }

    public sealed class AiRouteThreatSnapshot
    {
        public AiRouteThreatSnapshot(RouteId routeId, IReadOnlyList<int> attackerHandles, int lastHitTick,
            int hitCount, int deathCount, int lostCarriedAmount, int threatStrength, string defenseTriggerId)
        {
            RouteId = routeId; AttackerHandles = attackerHandles ?? Array.Empty<int>(); LastHitTick = lastHitTick;
            HitCount = hitCount; DeathCount = deathCount; LostCarriedAmount = lostCarriedAmount;
            ThreatStrength = threatStrength; DefenseTriggerId = defenseTriggerId ?? string.Empty;
        }
        public RouteId RouteId { get; }
        public IReadOnlyList<int> AttackerHandles { get; }
        public int LastHitTick { get; }
        public int HitCount { get; }
        public int DeathCount { get; }
        public int LostCarriedAmount { get; }
        public int ThreatStrength { get; }
        public string DefenseTriggerId { get; }
    }

    public readonly struct AiFacilityCommand
    {
        public AiFacilityCommand(string intentId, int slotIndex, BuildingId buildingId, CardId cardId) { IntentId = intentId; SlotIndex = slotIndex; BuildingId = buildingId; CardId = cardId; }
        public string IntentId { get; } public int SlotIndex { get; } public BuildingId BuildingId { get; } public CardId CardId { get; }
    }
    public readonly struct AiTrainCommand
    {
        public AiTrainCommand(string intentId, UnitId unitId, int quantity, DeploymentPoint point, RouteId routeId,
            TrainingOrderPriority priority = TrainingOrderPriority.Normal, string defenseTriggerId = "") { IntentId = intentId; UnitId = unitId; Quantity = quantity; Point = point; RouteId = routeId; Priority = priority; DefenseTriggerId = defenseTriggerId ?? string.Empty; }
        public string IntentId { get; } public UnitId UnitId { get; } public int Quantity { get; } public DeploymentPoint Point { get; } public RouteId RouteId { get; }
        public TrainingOrderPriority Priority { get; } public string DefenseTriggerId { get; }
    }
    public readonly struct AiResearchCommand
    {
        public AiResearchCommand(string intentId, ResearchCategory category) { IntentId = intentId; Category = category; }
        public string IntentId { get; } public ResearchCategory Category { get; }
    }
    public readonly struct AiBuildTowerCommand
    {
        public AiBuildTowerCommand(string intentId, int x, int y) { IntentId = intentId; X = x; Y = y; }
        public string IntentId { get; } public int X { get; } public int Y { get; }
    }

    public sealed class AiDecisionSnapshot
    {
        public AiDecisionSnapshot(int tick, string phaseId, string intentId, int utility, int commitmentUntilTick,
            string routeId, string targetId, string result, AiGateFailureReason gateFailure, bool suboptimal,
            string candidateId = "", string primaryShortage = "", int earliestAffordableTick = 0,
            string interruptionReason = "", AiTempoState tempoState = AiTempoState.Rallying,
            string commitmentCompletionReason = "", AiDefenseTriggerKind defenseTriggerKind = AiDefenseTriggerKind.None,
            string threatRouteId = "")
        {
            Tick = tick;
            PhaseId = phaseId;
            IntentId = intentId;
            Utility = utility;
            CommitmentUntilTick = commitmentUntilTick;
            RouteId = routeId;
            TargetId = targetId;
            Result = result;
            GateFailure = gateFailure;
            WasSuboptimal = suboptimal;
            CandidateId = candidateId;
            PrimaryShortage = primaryShortage;
            EarliestAffordableTick = earliestAffordableTick;
            InterruptionReason = interruptionReason;
            TempoState = tempoState;
            CommitmentCompletionReason = commitmentCompletionReason;
            DefenseTriggerKind = defenseTriggerKind;
            ThreatRouteId = threatRouteId ?? string.Empty;
        }

        public int Tick { get; }
        public string PhaseId { get; }
        public string IntentId { get; }
        public int Utility { get; }
        public int CommitmentUntilTick { get; }
        public string RouteId { get; }
        public string TargetId { get; }
        public string Result { get; }
        public AiGateFailureReason GateFailure { get; }
        public bool WasSuboptimal { get; }
        public string CandidateId { get; }
        public string PrimaryShortage { get; }
        public int EarliestAffordableTick { get; }
        public string InterruptionReason { get; }
        public AiTempoState TempoState { get; }
        public string CommitmentCompletionReason { get; }
        public AiDefenseTriggerKind DefenseTriggerKind { get; }
        public string ThreatRouteId { get; }
    }

    public sealed class AiHealthSnapshot
    {
        public AiHealthSnapshot(bool healthy, string defectId, string targetFormationId, string primaryShortage, int earliestAffordableTick)
        { Healthy = healthy; DefectId = defectId ?? string.Empty; TargetFormationId = targetFormationId ?? string.Empty; PrimaryShortage = primaryShortage ?? string.Empty; EarliestAffordableTick = earliestAffordableTick; }
        public bool Healthy { get; } public string DefectId { get; } public string TargetFormationId { get; }
        public string PrimaryShortage { get; } public int EarliestAffordableTick { get; }
    }

    public sealed class AiPerceptionSnapshot
    {
        public AiPerceptionSnapshot(int tick, IReadOnlyDictionary<int, int> playerByLane, IReadOnlyDictionary<int, int> enemyByLane,
            int exposedPlayerGatherers, int towerCoverage, int bossProximity, int enemyWallPressure, int playerWallPressure,
            int trainingQueue, int researchQueue, int constructionQueue, IReadOnlyList<ResourceBalanceSnapshot> resources,
            IReadOnlyList<AiRouteThreatSnapshot> routeThreats = null)
        { Tick = tick; PlayerByLane = playerByLane; EnemyByLane = enemyByLane; ExposedPlayerGatherers = exposedPlayerGatherers;
          TowerCoverage = towerCoverage; BossProximity = bossProximity; EnemyWallPressure = enemyWallPressure; PlayerWallPressure = playerWallPressure;
          TrainingQueue = trainingQueue; ResearchQueue = researchQueue; ConstructionQueue = constructionQueue; Resources = resources;
          RouteThreats = routeThreats ?? Array.Empty<AiRouteThreatSnapshot>(); }
        public int Tick { get; } public IReadOnlyDictionary<int, int> PlayerByLane { get; } public IReadOnlyDictionary<int, int> EnemyByLane { get; }
        public int ExposedPlayerGatherers { get; } public int TowerCoverage { get; } public int BossProximity { get; }
        public int EnemyWallPressure { get; } public int PlayerWallPressure { get; } public int TrainingQueue { get; }
        public int ResearchQueue { get; } public int ConstructionQueue { get; } public IReadOnlyList<ResourceBalanceSnapshot> Resources { get; }
        public IReadOnlyList<AiRouteThreatSnapshot> RouteThreats { get; }
    }

    public sealed class AiStrategySystem : GameSystemBase, IFixedMatchSimulation
    {
        private sealed class RuntimeCandidateProvider : IAiActionCandidateProvider
        {
            private readonly Func<IReadOnlyList<AiActionCandidate>> _factory;
            public RuntimeCandidateProvider(string providerId, Func<IReadOnlyList<AiActionCandidate>> factory)
            { ProviderId = providerId; _factory = factory; }
            public string ProviderId { get; }
            public IReadOnlyList<AiActionCandidate> BuildCandidates() => _factory();
        }
        private readonly MatchConfigSnapshot _config;
        private readonly BuildingSystem _buildings;
        private readonly TrainingSystem _training;
        private readonly EconomySystem _economy;
        private readonly MatchPhaseSystem _phases;
        private readonly TowerConstructionSystem _construction;
        private readonly ResearchSystem _research;
        private readonly CombatSystem _combat;
        private readonly HandAndOfferSystem _cards;
        private readonly GathererSystem _playerGatherers;
        private readonly AiUtilityScorer _scorer = new();
        private readonly AiIntentSelector _selector = new();
        
        private readonly AiTempoController _tempoController = new();
        private readonly Queue<AiPerceptionSnapshot> _perceptionHistory = new();
private readonly AiCommitmentController _commitment = new();
        private readonly List<AiDecisionSnapshot> _decisions = new();
        private readonly Dictionary<string, int> _repetitions = new(StringComparer.Ordinal);
        private readonly Dictionary<int, int> _playerUnitFirstSeen = new();
        private readonly Dictionary<string, int> _logisticsResponseUntilTick = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int> _logisticsRetryAfterTick = new(StringComparer.Ordinal);
        private uint _randomState;
        private int _nextDecisionTick;
        private int _orderIndex;
        private int _nextSuboptimalTick = int.MaxValue;
        private uint _mistakeRandomState;
        
        private string _lastPressureFormationId = string.Empty;
        private string _lastPressureRouteId = string.Empty;
        private int _consecutivePressureFormationCount;
        private int _consecutivePressureRouteCount;
private int _lastSuccessfulPressureTick;
        private AiTempoConfig _tempoConfig;
        private AiTempoSignals _tempoSignals;
        private AiPerceptionSnapshot _observedPerception;
        private bool _hasIssuedFormation;
        private AiHealthSnapshot _health = new(true, string.Empty, string.Empty, string.Empty, 0);

        public AiStrategySystem(MatchConfigSnapshot config, BuildingSystem buildings, TrainingSystem training,
            EconomySystem economy, MatchPhaseSystem phases, TowerConstructionSystem construction,
            ResearchSystem research, CombatSystem combat, HandAndOfferSystem cards, GathererSystem playerGatherers = null) : base(SystemLifetime.Scene)
        { _config = config ?? throw new ArgumentNullException(nameof(config)); _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings)); _training = training ?? throw new ArgumentNullException(nameof(training)); _economy = economy ?? throw new ArgumentNullException(nameof(economy)); _phases = phases ?? throw new ArgumentNullException(nameof(phases)); _construction = construction ?? throw new ArgumentNullException(nameof(construction)); _research = research ?? throw new ArgumentNullException(nameof(research)); _combat = combat ?? throw new ArgumentNullException(nameof(combat)); _cards = cards ?? throw new ArgumentNullException(nameof(cards)); _playerGatherers = playerGatherers; }

        public string CurrentIntentId => _commitment.IntentId;
        
        public AiTempoState CurrentTempoState => _tempoSignals.State;
        public AiTempoSignals GetTempoSignals() => _tempoSignals;
public int CommitmentUntilTick => _commitment.UntilTick;
        public IReadOnlyList<AiDecisionSnapshot> GetDecisions() => _decisions.ToArray();
        public AiHealthSnapshot GetHealth() => _health;
        public IReadOnlyList<AiRouteThreatSnapshot> GetVisibleRouteThreats() =>
            (_observedPerception?.RouteThreats ?? Array.Empty<AiRouteThreatSnapshot>()).ToArray();
        public AiPerceptionSnapshot GetPerception(int tick) => BuildPerception(tick);
        public event Action Changed;

protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            var slot = 0;
            foreach (var card in _cards.GetHand().OrderBy(value => value.Id.Value, StringComparer.Ordinal).ToArray())
            {
                while (slot < 9 && _buildings.GetSnapshot()[slot].BuildingId.HasValue) slot++;
                if (slot >= 9 || !_cards.TryPlayBuilding(card.Id, slot))
                    throw new InvalidOperationException($"Enemy starting card '{card.Id.Value}' could not be consumed into a legal building slot.");
                slot++;
            }

            _randomState = unchecked((uint)(_config.Seed * 747796405) ^ 0xA17E57u);
            _mistakeRandomState = unchecked((uint)(_config.Seed * 2891336453L) ^ 0x51B0A1u);
            _tempoConfig = new AiTempoConfig(
                _config.AiStrategy.PressureMinIntervalTicks,
                _config.AiStrategy.PressureTargetIntervalTicks,
                _config.AiStrategy.PressureMaxIntervalTicks,
                _config.AiStrategy.ActiveUnitSoftCap,
                _config.AiStrategy.QueuedUnitSoftCap);
            _nextDecisionTick = Math.Max(
                _config.AiStrategy.ReactionDelayTicks,
                _config.AiStrategy.DecisionIntervalTicks);
            ScheduleNextSuboptimal(0);
            CapturePerception(0);
            UpdateTempo(0);
            ResumeAffordableBuildings();
            return Task.CompletedTask;
        }

protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _decisions.Clear();
            _repetitions.Clear();
            _perceptionHistory.Clear();
            _observedPerception = null;
            _lastPressureFormationId = string.Empty;
            _lastPressureRouteId = string.Empty;
            _consecutivePressureFormationCount = 0;
            _consecutivePressureRouteCount = 0;
            
_commitment.Clear();
            return Task.CompletedTask;
        }

private void CapturePerception(int tick)
        {
            _perceptionHistory.Enqueue(BuildPerception(tick));
            var visibleTick = tick - _config.AiStrategy.ReactionDelayTicks;
            while (_perceptionHistory.Count > 0 && _perceptionHistory.Peek().Tick <= visibleTick)
                _observedPerception = _perceptionHistory.Dequeue();

            if (_observedPerception == null)
                _observedPerception = new AiPerceptionSnapshot(
                    0,
                    Enumerable.Range(0, 3).ToDictionary(lane => lane, _ => 0),
                    Enumerable.Range(0, 3).ToDictionary(lane => lane, _ => 0),
                    0, 0, 0, 0, 0, 0, 0, 0,
                    Array.Empty<ResourceBalanceSnapshot>());
        }

        private void UpdateTempo(int tick)
        {
            var activeUnits = _combat.GetUnits().Count(value => value.Faction == MatchFaction.Enemy);
            var queuedUnits = _training.GetSnapshot().Sum(value => value.Remaining);
            _tempoSignals = _tempoController.Evaluate(
                _tempoConfig, tick, _lastSuccessfulPressureTick, activeUnits, queuedUnits);
        }

        private void ScheduleNextSuboptimal(int fromTick)
        {
            var minimum = _config.AiStrategy.SuboptimalIntervalMinTicks;
            var maximum = _config.AiStrategy.SuboptimalIntervalMaxTicks;
            if (minimum <= 0 || maximum <= 0)
            {
                _nextSuboptimalTick = int.MaxValue;
                return;
            }

            _mistakeRandomState = unchecked(_mistakeRandomState * 1664525u + 1013904223u);
            var range = Math.Max(1, maximum - minimum + 1);
            _nextSuboptimalTick = checked(fromTick + minimum +
                (int)(_mistakeRandomState % (uint)range));
        }


public void SimulateTick(int tick)
        {
            ObservePlayerUnits(tick);
            CapturePerception(tick);
            UpdateHeatTempo(tick);
            UpdateTempo(tick);
            ResumeAffordableBuildings();

            var interrupt = ResolveInterrupt(tick);
            var commitmentCompletion = string.Empty;
            if (!string.IsNullOrEmpty(_commitment.IntentId) && tick >= _commitment.UntilTick)
            {
                commitmentCompletion = "normal-complete";
                _commitment.Clear();
            }
            else if (!string.IsNullOrEmpty(_commitment.IntentId) &&
                     interrupt != AiCommitmentInterruptReason.None)
            {
                commitmentCompletion = interrupt switch
                {
                    AiCommitmentInterruptReason.WallEmergency => "wall-emergency",
                    AiCommitmentInterruptReason.LogisticsThreat => "logistics-threat",
                    AiCommitmentInterruptReason.TargetLost => "target-lost",
                    AiCommitmentInterruptReason.PathInvalid => "path-invalid",
                    AiCommitmentInterruptReason.TransactionFailed => "transaction-failed",
                    AiCommitmentInterruptReason.BossEvent => "boss-event",
                    _ => string.Empty
                };
                _commitment.Clear();
            }

            if (TryIssueLogisticsDefense(tick, interrupt, commitmentCompletion))
                return;
            if (TryIssueFirstProbe(tick, interrupt))
                return;
            if (_training.GetSnapshot().Count == 0)
                ResolveCardOffer();
            if (tick < _nextDecisionTick || !_commitment.CanSwitch(tick, interrupt))
                return;

            _nextDecisionTick = tick + Math.Max(1, _config.AiStrategy.DecisionIntervalTicks);
            var phase = _config.Phases.FirstOrDefault(value => value.Id.Equals(_phases.CurrentPhaseId));
            var candidates = BuildCandidates(phase, tick);
            var legal = candidates.Where(value => value.IsLegal).ToArray();
            if (legal.Length == 0)
            {
                RecordBlocked(tick, phase, candidates);
                return;
            }

            var weights = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var id in ContentConstants.P1AiIntentIds)
                weights[id] = 0;
            if (phase != null)
                foreach (var weight in phase.BaseIntentWeights)
                    weights[weight.IntentId] = weight.Weight;
            foreach (var bias in _config.AiStrategy.DoctrineBiases)
                weights[bias.IntentId] = weights.GetValueOrDefault(bias.IntentId) + bias.Weight;

            var coefficients = _config.AiStrategy.FeatureCoefficients.Select(value =>
                new AiUtilityCoefficient(value.FeatureId, value.IntentId, value.Coefficient)).ToArray();
            var scored = _scorer.Score(
                legal, BuildFeatures(tick), coefficients, weights, _commitment.IntentId,
                _config.AiStrategy.SwitchCost, _repetitions, _config.AiStrategy.RepetitionPenalty);

            var forceSuboptimal = tick >= _nextSuboptimalTick;
            AiDecision decision;
            var forcedProbe = !_hasIssuedFormation &&
                              tick >= _config.AiStrategy.FirstProbeStartTick &&
                              tick <= _config.AiStrategy.FirstProbeEndTick
                ? scored.Where(value => value.Candidate.CommandPlan.PlanId == "formation.probe")
                    .OrderByDescending(value => value.Score)
                    .ThenBy(value => value.Candidate.CandidateId, StringComparer.Ordinal)
                    .FirstOrDefault()
                : default;
            if (!string.IsNullOrEmpty(forcedProbe.Candidate.CandidateId))
                decision = new AiDecision(forcedProbe.Candidate, forcedProbe.Score, false);
            else
                decision = _selector.Select(
                    scored,
                    Math.Max(1, _config.AiStrategy.TemperatureMilli * 1000 /
                        Math.Max(1, _config.AiStrategy.DecisionQualityMilli)),
                    ref _randomState,
                    forceSuboptimal);

            if (forceSuboptimal && decision.WasSuboptimal)
                ScheduleNextSuboptimal(tick);

            var result = Execute(decision.Candidate);
            var successful = IsSuccessfulExecution(result);
            if (successful)
            {
                var minimum = _config.AiStrategy.Commitments
                    .FirstOrDefault(value => value.IntentId == decision.Candidate.IntentId).MinimumTicks;
                _commitment.Commit(
                    decision.Candidate.IntentId, decision.Candidate.CandidateId,
                    decision.Candidate.TargetId, decision.Candidate.CommandPlan.CompletionPolicy,
                    tick, Math.Max(80, minimum));
                
                if (decision.Candidate.CommandKind == AiCommandKind.Train)
                    RecordPressurePattern(decision.Candidate);
if (decision.Candidate.CommandKind == AiCommandKind.Train)
                    _lastSuccessfulPressureTick = tick;
            }
            else
            {
                _commitment.Clear();
                commitmentCompletion = result.Contains("route", StringComparison.Ordinal)
                    ? "path-invalid"
                    : "transaction-failed";
            }

            _repetitions[decision.Candidate.IntentId] = Math.Min(
                3, _repetitions.GetValueOrDefault(decision.Candidate.IntentId) + 1);
            foreach (var other in _repetitions.Keys.ToArray())
                if (other != decision.Candidate.IntentId && _repetitions[other] > 0)
                    _repetitions[other]--;

            UpdateTempo(tick);
            _decisions.Add(new AiDecisionSnapshot(
                tick, phase?.Id.Value ?? string.Empty, decision.Candidate.IntentId,
                decision.Score, _commitment.UntilTick, decision.Candidate.RouteId,
                decision.Candidate.TargetId, result, AiGateFailureReason.None,
                decision.WasSuboptimal, decision.Candidate.CandidateId,
                _health.PrimaryShortage, _health.EarliestAffordableTick,
                interrupt.ToString(), _tempoSignals.State, commitmentCompletion));
            Changed?.Invoke();
        }

        private void ResolveCardOffer()
        {
            var offer = _cards.GetOffer();
            if (!offer.Active || offer.Choices.Count == 0) return;
            var selected = offer.Choices
                .OrderByDescending(ScoreRewardChoice)
                .ThenBy(value => value.Id.Value, StringComparer.Ordinal)
                .First();
            var claimed = _cards.ChooseOffer(selected.Id);
            if (!claimed && selected.Kind != RewardChoiceKind.ProcessedResourceBundle)
            {
                var replacement = _cards.GetHand().OrderBy(value => value.Id.Value, StringComparer.Ordinal).FirstOrDefault();
                claimed = replacement != null && _cards.TryReplaceAndChoose(selected.Id, replacement.Id);
            }
            if (!claimed) return;
            if (selected.Kind == RewardChoiceKind.ReinforcementItem)
            {
                var item = _cards.GetHand().FirstOrDefault(value =>
                    value.ReinforcementTemplateId.HasValue && selected.ReinforcementTemplateId.HasValue &&
                    value.ReinforcementTemplateId.Value.Equals(selected.ReinforcementTemplateId.Value));
                var area = _config.BattlefieldLayout.Zones.SingleOrDefault(value => value.Kind == ZoneKind.EnemyDeployment);
                if (item != null) _cards.TryDeployReinforcement(item.Id, _training,
                    area.X + area.Width / 2, area.Y + area.Height / 2);
                return;
            }
            if (selected.Kind != RewardChoiceKind.ContentCard || !selected.CardId.HasValue) return;
            var cardId = selected.CardId.Value;
            var building = _config.Buildings.FirstOrDefault(value => value.SourceCardId.Equals(cardId));
            if (building == null) return;
            if (building.Category == BuildingCategory.BattlefieldStructure) return;
            var empty = _buildings.GetSnapshot().FirstOrDefault(value => !value.BuildingId.HasValue);
            if (empty == null)
            {
                var replacement = _buildings.GetSnapshot()
                    .Where(value => value.BuildingId.HasValue)
                    .OrderBy(value => ReplacementPriority(value.BuildingId.Value.Value))
                    .ThenBy(value => value.SlotIndex)
                    .FirstOrDefault();
                if (replacement != null)
                {
                    _buildings.Demolish(replacement.InstanceId);
                    empty = _buildings.GetSnapshot()[replacement.SlotIndex];
                }
            }
            if (empty != null) _cards.TryPlayBuilding(cardId, empty.SlotIndex);
        }

        private int ScoreRewardChoice(RewardChoiceSnapshot choice) => (choice.Kind switch
        {
            RewardChoiceKind.ContentCard when choice.CardId.HasValue => ScoreOfferCard(choice.CardId.Value),
            RewardChoiceKind.ProcessedResourceBundle => 460 + choice.Resources.Sum(value =>
                value.Amount + Math.Max(0, 20 - (_economy.GetSnapshot().FirstOrDefault(balance => balance.Id.Equals(value.ResourceId))?.Amount ?? 0))),
            RewardChoiceKind.ReinforcementItem => 430 + choice.Units.Count * 45,
            _ => 0
        }) + (choice.Rarity switch { RewardRarity.Rare => 25, RewardRarity.Epic => 55, _ => 0 });

        private int ScoreOfferCard(CardId cardId)
        {
            var building = _config.Buildings.FirstOrDefault(value => value.SourceCardId.Equals(cardId));
            if (building == null) return 0;
            var alreadyBuilt = _buildings.GetSnapshot().Any(value => value.BuildingId.HasValue && value.BuildingId.Value.Equals(building.Id));
            var baseScore = building.Category switch
            {
                BuildingCategory.Gathering => 500,
                BuildingCategory.Processing => 420,
                BuildingCategory.SoldierCamp => 360,
                BuildingCategory.Research => 300,
                BuildingCategory.BattlefieldStructure => _construction.GetTowers().Count == 0 &&
                    _construction.GetSites().Count == 0 ? 620 : 120,
                _ => 100
            };
            if (alreadyBuilt) baseScore -= 260;
            var stableBias = cardId.Value.Aggregate(0, (value, character) => unchecked(value * 31 + character));
            return baseScore + (int)((uint)(stableBias ^ _config.Seed) % 97u);
        }

        private static int ReplacementPriority(string buildingId) => buildingId switch
        {
            "building.sawmill" => 0,
            "building.archer-camp" => 1,
            "building.winery" => 2,
            "building.research-lab" => 3,
            "building.shield-camp" => 4,
            _ => 10
        };

private static bool IsSuccessfulExecution(string result)
        {
            return !string.IsNullOrEmpty(result) &&
                   !result.Contains("failed", StringComparison.Ordinal) &&
                   !result.Contains("invalid", StringComparison.Ordinal) &&
                   !result.Contains("rolled-back", StringComparison.Ordinal) &&
                   !result.Contains("transaction", StringComparison.Ordinal);
        }


private bool TryIssueFirstProbe(int tick, AiCommitmentInterruptReason interrupt)
        {
            if (_hasIssuedFormation || tick < _config.AiStrategy.FirstProbeStartTick ||
                tick > _config.AiStrategy.FirstProbeEndTick)
                return false;

            var allowed = new HashSet<string>(ContentConstants.P1AiIntentIds, StringComparer.Ordinal);
            var candidate = BuildTrainingCandidates(allowed, tick)
                .Where(value => value.IsLegal && value.CommandPlan.PlanId == "formation.probe")
                .OrderBy(value => value.CandidateId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (string.IsNullOrEmpty(candidate.CandidateId))
                return false;

            var result = Execute(candidate);
            if (!IsSuccessfulExecution(result))
                return false;

            var phase = _config.Phases.FirstOrDefault(value => value.Id.Equals(_phases.CurrentPhaseId));
            var minimum = _config.AiStrategy.Commitments
                .FirstOrDefault(value => value.IntentId == candidate.IntentId).MinimumTicks;
            _commitment.Commit(candidate.IntentId, candidate.CandidateId, candidate.TargetId,
                candidate.CommandPlan.CompletionPolicy, tick, Math.Max(80, minimum));
            RecordPressurePattern(candidate);
            
_lastSuccessfulPressureTick = tick;
            _nextDecisionTick = tick + Math.Max(1, _config.AiStrategy.DecisionIntervalTicks);
            UpdateTempo(tick);
            _decisions.Add(new AiDecisionSnapshot(
                tick, phase?.Id.Value ?? string.Empty, candidate.IntentId, 0,
                _commitment.UntilTick, candidate.RouteId, candidate.TargetId, result,
                AiGateFailureReason.None, false, candidate.CandidateId,
                _health.PrimaryShortage, _health.EarliestAffordableTick,
                interrupt.ToString(), _tempoSignals.State, "orders-enqueued"));
            Changed?.Invoke();
            return true;
        }

        private bool TryIssueLogisticsDefense(int tick, AiCommitmentInterruptReason interrupt,
            string commitmentCompletion)
        {
            if ((_observedPerception?.RouteThreats.Count ?? 0) == 0 && _logisticsResponseUntilTick.Count == 0)
                return false;
            foreach (var stale in _logisticsResponseUntilTick.Where(value => value.Value <= tick)
                         .Select(value => value.Key).ToArray())
                _logisticsResponseUntilTick.Remove(stale);

            var threat = (_observedPerception?.RouteThreats ?? Array.Empty<AiRouteThreatSnapshot>())
                .Where(value => !_logisticsResponseUntilTick.ContainsKey(value.RouteId.Value) &&
                                _logisticsRetryAfterTick.GetValueOrDefault(value.RouteId.Value) <= tick)
                .OrderByDescending(value => value.DeathCount)
                .ThenByDescending(value => value.LostCarriedAmount)
                .ThenByDescending(value => value.ThreatStrength)
                .ThenByDescending(value => value.LastHitTick)
                .ThenBy(value => value.RouteId.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (threat == null ||
                _logisticsResponseUntilTick.Count >= _config.AiStrategy.MaxConcurrentLogisticsResponses)
                return false;

            _nextDecisionTick = Math.Min(_nextDecisionTick, tick);
            var formation = SelectLogisticsFormation(threat);
            var failure = ValidateLogisticsDefense(formation, threat.RouteId);
            var result = "blocked";
            if (failure == AiGateFailureReason.None && TryResolveDeployment(threat.RouteId, out var point))
            {
                var created = new List<int>();
                result = $"train:{formation.Id}:{threat.RouteId.Value}";
                for (var index = 0; index < formation.UnitIds.Count; index++)
                {
                    var quantity = index < formation.Quantities.Count ? Math.Max(1, formation.Quantities[index]) : 1;
                    var command = new AiTrainCommand("intent.hold", formation.UnitIds[index], quantity, point,
                        threat.RouteId, TrainingOrderPriority.EmergencyDefense, threat.DefenseTriggerId);
                    var trainingFailure = _training.TryCreateOrder(command.UnitId, command.Quantity, command.Point,
                        command.RouteId, "source.ai-logistics-defense", command.IntentId, command.Priority,
                        command.DefenseTriggerId, out var orderId);
                    if (trainingFailure == TrainingFailure.None) { created.Add(orderId); continue; }
                    foreach (var id in created.OrderByDescending(value => value)) _training.Cancel(id);
                    result = $"train:rolled-back:{trainingFailure}";
                    failure = trainingFailure == TrainingFailure.InsufficientResources
                        ? AiGateFailureReason.Resource : AiGateFailureReason.TrainingSlot;
                    break;
                }
                if (failure == AiGateFailureReason.None)
                {
                    _logisticsResponseUntilTick[threat.RouteId.Value] =
                        tick + _config.AiStrategy.LogisticsThreatMemoryTicks;
                    _commitment.Commit("intent.hold", $"candidate.logistics-defense.{threat.RouteId.Value}",
                        threat.DefenseTriggerId, "orders-enqueued", tick, 80);
                }
            }

            _logisticsRetryAfterTick[threat.RouteId.Value] = tick +
                Math.Max(1, _config.AiStrategy.DecisionIntervalTicks);
            var phase = _config.Phases.FirstOrDefault(value => value.Id.Equals(_phases.CurrentPhaseId));
            _decisions.Add(new AiDecisionSnapshot(tick, phase?.Id.Value ?? string.Empty, "intent.hold",
                threat.ThreatStrength, _commitment.UntilTick, threat.RouteId.Value, threat.DefenseTriggerId,
                result, failure, false, $"candidate.logistics-defense.{threat.RouteId.Value}",
                failure is AiGateFailureReason.Resource or AiGateFailureReason.ProtectedReserve ? "Resource/ProtectedReserve" : string.Empty,
                0, interrupt.ToString(), _tempoSignals.State, commitmentCompletion,
                AiDefenseTriggerKind.LogisticsDefense, threat.RouteId.Value));
            Changed?.Invoke();
            return true;
        }

        private MatchEnemyFormationConfig SelectLogisticsFormation(AiRouteThreatSnapshot threat)
        {
            var reserve = _config.EnemyEconomy.Formations.FirstOrDefault(value =>
                value.Id == _config.EnemyEconomy.DefenseReserveFormationId);
            var candidates = _config.EnemyEconomy.Formations
                .Where(value => value.AllowedIntentIds.Contains("intent.hold"))
                .OrderBy(value => FormationCosts(value).Sum(cost => cost.Amount))
                .ThenBy(value => value.Id, StringComparer.Ordinal).ToArray();
            if (threat.AttackerHandles.Count <= 1 && reserve != null) return reserve;
            return candidates.FirstOrDefault(value => FormationStrength(value) >= threat.ThreatStrength) ??
                   candidates.LastOrDefault() ?? reserve;
        }

        private int FormationStrength(MatchEnemyFormationConfig formation) => FormationUnits(formation).Sum(value =>
            value.unit.AttackDamage * value.quantity + value.unit.MaxHealth * value.quantity / 4);

        private AiGateFailureReason ValidateLogisticsDefense(MatchEnemyFormationConfig formation, RouteId routeId)
        {
            if (formation == null || !formation.AllowedIntentIds.Contains("intent.hold"))
                return AiGateFailureReason.PhasePermission;
            if (!TryResolveDeployment(routeId, out _)) return AiGateFailureReason.UnreachablePath;
            if (!FormationCampsActive(formation)) return AiGateFailureReason.TrainingSlot;
            var quantity = FormationUnits(formation).Sum(value => value.quantity);
            var active = _combat.GetUnits().Count(value => value.Faction == MatchFaction.Enemy);
            var queued = _training.GetSnapshot().Sum(value => value.Remaining);
            if (_combat.GetUnits().Count + quantity > 60 ||
                active + queued + quantity > _config.AiStrategy.ActiveUnitSoftCap +
                _config.AiStrategy.EmergencyDefenseOverflowUnits)
                return AiGateFailureReason.ArmyCap;
            return HasAvailable(FormationCosts(formation)) ? AiGateFailureReason.None : AiGateFailureReason.ProtectedReserve;
        }

        private AiActionCandidate[] BuildCandidates(MatchPhaseConfig phase, int tick)
        {
            var allowed = new HashSet<string>(phase?.AllowedIntentIds ?? ContentConstants.P1AiIntentIds, StringComparer.Ordinal);
            var result = new List<AiActionCandidate>();
            var providers = new IAiActionCandidateProvider[]
            {
                new RuntimeCandidateProvider("provider.training", () => BuildTrainingCandidates(allowed, tick).ToArray()),
                new RuntimeCandidateProvider("provider.development", () => BuildFacilityCandidates(allowed).ToArray()),
                new RuntimeCandidateProvider("provider.research", () => BuildResearchCandidates(allowed).ToArray()),
                new RuntimeCandidateProvider("provider.tower", () => BuildTowerCandidates(allowed).ToArray()),
                new RuntimeCandidateProvider("provider.reserve", () => new[] { BuildReserveCandidate(allowed, tick) })
            };
            foreach (var provider in providers.OrderBy(value => value.ProviderId, StringComparer.Ordinal))
                result.AddRange(provider.BuildCandidates());
            return result.ToArray();
        }

private IEnumerable<AiActionCandidate> BuildTrainingCandidates(HashSet<string> allowed, int tick)
        {
            var intents = new[] { "intent.assault", "intent.hold", "intent.raid-economy" };
            var routes = _config.BattlefieldLayout.Routes
                .OrderBy(value => value.Points.Count == 0 ? int.MaxValue : value.Points[^1].Y)
                .ThenBy(value => value.Id.Value, StringComparer.Ordinal)
                .ToArray();
            var activeUnits = _combat.GetUnits().Count(value => value.Faction == MatchFaction.Enemy);
            var queuedUnits = _training.GetSnapshot().Sum(value => value.Remaining);
            var perception = _observedPerception ?? BuildPerception(0);

            foreach (var intent in intents)
            {
                foreach (var formation in _config.EnemyEconomy.Formations
                             .OrderBy(value => value.Id, StringComparer.Ordinal))
                {
                    var formationAllowsIntent = formation.AllowedIntentIds.Count == 0 ||
                                                formation.AllowedIntentIds.Contains(intent);
                    var formationQuantity = FormationUnits(formation).Sum(value => value.quantity);
                    foreach (var route in routes)
                    {
                        var costs = FormationCosts(formation);
                        var enemyWall = _combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy);
                        var emergencyDefense = intent == "intent.hold" &&
                            enemyWall.Health * 100 <= enemyWall.MaxHealth * 30;
                        var tempoFailure = !_hasIssuedFormation && tick < _config.AiStrategy.FirstProbeStartTick
                            ? AiGateFailureReason.PacingCooldown
                            : _tempoController.GetOffensiveGateFailure(
                                _tempoConfig, tick, _lastSuccessfulPressureTick,
                                activeUnits, queuedUnits, emergencyDefense);
                        var failure = !allowed.Contains(intent) || !formationAllowsIntent
                            ? AiGateFailureReason.PhasePermission
                            : !TryResolveDeployment(route.Id, out _)
                                ? AiGateFailureReason.UnreachablePath
                                : activeUnits + queuedUnits + formationQuantity >
                                  _config.AiStrategy.ActiveUnitSoftCap ||
                                  queuedUnits + formationQuantity >
                                  _config.AiStrategy.QueuedUnitSoftCap
                                    ? AiGateFailureReason.ArmyCap
                                    : tempoFailure != AiGateFailureReason.None
                                        ? tempoFailure
                                        : intent != "intent.hold" && WouldSpendProtectedReserve(costs)
                                            ? AiGateFailureReason.ProtectedReserve
                                        : !FormationCampsActive(formation)
                                            ? AiGateFailureReason.TrainingSlot
                                            : !HasAvailable(costs)
                                                ? AiGateFailureReason.Resource
                                                : AiGateFailureReason.None;

                        var lane = ResolveRouteLane(route.Id);
                        var opposition = perception.PlayerByLane.GetValueOrDefault(lane);
                        var exposedGatherers = perception.ExposedPlayerGatherers;
                        var strength = FormationUnits(formation).Sum(value =>
                            value.unit.AttackDamage * value.quantity +
                            value.unit.MaxHealth * value.quantity / 4);
                        var commandIds = formation.UnitIds.Select((id, index) =>
                        {
                            var quantity = index < formation.Quantities.Count
                                ? formation.Quantities[index]
                                : 1;
                            return $"train:{id.Value}:{quantity}:{route.Id.Value}";
                        }).ToArray();

                        var advancedWeight = FormationUnits(formation).Any(value => IsAdvancedUnit(value.unit.Id))
                            ? _config.Heat.GetTier(tick).AdvancedUnitWeightMultiplierMilli
                            : 1000;
                        yield return new AiActionCandidate(
                            $"candidate.train.{intent}.{formation.Id}.{route.Id.Value}",
                            intent, AiCommandKind.Train, "ai.training-provider",
                            TargetFor(intent), route.Id.Value, failure,
                            costs.Select(value =>
                                new AiResourceCost(value.ResourceId, value.Amount)).ToArray(),
                            Math.Min(700, (strength / 10 +
                                (intent == "intent.raid-economy" ? exposedGatherers * 45 : 0)) * advancedWeight / 1000),
                            Math.Min(500, opposition * 90 +
                                GetPressurePatternPenalty(formation.Id, route.Id.Value)),
                            new AiCommandPlan(
                                formation.Id, commandIds, "all-orders-enqueued"));
                    }
                }
            }
        }

        private void UpdateHeatTempo(int tick)
        {
            var multiplier = _config.Heat.GetTier(tick).AiPressureIntervalMultiplierMilli;
            _tempoConfig = new AiTempoConfig(
                Math.Max(400, _config.AiStrategy.PressureMinIntervalTicks * multiplier / 1000),
                Math.Max(400, _config.AiStrategy.PressureTargetIntervalTicks * multiplier / 1000),
                Math.Max(400, _config.AiStrategy.PressureMaxIntervalTicks * multiplier / 1000),
                _config.AiStrategy.ActiveUnitSoftCap,
                _config.AiStrategy.QueuedUnitSoftCap);
        }

        private static bool IsAdvancedUnit(UnitId unitId) => unitId.Value is not
            (ContentConstants.ShieldGuardUnitId or ContentConstants.ArcherUnitId);

private int GetPressurePatternPenalty(string formationId, string routeId)
        {
            var formationRepeats = string.Equals(
                    formationId, _lastPressureFormationId, StringComparison.Ordinal)
                ? Math.Min(3, _consecutivePressureFormationCount)
                : 0;
            var routeRepeats = string.Equals(
                    routeId, _lastPressureRouteId, StringComparison.Ordinal)
                ? Math.Min(3, _consecutivePressureRouteCount)
                : 0;
            return (formationRepeats + routeRepeats) *
                   Math.Max(0, _config.AiStrategy.RepetitionPenalty);
        }

        private void RecordPressurePattern(AiActionCandidate candidate)
        {
            var formationId = candidate.CommandPlan.PlanId ?? string.Empty;
            if (string.Equals(formationId, _lastPressureFormationId, StringComparison.Ordinal))
                _consecutivePressureFormationCount = Math.Min(3, _consecutivePressureFormationCount + 1);
            else
            {
                _lastPressureFormationId = formationId;
                _consecutivePressureFormationCount = 1;
            }

            if (string.Equals(candidate.RouteId, _lastPressureRouteId, StringComparison.Ordinal))
                _consecutivePressureRouteCount = Math.Min(3, _consecutivePressureRouteCount + 1);
            else
            {
                _lastPressureRouteId = candidate.RouteId;
                _consecutivePressureRouteCount = 1;
            }
        }


        private IEnumerable<AiActionCandidate> BuildFacilityCandidates(HashSet<string> allowed)
        {
            var empty = _buildings.GetSnapshot().FirstOrDefault(value => !value.BuildingId.HasValue);
            foreach (var building in _config.Buildings
                         .Where(value => value.Category != BuildingCategory.BattlefieldStructure)
                         .OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                var failure = !allowed.Contains("intent.develop") ? AiGateFailureReason.PhasePermission : empty == null ? AiGateFailureReason.TrainingSlot :
                    !_cards.Contains(building.SourceCardId) ? AiGateFailureReason.Card : AiGateFailureReason.None;
                yield return new AiActionCandidate($"candidate.facility.{building.Id.Value}", "intent.develop", AiCommandKind.Facility,
                    "ai.development-provider", building.Id.Value, string.Empty, failure, Array.Empty<AiResourceCost>(), 120, 20,
                    new AiCommandPlan(building.Id.Value, new[] { $"build:{building.Id.Value}" }, "facility-created"));
            }
        }

        private IEnumerable<AiActionCandidate> BuildResearchCandidates(HashSet<string> allowed)
        {
            foreach (var upgrade in _research.GetCandidates().OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                var snapshot = _research.GetSnapshot();
                var failure = !allowed.Contains("intent.research") ? AiGateFailureReason.PhasePermission :
                    snapshot.Active || !snapshot.LabAvailable ? AiGateFailureReason.ResearchSlot :
                    !HasAvailable(_config.Research.Costs) ? AiGateFailureReason.Resource :
                    WouldSpendProtectedReserve(_config.Research.Costs) ? AiGateFailureReason.ProtectedReserve : AiGateFailureReason.None;
                yield return new AiActionCandidate($"candidate.research.{upgrade.Id.Value}", "intent.research",
                    AiCommandKind.Research, "ai.research-provider", upgrade.Id.Value, string.Empty, failure,
                    _config.Research.Costs.Select(value => new AiResourceCost(value.ResourceId, value.Amount)).ToArray(),
                    180, 30, new AiCommandPlan(upgrade.Id.Value, new[] { $"research:{upgrade.Id.Value}" }, "research-enqueued"));
            }
        }

        private IEnumerable<AiActionCandidate> BuildTowerCandidates(HashSet<string> allowed)
        {
            var zone = _config.BattlefieldLayout.Zones.FirstOrDefault(value => value.Kind == ZoneKind.TowerBuildable);
            for (var index = 0; index < 3; index++)
            {
                var x = zone.X + (index + 2) * zone.Width / 5;
                var y = zone.Y + (index + 1) * zone.Height / 4;
                var constructionFailure = _construction.ValidateStartSite(x, y);
                var failure = !allowed.Contains("intent.build-tower") ? AiGateFailureReason.PhasePermission :
                    constructionFailure switch
                    {
                        TowerConstructionFailure.None => AiGateFailureReason.None,
                        TowerConstructionFailure.CardMissing => AiGateFailureReason.Card,
                        TowerConstructionFailure.SiteLimitReached or TowerConstructionFailure.TowerLimitReached => AiGateFailureReason.TowerLimit,
                        TowerConstructionFailure.InvalidPosition => AiGateFailureReason.ForbiddenZone,
                        TowerConstructionFailure.PathBlocked => AiGateFailureReason.UnreachablePath,
                        _ => AiGateFailureReason.Resource
                    };
                if (failure == AiGateFailureReason.None && WouldSpendProtectedReserve(_config.Construction.Costs))
                    failure = AiGateFailureReason.ProtectedReserve;
                yield return new AiActionCandidate($"candidate.tower.{index}", "intent.build-tower",
                    AiCommandKind.BuildTower, "ai.tower-provider", $"tower-slot.{index}", string.Empty, failure,
                    _config.Construction.Costs.Select(value => new AiResourceCost(value.ResourceId, value.Amount)).ToArray(),
                    220, 60, new AiCommandPlan($"{x},{y}", new[] { $"tower:{x}:{y}" }, "construction-site-created"));
            }

            foreach (var threat in (_observedPerception?.RouteThreats ?? Array.Empty<AiRouteThreatSnapshot>())
                         .Where(value => value.DeathCount >= _config.AiStrategy.TowerEscalationKillCount)
                         .OrderByDescending(value => value.DeathCount).ThenBy(value => value.RouteId.Value, StringComparer.Ordinal))
            {
                var route = _config.BattlefieldLayout.Routes.FirstOrDefault(value => value.Id.Equals(threat.RouteId));
                if (route == null || route.Points.Count == 0) continue;
                var x = zone.X + zone.Width * 4 / 5;
                var y = Math.Clamp(route.Points[^1].Y, zone.Y, zone.Y + zone.Height);
                var constructionFailure = _construction.ValidateStartSite(x, y);
                var failure = !allowed.Contains("intent.build-tower") ? AiGateFailureReason.PhasePermission :
                    constructionFailure switch
                    {
                        TowerConstructionFailure.None => AiGateFailureReason.None,
                        TowerConstructionFailure.CardMissing => AiGateFailureReason.Card,
                        TowerConstructionFailure.SiteLimitReached or TowerConstructionFailure.TowerLimitReached => AiGateFailureReason.TowerLimit,
                        TowerConstructionFailure.InvalidPosition => AiGateFailureReason.ForbiddenZone,
                        TowerConstructionFailure.PathBlocked => AiGateFailureReason.UnreachablePath,
                        _ => AiGateFailureReason.Resource
                    };
                if (failure == AiGateFailureReason.None && WouldSpendProtectedReserve(_config.Construction.Costs))
                    failure = AiGateFailureReason.ProtectedReserve;
                yield return new AiActionCandidate($"candidate.tower.logistics.{threat.RouteId.Value}",
                    "intent.build-tower", AiCommandKind.BuildTower, "ai.tower-provider",
                    $"target.logistics-route.{threat.RouteId.Value}", threat.RouteId.Value, failure,
                    _config.Construction.Costs.Select(value => new AiResourceCost(value.ResourceId, value.Amount)).ToArray(),
                    420 + threat.DeathCount * 80, 20,
                    new AiCommandPlan($"{x},{y}", new[] { $"tower:{x}:{y}" }, "construction-site-created"));
            }
        }

        private AiActionCandidate BuildReserveCandidate(HashSet<string> allowed, int tick)
        {
            var formation = SelectReserveFormation();
            var forecasts = ForecastFormation(formation, tick);
            var missing = forecasts.Where(value => value.CurrentInventory - value.ReservedSpending < FormationCosts(formation)
                .Where(cost => cost.ResourceId.Equals(value.ResourceId)).Sum(cost => cost.Amount))
                .OrderBy(value => value.EarliestAffordableTick < 0 ? int.MaxValue : value.EarliestAffordableTick).FirstOrDefault();
            var unreachable = forecasts.Any(value => value.EarliestAffordableTick < 0 && FormationCosts(formation).Any(cost => cost.ResourceId.Equals(value.ResourceId) && cost.Amount > value.CurrentInventory - value.ReservedSpending));
            _health = new(!unreachable, unreachable ? "ai.reserve.permanently-unreachable" : string.Empty, formation?.Id,
                missing.ResourceId.Value ?? string.Empty, missing.EarliestAffordableTick);
            return new AiActionCandidate($"candidate.reserve.{formation?.Id}", "intent.reserve", AiCommandKind.None,
                "ai.reserve-provider", formation?.Id ?? string.Empty, string.Empty,
                !allowed.Contains("intent.reserve") ? AiGateFailureReason.PhasePermission : AiGateFailureReason.None,
                FormationCosts(formation).Select(value => new AiResourceCost(value.ResourceId, value.Amount)).ToArray(), unreachable ? -200 : 80, 0,
                new AiCommandPlan(formation?.Id ?? string.Empty, Array.Empty<string>(), "budget-reached"));
        }

        private IReadOnlyList<AiFeatureValue> BuildFeatures(int tick)
        {
            var inventory = _economy.GetSnapshot();
            var perception = _observedPerception ?? BuildPerception(0);
            return new[]
            {
                new AiFeatureValue("feature.resource-pressure", Math.Clamp(1000 - inventory.Sum(value => value.Available), 0, 1000)),
                new AiFeatureValue("feature.enemy-wall-danger", perception.EnemyWallPressure),
                new AiFeatureValue("feature.player-wall-damage", perception.PlayerWallPressure),
                new AiFeatureValue("feature.reserve", Math.Clamp(inventory.Sum(value => value.Available), 0, 1000)),
                new AiFeatureValue("feature.boss-event", perception.BossProximity),
                new AiFeatureValue("feature.research-open", !_research.GetSnapshot().Active ? 1000 : 0),
                new AiFeatureValue("feature.tower-gap", Math.Max(0, 1000 - _construction.GetTowers().Count * 300)),
                new AiFeatureValue("feature.pressure-due", _tempoSignals.PressureDueMilli),
                new AiFeatureValue("feature.recovery-needed", _tempoSignals.RecoveryNeededMilli),
                new AiFeatureValue("feature.overextension", _tempoSignals.OverextensionMilli)
            };
        }

        private string Execute(AiActionCandidate candidate)
        {
            if (candidate.CommandKind == AiCommandKind.None) return $"reserve:{candidate.TargetId}";
            if (candidate.CommandKind == AiCommandKind.Train)
            {
                var formation = _config.EnemyEconomy.Formations.FirstOrDefault(value => value.Id == candidate.CommandPlan.PlanId);
                var routeId = new RouteId(candidate.RouteId);
                if (formation == null || !TryResolveDeployment(routeId, out var point)) return "train:route-or-formation-invalid";
                var costs = FormationCosts(formation);
                if (!FormationCampsActive(formation) || !HasAvailable(costs)) return "train:preflight-failed";
                var created = new List<int>();
                for (var index = 0; index < formation.UnitIds.Count; index++)
                {
                    var quantity = index < formation.Quantities.Count ? Math.Max(1, formation.Quantities[index]) : 1;
                    var command = new AiTrainCommand(candidate.IntentId, formation.UnitIds[index], quantity, point, routeId);
                    var failure = _training.TryCreateOrder(command.UnitId, command.Quantity, command.Point, command.RouteId,
                        "source.ai-training", command.IntentId, out var orderId);
                    if (failure == TrainingFailure.None) { created.Add(orderId); continue; }
                    foreach (var createdOrderId in created.OrderByDescending(value => value)) _training.Cancel(createdOrderId);
                    return $"train:rolled-back:{failure}";
                }
                _hasIssuedFormation = true; _orderIndex++;
                return $"train:{formation.Id}:{routeId.Value}:orders={created.Count}";
            }
            if (candidate.CommandKind == AiCommandKind.Research)
            {
                var upgradeId = new ResearchUpgradeId(candidate.CommandPlan.PlanId);
                if (_research.TryStart(upgradeId) == ResearchFailure.None) return $"research:{upgradeId.Value}";
                return "research:transaction-failed";
            }
            if (candidate.CommandKind == AiCommandKind.BuildTower)
            {
                var parts = candidate.CommandPlan.PlanId.Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out var x) && int.TryParse(parts[1], out var y) &&
                    _construction.TryStartSite(x, y, out var id) == TowerConstructionFailure.None) return $"tower:{id}";
                return "tower:path-or-transaction-failed";
            }
            var empty = _buildings.GetSnapshot().FirstOrDefault(value => !value.BuildingId.HasValue);
            var building = _config.Buildings.FirstOrDefault(value => value.Id.Value == candidate.CommandPlan.PlanId);
            if (empty != null && building != null)
            {
                var command = new AiFacilityCommand(candidate.IntentId, empty.SlotIndex, building.Id, building.SourceCardId);
                if (_cards.Contains(command.CardId) && _buildings.TryBuild(command.SlotIndex, command.BuildingId, out var instanceId))
                {
                    if (!_cards.TryConsume(command.CardId)) { _buildings.Demolish(instanceId); return "facility:rolled-back-card"; }
                    return $"facility:{instanceId}:{building.Id.Value}";
                }
            }
            return "facility:transaction-failed";
        }

        private void RecordBlocked(int tick, MatchPhaseConfig phase, IReadOnlyList<AiActionCandidate> candidates)
        {
            var blocked = candidates.OrderBy(value => value.CandidateId, StringComparer.Ordinal).First();
            _decisions.Add(new AiDecisionSnapshot(tick, phase?.Id.Value ?? string.Empty, blocked.IntentId, 0,
                _commitment.UntilTick, blocked.RouteId, blocked.TargetId, "blocked", blocked.FailureReason, false));
            Changed?.Invoke();
        }

        private AiCommitmentInterruptReason ResolveInterrupt(int tick)
        {
            var routeThreats = _observedPerception?.RouteThreats;
            if (routeThreats != null)
                for (var index = 0; index < routeThreats.Count; index++)
                    if (!_logisticsResponseUntilTick.ContainsKey(routeThreats[index].RouteId.Value) &&
                        _logisticsRetryAfterTick.GetValueOrDefault(routeThreats[index].RouteId.Value) <= tick)
                        return AiCommitmentInterruptReason.LogisticsThreat;
            var wall = _combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy);
            if (wall.Health * 100 <= wall.MaxHealth * 30) return AiCommitmentInterruptReason.WallEmergency;
            if (_config.BattlefieldLayout.BossSpawns.Any(value => value.WarningTick == tick || value.SpawnTick == tick)) return AiCommitmentInterruptReason.BossEvent;
            return AiCommitmentInterruptReason.None;
        }

        private void ObservePlayerUnits(int tick)
        {
            foreach (var unit in _combat.GetUnits().Where(value => value.Faction == MatchFaction.Player))
                if (!_playerUnitFirstSeen.ContainsKey(unit.Id)) _playerUnitFirstSeen.Add(unit.Id, tick);
            foreach (var stale in _playerUnitFirstSeen.Keys.Where(id => _combat.GetUnits().All(value => value.Id != id)).ToArray())
                _playerUnitFirstSeen.Remove(stale);
        }

        private int ResolveObservedPlayerLane(int tick)
        {
            var eligible = _combat.GetUnits().Where(value => value.Faction == MatchFaction.Player &&
                _playerUnitFirstSeen.TryGetValue(value.Id, out var seen) &&
                tick - seen >= _config.AiStrategy.ReactionDelayTicks).ToArray();
            if (eligible.Length == 0) return 1;
            return eligible.GroupBy(value => value.Lane).OrderByDescending(value => value.Count())
                .ThenBy(value => value.Key).First().Key;
        }

        private AiPerceptionSnapshot BuildPerception(int tick)
        {
            var units = _combat.GetUnits();
            var playerByLane = Enumerable.Range(0, 3).ToDictionary(lane => lane,
                lane => units.Count(value => value.Faction == MatchFaction.Player && value.Lane == lane));
            var enemyByLane = Enumerable.Range(0, 3).ToDictionary(lane => lane,
                lane => units.Count(value => value.Faction == MatchFaction.Enemy && value.Lane == lane));
            var enemyWall = _combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy);
            var playerWall = _combat.GetWalls().Single(value => value.Faction == MatchFaction.Player);
            var exposedGatherers = _playerGatherers?.GetSnapshot().Count(value => value.CarriedAmount > 0 || value.State == GathererState.Returning) ?? 0;
            var memoryStart = tick - _config.AiStrategy.LogisticsThreatMemoryTicks;
            var allIncidents = _combat.GetGathererThreatIncidents();
            var incidents = allIncidents.Count == 0 ? Array.Empty<GathererThreatIncident>() : allIncidents
                .Where(value => value.Tick >= memoryStart && value.Tick <= tick).ToArray();
            if (incidents.Length == 0)
                return CreatePerceptionSnapshot(tick, playerByLane, enemyByLane, exposedGatherers,
                    enemyWall, playerWall, Array.Empty<AiRouteThreatSnapshot>());
            var activePlayerUnits = units.Where(value => value.Faction == MatchFaction.Player).ToDictionary(value => value.Id);
            var routeThreats = incidents.GroupBy(value => value.RouteId)
                .Select(group =>
                {
                    var attackers = group.Select(value => value.AttackerHandle).Distinct().Where(activePlayerUnits.ContainsKey)
                        .OrderBy(value => value).ToArray();
                    var strength = attackers.Sum(handle =>
                    {
                        var unit = activePlayerUnits[handle];
                        var definition = _config.Units.FirstOrDefault(value => value.Id.Equals(unit.UnitId));
                        return definition == null ? 0 : definition.AttackDamage + definition.MaxHealth / 4;
                    });
                    var last = group.Max(value => value.Tick);
                    return new AiRouteThreatSnapshot(group.Key, attackers, last, group.Count(),
                        group.Count(value => value.WasKilled), group.Sum(value => value.LostCarriedAmount),
                        strength, $"logistics:{group.Key.Value}:{last}");
                })
                .Where(value => value.AttackerHandles.Count > 0)
                .OrderByDescending(value => value.DeathCount)
                .ThenByDescending(value => value.LostCarriedAmount)
                .ThenByDescending(value => value.ThreatStrength)
                .ThenByDescending(value => value.LastHitTick)
                .ThenBy(value => value.RouteId.Value, StringComparer.Ordinal).ToArray();
            return CreatePerceptionSnapshot(tick, playerByLane, enemyByLane, exposedGatherers, enemyWall, playerWall, routeThreats);
        }

        private AiPerceptionSnapshot CreatePerceptionSnapshot(int tick, IReadOnlyDictionary<int, int> playerByLane,
            IReadOnlyDictionary<int, int> enemyByLane, int exposedGatherers, WallSnapshot enemyWall,
            WallSnapshot playerWall, IReadOnlyList<AiRouteThreatSnapshot> routeThreats) =>
            new(tick, playerByLane, enemyByLane, exposedGatherers, _construction.GetTowers().Count,
                _config.BattlefieldLayout.BossSpawns.Any(value => Math.Abs(value.SpawnTick - tick) <= 200) ? 1000 : 0,
                1000 - enemyWall.Health * 1000 / Math.Max(1, enemyWall.MaxHealth),
                1000 - playerWall.Health * 1000 / Math.Max(1, playerWall.MaxHealth),
                _training.GetSnapshot().Sum(value => value.Remaining), _research.GetSnapshot().Active ? 1 : 0,
                _construction.GetSites().Count, _economy.GetSnapshot(), routeThreats);

        private string ResolveRouteId(int lane)
        {
            var routes = _config.BattlefieldLayout.Routes.OrderBy(value => value.Id.Value, StringComparer.Ordinal).ToArray();
            return routes.Length == 0 ? $"route.lane-{lane}" : routes[Math.Abs(lane) % routes.Length].Id.Value;
        }

        public bool TryResolveDeployment(RouteId routeId, out DeploymentPoint point)
        {
            point = default;
            var route = _config.BattlefieldLayout.Routes.FirstOrDefault(value => value.Id.Equals(routeId));
            var zone = _config.BattlefieldLayout.Zones.FirstOrDefault(value => value.Kind == ZoneKind.EnemyDeployment);
            if (route == null || route.Points.Count < 2 || zone.Width <= 0 || zone.Height <= 0) return false;
            var lane = ResolveRouteLane(routeId);
            if (lane < 0 || lane > 2) return false;
            var routeY = route.Points[^1].Y;
            var x = zone.X + zone.Width / 2;
            var y = Math.Clamp(routeY, zone.Y, zone.Y + zone.Height);
            point = DeploymentPoint.World(x, y, lane);
            return point.IsValid && point.HasWorldPosition && x >= zone.X && x <= zone.X + zone.Width &&
                y >= zone.Y && y <= zone.Y + zone.Height;
        }

        private int ResolveRouteLane(RouteId routeId)
        {
            var routes = _config.BattlefieldLayout.Routes.OrderBy(value => value.Points.Count == 0 ? int.MaxValue : value.Points[^1].Y)
                .ThenBy(value => value.Id.Value, StringComparer.Ordinal).ToArray();
            return Array.FindIndex(routes, value => value.Id.Equals(routeId));
        }

        private MatchEnemyFormationConfig SelectReserveFormation() => _config.EnemyEconomy.Formations
            .OrderBy(value => value.Id == "formation.probe" ? 0 : 1)
            .ThenBy(value => FormationCosts(value).Sum(cost => cost.Amount)).ThenBy(value => value.Id, StringComparer.Ordinal).FirstOrDefault();

        private IEnumerable<(MatchUnitConfig unit, int quantity)> FormationUnits(MatchEnemyFormationConfig formation)
        {
            if (formation == null) yield break;
            for (var index = 0; index < formation.UnitIds.Count; index++)
            {
                var unit = _config.Units.FirstOrDefault(value => value.Id.Equals(formation.UnitIds[index]));
                if (unit != null) yield return (unit, index < formation.Quantities.Count ? Math.Max(1, formation.Quantities[index]) : 1);
            }
        }

        private ResourceAmount[] FormationCosts(MatchEnemyFormationConfig formation) => FormationUnits(formation)
            .SelectMany(value => value.unit.TrainingCosts.Select(cost => new ResourceAmount(cost.ResourceId, checked(cost.Amount * value.quantity))))
            .GroupBy(value => value.ResourceId).Select(group => new ResourceAmount(group.Key, group.Sum(value => value.Amount)))
            .OrderBy(value => value.ResourceId.Value, StringComparer.Ordinal).ToArray();

        private bool FormationCampsActive(MatchEnemyFormationConfig formation) => FormationUnits(formation)
            .All(value => _config.Units.Where(unit => unit.Id.Equals(value.unit.Id)).All(unit =>
                _buildings.GetSnapshot().Where(slot => slot.BuildingId.HasValue).Select(slot => _buildings.GetConfig(slot.InstanceId))
                    .Any(building => building != null && building.ActivatedSoldierCardId.HasValue &&
                        building.ActivatedSoldierCardId.Value.Equals(unit.SoldierCardId))));

        public IReadOnlyList<AiEconomyForecast> ForecastFormation(MatchEnemyFormationConfig formation, int tick)
        {
            var costs = FormationCosts(formation).ToDictionary(value => value.ResourceId, value => value.Amount);
            var balances = _economy.GetSnapshot().ToDictionary(value => value.Id);
            var facilities = _buildings.GetSnapshot().Where(value => value.BuildingId.HasValue)
                .Select(value => _buildings.GetConfig(value.InstanceId)).Where(value => value != null).ToArray();
            var flows = new List<AiResourceFlow>();
            foreach (var resource in _config.Resources.OrderBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                var gatherMilli = _config.BattlefieldLayout.Gatherers
                    .Where(value => value.AllowedResourceIds.Contains(resource.Id))
                    .Sum(value => (long)value.CarryAmount * _config.EnemyEconomy.EconomicEfficiencyMilli * 1000 /
                        Math.Max(1, _config.BattlefieldLayout.GathererDispatchIntervalTicks) /
                        Math.Max(1, value.AllowedResourceIds.Count));
                gatherMilli += facilities.Where(value => value.Category == BuildingCategory.Gathering &&
                        value.GathererAllowedResourceIds.Contains(resource.Id))
                    .Sum(value => (long)value.GathererCarryAmount * _config.EnemyEconomy.EconomicEfficiencyMilli * 1000 /
                        Math.Max(1, value.GathererDispatchIntervalTicks));
                var consumedMilli = facilities.Sum(value => value.Category == BuildingCategory.Gathering
                    ? (long)value.GathererDispatchCosts.Where(input => input.ResourceId.Equals(resource.Id)).Sum(input => input.Amount) * 1000 /
                      Math.Max(1, value.GathererDispatchIntervalTicks)
                    : (long)value.Inputs.Where(input => input.ResourceId.Equals(resource.Id)).Sum(input => input.Amount) * 1000 /
                      Math.Max(1, value.ProductionCycleTicks));
                var outputMilli = facilities.Where(value => value.Category == BuildingCategory.Processing)
                    .Sum(value => (long)value.Outputs.Where(output => output.ResourceId.Equals(resource.Id)).Sum(output => output.Amount) * 1000 /
                        Math.Max(1, value.ProductionCycleTicks));
                var balance = balances[resource.Id];
                flows.Add(new AiResourceFlow(resource.Id, balance.Amount, balance.Reserved, (int)Math.Min(int.MaxValue, gatherMilli),
                    (int)Math.Min(int.MaxValue, consumedMilli), (int)Math.Min(int.MaxValue, outputMilli), costs.GetValueOrDefault(resource.Id)));
            }
            return new AiEconomyForecaster().Forecast(flows, tick);
        }

        private void ResumeAffordableBuildings()
        {
            foreach (var slot in _buildings.GetSnapshot().Where(value => value.BuildingId.HasValue && value.Paused &&
                         value.BlockReason is ProductionBlockReason.MissingInput or ProductionBlockReason.ReserveProtected))
            {
                var building = _buildings.GetConfig(slot.InstanceId);
                if (building == null) continue;
                var costs = building.Category == BuildingCategory.Gathering
                    ? building.GathererDispatchCosts
                    : building.Inputs;
                if (!HasAvailable(costs)) continue;
                var preservesReserve = building.InputReserveFloors.All(floor =>
                {
                    var debit = costs.Where(value => value.ResourceId.Equals(floor.ResourceId)).Sum(value => value.Amount);
                    return _economy.GetAvailable(floor.ResourceId) - debit >= floor.Amount;
                });
                if (preservesReserve) _buildings.TryResumeAfterResourceShortage(slot.InstanceId);
            }
        }

        private string TargetFor(string intent) => intent switch
        {
            "intent.assault" => _config.Combat.PlayerWall.Id,
            "intent.raid-economy" => "target.player-gather-line",
            "intent.hold" => "target.player-main-route",
            "intent.build-tower" => "target.enemy-tower-zone",
            "intent.research" => "target.enemy-research",
            "intent.develop" => "target.enemy-facility-slot",
            _ => "target.reserve-budget"
        };

        private bool WouldSpendProtectedReserve(IEnumerable<ResourceAmount> costs)
        {
            var wall = _combat.GetWalls().Single(value => value.Faction == MatchFaction.Enemy);
            if (wall.Health * 100 <= wall.MaxHealth * 30)
                return false;
            var formation = _config.EnemyEconomy.Formations.FirstOrDefault(value =>
                value.Id == _config.EnemyEconomy.DefenseReserveFormationId) ?? SelectReserveFormation();
            if (formation == null)
                return false;
            var protectedByResource = FormationCosts(formation).ToDictionary(
                value => value.ResourceId,
                value => Math.Max(value.Amount,
                    (int)Math.Ceiling(value.Amount * _config.EnemyEconomy.ReserveRatioMilli / 1000d)));
            foreach (var cost in costs)
                if (protectedByResource.TryGetValue(cost.ResourceId, out var protectedAmount) &&
                    _economy.GetAvailable(cost.ResourceId) - cost.Amount < protectedAmount)
                    return true;
            return false;
        }

        private bool HasAvailable(IEnumerable<ResourceAmount> costs) => costs.All(value => _economy.GetAvailable(value.ResourceId) >= value.Amount);
        private bool CanTrainAny() => _config.Units.Where(value => value.CanAttack)
            .Any(value => value.TrainingCosts.All(cost => _economy.GetAvailable(cost.ResourceId) >= cost.Amount));
    }
}
