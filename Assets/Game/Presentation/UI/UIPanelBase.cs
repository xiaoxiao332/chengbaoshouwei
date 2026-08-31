using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace FortressFrontier.Presentation.UI
{
    [RequireComponent(typeof(CanvasGroup))]
    public abstract class UIPanelBase : MonoBehaviour
    {
        private CanvasGroup _canvasGroup;
        private bool _isInitialized;

        public bool IsOpen { get; private set; }

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            if (_isInitialized)
            {
                return;
            }

            _canvasGroup = GetComponent<CanvasGroup>();
            await OnInitializeAsync(cancellationToken);
            _isInitialized = true;
            SetVisible(false);
        }

        public async Task OpenAsync(object arguments, CancellationToken cancellationToken)
        {
            await InitializeAsync(cancellationToken);
            SetVisible(true);
            await OnOpenAsync(arguments, cancellationToken);
            IsOpen = true;
        }

        public async Task CloseAsync(CancellationToken cancellationToken)
        {
            if (this == null || !IsOpen)
            {
                return;
            }

            await OnCloseAsync(cancellationToken);
            IsOpen = false;
            if (this != null)
            {
                SetVisible(false);
            }
        }

        public void SetInputEnabled(bool enabled)
        {
            EnsureCanvasGroup();
            _canvasGroup.interactable = enabled;
            _canvasGroup.blocksRaycasts = enabled;
        }

        protected virtual Task OnInitializeAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task OnOpenAsync(object arguments, CancellationToken cancellationToken) => Task.CompletedTask;
        protected virtual Task OnCloseAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        private void SetVisible(bool visible)
        {
            EnsureCanvasGroup();
            _canvasGroup.alpha = visible ? 1f : 0f;
            _canvasGroup.interactable = visible;
            _canvasGroup.blocksRaycasts = visible;
        }

        private void EnsureCanvasGroup()
        {
            if (_canvasGroup == null)
            {
                _canvasGroup = GetComponent<CanvasGroup>();
            }
        }
    }
}
