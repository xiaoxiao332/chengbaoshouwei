using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FortressFrontier.Core.AI;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Progression;

namespace FortressFrontier.Runtime.Gameplay
{
    public enum MatchFailureCause
    {
        None,
        GatheringInterrupted,
        ProcessingBottleneck,
        DeploymentGap,
        UnitCompositionCountered,
        ResearchAllocationError,
        TowerOverinvestment
    }

    public sealed class MatchAnalysisSnapshot
    {
        public MatchAnalysisSnapshot(int durationTicks, WallSnapshot playerWall, WallSnapshot enemyWall,
            IReadOnlyList<UnitCombatCountSnapshot> combatCounts, IReadOnlyList<WallDamageSourceSnapshot> wallDamageSources,
            ResourceId primaryResourceBreakpoint, int resourceBreakpointTicks, int processingBottleneckTicks,
            int maximumDeploymentGapTicks, int playerBossClaims, int enemyBossClaims,
            int playerTowerCount, int unusedHandCount, int pendingResearchCount,
            int enemyLedgerEntryCount, MatchFailureCause failureCause,
            int firstResourceDepositTick = -1, int firstEnemyPressureTick = -1,
            int averageEnemyPressureIntervalTicks = 0,
            IReadOnlyDictionary<string, int> enemyIntentPermille = null,
            int protectedReserveDelayCount = 0, int completedPressureCycles = 0,
            int maximumConsecutiveEnemyIntentCount = 0, int longestEnemyPressureGapTicks = 0,
            int averageRecoveryDurationTicks = 0)
        {
            DurationTicks = durationTicks;
            PlayerWall = playerWall;
            EnemyWall = enemyWall;
            CombatCounts = combatCounts ?? Array.Empty<UnitCombatCountSnapshot>();
            WallDamageSources = wallDamageSources ?? Array.Empty<WallDamageSourceSnapshot>();
            PrimaryResourceBreakpoint = primaryResourceBreakpoint;
            ResourceBreakpointTicks = resourceBreakpointTicks;
            ProcessingBottleneckTicks = processingBottleneckTicks;
            MaximumDeploymentGapTicks = maximumDeploymentGapTicks;
            PlayerBossClaims = playerBossClaims;
            EnemyBossClaims = enemyBossClaims;
            PlayerTowerCount = playerTowerCount;
            UnusedHandCount = unusedHandCount;
            PendingResearchCount = pendingResearchCount;
            EnemyLedgerEntryCount = enemyLedgerEntryCount;
            FailureCause = failureCause;
            FirstResourceDepositTick = firstResourceDepositTick;
            FirstEnemyPressureTick = firstEnemyPressureTick;
            AverageEnemyPressureIntervalTicks = averageEnemyPressureIntervalTicks;
            EnemyIntentPermille = enemyIntentPermille == null
                ? new Dictionary<string, int>()
                : new Dictionary<string, int>(enemyIntentPermille, StringComparer.Ordinal);
            
            CompletedPressureCycles = completedPressureCycles;
            MaximumConsecutiveEnemyIntentCount = maximumConsecutiveEnemyIntentCount;
            LongestEnemyPressureGapTicks = longestEnemyPressureGapTicks;
            AverageRecoveryDurationTicks = averageRecoveryDurationTicks;
ProtectedReserveDelayCount = protectedReserveDelayCount;
        }

        public int DurationTicks { get; }
        public WallSnapshot PlayerWall { get; }
        public WallSnapshot EnemyWall { get; }
        public IReadOnlyList<UnitCombatCountSnapshot> CombatCounts { get; }
        public IReadOnlyList<WallDamageSourceSnapshot> WallDamageSources { get; }
        public ResourceId PrimaryResourceBreakpoint { get; }
        public int ResourceBreakpointTicks { get; }
        public int ProcessingBottleneckTicks { get; }
        public int MaximumDeploymentGapTicks { get; }
        public int PlayerBossClaims { get; }
        public int EnemyBossClaims { get; }
        public int PlayerTowerCount { get; }
        public int UnusedHandCount { get; }
        public int PendingResearchCount { get; }
        public int EnemyLedgerEntryCount { get; }
        public MatchFailureCause FailureCause { get; }
        public int FirstResourceDepositTick { get; }
        public int FirstEnemyPressureTick { get; }
        public int AverageEnemyPressureIntervalTicks { get; }
        public IReadOnlyDictionary<string, int> EnemyIntentPermille { get; }
        
