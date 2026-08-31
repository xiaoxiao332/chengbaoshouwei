using System;
using System.Collections.Generic;
using FortressFrontier.Core.Identifiers;

namespace FortressFrontier.Runtime.Progression
{
    public sealed class ProgressionCardDefinition
    {
        public ProgressionCardDefinition(CardId id, bool defaultUnlocked, int unlockGoldCost, int maxLevel,
            IReadOnlyList<int> upgradeGoldCosts, IReadOnlyList<CardId> prerequisites,
            IReadOnlyDictionary<string, int> growthBasisPoints)
        {
            Id = id;
            DefaultUnlocked = defaultUnlocked;
            UnlockGoldCost = unlockGoldCost;
            MaxLevel = maxLevel;
            UpgradeGoldCosts = upgradeGoldCosts ?? throw new ArgumentNullException(nameof(upgradeGoldCosts));
            Prerequisites = prerequisites ?? throw new ArgumentNullException(nameof(prerequisites));
            GrowthBasisPoints = growthBasisPoints ?? throw new ArgumentNullException(nameof(growthBasisPoints));
        }

        public CardId Id { get; }
        public bool DefaultUnlocked { get; }
        public int UnlockGoldCost { get; }
        public int MaxLevel { get; }
        public IReadOnlyList<int> UpgradeGoldCosts { get; }
        public IReadOnlyList<CardId> Prerequisites { get; }
        public IReadOnlyDictionary<string, int> GrowthBasisPoints { get; }
    }

    public interface IProgressionContent
    {
        int InitialGold { get; }
        CampaignStageId InitialCampaignStageId { get; }
        IReadOnlyList<ProgressionCardDefinition> Cards { get; }
        IReadOnlyList<ProgressionStageDefinition> Stages { get; }
        bool IsCardPurchasable(CampaignStageId stageId, CardId cardId);
    }

    public sealed class ProgressionStageDefinition
    {
        public ProgressionStageDefinition(CampaignStageId id, CampaignStageId? prerequisiteStageId,
            IReadOnlyList<BattlefieldId> unlockedBattlefields)
        {
            Id = id;
            PrerequisiteStageId = prerequisiteStageId;
            UnlockedBattlefields = unlockedBattlefields ?? throw new ArgumentNullException(nameof(unlockedBattlefields));
        }

        public CampaignStageId Id { get; }
        public CampaignStageId? PrerequisiteStageId { get; }
        public IReadOnlyList<BattlefieldId> UnlockedBattlefields { get; }
    }

    public enum ProgressionTransactionStatus
    {
        Success,
        InsufficientGold,
        PrerequisiteMissing,
        AlreadyUnlocked,
        AtMaxLevel,
        UnknownCard,
        StageLocked,
        SaveFailed
    }

    public readonly struct ProgressionTransactionResult
    {
        public ProgressionTransactionResult(ProgressionTransactionStatus status, int goldSpent)
        {
            Status = status;
            GoldSpent = goldSpent;
        }
        public ProgressionTransactionStatus Status { get; }
        public int GoldSpent { get; }
        public bool Succeeded => Status == ProgressionTransactionStatus.Success;
    }

    public sealed class CardProgressSnapshot
    {
        public CardProgressSnapshot(CardId id, bool unlocked, int level)
        {
            Id = id;
            Unlocked = unlocked;
            Level = level;
        }
        public CardId Id { get; }
        public bool Unlocked { get; }
        public int Level { get; }
    }

