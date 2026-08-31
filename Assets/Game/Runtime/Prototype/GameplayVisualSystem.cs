using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using FortressFrontier.Core.AI;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Flow;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.UI;
using FortressFrontier.Runtime.Monetization;

namespace FortressFrontier.Runtime.Prototype
{
    public sealed class GameplayVisualSystem : GameSystemBase, IGameplayCommands
    {
        public static readonly PanelKey PanelId = new("ui.gameplay");
        public static readonly PanelKey ResultPanelId = new("ui.result");
        private readonly IPanelService _panels;
        private readonly IApplicationFlow _applicationFlow;
        private readonly EconomySystem _economy;
        private readonly BuildingSystem _buildings;
        private readonly CampSystem _camps;
        private readonly TrainingSystem _training;
        private readonly HandAndOfferSystem _hand;
        private readonly ResourceNodeSystem _resourceNodes;
        private readonly GathererSystem _playerGatherers;
        private readonly GathererSystem _enemyGatherers;
        private readonly CombatSystem _combat;
        private readonly FixedSimulationSystem _simulation;
        private readonly EnemyEconomySystem _enemyEconomy;
        private readonly AiStrategySystem _aiStrategy;
        private readonly TowerConstructionSystem _playerConstruction;
        private readonly TowerConstructionSystem _enemyConstruction;
        private readonly ResearchSystem _research;
        private readonly BossSystem _boss;
        private readonly MatchAnalyticsSystem _analytics;
        private readonly MatchSettlementSystem _settlement;
        private readonly MatchConfigSnapshot _config;
        private readonly MatchPresentationConfig _presentation;
        private readonly IGameplaySpriteResolver _sprites;
        private readonly RewardedAdSystem _rewardedAds;
        private IGameplayView _view;
        private CancellationTokenSource _lifetime;
        private GameplayCardTab _tab;
        private CardId? _selectedCardId;
        private RewardChoiceId? _pendingReplacementChoiceId;
        private bool _buildingMenuOpen;
        private int _selectedBuildingSlotIndex = -1;
        private BlueprintVisualState _blueprintState;
        private bool _researchOpen;
        private int _soldierPage;
        private string _status = "采集者会执行一次采集后回收；选择建筑牌可查看跟随指针的蓝图。";

