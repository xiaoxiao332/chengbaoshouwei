using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Resources;
using FortressFrontier.Runtime.Scenes;
using UnityEngine;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class GameplayWorldPresentationSystem : GameSystemBase, IGameTickable
    {
        private sealed class LeaseEntry
        {
            public ResourceKey Key;
            public IInstanceLease Lease;
            
            public bool HasPosition;
            public int LastX;
            public int LastY;
            public int FacingDirection;
            public int AttackRevision;
            public int DamageRevision;
public GameplayWorldEntityView View;
        }

        private readonly IResourceService _resources;
        private readonly GameplayWorldContext _world;
        private readonly ResourceNodeSystem _nodes;
        private readonly GathererSystem _playerGatherers;
        private readonly GathererSystem _enemyGatherers;
        private readonly TrainingSystem _training;
        private readonly EnemyTrainingSystem _enemyTraining;
        private readonly CombatSystem _combat;
        private readonly TowerConstructionSystem _playerConstruction;
        private readonly TowerConstructionSystem _enemyConstruction;
        
        private readonly FixedSimulationSystem _simulation;
private readonly BossSystem _boss;
        private readonly GameplayWorldPresentationProfile _profile;
        private readonly Dictionary<string, LeaseEntry> _nodeViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LeaseEntry> _gathererViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LeaseEntry> _unitViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LeaseEntry> _projectileViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LeaseEntry> _previewViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LeaseEntry> _constructionViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LeaseEntry> _bossViews = new(StringComparer.Ordinal);
        private readonly Dictionary<string, LeaseEntry> _bossEffectViews = new(StringComparer.Ordinal);
        private CancellationTokenSource _lifetime;
        private Task _syncTask = Task.CompletedTask;
        private float _elapsed;

        public GameplayWorldPresentationSystem(IResourceService resources, GameplayWorldContext world,
            ResourceNodeSystem nodes, GathererSystem playerGatherers, GathererSystem enemyGatherers,
            TrainingSystem training, EnemyTrainingSystem enemyTraining, CombatSystem combat, TowerConstructionSystem playerConstruction,
            TowerConstructionSystem enemyConstruction, BossSystem boss, FixedSimulationSystem simulation,
            MatchPresentationConfig presentation) : base(SystemLifetime.Scene)
        {
            _resources = resources; _world = world; _nodes = nodes; _playerGatherers = playerGatherers;
            _enemyGatherers = enemyGatherers; _training = training; _enemyTraining = enemyTraining;
            _combat = combat; _playerConstruction = playerConstruction; _enemyConstruction = enemyConstruction;
            _boss = boss; _simulation = simulation; _profile = new GameplayWorldPresentationProfile(presentation);
        }

        protected override async Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _world.Initialize();
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            await SyncAsync(_lifetime.Token);
        }

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _lifetime?.Cancel();
            try { await _syncTask; }
            catch (OperationCanceledException) { }
            DisposeAll(_nodeViews); DisposeAll(_gathererViews); DisposeAll(_previewViews); DisposeAll(_unitViews);
            DisposeAll(_projectileViews); DisposeAll(_constructionViews); DisposeAll(_bossViews); DisposeAll(_bossEffectViews);
            _lifetime?.Dispose(); _lifetime = null;
            _world.Shutdown();
        }

