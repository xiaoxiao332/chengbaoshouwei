using System.Collections.Generic;
using FortressFrontier.Core.Systems;
using UnityEngine;

namespace FortressFrontier.Runtime.Scenes
{
    public abstract class SceneSystemInstallerBase : MonoBehaviour
    {
        public abstract IEnumerable<GameSystemBase> CreateSystems(
            GameContext context,
            SceneSystemDependencies dependencies);
    }
}
