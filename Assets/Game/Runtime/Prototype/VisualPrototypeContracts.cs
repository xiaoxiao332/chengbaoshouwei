using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Gameplay;
using UnityEngine;
using FortressFrontier.Runtime.Monetization;

namespace FortressFrontier.Runtime.Prototype
{
    public sealed class ReinforcementUnitViewModel
    {
        public ReinforcementUnitViewModel(ResourceKey spriteKey, int quantity)
        { SpriteKey = spriteKey; Quantity = Math.Max(1, quantity); }
        public ResourceKey SpriteKey { get; }
        public int Quantity { get; }
    }

    public enum SelectionCategory { All, Soldiers, Camps, Tactics }
    public enum GameplayCardTab { Soldiers, Items }
    public enum BlueprintVisualState { Valid, Blocked, Building }
    public enum GameplayBuildingCommand { ResumeAfterResourceShortage, Demolish, Upgrade }

    public sealed class SelectionCardViewModel
    {
        public SelectionCardViewModel(CardId id, string name, string subtitle, int level, bool unlocked, int progress,
            int progressMax, ResourceKey artKey = default)
        {
            Id = id; Name = name; Subtitle = subtitle; Level = level; Unlocked = unlocked; Progress = progress;
            ProgressMax = progressMax; ArtKey = artKey;
        }
        public CardId Id { get; }
        public string Name { get; }
        public string Subtitle { get; }
        public int Level { get; }
        public bool Unlocked { get; }
        public int Progress { get; }
        public int ProgressMax { get; }
        public ResourceKey ArtKey { get; }
    }

    public sealed class SelectionViewModel
    {
        public SelectionViewModel(int gold, int expeditionStep, int expeditionMax, SelectionCategory category, IReadOnlyList<SelectionCardViewModel> cards, CardId selectedCardId, BattlefieldId battlefieldId, string battlefieldName, bool battlefieldUnlocked, MapModeId modeId, IReadOnlyList<MapModeId> modeIds,
            ResourceKey mapArt = default, int cardPageIndex = 0, int cardPageCount = 1)
        {
            Gold = gold; ExpeditionStep = expeditionStep; ExpeditionMax = expeditionMax; Category = category; Cards = cards; SelectedCardId = selectedCardId; BattlefieldId = battlefieldId; BattlefieldName = battlefieldName ?? string.Empty; BattlefieldUnlocked = battlefieldUnlocked; ModeId = modeId; ModeIds = modeIds ?? Array.Empty<MapModeId>();
            MapArt = mapArt; CardPageIndex = Math.Max(0, cardPageIndex); CardPageCount = Math.Max(1, cardPageCount);
        }
        public int Gold { get; }
        public int ExpeditionStep { get; }
        public int ExpeditionMax { get; }
        public SelectionCategory Category { get; }
        public IReadOnlyList<SelectionCardViewModel> Cards { get; }
        public CardId SelectedCardId { get; }
        public BattlefieldId BattlefieldId { get; }
        public string BattlefieldName { get; }
        public bool BattlefieldUnlocked { get; }
        public MapModeId ModeId { get; }
        public IReadOnlyList<MapModeId> ModeIds { get; }
        public ResourceKey MapArt { get; }
        public int CardPageIndex { get; }
        public int CardPageCount { get; }
    }

    public interface ISelectionCommands
    {
        void SelectCategory(SelectionCategory category);
        void SelectCard(CardId cardId);
        void SelectBattlefield(BattlefieldId battlefieldId);
        void CycleBattlefield(int direction);
        void CycleCardPage(int direction);
        void SelectMode(MapModeId modeId);
        Task UnlockSelectedCardAsync(CancellationToken cancellationToken);
        Task UpgradeSelectedCardAsync(CancellationToken cancellationToken);
        Task StartMatchAsync(CancellationToken cancellationToken);
        Task OpenSettingsAsync(CancellationToken cancellationToken);
    }

    public interface ISelectionView
    {
        void Bind(ISelectionCommands commands, IGameplaySpriteResolver sprites, SelectionViewModel viewModel);
        void Render(SelectionViewModel viewModel);
    }

