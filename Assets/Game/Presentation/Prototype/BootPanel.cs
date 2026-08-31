using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class BootPanel : UIPanelBase
    {
        [SerializeField] private Image _progressFill;
        [SerializeField] private Text _statusText;
        [SerializeField] private GameObject _progressRoot;
        [SerializeField] private GameObject _readyMenu;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _settingsButton;
        private float _phase;
        private bool _ready;
        private bool _transitioning;
        private IBootMenuCommands _commands;
        private const float StatusAnimationPeriod = 1.8f;
        private const float StatusMinimumAlpha = 0.58f;
        private const float StatusVerticalAmplitude = 4f;
        private RectTransform _statusRect;
        private Vector2 _statusBaseAnchoredPosition;
        private Color _statusBaseColor;
        private float _statusAnimationElapsed;
        private bool _statusAnimationInitialized;

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            _startButton.onClick.AddListener(StartGame);
            _settingsButton.onClick.AddListener(OpenSettings);
            CacheStatusAnimationBaseline();
            ShowLoading();
            return Task.CompletedTask;
        }

        protected override Task OnOpenAsync(object arguments, CancellationToken cancellationToken)
        {
            if (!_ready) ShowLoading();
            ResetStatusAnimation();
            Render();
            return Task.CompletedTask;
        }

        protected override Task OnCloseAsync(CancellationToken cancellationToken)
        {
            ResetStatusAnimation();
            return Task.CompletedTask;
        }


        public void Bind(IBootMenuCommands commands) =>
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));

        public void SetReady()
        {
            _ready = true;
            _transitioning = false;
            _phase = 1f;
            if (_progressRoot != null) _progressRoot.SetActive(false);
            if (_readyMenu != null) _readyMenu.SetActive(true);
            if (_statusText != null) _statusText.text = "整备完成";
            SetButtonsEnabled(true);
        }

        private void Update()
        {
            if (!IsOpen) return;
            if (!_ready)
            {
                _phase = Mathf.MoveTowards(_phase, 0.88f, Time.unscaledDeltaTime * 0.22f);
                Render();
            }

            AnimateStatus();
        }

        private void Render()
        {
            if (_progressFill != null) _progressFill.fillAmount = _phase;
            if (_statusText != null) _statusText.text = _phase < 0.55f ? "正在整备防线…" : "正在进入前线…";
        }

        private void ShowLoading()
        {
            _ready = false;
            _transitioning = false;
            _phase = 0.18f;
            if (_progressRoot != null) _progressRoot.SetActive(true);
            if (_readyMenu != null) _readyMenu.SetActive(false);
        }

        private async void StartGame()
        {
            if (_commands == null || !_ready || _transitioning) return;
            _transitioning = true;
            SetButtonsEnabled(false);
            try { await _commands.StartGameAsync(CancellationToken.None); }
            catch (Exception exception)
            {
                _transitioning = false;
                SetButtonsEnabled(true);
                Debug.LogException(exception, this);
            }
        }

        private async void OpenSettings()
        {
            if (_commands == null || !_ready || _transitioning) return;
            try { await _commands.OpenSettingsAsync(CancellationToken.None); }
            catch (Exception exception) { Debug.LogException(exception, this); }
        }

        private void CacheStatusAnimationBaseline()
        {
            if (_statusText == null) return;
            _statusRect = _statusText.rectTransform;
            _statusBaseAnchoredPosition = _statusRect.anchoredPosition;
            _statusBaseColor = _statusText.color;
            _statusAnimationInitialized = true;
        }

        private void ResetStatusAnimation()
        {
            if (!_statusAnimationInitialized) CacheStatusAnimationBaseline();
            _statusAnimationElapsed = 0f;
            if (_statusRect != null) _statusRect.anchoredPosition = _statusBaseAnchoredPosition;
            if (_statusText != null) _statusText.color = _statusBaseColor;
        }

        private void AnimateStatus()
        {
            if (!_statusAnimationInitialized) CacheStatusAnimationBaseline();
            if (_statusText == null || _statusRect == null) return;

            _statusAnimationElapsed = Mathf.Repeat(
                _statusAnimationElapsed + Time.unscaledDeltaTime, StatusAnimationPeriod);
            var wave = Mathf.Sin(_statusAnimationElapsed / StatusAnimationPeriod * Mathf.PI * 2f);
            var blend = (wave + 1f) * 0.5f;
            _statusRect.anchoredPosition = _statusBaseAnchoredPosition +
                Vector2.up * (wave * StatusVerticalAmplitude);
            var color = _statusBaseColor;
            color.a = _statusBaseColor.a * Mathf.Lerp(StatusMinimumAlpha, 1f, blend);
            _statusText.color = color;
        }

        private void SetButtonsEnabled(bool enabled)
        {
            if (_startButton != null) _startButton.interactable = enabled;
            if (_settingsButton != null) _settingsButton.interactable = enabled;
        }
    }
}