        public int CompletedPressureCycles { get; }
        public int MaximumConsecutiveEnemyIntentCount { get; }
        public int LongestEnemyPressureGapTicks { get; }
        public int AverageRecoveryDurationTicks { get; }
public int ProtectedReserveDelayCount { get; }
    }

    /// <summary>Read-only match telemetry. It samples authoritative systems and never mutates gameplay state.</summary>
    public sealed class MatchAnalyticsSystem : GameSystemBase, IFixedMatchSimulation
    {
        private static readonly string[] RawResourceIds =
        {
            ContentConstants.FoodResourceId, ContentConstants.WoodResourceId,
            ContentConstants.RawStoneResourceId, ContentConstants.IronOreResourceId
        };

        private readonly EconomySystem _economy;
        private readonly BuildingSystem _buildings;
        private readonly CombatSystem _combat;
        private readonly HandAndOfferSystem _hand;
        private readonly PlayerResearchSystem _research;
        private readonly PlayerTowerConstructionSystem _construction;
        private readonly BossSystem _boss;
        
        private readonly AiStrategySystem _aiStrategy;
private readonly EnemyEconomySystem _enemyEconomy;
        private readonly Dictionary<ResourceId, int> _zeroAvailabilityTicks = new();
        private readonly Dictionary<ProductionBlockReason, int> _buildingBlockTicks = new();
        private int _currentTick;
        private int _lastPlayerDeploymentTick;
        private int _lastPlayerSpawned;
        
        
        private AiTempoState _lastTempoState = AiTempoState.Rallying;
        private int _recoveryStartTick = -1;
        private int _completedPressureCycles;
        private int _totalRecoveryDurationTicks;
        private int _lastEnemyPressureTick = -1;
        private int _longestEnemyPressureGapTicks;
        private int _lastAiDecisionCount;
        private string _lastSuccessfulEnemyIntent = string.Empty;
        private int _currentConsecutiveEnemyIntentCount;
        private int _maximumConsecutiveEnemyIntentCount;
private int _firstResourceDepositTick = -1;
private int _maximumDeploymentGapTicks;

        public MatchAnalyticsSystem(EconomySystem economy, BuildingSystem buildings, CombatSystem combat,
            HandAndOfferSystem hand, PlayerResearchSystem research, PlayerTowerConstructionSystem construction,
            BossSystem boss, EnemyEconomySystem enemyEconomy, AiStrategySystem aiStrategy = null)
            : base(SystemLifetime.Scene)
        {
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _buildings = buildings ?? throw new ArgumentNullException(nameof(buildings));
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
            _hand = hand ?? throw new ArgumentNullException(nameof(hand));
            _research = research ?? throw new ArgumentNullException(nameof(research));
            _construction = construction ?? throw new ArgumentNullException(nameof(construction));
            _boss = boss ?? throw new ArgumentNullException(nameof(boss));
            _enemyEconomy = enemyEconomy ?? throw new ArgumentNullException(nameof(enemyEconomy));
            _aiStrategy = aiStrategy;
            foreach (var id in RawResourceIds)
                _zeroAvailabilityTicks[new ResourceId(id)] = 0;
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) => Task.CompletedTask;

protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            foreach (var id in _zeroAvailabilityTicks.Keys.ToArray())
                _zeroAvailabilityTicks[id] = 0;
            _buildingBlockTicks.Clear();
            _currentTick = 0;
            _lastPlayerDeploymentTick = 0;
            _lastPlayerSpawned = 0;
            _maximumDeploymentGapTicks = 0;
            
