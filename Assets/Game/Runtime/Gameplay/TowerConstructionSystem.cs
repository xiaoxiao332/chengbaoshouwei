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
    public enum TowerConstructionFailure
    {
        None,
        InvalidPosition,
        PathBlocked,
        SiteLimitReached,
        TowerLimitReached,
        CardMissing,
        InsufficientResources
    }

    public enum TowerSiteState { Blueprint, BuilderTraveling, Constructing, Completed }

    public interface IMatchCardInventory
    {
        bool Contains(CardId cardId);
        bool TryConsume(CardId cardId);
    }

    public sealed class FixedMatchCardInventory : IMatchCardInventory
    {
        private readonly Dictionary<CardId, int> _cards = new();

        public FixedMatchCardInventory(IEnumerable<CardId> cards)
        {
            foreach (var card in cards ?? Array.Empty<CardId>())
                _cards[card] = _cards.GetValueOrDefault(card) + 1;
        }

        public bool Contains(CardId cardId) => _cards.GetValueOrDefault(cardId) > 0;
        public bool TryConsume(CardId cardId)
        {
            if (!Contains(cardId)) return false;
            if (--_cards[cardId] == 0) _cards.Remove(cardId);
            return true;
        }
    }

    public sealed class TowerSiteSnapshot
    {
        public TowerSiteSnapshot(int id, MatchFaction faction, int x, int y, TowerSiteState state,
            int progressTicks, int requiredTicks, bool builderActive, int builderX, int builderY)
        { Id = id; Faction = faction; X = x; Y = y; State = state; ProgressTicks = progressTicks; RequiredTicks = requiredTicks; BuilderActive = builderActive; BuilderX = builderX; BuilderY = builderY; }
        public int Id { get; }
        public MatchFaction Faction { get; }
        public int X { get; }
        public int Y { get; }
        public TowerSiteState State { get; }
        public int ProgressTicks { get; }
        public int RequiredTicks { get; }
        public bool BuilderActive { get; }
        public int BuilderX { get; }
        public int BuilderY { get; }
    }

    public sealed class TowerSnapshot
    {
        public TowerSnapshot(int id, MatchFaction faction, int x, int y, int health, int maxHealth)
        { Id = id; Faction = faction; X = x; Y = y; Health = health; MaxHealth = maxHealth; }
        public int Id { get; }
        public MatchFaction Faction { get; }
        public int X { get; }
        public int Y { get; }
        public int Health { get; }
        public int MaxHealth { get; }
    }

    /// <summary>Faction-neutral tower command boundary. It owns only world construction state.</summary>
    public class TowerConstructionSystem : GameSystemBase, IFixedMatchSimulation
    {
        private const int BuilderTravelTicks = 10;
        private sealed class Site
        {
            public int Id; public int X; public int Y; public int Progress; public TowerSiteState State;
            public int BuilderTravelProgress;
        }
        private sealed class Tower { public int Id; public int X; public int Y; public int Health; }

        private readonly MatchFaction _faction;
        private readonly MatchConfigSnapshot _config;
        private readonly EconomySystem _economy;
        private readonly IMatchCardInventory _cards;
        private readonly CardId _towerCardId;
        private readonly bool _enabled;
        private readonly List<Site> _sites = new();
        private readonly List<Tower> _towers = new();
        private int _nextId = 1;
        private int _currentTick;
        private int _builderRespawnTick;

        public TowerConstructionSystem(MatchFaction faction, MatchConfigSnapshot config, EconomySystem economy,
            IMatchCardInventory cards) : base(SystemLifetime.Scene)
        {
            _faction = faction;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _economy = economy ?? throw new ArgumentNullException(nameof(economy));
            _cards = cards ?? throw new ArgumentNullException(nameof(cards));
            var tower = config.Buildings.FirstOrDefault(value => value.Id.Equals(config.Construction.TowerBuildingId));
            _towerCardId = tower?.SourceCardId ?? default;
            _enabled = tower != null && config.Construction.MaxSites > 0 && config.Construction.MaxTowers > 0;
        }

        public event Action Changed;
        public bool HasTowerCard => _enabled && _cards.Contains(_towerCardId);
        public IReadOnlyList<TowerSiteSnapshot> GetSites() => _sites.OrderBy(value => value.Id).Select(ToSnapshot).ToArray();
        public IReadOnlyList<TowerSnapshot> GetTowers() => _towers.OrderBy(value => value.Id)
            .Select(value => new TowerSnapshot(value.Id, _faction, value.X, value.Y, value.Health, _config.Construction.MaxHealth)).ToArray();

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) => Task.CompletedTask;
        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        { _sites.Clear(); _towers.Clear(); return Task.CompletedTask; }

        public TowerConstructionFailure TryStartSite(int x, int y, out int siteId)
        {
            siteId = 0;
            var validation = ValidateStartSite(x, y);
            if (validation != TowerConstructionFailure.None) return validation;
            if (!_economy.TryReserve(_config.Construction.Costs, "source.tower-construction", "intent.build-tower",
                    out var reservation, out _))
                return TowerConstructionFailure.InsufficientResources;
            if (!_cards.TryConsume(_towerCardId))
            { _economy.Release(reservation); return TowerConstructionFailure.CardMissing; }
            if (!_economy.TryCommit(reservation, _config.Construction.Costs, out _))
                throw new InvalidOperationException("A validated tower construction reservation could not be committed.");

            var site = new Site { Id = _nextId++, X = x, Y = y, State = TowerSiteState.Blueprint };
            _sites.Add(site); siteId = site.Id; Changed?.Invoke();
            return TowerConstructionFailure.None;
        }

        public TowerConstructionFailure ValidateStartSite(int x, int y)
        {
            if (!_enabled || !_cards.Contains(_towerCardId)) return TowerConstructionFailure.CardMissing;
            if (!IsLegalPosition(x, y)) return TowerConstructionFailure.InvalidPosition;
            if (!HasStraightGateRoute(x, y)) return TowerConstructionFailure.PathBlocked;
            if (_sites.Count >= _config.Construction.MaxSites) return TowerConstructionFailure.SiteLimitReached;
            if (_towers.Count + _sites.Count >= _config.Construction.MaxTowers) return TowerConstructionFailure.TowerLimitReached;
            return _config.Construction.Costs.All(value => _economy.GetAvailable(value.ResourceId) >= value.Amount)
                ? TowerConstructionFailure.None : TowerConstructionFailure.InsufficientResources;
        }

        public bool KillActiveBuilder()
        {
            var site = _sites.FirstOrDefault(value => value.State is TowerSiteState.BuilderTraveling or TowerSiteState.Constructing);
            if (site == null) return false;
            site.Progress = site.Progress * _config.Construction.RetainedProgressMilli / 1000;
            site.BuilderTravelProgress = 0;
            site.State = TowerSiteState.Blueprint;
            _builderRespawnTick = _currentTick + _config.Construction.BuilderRespawnTicks;
            Changed?.Invoke(); return true;
        }

        public bool TryDamageTower(int towerId, int damage)
        {
            if (damage <= 0) return false;
            var tower = _towers.FirstOrDefault(value => value.Id == towerId);
            if (tower == null) return false;
            tower.Health = Math.Max(0, tower.Health - damage);
            if (tower.Health == 0) _towers.Remove(tower);
            Changed?.Invoke(); return true;
        }

        public void SimulateTick(int tick)
        {
            _currentTick = tick;
            if (tick < _builderRespawnTick || _sites.Count == 0) return;
            var active = _sites.FirstOrDefault(value => value.State is TowerSiteState.BuilderTraveling or TowerSiteState.Constructing);
            if (active == null)
            {
                active = _sites.OrderBy(value => value.Id).First();
                active.State = TowerSiteState.BuilderTraveling;
                active.BuilderTravelProgress = 0;
            }
            if (active.State == TowerSiteState.BuilderTraveling)
            {
                if (++active.BuilderTravelProgress < BuilderTravelTicks) { Changed?.Invoke(); return; }
                active.State = TowerSiteState.Constructing;
            }
            if (++active.Progress < Math.Max(1, _config.Construction.ConstructionTicks))
            { Changed?.Invoke(); return; }
            active.State = TowerSiteState.Completed;
            _sites.Remove(active);
            _towers.Add(new Tower { Id = active.Id, X = active.X, Y = active.Y, Health = _config.Construction.MaxHealth });
            Changed?.Invoke();
        }

        private TowerSiteSnapshot ToSnapshot(Site site)
        {
            var gate = _faction == MatchFaction.Player ? _config.Combat.PlayerWall.Gate : _config.Combat.EnemyWall.Gate;
            var traveling = site.State == TowerSiteState.BuilderTraveling;
            var builderX = traveling ? gate.X + (site.X - gate.X) * site.BuilderTravelProgress / BuilderTravelTicks : site.X;
            var builderY = traveling ? gate.Y + (site.Y - gate.Y) * site.BuilderTravelProgress / BuilderTravelTicks : site.Y;
            return new TowerSiteSnapshot(site.Id, _faction, site.X, site.Y, site.State, site.Progress,
                _config.Construction.ConstructionTicks, site.State is TowerSiteState.BuilderTraveling or TowerSiteState.Constructing,
                builderX, builderY);
        }

        private bool IsLegalPosition(int x, int y)
        {
            var zones = _config.BattlefieldLayout.Zones;
            return zones.Any(value => value.Kind == ZoneKind.TowerBuildable && Contains(value, x, y)) &&
                   !zones.Any(value => (value.Kind is ZoneKind.TowerForbidden or ZoneKind.BossForbidden or ZoneKind.MainGate) && Contains(value, x, y));
        }

        private bool HasStraightGateRoute(int x, int y)
        {
            var gate = _faction == MatchFaction.Player ? _config.Combat.PlayerWall.Gate : _config.Combat.EnemyWall.Gate;
            for (var step = 1; step < 10; step++)
            {
                var sampleX = gate.X + (x - gate.X) * step / 10;
                var sampleY = gate.Y + (y - gate.Y) * step / 10;
                if (_config.BattlefieldLayout.Zones.Any(value => value.Kind == ZoneKind.BossForbidden && Contains(value, sampleX, sampleY)))
                    return false;
            }
            return true;
        }

        private static bool Contains(MatchRect rect, int x, int y) =>
            x >= rect.X && x <= rect.X + rect.Width && y >= rect.Y && y <= rect.Y + rect.Height;
    }

    public sealed class PlayerTowerConstructionSystem : TowerConstructionSystem
    {
        public PlayerTowerConstructionSystem(MatchConfigSnapshot config, EconomySystem economy, IMatchCardInventory cards)
            : base(MatchFaction.Player, config, economy, cards) { }
    }

    public sealed class EnemyTowerConstructionSystem : TowerConstructionSystem
    {
        public EnemyTowerConstructionSystem(MatchConfigSnapshot config, EconomySystem economy, IMatchCardInventory cards)
            : base(MatchFaction.Enemy, config, economy, cards) { }
    }
}
