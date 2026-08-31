using System.Collections;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Prototype;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Presentation.Prototype
{
    [DisallowMultipleComponent]
    public sealed class BuildingSlotProgressView : MonoBehaviour
    {
        private const float ConstructionFeedbackSeconds = 1f;

        [SerializeField] private Slider _constructionSlider;
        [SerializeField] private Slider _upgradeSlider;
        [SerializeField] private Image _upgradeIcon;

        private Coroutine _constructionRoutine;
        private string _buildingId;

        public Slider ConstructionSlider => _constructionSlider;
        public Slider UpgradeSlider => _upgradeSlider;
        public Image UpgradeIcon => _upgradeIcon;

        public void Render(BuildingSlotViewModel viewModel)
        {
            var buildingId = viewModel?.BuildingId ?? string.Empty;
            if (string.IsNullOrWhiteSpace(buildingId))
            {
                ResetView();
                _buildingId = string.Empty;
                return;
            }

            if (!string.Equals(_buildingId, buildingId, System.StringComparison.Ordinal))
                StartConstructionFeedback();
            _buildingId = buildingId;

            var upgrading = viewModel.UpgradeState == BuildingUpgradeState.Upgrading;
            if (upgrading && _constructionRoutine != null)
            {
                StopCoroutine(_constructionRoutine);
                _constructionRoutine = null;
                SetVisible(_constructionSlider, false);
            }
            SetVisible(_upgradeSlider, upgrading);
            if (upgrading && _upgradeSlider != null)
                _upgradeSlider.SetValueWithoutNotify(viewModel.UpgradeProgressMilli);
            if (_upgradeIcon != null)
                _upgradeIcon.gameObject.SetActive(viewModel.Level > 1);
        }

        private void OnDisable()
        {
            ResetView();
            _buildingId = string.Empty;
        }

        private void StartConstructionFeedback()
        {
            if (_constructionRoutine != null) StopCoroutine(_constructionRoutine);
            SetVisible(_upgradeSlider, false);
            SetVisible(_constructionSlider, true);
            if (_constructionSlider != null) _constructionSlider.SetValueWithoutNotify(0);
            _constructionRoutine = StartCoroutine(AnimateConstruction());
        }

        private IEnumerator AnimateConstruction()
        {
            var elapsed = 0f;
            while (elapsed < ConstructionFeedbackSeconds)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_constructionSlider != null)
                    _constructionSlider.SetValueWithoutNotify(Mathf.Clamp01(elapsed / ConstructionFeedbackSeconds) * 1000f);
                yield return null;
            }
            if (_constructionSlider != null) _constructionSlider.SetValueWithoutNotify(1000f);
            SetVisible(_constructionSlider, false);
            _constructionRoutine = null;
        }

        private void ResetView()
        {
            if (_constructionRoutine != null)
            {
                StopCoroutine(_constructionRoutine);
                _constructionRoutine = null;
            }
            SetVisible(_constructionSlider, false);
            SetVisible(_upgradeSlider, false);
            if (_upgradeIcon != null) _upgradeIcon.gameObject.SetActive(false);
        }

        private static void SetVisible(Slider slider, bool visible)
        {
            if (slider != null) slider.gameObject.SetActive(visible);
        }
    }
}