    public sealed class GameplayViewModel
    {
        public GameplayViewModel(GameplayCardTab tab, int selectedCardIndex, bool buildingMenuOpen, bool deploymentGridVisible, BlueprintVisualState blueprintState, bool choiceOpen, bool researchOpen, int itemCount, string status,
            IReadOnlyList<string> resourceGroups = null, IReadOnlyList<BuildingSlotViewModel> buildingSlots = null, int deployedUnitCount = 0,
            CardId? selectedCardId = null, IReadOnlyList<GameplayCardViewModel> soldierCards = null,
            IReadOnlyList<GameplayCardViewModel> itemHand = null, IReadOnlyList<ResourceNodeSnapshot> resourceNodes = null,
            IReadOnlyList<GathererSnapshot> gatherers = null, IReadOnlyList<CombatUnitSnapshot> units = null,
            IReadOnlyList<WallSnapshot> walls = null, MatchClockViewModel clock = null, GameplayOfferViewModel offer = null,
            SoldierSelectionSnapshot soldierSelection = null, IReadOnlyList<DeploymentSlotSnapshot> deploymentSlots = null,
            MatchRect deploymentArea = default, string logisticsStatus = "", string enemyIntelStatus = "",
            int selectedBuildingSlotIndex = -1, ResourceKey mapArt = default, int soldierPageIndex = 0,
            int soldierPageCount = 1, ResearchSnapshot research = null, string researchCost = "", string researchReason = "")
        {
            Tab = tab; SelectedCardIndex = selectedCardIndex; BuildingMenuOpen = buildingMenuOpen; DeploymentGridVisible = deploymentGridVisible; BlueprintState = blueprintState; ChoiceOpen = choiceOpen; ResearchOpen = researchOpen; ItemCount = itemCount; Status = status;
            ResourceGroups = resourceGroups ?? Array.Empty<string>();
            BuildingSlots = buildingSlots ?? Array.Empty<BuildingSlotViewModel>();
            DeployedUnitCount = deployedUnitCount;
            SelectedCardId = selectedCardId;
            SoldierCards = soldierCards ?? Array.Empty<GameplayCardViewModel>();
            ItemHand = itemHand ?? Array.Empty<GameplayCardViewModel>();
            ResourceNodes = resourceNodes ?? Array.Empty<ResourceNodeSnapshot>();
            Gatherers = gatherers ?? Array.Empty<GathererSnapshot>();
            Units = units ?? Array.Empty<CombatUnitSnapshot>();
            Walls = walls ?? Array.Empty<WallSnapshot>();
            Clock = clock ?? new MatchClockViewModel(0, 0);
            Offer = offer ?? GameplayOfferViewModel.Empty;
            SoldierSelection = soldierSelection ?? new SoldierSelectionSnapshot(
                new System.Collections.ObjectModel.ReadOnlyDictionary<UnitId, int>(new Dictionary<UnitId, int>()), 0);
            DeploymentSlots = deploymentSlots ?? Array.Empty<DeploymentSlotSnapshot>();
            DeploymentArea = deploymentArea;
            LogisticsStatus = logisticsStatus ?? string.Empty;
            EnemyIntelStatus = enemyIntelStatus ?? string.Empty;
            SelectedBuildingSlotIndex = selectedBuildingSlotIndex;
            MapArt = mapArt; SoldierPageIndex = Math.Max(0, soldierPageIndex); SoldierPageCount = Math.Max(1, soldierPageCount);
            Research = research; ResearchCost = researchCost ?? string.Empty; ResearchReason = researchReason ?? string.Empty;
        }
        public GameplayCardTab Tab { get; }
        public int SelectedCardIndex { get; }
        public bool BuildingMenuOpen { get; }
        public bool DeploymentGridVisible { get; }
        public BlueprintVisualState BlueprintState { get; }
        public bool ChoiceOpen { get; }
        public bool ResearchOpen { get; }
        public int ItemCount { get; }
        public string Status { get; }
        public IReadOnlyList<string> ResourceGroups { get; }
        public IReadOnlyList<BuildingSlotViewModel> BuildingSlots { get; }
        public int DeployedUnitCount { get; }
        public CardId? SelectedCardId { get; }
        public IReadOnlyList<GameplayCardViewModel> SoldierCards { get; }
        public IReadOnlyList<GameplayCardViewModel> ItemHand { get; }
        public IReadOnlyList<ResourceNodeSnapshot> ResourceNodes { get; }
        public IReadOnlyList<GathererSnapshot> Gatherers { get; }
        public IReadOnlyList<CombatUnitSnapshot> Units { get; }
        public IReadOnlyList<WallSnapshot> Walls { get; }
        public MatchClockViewModel Clock { get; }
        public GameplayOfferViewModel Offer { get; }
        public SoldierSelectionSnapshot SoldierSelection { get; }
        public IReadOnlyList<DeploymentSlotSnapshot> DeploymentSlots { get; }
        public MatchRect DeploymentArea { get; }
        public string LogisticsStatus { get; }
        public string EnemyIntelStatus { get; }
        public int SelectedBuildingSlotIndex { get; }
        public ResourceKey MapArt { get; }
        public int SoldierPageIndex { get; }
        public int SoldierPageCount { get; }
        public ResearchSnapshot Research { get; }
        public string ResearchCost { get; }
        public string ResearchReason { get; }
    }

