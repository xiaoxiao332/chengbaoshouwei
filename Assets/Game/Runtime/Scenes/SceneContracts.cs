using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using UnityEngine.SceneManagement;

namespace FortressFrontier.Runtime.Scenes
{
    public interface ISceneLease : IAsyncDisposable
    {
        SceneKey Key { get; }
        Scene Scene { get; }
    }

    public interface ISceneService
    {
        Task<ISceneLease> LoadAdditiveAsync(SceneKey key, CancellationToken cancellationToken);
    }
}
