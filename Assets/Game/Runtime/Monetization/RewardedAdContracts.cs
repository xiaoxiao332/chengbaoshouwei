using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.Settings;

namespace FortressFrontier.Runtime.Monetization
{
    public sealed class RewardedAdConfiguration
    {
        public RewardedAdConfiguration(long mediaId, string mediaKey, long rewardSpaceId, string privacyPolicyUrl)
        {
            MediaId = mediaId;
            MediaKey = mediaKey?.Trim() ?? string.Empty;
            RewardSpaceId = rewardSpaceId;
            PrivacyPolicyUrl = privacyPolicyUrl?.Trim() ?? string.Empty;
        }

        public long MediaId { get; }
        public string MediaKey { get; }
        public long RewardSpaceId { get; }
        public string PrivacyPolicyUrl { get; }
        public bool IsComplete => MediaId > 0 && RewardSpaceId > 0 && MediaKey.Length > 0
                                  && Uri.TryCreate(PrivacyPolicyUrl, UriKind.Absolute, out var uri)
                                  && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
    }

    public enum RewardedAdPlaybackStatus
    {
        Verified,
        ClosedWithoutReward,
        Failed,
        Unavailable
    }

    public readonly struct RewardedAdPlaybackResult
    {
        public RewardedAdPlaybackResult(RewardedAdPlaybackStatus status, string message = null)
        { Status = status; Message = message ?? string.Empty; }
        public RewardedAdPlaybackStatus Status { get; }
        public string Message { get; }
    }

    public interface IRewardedAdService
    {
        bool IsAvailable { get; }
        Task<RewardedAdPlaybackResult> ShowAsync(string matchId, int rewardAmount, CancellationToken cancellationToken);
    }

    public sealed class RewardedAdOffer
    {
        public RewardedAdOffer(bool visible, bool consentGranted, int bonusGold, string privacyPolicyUrl,
            string statusText = "")
        {
            Visible = visible;
            ConsentGranted = consentGranted;
            BonusGold = Math.Max(0, bonusGold);
            PrivacyPolicyUrl = privacyPolicyUrl ?? string.Empty;
            StatusText = statusText ?? string.Empty;
        }

        public bool Visible { get; }
        public bool ConsentGranted { get; }
        public int BonusGold { get; }
        public string PrivacyPolicyUrl { get; }
        public string StatusText { get; }
        public string ButtonText => ConsentGranted
            ? $"观看广告再得 {BonusGold} 金币"
            : $"同意隐私政策并观看 · +{BonusGold} 金币";
    }

    public sealed class RewardedAdSystem : GameSystemBase
    {
        private readonly RewardedAdConfiguration _configuration;
        private readonly IRewardedAdService _ads;
        private readonly IApplicationSettingsReader _settings;
        private readonly IApplicationSettingsCommands _settingsCommands;
        private readonly IRewardedAdBonusService _bonus;
        private readonly HashSet<string> _verifiedPendingClaims = new(StringComparer.Ordinal);

        public RewardedAdSystem(RewardedAdConfiguration configuration, IRewardedAdService ads,
            IApplicationSettingsReader settings, IApplicationSettingsCommands settingsCommands,
            IRewardedAdBonusService bonus) : base(SystemLifetime.Global)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _ads = ads ?? throw new ArgumentNullException(nameof(ads));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _settingsCommands = settingsCommands ?? throw new ArgumentNullException(nameof(settingsCommands));
            _bonus = bonus ?? throw new ArgumentNullException(nameof(bonus));
        }

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken) => Task.CompletedTask;

        public RewardedAdOffer CreateOffer(SettlementReceipt receipt, string statusText = "")
        {
            var visible = receipt.Succeeded && !receipt.RewardedAdBonusClaimed && receipt.RewardedAdBonusGold > 0
                          && _configuration.IsComplete && _ads.IsAvailable;
            return new RewardedAdOffer(visible, _settings.GetSnapshot().RewardedAdConsentGranted,
                receipt.RewardedAdBonusGold, _configuration.PrivacyPolicyUrl, statusText);
        }

        public async Task<RewardedAdOffer> WatchAndClaimAsync(SettlementReceipt receipt, CancellationToken cancellationToken)
        {
            var offer = CreateOffer(receipt);
            if (!offer.Visible) return offer;
            if (!offer.ConsentGranted && !await _settingsCommands.SetRewardedAdConsentAsync(true, cancellationToken))
                return CreateOffer(receipt, "隐私授权保存失败，请重试。");

            if (!_verifiedPendingClaims.Contains(receipt.MatchId.Value))
            {
                var playback = await _ads.ShowAsync(receipt.MatchId.Value, receipt.RewardedAdBonusGold, cancellationToken);
                if (playback.Status != RewardedAdPlaybackStatus.Verified)
                {
                    var message = playback.Status == RewardedAdPlaybackStatus.ClosedWithoutReward
                        ? "广告未完整观看，本次没有发放奖励。"
                        : string.IsNullOrWhiteSpace(playback.Message) ? "广告暂不可用，请稍后重试。" : playback.Message;
                    return CreateOffer(receipt, message);
                }
                _verifiedPendingClaims.Add(receipt.MatchId.Value);
            }

            var claim = await _bonus.ClaimRewardedAdBonusAsync(receipt.MatchId, cancellationToken);
            if (claim.Succeeded) _verifiedPendingClaims.Remove(receipt.MatchId.Value);
            return claim.Status switch
            {
                RewardedAdBonusClaimStatus.Success => new RewardedAdOffer(false, true, receipt.RewardedAdBonusGold,
                    _configuration.PrivacyPolicyUrl, $"已领取 {claim.GoldAwarded} 金币"),
                RewardedAdBonusClaimStatus.AlreadyClaimed => new RewardedAdOffer(false, true, receipt.RewardedAdBonusGold,
                    _configuration.PrivacyPolicyUrl, "本局激励奖励已领取。"),
                RewardedAdBonusClaimStatus.SaveFailed => CreateOffer(receipt, "广告已验证，但奖励保存失败；可直接重试领取，无需重看。"),
                _ => CreateOffer(receipt, "本局奖励状态异常，请返回后重试。")
            };
        }
    }
}
