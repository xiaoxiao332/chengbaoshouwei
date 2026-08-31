using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Scenes;
using FortressFrontier.Presentation.Prototype;

namespace FortressFrontier.Bootstrap
{
    public sealed class SelectionInstaller : SceneSystemInstallerBase
    {
        public override IEnumerable<GameSystemBase> CreateSystems(GameContext context, SceneSystemDependencies dependencies)
        {
            var sprites = new GameplaySpriteAssetSystem(dependencies.Resources,
                dependencies.SelectionContent.Battlefields.Select(value => value.MapArt)
                    .Concat(dependencies.SelectionContent.CardArt.Values));
            yield return sprites;
            yield return new SelectionVisualSystem(dependencies.Panels, dependencies.ApplicationFlow, dependencies.Progression,
                dependencies.ProgressionCommands, dependencies.SelectionContent, sprites, dependencies.SettingsOverlay);
        }
    }
}
