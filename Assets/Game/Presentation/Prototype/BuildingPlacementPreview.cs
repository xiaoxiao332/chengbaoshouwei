using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    [DisallowMultipleComponent]
    public sealed class BuildingPlacementPreview : MonoBehaviour
    {
        [SerializeField] private RectTransform _previewRoot;
        [SerializeField] private Image _image;
        [SerializeField] private Canvas _canvas;

        public bool IsVisible => _previewRoot != null && _previewRoot.gameObject.activeSelf;

        public void Show(Sprite sprite)
        {
            if (_previewRoot == null || _image == null || sprite == null)
            { Hide(); return; }
            _image.sprite = sprite;
            _previewRoot.gameObject.SetActive(true);
            FollowPointer();
        }

        public void Hide()
        {
            if (_previewRoot != null) _previewRoot.gameObject.SetActive(false);
            if (_image != null) _image.sprite = null;
        }

        private void LateUpdate()
        {
            if (IsVisible) FollowPointer();
        }

        private void OnDisable() => Hide();

        private void FollowPointer()
        {
            var pointer = Pointer.current;
            if (pointer != null) SetScreenPosition(pointer.position.ReadValue());
        }

        public bool SetScreenPosition(Vector2 screenPosition)
        {
            var parent = _previewRoot != null ? _previewRoot.parent as RectTransform : null;
            if (parent == null) return false;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            var camera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            if (!RectTransformUtility.ScreenPointToWorldPointInRectangle(parent, screenPosition, camera, out var world)) return false;
            _previewRoot.position = world;
            return true;
        }
    }
}
