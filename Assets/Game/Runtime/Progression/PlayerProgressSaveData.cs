using System;
using System.Collections.Generic;

namespace FortressFrontier.Runtime.Progression
{
    [Serializable]
    public sealed class CardProgressSaveData
    {
        public bool Unlocked;
        public int Level;
    }

    [Serializable]
    public sealed class PlayerProgressSaveData
    {
        public int Version = 3;
        public int Gold;
        public string CurrentCampaignStageId;
        public Dictionary<string, CardProgressSaveData> Cards = new(StringComparer.Ordinal);
        public List<string> UnlockedBattlefieldIds = new();
        public string LastSelectedBattlefieldId;
        public Dictionary<string, string> LastSelectedMapModeByBattlefield = new(StringComparer.Ordinal);
        public List<string> FirstClearBattlefieldIds = new();
        public List<string> ClaimedMatchIds = new();
        public List<SettlementReceiptSaveData> SettlementReceipts = new();
    }

    [Serializable]
    public sealed class SettlementReceiptSaveData
    {
        public string MatchId;
        public int GoldAwarded;
        public int GoldBalance;
        public bool FirstClear;
        public int RewardedAdBonusGold;
        public bool RewardedAdBonusClaimed;
    }
}
