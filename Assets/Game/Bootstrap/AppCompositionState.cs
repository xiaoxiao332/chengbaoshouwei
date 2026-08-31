using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.StateMachine;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Scenes;

namespace FortressFrontier.Bootstrap
{
    internal sealed class AppCompositionState : IAsyncState<AppStateId>
    {
        private readonly SceneFlowSystem _sceneFlow;
        private readonly UIManager _uiManager;
        private readonly SceneKey? _sceneKey;
        private readonly UIStateId? _uiState;
        private readonly Func<AppStateId, UIStateId?> _resolveUiState;

        public AppCompositionState(
            AppStateId id,
            SceneFlowSystem sceneFlow,
            UIManager uiManager,
            SceneKey? sceneKey,
            UIStateId? uiState,
            Func<AppStateId, UIStateId?> resolveUiState)
        {
            Id = id;
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));
            _uiManager = uiManager ?? throw new ArgumentNullException(nameof(uiManager));
            _sceneKey = sceneKey;
            _uiState = uiState;
            _resolveUiState = resolveUiState ?? throw new ArgumentNullException(nameof(resolveUiState));
        }

        public AppStateId Id { get; }

        public async Task EnterAsync(AppStateId? previousState, CancellationToken cancellationToken)
        {
            if (_uiState.HasValue)
            {
                await _uiManager.TransitionAsync(_uiState.Value, cancellationToken);
            }

            try
            {
                if (_sceneKey.HasValue)
                {
                    await _sceneFlow.TransitionAsync(_sceneKey.Value, cancellationToken);
                }
            }
            catch
            {
                if (previousState.HasValue)
                {
                    var previousUi = _resolveUiState(previousState.Value);
                    if (previousUi.HasValue)
                    {
                        await _uiManager.TransitionAsync(previousUi.Value, CancellationToken.None);
                    }
                }
                throw;
            }
        }

        public Task ExitAsync(AppStateId nextState, CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
