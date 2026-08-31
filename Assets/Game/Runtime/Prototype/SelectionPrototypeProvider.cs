using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Flow;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Prototype
{
    public sealed class SelectionPrototypeProvider : ISelectionCommands
    {
        private static readonly SelectionBattlefieldDefinition FallbackBattlefield = new(new BattlefieldId("battlefield.prologue"),
            "边境序章", new[] { new MapModeId("mode.prologue.peaceful"), new MapModeId("mode.prologue.offensive"), new MapModeId("mode.prologue.nightmare") });
        private readonly IApplicationFlow _applicationFlow;
        private readonly IProgressionReader _progression;
        private readonly IProgressionCommands _progressionCommands;
        private readonly ISelectionContent _content;
        private readonly ISettingsOverlay _settingsOverlay;
        private readonly SelectionCardViewModel[] _catalogCards =
        {
            new(new CardId("card.soldier.shield-guard"), "盾卫", "坚固前排 · 守护城墙", 1, true, 0, 5),
            new(new CardId("card.soldier.archer"), "弓手", "远程输出 · 快速训练", 1, true, 0, 5),
            new(new CardId("card.soldier.siege-ram"), "破城锤", "攻城核心 · 高墙伤", 0, false, 0, 5),
            new(new CardId("card.soldier.heavy-warrior"), "重装战士", "高耐久近战 · 昂贵前排", 1, true, 0, 5),
            new(new CardId("card.soldier.mage"), "法师", "火球范围伤害 · 魔法后排", 1, true, 0, 5),
            new(new CardId("card.soldier.longbow"), "长弓兵", "超远射程 · 脆弱输出", 1, true, 0, 5),
            new(new CardId("card.soldier.cannon"), "炮车", "炮弹范围伤害 · 攻墙强化", 1, true, 0, 5),
            new(new CardId("card.building.sawmill"), "伐木场", "加工木板 · 提升物流", 1, true, 0, 5),
            new(new CardId("card.building.gatherer-lodge"), "食物营地", "付费派出食物采集者", 1, true, 0, 5),
            new(new CardId("card.building.wood-gatherer-camp"), "伐木营地", "付费派出木材采集者", 1, true, 0, 5),
            new(new CardId("card.building.stone-gatherer-camp"), "石矿营地", "付费派出石料采集者", 1, true, 0, 5),
            new(new CardId("card.building.iron-gatherer-camp"), "铁矿营地", "付费派出铁矿采集者", 1, true, 0, 5),
            new(new CardId("card.building.shield-camp"), "盾卫营地", "激活盾卫训练", 1, true, 0, 5),
            new(new CardId("card.building.research-lab"), "研究院", "三类本局研究", 0, false, 0, 5),
            new(new CardId("card.battlefield.arrow-tower"), "箭塔", "战场工程 · 持续压制", 0, false, 0, 5),
            new(new CardId("card.tactic.arrow-rain"), "箭雨", "一次性范围打击", 0, false, 0, 5)
        };
        private SelectionCategory _category;
        private CardId _selectedCardId = new("card.soldier.shield-guard");
        private BattlefieldId _battlefieldId;
        private MapModeId _modeId;
        private int _cardPage;

        public SelectionPrototypeProvider(IApplicationFlow applicationFlow, IProgressionReader progression = null,
            IProgressionCommands progressionCommands = null, ISelectionContent content = null,
            ISettingsOverlay settingsOverlay = null)
        {
            _applicationFlow = applicationFlow ?? throw new ArgumentNullException(nameof(applicationFlow));
            _progression = progression;
            _progressionCommands = progressionCommands;
            _content = content;
            _settingsOverlay = settingsOverlay;
            var battlefield = Battlefields[0];
            _battlefieldId = battlefield.Id;
            _modeId = battlefield.ModeIds[0];
        }
        public event Action<SelectionViewModel> Changed;
        public SelectionViewModel Snapshot
        {
            get
            {
                var progression = _progression?.GetSnapshot();
                var battlefields = Battlefields;
                var battlefield = battlefields.First(value => value.Id.Equals(_battlefieldId));
                var unlocked = progression == null || progression.UnlockedBattlefields.Contains(_battlefieldId);
                var allCards = FilterCards();
                var pageCount = Math.Max(1, (allCards.Count + 7) / 8);
                _cardPage = Math.Clamp(_cardPage, 0, pageCount - 1);
                var pageCards = allCards.Skip(_cardPage * 8).Take(8).ToArray();
                return new SelectionViewModel(progression?.Gold ?? 1280,
                    battlefields.ToList().FindIndex(value => value.Id.Equals(_battlefieldId)) + 1,
                    battlefields.Count, _category, pageCards, _selectedCardId, _battlefieldId,
                    battlefield.DisplayName, unlocked, _modeId, battlefield.ModeIds,
                    battlefield.MapArt, _cardPage, pageCount);
            }
        }

        private IReadOnlyList<SelectionBattlefieldDefinition> Battlefields =>
            _content?.Battlefields is { Count: > 0 } values ? values : new[] { FallbackBattlefield };

        public void SelectCategory(SelectionCategory category)
        {
            _category = category;
            _cardPage = 0;
            var cards = FilterCards();
            if (cards.Count > 0 && cards.All(card => !card.Id.Equals(_selectedCardId))) _selectedCardId = cards[0].Id;
            Changed?.Invoke(Snapshot);
        }

        public void SelectCard(CardId cardId)
        {
            if (_catalogCards.Any(card => card.Id.Equals(cardId))) _selectedCardId = cardId;
            Changed?.Invoke(Snapshot);
        }

        public void SelectMode(MapModeId modeId)
        {
            var battlefield = Battlefields.First(value => value.Id.Equals(_battlefieldId));
            if (!battlefield.ModeIds.Contains(modeId)) return;
            _modeId = modeId;
            Changed?.Invoke(Snapshot);
        }

        public void SelectBattlefield(BattlefieldId battlefieldId)
        {
            var battlefield = Battlefields.FirstOrDefault(value => value.Id.Equals(battlefieldId));
            if (battlefield == null) return;
            _battlefieldId = battlefield.Id;
            _modeId = battlefield.ModeIds[0];
            Changed?.Invoke(Snapshot);
        }

        public void CycleBattlefield(int direction)
        {
            var battlefields = Battlefields;
            var current = battlefields.ToList().FindIndex(value => value.Id.Equals(_battlefieldId));
            var next = (current + (direction < 0 ? -1 : 1) + battlefields.Count) % battlefields.Count;
            SelectBattlefield(battlefields[next].Id);
        }

        public void CycleCardPage(int direction)
        {
            var cards = FilterCards();
            var pageCount = Math.Max(1, (cards.Count + 7) / 8);
            _cardPage = (_cardPage + (direction < 0 ? -1 : 1) + pageCount) % pageCount;
            var page = cards.Skip(_cardPage * 8).Take(8).ToArray();
            if (page.Length > 0) _selectedCardId = page[0].Id;
            Changed?.Invoke(Snapshot);
        }

        public Task StartMatchAsync(CancellationToken cancellationToken)
        {
            if (!Snapshot.BattlefieldUnlocked)
                throw new InvalidOperationException($"Battlefield '{_battlefieldId}' is locked.");
            var now = DateTime.UtcNow;
            var matchId = new MatchId($"prototype-{now:yyyyMMddHHmmssfff}");
            var seed = unchecked((int)(now.Ticks ^ (now.Ticks >> 32)));
            return _applicationFlow.StartMatchAsync(new MatchLaunchRequest(_battlefieldId, _modeId, matchId, seed), cancellationToken);
        }

        public Task OpenSettingsAsync(CancellationToken cancellationToken) =>
            _settingsOverlay?.OpenSettingsAsync(cancellationToken) ?? Task.CompletedTask;

        public async Task UnlockSelectedCardAsync(CancellationToken cancellationToken)
        {
            if (_progressionCommands == null) return;
            await _progressionCommands.UnlockCardAsync(_selectedCardId, cancellationToken);
            Changed?.Invoke(Snapshot);
        }

        public async Task UpgradeSelectedCardAsync(CancellationToken cancellationToken)
        {
            if (_progressionCommands == null) return;
            await _progressionCommands.UpgradeCardAsync(_selectedCardId, cancellationToken);
            Changed?.Invoke(Snapshot);
        }

        private IReadOnlyList<SelectionCardViewModel> FilterCards()
        {
            var progression = _progression?.GetSnapshot();
            var states = progression?.Cards.ToDictionary(value => value.Id) ?? new Dictionary<CardId, CardProgressSnapshot>();
            var cards = _catalogCards.Select(card =>
            {
                var artKey = _content != null && _content.CardArt.TryGetValue(card.Id, out var configuredArt)
                    ? configuredArt
                    : card.ArtKey;
                return states.TryGetValue(card.Id, out var state)
                    ? new SelectionCardViewModel(card.Id, card.Name, card.Subtitle, state.Level, state.Unlocked,
                        state.Level, card.ProgressMax, artKey)
                    : new SelectionCardViewModel(card.Id, card.Name, card.Subtitle, card.Level, card.Unlocked,
                        card.Progress, card.ProgressMax, artKey);
            }).ToArray();
            if (_category == SelectionCategory.All) return cards;
            return cards.Where(card => _category switch
            {
                SelectionCategory.Soldiers => card.Id.Value.Contains("soldier", StringComparison.Ordinal),
                SelectionCategory.Camps => card.Id.Value.Contains("building", StringComparison.Ordinal),
                _ => card.Id.Value.Contains("tactic", StringComparison.Ordinal) || card.Id.Value.Contains("battlefield", StringComparison.Ordinal)
            }).ToArray();
        }
    }
}
