using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Saving;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;

namespace FortressFrontier.Runtime.Progression
{
    public sealed class ProgressionSystem : GameSystemBase, ISaveParticipant, IProgressionReader, IProgressionCommands,
        IMatchSettlementService, IRewardedAdBonusService
    {
        private readonly IProgressionContent _content;
        private readonly Func<CancellationToken, Task> _persistProfileAsync;
        private readonly SemaphoreSlim _transactionGate = new(1, 1);
        private Dictionary<CardId, ProgressionCardDefinition> _definitions;
        private Dictionary<CampaignStageId, ProgressionStageDefinition> _stages;
        private PlayerProgressSaveData _state;

        public ProgressionSystem(IProgressionContent content, Func<CancellationToken, Task> persistProfileAsync)
            : base(SystemLifetime.Global)
        {
            _content = content ?? throw new ArgumentNullException(nameof(content));
            _persistProfileAsync = persistProfileAsync ?? throw new ArgumentNullException(nameof(persistProfileAsync));
        }

        public SaveFileKind FileKind => SaveFileKind.Profile;
        public string SectionKey => "player-progress";
        public int SectionVersion => 3;
        public Type StateType => typeof(PlayerProgressSaveData);

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _definitions = _content.Cards.ToDictionary(card => card.Id);
            _stages = _content.Stages.ToDictionary(stage => stage.Id);
            _state ??= CreateDefault();
            Normalize(_state);
            return Task.CompletedTask;
        }

