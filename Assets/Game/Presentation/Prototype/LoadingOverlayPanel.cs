using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Presentation.UI;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class LoadingOverlayPanel : UIPanelBase
    {
        [SerializeField] private Text _label;
        private float _time;
        protected override Task OnOpenAsync(object arguments, CancellationToken cancellationToken) { _time = 0f; return Task.CompletedTask; }
        private void Update()
        {
            if (!IsOpen || _label == null) return;
            _time += Time.unscaledDeltaTime;
            _label.text = "调度远征" + new string('·', 1 + Mathf.FloorToInt(_time * 2f) % 3);
        }
    }
}
