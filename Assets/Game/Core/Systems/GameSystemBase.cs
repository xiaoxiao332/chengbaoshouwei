using System;
using System.Threading;
using System.Threading.Tasks;

namespace FortressFrontier.Core.Systems
{
    public enum SystemLifetime
    {
        Global,
        Scene
    }

    public abstract class GameSystemBase
    {
        private bool _isInitialized;

        protected GameSystemBase(SystemLifetime lifetime)
        {
            Lifetime = lifetime;
        }

        public SystemLifetime Lifetime { get; }
        public bool IsInitialized => _isInitialized;

        public async Task InitializeAsync(GameContext context, CancellationToken cancellationToken)
        {
            if (_isInitialized)
            {
                return;
            }

            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }
            await OnInitializeAsync(context, cancellationToken);
            _isInitialized = true;
        }

        public async Task ShutdownAsync(CancellationToken cancellationToken)
        {
            if (!_isInitialized)
            {
                return;
            }

            try
            {
                await OnShutdownAsync(cancellationToken);
            }
            finally
            {
                _isInitialized = false;
            }
        }

        protected abstract Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken);

        protected virtual Task OnShutdownAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}
