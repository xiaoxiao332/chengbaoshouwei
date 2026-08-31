using System;
using System.Threading;
using System.Threading.Tasks;

namespace FortressFrontier.Infrastructure.Resources
{
    internal static class TaskCancellation
    {
        public static async Task<T> WaitAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return await task.ConfigureAwait(false);
            }

            var cancellationSource = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (cancellationToken.Register(() => cancellationSource.TrySetResult(true)))
            {
                if (task != await Task.WhenAny(task, cancellationSource.Task).ConfigureAwait(false))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task.ConfigureAwait(false);
        }
    }
}
