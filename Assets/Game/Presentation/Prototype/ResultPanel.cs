using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Monetization;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class ResultPanel : UIPanelBase
    {
        [SerializeField] private Text _title;
        [SerializeField] private Text _summary;
        [SerializeField] private Button _returnButton;
        [SerializeField] private Button _retryButton;
        [SerializeField] private Button _rewardedAdButton;
        [SerializeField] private Text _rewardedAdLabel;
        [SerializeField] private Text _rewardedAdStatus;
        [SerializeField] private Button _privacyPolicyButton;
        private ResultPanelArguments _arguments;
        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            _returnButton.onClick.AddListener(Return);
            if (_retryButton != null) _retryButton.onClick.AddListener(Retry);
            if (_rewardedAdButton != null) _rewardedAdButton.onClick.AddListener(WatchRewardedAd);
            if (_privacyPolicyButton != null) _privacyPolicyButton.onClick.AddListener(OpenPrivacyPolicy);
            return Task.CompletedTask;
        }
        protected override Task OnOpenAsync(object arguments, CancellationToken cancellationToken)
        {
            if (arguments is ResultPanelArguments result)
            {
                _arguments = result; _title.text = result.Title; _summary.text = result.Summary;
                _returnButton.gameObject.SetActive(result.Settled);
                if (_retryButton != null) _retryButton.gameObject.SetActive(!result.Settled && result.RetryCommand != null);
                RenderRewardedAd(result.RewardedAdOffer);
            }
            return Task.CompletedTask;
        }
        private async void Return() { if (_arguments == null) return; try { await _arguments.ReturnCommand(CancellationToken.None); } catch (Exception exception) { Debug.LogException(exception, this); } }
        private async void Retry() { if (_arguments?.RetryCommand == null) return; try { await _arguments.RetryCommand(CancellationToken.None); } catch (Exception exception) { Debug.LogException(exception, this); } }
        private async void WatchRewardedAd()
        {
            if (_arguments?.WatchRewardedAdCommand == null) return;
            try
            {
                _rewardedAdButton.interactable = false;
                if (_rewardedAdStatus != null) _rewardedAdStatus.text = "正在准备广告…";
                RenderRewardedAd(await _arguments.WatchRewardedAdCommand(CancellationToken.None));
            }
            catch (Exception exception)
            {
                Debug.LogException(exception, this);
                if (_rewardedAdStatus != null) _rewardedAdStatus.text = "广告暂不可用，请稍后重试。";
                if (_rewardedAdButton != null) _rewardedAdButton.interactable = true;
            }
        }

        private void OpenPrivacyPolicy()
        {
            var url = _arguments?.RewardedAdOffer?.PrivacyPolicyUrl;
            if (!string.IsNullOrWhiteSpace(url)) Application.OpenURL(url);
        }

        private void RenderRewardedAd(RewardedAdOffer offer)
        {
            var visible = offer?.Visible == true && _arguments?.WatchRewardedAdCommand != null;
            if (_rewardedAdButton != null)
            {
                _rewardedAdButton.gameObject.SetActive(visible);
                _rewardedAdButton.interactable = visible;
            }
            if (_rewardedAdLabel != null && offer != null) _rewardedAdLabel.text = offer.ButtonText;
            if (_rewardedAdStatus != null)
            {
                _rewardedAdStatus.gameObject.SetActive(offer != null && !string.IsNullOrWhiteSpace(offer.StatusText));
                _rewardedAdStatus.text = offer?.StatusText ?? string.Empty;
            }
            if (_privacyPolicyButton != null)
                _privacyPolicyButton.gameObject.SetActive(visible && !offer.ConsentGranted);
        }
    }
}