            _lastTempoState = AiTempoState.Rallying;
            _recoveryStartTick = -1;
            _completedPressureCycles = 0;
            _totalRecoveryDurationTicks = 0;
            _lastEnemyPressureTick = -1;
            _longestEnemyPressureGapTicks = 0;
            _lastAiDecisionCount = 0;
            _lastSuccessfulEnemyIntent = string.Empty;
            _currentConsecutiveEnemyIntentCount = 0;
            _maximumConsecutiveEnemyIntentCount = 0;
_firstResourceDepositTick = -1;
            return Task.CompletedTask;
        }

public void SimulateTick(int tick)
        {
            _currentTick = tick;
            var balances = _economy.GetSnapshot().ToDictionary(value => value.Id);
            foreach (var id in _zeroAvailabilityTicks.Keys.ToArray())
                if (!balances.TryGetValue(id, out var balance) || balance.Available <= 0)
                    _zeroAvailabilityTicks[id]++;

            if (_firstResourceDepositTick < 0 && balances.Any(value =>
                    RawResourceIds.Contains(value.Key.Value, StringComparer.Ordinal) &&
                    value.Value.Amount > 0))
                _firstResourceDepositTick = tick;

            foreach (var slot in _buildings.GetSnapshot().Where(value => value.BuildingId.HasValue))
                if (slot.BlockReason != ProductionBlockReason.None)
                    _buildingBlockTicks[slot.BlockReason] =
                        _buildingBlockTicks.GetValueOrDefault(slot.BlockReason) + 1;

            SampleAiTempo(tick);

            
var spawned = _combat.GetCombatCounts().Where(value => value.Faction == MatchFaction.Player)
                .Sum(value => value.Spawned);
            if (spawned > _lastPlayerSpawned)
            {
                _maximumDeploymentGapTicks = Math.Max(_maximumDeploymentGapTicks,
                    tick - _lastPlayerDeploymentTick);
                _lastPlayerDeploymentTick = tick;
                _lastPlayerSpawned = spawned;
            }
        }

private void SampleAiTempo(int tick)
        {
            if (_aiStrategy == null)
                return;

            var decisions = _aiStrategy.GetDecisions();
            for (var index = _lastAiDecisionCount; index < decisions.Count; index++)
            {
                var decision = decisions[index];
                var succeeded = decision.GateFailure == AiGateFailureReason.None &&
                                !decision.Result.Contains("failed", StringComparison.Ordinal) &&
                                !decision.Result.Contains("invalid", StringComparison.Ordinal) &&
                                !decision.Result.Contains("rolled-back", StringComparison.Ordinal) &&
                                !decision.Result.Contains("transaction", StringComparison.Ordinal);
                var isPressure = succeeded &&
                                 decision.DefenseTriggerKind != AiDefenseTriggerKind.LogisticsDefense &&
                                 decision.Result.StartsWith("train:", StringComparison.Ordinal);
                if (!isPressure)
                    continue;

                if (string.Equals(decision.IntentId, _lastSuccessfulEnemyIntent, StringComparison.Ordinal))
                    _currentConsecutiveEnemyIntentCount++;
                else
                {
                    _lastSuccessfulEnemyIntent = decision.IntentId;
                    _currentConsecutiveEnemyIntentCount = 1;
                }

                _maximumConsecutiveEnemyIntentCount = Math.Max(
                    _maximumConsecutiveEnemyIntentCount, _currentConsecutiveEnemyIntentCount);

                if (_lastEnemyPressureTick >= 0)
                    _longestEnemyPressureGapTicks = Math.Max(
                        _longestEnemyPressureGapTicks, decision.Tick - _lastEnemyPressureTick);
                _lastEnemyPressureTick = decision.Tick;
            }

            _lastAiDecisionCount = decisions.Count;
            var tempoState = _aiStrategy.CurrentTempoState;
            if (tempoState == AiTempoState.Recovering && _lastTempoState != AiTempoState.Recovering)
                _recoveryStartTick = tick;
            else if (tempoState != AiTempoState.Recovering &&
                     _lastTempoState == AiTempoState.Recovering &&
                     _recoveryStartTick >= 0)
            {
                _completedPressureCycles++;
                _totalRecoveryDurationTicks += Math.Max(0, tick - _recoveryStartTick);
                _recoveryStartTick = -1;
            }

            _lastTempoState = tempoState;
        }


public MatchAnalysisSnapshot Capture(bool victory)
        {
            var walls = _combat.GetWalls();
            var playerWall = walls.Single(value => value.Faction == MatchFaction.Player);
            var enemyWall = walls.Single(value => value.Faction == MatchFaction.Enemy);
            var breakpoint = _zeroAvailabilityTicks.OrderByDescending(value => value.Value)
                .ThenBy(value => value.Key.Value, StringComparer.Ordinal).First();
            var deploymentGap = Math.Max(_maximumDeploymentGapTicks, _currentTick - _lastPlayerDeploymentTick);
            var claims = _boss.GetClaims();
            var processingTicks = _buildingBlockTicks.GetValueOrDefault(ProductionBlockReason.MissingInput);
            var counts = _combat.GetCombatCounts();
            var failureCause = victory ? MatchFailureCause.None : ClassifyFailure(counts, breakpoint.Value,
                processingTicks, deploymentGap, _research.GetSnapshot().CompletedRanks, _construction.GetTowers().Count);

            var decisions = _aiStrategy?.GetDecisions() ?? Array.Empty<AiDecisionSnapshot>();
            var pressureTicks = decisions.Where(value =>
                    value.DefenseTriggerKind != AiDefenseTriggerKind.LogisticsDefense &&
                    value.Result.StartsWith("train:", StringComparison.Ordinal))
                .Select(value => value.Tick).OrderBy(value => value).ToArray();
            var averagePressureInterval = pressureTicks.Length < 2 ? 0 :
                pressureTicks.Zip(pressureTicks.Skip(1), (left, right) => right - left).Sum() / (pressureTicks.Length - 1);
            var intentTotal = Math.Max(1, decisions.Count);
            var intentPermille = decisions.GroupBy(value => value.IntentId, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.Count() * 1000 / intentTotal, StringComparer.Ordinal);

            return new MatchAnalysisSnapshot(_currentTick, playerWall, enemyWall, counts,
                _combat.GetWallDamageSources(), breakpoint.Key, breakpoint.Value, processingTicks, deploymentGap,
                claims.Count(value => value.Faction == MatchFaction.Player),
                claims.Count(value => value.Faction == MatchFaction.Enemy), _construction.GetTowers().Count,
                _hand.TotalCount, _research.GetSnapshot().CompletedRanks, _enemyEconomy.GetLedger().Count,
                failureCause, _firstResourceDepositTick, pressureTicks.Length == 0 ? -1 : pressureTicks[0],
                averagePressureInterval, intentPermille,
                decisions.Count(value => value.GateFailure == AiGateFailureReason.ProtectedReserve),
                _completedPressureCycles, _maximumConsecutiveEnemyIntentCount,
                Math.Max(_longestEnemyPressureGapTicks,
                    _lastEnemyPressureTick < 0 ? 0 : _currentTick - _lastEnemyPressureTick),
                _completedPressureCycles == 0 ? 0 :
                    _totalRecoveryDurationTicks / _completedPressureCycles);
        }

