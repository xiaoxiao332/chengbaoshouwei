using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Saving;
using FortressFrontier.Core.Systems;
using FortressFrontier.Infrastructure.Saving;
using FortressFrontier.Runtime.Progression;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class ProgressionSystemTests
    {
        private static readonly CardId BaseCardId = new("card.base");
        private static readonly CardId LockedCardId = new("card.locked");

        [Test]
        public async Task UnlockCard_PersistsThenPublishesNewBalance()
        {
            var saves = 0;
            var system = await CreateSystem(_ => { saves++; return Task.CompletedTask; });

            var result = await system.UnlockCardAsync(LockedCardId, CancellationToken.None);
            var duplicate = await system.UnlockCardAsync(LockedCardId, CancellationToken.None);
            var snapshot = system.GetSnapshot();

            Assert.That(result.Status, Is.EqualTo(ProgressionTransactionStatus.Success));
            Assert.That(result.GoldSpent, Is.EqualTo(100));
            Assert.That(duplicate.Status, Is.EqualTo(ProgressionTransactionStatus.AlreadyUnlocked));
            Assert.That(saves, Is.EqualTo(1));
            Assert.That(snapshot.Gold, Is.EqualTo(100));
            Assert.That(snapshot.Cards.Single(card => card.Id.Equals(LockedCardId)).Unlocked, Is.True);
        }

        [Test]
        public async Task UnlockCard_WhenGoldIsInsufficient_DoesNotPersistOrMutate()
        {
            var saves = 0;
            var system = await CreateSystem(_ => { saves++; return Task.CompletedTask; });
            var state = (PlayerProgressSaveData)system.CaptureState();
            state.Gold = 50;
            system.RestoreState(state, 1);

            var result = await system.UnlockCardAsync(LockedCardId, CancellationToken.None);

            Assert.That(result.Status, Is.EqualTo(ProgressionTransactionStatus.InsufficientGold));
            Assert.That(saves, Is.Zero);
            Assert.That(system.GetSnapshot().Gold, Is.EqualTo(50));
            Assert.That(system.GetSnapshot().Cards.Single(card => card.Id.Equals(LockedCardId)).Unlocked, Is.False);
        }

        [Test]
        public async Task UpgradeCard_UsesConfiguredPercentageAndStopsAtMaximumLevel()
        {
            var system = await CreateSystem(_ => Task.CompletedTask);

            var first = await system.UpgradeCardAsync(BaseCardId, CancellationToken.None);
            var second = await system.UpgradeCardAsync(BaseCardId, CancellationToken.None);

            Assert.That(first.Status, Is.EqualTo(ProgressionTransactionStatus.Success));
            Assert.That(system.GetAttributeMultiplierBasisPoints(BaseCardId, "health"), Is.EqualTo(10400));
            Assert.That(second.Status, Is.EqualTo(ProgressionTransactionStatus.AtMaxLevel));
        }

        [Test]
        public async Task SaveFailure_RollsBackGoldAndCardState()
        {
            var system = await CreateSystem(_ => throw new InvalidOperationException("disk unavailable"));

            var result = await system.UnlockCardAsync(LockedCardId, CancellationToken.None);
            var snapshot = system.GetSnapshot();

            Assert.That(result.Status, Is.EqualTo(ProgressionTransactionStatus.SaveFailed));
            Assert.That(snapshot.Gold, Is.EqualTo(200));
            Assert.That(snapshot.Cards.Single(card => card.Id.Equals(LockedCardId)).Unlocked, Is.False);
        }

        [Test]
        public async Task UnlockCard_SaveAndReload_RestoresAuthoritativeProgress()
        {
            var directory = Path.Combine(Path.GetTempPath(), "FortressFrontierProgressionTests", Guid.NewGuid().ToString("N"));
            try
            {
                ProgressionSystem first = null;
                SaveCoordinator firstCoordinator = null;
                first = new ProgressionSystem(new TestContent(), token => firstCoordinator.SaveAsync(SaveFileKind.Profile, token));
                firstCoordinator = new SaveCoordinator(directory, "test", () => new ISaveParticipant[] { first });
                await first.InitializeAsync(new GameContext("test"), CancellationToken.None);
                await firstCoordinator.LoadAsync(SaveFileKind.Profile, CancellationToken.None);
                await first.UnlockCardAsync(LockedCardId, CancellationToken.None);

                ProgressionSystem second = null;
                SaveCoordinator secondCoordinator = null;
                second = new ProgressionSystem(new TestContent(), token => secondCoordinator.SaveAsync(SaveFileKind.Profile, token));
                secondCoordinator = new SaveCoordinator(directory, "test", () => new ISaveParticipant[] { second });
                await second.InitializeAsync(new GameContext("test"), CancellationToken.None);
                var load = await secondCoordinator.LoadAsync(SaveFileKind.Profile, CancellationToken.None);

                Assert.That(load.Succeeded, Is.True);
                Assert.That(second.GetSnapshot().Gold, Is.EqualTo(100));
                Assert.That(second.GetSnapshot().Cards.Single(card => card.Id.Equals(LockedCardId)).Unlocked, Is.True);
            }
            finally
            {
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }

        [Test]
        public async Task FirstClear_AtomicallyAdvancesCampaignAndUnlocksNextBattlefield()
        {
            var system = await CreateSystem(_ => Task.CompletedTask);
            var result = await system.SettleMatchAsync(
                new MatchResult(new MatchId("match-first-clear"), new BattlefieldId("battlefield.prologue"),
                    new MapModeId("mode.prologue.peaceful"), true, true),
                new FortressFrontier.Runtime.Content.MatchRewardConfig(10, 20, 30, 1000), CancellationToken.None);

            var snapshot = system.GetSnapshot();
            Assert.That(result.Succeeded, Is.True);
            Assert.That(result.FirstClear, Is.True);
            Assert.That(snapshot.CampaignStageId.Value, Is.EqualTo("stage.river-pass"));
            Assert.That(snapshot.UnlockedBattlefields.Select(value => value.Value),
                Is.EquivalentTo(new[] { "battlefield.prologue", "battlefield.river-pass" }));
            Assert.That(snapshot.FirstClears.Single().Value, Is.EqualTo("battlefield.prologue"));
        }

        [Test]
        public async Task FirstClear_SaveFailureRollsBackCampaignUnlockAndReward()
        {
            var system = await CreateSystem(_ => throw new InvalidOperationException("disk unavailable"));
            var result = await system.SettleMatchAsync(
                new MatchResult(new MatchId("match-save-fail"), new BattlefieldId("battlefield.prologue"),
                    new MapModeId("mode.prologue.peaceful"), true, true),
                new FortressFrontier.Runtime.Content.MatchRewardConfig(10, 20, 30, 1000), CancellationToken.None);

            var snapshot = system.GetSnapshot();
            Assert.That(result.Status, Is.EqualTo(SettlementStatus.SaveFailed));
            Assert.That(snapshot.Gold, Is.EqualTo(200));
            Assert.That(snapshot.CampaignStageId.Value, Is.EqualTo("stage.test"));
            Assert.That(snapshot.UnlockedBattlefields.Single().Value, Is.EqualTo("battlefield.prologue"));
            Assert.That(snapshot.FirstClears, Is.Empty);
        }

        [Test]
        public async Task RewardedAdBonus_IsHalfEligibleGold_ExcludesFirstClear_AndIsIdempotent()
        {
            var saves = 0;
            var system = await CreateSystem(_ => { saves++; return Task.CompletedTask; });
            var receipt = await system.SettleMatchAsync(
                new MatchResult(new MatchId("match-rewarded"), new BattlefieldId("battlefield.prologue"),
                    new MapModeId("mode.prologue.peaceful"), true, true),
                new FortressFrontier.Runtime.Content.MatchRewardConfig(10, 20, 30, 1000), CancellationToken.None);

            var first = await system.ClaimRewardedAdBonusAsync(receipt.MatchId, CancellationToken.None);
            var duplicate = await system.ClaimRewardedAdBonusAsync(receipt.MatchId, CancellationToken.None);

            Assert.That(receipt.RewardedAdBonusGold, Is.EqualTo(15));
            Assert.That(first.Status, Is.EqualTo(RewardedAdBonusClaimStatus.Success));
            Assert.That(first.GoldAwarded, Is.EqualTo(15));
            Assert.That(first.GoldBalance, Is.EqualTo(275));
            Assert.That(duplicate.Status, Is.EqualTo(RewardedAdBonusClaimStatus.AlreadyClaimed));
            Assert.That(duplicate.GoldAwarded, Is.Zero);
            Assert.That(system.GetSnapshot().Gold, Is.EqualTo(275));
            Assert.That(saves, Is.EqualTo(2));
        }

        [Test]
        public async Task RewardedAdBonus_SaveFailure_RollsBackAndCanRetryWithoutAnotherVerification()
        {
            var failBonusSave = false;
            var system = await CreateSystem(_ => failBonusSave
                ? Task.FromException(new IOException("simulated write failure"))
                : Task.CompletedTask);
            var receipt = await system.SettleMatchAsync(
                new MatchResult(new MatchId("match-rewarded-retry"), new BattlefieldId("battlefield.prologue"),
                    new MapModeId("mode.prologue.peaceful"), true, false),
                new FortressFrontier.Runtime.Content.MatchRewardConfig(10, 20, 30, 1000), CancellationToken.None);
            failBonusSave = true;

            var failed = await system.ClaimRewardedAdBonusAsync(receipt.MatchId, CancellationToken.None);
            failBonusSave = false;
            var retried = await system.ClaimRewardedAdBonusAsync(receipt.MatchId, CancellationToken.None);

            Assert.That(failed.Status, Is.EqualTo(RewardedAdBonusClaimStatus.SaveFailed));
            Assert.That(retried.Status, Is.EqualTo(RewardedAdBonusClaimStatus.Success));
            Assert.That(retried.GoldAwarded, Is.EqualTo(5));
            Assert.That(system.GetSnapshot().Gold, Is.EqualTo(215));
        }

        private static async Task<ProgressionSystem> CreateSystem(Func<CancellationToken, Task> save)
        {
            var system = new ProgressionSystem(new TestContent(), save);
            await system.InitializeAsync(new GameContext("test"), CancellationToken.None);
            return system;
        }

        private sealed class TestContent : IProgressionContent
        {
            public int InitialGold => 200;
            public CampaignStageId InitialCampaignStageId => new("stage.test");
            public IReadOnlyList<ProgressionCardDefinition> Cards { get; } = new[]
            {
                new ProgressionCardDefinition(BaseCardId, true, 0, 2, new[] { 40 }, Array.Empty<CardId>(),
                    new Dictionary<string, int>(StringComparer.Ordinal) { ["health"] = 400 }),
                new ProgressionCardDefinition(LockedCardId, false, 100, 3, new[] { 60, 90 }, new[] { BaseCardId },
                    new Dictionary<string, int>(StringComparer.Ordinal) { ["effect"] = 400 })
            };
            public IReadOnlyList<ProgressionStageDefinition> Stages { get; } = new[]
            {
                new ProgressionStageDefinition(new CampaignStageId("stage.test"), null,
                    new[] { new BattlefieldId("battlefield.prologue") }),
                new ProgressionStageDefinition(new CampaignStageId("stage.river-pass"), new CampaignStageId("stage.test"),
                    new[] { new BattlefieldId("battlefield.river-pass") })
            };

            public bool IsCardPurchasable(CampaignStageId stageId, CardId cardId) => stageId.Equals(InitialCampaignStageId);
        }
    }
}
