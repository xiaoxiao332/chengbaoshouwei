using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class FatalErrorPanel : UIPanelBase
    {
        [SerializeField] private Text _message;
        [SerializeField] private Button _closeButton;
        protected override Task OnInitializeAsync(CancellationToken cancellationToken)
        {
            _closeButton.onClick.AddListener(Close);
            return Task.CompletedTask;
        }
        protected override Task OnOpenAsync(object arguments, CancellationToken cancellationToken)
        {
            if (arguments is FatalErrorPanelArguments error) _message.text = error.Message;
            return Task.CompletedTask;
        }
        private async void Close() => await CloseAsync(CancellationToken.None);
    }
}
