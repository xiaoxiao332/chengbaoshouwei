using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FortressFrontier.Presentation.Prototype
{
    [DisallowMultipleComponent]
    public sealed class GameplayHoverTarget : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action _entered;
        private Action _exited;

        public void Bind(Action entered, Action exited)
        {
            _entered = entered;
            _exited = exited;
        }

        public void OnPointerEnter(PointerEventData eventData) => _entered?.Invoke();
        public void OnPointerExit(PointerEventData eventData) => _exited?.Invoke();

        private void OnDestroy()
        {
            _entered = null;
            _exited = null;
        }
    }
}
