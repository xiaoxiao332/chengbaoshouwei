using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FortressFrontier.Core.Systems
{
    public sealed class SystemHost
    {
        private readonly List<GameSystemBase> _systems = new();

        public IReadOnlyList<GameSystemBase> Systems => _systems;

        public void Register(GameSystemBase system)
        {
            if (system == null)
            {
                throw new ArgumentNullException(nameof(system));
            }

            if (_systems.Any(existing => existing.GetType() == system.GetType()))
            {
                throw new InvalidOperationException($"System type already registered: {system.GetType().FullName}");
            }

            _systems.Add(system);
        }

        public async Task InitializeAsync(
            GameContext context,
            SystemLifetime lifetime,
            CancellationToken cancellationToken)
        {
            var initializedThisCall = new List<GameSystemBase>();

            try
            {
                foreach (var system in _systems.Where(system => system.Lifetime == lifetime))
                {
                    var wasInitialized = system.IsInitialized;
                    await system.InitializeAsync(context, cancellationToken);
                    if (!wasInitialized && system.IsInitialized)
                    {
                        initializedThisCall.Add(system);
                    }

                    if (system is ISceneEnterHandler enterHandler)
                    {
                        await enterHandler.OnSceneEnterAsync(cancellationToken);
                    }
                }
            }
            catch
            {
                for (var index = initializedThisCall.Count - 1; index >= 0; index--)
                {
                    try
                    {
                        await initializedThisCall[index].ShutdownAsync(CancellationToken.None);
                    }
                    catch
                    {
                        // Preserve the original initialization exception.
                    }
                }

                throw;
            }
        }

        public async Task ShutdownAsync(SystemLifetime lifetime, CancellationToken cancellationToken)
        {
            for (var index = _systems.Count - 1; index >= 0; index--)
            {
                var system = _systems[index];
                if (system.Lifetime == lifetime)
                {
                    if (system is ISceneExitHandler exitHandler)
                    {
                        await exitHandler.OnSceneExitAsync(cancellationToken);
                    }
                    await system.ShutdownAsync(cancellationToken);
                }
            }
        }

        public async Task NotifyApplicationPauseAsync(bool isPaused, CancellationToken cancellationToken)
        {
            foreach (var handler in _systems.OfType<IApplicationPauseHandler>())
            {
                await handler.OnApplicationPauseAsync(isPaused, cancellationToken);
            }
        }

        public void Tick(float deltaTime)
        {
            foreach (var tickable in _systems.OfType<IGameTickable>())
            {
                tickable.Tick(deltaTime);
            }
        }
    }
}
