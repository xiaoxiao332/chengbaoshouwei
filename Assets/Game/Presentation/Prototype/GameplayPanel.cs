using System;
using System.Collections;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Core.Identifiers;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class GameplayPanel : UIPanelBase, IGameplayView
    {
        [SerializeField] private Button _soldierTabButton;
        [SerializeField] private Button _itemTabButton;
        [SerializeField] private Image _soldierTabFrame;
        [SerializeField] private Image _itemTabFrame;
        [SerializeField] private GameObject _soldierCards;
        [SerializeField] private GameObject _itemCards;
        [SerializeField] private Image _worldBackground;
        [SerializeField] private Button[] _cardButtons = Array.Empty<Button>();
        [SerializeField] private RectTransform[] _cardRects = Array.Empty<RectTransform>();
        [SerializeField] private Image[] _cardArtImages = Array.Empty<Image>();
        [SerializeField] private ReinforcementCardVisual[] _itemReinforcementVisuals = Array.Empty<ReinforcementCardVisual>();
        [SerializeField] private Button[] _soldierDecreaseButtons = Array.Empty<Button>();
        [SerializeField] private Button[] _soldierIncreaseButtons = Array.Empty<Button>();
        [SerializeField] private Text[] _soldierCountTexts = Array.Empty<Text>();
        [SerializeField] private Button _worldCancelButton;
        [SerializeField] private BuildingPlacementPreview _buildingPlacementPreview;
        [SerializeField] private Button[] _buildingSlotButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _buildingSlotFrames = Array.Empty<Image>();
        [SerializeField] private GameObject _buildingMenu;
        [SerializeField] private Button[] _buildingActionButtons = Array.Empty<Button>();
        [SerializeField] private GameObject _cardHoverPanel;
        [SerializeField] private Text _cardHoverNameText;
        [SerializeField] private Text _cardHoverCostText;
        [SerializeField] private Text _cardHoverAttributesText;
        [SerializeField] private GameObject _deploymentGrid;
        [SerializeField] private DeploymentAreaInput _deploymentAreaInput;
        [SerializeField] private Button _useItemButton;
        [SerializeField] private Text _itemCountText;
        [SerializeField] private GameObject _choicePanel;
        [SerializeField] private Button[] _choiceOptions = Array.Empty<Button>();
        [SerializeField] private Image[] _choiceArtImages = Array.Empty<Image>();
        [SerializeField] private ReinforcementCardVisual[] _choiceReinforcementVisuals = Array.Empty<ReinforcementCardVisual>();
        [SerializeField] private Button _researchButton;
        [SerializeField] private GameObject _researchPanel;
        [SerializeField] private Button _researchCloseButton;
        [SerializeField] private Button[] _researchOptionButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _researchOptionImages = Array.Empty<Image>();
        [SerializeField] private Text[] _researchOptionTexts = Array.Empty<Text>();
        [SerializeField] private Image _researchProgressFill;
        [SerializeField] private Text _researchStatusText;
        [SerializeField] private Button _previousSoldierPageButton;
        [SerializeField] private Button _nextSoldierPageButton;
        [SerializeField] private Text _soldierPageText;
        [SerializeField] private Button _resultButton;
        [SerializeField] private Text[] _resourceTexts = Array.Empty<Text>();
        [SerializeField] private Text _clockText;
        [SerializeField] private Text _playerWallText;
        [SerializeField] private Text _enemyWallText;
        [SerializeField] private Image[] _buildingImages = Array.Empty<Image>();
        [SerializeField] private BuildingSlotProgressView[] _buildingProgressViews = Array.Empty<BuildingSlotProgressView>();
        [SerializeField] private UpgradeButtonFeedback _upgradeButtonFeedback;
        [SerializeField] private Image[] _deployedUnitImages = Array.Empty<Image>();
        private IGameplayCommands _commands;
        private IGameplaySpriteResolver _sprites;
        private GameplayViewModel _viewModel;
        private int _hoveredCardIndex = -1;
        private int _hoveredBuildingSlot = -1;
        private bool _buildingMenuPointerInside;
        private Coroutine _buildingMenuHideRoutine;
        private static readonly Color Active = new(0.91f, 0.44f, 0.14f, 1f);
        private static readonly Color Idle = new(0.18f, 0.14f, 0.11f, 1f);

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            for (var i = 0; i < Math.Min(4, _cardButtons.Length); i++)
                if (_cardButtons[i] != null) _cardButtons[i].gameObject.SetActive(false);
            _soldierTabButton.onClick.AddListener(() => _commands?.SelectTab(GameplayCardTab.Soldiers));
            _itemTabButton.onClick.AddListener(() => _commands?.SelectTab(GameplayCardTab.Items));
            for (var i = 0; i < _cardButtons.Length; i++) { var index = i; _cardButtons[i].onClick.AddListener(() => ActivateCard(index)); }
            for (var i = 0; i < _soldierDecreaseButtons.Length; i++) { var index = i; _soldierDecreaseButtons[i].onClick.AddListener(() => AdjustSoldier(index, -1)); }
            for (var i = 0; i < _soldierIncreaseButtons.Length; i++) { var index = i; _soldierIncreaseButtons[i].onClick.AddListener(() => AdjustSoldier(index, 1)); }
            _deploymentAreaInput?.Bind(SubmitDeployment);
            if (_worldCancelButton != null) _worldCancelButton.onClick.AddListener(CancelSelectedBuilding);
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                var card = i;
                BindHover(_cardButtons[i].gameObject, () => ShowCardHover(card), () => HideCardHover(card));
            }
            for (var i = 0; i < _buildingSlotButtons.Length; i++)
            {
                var slot = i;
                _buildingSlotButtons[i].onClick.AddListener(() => ActivateBuildingSlot(slot));
                BindHover(_buildingSlotButtons[i].gameObject, () => EnterBuildingSlot(slot), () => ExitBuildingSlot(slot));
            }
            if (_buildingMenu != null)
                BindHover(_buildingMenu, EnterBuildingMenu, ExitBuildingMenu);
            for (var i = 0; i < _buildingActionButtons.Length; i++)
                if (_buildingActionButtons[i] != null)
                    BindHover(_buildingActionButtons[i].gameObject, EnterBuildingMenu, ExitBuildingMenu);
            if (_buildingActionButtons.Length > 0) _buildingActionButtons[0].onClick.AddListener(ResumeSelectedBuildingAfterResourceShortage);
            if (_buildingActionButtons.Length > 1) _buildingActionButtons[1].onClick.AddListener(UpgradeSelectedBuilding);
            if (_buildingActionButtons.Length > 2) _buildingActionButtons[2].onClick.AddListener(() => _commands?.ExecuteBuildingCommand(GameplayBuildingCommand.Demolish));
            _cardHoverPanel?.SetActive(false);
            _buildingMenu?.SetActive(false);
            _useItemButton.gameObject.SetActive(false);
            for (var i = 0; i < _choiceOptions.Length; i++) { var index = i; _choiceOptions[i].onClick.AddListener(() => ChooseOffer(index)); }
            _researchButton.onClick.AddListener(() => _commands?.ToggleResearch());
            _researchCloseButton?.onClick.AddListener(() => _commands?.ToggleResearch());
            for (var i = 0; i < _researchOptionButtons.Length; i++)
            { var index = i; _researchOptionButtons[i]?.onClick.AddListener(() => StartResearch(index)); }
            _previousSoldierPageButton?.onClick.AddListener(() => _commands?.CycleSoldierPage(-1));
            _nextSoldierPageButton?.onClick.AddListener(() => _commands?.CycleSoldierPage(1));
            _resultButton.onClick.AddListener(ShowResult);
            _resultButton.gameObject.SetActive(false);
            _researchButton.gameObject.SetActive(true);
            _itemTabButton.gameObject.SetActive(true);
            return Task.CompletedTask;
        }

        public void Bind(IGameplayCommands commands, IGameplaySpriteResolver sprites, GameplayViewModel viewModel)
        { _commands = commands; _sprites = sprites ?? throw new ArgumentNullException(nameof(sprites)); Render(viewModel); }
        public void Render(GameplayViewModel viewModel)
        {
            _viewModel = viewModel;
            if (_worldBackground != null) _worldBackground.sprite = _sprites.Resolve(viewModel.MapArt);
            var soldiers = viewModel.Tab == GameplayCardTab.Soldiers;
            _soldierCards.SetActive(soldiers); _itemCards.SetActive(!soldiers);
            _soldierTabFrame.color = soldiers ? Active : Idle; _itemTabFrame.color = soldiers ? Idle : Active;
            RenderCards(viewModel);
            var selectedBuilding = SelectedBuildingCard(viewModel);
            if (selectedBuilding != null) _buildingPlacementPreview?.Show(_sprites.Resolve(selectedBuilding.ArtKey));
            else _buildingPlacementPreview?.Hide();
            RenderBuildingMenu(viewModel);
            _deploymentGrid.SetActive(viewModel.DeploymentGridVisible);
            _choicePanel.SetActive(viewModel.Offer.Active && !viewModel.Offer.ReplacementMode);
            _researchPanel.SetActive(viewModel.ResearchOpen);
            RenderResearch(viewModel);
            if (_previousSoldierPageButton != null && _previousSoldierPageButton.transform.parent != null)
                _previousSoldierPageButton.transform.parent.gameObject.SetActive(soldiers);
            if (_soldierPageText != null) _soldierPageText.text = $"{viewModel.SoldierPageIndex + 1}/{viewModel.SoldierPageCount}";
            if (_previousSoldierPageButton != null) _previousSoldierPageButton.interactable = viewModel.SoldierPageCount > 1;
            if (_nextSoldierPageButton != null) _nextSoldierPageButton.interactable = viewModel.SoldierPageCount > 1;
            _itemCountText.text = $"{viewModel.ItemCount}/6";
            for (var i = 0; i < _resourceTexts.Length; i++)
                _resourceTexts[i].text = i < viewModel.ResourceGroups.Count ? viewModel.ResourceGroups[i] : string.Empty;
            for (var i = 0; i < _buildingImages.Length; i++)
            {
                var slot = i < viewModel.BuildingSlots.Count ? viewModel.BuildingSlots[i] : null;
                var visible = slot != null && !string.IsNullOrWhiteSpace(slot.BuildingId);
                _buildingImages[i].gameObject.SetActive(visible);
                if (!visible) continue;
                _buildingImages[i].sprite = _sprites.Resolve(slot.ArtKey);
                _buildingImages[i].color = slot.Paused ? new Color(0.78f, 0.32f, 0.24f, 0.85f) : Color.white;
                if (i < _buildingProgressViews.Length && _buildingProgressViews[i] != null)
                    _buildingProgressViews[i].Render(slot);
            }
            for (var i = 0; i < _deployedUnitImages.Length; i++)
                _deployedUnitImages[i].gameObject.SetActive(false);
            if (_clockText != null) _clockText.text = viewModel.Clock.Text;
            var playerWall = viewModel.Walls.FirstOrDefault(value => value.Faction == MatchFaction.Player);
            var enemyWall = viewModel.Walls.FirstOrDefault(value => value.Faction == MatchFaction.Enemy);
            if (_playerWallText != null && playerWall != null) _playerWallText.text = $"我方城墙  {playerWall.Health:N0} / {playerWall.MaxHealth:N0}";
            if (_enemyWallText != null && enemyWall != null) _enemyWallText.text = $"敌方城墙  {enemyWall.Health:N0} / {enemyWall.MaxHealth:N0}";
            for (var i = 0; i < _choiceOptions.Length; i++)
            {
                var visible = i < viewModel.Offer.Choices.Count;
                _choiceOptions[i].gameObject.SetActive(visible);
                if (!visible) continue;
                var choice = viewModel.Offer.Choices[i];
                var reinforcement = choice.ReinforcementUnits.Count > 0;
                var label = ButtonLabel(_choiceOptions[i]);
                if (label != null) label.gameObject.SetActive(!reinforcement);
                if (!reinforcement) SetLabel(_choiceOptions[i], choice);
                if (i < _choiceArtImages.Length && _choiceArtImages[i] != null)
                {
                    _choiceArtImages[i].sprite = _sprites.Resolve(choice.IconResourceKey);
                    _choiceArtImages[i].color = Color.white;
                    _choiceArtImages[i].preserveAspect = true;
                }
                if (i < _choiceReinforcementVisuals.Length && _choiceReinforcementVisuals[i] != null)
                    _choiceReinforcementVisuals[i].Bind(_sprites, choice.ReinforcementUnits,
                        $"{RarityName(choice.Rarity)} · {choice.Name}");
            }
            for (var i = 0; i < _soldierCountTexts.Length; i++)
            {
                var card = i < viewModel.SoldierCards.Count ? viewModel.SoldierCards[i] : null;
                var count = card?.UnitId.HasValue == true && viewModel.SoldierSelection.Quantities.TryGetValue(card.UnitId.Value, out var selected)
                    ? selected : 0;
                _soldierCountTexts[i].text = count.ToString();
                _soldierCountTexts[i].transform.parent.gameObject.SetActive(card != null);
            }
            var buildingSelected = selectedBuilding != null;
            for (var i = 0; i < _buildingSlotFrames.Length; i++)
            {
                var empty = i >= viewModel.BuildingSlots.Count || string.IsNullOrWhiteSpace(viewModel.BuildingSlots[i].BuildingId);
                _buildingSlotFrames[i].color = buildingSelected && empty ? new Color(0.34f, 0.92f, 0.58f, 1f) : Color.white;
            }
            if (_hoveredCardIndex >= 0) ShowCardHover(_hoveredCardIndex);
        }

        private void RenderResearch(GameplayViewModel viewModel)
        {
            var research = viewModel.Research;
            for (var i = 0; i < _researchOptionButtons.Length; i++)
            {
                var visible = research != null && i < research.Candidates.Count;
                var button = _researchOptionButtons[i];
                if (button == null) continue;
                button.gameObject.SetActive(visible);
                if (!visible) continue;
                var candidate = research.Candidates[i];
                button.interactable = research.LabAvailable && !research.Active && candidate.Rank < candidate.MaxRank;
                if (i < _researchOptionImages.Length && _researchOptionImages[i] != null)
                    _researchOptionImages[i].sprite = _sprites.Resolve(candidate.PresentationKey);
                if (i < _researchOptionTexts.Length && _researchOptionTexts[i] != null)
                {
                    var modifiers = string.Join(" / ", candidate.Modifiers.Select(value =>
                    {
                        var perRank = value.PercentPerRankBasisPoints / 100f;
                        return $"{ResearchProperty(value.PropertyKey)} +{perRank * candidate.Rank:0.#}% → +{perRank * (candidate.Rank + 1):0.#}%";
                    }));
                    _researchOptionTexts[i].text = $"{ResearchRole(candidate.TargetRole)}  Lv.{candidate.Rank}/{candidate.MaxRank}\n{modifiers}";
                }
            }
            if (_researchProgressFill != null)
                _researchProgressFill.fillAmount = research?.Active == true
                    ? research.ProgressTicks / (float)Math.Max(1, research.RequiredTicks) : 0f;
            if (_researchStatusText != null)
                _researchStatusText.text = research?.Active == true
                    ? $"研究进行中  {research.ProgressTicks}/{research.RequiredTicks} Tick\n花费 {viewModel.ResearchCost}"
                    : string.IsNullOrWhiteSpace(viewModel.ResearchReason)
                        ? $"选择一个类别预设 · 花费 {viewModel.ResearchCost}" : viewModel.ResearchReason;
        }

        private void StartResearch(int index)
        {
            if (_viewModel?.Research == null || index < 0 || index >= _viewModel.Research.Candidates.Count) return;
            _commands?.StartResearch(_viewModel.Research.Candidates[index].Id);
        }

        private static string ResearchRole(ResearchCategory role) => role switch
        { ResearchCategory.Melee => "近战", ResearchCategory.Ranged => "远程", ResearchCategory.Magic => "魔法", _ => "攻城" };
        private static string ResearchProperty(string key) => key switch
        { "damage" => "攻击", "health" => "生命", "range" => "射程", _ => key };

        private void RenderCards(GameplayViewModel viewModel)
        {
            for (var index = 0; index < _cardButtons.Length; index++)
            {
                var sourceIndex = index < 4 ? index : index - 4;
                var source = index < 4 ? viewModel.SoldierCards : viewModel.ItemHand;
                var visible = sourceIndex < source.Count;
                _cardButtons[index].gameObject.SetActive(visible);
                if (!visible) continue;
                var card = source[sourceIndex];
                if (index < 4)
                {
                    SetLabel(_cardButtons[index], card);
                    if (index < _cardArtImages.Length && _cardArtImages[index] != null)
                        _cardArtImages[index].sprite = _sprites.Resolve(card.ArtKey);
                }
                else
                {
                    var label = ButtonLabel(_cardButtons[index]);
                    if (label != null) label.gameObject.SetActive(false);
                    var itemIndex = index - 4;
                    var reinforcement = card.Type == CardType.ReinforcementItem;
                    if (index < _cardArtImages.Length && _cardArtImages[index] != null)
                    {
                        _cardArtImages[index].gameObject.SetActive(!reinforcement);
                        if (!reinforcement) _cardArtImages[index].sprite = _sprites.Resolve(card.ArtKey);
                    }
                    if (itemIndex < _itemReinforcementVisuals.Length && _itemReinforcementVisuals[itemIndex] != null)
                        _itemReinforcementVisuals[itemIndex].Bind(_sprites, reinforcement ? card.ReinforcementUnits : null, card.Name);
                }
                _cardButtons[index].interactable = card.Enabled;
                if (index < _cardRects.Length)
                {
                    var selected = viewModel.SelectedCardId.HasValue && viewModel.SelectedCardId.Value.Equals(card.Id);
                    _cardRects[index].anchoredPosition = new Vector2(_cardRects[index].anchoredPosition.x, selected ? 12f : 0f);
                }
            }
        }

        private void ActivateCard(int index)
        {
            if (_commands == null || _viewModel == null) return;
            var sourceIndex = index < 4 ? index : index - 4;
            var source = index < 4 ? _viewModel.SoldierCards : _viewModel.ItemHand;
            if (sourceIndex < 0 || sourceIndex >= source.Count) return;
            var card = source[sourceIndex];
            _commands.SelectCard(card.Id);
            if (card.UnitId.HasValue)
            {
                var count = _viewModel.SoldierSelection.Quantities.TryGetValue(card.UnitId.Value, out var selected) ? selected : 0;
                _commands.UpdateSoldierSelection(card.UnitId.Value, Math.Max(1, count));
                return;
            }
            if (card.Type == CardType.Tactic)
            { _commands.UseTactic(card.Id); return; }
            if (card.Type == CardType.BattlefieldItem)
            { _commands.PlaceTower(card.Id); return; }
            if (card.Type == CardType.ReinforcementItem) return;
            // Building cards remain selected until the player clicks a legal nine-grid slot.
        }

        private void ActivateBuildingSlot(int slotIndex)
        {
            var card = SelectedBuildingCard(_viewModel);
            if (_commands == null || card == null || slotIndex < 0 || slotIndex >= _viewModel.BuildingSlots.Count) return;
            if (!string.IsNullOrWhiteSpace(_viewModel.BuildingSlots[slotIndex].BuildingId)) return;
            _commands.PlayBuilding(card.Id, slotIndex);
        }

        private void ShowCardHover(int index)
        {
            if (_viewModel == null || _cardHoverPanel == null) return;
            var sourceIndex = index < 4 ? index : index - 4;
            var source = index < 4 ? _viewModel.SoldierCards : _viewModel.ItemHand;
            if (sourceIndex < 0 || sourceIndex >= source.Count || index < 0 || index >= _cardButtons.Length)
            { HideCardHover(index); return; }
            _hoveredCardIndex = index;
            var card = source[sourceIndex];
            if (_cardHoverNameText != null) _cardHoverNameText.text = card.Name;
            if (_cardHoverCostText != null) _cardHoverCostText.text = string.IsNullOrWhiteSpace(card.Cost) ? "消耗：无" : $"消耗：{card.Cost}";
            if (_cardHoverAttributesText != null) _cardHoverAttributesText.text = string.IsNullOrWhiteSpace(card.Attributes) ? card.Details : card.Attributes;
            _cardHoverPanel.SetActive(true);
            PositionPanelNear(_cardHoverPanel.transform as RectTransform, _cardButtons[index].transform as RectTransform, 104f);
        }

        private void HideCardHover(int index)
        {
            if (_hoveredCardIndex != index) return;
            _hoveredCardIndex = -1;
            _cardHoverPanel?.SetActive(false);
        }

        private void EnterBuildingSlot(int slotIndex)
        {
            if (_viewModel == null || slotIndex < 0 || slotIndex >= _viewModel.BuildingSlots.Count ||
                string.IsNullOrWhiteSpace(_viewModel.BuildingSlots[slotIndex].BuildingId)) return;
            CancelBuildingMenuHide();
            _hoveredBuildingSlot = slotIndex;
            _commands?.ShowBuildingMenu(slotIndex);
        }

        private void ExitBuildingSlot(int slotIndex)
        {
            if (_hoveredBuildingSlot == slotIndex) _hoveredBuildingSlot = -1;
            ScheduleBuildingMenuHide();
        }

        private void EnterBuildingMenu()
        {
            _buildingMenuPointerInside = true;
            CancelBuildingMenuHide();
        }

        private void ExitBuildingMenu()
        {
            _buildingMenuPointerInside = false;
            ScheduleBuildingMenuHide();
        }

        private void ResumeSelectedBuildingAfterResourceShortage()
        {
            var index = _viewModel?.SelectedBuildingSlotIndex ?? -1;
            if (index < 0 || index >= _viewModel.BuildingSlots.Count) return;
            if (_viewModel.BuildingSlots[index].Paused)
                _commands?.ExecuteBuildingCommand(GameplayBuildingCommand.ResumeAfterResourceShortage);
        }

        private void UpgradeSelectedBuilding()
        {
            if (_commands == null) return;
            _upgradeButtonFeedback?.Play(_commands.ExecuteBuildingCommand(GameplayBuildingCommand.Upgrade));
        }

        private void RenderBuildingMenu(GameplayViewModel viewModel)
        {
            if (_buildingMenu == null) return;
            var index = viewModel.SelectedBuildingSlotIndex;
            var valid = viewModel.BuildingMenuOpen && index >= 0 && index < viewModel.BuildingSlots.Count &&
                !string.IsNullOrWhiteSpace(viewModel.BuildingSlots[index].BuildingId);
            _buildingMenu.SetActive(valid);
            if (!valid) return;
            var buildingTarget = index < _buildingImages.Length && _buildingImages[index] != null
                ? _buildingImages[index].rectTransform
                : _buildingSlotButtons[index].transform as RectTransform;
            PositionBuildingMenu(_buildingMenu.transform as RectTransform, buildingTarget);
            if (_buildingActionButtons.Length > 0)
            {
                var slot = viewModel.BuildingSlots[index];
                var state = slot.BlockReason switch
                {
                    ProductionBlockReason.ReserveProtected => "\n保留军需",
                    ProductionBlockReason.MissingInput => "\n等待补给",
                    ProductionBlockReason.OutputFull => "\n仓储已满",
                    _ => string.Empty
                };
                SetLabel(_buildingActionButtons[0], slot.Paused ? "继续" + state : "运行中");
                _buildingActionButtons[0].interactable = slot.Paused;
            }
            if (_buildingActionButtons.Length > 1)
            {
                var upgradeState = viewModel.BuildingSlots[index].UpgradeState;
                _buildingActionButtons[1].interactable = upgradeState is BuildingUpgradeState.Locked or BuildingUpgradeState.Ready;
                _upgradeButtonFeedback?.SetInteractableVisual(_buildingActionButtons[1].interactable);
            }
        }

        private void ScheduleBuildingMenuHide()
        {
            CancelBuildingMenuHide();
            _buildingMenuHideRoutine = StartCoroutine(HideBuildingMenuAfterGrace());
        }

        private IEnumerator HideBuildingMenuAfterGrace()
        {
            yield return new WaitForSecondsRealtime(0.12f);
            _buildingMenuHideRoutine = null;
            if (_hoveredBuildingSlot < 0 && !_buildingMenuPointerInside) _commands?.HideBuildingMenu();
        }

        private void CancelBuildingMenuHide()
        {
            if (_buildingMenuHideRoutine == null) return;
            StopCoroutine(_buildingMenuHideRoutine);
            _buildingMenuHideRoutine = null;
        }

        private void PositionPanelNear(RectTransform panel, RectTransform target, float verticalOffset)
        {
            if (panel == null || target == null || panel.parent is not RectTransform parent) return;
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var screen = RectTransformUtility.WorldToScreenPoint(camera, target.position);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out var local)) return;
            var halfWidth = panel.rect.width * 0.5f;
            var halfHeight = panel.rect.height * 0.5f;
            local.y += verticalOffset;
            local.x = Mathf.Clamp(local.x, parent.rect.xMin + halfWidth + 12f, parent.rect.xMax - halfWidth - 12f);
            local.y = Mathf.Clamp(local.y, parent.rect.yMin + halfHeight + 12f, parent.rect.yMax - halfHeight - 12f);
            panel.anchoredPosition = local;
        }

        private void PositionBuildingMenu(RectTransform panel, RectTransform target)
        {
            if (panel == null || target == null || panel.parent is not RectTransform parent) return;
            var canvas = GetComponentInParent<Canvas>();
            var camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var targetTop = target.TransformPoint(new Vector3(target.rect.center.x, target.rect.yMax));
            var screen = RectTransformUtility.WorldToScreenPoint(camera, targetTop);
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, screen, camera, out var local)) return;
            var halfWidth = panel.rect.width * 0.5f;
            var halfHeight = panel.rect.height * 0.5f;
            local.y += halfHeight + 10f;
            local.x = Mathf.Clamp(local.x, parent.rect.xMin + halfWidth + 12f, parent.rect.xMax - halfWidth - 12f);
            local.y = Mathf.Clamp(local.y, parent.rect.yMin + halfHeight + 12f, parent.rect.yMax - halfHeight - 12f);
            panel.localPosition = new Vector3(local.x, local.y, panel.localPosition.z);
        }

        private static void BindHover(GameObject target, Action entered, Action exited)
        {
            if (target == null) return;
            var hover = target.GetComponent<GameplayHoverTarget>() ?? target.AddComponent<GameplayHoverTarget>();
            hover.Bind(entered, exited);
        }

        private void CancelSelectedBuilding()
        {
            if (SelectedBuildingCard(_viewModel) != null || _viewModel?.SoldierSelection.TotalCount > 0) _commands?.CancelSelection();
        }

        private void AdjustSoldier(int index, int delta)
        {
            if (_commands == null || _viewModel == null || index < 0 || index >= _viewModel.SoldierCards.Count) return;
            var unitId = _viewModel.SoldierCards[index].UnitId;
            if (!unitId.HasValue) return;
            var current = _viewModel.SoldierSelection.Quantities.TryGetValue(unitId.Value, out var count) ? count : 0;
            _commands.SelectCard(_viewModel.SoldierCards[index].Id);
            _commands.UpdateSoldierSelection(unitId.Value, Math.Clamp(current + delta, 0, 5));
        }

        private void SubmitDeployment(Vector2 normalized)
        {
            var reinforcementSelected = _viewModel?.SelectedCardId.HasValue == true &&
                _viewModel.ItemHand.Any(value => value.Id.Equals(_viewModel.SelectedCardId.Value) && value.ReinforcementTemplateId.HasValue);
            if (_commands == null || _viewModel == null || (_viewModel.SoldierSelection.TotalCount == 0 && !reinforcementSelected)) return;
            var area = _viewModel.DeploymentArea;
            var x = area.X + Mathf.RoundToInt(normalized.x * area.Width);
            var y = area.Y + Mathf.RoundToInt(normalized.y * area.Height);
            _commands.SubmitDeployment(x, y);
        }

        private static GameplayCardViewModel SelectedBuildingCard(GameplayViewModel viewModel)
        {
            if (viewModel == null || !viewModel.SelectedCardId.HasValue) return null;
            return viewModel.ItemHand.FirstOrDefault(value => value.Id.Equals(viewModel.SelectedCardId.Value) && value.Type == CardType.BuildingItem);
        }

        private void ChooseOffer(int index)
        {
            if (_commands == null || _viewModel == null || index < 0 || index >= _viewModel.Offer.Choices.Count) return;
            _commands.ChooseOffer(_viewModel.Offer.Choices[index].Id);
        }

        private static void SetLabel(Button button, GameplayCardViewModel card)
        {
            var label = ButtonLabel(button);
            if (label != null) label.text = $"{card.Name}\n{card.Details}{(card.Count > 1 ? $"  ×{card.Count}" : string.Empty)}";
        }

        private static void SetLabel(Button button, GameplayRewardChoiceViewModel choice)
        {
            var label = ButtonLabel(button);
            if (label == null) return;
            label.text = $"{RarityName(choice.Rarity)} · {choice.Name}\n{choice.Details}";
            label.color = RarityColor(choice.Rarity);
        }

        private static string RarityName(RewardRarity rarity) => rarity switch
        { RewardRarity.Rare => "稀有", RewardRarity.Epic => "史诗", _ => "普通" };

        private static Color RarityColor(RewardRarity rarity) => rarity switch
        {
            RewardRarity.Rare => new Color(0.42f, 0.72f, 1f),
            RewardRarity.Epic => new Color(0.78f, 0.52f, 1f),
            _ => new Color(0.94f, 0.76f, 0.38f)
        };

        private static void SetLabel(Button button, string value)
        {
            var label = ButtonLabel(button);
            if (label != null) label.text = value ?? string.Empty;
        }

        private static Text ButtonLabel(Button button) => button == null ? null :
            button.transform.Find("Label")?.GetComponent<Text>() ?? button.GetComponentInChildren<Text>(true);

        private async void ShowResult() { if (_commands == null) return; try { await _commands.ShowResultAsync(CancellationToken.None); } catch (Exception exception) { Debug.LogException(exception, this); } }
    }
}