        public GameplayVisualSystem(IPanelService panels, IApplicationFlow applicationFlow,
            EconomySystem economy, BuildingSystem buildings, CampSystem camps, TrainingSystem training,
            HandAndOfferSystem hand, ResourceNodeSystem resourceNodes, GathererSystem playerGatherers,
            GathererSystem enemyGatherers, CombatSystem combat, FixedSimulationSystem simulation,
            EnemyEconomySystem enemyEconomy, AiStrategySystem aiStrategy,
            TowerConstructionSystem playerConstruction, TowerConstructionSystem enemyConstruction,
            ResearchSystem research, BossSystem boss,
            MatchAnalyticsSystem analytics, MatchSettlementSystem settlement, MatchConfigSnapshot config,
            MatchPresentationConfig presentation,
            IGameplaySpriteResolver sprites, RewardedAdSystem rewardedAds = null) : base(SystemLifetime.Scene)
        {
            _panels = panels;
            _applicationFlow = applicationFlow;
            _economy = economy;
            _buildings = buildings;
            _camps = camps;
            _training = training;
            _hand = hand;
            _resourceNodes = resourceNodes;
            _playerGatherers = playerGatherers;
            _enemyGatherers = enemyGatherers;
            _combat = combat;
            _simulation = simulation;
            _enemyEconomy = enemyEconomy; _aiStrategy = aiStrategy;
            _playerConstruction = playerConstruction; _enemyConstruction = enemyConstruction;
            _research = research; _boss = boss;
            _analytics = analytics ?? throw new ArgumentNullException(nameof(analytics));
            _settlement = settlement;
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));
            _sprites = sprites ?? throw new ArgumentNullException(nameof(sprites));
            _rewardedAds = rewardedAds;
        }

        protected override async Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _view = await _panels.OpenViewAsync<IGameplayView>(PanelId, null, cancellationToken);
            _buildings.Changed += Refresh;
            _camps.Changed += Refresh;
            _training.Changed += Refresh;
            _hand.Changed += Refresh;
            _resourceNodes.Changed += Refresh;
            _playerGatherers.Changed += Refresh;
            _enemyGatherers.Changed += Refresh;
            _combat.Changed += Refresh;
            _research.Changed += Refresh;
            _combat.MatchEnded += OnMatchEnded;
            _view.Bind(this, _sprites, BuildViewModel());
        }

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _buildings.Changed -= Refresh;
            _camps.Changed -= Refresh;
            _training.Changed -= Refresh;
            _hand.Changed -= Refresh;
            _resourceNodes.Changed -= Refresh;
            _playerGatherers.Changed -= Refresh;
            _enemyGatherers.Changed -= Refresh;
            _combat.Changed -= Refresh;
            _research.Changed -= Refresh;
            _combat.MatchEnded -= OnMatchEnded;
            _lifetime?.Cancel();
            _lifetime?.Dispose();
            _lifetime = null;
            _view = null;
            await _panels.CloseAsync(ResultPanelId, cancellationToken);
            await _panels.CloseAsync(PanelId, cancellationToken);
        }

        public void SelectTab(GameplayCardTab tab)
        {
            _tab = tab;
            if (tab != GameplayCardTab.Soldiers) _training.CancelSelection();
            _selectedCardId = null;
            _buildingMenuOpen = false;
            _selectedBuildingSlotIndex = -1;
            _status = tab == GameplayCardTab.Soldiers ? "选择兵种与数量，再点击浅蓝部署区。" : "道具卡：建筑牌占用九格，工具牌使用后消耗。";
            Refresh();
        }

        public void SelectCard(CardId cardId)
        {
            if (_pendingReplacementChoiceId.HasValue)
            {
                var succeeded = _hand.TryReplaceAndChoose(_pendingReplacementChoiceId.Value, cardId);
                _status = succeeded ? "已替换旧道具并领取奖励。" : "替换失败，请选择一张现有道具牌。";
                if (succeeded) _pendingReplacementChoiceId = null;
                Refresh();
                return;
            }
            _selectedCardId = cardId;
            Refresh();
        }

        public void CancelSelection()
        {
            if (_pendingReplacementChoiceId.HasValue)
            {
                _pendingReplacementChoiceId = null;
                _status = "已取消替换，返回奖励四选一。";
                Refresh();
                return;
            }
            _training.CancelSelection();
            _selectedCardId = null;
            _status = "已取消当前选择。";
            Refresh();
        }

        public void UpdateSoldierSelection(UnitId unitId, int count)
        {
            var failure = _training.UpdateSelection(unitId, count);
            _status = failure == TrainingFailure.None
                ? count > 0 ? $"已选择 {DisplayUnit(unitId)} ×{count}，点击浅蓝区域确认落点。" : $"已移除 {DisplayUnit(unitId)}。"
                : $"选择失败：{TrainingFailureText(failure)}";
            Refresh();
        }

        public void SubmitDeployment(int worldX, int worldY)
        {
            if (_selectedCardId.HasValue && _hand.TryDeployReinforcement(_selectedCardId.Value, _training, worldX, worldY))
            {
                _status = "援军已部署。";
                _selectedCardId = null;
                Refresh();
                return;
            }
            var failure = _training.SubmitSelection(worldX, worldY, out var orderIds);
            if (failure == TrainingFailure.None)
            {
                _selectedCardId = null;
                _status = $"已提交 {orderIds.Count} 个兵种订单，士兵将逐个在预览位置出征。";
            }
            else _status = $"部署失败：{TrainingFailureText(failure)}";
            Refresh();
        }

        public void PlayBuilding(CardId cardId, int slotIndex)
        {
            var succeeded = _hand.TryPlayBuilding(cardId, slotIndex);
            if (succeeded) _selectedCardId = null;
            _status = succeeded ? $"建筑牌 {DisplayCard(cardId)} 已部署到第 {slotIndex + 1} 格。" : "建筑牌无法部署：卡牌、槽位或配置无效。";
            Refresh();
        }

        public void PlaceTower(CardId cardId)
        {
            if (cardId.Value != "card.battlefield.arrow-tower") { _status = "该战场道具无法作为箭塔工程使用。"; Refresh(); return; }
            var area = _training.PlayerDeploymentArea;
            var failure = _playerConstruction.TryStartSite(area.X + area.Width, area.Y + area.Height / 2, out _);
            _status = failure == TowerConstructionFailure.None ? "箭塔工地已建立，建筑工正在前往。" : $"箭塔工程失败：{failure}";
            Refresh();
        }

        public void UseTactic(CardId cardId)
        {
            if (!_hand.TryConsumeTactic(cardId, out var effect))
            { _status = "工具牌无法使用：手牌不存在或资源已满。"; Refresh(); return; }
            if (effect.Kind == TacticEffectKind.AreaDamage)
            {
                var hits = _combat.ApplyAreaDamage(MatchFaction.Enemy, effect.Magnitude);
                _status = $"箭雨命中 {hits} 个敌军。";
            }
            else _status = $"{DisplayCard(cardId)} 已生效。";
            Refresh();
        }

        public void ChooseOffer(RewardChoiceId choiceId)
        {
            var selectedChoice = _hand.GetOffer().Choices.FirstOrDefault(value => value.Id.Equals(choiceId));
            if (_hand.ChooseOffer(choiceId))
            {
                if (selectedChoice?.Kind == RewardChoiceKind.ReinforcementItem) _tab = GameplayCardTab.Items;
                _status = "奖励已领取。";
            }
            else
            {
                var choice = selectedChoice;
                if (choice != null && choice.Kind != RewardChoiceKind.ProcessedResourceBundle &&
                    _hand.TotalCount >= _config.HandAndOffers.HandLimit)
                {
                    _pendingReplacementChoiceId = choiceId;
                    _tab = GameplayCardTab.Items;
                    _status = "手牌已满：请选择一张旧道具牌替换，或取消返回四选一。";
                }
                else _status = "领取失败：候选已过期或资源容量不足。";
            }
            Refresh();
        }

        public void ToggleBuildingMenu()
        {
            if (_buildingMenuOpen) HideBuildingMenu();
            else
            {
                var first = _buildings.GetSnapshot().FirstOrDefault(value => value.BuildingId.HasValue);
                if (first != null) ShowBuildingMenu(first.SlotIndex);
            }
        }

        public void ShowBuildingMenu(int slotIndex)
        {
            var slots = _buildings.GetSnapshot();
            if (slotIndex < 0 || slotIndex >= slots.Count || !slots[slotIndex].BuildingId.HasValue)
            { HideBuildingMenu(); return; }
            _selectedBuildingSlotIndex = slotIndex;
            _buildingMenuOpen = true;
            Refresh();
        }

        public void HideBuildingMenu()
        {
            if (!_buildingMenuOpen && _selectedBuildingSlotIndex < 0) return;
            _buildingMenuOpen = false;
            _selectedBuildingSlotIndex = -1;
            Refresh();
        }

        public bool ExecuteBuildingCommand(GameplayBuildingCommand command)
        {
            var slots = _buildings.GetSnapshot();
            if (_selectedBuildingSlotIndex < 0 || _selectedBuildingSlotIndex >= slots.Count ||
                !slots[_selectedBuildingSlotIndex].BuildingId.HasValue)
            { _status = "鼠标移入一栋建筑后再执行管理命令。"; HideBuildingMenu(); return false; }
            var selected = slots[_selectedBuildingSlotIndex];
            var succeeded = command switch
            {
                GameplayBuildingCommand.ResumeAfterResourceShortage =>
                    _buildings.TryResumeAfterResourceShortage(selected.InstanceId),
                GameplayBuildingCommand.Demolish => _buildings.Demolish(selected.InstanceId),
                GameplayBuildingCommand.Upgrade => _buildings.TryStartUpgrade(selected.InstanceId),
                _ => false
            };
            _status = command == GameplayBuildingCommand.ResumeAfterResourceShortage
                ? succeeded ? "建筑已继续运行。" : "建筑当前不处于可继续的资源短缺状态。"
                : succeeded ? $"建筑命令 {command} 已执行。" : $"建筑命令 {command} 当前不可执行。";
            if (command == GameplayBuildingCommand.Demolish && succeeded)
            { _buildingMenuOpen = false; _selectedBuildingSlotIndex = -1; }
            Refresh();
            return succeeded;
        }

        public void CycleBlueprintState()
        { _blueprintState = (BlueprintVisualState)(((int)_blueprintState + 1) % 3); Refresh(); }
        public void ToggleResearch()
        {
            _researchOpen = !_researchOpen;
            Refresh();
        }
        public void CycleSoldierPage(int direction)
        {
            var activeCount = _config.Combat.Units.Count(value => value.CanAttack &&
                _buildings.GetSnapshot().Any(slot => slot.BuildingId.HasValue &&
                    _config.Buildings.Any(building => building.Id.Equals(slot.BuildingId.Value) &&
                        building.ActivatedSoldierCardId.HasValue && building.ActivatedSoldierCardId.Value.Equals(value.SoldierCardId))));
            var pageCount = Math.Max(1, (activeCount + 3) / 4);
            _soldierPage = (_soldierPage + (direction < 0 ? -1 : 1) + pageCount) % pageCount;
            Refresh();
        }
        public void StartResearch(ResearchUpgradeId upgradeId)
        {
            var failure = _research.TryStart(upgradeId);
            _status = failure == ResearchFailure.None ? $"已开始 {upgradeId.Value} 研究。" : $"研究失败：{failure}";
            Refresh();
        }

        public Task ShowResultAsync(CancellationToken cancellationToken)
        {
            if (!_combat.HasEnded)
            { _status = "对局尚未决出胜负；结算只由城墙归零触发。"; Refresh(); return Task.CompletedTask; }
            return CompleteMatchAsync(_combat.PlayerVictory, cancellationToken);
        }

        private void OnMatchEnded(bool victory)
        {
            var token = _lifetime?.Token ?? CancellationToken.None;
            _ = CompleteMatchAsync(victory, token);
        }

        private async Task CompleteMatchAsync(bool victory, CancellationToken cancellationToken)
        {
            try
            {
                var receipt = await _settlement.SettleAsync(true, victory, cancellationToken);
                await OpenResultAsync(receipt, victory, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception exception)
            {
                _status = "结算失败：" + exception.Message;
                Refresh();
            }
        }

        private async Task RetrySettlementAsync(CancellationToken cancellationToken)
        {
            var receipt = await _settlement.SettleAsync(true, _combat.PlayerVictory, cancellationToken);
            await OpenResultAsync(receipt, _combat.PlayerVictory, cancellationToken);
        }

        private Task OpenResultAsync(SettlementReceipt receipt, bool victory, CancellationToken cancellationToken)
        {
            var arguments = receipt.Succeeded
                ? new ResultPanelArguments(victory ? "敌方城墙已攻破" : "我方城墙已失守",
                    MatchResultReportFormatter.Format(_config, receipt, victory, _analytics.Capture(victory)) +
                    FormatEnemyTimeline(), true,
                    _applicationFlow.ReturnToSelectionAsync, null,
                    _rewardedAds?.CreateOffer(receipt),
                    _rewardedAds == null ? null : token => _rewardedAds.WatchAndClaimAsync(receipt, token))
                : new ResultPanelArguments("保存失败", "奖励尚未到账，请重试保存。", false,
                    _applicationFlow.ReturnToSelectionAsync, RetrySettlementAsync);
            return _panels.OpenAsync(ResultPanelId, arguments, cancellationToken);
        }

        private GameplayViewModel BuildViewModel()
        {
            var balances = _economy.GetSnapshot().ToDictionary(value => value.Id.Value, StringComparer.Ordinal);
            string Resource(string id, string label) => balances.TryGetValue(id, out var value) ? $"{label} {value.Amount}/{value.Capacity}" : string.Empty;
            var groups = new[]
            {
                $"{Resource("resource.food", "食物")}  {Resource("resource.wine", "酒")}",
                $"{Resource("resource.wood", "木材")}  {Resource("resource.plank", "木板")}",
                $"{Resource("resource.raw-stone", "原石")}  {Resource("resource.stone", "石料")}",
                $"{Resource("resource.iron-ore", "铁矿")}  {Resource("resource.iron-ingot", "铁锭")}" 
            };
            var slots = _buildings.GetSnapshot();
            var slotModels = slots.Select(value => new BuildingSlotViewModel(value.BuildingId?.Value, value.Level,
                value.UpgradeState, value.BlockReason, value.BuildingId.HasValue
                    ? _presentation.GetBuildingArt(value.BuildingId.Value) : default, value.Paused,
                value.UpgradeProgressMilli)).ToArray();
            var soldiers = new List<GameplayCardViewModel>();
            AddSoldier(soldiers, "unit.shield-guard", "card.soldier.shield-guard", "盾卫", "building.shield-camp");
            AddSoldier(soldiers, "unit.archer", "card.soldier.archer", "弓手", "building.archer-camp");
            AddSoldier(soldiers, "unit.siege-ram", "card.soldier.siege-ram", "破城槌", "building.ram-camp");
            AddSoldier(soldiers, "unit.heavy-warrior", "card.soldier.heavy-warrior", "重装战士", "building.heavy-warrior-camp");
            AddSoldier(soldiers, "unit.mage", "card.soldier.mage", "法师", "building.mage-camp");
            AddSoldier(soldiers, "unit.longbow", "card.soldier.longbow", "长弓兵", "building.longbow-camp");
            AddSoldier(soldiers, "unit.cannon", "card.soldier.cannon", "炮车", "building.cannon-camp");
            var soldierPageCount = Math.Max(1, (soldiers.Count + 3) / 4);
            _soldierPage = Math.Clamp(_soldierPage, 0, soldierPageCount - 1);
            var visibleSoldiers = soldiers.Skip(_soldierPage * 4).Take(4).ToArray();
            var itemCards = _hand.GetHand().Select(value =>
            {
                var card = CardView(value.Id, value.Type, value.Count);
                if (!value.ReinforcementTemplateId.HasValue) return card;
                var template = _config.HandAndOffers.ReinforcementTemplates.Single(config =>
                    config.Id.Equals(value.ReinforcementTemplateId.Value));
                var units = ReinforcementUnits(value.ReinforcementUnits);
                var composition = FormatReinforcement(value.ReinforcementUnits);
                return new GameplayCardViewModel(card.Id, CardType.ReinforcementItem, template.DisplayName,
                    $"{composition} · 合法部署成功后消耗", card.Count, enabled: true, artKey: card.ArtKey,
                    cost: "合法部署成功后消耗 1 张", attributes: composition,
                    reinforcementTemplateId: value.ReinforcementTemplateId, reinforcementUnits: units);
            }).ToArray();
            var offer = _hand.GetOffer();
            var offerView = new GameplayOfferViewModel(offer.Active, offer.Choices.Select(value =>
                new GameplayRewardChoiceViewModel(value.Id, value.Kind,
                    value.Kind == RewardChoiceKind.ContentCard && value.CardId.HasValue
                        ? DisplayCard(value.CardId.Value)
                        : value.DisplayName, value.Kind switch
                {
                    RewardChoiceKind.ContentCard => "建筑牌 · 领取后进入道具手牌",
                    RewardChoiceKind.ProcessedResourceBundle => string.Join(" + ", value.Resources.Select(amount => $"{ResourceLabel(amount.ResourceId)} ×{amount.Amount}")),
                    RewardChoiceKind.ReinforcementItem => string.Join(" + ", value.Units.GroupBy(id => id).Select(group => $"{UnitLabel(group.Key)} ×{group.Count()}")),
                    _ => string.Empty
                }, value.Kind == RewardChoiceKind.ReinforcementItem ? ReinforcementUnits(value.Units) : null,
                    value.Rarity, value.Kind switch
                    {
                        RewardChoiceKind.ContentCard => _config.HandAndOffers.BuildingRewardArt,
                        RewardChoiceKind.ProcessedResourceBundle => _config.HandAndOffers.ResourceRewardArt,
                        RewardChoiceKind.ReinforcementItem => _config.HandAndOffers.ReinforcementRewardArt,
                        _ => default
                    })).ToArray(), _pendingReplacementChoiceId.HasValue);
            var gatherers = _playerGatherers.GetSnapshot().Concat(_enemyGatherers.GetSnapshot()).ToArray();
            var selection = _training.GetSelectionSnapshot();
            var enemyBalances = _enemyEconomy.GetSnapshot();
            var enemyGathererSnapshots = _enemyGatherers.GetSnapshot();
            var gatheringStatus = enemyGathererSnapshots.Count == 0
                ? "补员中"
                : enemyGathererSnapshots.All(value => value.State == GathererState.Outbound &&
                                                      value.TargetNodeId.Value == null)
                    ? "受阻"
                    : "运转";
            var currentPhase = _config.Phases.LastOrDefault(value => value.StartTick <= _simulation.TickCount);
            var accelerationStatus = currentPhase != null &&
                                     _simulation.TickCount >= currentPhase.PublicAccelerationStartTick
                ? $"双方加工 ×{currentPhase.PublicProductionMultiplierMilli / 1000m:0.#}"
                : "常规加工";
            var visibleThreat = _aiStrategy.GetVisibleRouteThreats().FirstOrDefault();
            var emergencyQueued = _aiStrategy.GetDecisions().TakeLast(8).Any(value =>
                value.DefenseTriggerKind == AiDefenseTriggerKind.LogisticsDefense &&
                value.Result.StartsWith("train:", StringComparison.Ordinal));
            var logisticsAlert = visibleThreat != null
                ? $"后勤遭袭 {visibleThreat.RouteId.Value.Replace("route.", string.Empty)}"
                : emergencyQueued ? "紧急补防" : "后勤正常";
            var logistics = $"敌方后勤：节奏 {TempoLabel()} · 采集线 {gatheringStatus} · {accelerationStatus} · " +
                $"塔 {_enemyConstruction.GetTowers().Count}/{_enemyConstruction.GetSites().Count} · " +
                $"我方研究完成 {_research.GetSnapshot().CompletedRanks} · " +
                $"Boss {_boss.GetSnapshot().Count(value => value.State is BossRuntimeState.Warning or BossRuntimeState.Active or BossRuntimeState.RewardCore)}";
            var researchSnapshot = _research.GetSnapshot();
            var researchReason = !researchSnapshot.LabAvailable
                ? "需要正常运转的研究院"
                : researchSnapshot.Active ? "研究进行中" : researchSnapshot.Candidates.Count == 0
                    ? researchSnapshot.CompletedRanks >= 24 ? "所有研究已满级" : "先建造并激活对应兵种营地"
                    : string.Empty;
            var reinforcementSelected = _selectedCardId.HasValue && itemCards.Any(value =>
                value.Id.Equals(_selectedCardId.Value) && value.Type == CardType.ReinforcementItem);
            return new GameplayViewModel(_tab, -1, _buildingMenuOpen,
                (_tab == GameplayCardTab.Soldiers && selection.TotalCount > 0) || reinforcementSelected,
                _blueprintState, offer.Active, _researchOpen, _hand.TotalCount, _status, groups, slotModels,
                _combat.GetUnits().Count, _selectedCardId, visibleSoldiers, itemCards, _resourceNodes.GetSnapshot(),
                gatherers, _combat.GetUnits(), _combat.GetWalls(),
                new MatchClockViewModel(_simulation.TickCount / ContentConstants.FixedTicksPerSecond, 0), offerView,
                selection, _training.GetDeploymentSlots(), _training.PlayerDeploymentArea, string.Empty,
                $"敌方情报  阶段 {PhaseLabel()}  节奏 {TempoLabel()}\n" +
                $"采集线 {gatheringStatus}  {accelerationStatus}\n{logisticsAlert}",
                _buildingMenuOpen ? _selectedBuildingSlotIndex : -1, _presentation.MapArt,
                _soldierPage, soldierPageCount, researchSnapshot, FormatAmounts(_config.Research.Costs), researchReason);
        }

        private string TempoLabel() => _aiStrategy.CurrentTempoState switch
        {
            AiTempoState.Recovering => "恢复",
            AiTempoState.PressureDue => "压力将至",
            _ => "集结"
        };

        
private string PhaseLabel() => _config.Phases.LastOrDefault(value => value.StartTick <= _simulation.TickCount)?.Id.Value.Replace("phase.", string.Empty) ?? "development";
        private string LatestRoute() => _aiStrategy.GetDecisions().LastOrDefault()?.RouteId.Replace("route.", string.Empty) ?? "待定";
        private string FormatEnemyTimeline()
        {
            var lines = _aiStrategy.GetDecisions().TakeLast(20).Select(value =>
                $"{value.Tick / ContentConstants.FixedTicksPerSecond,3}s  {value.PhaseId.Replace("phase.", string.Empty)} / {value.IntentId.Replace("intent.", string.Empty)} / {value.RouteId.Replace("route.", string.Empty)} / {value.Result}");
            var ledger = _enemyEconomy.GetLedger().TakeLast(20).Select(value =>
                $"{value.Tick / ContentConstants.FixedTicksPerSecond,3}s  {value.IntentId.Replace("intent.", string.Empty)}  {value.ResourceId.Value.Replace("resource.", string.Empty)} {value.Amount:+#;-#;0}  {value.SourceId}");
            var threats = _combat.GetGathererThreatIncidents().TakeLast(20).Select(value =>
                $"{value.Tick / ContentConstants.FixedTicksPerSecond,3}s  后勤威胁 {value.RouteId.Value.Replace("route.", string.Empty)}  伤害 {value.Damage}  货损 {value.LostCarriedAmount}  {(value.WasKilled ? "采集者阵亡" : "受击")}");
            return "\n\n敌方 AI 决策时间线\n" + string.Join("\n", lines) +
                   "\n\n敌方后勤事件\n" + string.Join("\n", threats) +
                   "\n\n敌方经济账本\n" + string.Join("\n", ledger);
        }

        private static CardType ResolveCardType(CardId id) => id.Value.StartsWith("card.tactic.", StringComparison.Ordinal)
            ? CardType.Tactic : id.Value.StartsWith("card.soldier.", StringComparison.Ordinal)
                ? CardType.Soldier : id.Value.StartsWith("card.battlefield.", StringComparison.Ordinal)
                    ? CardType.BattlefieldItem : CardType.BuildingItem;

        private void AddSoldier(ICollection<GameplayCardViewModel> cards, string unitId, string cardId,
            string name, string campId)
        {
            if (_buildings.GetSnapshot().Any(value => value.BuildingId?.Value == campId))
            {
                var id = new UnitId(unitId);
                var count = _training.GetSelectionSnapshot().Quantities.TryGetValue(id, out var selected) ? selected : 0;
                var config = _config.Combat.Units.Single(value => value.Id.Equals(id));
                var cost = FormatAmounts(config.TrainingCosts);
                var attributes = $"生命 {config.MaxHealth} · 攻击 {config.AttackDamage} · 训练 {FormatSeconds(config.TrainingTicks)}";
                cards.Add(new GameplayCardViewModel(new CardId(cardId), CardType.Soldier, name,
                    $"{cost} · {attributes}", count, id, true, _presentation.GetUnit(id).Sprite,
                    cost, attributes));
            }
        }

        private GameplayCardViewModel CardView(CardId id, CardType type, int count)
        {
            var cost = "1 张手牌";
            string attributes;
            if (type == CardType.BuildingItem)
            {
                var building = _config.Buildings.FirstOrDefault(value => value.SourceCardId.Equals(id));
                attributes = building == null ? "部署至城内九格" : BuildingAttributes(building);
            }
            else if (type == CardType.BattlefieldItem)
            {
                var constructionCost = FormatAmounts(_config.Construction.Costs);
                attributes = string.IsNullOrWhiteSpace(constructionCost) ? "建立箭塔工地" : $"工程 {constructionCost} · 建立箭塔工地";
            }
            else if (type == CardType.ReinforcementItem)
            {
                attributes = "合法部署成功后消耗";
            }
            else
            {
                attributes = id.Value switch
                {
                    "card.tactic.field-rations" => "立即补充食物",
                    "card.tactic.emergency-supplies" => "立即补充木板与石料",
                    "card.tactic.arrow-rain" => "对敌军造成范围伤害",
                    _ => "一次性战术效果"
                };
            }
            return new GameplayCardViewModel(id, type, DisplayCard(id), $"{cost} · {attributes}", count,
                artKey: _presentation.GetCardArt(id), cost: cost, attributes: attributes);
        }

        private IReadOnlyList<ReinforcementUnitViewModel> ReinforcementUnits(IEnumerable<UnitId> units) => units
            .GroupBy(value => value)
            .OrderBy(group => group.Key.Value, StringComparer.Ordinal)
            .Select(group => new ReinforcementUnitViewModel(_presentation.GetUnit(group.Key).Sprite, group.Count()))
            .ToArray();

        private static string FormatReinforcement(IEnumerable<UnitId> units) => string.Join(" + ", units
            .GroupBy(value => value)
            .OrderBy(group => group.Key.Value, StringComparer.Ordinal)
            .Select(group => $"{UnitLabel(group.Key)} ×{group.Count()}"));

        private static string UnitLabel(UnitId id) => id.Value switch
        {
            "unit.shield-guard" => "盾兵", "unit.archer" => "弓箭手", "unit.siege-ram" => "破城槌",
            "unit.heavy-warrior" => "重装战士", "unit.mage" => "法师", "unit.longbow" => "长弓兵",
            "unit.cannon" => "炮车", _ => id.Value.Replace("unit.", string.Empty)
        };

        private static string BuildingAttributes(MatchBuildingConfig building)
        {
            if (building.ActivatedSoldierCardId.HasValue) return "激活对应兵种卡 · 支持逐兵训练";
            var input = FormatAmounts(building.Inputs);
            var output = FormatAmounts(building.Outputs);
            if (!string.IsNullOrWhiteSpace(output))
                return string.IsNullOrWhiteSpace(input) ? $"产出 {output}" : $"{input} → {output} · 周期 {FormatSeconds(building.ProductionCycleTicks)}";
            return building.Category == BuildingCategory.Storage ? "提高局内资源容量" : "提供局内功能";
        }

        private static string FormatAmounts(IReadOnlyList<ResourceAmount> amounts) => amounts == null || amounts.Count == 0
            ? string.Empty
            : string.Join(" + ", amounts.Select(value => $"{ResourceLabel(value.ResourceId)} {value.Amount}"));

        private static string ResourceLabel(ResourceId id) => id.Value switch
        {
            "resource.food" => "食物", "resource.wine" => "酒", "resource.wood" => "木材",
            "resource.plank" => "木板", "resource.raw-stone" => "原石", "resource.stone" => "石料",
            "resource.iron-ore" => "铁矿", "resource.iron-ingot" => "铁锭", _ => id.Value.Replace("resource.", string.Empty)
        };

        private static string FormatSeconds(int ticks) => $"{ticks / (float)ContentConstants.FixedTicksPerSecond:0.#}秒";
        private static string DisplayUnit(UnitId id) => id.Value == "unit.archer" ? "弓手" : "盾卫";
        private static string DisplayCard(CardId id) => id.Value switch
        {
            "card.building.pasture" => "牧场", "card.building.winery" => "酿酒厂",
            "card.building.sawmill" => "锯木厂", "card.building.stoneworks" => "石料工坊",
            "card.building.iron-smelter" => "冶炼厂", "card.building.warehouse" => "仓库",
            "card.building.shield-camp" => "盾卫营地", "card.building.archer-camp" => "弓手营地",
            "card.building.ram-camp" => "攻城槌营地", "card.building.research-lab" => "研究院",
            "card.building.heavy-warrior-camp" => "重装战士营地", "card.building.mage-camp" => "法师营地",
            "card.building.longbow-camp" => "长弓营地", "card.building.cannon-camp" => "炮车营地",
            "card.building.gatherer-lodge" => "采集者小屋",
            "card.building.wood-gatherer-camp" => "伐木营地",
            "card.building.stone-gatherer-camp" => "采石营地",
            "card.building.iron-gatherer-camp" => "采铁营地",
            "card.battlefield.arrow-tower" => "箭塔工程",
            "card.tactic.field-rations" => "补充口粮", "card.tactic.emergency-supplies" => "紧急补给",
            "card.tactic.arrow-rain" => "箭雨", _ => id.Value
        };
        private static string TrainingFailureText(TrainingFailure failure) => failure switch
        {
            TrainingFailure.CardInactive => "对应营地尚未建造", TrainingFailure.InsufficientResources => "资源不足",
            TrainingFailure.InvalidDeploymentPoint => "部署点无效", TrainingFailure.TooManyUnitTypes => "最多同时选择 3 个兵种",
            TrainingFailure.TooManyUnits => "单次订单最多 8 名", TrainingFailure.SelectionEmpty => "尚未选择士兵",
            _ => failure.ToString()
        };

        private void Refresh() => _view?.Render(BuildViewModel());
    }
}
