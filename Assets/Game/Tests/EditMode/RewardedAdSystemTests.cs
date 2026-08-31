using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Monetization;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.Settings;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class RewardedAdSystemTests
    {
        [Test]
        public async Task Watch_FirstPersistsConsent_ThenVerifiesAndClaimsOnce()
        {
            var settings = new ApplicationSettingsSystem(_ => Task.CompletedTask, _ => { });
            await settings.InitializeAsync(new GameContext("test"), CancellationToken.None);
            var ads = new FakeAds();
            var bonus = new FakeBonus();
            var system = new RewardedAdSystem(new RewardedAdConfiguration(1, "key", 2, "https://example.com/privacy"),
                ads, settings, settings, bonus);
            await system.InitializeAsync(new GameContext("test"), CancellationToken.None);
            var receipt = Receipt();

            var result = await system.WatchAndClaimAsync(receipt, CancellationToken.None);

            Assert.That(settings.GetSnapshot().RewardedAdConsentGranted, Is.True);
            Assert.That(ads.ShowCount, Is.EqualTo(1));
            Assert.That(bonus.ClaimCount, Is.EqualTo(1));
            Assert.That(result.Visible, Is.False);
            Assert.That(result.StatusText, Does.Contain("已领取"));
        }

        [Test]
        public async Task VerifiedAd_WhenClaimSaveFails_RetryDoesNotShowAnotherAd()
        {
            var settings = new ApplicationSettingsSystem(_ => Task.CompletedTask, _ => { });
            await settings.InitializeAsync(new GameContext("test"), CancellationToken.None);
            var ads = new FakeAds();
            var bonus = new FakeBonus { FailFirst = true };
            var system = new RewardedAdSystem(new RewardedAdConfiguration(1, "key", 2, "https://example.com/privacy"),
                ads, settings, settings, bonus);
            await system.InitializeAsync(new GameContext("test"), CancellationToken.None);
            var receipt = Receipt();

            var failed = await system.WatchAndClaimAsync(receipt, CancellationToken.None);
            var retried = await system.WatchAndClaimAsync(receipt, CancellationToken.None);

            Assert.That(failed.StatusText, Does.Contain("无需重看"));
            Assert.That(retried.Visible, Is.False);
            Assert.That(ads.ShowCount, Is.EqualTo(1));
            Assert.That(bonus.ClaimCount, Is.EqualTo(2));
        }

        private static SettlementReceipt Receipt() => new(new MatchId("match-ad"), 30, 230,
            false, false, SettlementStatus.Success, 15, false);

        private sealed class FakeAds : IRewardedAdService
        {
            public bool IsAvailable => true;
            public int ShowCount { get; private set; }
            public Task<RewardedAdPlaybackResult> ShowAsync(string matchId, int rewardAmount,
                CancellationToken cancellationToken)
            {
                ShowCount++;
                return Task.FromResult(new RewardedAdPlaybackResult(RewardedAdPlaybackStatus.Verified));
            }
        }

        private sealed class FakeBonus : IRewardedAdBonusService
        {
            public bool FailFirst { get; set; }
            public int ClaimCount { get; private set; }
            public Task<RewardedAdBonusClaimResult> ClaimRewardedAdBonusAsync(MatchId matchId,
                CancellationToken cancellationToken)
            {
                ClaimCount++;
                var status = FailFirst && ClaimCount == 1
                    ? RewardedAdBonusClaimStatus.SaveFailed
                    : RewardedAdBonusClaimStatus.Success;
                return Task.FromResult(new RewardedAdBonusClaimResult(status,
                    status == RewardedAdBonusClaimStatus.Success ? 15 : 0, 245));
            }
        }
    }
}
