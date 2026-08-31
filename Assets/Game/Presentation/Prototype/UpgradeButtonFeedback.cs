using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    [DisallowMultipleComponent]
    public sealed class UpgradeButtonFeedback : MonoBehaviour
    {
        private const float SuccessSeconds = 0.42f;
        private const float FailureSeconds = 0.42f;

        [SerializeField] private RectTransform _visualPivot;
        [SerializeField] private Image _visualImage;
        [SerializeField] private Color _baseColor = new(0.851f, 0.42f, 0.169f, 1f);
        [SerializeField] private Color _disabledColor = new(0.38f, 0.32f, 0.28f, 0.72f);

        private Button _button;
        private Coroutine _feedbackRoutine;
        private Vector2 _basePosition;

        public RectTransform VisualPivot => _visualPivot;
        public Image VisualImage => _visualImage;
        public bool IsPlaying => _feedbackRoutine != null;
        public bool? LastSucceeded { get; private set; }

        private void Awake()
        {
            _button = GetComponent<Button>();
            if (_visualPivot != null) _basePosition = _visualPivot.anchoredPosition;
            ResetVisual();
        }

        private void OnDisable()
        {
            if (_feedbackRoutine != null)
            {
                StopCoroutine(_feedbackRoutine);
                _feedbackRoutine = null;
            }
            ResetVisual();
        }

        public void Play(bool succeeded)
        {
            if (!isActiveAndEnabled) return;
            LastSucceeded = succeeded;
            if (_feedbackRoutine != null) StopCoroutine(_feedbackRoutine);
            ResetVisual();
            _feedbackRoutine = StartCoroutine(succeeded ? AnimateSuccess() : AnimateFailure());
        }

        public void SetInteractableVisual(bool interactable)
        {
            if (_feedbackRoutine == null && _visualImage != null)
                _visualImage.color = interactable ? _baseColor : _disabledColor;
        }

        private IEnumerator AnimateSuccess()
        {
            var elapsed = 0f;
            var highlight = new Color(1f, 0.76f, 0.18f, 1f);
            while (elapsed < SuccessSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / SuccessSeconds);
                var pulse = normalized < 0.45f
                    ? Mathf.Lerp(1f, 1.15f, Mathf.SmoothStep(0f, 1f, normalized / 0.45f))
                    : normalized < 0.72f
                        ? Mathf.Lerp(1.15f, 0.97f, Mathf.SmoothStep(0f, 1f, (normalized - 0.45f) / 0.27f))
                        : Mathf.Lerp(0.97f, 1f, Mathf.SmoothStep(0f, 1f, (normalized - 0.72f) / 0.28f));
                if (_visualPivot != null) _visualPivot.localScale = Vector3.one * pulse;
                if (_visualImage != null) _visualImage.color = Color.Lerp(_baseColor, highlight, Mathf.Sin(normalized * Mathf.PI));
                yield return null;
            }
            _feedbackRoutine = null;
            ResetVisual();
        }

        private IEnumerator AnimateFailure()
        {
            var elapsed = 0f;
            var failure = new Color(0.94f, 0.2f, 0.15f, 1f);
            while (elapsed < FailureSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                var normalized = Mathf.Clamp01(elapsed / FailureSeconds);
                if (_visualPivot != null)
                    _visualPivot.anchoredPosition = _basePosition + Vector2.right * (Mathf.Sin(normalized * Mathf.PI * 8f) * 10f * (1f - normalized));
                if (_visualImage != null) _visualImage.color = Color.Lerp(failure, _baseColor, normalized);
                yield return null;
            }
            _feedbackRoutine = null;
            ResetVisual();
        }

        private void ResetVisual()
        {
            if (_visualPivot != null)
            {
                _visualPivot.anchoredPosition = _basePosition;
                _visualPivot.localScale = Vector3.one;
            }
            if (_visualImage != null)
                _visualImage.color = _button != null && !_button.interactable ? _disabledColor : _baseColor;
        }
    }
}