        private static MatchFailureCause ClassifyFailure(IReadOnlyList<UnitCombatCountSnapshot> counts,
            int rawResourceZeroTicks, int processingTicks, int deploymentGapTicks, int pendingResearch, int towers)
        {
            if (rawResourceZeroTicks >= 600) return MatchFailureCause.GatheringInterrupted;
            if (processingTicks >= 300) return MatchFailureCause.ProcessingBottleneck;
            if (deploymentGapTicks >= 600) return MatchFailureCause.DeploymentGap;
            var player = counts.Where(value => value.Faction == MatchFaction.Player).Sum(value => value.Casualties);
            var enemy = counts.Where(value => value.Faction == MatchFaction.Enemy).Sum(value => value.Casualties);
            if (player > Math.Max(2, enemy * 2)) return MatchFailureCause.UnitCompositionCountered;
            if (pendingResearch > 0) return MatchFailureCause.ResearchAllocationError;
            if (towers >= 2 && counts.Where(value => value.Faction == MatchFaction.Player).Sum(value => value.Spawned) < 4)
                return MatchFailureCause.TowerOverinvestment;
            return MatchFailureCause.DeploymentGap;
        }
    }

    public static class MatchResultReportFormatter
    {
        public static string Format(MatchConfigSnapshot config, SettlementReceipt receipt, bool victory,
            MatchAnalysisSnapshot analysis)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (analysis == null) throw new ArgumentNullException(nameof(analysis));
            var mode = config.MapModeKind switch
            {
                MapModeKind.ActiveOffense => "主动进攻",
                MapModeKind.Nightmare => "噩梦",
                _ => "和平发展"
            };
            var minutes = analysis.DurationTicks / ContentConstants.FixedTicksPerSecond / 60;
            var seconds = analysis.DurationTicks / ContentConstants.FixedTicksPerSecond % 60;
            var playerCounts = analysis.CombatCounts.Where(value => value.Faction == MatchFaction.Player).ToArray();
            var enemyCounts = analysis.CombatCounts.Where(value => value.Faction == MatchFaction.Enemy).ToArray();
            var playerSpawned = playerCounts.Sum(value => value.Spawned);
            var playerLost = playerCounts.Sum(value => value.Casualties);
            var enemyLost = enemyCounts.Sum(value => value.Casualties);
            var wallSource = analysis.WallDamageSources.Where(value => value.Attacker == MatchFaction.Player)
                .OrderByDescending(value => value.Damage).FirstOrDefault();
            var sourceText = wallSource == null ? "无" : $"{DisplayUnit(wallSource.UnitId)} {wallSource.Damage}";
            var firstClear = receipt.FirstClear ? config.Reward.FirstClearGold : 0;
            var victoryGold = victory ? config.Reward.VictoryGold : 0;
            
            var firstDeposit = analysis.FirstResourceDepositTick < 0 ? "未发生" :
                $"{analysis.FirstResourceDepositTick / ContentConstants.FixedTicksPerSecond}秒";
            var firstPressure = analysis.FirstEnemyPressureTick < 0 ? "未发生" :
                $"{analysis.FirstEnemyPressureTick / ContentConstants.FixedTicksPerSecond}秒";
            var averagePressure = analysis.AverageEnemyPressureIntervalTicks <= 0 ? "样本不足" :
                $"{analysis.AverageEnemyPressureIntervalTicks / ContentConstants.FixedTicksPerSecond}秒";
            var intentText = analysis.EnemyIntentPermille.Count == 0 ? "无" : string.Join(" / ",
                analysis.EnemyIntentPermille.OrderByDescending(value => value.Value)
                    .ThenBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => $"{value.Key.Replace("intent.", string.Empty)} {value.Value / 10}%"));
