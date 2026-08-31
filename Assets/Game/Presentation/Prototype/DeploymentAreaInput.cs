using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FortressFrontier.Presentation.Prototype
{
    public sealed class DeploymentAreaInput : MonoBehaviour, IPointerClickHandler
    {
        private Action<Vector2> _clicked;

        public void Bind(Action<Vector2> clicked) => _clicked = clicked;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!isActiveAndEnabled || transform is not RectTransform rect) return;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, eventData.position,
                    eventData.pressEventCamera, out var local)) return;
            var area = rect.rect;
            if (area.width <= 0f || area.height <= 0f) return;
            _clicked?.Invoke(new Vector2(
                Mathf.Clamp01((local.x - area.xMin) / area.width),
                Mathf.Clamp01((local.y - area.yMin) / area.height)));
        }
    }
}
