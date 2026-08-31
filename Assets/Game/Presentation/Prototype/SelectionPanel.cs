using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class SelectionPanel : UIPanelBase, ISelectionView
    {
        [SerializeField] private Text _goldText;
        [SerializeField] private Text _progressText;
        [SerializeField] private Text _detailTitle;
        [SerializeField] private Text _detailBody;
        [SerializeField] private Text _modeSummary;
        [SerializeField] private Text _battlefieldName;
        [SerializeField] private Image _mapPreview;
        [SerializeField] private Button[] _categoryButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _categoryFrames = Array.Empty<Image>();
        [SerializeField] private Button[] _cardButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _cardFrames = Array.Empty<Image>();
        [SerializeField] private Image[] _cardImages = Array.Empty<Image>();
        [SerializeField] private Text[] _cardLabels = Array.Empty<Text>();
        [SerializeField] private Button[] _modeButtons = Array.Empty<Button>();
        [SerializeField] private Image[] _modeFrames = Array.Empty<Image>();
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _unlockButton;
        [SerializeField] private Button _upgradeButton;
        [SerializeField] private Button _previousBattlefieldButton;
        [SerializeField] private Button _nextBattlefieldButton;
        [SerializeField] private Button _settingsButton;
        [SerializeField] private Button _previousCardPageButton;
        [SerializeField] private Button _nextCardPageButton;
        [SerializeField] private Text _cardPageText;
        private ISelectionCommands _commands;
        private IGameplaySpriteResolver _sprites;
        private SelectionViewModel _viewModel;
        private static readonly Color Selected = new(0.20f, 0.48f, 0.82f, 1f);
        private static readonly Color Idle = new(0.20f, 0.15f, 0.11f, 0.94f);

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            for (var i = 0; i < _categoryButtons.Length; i++)
            {
                var index = i;
                _categoryButtons[i].onClick.AddListener(() => _commands?.SelectCategory((SelectionCategory)index));
            }
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                var index = i;
                _cardButtons[i].onClick.AddListener(() => SelectCard(index));
            }
            for (var i = 0; i < _modeButtons.Length; i++)
            {
                var index = i;
                _modeButtons[i].onClick.AddListener(() => SelectMode(index));
            }
            _startButton.onClick.AddListener(StartMatch);
            _unlockButton?.onClick.AddListener(UnlockSelected);
            _upgradeButton?.onClick.AddListener(UpgradeSelected);
            _previousBattlefieldButton?.onClick.AddListener(() => _commands?.CycleBattlefield(-1));
            _nextBattlefieldButton?.onClick.AddListener(() => _commands?.CycleBattlefield(1));
            _settingsButton?.onClick.AddListener(OpenSettings);
            _previousCardPageButton?.onClick.AddListener(() => _commands?.CycleCardPage(-1));
            _nextCardPageButton?.onClick.AddListener(() => _commands?.CycleCardPage(1));
            return Task.CompletedTask;
        }

        public void Bind(ISelectionCommands commands, IGameplaySpriteResolver sprites, SelectionViewModel viewModel)
        {
            _commands = commands;
            _sprites = sprites;
            Render(viewModel);
        }

        public void Render(SelectionViewModel viewModel)
        {
            _viewModel = viewModel;
            _goldText.text = viewModel.Gold.ToString("N0");
            _progressText.text = $"远征进度  {viewModel.ExpeditionStep}/{viewModel.ExpeditionMax}";
            if (_battlefieldName != null)
                _battlefieldName.text = viewModel.BattlefieldUnlocked ? viewModel.BattlefieldName : $"锁定 · {viewModel.BattlefieldName}";
            if (_mapPreview != null) _mapPreview.sprite = _sprites?.Resolve(viewModel.MapArt);
            if (_cardPageText != null) _cardPageText.text = $"{viewModel.CardPageIndex + 1}/{viewModel.CardPageCount}";
            if (_previousCardPageButton != null) _previousCardPageButton.interactable = viewModel.CardPageCount > 1;
            if (_nextCardPageButton != null) _nextCardPageButton.interactable = viewModel.CardPageCount > 1;
            _startButton.interactable = viewModel.BattlefieldUnlocked;

            for (var i = 0; i < _categoryFrames.Length; i++)
                _categoryFrames[i].color = i == (int)viewModel.Category ? Selected : Idle;
            for (var i = 0; i < _cardButtons.Length; i++)
            {
                var visible = i < viewModel.Cards.Count;
                _cardButtons[i].gameObject.SetActive(visible);
                if (!visible)
                {
                    _cardImages[i].sprite = null;
                    _cardImages[i].enabled = false;
                    continue;
                }
                var card = viewModel.Cards[i];
                _cardImages[i].sprite = _sprites?.Resolve(card.ArtKey);
                _cardImages[i].enabled = _cardImages[i].sprite != null;
                _cardLabels[i].text = card.Unlocked
                    ? $"Lv.{card.Level}\n{card.Name}\n{card.Progress}/{card.ProgressMax}"
                    : $"锁定\n{card.Name}\n尚未解锁";
                _cardFrames[i].color = card.Id.Equals(viewModel.SelectedCardId)
                    ? Selected
                    : card.Unlocked ? new Color(0.68f, 0.46f, 0.22f, 1f) : new Color(0.24f, 0.24f, 0.24f, 1f);
            }

            SelectionCardViewModel selected = null;
            foreach (var card in viewModel.Cards)
                if (card.Id.Equals(viewModel.SelectedCardId)) selected = card;
            if (selected != null)
            {
                _detailTitle.text = selected.Name;
                _detailBody.text = selected.Unlocked
                    ? $"{selected.Subtitle}\n当前等级  Lv.{selected.Level}\n升级仅按配置白名单提升属性"
                    : $"{selected.Subtitle}\n尚未解锁 · 解锁会由存档事务扣除金币";
                if (_unlockButton != null) _unlockButton.gameObject.SetActive(!selected.Unlocked);
                if (_upgradeButton != null) _upgradeButton.gameObject.SetActive(selected.Unlocked);
            }

            for (var i = 0; i < _modeFrames.Length; i++)
            {
                var visible = i < viewModel.ModeIds.Count;
                _modeButtons[i].gameObject.SetActive(visible);
                if (visible) _modeFrames[i].color = viewModel.ModeId.Equals(viewModel.ModeIds[i]) ? Selected : Idle;
            }
            _modeSummary.text = viewModel.ModeId.Value.EndsWith(".offensive", StringComparison.Ordinal)
                ? "主动进攻 · 敌方采集效率 108% · 奖励 ×1.25"
                : viewModel.ModeId.Value.EndsWith(".nightmare", StringComparison.Ordinal)
                    ? "噩梦 · 决策强化 · 敌方采集效率 110% · 奖励 ×1.50"
                    : "和平发展 · 敌方采集效率 100% · 奖励 ×1.00";
        }

        private void SelectCard(int index)
        {
            if (_viewModel != null && index < _viewModel.Cards.Count) _commands?.SelectCard(_viewModel.Cards[index].Id);
        }

        private void SelectMode(int index)
        {
            if (_viewModel != null && index < _viewModel.ModeIds.Count) _commands?.SelectMode(_viewModel.ModeIds[index]);
        }

        private async void StartMatch()
        {
            if (_commands == null) return;
            try { await _commands.StartMatchAsync(CancellationToken.None); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private async void UnlockSelected()
        {
            if (_commands == null) return;
            try { await _commands.UnlockSelectedCardAsync(CancellationToken.None); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private async void UpgradeSelected()
        {
            if (_commands == null) return;
            try { await _commands.UpgradeSelectedCardAsync(CancellationToken.None); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private async void OpenSettings()
        {
            if (_commands == null) return;
            try { await _commands.OpenSettingsAsync(CancellationToken.None); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }
    }
}