var multiplier = config.Reward.ModeMultiplierMilli / 1000m;
            return $"战场：{config.BattlefieldDisplayName} · {mode}\n" +
                   $"局时：{minutes:00}:{seconds:00} · Boss归属 我{analysis.PlayerBossClaims}/敌{analysis.EnemyBossClaims}\n" +
                   $"城墙：我 {analysis.PlayerWall.Health}/{analysis.PlayerWall.MaxHealth} · 敌 {analysis.EnemyWall.Health}/{analysis.EnemyWall.MaxHealth}\n" +
                   $"交换：出征 {playerSpawned} · 我损 {playerLost} · 敌损 {enemyLost}\n" +
                   $"城墙伤害主力：{sourceText} · 未用手牌 {analysis.UnusedHandCount}\n" +
                   $"首次入库：{firstDeposit} · 首次敌军压力：{firstPressure} · 平均压力间隔：{averagePressure}\n" +
                   $"断点：{DisplayResource(analysis.PrimaryResourceBreakpoint)} {analysis.ResourceBreakpointTicks / ContentConstants.FixedTicksPerSecond}秒 · " +
                   $"最长部署空窗 {analysis.MaximumDeploymentGapTicks / ContentConstants.FixedTicksPerSecond}秒\n" +
                   $"敌方意图占比：{intentText} · 储备保护延迟 {analysis.ProtectedReserveDelayCount} 次\n" +
                   $"压力循环：{analysis.CompletedPressureCycles} · 连续同意图峰值 {analysis.MaximumConsecutiveEnemyIntentCount} · " +
                   $"最长无压力 {analysis.LongestEnemyPressureGapTicks / ContentConstants.FixedTicksPerSecond}秒 · " +
                   $"平均恢复 {analysis.AverageRecoveryDurationTicks / ContentConstants.FixedTicksPerSecond}秒\n" +
                   $"战况分析：{DisplayFailure(analysis.FailureCause, victory)}\n" +
                   $"金币明细：完成 {config.Reward.CompletionGold} + 胜利 {victoryGold} + 首通 {firstClear}，模式 ×{multiplier:0.##}\n" +
                   $"获得金币 +{receipt.GoldAwarded} · 总金币 {receipt.GoldBalance}";
        }

        private static string DisplayUnit(UnitId id) => id.Value switch
        {
            "unit.shield-guard" => "盾卫", "unit.archer" => "弓手", "unit.siege-ram" => "攻城槌", _ => id.Value
        };

        private static string DisplayResource(ResourceId id) => id.Value switch
        {
            ContentConstants.FoodResourceId => "食物", ContentConstants.WoodResourceId => "木材",
            ContentConstants.RawStoneResourceId => "原石", ContentConstants.IronOreResourceId => "铁矿", _ => id.Value
        };

        private static string DisplayFailure(MatchFailureCause cause, bool victory) => victory ? "推进成功" : cause switch
        {
            MatchFailureCause.GatheringInterrupted => "采集线被截断",
            MatchFailureCause.ProcessingBottleneck => "加工瓶颈",
            MatchFailureCause.DeploymentGap => "部署空窗",
            MatchFailureCause.UnitCompositionCountered => "兵种组合被克制",
            MatchFailureCause.ResearchAllocationError => "研究分配失误",
            MatchFailureCause.TowerOverinvestment => "过度投资箭塔",
            _ => "未分类"
        };
    }
}
