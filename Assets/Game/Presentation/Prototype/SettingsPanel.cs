using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class SettingsPanel : UIPanelBase, ISettingsView
    {
        [SerializeField] private Slider _masterVolumeSlider;
        [SerializeField] private Slider _musicVolumeSlider;
        [SerializeField] private Slider _sfxVolumeSlider;
        [SerializeField] private Toggle _muteToggle;
        [SerializeField] private Text _masterVolumeValue;
        [SerializeField] private Text _musicVolumeValue;
        [SerializeField] private Text _sfxVolumeValue;
        [SerializeField] private Text _errorText;
        [SerializeField] private Button _applyButton;
        [SerializeField] private Button _cancelButton;
        private ISettingsViewCommands _commands;
        private bool _submitting;

        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            _masterVolumeSlider.onValueChanged.AddListener(RenderVolumeValue);
            _musicVolumeSlider.onValueChanged.AddListener(RenderMusicVolumeValue);
            _sfxVolumeSlider.onValueChanged.AddListener(RenderSfxVolumeValue);
            _applyButton.onClick.AddListener(ApplyAndClose);
            _cancelButton.onClick.AddListener(Cancel);
            return Task.CompletedTask;
        }

        public void Bind(ISettingsViewCommands commands, ApplicationSettingsSnapshot snapshot)
        {
            _commands = commands ?? throw new ArgumentNullException(nameof(commands));
            _submitting = false;
            _masterVolumeSlider.SetValueWithoutNotify(snapshot.MasterVolumePercent);
            _musicVolumeSlider.SetValueWithoutNotify(snapshot.MusicVolumePercent);
            _sfxVolumeSlider.SetValueWithoutNotify(snapshot.SfxVolumePercent);
            _muteToggle.SetIsOnWithoutNotify(snapshot.Muted);
            RenderVolumeValue(snapshot.MasterVolumePercent);
            RenderMusicVolumeValue(snapshot.MusicVolumePercent);
            RenderSfxVolumeValue(snapshot.SfxVolumePercent);
            if (_errorText != null) _errorText.text = string.Empty;
            SetButtonsEnabled(true);
        }

        public void ShowSaveError()
        {
            _submitting = false;
            SetButtonsEnabled(true);
            if (_errorText != null) _errorText.text = "设置保存失败，请重试";
        }

        private void RenderVolumeValue(float value)
        {
            if (_masterVolumeValue != null) _masterVolumeValue.text = $"{Mathf.RoundToInt(value)}%";
        }

        private void RenderMusicVolumeValue(float value)
        {
            if (_musicVolumeValue != null) _musicVolumeValue.text = $"{Mathf.RoundToInt(value)}%";
        }

        private void RenderSfxVolumeValue(float value)
        {
            if (_sfxVolumeValue != null) _sfxVolumeValue.text = $"{Mathf.RoundToInt(value)}%";
        }

        private async void ApplyAndClose()
        {
            if (_commands == null || _submitting) return;
            _submitting = true;
            SetButtonsEnabled(false);
            try
            {
                await _commands.ApplyAndCloseAsync(Mathf.RoundToInt(_masterVolumeSlider.value),
                    Mathf.RoundToInt(_musicVolumeSlider.value), Mathf.RoundToInt(_sfxVolumeSlider.value),
                    _muteToggle.isOn, CancellationToken.None);
            }
            catch (Exception exception)
            {
                _submitting = false;
                SetButtonsEnabled(true);
                Debug.LogException(exception, this);
            }
        }

        private async void Cancel()
        {
            if (_commands == null || _submitting) return;
            _submitting = true;
            SetButtonsEnabled(false);
            try { await _commands.CancelAsync(CancellationToken.None); }
            catch (Exception exception)
            {
                _submitting = false;
                SetButtonsEnabled(true);
                Debug.LogException(exception, this);
            }
        }

        private void SetButtonsEnabled(bool enabled)
        {
            if (_applyButton != null) _applyButton.interactable = enabled;
            if (_cancelButton != null) _cancelButton.interactable = enabled;
        }
    }
}
