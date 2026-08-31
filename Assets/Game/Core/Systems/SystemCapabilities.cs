using System.Threading;
using System.Threading.Tasks;

namespace FortressFrontier.Core.Systems
{
    public interface IGameTickable
    {
        void Tick(float deltaTime);
    }

    public interface IApplicationPauseHandler
    {
        Task OnApplicationPauseAsync(bool isPaused, CancellationToken cancellationToken);
    }

    public interface ISceneEnterHandler
    {
        Task OnSceneEnterAsync(CancellationToken cancellationToken);
    }

    public interface ISceneExitHandler
    {
        Task OnSceneExitAsync(CancellationToken cancellationToken);
    }
}
