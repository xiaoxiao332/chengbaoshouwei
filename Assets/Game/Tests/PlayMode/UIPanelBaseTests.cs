using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace FortressFrontier.Tests.PlayMode
{
    public sealed class UIPanelBaseTests
    {
        [Test]
        public async Task OpenAndClose_UpdatesVisibilityAndInputState()
        {
            var gameObject = new GameObject("TestPanel", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var panel = gameObject.AddComponent<TestPanel>();
                var canvasGroup = gameObject.GetComponent<CanvasGroup>();

                await panel.InitializeAsync(CancellationToken.None);
                Assert.That(canvasGroup.alpha, Is.Zero);
                Assert.That(canvasGroup.blocksRaycasts, Is.False);

                await panel.OpenAsync(null, CancellationToken.None);
                Assert.That(panel.IsOpen, Is.True);
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.blocksRaycasts, Is.True);

                await panel.CloseAsync(CancellationToken.None);
                Assert.That(panel.IsOpen, Is.False);
                Assert.That(canvasGroup.alpha, Is.Zero);
                Assert.That(canvasGroup.blocksRaycasts, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}
