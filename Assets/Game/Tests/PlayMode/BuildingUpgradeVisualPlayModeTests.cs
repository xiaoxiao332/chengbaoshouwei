using System.Collections;
using System.Reflection;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Prototype;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FortressFrontier.Tests.PlayMode
{
    public sealed class BuildingUpgradeVisualPlayModeTests
    {
        [UnityTest]
        public IEnumerator BuildingSlotProgressView_ShowsConstructionAndAuthoritativeUpgradeStates()
        {
            var root = new GameObject("BuildingArt", typeof(RectTransform));
            root.SetActive(false);
            var construction = Slider(root.transform, "ConstructionSlider");
            var upgrade = Slider(root.transform, "UpgradeSlider");
            var icon = new GameObject("UpgradeIcon", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            icon.transform.SetParent(root.transform, false);
            var view = root.AddComponent<BuildingSlotProgressView>();
            SetField(view, "_constructionSlider", construction);
            SetField(view, "_upgradeSlider", upgrade);
            SetField(view, "_upgradeIcon", icon);
            root.SetActive(true);

            view.Render(new BuildingSlotViewModel("building.test", 1, BuildingUpgradeState.Ready,
                ProductionBlockReason.None));
            Assert.That(construction.gameObject.activeSelf, Is.True);
            Assert.That(upgrade.gameObject.activeSelf, Is.False);
            Assert.That(icon.gameObject.activeSelf, Is.False);
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(construction.value, Is.GreaterThan(0f));

            view.Render(new BuildingSlotViewModel("building.test", 1, BuildingUpgradeState.Upgrading,
                ProductionBlockReason.None, upgradeProgressMilli: 500));
            Assert.That(construction.gameObject.activeSelf, Is.False);
            Assert.That(upgrade.gameObject.activeSelf, Is.True);
            Assert.That(upgrade.value, Is.EqualTo(500f));

            view.Render(new BuildingSlotViewModel("building.test", 2, BuildingUpgradeState.Max,
                ProductionBlockReason.None));
            Assert.That(upgrade.gameObject.activeSelf, Is.False);
            Assert.That(icon.gameObject.activeSelf, Is.True);

            view.Render(null);
            Assert.That(construction.gameObject.activeSelf, Is.False);
            Assert.That(upgrade.gameObject.activeSelf, Is.False);
            Assert.That(icon.gameObject.activeSelf, Is.False);
            Object.Destroy(root);
            yield return null;
        }

        [UnityTest]
        public IEnumerator UpgradeButtonFeedback_AnimatesOnlyItsVisualPivot()
        {
            var root = new GameObject("UpgradeAction", typeof(RectTransform), typeof(Image), typeof(Button));
            root.SetActive(false);
            var rootRect = root.GetComponent<RectTransform>();
            var sibling = new GameObject("Sibling", typeof(RectTransform)).GetComponent<RectTransform>();
            sibling.SetParent(root.transform, false);
            var pivot = new GameObject("FeedbackPivot", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            pivot.SetParent(root.transform, false);
            var visual = pivot.GetComponent<Image>();
            var feedback = root.AddComponent<UpgradeButtonFeedback>();
            SetField(feedback, "_visualPivot", pivot);
            SetField(feedback, "_visualImage", visual);
            root.SetActive(true);
            var rootPosition = rootRect.anchoredPosition;
            var siblingPosition = sibling.anchoredPosition;

            feedback.Play(true);
            Assert.That(feedback.LastSucceeded, Is.True);
            var maximumScale = 1f;
            for (var frame = 0; frame < 15; frame++)
            {
                yield return null;
                maximumScale = Mathf.Max(maximumScale, pivot.localScale.x);
            }
            Assert.That(maximumScale, Is.GreaterThanOrEqualTo(1.12f));
            Assert.That(rootRect.anchoredPosition, Is.EqualTo(rootPosition));
            Assert.That(sibling.anchoredPosition, Is.EqualTo(siblingPosition));
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.That(pivot.localScale, Is.EqualTo(Vector3.one));

            feedback.Play(false);
            Assert.That(feedback.LastSucceeded, Is.False);
            var maximumOffset = 0f;
            for (var frame = 0; frame < 12; frame++)
            {
                yield return null;
                maximumOffset = Mathf.Max(maximumOffset, Mathf.Abs(pivot.anchoredPosition.x - rootPosition.x));
            }
            Assert.That(maximumOffset, Is.GreaterThanOrEqualTo(5f));
            Assert.That(rootRect.anchoredPosition, Is.EqualTo(rootPosition));
            Assert.That(sibling.anchoredPosition, Is.EqualTo(siblingPosition));
            Object.Destroy(root);
            yield return null;
        }

        private static Slider Slider(Transform parent, string name)
        {
            var gameObject = new GameObject(name, typeof(RectTransform), typeof(Slider));
            gameObject.transform.SetParent(parent, false);
            var slider = gameObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1000f;
            gameObject.SetActive(false);
            return slider;
        }

        private static void SetField<T>(object target, string name, T value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)?.SetValue(target, value);
        }
    }
}
