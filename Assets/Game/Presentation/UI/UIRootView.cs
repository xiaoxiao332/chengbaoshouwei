using System;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.UI
{
    public sealed class UIRootView : MonoBehaviour
    {
        [SerializeField] private RectTransform _bgRoot;
        [SerializeField] private RectTransform _windowRoot;
        [SerializeField] private RectTransform _popRoot;
        [SerializeField] private RectTransform _overRoot;
        [SerializeField] private RectTransform _safeAreaRoot;

        public RectTransform SafeAreaRoot => _safeAreaRoot;

        private void OnEnable()
        {
            ApplySafeArea();
        }

        private void OnRectTransformDimensionsChange()
        {
            ApplySafeArea();
        }

        public RectTransform GetLayerRoot(UIPanelLayer layer)
        {
            return layer switch
            {
                UIPanelLayer.Bg => Require(_bgRoot, nameof(_bgRoot)),
                UIPanelLayer.Window => Require(_windowRoot, nameof(_windowRoot)),
                UIPanelLayer.Pop => Require(_popRoot, nameof(_popRoot)),
                UIPanelLayer.Over => Require(_overRoot, nameof(_overRoot)),
                _ => throw new ArgumentOutOfRangeException(nameof(layer), layer, null)
            };
        }

        private void OnValidate()
        {
            ConfigureCanvas(_bgRoot, 0);
            ConfigureCanvas(_windowRoot, 100);
            ConfigureCanvas(_popRoot, 200);
            ConfigureCanvas(_overRoot, 300);

            var scaler = GetComponentInParent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                scaler.matchWidthOrHeight = 0.5f;
            }
        }

        private void ApplySafeArea()
        {
            if (_safeAreaRoot == null || Screen.width <= 0 || Screen.height <= 0)
            {
                return;
            }

            var safeArea = Screen.safeArea;
            _safeAreaRoot.anchorMin = new Vector2(
                safeArea.xMin / Screen.width,
                safeArea.yMin / Screen.height);
            _safeAreaRoot.anchorMax = new Vector2(
                safeArea.xMax / Screen.width,
                safeArea.yMax / Screen.height);
            _safeAreaRoot.offsetMin = Vector2.zero;
            _safeAreaRoot.offsetMax = Vector2.zero;
        }

        private static RectTransform Require(RectTransform value, string fieldName)
        {
            return value != null
                ? value
                : throw new InvalidOperationException($"UIRootView field is not assigned: {fieldName}.");
        }

        private static void ConfigureCanvas(RectTransform root, int sortingOrder)
        {
            if (root == null || !root.TryGetComponent<Canvas>(out var canvas))
            {
                return;
            }

            canvas.overrideSorting = true;
            canvas.sortingOrder = sortingOrder;
        }
    }
}
