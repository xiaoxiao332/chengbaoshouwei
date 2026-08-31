using System;
using System.Collections.Generic;
using FortressFrontier.Core.Systems;
using UnityEngine;

namespace FortressFrontier.Runtime.Scenes
{
    public sealed class SceneContext : MonoBehaviour
    {
        [SerializeField] private SceneSystemInstallerBase[] _installers = Array.Empty<SceneSystemInstallerBase>();

        public IEnumerable<GameSystemBase> CreateSystems(
            GameContext context,
            SceneSystemDependencies dependencies)
        {
            foreach (var installer in _installers)
            {
                if (installer == null)
                {
                    throw new InvalidOperationException($"{name} contains a missing scene installer reference.");
                }

                foreach (var system in installer.CreateSystems(context, dependencies))
                {
                    yield return system;
                }
            }
        }
    }
}
