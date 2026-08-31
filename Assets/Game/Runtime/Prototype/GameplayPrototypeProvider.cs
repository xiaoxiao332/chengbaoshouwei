using System;
using System.Collections.Generic;

namespace FortressFrontier.Runtime.Prototype
{
    public sealed class GameplayPrototypeProvider
    {
        private GameplayCardTab _tab;
        private int _selectedCardIndex;
        private bool _buildingMenuOpen;
        private bool _deploymentGridVisible = true;
        private BlueprintVisualState _blueprintState;
        private bool _choiceOpen;
        private bool _researchOpen;
        private int _itemCount = 4;
        private string _status = "部署网格已开启 · 点击卡牌演示训练状态";
        private IReadOnlyList<string> _resourceGroups = Array.Empty<string>();
        private IReadOnlyList<BuildingSlotViewModel> _buildingSlots = Array.Empty<BuildingSlotViewModel>();
        private int _deployedUnitCount;

        public event Action<GameplayViewModel> Changed;
        public GameplayViewModel Snapshot => new(_tab, _selectedCardIndex, _buildingMenuOpen, _deploymentGridVisible, _blueprintState, _choiceOpen, _researchOpen, _itemCount, _status, _resourceGroups, _buildingSlots, _deployedUnitCount);
        public void SelectTab(GameplayCardTab tab) { _tab = tab; _selectedCardIndex = 0; _status = tab == GameplayCardTab.Soldiers ? "兵种卡：逐兵训练队列" : "道具卡：本局一次性使用"; Publish(); }
        public void SelectCard(int index) { _selectedCardIndex = Math.Max(0, index); _deploymentGridVisible = _tab == GameplayCardTab.Soldiers; _status = _tab == GameplayCardTab.Soldiers ? $"已选择第 {index + 1} 张兵种卡" : $"已选择第 {index + 1} 张道具卡"; Publish(); }
        public void ToggleBuildingMenu() { _buildingMenuOpen = !_buildingMenuOpen; _status = _buildingMenuOpen ? "建筑菜单：升级 / 暂停 / 拆除" : "建筑菜单已收起"; Publish(); }
        public void CycleBlueprintState() { _blueprintState = (BlueprintVisualState)(((int)_blueprintState + 1) % 3); _deploymentGridVisible = true; _status = $"蓝图状态：{_blueprintState}"; Publish(); }
        public void UseItem() { if (_itemCount > 0) _itemCount--; _status = _itemCount > 0 ? "战术道具已消耗" : "道具已用尽"; Publish(); }
        public void ToggleResearch() { _researchOpen = !_researchOpen; _choiceOpen = false; _status = "研究点分配演示"; Publish(); }
        public void ConfirmChoice(int index) { _choiceOpen = false; _status = $"已选择强化方案 {index + 1}"; Publish(); }
        public void SetStatus(string status) { _status = status ?? string.Empty; _choiceOpen = false; _researchOpen = false; Publish(); }
        public void SetP0State(string status, IReadOnlyList<string> resourceGroups, IReadOnlyList<BuildingSlotViewModel> buildingSlots, int deployedUnitCount)
        { _status = status ?? string.Empty; _resourceGroups = resourceGroups ?? Array.Empty<string>(); _buildingSlots = buildingSlots ?? Array.Empty<BuildingSlotViewModel>(); _deployedUnitCount = deployedUnitCount; _choiceOpen = false; _researchOpen = false; Publish(); }
        private void Publish() => Changed?.Invoke(Snapshot);
    }
}