        protected override Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            _definitions = null;
            _stages = null;
            return Task.CompletedTask;
        }

        public ProgressionSnapshot GetSnapshot()
        {
            EnsureInitialized();
            var cards = _definitions.Keys.OrderBy(id => id.Value, StringComparer.Ordinal)
                .Select(id =>
                {
                    var progress = _state.Cards[id.Value];
                    return new CardProgressSnapshot(id, progress.Unlocked, progress.Level);
                }).ToArray();
            return new ProgressionSnapshot(_state.Gold, new CampaignStageId(_state.CurrentCampaignStageId), cards,
                _state.UnlockedBattlefieldIds.OrderBy(value => value, StringComparer.Ordinal).Select(value => new BattlefieldId(value)).ToArray(),
                _state.FirstClearBattlefieldIds.OrderBy(value => value, StringComparer.Ordinal).Select(value => new BattlefieldId(value)).ToArray());
        }

        public int GetAttributeMultiplierBasisPoints(CardId cardId, string propertyKey)
        {
            EnsureInitialized();
            if (!_definitions.TryGetValue(cardId, out var definition)) throw new KeyNotFoundException($"Unknown card: '{cardId}'.");
            if (string.IsNullOrWhiteSpace(propertyKey) || !definition.GrowthBasisPoints.TryGetValue(propertyKey, out var growth)) return 10000;
            var level = _state.Cards[cardId.Value].Level;
            return 10000 + Math.Max(0, level - 1) * growth;
        }

        public Task<ProgressionTransactionResult> UnlockCardAsync(CardId cardId, CancellationToken cancellationToken) =>
            ExecuteTransactionAsync(cardId, true, cancellationToken);

        public Task<ProgressionTransactionResult> UpgradeCardAsync(CardId cardId, CancellationToken cancellationToken) =>
            ExecuteTransactionAsync(cardId, false, cancellationToken);

        public async Task<SettlementReceipt> SettleMatchAsync(
            MatchResult result, MatchRewardConfig reward, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            if (reward == null) throw new ArgumentNullException(nameof(reward));
            await _transactionGate.WaitAsync(cancellationToken);
            try
            {
                var existing = _state.SettlementReceipts
                    .FirstOrDefault(value => string.Equals(value.MatchId, result.MatchId.Value, StringComparison.Ordinal));
                if (existing != null)
                    return ToReceipt(existing, true, SettlementStatus.Success);

                if (_state.ClaimedMatchIds.Contains(result.MatchId.Value, StringComparer.Ordinal))
                    return new SettlementReceipt(result.MatchId, 0, _state.Gold, false, true, SettlementStatus.Success);

                var previous = Clone(_state);
                var firstClear = result.Victory && !_state.FirstClearBattlefieldIds.Contains(result.BattlefieldId.Value, StringComparer.Ordinal);
                var eligibleBaseGold = (result.Completed ? reward.CompletionGold : 0)
                    + (result.Victory ? reward.VictoryGold : 0);
                var baseGold = eligibleBaseGold + (firstClear ? reward.FirstClearGold : 0);
                var awarded = (baseGold * reward.ModeMultiplierMilli + 500) / 1000;
                var eligibleAwarded = (eligibleBaseGold * reward.ModeMultiplierMilli + 500) / 1000;
                var rewardedAdBonus = (eligibleAwarded + 1) / 2;
                _state.Gold += awarded;
                if (firstClear)
                {
                    _state.FirstClearBattlefieldIds.Add(result.BattlefieldId.Value);
                    AdvanceCampaignAfterFirstClear(result.BattlefieldId);
                }
                _state.ClaimedMatchIds.Add(result.MatchId.Value);
                var savedReceipt = new SettlementReceiptSaveData
                {
                    MatchId = result.MatchId.Value,
                    GoldAwarded = awarded,
                    GoldBalance = _state.Gold,
                    FirstClear = firstClear,
                    RewardedAdBonusGold = rewardedAdBonus,
                    RewardedAdBonusClaimed = false
                };
                _state.SettlementReceipts.Add(savedReceipt);
                TrimSettlementHistory(_state);

                try
                {
                    await _persistProfileAsync(cancellationToken);
                    return ToReceipt(savedReceipt, false, SettlementStatus.Success);
                }
                catch (OperationCanceledException)
                {
                    _state = previous;
                    throw;
                }
                catch
                {
                    _state = previous;
                    return new SettlementReceipt(result.MatchId, 0, previous.Gold, false, false, SettlementStatus.SaveFailed);
                }
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        public async Task<RewardedAdBonusClaimResult> ClaimRewardedAdBonusAsync(
            MatchId matchId, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            await _transactionGate.WaitAsync(cancellationToken);
            try
            {
                var receipt = _state.SettlementReceipts.FirstOrDefault(value =>
                    string.Equals(value.MatchId, matchId.Value, StringComparison.Ordinal));
                if (receipt == null)
                    return new RewardedAdBonusClaimResult(RewardedAdBonusClaimStatus.MatchNotFound, 0, _state.Gold);
                if (receipt.RewardedAdBonusClaimed)
                    return new RewardedAdBonusClaimResult(RewardedAdBonusClaimStatus.AlreadyClaimed, 0, _state.Gold);
                if (receipt.RewardedAdBonusGold <= 0)
                    return new RewardedAdBonusClaimResult(RewardedAdBonusClaimStatus.NotEligible, 0, _state.Gold);

                var previous = Clone(_state);
                var awarded = receipt.RewardedAdBonusGold;
                _state.Gold += awarded;
                receipt.GoldBalance = _state.Gold;
                receipt.RewardedAdBonusClaimed = true;
                try
                {
                    await _persistProfileAsync(cancellationToken);
                    return new RewardedAdBonusClaimResult(RewardedAdBonusClaimStatus.Success, awarded, _state.Gold);
                }
                catch (OperationCanceledException)
                {
                    _state = previous;
                    throw;
                }
                catch
                {
                    _state = previous;
                    return new RewardedAdBonusClaimResult(RewardedAdBonusClaimStatus.SaveFailed, 0, _state.Gold);
                }
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        public object CaptureState()
        {
            EnsureInitialized();
            return Clone(_state);
        }

        public object CreateDefaultState() => CreateDefault();

        public void RestoreState(object state, int storedVersion)
        {
            _state = state as PlayerProgressSaveData ?? CreateDefault();
            Normalize(_state);
        }

        private async Task<ProgressionTransactionResult> ExecuteTransactionAsync(CardId cardId, bool unlock, CancellationToken cancellationToken)
        {
            EnsureInitialized();
            await _transactionGate.WaitAsync(cancellationToken);
            try
            {
                var validation = unlock ? ValidateUnlock(cardId, out var cost) : ValidateUpgrade(cardId, out cost);
                if (validation != ProgressionTransactionStatus.Success) return new ProgressionTransactionResult(validation, 0);

                var previous = Clone(_state);
                _state.Gold -= cost;
                var progress = _state.Cards[cardId.Value];
                if (unlock) { progress.Unlocked = true; progress.Level = 1; }
                else progress.Level++;

                try
                {
                    await _persistProfileAsync(cancellationToken);
                    return new ProgressionTransactionResult(ProgressionTransactionStatus.Success, cost);
                }
                catch (OperationCanceledException)
                {
                    _state = previous;
                    throw;
                }
                catch
                {
                    _state = previous;
                    return new ProgressionTransactionResult(ProgressionTransactionStatus.SaveFailed, 0);
                }
            }
            finally
            {
                _transactionGate.Release();
            }
        }

        private ProgressionTransactionStatus ValidateUnlock(CardId cardId, out int cost)
        {
            cost = 0;
            if (!_definitions.TryGetValue(cardId, out var definition)) return ProgressionTransactionStatus.UnknownCard;
            var progress = _state.Cards[cardId.Value];
            if (progress.Unlocked) return ProgressionTransactionStatus.AlreadyUnlocked;
            if (!_content.IsCardPurchasable(new CampaignStageId(_state.CurrentCampaignStageId), cardId)) return ProgressionTransactionStatus.StageLocked;
            if (definition.Prerequisites.Any(required => !_state.Cards.TryGetValue(required.Value, out var requiredProgress) || !requiredProgress.Unlocked))
                return ProgressionTransactionStatus.PrerequisiteMissing;
            cost = definition.UnlockGoldCost;
            return _state.Gold < cost ? ProgressionTransactionStatus.InsufficientGold : ProgressionTransactionStatus.Success;
        }

        private ProgressionTransactionStatus ValidateUpgrade(CardId cardId, out int cost)
        {
            cost = 0;
            if (!_definitions.TryGetValue(cardId, out var definition)) return ProgressionTransactionStatus.UnknownCard;
            var progress = _state.Cards[cardId.Value];
            if (!progress.Unlocked) return ProgressionTransactionStatus.PrerequisiteMissing;
            if (progress.Level >= definition.MaxLevel) return ProgressionTransactionStatus.AtMaxLevel;
            cost = definition.UpgradeGoldCosts[progress.Level - 1];
            return _state.Gold < cost ? ProgressionTransactionStatus.InsufficientGold : ProgressionTransactionStatus.Success;
        }

        private PlayerProgressSaveData CreateDefault()
        {
            var state = new PlayerProgressSaveData
            {
                Gold = _content.InitialGold,
                CurrentCampaignStageId = _content.InitialCampaignStageId.Value
            };
            foreach (var card in _content.Cards)
                state.Cards[card.Id.Value] = new CardProgressSaveData { Unlocked = card.DefaultUnlocked, Level = card.DefaultUnlocked ? 1 : 0 };
            UnlockStageBattlefields(state, _content.InitialCampaignStageId);
            return state;
        }

        private void Normalize(PlayerProgressSaveData state)
        {
            state.Cards = state.Cards == null
                ? new Dictionary<string, CardProgressSaveData>(StringComparer.Ordinal)
                : new Dictionary<string, CardProgressSaveData>(state.Cards, StringComparer.Ordinal);
            state.CurrentCampaignStageId = string.IsNullOrWhiteSpace(state.CurrentCampaignStageId)
                ? _content.InitialCampaignStageId.Value
                : state.CurrentCampaignStageId;
            foreach (var definition in _content.Cards)
            {
                if (!state.Cards.TryGetValue(definition.Id.Value, out var progress) || progress == null)
                {
                    state.Cards[definition.Id.Value] = new CardProgressSaveData
                    {
                        Unlocked = definition.DefaultUnlocked,
                        Level = definition.DefaultUnlocked ? 1 : 0
                    };
                    continue;
                }
                progress.Level = progress.Unlocked ? Math.Clamp(progress.Level, 1, definition.MaxLevel) : 0;
            }
            state.UnlockedBattlefieldIds ??= new List<string>();
            UnlockStageBattlefields(state, new CampaignStageId(state.CurrentCampaignStageId));
            state.LastSelectedMapModeByBattlefield ??= new Dictionary<string, string>(StringComparer.Ordinal);
            state.FirstClearBattlefieldIds ??= new List<string>();
            state.ClaimedMatchIds ??= new List<string>();
            state.SettlementReceipts ??= new List<SettlementReceiptSaveData>();
            state.SettlementReceipts.RemoveAll(value => value == null || string.IsNullOrWhiteSpace(value.MatchId));
            TrimSettlementHistory(state);
            state.Version = 3;
        }

        private static PlayerProgressSaveData Clone(PlayerProgressSaveData source)
        {
            return new PlayerProgressSaveData
            {
                Version = source.Version,
                Gold = source.Gold,
                CurrentCampaignStageId = source.CurrentCampaignStageId,
                Cards = source.Cards.ToDictionary(pair => pair.Key, pair => new CardProgressSaveData { Unlocked = pair.Value.Unlocked, Level = pair.Value.Level }, StringComparer.Ordinal),
                UnlockedBattlefieldIds = new List<string>(source.UnlockedBattlefieldIds),
                LastSelectedBattlefieldId = source.LastSelectedBattlefieldId,
                LastSelectedMapModeByBattlefield = new Dictionary<string, string>(source.LastSelectedMapModeByBattlefield, StringComparer.Ordinal),
                FirstClearBattlefieldIds = new List<string>(source.FirstClearBattlefieldIds),
                ClaimedMatchIds = new List<string>(source.ClaimedMatchIds),
                SettlementReceipts = source.SettlementReceipts.Select(value => new SettlementReceiptSaveData
                {
                    MatchId = value.MatchId,
                    GoldAwarded = value.GoldAwarded,
                    GoldBalance = value.GoldBalance,
                    FirstClear = value.FirstClear,
                    RewardedAdBonusGold = value.RewardedAdBonusGold,
                    RewardedAdBonusClaimed = value.RewardedAdBonusClaimed
                }).ToList()
            };
        }

        private static SettlementReceipt ToReceipt(SettlementReceiptSaveData value, bool duplicate, SettlementStatus status) =>
            new(new MatchId(value.MatchId), value.GoldAwarded, value.GoldBalance, value.FirstClear, duplicate, status,
                value.RewardedAdBonusGold, value.RewardedAdBonusClaimed);

        private static void TrimSettlementHistory(PlayerProgressSaveData state)
        {
            const int maximumReceiptCount = 256;
            if (state.SettlementReceipts.Count > maximumReceiptCount)
                state.SettlementReceipts.RemoveRange(0, state.SettlementReceipts.Count - maximumReceiptCount);
            var receiptIds = new HashSet<string>(state.SettlementReceipts.Select(value => value.MatchId), StringComparer.Ordinal);
            var claimed = state.ClaimedMatchIds
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            foreach (var id in receiptIds)
                if (!claimed.Contains(id, StringComparer.Ordinal)) claimed.Add(id);
            if (claimed.Count > maximumReceiptCount)
                claimed.RemoveRange(0, claimed.Count - maximumReceiptCount);
            state.ClaimedMatchIds = claimed;
        }

        private void EnsureInitialized()
        {
            if (!IsInitialized || _definitions == null || _stages == null || _state == null) throw new InvalidOperationException("ProgressionSystem is not initialized.");
        }

        private void AdvanceCampaignAfterFirstClear(BattlefieldId battlefieldId)
        {
            var currentStageId = new CampaignStageId(_state.CurrentCampaignStageId);
            if (!_stages.TryGetValue(currentStageId, out var currentStage)
                || !currentStage.UnlockedBattlefields.Contains(battlefieldId)) return;
            var next = _content.Stages
                .Where(stage => stage.PrerequisiteStageId.HasValue && stage.PrerequisiteStageId.Value.Equals(currentStageId))
                .OrderBy(stage => stage.Id.Value, StringComparer.Ordinal)
                .FirstOrDefault();
            if (next == null) return;
            _state.CurrentCampaignStageId = next.Id.Value;
            UnlockStageBattlefields(_state, next.Id);
        }

        private void UnlockStageBattlefields(PlayerProgressSaveData state, CampaignStageId stageId)
        {
            if (_stages == null || !_stages.TryGetValue(stageId, out var stage)) return;
            foreach (var battlefield in stage.UnlockedBattlefields)
                if (!state.UnlockedBattlefieldIds.Contains(battlefield.Value, StringComparer.Ordinal))
                    state.UnlockedBattlefieldIds.Add(battlefield.Value);
        }
    }
}
