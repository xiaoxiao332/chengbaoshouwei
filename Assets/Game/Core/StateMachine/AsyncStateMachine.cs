using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace FortressFrontier.Core.StateMachine
{
    public interface IAsyncState<TStateId> where TStateId : struct, Enum
    {
        TStateId Id { get; }
        Task EnterAsync(TStateId? previousState, CancellationToken cancellationToken);
        Task ExitAsync(TStateId nextState, CancellationToken cancellationToken);
    }

    public sealed class AsyncStateMachine<TStateId> where TStateId : struct, Enum
    {
        private readonly Dictionary<TStateId, IAsyncState<TStateId>> _states = new();
        private readonly SemaphoreSlim _transitionGate = new(1, 1);

        public TStateId? CurrentState { get; private set; }

        public void Register(IAsyncState<TStateId> state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }
            if (!_states.TryAdd(state.Id, state))
            {
                throw new InvalidOperationException($"State already registered: {state.Id}");
            }
        }

        public async Task TransitionAsync(TStateId nextState, CancellationToken cancellationToken)
        {
            await _transitionGate.WaitAsync(cancellationToken);
            try
            {
                if (CurrentState.HasValue && EqualityComparer<TStateId>.Default.Equals(CurrentState.Value, nextState))
                {
                    return;
                }

                if (!_states.TryGetValue(nextState, out var next))
                {
                    throw new KeyNotFoundException($"State is not registered: {nextState}");
                }

                var previousId = CurrentState;
                await next.EnterAsync(previousId, cancellationToken);

                if (previousId.HasValue)
                {
                    await _states[previousId.Value].ExitAsync(nextState, cancellationToken);
                }

                CurrentState = nextState;
            }
            finally
            {
                _transitionGate.Release();
            }
        }
    }
}