    public sealed class ProgressionSnapshot
    {
        public ProgressionSnapshot(int gold, CampaignStageId stageId, IReadOnlyList<CardProgressSnapshot> cards,
            IReadOnlyList<BattlefieldId> unlockedBattlefields, IReadOnlyList<BattlefieldId> firstClears)
        {
            Gold = gold;
            CampaignStageId = stageId;
            Cards = cards;
            UnlockedBattlefields = unlockedBattlefields ?? throw new ArgumentNullException(nameof(unlockedBattlefields));
            FirstClears = firstClears ?? throw new ArgumentNullException(nameof(firstClears));
        }
        public int Gold { get; }
        public CampaignStageId CampaignStageId { get; }
        public IReadOnlyList<CardProgressSnapshot> Cards { get; }
        public IReadOnlyList<BattlefieldId> UnlockedBattlefields { get; }
        public IReadOnlyList<BattlefieldId> FirstClears { get; }
    }

    public readonly struct MatchResult
    {
        public MatchResult(MatchId matchId, BattlefieldId battlefieldId, MapModeId mapModeId, bool completed, bool victory)
        {
            MatchId = matchId;
            BattlefieldId = battlefieldId;
            MapModeId = mapModeId;
            Completed = completed;
            Victory = victory;
        }

        public MatchId MatchId { get; }
        public BattlefieldId BattlefieldId { get; }
        public MapModeId MapModeId { get; }
        public bool Completed { get; }
        public bool Victory { get; }
    }

    public enum SettlementStatus
    {
        Success,
        SaveFailed
    }

    public readonly struct SettlementReceipt
    {
        public SettlementReceipt(MatchId matchId, int goldAwarded, int goldBalance, bool firstClear, bool duplicate, SettlementStatus status,
            int rewardedAdBonusGold = 0, bool rewardedAdBonusClaimed = false)
        {
            MatchId = matchId;
            GoldAwarded = goldAwarded;
            GoldBalance = goldBalance;
            FirstClear = firstClear;
            Duplicate = duplicate;
            Status = status;
            RewardedAdBonusGold = Math.Max(0, rewardedAdBonusGold);
            RewardedAdBonusClaimed = rewardedAdBonusClaimed;
        }

        public MatchId MatchId { get; }
        public int GoldAwarded { get; }
        public int GoldBalance { get; }
        public bool FirstClear { get; }
        public bool Duplicate { get; }
        public SettlementStatus Status { get; }
        public int RewardedAdBonusGold { get; }
        public bool RewardedAdBonusClaimed { get; }
        public bool Succeeded => Status == SettlementStatus.Success;
    }

    public enum RewardedAdBonusClaimStatus
    {
        Success,
        AlreadyClaimed,
        MatchNotFound,
        NotEligible,
        SaveFailed
    }

    public readonly struct RewardedAdBonusClaimResult
    {
        public RewardedAdBonusClaimResult(RewardedAdBonusClaimStatus status, int goldAwarded, int goldBalance)
        { Status = status; GoldAwarded = goldAwarded; GoldBalance = goldBalance; }
        public RewardedAdBonusClaimStatus Status { get; }
        public int GoldAwarded { get; }
        public int GoldBalance { get; }
        public bool Succeeded => Status == RewardedAdBonusClaimStatus.Success || Status == RewardedAdBonusClaimStatus.AlreadyClaimed;
    }

    public interface IRewardedAdBonusService
    {
        System.Threading.Tasks.Task<RewardedAdBonusClaimResult> ClaimRewardedAdBonusAsync(
            MatchId matchId, System.Threading.CancellationToken cancellationToken);
    }

    public interface IProgressionReader
    {
        ProgressionSnapshot GetSnapshot();
    }

    public interface IProgressionCommands
    {
        System.Threading.Tasks.Task<ProgressionTransactionResult> UnlockCardAsync(CardId cardId,
            System.Threading.CancellationToken cancellationToken);
        System.Threading.Tasks.Task<ProgressionTransactionResult> UpgradeCardAsync(CardId cardId,
            System.Threading.CancellationToken cancellationToken);
    }

    public interface IMatchSettlementService
    {
        System.Threading.Tasks.Task<SettlementReceipt> SettleMatchAsync(
            MatchResult result,
            Content.MatchRewardConfig reward,
            System.Threading.CancellationToken cancellationToken);
    }
}
