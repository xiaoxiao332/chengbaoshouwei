using System;
using FortressFrontier.Runtime.Resources;
using FortressFrontier.Runtime.UI;
using FortressFrontier.Runtime.Flow;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Monetization;
using FortressFrontier.Runtime.Audio;

namespace FortressFrontier.Runtime.Scenes
{
    public sealed class SceneSystemDependencies
    {
        public SceneSystemDependencies(IResourceService resources, IPanelService panels, IApplicationFlow applicationFlow,
            IMatchContent matchContent, IProgressionReader progression, IMatchSettlementService settlement,
            IMatchSessionContext matchSession, IProgressionCommands progressionCommands = null,
            ISelectionContent selectionContent = null, ISettingsOverlay settingsOverlay = null,
            RewardedAdSystem rewardedAds = null, IAudioPlaybackService audio = null)
        {
            Resources = resources ?? throw new ArgumentNullException(nameof(resources));
            Panels = panels ?? throw new ArgumentNullException(nameof(panels));
            ApplicationFlow = applicationFlow ?? throw new ArgumentNullException(nameof(applicationFlow));
            MatchContent = matchContent ?? throw new ArgumentNullException(nameof(matchContent));
            Progression = progression ?? throw new ArgumentNullException(nameof(progression));
            Settlement = settlement ?? throw new ArgumentNullException(nameof(settlement));
            MatchSession = matchSession ?? throw new ArgumentNullException(nameof(matchSession));
            ProgressionCommands = progressionCommands ?? progression as IProgressionCommands;
            SelectionContent = selectionContent ?? matchContent as ISelectionContent;
            SettingsOverlay = settingsOverlay;
            RewardedAds = rewardedAds;
            Audio = audio;
        }

        public IResourceService Resources { get; }
        public IPanelService Panels { get; }
        public IApplicationFlow ApplicationFlow { get; }
        public IMatchContent MatchContent { get; }
        public IProgressionReader Progression { get; }
        public IProgressionCommands ProgressionCommands { get; }
        public ISelectionContent SelectionContent { get; }
        public ISettingsOverlay SettingsOverlay { get; }
        public IMatchSettlementService Settlement { get; }
        public IMatchSessionContext MatchSession { get; }
        public RewardedAdSystem RewardedAds { get; }
        public IAudioPlaybackService Audio { get; }
    }
}
