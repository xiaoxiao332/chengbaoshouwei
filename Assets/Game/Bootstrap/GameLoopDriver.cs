using UnityEngine;

namespace FortressFrontier.Bootstrap
{
    public sealed class GameLoopDriver : MonoBehaviour
    {
        [SerializeField] private GlobalManager _globalManager;

        private void Update()
        {
            if (_globalManager != null)
            {
                _globalManager.Tick(Time.deltaTime);
            }
        }
    }
}