    public sealed class GameplayCardViewModel
    {
        public GameplayCardViewModel(CardId id, CardType type, string name, string details, int count,
            UnitId? unitId = null, bool enabled = true, ResourceKey artKey = default,
            string cost = "", string attributes = "", ReinforcementTemplateId? reinforcementTemplateId = null,
            IReadOnlyList<ReinforcementUnitViewModel> reinforcementUnits = null)
        {
            Id = id; Type = type; Name = name; Details = details; Count = count; UnitId = unitId;
            Enabled = enabled; ArtKey = artKey; Cost = cost ?? string.Empty; Attributes = attributes ?? string.Empty;
            ReinforcementTemplateId = reinforcementTemplateId;
            ReinforcementUnits = reinforcementUnits ?? Array.Empty<ReinforcementUnitViewModel>();
        }
        public CardId Id { get; }
        public CardType Type { get; }
        public string Name { get; }
        public string Details { get; }
        public int Count { get; }
        public UnitId? UnitId { get; }
        public bool Enabled { get; }
        public ResourceKey ArtKey { get; }
        public string Cost { get; }
        public string Attributes { get; }
        public ReinforcementTemplateId? ReinforcementTemplateId { get; }
        public IReadOnlyList<ReinforcementUnitViewModel> ReinforcementUnits { get; }
    }

    public sealed class MatchClockViewModel
    {
        public MatchClockViewModel(int elapsedSeconds, int durationSeconds)
        { ElapsedSeconds = elapsedSeconds; DurationSeconds = durationSeconds; }
        public int ElapsedSeconds { get; }
        public int DurationSeconds { get; }
        public string Text => $"{ElapsedSeconds / 60:00}:{ElapsedSeconds % 60:00}";
    }

    public sealed class GameplayOfferViewModel
    {
        public static GameplayOfferViewModel Empty { get; } = new(false, Array.Empty<GameplayRewardChoiceViewModel>());
        public GameplayOfferViewModel(bool active, IReadOnlyList<GameplayRewardChoiceViewModel> choices, bool replacementMode = false)
        { Active = active; Choices = choices ?? Array.Empty<GameplayRewardChoiceViewModel>(); ReplacementMode = replacementMode; }
        public bool Active { get; }
        public IReadOnlyList<GameplayRewardChoiceViewModel> Choices { get; }
        public bool ReplacementMode { get; }
    }