public void Tick(float deltaTime)
        {
            if (_lifetime == null) return;
            TickVisuals(deltaTime, _simulation.IsPaused);
            if (_syncTask.IsFaulted || !_syncTask.IsCompleted) return;
            _elapsed += deltaTime;
            if (_elapsed < 0.1f) return;
            _elapsed = 0f;
            _syncTask = SyncAsync(_lifetime.Token);
        }

        private void TickVisuals(float deltaTime, bool paused)
        {
            TickVisuals(_nodeViews, deltaTime, paused);
            TickVisuals(_gathererViews, deltaTime, paused);
            TickVisuals(_unitViews, deltaTime, paused);
            TickVisuals(_projectileViews, deltaTime, paused);
            TickVisuals(_previewViews, deltaTime, paused);
            TickVisuals(_constructionViews, deltaTime, paused);
            TickVisuals(_bossViews, deltaTime, paused);
            TickVisuals(_bossEffectViews, deltaTime, paused);
        }

        private static void TickVisuals(Dictionary<string, LeaseEntry> views, float deltaTime, bool paused)
        {
            foreach (var entry in views.Values) entry.View.TickVisual(deltaTime, paused);
        }

        private async Task SyncAsync(CancellationToken cancellationToken)
        {
            var nodes = _nodes.GetSnapshot().Where(value => value.Active).ToArray();
            await Sync(nodes.Select(value => new Desired(
                value.Id.Value,
                _profile.ResourceNode(value.ResourceId ?? throw new InvalidOperationException($"Active node '{value.Id}' has no resource.")),
                value.X, value.Y, value.IsDepleted ? "枯竭" : value.Remaining.ToString(), value.IsDepleted ? 0.55f : 0.8f)),
                _nodeViews, _world.WorldConstructionOverlay, cancellationToken);

            var gatherers = _playerGatherers.GetSnapshot().Concat(_enemyGatherers.GetSnapshot()).ToArray();
            await Sync(gatherers.Select(value => new Desired(
                $"{value.Faction}:{value.Id}", value.BuildingInstanceId == 0
                    ? _profile.Gatherer(value.Faction) : _profile.Gatherer(value.UnitId, value.Faction), value.X, value.Y,
                value.CarriedAmount > 0 ? $"+{value.CarriedAmount}" : string.Empty, 0.72f, Color.white,
                 value.State == GathererState.Gathering ? WorldEntityMotionState.Gathering : WorldEntityMotionState.Moving,
                 DefaultFacing(value.Faction), true, 0, value.DamageRevision, false, true)),
                _gathererViews, _world.WorldUnitsOverlay, cancellationToken);

            var units = _combat.GetUnits();
            await Sync(units.Select(value => new Desired(
                value.Id.ToString(), _profile.Unit(value.UnitId, value.Faction), value.X, value.Y,
                value.Health < value.MaxHealth ? $"{value.Health}" : string.Empty, value.UnitId.Value == "unit.archer" ? 0.68f : 0.75f, Color.white,
                WorldEntityMotionState.Idle, DefaultFacing(value.Faction), true,
                value.AttackRevision, value.DamageRevision, value.UnitId.Value == "unit.siege-ram", true)),
                _unitViews, _world.WorldUnitsOverlay, cancellationToken);

            var projectiles = _combat.GetProjectiles();
            await Sync(projectiles.Select(CreateProjectileDesired),
                _projectileViews, _world.WorldEffectsOverlay, cancellationToken);

            var previews = _training.GetDeploymentSlots().Select(value => (value, MatchFaction.Player))
                .Concat(_enemyTraining.GetDeploymentSlots().Select(value => (value, MatchFaction.Enemy))).ToArray();
            var previewVisuals = previews.Select(pair => new Desired(
                $"preview:{pair.Item2}:{pair.value.Id}", _profile.Unit(pair.value.UnitId, pair.Item2), pair.value.Point.X, pair.value.Point.Y,
                pair.value.State == DeploymentSlotState.Training ? "路线预告" : "等待营地", 0.72f,
                pair.Item2 == MatchFaction.Enemy ? new Color(0.88f, 0.32f, 0.28f, 0.38f) : new Color(0.39f, 0.78f, 0.88f, 0.38f),
                WorldEntityMotionState.Preview, DefaultFacing(pair.Item2)))
                .Concat(previews.Where(pair => pair.Item2 == MatchFaction.Enemy).Select(pair => new Desired(
                    $"route:{pair.value.Id}", _profile.EnemyOrderRoute(), pair.value.Point.X - 54, pair.value.Point.Y,
                    "路线预告", 0.54f, new Color(0.88f, 0.22f, 0.18f, 0.24f))));
            await Sync(previewVisuals, _previewViews, _world.WorldUnitsOverlay, cancellationToken);

            var sites = _playerConstruction.GetSites().Concat(_enemyConstruction.GetSites()).ToArray();
            var towers = _playerConstruction.GetTowers().Concat(_enemyConstruction.GetTowers()).ToArray();
            var construction = sites.Select(value => new Desired($"site:{value.Faction}:{value.Id}",
                    _profile.TowerSite(value.Faction), value.X, value.Y,
                    value.State == TowerSiteState.Constructing ? $"{value.ProgressTicks * 100 / Math.Max(1, value.RequiredTicks)}%" : "工地", 0.82f))
                .Concat(sites.Where(value => value.BuilderActive).Select(value => new Desired($"builder:{value.Faction}:{value.Id}",
                    _profile.Builder(value.Faction), value.BuilderX, value.BuilderY, "", 0.7f)))
                .Concat(towers.Select(value => new Desired($"tower:{value.Faction}:{value.Id}", _profile.Tower(value.Faction),
                    value.X, value.Y, value.Health.ToString(), 0.86f)));
            await Sync(construction, _constructionViews, _world.WorldConstructionOverlay, cancellationToken);

            var bosses = _boss.GetSnapshot().Where(value => value.State is BossRuntimeState.Active or BossRuntimeState.RewardCore).ToArray();
            await Sync(bosses.Select(value => new Desired($"boss:{value.SpawnId}",
                 _profile.Boss(value.State == BossRuntimeState.RewardCore), value.X, value.Y,
                 value.State == BossRuntimeState.RewardCore ? "资源掉落" : $"{value.Health} · {value.CombatState}",
                 value.State == BossRuntimeState.RewardCore ? 0.7f : 1f, Color.white,
                 WorldEntityMotionState.Idle, 1, false, value.AttackRevision, value.DamageRevision, true, true)),
                 _bossViews, _world.WorldUnitsOverlay, cancellationToken);

            var hazards = _boss.GetHazards();
            var hazardViews = hazards.Select(value => new Desired($"boss-zone:{value.Id}",
                    _profile.BossWarningZone(), value.X, value.Y, string.Empty, Math.Max(0.5f, value.Radius * 2f / 112f),
                    new Color(0.72f, 0.10f, 0.08f, value.State == BossHazardState.Impact ? 0.72f : 0.42f)))
                .Concat(hazards.Where(value => value.State is BossHazardState.MeteorFalling or BossHazardState.Impact)
                    .Select(value => new Desired($"boss-meteor:{value.Id}", _profile.BossMeteor(),
                        value.MeteorX, value.MeteorY, string.Empty, 0.84f, Color.white,
                        WorldEntityMotionState.Moving, 1, true, smoothPosition: true)));
            await Sync(hazardViews, _bossEffectViews, _world.WorldEffectsOverlay, cancellationToken);
        }

        private static int DefaultFacing(MatchFaction faction) => faction == MatchFaction.Player ? 1 : -1;

        private Desired CreateProjectileDesired(CombatProjectileSnapshot value)
        {
            var progress = Mathf.Clamp01(value.FlightProgressMilli / 1000f);
            var flightX = value.TargetX - value.OriginX;
            var flightY = value.TargetY - value.OriginY;
            var span = Mathf.Sqrt((float)flightX * flightX + (float)flightY * flightY);
            var arcHeight = value.ProjectileKind == UnitProjectileKind.Fireball ? 0f : Mathf.Clamp(span * 0.18f, 24f, 96f);
            var arcOffset = 4f * arcHeight * progress * (1f - progress);
            var tangentY = flightY + 4f * arcHeight * (1f - 2f * progress);
            var rotation = Mathf.Atan2(tangentY, flightX == 0 ? value.TargetX - value.X : flightX) * Mathf.Rad2Deg;
            var key = string.IsNullOrWhiteSpace(value.PresentationKey.Value)
                ? _profile.Projectile(value.ProjectileKind) : value.PresentationKey;
            var scale = value.ProjectileKind == UnitProjectileKind.Cannonball ? 0.88f : value.ProjectileKind == UnitProjectileKind.Fireball ? 0.82f : 0.72f;
            Vector2? initialPosition = null;
            if (value.ProjectileKind == UnitProjectileKind.Cannonball && value.SourceUnitId.Value == "unit.cannon" &&
                value.SourceUnitHandle > 0 &&
                _unitViews.TryGetValue(value.SourceUnitHandle.ToString(), out var source) &&
                source.View.TryGetProjectileOrigin(_world.WorldEffectsOverlay, out var projectileOrigin))
                initialPosition = projectileOrigin;
            return new Desired($"projectile:{value.Id}", key, value.X,
                Mathf.RoundToInt(value.Y + arcOffset), string.Empty, scale, Color.white,
                WorldEntityMotionState.Projectile, 1, smoothPosition: true, movementRotation: rotation,
                initialPosition: initialPosition);
        }

        private async Task Sync(IEnumerable<Desired> desiredSource, Dictionary<string, LeaseEntry> current,
            RectTransform parent, CancellationToken cancellationToken)
        {
            var desired = desiredSource.ToDictionary(value => value.Id, StringComparer.Ordinal);
            foreach (var stale in current.Keys.Where(id => !desired.ContainsKey(id)).ToArray())
            { current[stale].Lease.Dispose(); current.Remove(stale); }

            foreach (var pair in desired.OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                var created = false;
                if (!current.TryGetValue(pair.Key, out var entry) || !entry.Key.Equals(pair.Value.Key))
                {
                    if (entry != null) { entry.Lease.Dispose(); current.Remove(pair.Key); }
                    var lease = await _resources.SpawnAsync(pair.Value.Key, parent, cancellationToken);
                    var view = lease.Instance.GetComponent<GameplayWorldEntityView>()
                        ?? throw new InvalidOperationException($"World prefab '{pair.Value.Key}' has no GameplayWorldEntityView.");
                    entry = new LeaseEntry
                    {
                        Key = pair.Value.Key, Lease = lease, View = view,
                        FacingDirection = pair.Value.DefaultFacing,
                        AttackRevision = pair.Value.AttackRevision,
                        DamageRevision = pair.Value.DamageRevision
                    };
                    current.Add(pair.Key, entry);
                    created = true;
                }

                var motionState = pair.Value.MotionState;
                var facingDirection = entry.FacingDirection == 0 ? pair.Value.DefaultFacing : entry.FacingDirection;
                if (entry.HasPosition && pair.Value.DetectMovement)
                {
                    var deltaX = pair.Value.X - entry.LastX;
                    var moved = deltaX != 0 || pair.Value.Y != entry.LastY;
                    if (deltaX != 0) facingDirection = deltaX > 0 ? 1 : -1;
                    if (motionState != WorldEntityMotionState.Gathering)
                        motionState = moved ? WorldEntityMotionState.Moving : WorldEntityMotionState.Idle;
                }

                var attackTriggered = !created && pair.Value.AttackRevision != entry.AttackRevision;
                var damageTriggered = !created && pair.Value.DamageRevision != entry.DamageRevision;
                entry.LastX = pair.Value.X; entry.LastY = pair.Value.Y; entry.HasPosition = true;
                entry.FacingDirection = facingDirection;
                entry.AttackRevision = pair.Value.AttackRevision;
                entry.DamageRevision = pair.Value.DamageRevision;
                entry.View.Present(pair.Value.X, pair.Value.Y, pair.Value.Label, pair.Value.Scale, pair.Value.Tint,
                    motionState, facingDirection, attackTriggered, damageTriggered, pair.Value.HeavyAttack,
                    pair.Value.SmoothPosition, pair.Value.MovementRotation, created ? pair.Value.InitialPosition : null);
            }
        }

        private static void DisposeAll(Dictionary<string, LeaseEntry> values)
        { foreach (var entry in values.Values) entry.Lease.Dispose(); values.Clear(); }

        private readonly struct Desired
        {
            public Desired(string id, ResourceKey key, int x, int y, string label, float scale)
                : this(id, key, x, y, label, scale, Color.white) { }

            public Desired(string id, ResourceKey key, int x, int y, string label, float scale, Color tint,
                WorldEntityMotionState motionState = WorldEntityMotionState.Static, int defaultFacing = 1,
                bool detectMovement = false, int attackRevision = 0, int damageRevision = 0,
                bool heavyAttack = false, bool smoothPosition = false, float movementRotation = 0f,
                Vector2? initialPosition = null)
            {
                Id = id; Key = key; X = x; Y = y; Label = label; Scale = scale; Tint = tint;
                MotionState = motionState; DefaultFacing = defaultFacing; DetectMovement = detectMovement;
                AttackRevision = attackRevision; DamageRevision = damageRevision; HeavyAttack = heavyAttack;
                SmoothPosition = smoothPosition; MovementRotation = movementRotation; InitialPosition = initialPosition;
            }

            public string Id { get; }
            public ResourceKey Key { get; }
            public int X { get; }
            public int Y { get; }
            public string Label { get; }
            public float Scale { get; }
            public Color Tint { get; }
            public WorldEntityMotionState MotionState { get; }
            public int DefaultFacing { get; }
            public bool DetectMovement { get; }
            public int AttackRevision { get; }
            public int DamageRevision { get; }
            public bool HeavyAttack { get; }
            public bool SmoothPosition { get; }
            public float MovementRotation { get; }
            public Vector2? InitialPosition { get; }
        }
    }
}
