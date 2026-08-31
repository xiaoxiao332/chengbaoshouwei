using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using UnityEngine.SceneManagement;

namespace FortressFrontier.Runtime.Scenes
{
    public sealed class SceneFlowSystem : GameSystemBase, IGameTickable, IApplicationPauseHandler
    {
        private readonly ISceneService _sceneService;
        private readonly SceneSystemDependencies _dependencies;
        private readonly SemaphoreSlim _transitionGate = new(1, 1);
        private GameContext _context;
        private ISceneLease _activeSceneLease;
        private SystemHost _activeSceneHost;

        public SceneFlowSystem(ISceneService sceneService, SceneSystemDependencies dependencies)
            : base(SystemLifetime.Global)
        {
            _sceneService = sceneService ?? throw new ArgumentNullException(nameof(sceneService));
            _dependencies = dependencies ?? throw new ArgumentNullException(nameof(dependencies));
        }

        public IReadOnlyList<GameSystemBase> ActiveSceneSystems =>
            _activeSceneHost?.Systems ?? Array.Empty<GameSystemBase>();

        protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            _context = context;
            return Task.CompletedTask;
        }

        public async Task TransitionAsync(SceneKey nextScene, CancellationToken cancellationToken)
        {
            await _transitionGate.WaitAsync(cancellationToken);
            ISceneLease pendingLease = null;
            SystemHost pendingHost = null;

            try
            {
                pendingLease = await _sceneService.LoadAdditiveAsync(nextScene, cancellationToken);
                var contexts = pendingLease.Scene.GetRootGameObjects()
                    .SelectMany(root => root.GetComponentsInChildren<SceneContext>(true))
                    .ToArray();

                if (contexts.Length != 1)
                {
                    throw new InvalidOperationException(
                        $"Scene '{nextScene}' must contain exactly one SceneContext; found {contexts.Length}.");
                }

                pendingHost = new SystemHost();
                foreach (var system in contexts[0].CreateSystems(_context, _dependencies))
                {
                    if (system.Lifetime != SystemLifetime.Scene)
                    {
                        throw new InvalidOperationException(
                            $"Scene installer created non-scene system: {system.GetType().FullName}");
                    }

                    pendingHost.Register(system);
                }

                await pendingHost.InitializeAsync(_context, SystemLifetime.Scene, cancellationToken);
                SceneManager.SetActiveScene(pendingLease.Scene);

                var previousLease = _activeSceneLease;
                var previousHost = _activeSceneHost;
                _activeSceneLease = pendingLease;
                _activeSceneHost = pendingHost;
                pendingLease = null;
                pendingHost = null;

                if (previousHost != null)
                {
                    await previousHost.ShutdownAsync(SystemLifetime.Scene, cancellationToken);
                }

                if (previousLease != null)
                {
                    await previousLease.DisposeAsync();
                }
            }
            catch
            {
                if (pendingLease != null)
                {
                    await pendingLease.DisposeAsync();
                }

                if (pendingHost != null)
                {
                    await pendingHost.ShutdownAsync(SystemLifetime.Scene, CancellationToken.None);
                }

                throw;
            }
            finally
            {
                _transitionGate.Release();
            }
        }

        protected override async Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            if (_activeSceneHost != null)
            {
                await _activeSceneHost.ShutdownAsync(SystemLifetime.Scene, cancellationToken);
                _activeSceneHost = null;
            }

            if (_activeSceneLease != null)
            {
                await _activeSceneLease.DisposeAsync();
                _activeSceneLease = null;
            }

            _context = null;
        }

        public void Tick(float deltaTime)
        {
            _activeSceneHost?.Tick(deltaTime);
        }

        public Task OnApplicationPauseAsync(bool isPaused, CancellationToken cancellationToken)
        {
            return _activeSceneHost == null
                ? Task.CompletedTask
                : _activeSceneHost.NotifyApplicationPauseAsync(isPaused, cancellationToken);
        }
    }
}
