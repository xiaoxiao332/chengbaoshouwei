using System;
using System.Collections.Generic;
using FortressFrontier.Core.Identifiers;

namespace FortressFrontier.Runtime.Content
{
    public sealed class SelectionBattlefieldDefinition
    {
        public SelectionBattlefieldDefinition(BattlefieldId id, string displayName, IReadOnlyList<MapModeId> modeIds,
            ResourceKey mapArt = default)
        {
            Id = id;
            DisplayName = displayName ?? string.Empty;
            ModeIds = modeIds ?? throw new ArgumentNullException(nameof(modeIds));
            MapArt = mapArt;
        }

        public BattlefieldId Id { get; }
        public string DisplayName { get; }
        public IReadOnlyList<MapModeId> ModeIds { get; }
        public ResourceKey MapArt { get; }
    }

    public interface ISelectionContent
    {
        IReadOnlyList<SelectionBattlefieldDefinition> Battlefields { get; }
        IReadOnlyDictionary<CardId, ResourceKey> CardArt { get; }
    }
}