    public sealed class GameplayRewardChoiceViewModel
    {
        public GameplayRewardChoiceViewModel(RewardChoiceId id, RewardChoiceKind kind, string name, string details,
            IReadOnlyList<ReinforcementUnitViewModel> reinforcementUnits = null,
            RewardRarity rarity = RewardRarity.Common, ResourceKey iconResourceKey = default)
        { Id = id; Kind = kind; Name = name ?? string.Empty; Details = details ?? string.Empty; ReinforcementUnits = reinforcementUnits ?? Array.Empty<ReinforcementUnitViewModel>(); Rarity = rarity; IconResourceKey = iconResourceKey; }
        public RewardChoiceId Id { get; }
        public RewardChoiceKind Kind { get; }
        public string Name { get; }
        public string Details { get; }
        public IReadOnlyList<ReinforcementUnitViewModel> ReinforcementUnits { get; }
        public RewardRarity Rarity { get; }
        public ResourceKey IconResourceKey { get; }
    }

    public sealed class BuildingSlotViewModel
    {
        public BuildingSlotViewModel(string buildingId, int level, BuildingUpgradeState upgradeState,
            ProductionBlockReason blockReason, ResourceKey artKey = default, bool paused = false,
            int upgradeProgressMilli = 0)
        {
            BuildingId = buildingId; Level = level; UpgradeState = upgradeState; BlockReason = blockReason;
            ArtKey = artKey; Paused = paused; UpgradeProgressMilli = Math.Clamp(upgradeProgressMilli, 0, 1000);
        }
        public string BuildingId { get; }
        public int Level { get; }
        public BuildingUpgradeState UpgradeState { get; }
        public ProductionBlockReason BlockReason { get; }
        public ResourceKey ArtKey { get; }
        public bool Paused { get; }
        public int UpgradeProgressMilli { get; }
    }

    public interface IGameplayCommands
    {
        void SelectTab(GameplayCardTab tab);
        void SelectCard(CardId cardId);
        void CancelSelection();
        void UpdateSoldierSelection(UnitId unitId, int count);
        void SubmitDeployment(int worldX, int worldY);
        void PlayBuilding(CardId cardId, int slotIndex);
        void PlaceTower(CardId cardId);
        void UseTactic(CardId cardId);
        void ChooseOffer(RewardChoiceId choiceId);
        void ToggleBuildingMenu();
        void ShowBuildingMenu(int slotIndex);
        void HideBuildingMenu();
        bool ExecuteBuildingCommand(GameplayBuildingCommand command);
        void CycleBlueprintState();
        void ToggleResearch();
        void CycleSoldierPage(int direction);
        void StartResearch(ResearchUpgradeId upgradeId);
        Task ShowResultAsync(CancellationToken cancellationToken);
    }

    public interface IGameplayView
    {
        void Bind(IGameplayCommands commands, IGameplaySpriteResolver sprites, GameplayViewModel viewModel);
        void Render(GameplayViewModel viewModel);
    }

    public interface IGameplaySpriteResolver
    {
        Sprite Resolve(ResourceKey key);
    }

    public sealed class ResultPanelArguments
    {
        public ResultPanelArguments(string title, string summary, Func<CancellationToken, Task> returnCommand)
            : this(title, summary, true, returnCommand, null, null, null) { }
        public ResultPanelArguments(string title, string summary, bool settled,
            Func<CancellationToken, Task> returnCommand, Func<CancellationToken, Task> retryCommand,
            RewardedAdOffer rewardedAdOffer = null,
            Func<CancellationToken, Task<RewardedAdOffer>> watchRewardedAdCommand = null)
        {
            Title = title; Summary = summary; Settled = settled;
            ReturnCommand = returnCommand ?? throw new ArgumentNullException(nameof(returnCommand));
            RetryCommand = retryCommand;
            RewardedAdOffer = rewardedAdOffer;
            WatchRewardedAdCommand = watchRewardedAdCommand;
        }
        public string Title { get; }
        public string Summary { get; }
        public bool Settled { get; }
        public Func<CancellationToken, Task> ReturnCommand { get; }
        public Func<CancellationToken, Task> RetryCommand { get; }
        public RewardedAdOffer RewardedAdOffer { get; }
        public Func<CancellationToken, Task<RewardedAdOffer>> WatchRewardedAdCommand { get; }
    }

    public sealed class FatalErrorPanelArguments
    {
        public FatalErrorPanelArguments(string message) => Message = string.IsNullOrWhiteSpace(message) ? "未知错误" : message;
        public string Message { get; }
    }
}
