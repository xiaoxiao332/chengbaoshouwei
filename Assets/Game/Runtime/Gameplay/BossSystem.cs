using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Gameplay
{
    public enum BossRuntimeState { Dormant, Warning, Active, RewardCore, Resolved }
    public enum BossCombatState { Idle, Retaliating, SkillTelegraph, SkillImpact, Recovering }
    public enum BossHazardState { Telegraph, MeteorFalling, Impact }

    public sealed class BossRuntimeSnapshot
    {
        public BossRuntimeSnapshot(string spawnId, BossRuntimeState state, int x, int y, int health, int maxHealth,
            int rewardExpiresTick, MatchFaction? winner, BossCombatState combatState = BossCombatState.Idle,
            int attackRevision = 0, int damageRevision = 0)
        {
            SpawnId = spawnId; State = state; X = x; Y = y; Health = health; MaxHealth = maxHealth;
            RewardExpiresTick = rewardExpiresTick; Winner = winner; CombatState = combatState;
            AttackRevision = attackRevision; DamageRevision = damageRevision;
        }
        public string SpawnId { get; }
        public BossRuntimeState State { get; }
        public BossCombatState CombatState { get; }
        public int X { get; }
        public int Y { get; }
        public int Health { get; }
        public int MaxHealth { get; }
        public int RewardExpiresTick { get; }
        public MatchFaction? Winner { get; }
        public int AttackRevision { get; }
        public int DamageRevision { get; }
    }

    public sealed class BossHazardSnapshot
    {
        public BossHazardSnapshot(string id, string spawnId, BossHazardState state, int x, int y,
            int radius, int meteorX, int meteorY, int impactTick)
        { Id = id; SpawnId = spawnId; State = state; X = x; Y = y; Radius = radius; MeteorX = meteorX; MeteorY = meteorY; ImpactTick = impactTick; }
        public string Id { get; }
        public string SpawnId { get; }
        public BossHazardState State { get; }
        public int X { get; }
        public int Y { get; }
        public int Radius { get; }
        public int MeteorX { get; }
        public int MeteorY { get; }
        public int ImpactTick { get; }
    }

    public sealed class BossRewardClaimSnapshot
    {
        public BossRewardClaimSnapshot(string spawnId, MatchFaction faction, string rewardId, BossRewardKind kind,
            int magnitude, ResourceId? resourceId = null, int grantedAmount = 0)
        {
            SpawnId = spawnId; Faction = faction; RewardId = rewardId; Kind = kind; Magnitude = magnitude;
            ResourceId = resourceId; GrantedAmount = grantedAmount;
        }
        public string SpawnId { get; }
        public MatchFaction Faction { get; }
        public string RewardId { get; }
        public BossRewardKind Kind { get; }
        public int Magnitude { get; }
        public ResourceId? ResourceId { get; }
        public int GrantedAmount { get; }
    }

    public sealed class BossSystem : GameSystemBase, IFixedMatchSimulation
    {
        private const int FirstSkillDelayTicks = 30;
        private const int SkillCooldownTicks = 80;
        private const int TelegraphTicks = 20;
        private const int MeteorFlightTicks = 10;
        private const int ImpactLingerTicks = 4;
        private const int RecoveryTicks = 10;
        private const int HazardCount = 3;

        private sealed class HazardState
        {
            public string Id; public int X; public int Y; public int Radius;
            public int TelegraphTick; public int MeteorTick; public int ImpactTick;
            public BossHazardState State;
        }

        private sealed class BossState
        {
            public MatchBossSpawnConfig Spawn; public BossRuntimeState State; public BossCombatState CombatState;
            public int X; public int Y; public int Health; public int BasicCooldown; public int SkillCooldown;
            public int RecoveryTicks; public int RewardExpiresTick; public MatchFaction? Winner;
            public bool Aggro; public int LastAttackerId; public MatchFaction LastAttackerFaction;
            public int AttackRevision; public int DamageRevision;
            public readonly List<HazardState> Hazards = new();
        }

        private readonly MatchConfigSnapshot _config;
        private readonly EconomySystem _playerEconomy;
        private readonly EconomySystem _enemyEconomy;
        private readonly Dictionary<string, BossState> _states = new(StringComparer.Ordinal);
        private readonly List<BossRewardClaimSnapshot> _claims = new();
        private CombatSystem _combat;
        private int _currentTick;

        public BossSystem(MatchConfigSnapshot config, EconomySystem playerEconomy = null,
            EconomySystem enemyEconomy = null) : base(SystemLifetime.Scene)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _playerEconomy = playerEconomy;
            _enemyEconomy = enemyEconomy;
        }

        public event Action Changed;
        public event Action<BossRewardClaimSnapshot> RewardClaimed;

        public IReadOnlyList<BossRuntimeSnapshot> GetSnapshot() => _states.Values.OrderBy(value => value.Spawn.SpawnTick)
            .ThenBy(value => value.Spawn.Id, StringComparer.Ordinal).Select(value => new BossRuntimeSnapshot(value.Spawn.Id,
                value.State, value.X, value.Y, value.Health, _config.Boss.MaxHealth,
                value.RewardExpiresTick, value.Winner, value.CombatState, value.AttackRevision, value.DamageRevision)).ToArray();

        public IReadOnlyList<BossHazardSnapshot> GetHazards() => _states.Values
            .OrderBy(value => value.Spawn.Id, StringComparer.Ordinal)
            .SelectMany(value => value.Hazards.OrderBy(hazard => hazard.Id, StringComparer.Ordinal).Select(hazard =>
            {
                var remaining = Math.Max(0, hazard.ImpactTick - _currentTick);
                var meteorY = hazard.Y + remaining * 12;
                return new BossHazardSnapshot(hazard.Id, value.Spawn.Id, hazard.State, hazard.X, hazard.Y,
                    hazard.Radius, hazard.X, meteorY, hazard.ImpactTick);
            })).ToArray();

        public IReadOnlyList<BossRewardClaimSnapshot> GetClaims() => _claims.ToArray();

        internal void BindCombat(CombatSystem combat)
        {
            if (_combat != null) throw new InvalidOperationException("Boss combat was already bound.");
            _combat = combat ?? throw new ArgumentNullException(nameof(combat));
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            foreach (var spawn in _config.BattlefieldLayout.BossSpawns)
                _states.Add(spawn.Id, new BossState
                {
                    Spawn = spawn, State = BossRuntimeState.Dormant, CombatState = BossCombatState.Idle,
                    X = spawn.Position.X, Y = spawn.Position.Y, Health = _config.Boss.MaxHealth
                });
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        { _states.Clear(); _claims.Clear(); _combat = null; return Task.CompletedTask; }

        public bool TryDamage(string spawnId, MatchFaction sourceFaction, int damage, int sourceUnitId = 0)
        {
            if (damage <= 0 || !_states.TryGetValue(spawnId, out var state) || state.State != BossRuntimeState.Active) return false;
            state.Health = Math.Max(0, state.Health - Math.Max(1, damage - _config.Boss.Armor));
            state.DamageRevision++;
            state.Aggro = true;
            state.LastAttackerFaction = sourceFaction;
            state.LastAttackerId = sourceUnitId;
            if (state.CombatState is BossCombatState.Idle or BossCombatState.Retaliating)
                state.CombatState = BossCombatState.Retaliating;
            if (state.Health == 0)
            {
                state.State = BossRuntimeState.RewardCore;
                state.CombatState = BossCombatState.Idle;
                state.Hazards.Clear();
                state.RewardExpiresTick = _currentTick + _config.Boss.RewardCoreLifetimeTicks;
            }
            Changed?.Invoke();
            return true;
        }

        public void SimulateTick(int tick)
        {
            if (_combat == null) return;
            _currentTick = tick;
            var changed = false;
            foreach (var state in _states.Values.OrderBy(value => value.Spawn.SpawnTick)
                         .ThenBy(value => value.Spawn.Id, StringComparer.Ordinal))
            {
                if (state.State == BossRuntimeState.Dormant && tick >= state.Spawn.WarningTick)
                { state.State = BossRuntimeState.Warning; changed = true; }
                if (state.State == BossRuntimeState.Warning && tick >= state.Spawn.SpawnTick)
                {
                    state.State = BossRuntimeState.Active;
                    state.SkillCooldown = FirstSkillDelayTicks;
                    changed = true;
                }
                if (state.State == BossRuntimeState.Active) changed |= TickActive(state);
                else if (state.State == BossRuntimeState.RewardCore)
                {
                    var collector = _combat.GetUnits()
                        .Where(value => Distance(value.X, value.Y, state.X, state.Y) <= _config.Boss.CollisionRadius * 2)
                        .OrderBy(value => value.Id).FirstOrDefault();
                    if (collector != null && Claim(state, collector.Faction)) changed = true;
                    else if (tick >= state.RewardExpiresTick)
                    { state.State = BossRuntimeState.Resolved; changed = true; }
                }
            }
            if (changed) Changed?.Invoke();
        }

        private bool TickActive(BossState state)
        {
            var changed = TickHazards(state);
            if (state.CombatState == BossCombatState.SkillTelegraph || state.CombatState == BossCombatState.SkillImpact)
                return changed;
            if (state.CombatState == BossCombatState.Recovering)
            {
                if (state.RecoveryTicks > 0) state.RecoveryTicks--;
                if (state.RecoveryTicks == 0)
                { state.CombatState = state.Aggro ? BossCombatState.Retaliating : BossCombatState.Idle; changed = true; }
                return changed;
            }
            if (!state.Aggro) return changed;

            if (state.BasicCooldown > 0) state.BasicCooldown--;
            if (state.SkillCooldown > 0) state.SkillCooldown--;
            var target = FindRetaliationTarget(state);
            if (target == null)
            {
                changed |= MoveToward(state, state.Spawn.Position.X, state.Spawn.Position.Y);
                if (state.X == state.Spawn.Position.X && state.Y == state.Spawn.Position.Y)
                { state.Aggro = false; state.CombatState = BossCombatState.Idle; }
                return changed;
            }

            if (state.SkillCooldown == 0)
            { CreateHazards(state); return true; }

            var attackRange = Math.Max(_config.Boss.CollisionRadius * 3, 48);
            var distance = Distance(state.X, state.Y, target.X, target.Y);
            if (distance > attackRange)
                changed |= MoveToward(state, target.X, target.Y);
            else if (state.BasicCooldown == 0 && _combat.TryDamageUnit(target.Id, _config.Boss.AttackDamage))
            {
                state.BasicCooldown = Math.Max(1, _config.Boss.AttackIntervalTicks);
                state.AttackRevision++;
                state.CombatState = BossCombatState.Retaliating;
                changed = true;
            }
            return changed;
        }

        private bool TickHazards(BossState state)
        {
            if (state.Hazards.Count == 0) return false;
            var changed = false;
            if (_currentTick >= state.Hazards[0].ImpactTick && state.Hazards[0].State != BossHazardState.Impact)
            {
                foreach (var hazard in state.Hazards)
                {
                    hazard.State = BossHazardState.Impact;
                    _combat.ApplyBossMeteor(hazard.X, hazard.Y, hazard.Radius,
                        Math.Max(1, _config.Boss.AttackDamage * 2), Math.Max(60, _config.Boss.CollisionRadius * 3));
                }
                state.CombatState = BossCombatState.SkillImpact;
                state.AttackRevision++;
                changed = true;
            }
            else if (_currentTick >= state.Hazards[0].MeteorTick && state.Hazards[0].State == BossHazardState.Telegraph)
            {
                foreach (var hazard in state.Hazards) hazard.State = BossHazardState.MeteorFalling;
                changed = true;
            }
            if (_currentTick >= state.Hazards[0].ImpactTick + ImpactLingerTicks)
            {
                state.Hazards.Clear();
                state.CombatState = BossCombatState.Recovering;
                state.RecoveryTicks = RecoveryTicks;
                state.SkillCooldown = SkillCooldownTicks;
                changed = true;
            }
            return changed;
        }

        private void CreateHazards(BossState state)
        {
            state.Hazards.Clear();
            var targets = _combat.GetUnits().Where(value => Distance(value.X, value.Y, state.X, state.Y) <= _config.Boss.AcquireRadius)
                .OrderBy(value => Distance(value.X, value.Y, state.X, state.Y)).ThenBy(value => value.Id).Take(HazardCount).ToArray();
            var radius = Math.Max(56, _config.Boss.CollisionRadius * 2);
            var offsets = new[] { (-radius * 2, 0), (radius * 2, 0), (0, radius * 2) };
            for (var index = 0; index < HazardCount; index++)
            {
                var targetX = index < targets.Length ? targets[index].X : state.X + offsets[index].Item1;
                var targetY = index < targets.Length ? targets[index].Y : state.Y + offsets[index].Item2;
                targetX = Math.Clamp(targetX, radius, Math.Max(radius, _config.BattlefieldLayout.ReferenceWidth - radius));
                targetY = Math.Clamp(targetY, radius, Math.Max(radius, _config.BattlefieldLayout.ReferenceHeight - radius));
                state.Hazards.Add(new HazardState
                {
                    Id = $"{state.Spawn.Id}:hazard:{index}", X = targetX, Y = targetY, Radius = radius,
                    TelegraphTick = _currentTick, MeteorTick = _currentTick + TelegraphTicks - MeteorFlightTicks,
                    ImpactTick = _currentTick + TelegraphTicks, State = BossHazardState.Telegraph
                });
            }
            state.CombatState = BossCombatState.SkillTelegraph;
        }

        private CombatUnitSnapshot FindRetaliationTarget(BossState state)
        {
            var units = _combat.GetUnits();
            var attacker = state.LastAttackerId == 0 ? null : units.FirstOrDefault(value => value.Id == state.LastAttackerId);
            if (attacker != null && Distance(attacker.X, attacker.Y, state.Spawn.Position.X, state.Spawn.Position.Y) <= _config.Boss.LeashRadius)
                return attacker;
            return units.Where(value => Distance(value.X, value.Y, state.X, state.Y) <= _config.Boss.AcquireRadius)
                .OrderBy(value => value.Faction == state.LastAttackerFaction ? 0 : 1)
                .ThenBy(value => Distance(value.X, value.Y, state.X, state.Y)).ThenBy(value => value.Id).FirstOrDefault();
        }

        private bool MoveToward(BossState state, int targetX, int targetY)
        {
            var deltaX = targetX - state.X;
            var deltaY = targetY - state.Y;
            var distance = Math.Abs(deltaX) + Math.Abs(deltaY);
            if (distance == 0 || _config.Boss.MovePerTick <= 0) return false;
            var step = Math.Min(_config.Boss.MovePerTick, distance);
            var nextX = state.X + deltaX * step / distance;
            var nextY = state.Y + deltaY * step / distance;
            if (nextX == state.X && nextY == state.Y)
            {
                if (Math.Abs(deltaX) >= Math.Abs(deltaY)) nextX += Math.Sign(deltaX);
                else nextY += Math.Sign(deltaY);
            }
            if (Distance(nextX, nextY, state.Spawn.Position.X, state.Spawn.Position.Y) > _config.Boss.LeashRadius) return false;
            state.X = nextX; state.Y = nextY; return true;
        }

        private bool Claim(BossState state, MatchFaction faction)
        {
            var rewards = faction == MatchFaction.Player ? _config.Boss.PlayerRewards : _config.Boss.EnemyRewards;
            var reward = rewards.OrderByDescending(value => value.Weight).ThenBy(value => value.Id, StringComparer.Ordinal).FirstOrDefault();
            if (reward == null) { state.State = BossRuntimeState.Resolved; state.Winner = faction; return true; }

            ResourceId? resourceId = null;
            var granted = 0;
            if (reward.Kind == BossRewardKind.ResourceBundle)
            {
                var economy = faction == MatchFaction.Player ? _playerEconomy : _enemyEconomy;
                if (economy == null || !TryGrantProcessedResource(economy, reward.Magnitude, out var id, out granted)) return false;
                resourceId = id;
            }
            state.State = BossRuntimeState.Resolved;
            state.Winner = faction;
            var claim = new BossRewardClaimSnapshot(state.Spawn.Id, faction, reward.Id, reward.Kind,
                reward.Magnitude, resourceId, granted);
            _claims.Add(claim);
            RewardClaimed?.Invoke(claim);
            return true;
        }

        private bool TryGrantProcessedResource(EconomySystem economy, int requested, out ResourceId resourceId,
            out int granted)
        {
            resourceId = default;
            granted = 0;
            var processed = new HashSet<ResourceId>(_config.Resources
                .Where(value => value.AcquisitionKind == ResourceAcquisitionKind.Processed).Select(value => value.Id));
            foreach (var balance in economy.GetSnapshot().Where(value => processed.Contains(value.Id))
                         .OrderByDescending(value => value.Capacity - value.Amount)
                         .ThenBy(value => value.Id.Value, StringComparer.Ordinal))
            {
                var room = balance.Capacity - balance.Amount;
                var amount = Math.Min(Math.Max(1, requested), room);
                if (amount <= 0 || !economy.TryAdd(balance.Id, amount, out _)) continue;
                resourceId = balance.Id;
                granted = amount;
                return true;
            }
            return false;
        }

        private static int Distance(int x1, int y1, int x2, int y2) => Math.Abs(x1 - x2) + Math.Abs(y1 - y2);
    }
}
