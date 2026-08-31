using System.Collections;
using System.IO;
using System.Threading;
using System.Linq;
using System.Reflection;
using FortressFrontier.Bootstrap;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Scenes;
using FortressFrontier.Runtime.Settings;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FortressFrontier.Tests.PlayMode
{
    public sealed class VisualFlowPlayModeTests
    {
        [UnityTearDown]
        public IEnumerator TearDownPersistentCompositionRoot()
        {
            var manager = Object.FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            if (manager == null) yield break;

            Object.Destroy(manager.gameObject);
            var deadline = Time.realtimeSinceStartup + 2f;
            while (manager != null && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(0.25f);
        }

        [UnityTest]
        public IEnumerator BootSelectionGameplayResultSelection_CompletesWithoutMissingBindings()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return WaitForPanel<BootPanel>(15f);
            var boot = Object.FindFirstObjectByType<BootPanel>(FindObjectsInactive.Include);
            var bootReadyDeadline = Time.realtimeSinceStartup + 15f;
            while (GetField<Text>(boot, "_statusText").text != "整备完成" &&
                   Time.realtimeSinceStartup < bootReadyDeadline)
                yield return null;
            Assert.That(GetField<Text>(boot, "_statusText").text, Is.EqualTo("整备完成"));
            yield return WaitForAudioPlayback("bgm_boot_dawn_at_ramparts", 10f);
            var manager = Object.FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            var settingsSystem = GetField<ApplicationSettingsSystem>(manager, "_applicationSettingsSystem");
            var originalSettings = settingsSystem.GetSnapshot();
            Assert.That(SceneManager.GetActiveScene().name, Is.EqualTo("Boot"));
            Assert.That(GetField<Button>(boot, "_startButton").gameObject.activeInHierarchy, Is.True,
                "Boot start button must be visible after initialization.");
            Assert.That(GetField<Button>(boot, "_settingsButton").gameObject.activeInHierarchy, Is.True,
                "Boot settings button must be visible after initialization.");
            var bootStatus = GetField<Text>(boot, "_statusText");
            var statusText = bootStatus.text;
            var minimumStatusY = bootStatus.rectTransform.anchoredPosition.y;
            var maximumStatusY = minimumStatusY;
            var minimumStatusAlpha = bootStatus.color.a;
            var maximumStatusAlpha = minimumStatusAlpha;
            var animationDeadline = Time.realtimeSinceStartup + 0.65f;
            while (Time.realtimeSinceStartup < animationDeadline)
            {
                minimumStatusY = Mathf.Min(minimumStatusY, bootStatus.rectTransform.anchoredPosition.y);
                maximumStatusY = Mathf.Max(maximumStatusY, bootStatus.rectTransform.anchoredPosition.y);
                minimumStatusAlpha = Mathf.Min(minimumStatusAlpha, bootStatus.color.a);
                maximumStatusAlpha = Mathf.Max(maximumStatusAlpha, bootStatus.color.a);
                yield return null;
            }
            Assert.That(bootStatus.text, Is.EqualTo(statusText), "Boot Status animation must not rewrite its current message.");
            Assert.That(maximumStatusY - minimumStatusY, Is.GreaterThan(1f).And.LessThanOrEqualTo(8.1f));
            Assert.That(maximumStatusAlpha - minimumStatusAlpha, Is.GreaterThan(0.05f));
            Assert.That(minimumStatusAlpha, Is.GreaterThanOrEqualTo(0.57f));
            Assert.That(maximumStatusAlpha, Is.LessThanOrEqualTo(1.01f));

            GetField<Button>(boot, "_settingsButton").onClick.Invoke();
            yield return WaitForPanel<SettingsPanel>(10f);
            var settings = Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            var initialSliderValue = GetField<Slider>(settings, "_masterVolumeSlider").value;
            var initialMusicValue = GetField<Slider>(settings, "_musicVolumeSlider").value;
            var initialSfxValue = GetField<Slider>(settings, "_sfxVolumeSlider").value;
            var initialMuted = GetField<Toggle>(settings, "_muteToggle").isOn;
            GetField<Slider>(settings, "_masterVolumeSlider").value = 12;
            GetField<Slider>(settings, "_musicVolumeSlider").value = 13;
            GetField<Slider>(settings, "_sfxVolumeSlider").value = 14;
            GetField<Toggle>(settings, "_muteToggle").isOn = !initialMuted;
            GetField<Button>(settings, "_cancelButton").onClick.Invoke();
            yield return null;
            GetField<Button>(boot, "_settingsButton").onClick.Invoke();
            yield return WaitForPanel<SettingsPanel>(10f);
            settings = Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            Assert.That(GetField<Slider>(settings, "_masterVolumeSlider").value, Is.EqualTo(initialSliderValue));
            Assert.That(GetField<Slider>(settings, "_musicVolumeSlider").value, Is.EqualTo(initialMusicValue));
            Assert.That(GetField<Slider>(settings, "_sfxVolumeSlider").value, Is.EqualTo(initialSfxValue));
            Assert.That(GetField<Toggle>(settings, "_muteToggle").isOn, Is.EqualTo(initialMuted));
            GetField<Slider>(settings, "_masterVolumeSlider").value = 67;
            GetField<Slider>(settings, "_musicVolumeSlider").value = 57;
            GetField<Slider>(settings, "_sfxVolumeSlider").value = 47;
            GetField<Toggle>(settings, "_muteToggle").isOn = false;
            GetField<Button>(settings, "_applyButton").onClick.Invoke();
            yield return null;
            Assert.That(AudioListener.volume, Is.EqualTo(0.67f).Within(0.001f));
            GetField<Button>(boot, "_startButton").onClick.Invoke();
            yield return WaitForScene("Selection", 15f);
            yield return WaitForAudioPlayback("bgm_selection_fortress_war_table", 10f);

            var selection = Object.FindFirstObjectByType<SelectionPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(selection, "SelectionPanel was not present after Selection scene load.");
            Assert.IsNotNull(GetField<object>(selection, "_commands"), "SelectionPanel commands were not bound.");
            GetField<Button>(selection, "_settingsButton").onClick.Invoke();
            yield return WaitForPanel<SettingsPanel>(10f);
            settings = Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            Assert.That(GetField<Slider>(settings, "_masterVolumeSlider").value, Is.EqualTo(67));
            Assert.That(GetField<Slider>(settings, "_musicVolumeSlider").value, Is.EqualTo(57));
            Assert.That(GetField<Slider>(settings, "_sfxVolumeSlider").value, Is.EqualTo(47));
            GetField<Button>(settings, "_cancelButton").onClick.Invoke();
            yield return null;
            var mapPreview = GetField<Image>(selection, "_mapPreview");
            Assert.That(mapPreview.sprite?.name, Is.EqualTo("map_prologue"));
            AssertSelectionCardSprites(selection);
            var cardPageText = GetField<Text>(selection, "_cardPageText");
            var nextCardPage = GetField<Button>(selection, "_nextCardPageButton");
            var previousCardPage = GetField<Button>(selection, "_previousCardPageButton");
            Assert.That(cardPageText.text, Is.EqualTo("1/2"));
            nextCardPage.onClick.Invoke();
            yield return null;
            Assert.That(cardPageText.text, Is.EqualTo("2/2"));
            AssertSelectionCardSprites(selection);
            yield return CaptureValidationScreenshot("selection-card-page2-1920x1080.png", 1920, 1080);
            previousCardPage.onClick.Invoke();
            yield return null;
            Assert.That(cardPageText.text, Is.EqualTo("1/2"));
            AssertSelectionCardSprites(selection);
            var categoryButtons = GetField<Button[]>(selection, "_categoryButtons");
            for (var categoryIndex = 1; categoryIndex < categoryButtons.Length; categoryIndex++)
            {
                categoryButtons[categoryIndex].onClick.Invoke();
                yield return null;
                AssertSelectionCardSprites(selection);
            }
            categoryButtons[0].onClick.Invoke();
            yield return null;
            AssertSelectionCardSprites(selection);
            GetField<Button>(selection, "_nextBattlefieldButton").onClick.Invoke();
            yield return null;
            Assert.That(mapPreview.sprite?.name, Is.EqualTo("map_river_pass"));
            GetField<Button>(selection, "_previousBattlefieldButton").onClick.Invoke();
            yield return null;
            Assert.That(mapPreview.sprite?.name, Is.EqualTo("map_prologue"));
            Screen.SetResolution(1920, 1080, false);
            yield return null;
            GetField<Button>(selection, "_startButton").onClick.Invoke();

            yield return WaitForScene("Gameplay", 15f);
            yield return WaitForAudioPlayback("bgm_prologue_development_border_smoke", 10f);
            var gameplay = Object.FindFirstObjectByType<GameplayPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(gameplay, "GameplayPanel was not present after Gameplay scene load.");
            Assert.IsNotNull(GetField<object>(gameplay, "_commands"), "GameplayPanel commands were not bound.");
            var windowCanvas = gameplay.transform.parent.GetComponent<Canvas>();
            var worldBackdropCanvas = gameplay.transform.Find("World")?.GetComponent<Canvas>();
            var worldContext = Object.FindFirstObjectByType<GameplayWorldContext>(FindObjectsInactive.Include);
            Assert.That(windowCanvas?.sortingOrder, Is.EqualTo(100));
            Assert.That(worldBackdropCanvas?.overrideSorting, Is.True);
            Assert.That(worldBackdropCanvas?.sortingOrder, Is.EqualTo(10));
            Assert.That(worldContext, Is.Not.Null);
            Assert.That(worldContext.WorldConstructionOverlay.GetComponent<Canvas>().sortingOrder, Is.EqualTo(40));
            Assert.That(worldContext.WorldUnitsOverlay.GetComponent<Canvas>().sortingOrder, Is.EqualTo(50));
            Assert.That(worldContext.WorldEffectsOverlay.GetComponent<Canvas>().sortingOrder, Is.EqualTo(60));
            var soldierTab = GetField<Button>(gameplay, "_soldierTabButton");
            var itemTab = GetField<Button>(gameplay, "_itemTabButton");
            var initialCardButtons = GetField<Button[]>(gameplay, "_cardButtons");
            Assert.That(initialCardButtons.Take(4).All(button => !button.gameObject.activeSelf), Is.True,
                "Soldier card slots must remain hidden until their camps activate them.");
            Assert.That(soldierTab.gameObject.activeSelf, Is.True, "Gameplay soldier tab is hidden.");
            Assert.That(itemTab.gameObject.activeSelf, Is.True, "Gameplay item tab is hidden.");
            Assert.That(gameplay.transform.Find("EnemyIntel"), Is.Null,
                "Gameplay must not contain a dedicated enemy-intel text node.");
            Assert.That(gameplay.transform.Find("Status"), Is.Null,
                "Gameplay must not contain a generic status text node.");
            Assert.That(GetField<Image>(gameplay, "_worldBackground").sprite?.name, Is.EqualTo("map_prologue"));
            var researchButton = GetField<Button>(gameplay, "_researchButton");
            var researchPanel = GetField<GameObject>(gameplay, "_researchPanel");
            researchButton.onClick.Invoke();
            yield return null;
            Assert.That(researchPanel.activeSelf, Is.True, "Research panel did not open.");
            Assert.That(GetField<Button[]>(gameplay, "_researchOptionButtons").Length, Is.EqualTo(3));
            Assert.That(GetField<Button[]>(gameplay, "_researchOptionButtons").All(value => !value.gameObject.activeSelf), Is.True,
                "Research candidates must remain hidden until a matching soldier camp activates their category.");
            Assert.That(GetField<Text>(gameplay, "_researchStatusText").text, Does.Contain("研究院"),
                "The empty research panel must explain the missing lab gate.");
            GetField<Button>(gameplay, "_researchCloseButton").onClick.Invoke();
            yield return null;
            Assert.That(researchPanel.activeSelf, Is.False);

            itemTab.onClick.Invoke();
            var cardButtons = GetField<Button[]>(gameplay, "_cardButtons");
            var cardArt = GetField<Image[]>(gameplay, "_cardArtImages");
            var slotButtons = GetField<Button[]>(gameplay, "_buildingSlotButtons");
            var cardTray = gameplay.transform.Find("CardTray") as RectTransform;
            var cardRects = GetField<RectTransform[]>(gameplay, "_cardRects");
            Assert.That(cardTray, Is.Not.Null);
            Assert.That(cardTray.anchorMin.x, Is.EqualTo(0.28f).Within(0.0001f));
            Assert.That(cardTray.anchorMin.y, Is.EqualTo(0.005f).Within(0.0001f));
            Assert.That(cardTray.anchorMax.x, Is.EqualTo(0.80f).Within(0.0001f));
            Assert.That(cardTray.anchorMax.y, Is.EqualTo(0.14f).Within(0.0001f));
            Canvas.ForceUpdateCanvases();
            Assert.That(cardRects.All(rect => IsFullyInside(rect, cardTray)), Is.True,
                "All soldier and item slots must remain inside the compact CardTray at 1920x1080.");
            Assert.That(cardButtons.Length, Is.EqualTo(10));
            Assert.That(slotButtons.Length, Is.EqualTo(9));
            Assert.That(soldierTab.image.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(soldierTab.image.preserveAspect, Is.True);
            Assert.That(soldierTab.image.sprite.name, Does.StartWith("ui_soldier_tab_frame"));
            Assert.That(cardButtons[0].image.type, Is.EqualTo(Image.Type.Simple));
            Assert.That(cardButtons[0].image.preserveAspect, Is.True);
            Assert.That(cardButtons[0].image.sprite.name, Does.StartWith("ui_unit_slot_frame"));
            Assert.That(cardButtons[0].image.sprite, Is.Not.EqualTo(soldierTab.image.sprite));
            Assert.That(GameObject.Find("BuildingMenuButton"), Is.Null);
            Assert.That(GameObject.Find("Blueprint"), Is.Null);

            var pointer = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(cardButtons[4].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            var cardHoverPanel = GetField<GameObject>(gameplay, "_cardHoverPanel");
            Assert.That(cardHoverPanel.activeSelf, Is.True);
            Assert.That(GetField<Text>(gameplay, "_cardHoverNameText").text, Is.Not.Empty);
            Assert.That(GetField<Text>(gameplay, "_cardHoverCostText").text, Does.StartWith("消耗："));
            Assert.That(GetField<Text>(gameplay, "_cardHoverAttributesText").text, Is.Not.Empty);
            ExecuteEvents.Execute(cardButtons[4].gameObject, pointer, ExecuteEvents.pointerExitHandler);
            Assert.That(cardHoverPanel.activeSelf, Is.False);

            cardButtons[4].onClick.Invoke();
            yield return null;
            Assert.That(GetField<GameplayViewModel>(gameplay, "_viewModel").SelectedCardId.HasValue, Is.True);
            var placementPreview = GetField<BuildingPlacementPreview>(gameplay, "_buildingPlacementPreview");
            Assert.That(placementPreview, Is.Not.Null, "Building placement preview binding is missing.");
            Assert.That(placementPreview.IsVisible, Is.True);
            var previewImage = GetField<Image>(placementPreview, "_image");
            Assert.That(previewImage.sprite, Is.EqualTo(cardArt[4].sprite));
            Assert.That(previewImage.color.a, Is.EqualTo(0.38f).Within(0.001f));
            Assert.That(previewImage.color.b, Is.GreaterThan(previewImage.color.r));
            var previewRoot = GetField<RectTransform>(placementPreview, "_previewRoot");
            var previewCanvas = GetField<Canvas>(placementPreview, "_canvas");
            var previewCamera = previewCanvas != null && previewCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? previewCanvas.worldCamera : null;
            var requestedPreviewPosition = new Vector2(Screen.width * 0.63f, Screen.height * 0.57f);
            Assert.That(placementPreview.SetScreenPosition(requestedPreviewPosition), Is.True);
            Assert.That(Vector2.Distance(RectTransformUtility.WorldToScreenPoint(previewCamera, previewRoot.position),
                requestedPreviewPosition), Is.LessThan(2f), "Building preview did not stay attached to its pointer screen position.");
            GetField<Button>(gameplay, "_worldCancelButton").onClick.Invoke();
            yield return null;
            Assert.That(GetField<GameplayViewModel>(gameplay, "_viewModel").SelectedCardId.HasValue, Is.False);
            Assert.That(placementPreview.IsVisible, Is.False);
            // Resolve buttons from the current ViewModel instead of assuming hand order.
            var nonPastureCards = new[] { "card.building.winery", "card.building.gatherer-lodge", "card.building.wood-gatherer-camp",
                "card.building.shield-camp", "card.building.archer-camp" };
            for (var itemIndex = 0; itemIndex < nonPastureCards.Length; itemIndex++)
            {
                var hand = GetField<GameplayViewModel>(gameplay, "_viewModel").ItemHand;
                var sourceIndex = hand.ToList().FindIndex(value => value.Id.Value == nonPastureCards[itemIndex]);
                Assert.That(sourceIndex, Is.GreaterThanOrEqualTo(0), $"Missing item card {nonPastureCards[itemIndex]}.");
                var buttonIndex = sourceIndex + 4;
                Assert.That(cardButtons[buttonIndex].gameObject.activeSelf, Is.True);
                Assert.That(cardArt[buttonIndex].sprite, Is.Not.Null);
                Assert.That(cardButtons[buttonIndex].GetComponentInChildren<Text>(true).gameObject.activeSelf, Is.False);
                cardButtons[buttonIndex].onClick.Invoke();
                yield return null;
                Assert.That(placementPreview.IsVisible, Is.True, $"Building preview did not show for {nonPastureCards[itemIndex]}.");
                slotButtons[itemIndex].onClick.Invoke();
                yield return null;
                Assert.That(placementPreview.IsVisible, Is.False, $"Building preview remained after placing {nonPastureCards[itemIndex]}.");
            }
            var buildingImages = GetField<Image[]>(gameplay, "_buildingImages");
            Assert.That(buildingImages.Length, Is.EqualTo(9));
            Assert.That(buildingImages.Take(5).All(image => image.gameObject.activeSelf), Is.True);
            var buildingProgressViews = GetField<BuildingSlotProgressView[]>(gameplay, "_buildingProgressViews");
            Assert.That(buildingProgressViews.Length, Is.EqualTo(9));
            Assert.That(buildingProgressViews.All(view => view != null && view.ConstructionSlider != null &&
                view.UpgradeSlider != null && view.UpgradeIcon != null), Is.True,
                "Every building art node must own both sliders and the upgraded icon.");
            Assert.That(buildingProgressViews.Take(5).All(view => view.ConstructionSlider.gameObject.activeSelf), Is.True,
                "Newly placed buildings must show the one-second construction feedback.");
            Assert.That(buildingProgressViews.All(view => !view.UpgradeSlider.gameObject.activeSelf &&
                !view.UpgradeIcon.gameObject.activeSelf), Is.True);
            yield return new WaitForSecondsRealtime(1.05f);
            Assert.That(buildingProgressViews.All(view => !view.ConstructionSlider.gameObject.activeSelf), Is.True,
                "Construction feedback must hide after completing without changing building authority.");

            var upgradeRuntimeRoot = Object.FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            var upgradeSceneFlow = GetField<SystemHost>(upgradeRuntimeRoot, "_systemHost").Systems.OfType<SceneFlowSystem>().Single();
            var upgradeBuildingSystem = upgradeSceneFlow.ActiveSceneSystems.Single(value => value.GetType() == typeof(BuildingSystem)) as BuildingSystem;
            var upgradeEconomy = upgradeSceneFlow.ActiveSceneSystems.Single(value => value.GetType() == typeof(EconomySystem)) as EconomySystem;
            var upgradeBuildingActions = GetField<Button[]>(gameplay, "_buildingActionButtons");
            var upgradeFeedback = GetField<UpgradeButtonFeedback>(gameplay, "_upgradeButtonFeedback");

            ExecuteEvents.Execute(slotButtons[0].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(upgradeBuildingSystem.GetSnapshot()[0].UpgradeState, Is.EqualTo(BuildingUpgradeState.Locked));
            Assert.That(upgradeBuildingActions[1].interactable, Is.True,
                "A non-max, idle building must allow an upgrade attempt even when work requirements are not met.");
            upgradeBuildingActions[1].onClick.Invoke();
            Assert.That(upgradeFeedback.LastSucceeded, Is.False);
            Assert.That(upgradeFeedback.IsPlaying, Is.True);
            Assert.That(upgradeBuildingSystem.GetSnapshot()[0].Level, Is.EqualTo(1));
            Assert.That(upgradeBuildingSystem.GetSnapshot()[0].UpgradeState, Is.EqualTo(BuildingUpgradeState.Locked));
            ExecuteEvents.Execute(slotButtons[0].gameObject, pointer, ExecuteEvents.pointerExitHandler);

            var upgradeSlot = upgradeBuildingSystem.GetSnapshot()[2];
            for (var work = upgradeSlot.EffectiveWorkCount; work < 4; work++)
                Assert.That(upgradeBuildingSystem.RecordExternalWork(upgradeSlot.InstanceId), Is.True);
            Assert.That(upgradeEconomy.TryAdd(new ResourceId("resource.plank"), 100, out _), Is.True);
            ExecuteEvents.Execute(slotButtons[2].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(upgradeBuildingActions[1].interactable, Is.True);
            ExecuteEvents.Execute(slotButtons[2].gameObject, pointer, ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(upgradeBuildingActions[1].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.14f);
            Assert.That(GetField<GameObject>(gameplay, "_buildingMenu").activeSelf, Is.True,
                "Entering an action button must cancel the delayed building-menu hide.");
            upgradeBuildingActions[1].onClick.Invoke();
            Assert.That(upgradeFeedback.LastSucceeded, Is.True);
            Assert.That(upgradeFeedback.IsPlaying, Is.True);
            yield return null;
            Assert.That(upgradeFeedback.VisualPivot.localScale.x, Is.GreaterThan(1f),
                "The live upgrade-button click must visibly animate its feedback pivot.");
            Assert.That(upgradeBuildingSystem.GetSnapshot()[2].UpgradeState, Is.EqualTo(BuildingUpgradeState.Upgrading));
            upgradeBuildingSystem.SimulateTick();
            Assert.That(buildingProgressViews[2].UpgradeSlider.gameObject.activeSelf, Is.True);
            Assert.That(buildingProgressViews[2].UpgradeSlider.value, Is.GreaterThan(0f));
            for (var guard = 0; guard < 100 && upgradeBuildingSystem.GetSnapshot()[2].Level < 2; guard++)
                upgradeBuildingSystem.SimulateTick();
            Assert.That(upgradeBuildingSystem.GetSnapshot()[2].Level, Is.EqualTo(2));
            Assert.That(buildingProgressViews[2].UpgradeIcon.gameObject.activeSelf, Is.True);

            upgradeSlot = upgradeBuildingSystem.GetSnapshot()[2];
            for (var work = upgradeSlot.EffectiveWorkCount; work < 10; work++)
                Assert.That(upgradeBuildingSystem.RecordExternalWork(upgradeSlot.InstanceId), Is.True);
            ExecuteEvents.Execute(upgradeBuildingActions[1].gameObject, pointer, ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(slotButtons[2].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(upgradeBuildingActions[1].interactable, Is.True);
            upgradeBuildingActions[1].onClick.Invoke();
            Assert.That(upgradeFeedback.LastSucceeded, Is.True);
            for (var guard = 0; guard < 100 && upgradeBuildingSystem.GetSnapshot()[2].UpgradeProgressMilli < 500; guard++)
                upgradeBuildingSystem.SimulateTick();
            Assert.That(buildingProgressViews[2].UpgradeSlider.gameObject.activeSelf, Is.True);
            Assert.That(buildingProgressViews[2].UpgradeIcon.gameObject.activeSelf, Is.True,
                "The upgraded icon must remain visible during the next upgrade.");
            yield return CaptureValidationScreenshot("building-upgrade-1920x1080.png", 1920, 1080);
            for (var guard = 0; guard < 100 && upgradeBuildingSystem.GetSnapshot()[2].UpgradeState == BuildingUpgradeState.Upgrading; guard++)
                upgradeBuildingSystem.SimulateTick();
            Assert.That(upgradeBuildingSystem.GetSnapshot()[2].Level, Is.EqualTo(3));
            Assert.That(buildingProgressViews[2].UpgradeSlider.gameObject.activeSelf, Is.False);
            Assert.That(buildingProgressViews[2].UpgradeIcon.gameObject.activeSelf, Is.True);
            ExecuteEvents.Execute(slotButtons[2].gameObject, pointer, ExecuteEvents.pointerExitHandler);

            researchButton.onClick.Invoke();
            yield return null;
            Assert.That(GetField<Button[]>(gameplay, "_researchOptionButtons").All(value => value.gameObject.activeSelf), Is.True,
                "Activated shield and archer camps must expose three deterministic category research candidates.");
            Assert.That(GetField<Image[]>(gameplay, "_researchOptionImages").All(value => value.sprite != null), Is.True,
                "Visible research candidates are missing presentation sprites.");
            Assert.That(GetField<Button[]>(gameplay, "_researchOptionButtons").All(value => !value.interactable), Is.True,
                "Research candidates must not bypass the missing-lab gate.");
            GetField<Button>(gameplay, "_researchCloseButton").onClick.Invoke();
            yield return null;

            ExecuteEvents.Execute(slotButtons[1].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            var buildingMenu = GetField<GameObject>(gameplay, "_buildingMenu");
            var buildingActions = GetField<Button[]>(gameplay, "_buildingActionButtons");
            Assert.That(buildingMenu.activeSelf, Is.True);
            Assert.That(buildingActions.Length, Is.EqualTo(3));
            Assert.That(GetField<GameplayViewModel>(gameplay, "_viewModel").SelectedBuildingSlotIndex, Is.EqualTo(1));
            var menuRect = buildingMenu.transform as RectTransform;
            var buildingRect = buildingImages[1].rectTransform;
            var menuBottom = RectTransformUtility.WorldToScreenPoint(null,
                menuRect.TransformPoint(new Vector3(menuRect.rect.center.x, menuRect.rect.yMin)));
            var buildingTop = RectTransformUtility.WorldToScreenPoint(null,
                buildingRect.TransformPoint(new Vector3(buildingRect.rect.center.x, buildingRect.rect.yMax)));
            var menuCenter = RectTransformUtility.WorldToScreenPoint(null, menuRect.position);
            var buildingCenter = RectTransformUtility.WorldToScreenPoint(null, buildingRect.position);
            Assert.That(menuBottom.y, Is.GreaterThan(buildingTop.y), "Building menu was not placed above the hovered building.");
            Assert.That(Mathf.Abs(menuCenter.x - buildingCenter.x), Is.LessThan(2f), "Building menu was not horizontally centered over the building.");
            var globalWithBuildings = Object.FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            var buildingSceneFlow = GetField<SystemHost>(globalWithBuildings, "_systemHost").Systems.OfType<SceneFlowSystem>().Single();
            var buildingSystem = buildingSceneFlow.ActiveSceneSystems.Single(value => value.GetType() == typeof(BuildingSystem)) as BuildingSystem;
            var selectedWinery = buildingSystem.GetSnapshot()[1];
            Assert.That(selectedWinery.Paused, Is.True, "The exhausted winery must remain shortage-latched.");
            Assert.That(selectedWinery.BlockReason,
                Is.EqualTo(ProductionBlockReason.MissingInput).Or.EqualTo(ProductionBlockReason.ReserveProtected));
            Assert.That(buildingImages[1].color, Is.EqualTo(new Color(0.78f, 0.32f, 0.24f, 0.85f)));
            Assert.That(buildingActions[0].interactable, Is.True);
            Assert.That(buildingActions[0].GetComponentInChildren<Text>(true).text, Does.StartWith("继续"));
            var buildingEconomy = buildingSceneFlow.ActiveSceneSystems.Single(value => value.GetType() == typeof(EconomySystem)) as EconomySystem;
            Assert.That(buildingEconomy.TryAdd(new ResourceId("resource.food"), 20, out _), Is.True);
            yield return null;
            Assert.That(buildingSystem.GetSnapshot()[1].Paused, Is.True,
                "Inventory recovery must not clear a resource-shortage latch.");
            Assert.That(buildingImages[1].color, Is.EqualTo(new Color(0.78f, 0.32f, 0.24f, 0.85f)));
            buildingActions[0].onClick.Invoke();
            Assert.That(buildingSystem.GetSnapshot()[1].Paused, Is.False);
            Assert.That(buildingImages[1].color, Is.EqualTo(Color.white));
            Assert.That(buildingActions[0].interactable, Is.False, "A running building must not expose an active pause command.");
            buildingActions[0].onClick.Invoke();
            Assert.That(buildingSystem.GetSnapshot()[1].Paused, Is.False);
            ExecuteEvents.Execute(slotButtons[1].gameObject, pointer, ExecuteEvents.pointerExitHandler);
            ExecuteEvents.Execute(buildingMenu, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(buildingMenu.activeSelf, Is.True, "Building menu hid while pointer moved from building to its shared action panel.");
            ExecuteEvents.Execute(buildingMenu, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(0.15f);
            Assert.That(buildingMenu.activeSelf, Is.False);

            var insufficientSlot = upgradeBuildingSystem.GetSnapshot()[1];
            for (var work = insufficientSlot.EffectiveWorkCount; work < 4; work++)
                Assert.That(upgradeBuildingSystem.RecordExternalWork(insufficientSlot.InstanceId), Is.True);
            var availablePlank = upgradeEconomy.GetAvailable(new ResourceId("resource.plank"));
            if (availablePlank > 0)
                Assert.That(upgradeEconomy.TryExchange(new[] { new ResourceAmount(new ResourceId("resource.plank"), availablePlank) },
                    null, out _), Is.True);
            ExecuteEvents.Execute(slotButtons[1].gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(upgradeBuildingActions[1].interactable, Is.True,
                "A rule-ready building must allow a click so insufficient resources can produce feedback.");
            upgradeBuildingActions[1].onClick.Invoke();
            Assert.That(upgradeFeedback.LastSucceeded, Is.False);
            Assert.That(upgradeFeedback.IsPlaying, Is.True);
            Assert.That(upgradeBuildingSystem.GetSnapshot()[1].Level, Is.EqualTo(1));
            Assert.That(upgradeBuildingSystem.GetSnapshot()[1].UpgradeState, Is.EqualTo(BuildingUpgradeState.Ready));
            Assert.That(upgradeEconomy.GetAvailable(new ResourceId("resource.plank")), Is.Zero);
            ExecuteEvents.Execute(slotButtons[1].gameObject, pointer, ExecuteEvents.pointerExitHandler);

            var worldSprites = Object.FindObjectsByType<GameplayWorldEntityView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Select(view => view.GetComponent<Image>()?.sprite?.name).Where(name => name != null).ToArray();
            var resourceSpriteNames = new[] { "prop_berry", "prop_tree", "prop_stone" };
            Assert.That(worldSprites.Where(resourceSpriteNames.Contains).Distinct().Count(), Is.GreaterThanOrEqualTo(2),
                "The seeded opening must visibly contain multiple battlefield-gathered resource types.");
            foreach (var wallName in new[] { "FriendlyWall", "EnemyWall" })
            {
                var wall = gameplay.GetComponentsInChildren<Image>(true).Single(value => value.name == wallName);
                Assert.That(wall.preserveAspect, Is.True);
            }

            soldierTab.onClick.Invoke();
            Assert.That(cardButtons[0].gameObject.activeSelf, Is.True);
            Assert.That(cardArt[0].sprite?.name ?? "<null Soldier0 Art>",
                Is.EqualTo("unit_shield_soldier_friendly"));
            Assert.That(cardArt[1].sprite?.name ?? "<null Soldier1 Art>",
                Is.EqualTo("unit_archer_friendly"));
            Canvas.ForceUpdateCanvases();
            var soldierLabel = cardButtons[0].transform.Find("Label") as RectTransform;
            var soldierControls = cardButtons[0].transform.Find("CountControls") as RectTransform;
            Assert.That(WorldRectsOverlap(cardArt[0].rectTransform, soldierLabel), Is.False,
                "Soldier art overlaps its details label.");
            Assert.That(WorldRectsOverlap(cardArt[0].rectTransform, soldierControls), Is.False,
                "Soldier art overlaps its count controls.");
            manager = Object.FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            var sceneFlow = GetField<SystemHost>(manager, "_systemHost").Systems.OfType<SceneFlowSystem>().Single();
            var simulation = sceneFlow.ActiveSceneSystems.OfType<FixedSimulationSystem>().Single();
            var economy = sceneFlow.ActiveSceneSystems.Single(value => value.GetType() == typeof(EconomySystem)) as EconomySystem;
            var buildings = sceneFlow.ActiveSceneSystems.Single(value => value.GetType() == typeof(BuildingSystem)) as BuildingSystem;
            var training = sceneFlow.ActiveSceneSystems.Single(value => value.GetType() == typeof(TrainingSystem)) as TrainingSystem;
            var enemyTraining = sceneFlow.ActiveSceneSystems.OfType<EnemyTrainingSystem>().Single();
            var playerGatherers = sceneFlow.ActiveSceneSystems.OfType<PlayerGathererSystem>().Single();
            var enemyGatherers = sceneFlow.ActiveSceneSystems.OfType<EnemyGathererSystem>().Single();
            var worldPresentation = sceneFlow.ActiveSceneSystems.OfType<GameplayWorldPresentationSystem>().Single();
            var aiStrategy = sceneFlow.ActiveSceneSystems.OfType<AiStrategySystem>().Single();
            var combat = sceneFlow.ActiveSceneSystems.OfType<CombatSystem>().Single();
            simulation.AdvanceTicks(620);
            Assert.That(playerGatherers.GetSnapshot().Select(value => value.SourceId).Distinct().Count(),
                Is.GreaterThanOrEqualTo(2),
                "The free universal source and at least one atomically paid specialist source must be represented from a zero-resource opening.");
            worldPresentation.Tick(0.2f);
            var gathererViews = GetField<System.Collections.IDictionary>(worldPresentation, "_gathererViews");
            var gathererPresentationDeadline = Time.realtimeSinceStartup + 1f;
            while (gathererViews.Count < playerGatherers.GetSnapshot().Count + enemyGatherers.GetSnapshot().Count &&
                   Time.realtimeSinceStartup < gathererPresentationDeadline)
            {
                worldPresentation.Tick(0.2f);
                yield return null;
            }
            Assert.That(gathererViews.Count, Is.GreaterThanOrEqualTo(
                playerGatherers.GetSnapshot().Count + enemyGatherers.GetSnapshot().Count),
                "World presentation did not expand to the authoritative overlapping gatherer snapshots.");
            var food = economy.GetSnapshot().Single(value => value.Id.Value == "resource.food");
            while (food.Available < 1 && simulation.TickCount < 890)
            {
                simulation.AdvanceTicks(Mathf.Min(10, 890 - simulation.TickCount));
                food = economy.GetSnapshot().Single(value => value.Id.Value == "resource.food");
            }
            Assert.That(food.Available, Is.GreaterThanOrEqualTo(1),
                $"No real gatherer deposit was observed by tick {simulation.TickCount}: amount={food.Amount}, reserved={food.Reserved}.");
            if (food.Available < 20)
                Assert.That(economy.TryAdd(new ResourceId("resource.food"), 20 - food.Available, out _), Is.True,
                    "This UI-flow test explicitly prepares the remaining training budget after proving a real deposit.");
            cardButtons[0].onClick.Invoke();
            Assert.That(training.GetSelectionSnapshot().TotalCount, Is.EqualTo(1),
                $"Soldier selection was rejected with food={food.Available} and five non-pasture buildings active.");
            var deploymentInput = GetField<DeploymentAreaInput>(gameplay, "_deploymentAreaInput");
            Assert.That(deploymentInput, Is.Not.Null, "Deployment input binding is missing.");
            var deploymentRect = (RectTransform)deploymentInput.transform;
            var deploymentImage = deploymentInput.GetComponent<Image>();
            Assert.That(deploymentImage.sprite, Is.Not.Null);
            Assert.That(deploymentImage.sprite.name, Is.EqualTo("overlay_deployment_area"));
            Assert.That(deploymentImage.raycastTarget, Is.True);
            Assert.That(deploymentRect.anchorMin.x, Is.EqualTo(548f / 1920f).Within(0.0001f));
            Assert.That(deploymentRect.anchorMin.y, Is.EqualTo(80f / 1080f).Within(0.0001f));
            Assert.That(deploymentRect.anchorMax.x, Is.EqualTo(820f / 1920f).Within(0.0001f));
            Assert.That(deploymentRect.anchorMax.y, Is.EqualTo(1000f / 1080f).Within(0.0001f));
            Assert.That(deploymentInput.gameObject.activeSelf, Is.True);

            var lowerPoint = RectTransformUtility.WorldToScreenPoint(null,
                deploymentRect.TransformPoint(new Vector3(deploymentRect.rect.xMin, deploymentRect.rect.yMin)));
            deploymentInput.OnPointerClick(new PointerEventData(EventSystem.current) { position = lowerPoint });
            Assert.That(training.GetDeploymentSlots(), Is.Not.Empty, "Lower deployment boundary click was rejected.");
            Assert.That(training.GetDeploymentSlots().All(value => value.Point.X is >= 548 and <= 820 && value.Point.Y is >= 80 and <= 1000), Is.True);
            foreach (var orderId in training.GetDeploymentSlots().Select(value => value.OrderId).Distinct().ToArray())
                Assert.That(training.Cancel(orderId), Is.EqualTo(TrainingFailure.None));

            cardButtons[0].onClick.Invoke();
            var upperPoint = RectTransformUtility.WorldToScreenPoint(null,
                deploymentRect.TransformPoint(new Vector3(deploymentRect.rect.xMax, deploymentRect.rect.yMax)));
            deploymentInput.OnPointerClick(new PointerEventData(EventSystem.current) { position = upperPoint });
            Assert.That(training.GetDeploymentSlots(), Is.Not.Empty, "Upper deployment boundary click was rejected.");
            Assert.That(training.GetDeploymentSlots().All(value => value.Point.X is >= 548 and <= 820 && value.Point.Y is >= 80 and <= 1000), Is.True);
            foreach (var orderId in training.GetDeploymentSlots().Select(value => value.OrderId).Distinct().ToArray())
                Assert.That(training.Cancel(orderId), Is.EqualTo(TrainingFailure.None));

            cardButtons[0].onClick.Invoke();
            var screenPoint = RectTransformUtility.WorldToScreenPoint(null, deploymentRect.TransformPoint(deploymentRect.rect.center));
            deploymentInput.OnPointerClick(new PointerEventData(EventSystem.current) { position = screenPoint });
            Assert.That(training.GetDeploymentSlots(), Is.Not.Empty, "Deployment click did not create preview slots.");
            DeploymentSlotSnapshot enemyPreview = null;
            var enemyPreviewTicks = 0;
            for (var tick = 0; tick < 80; tick++)
            {
                simulation.AdvanceTicks(1);
                var currentPreview = enemyTraining.GetDeploymentSlots().FirstOrDefault();
                if (currentPreview == null) continue;
                enemyPreview ??= currentPreview;
                if (currentPreview.RouteId.Equals(enemyPreview.RouteId) && currentPreview.Point.X == enemyPreview.Point.X &&
                    currentPreview.Point.Y == enemyPreview.Point.Y) enemyPreviewTicks++;
            }
            yield return null;
            Assert.That(combat.GetUnits().Any(value => value.Faction == MatchFaction.Player), Is.True,
                $"Training did not deploy a player unit by tick {simulation.TickCount}.");
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.That(Object.FindObjectsByType<GameplayWorldEntityView>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Any(view => view.name.Contains("Unit", System.StringComparison.OrdinalIgnoreCase)), Is.True);
            CombatUnitSnapshot enemySpawn = null;
            while (simulation.TickCount < 900 && enemySpawn == null)
            {
                simulation.AdvanceTicks(1);
                var currentPreview = enemyTraining.GetDeploymentSlots().FirstOrDefault();
                if (currentPreview != null)
                {
                    enemyPreview ??= currentPreview;
                    if (currentPreview.RouteId.Equals(enemyPreview.RouteId) && currentPreview.Point.X == enemyPreview.Point.X &&
                        currentPreview.Point.Y == enemyPreview.Point.Y) enemyPreviewTicks++;
                }
                enemySpawn = combat.GetUnits().FirstOrDefault(value => value.Faction == MatchFaction.Enemy);
            }
            Assert.That(aiStrategy.GetDecisions().Any(value => value.Result.StartsWith("train:", System.StringComparison.Ordinal) && value.Tick <= 800), Is.True);
            Assert.That(enemyPreview, Is.Not.Null,
                $"Enemy training never produced a public deployment preview. tick={simulation.TickCount}; " +
                "decisions=" + string.Join(";", aiStrategy.GetDecisions().Select(value =>
                    $"{value.Tick}:{value.CandidateId}:{value.Result}:{value.GateFailure}")) +
                "; orders=" + string.Join(";", enemyTraining.GetSnapshot().Select(value =>
                    $"{value.Id}:{value.UnitId.Value}:{value.Remaining}:{value.Priority}")));
            Assert.That(enemyPreviewTicks, Is.GreaterThanOrEqualTo(10));
            Assert.That(enemySpawn, Is.Not.Null, "Enemy preview never resolved into a spawned unit.");
            Assert.That(enemySpawn.RouteId, Is.EqualTo(enemyPreview.RouteId));
            Assert.That((enemySpawn.SpawnX, enemySpawn.SpawnY, enemySpawn.Lane),
                Is.EqualTo((enemyPreview.Point.X, enemyPreview.Point.Y, enemyPreview.Point.Lane)));
            if (simulation.TickCount < 600) simulation.AdvanceTicks(600 - simulation.TickCount);
            yield return null;
            var offerViewModel = GetField<GameplayViewModel>(gameplay, "_viewModel");
            Assert.That(offerViewModel.Offer.Active, Is.True, "Tick 600 did not open the automatic four-choice reward.");
            Assert.That(offerViewModel.Offer.Choices.Count, Is.EqualTo(4));
            Assert.That(offerViewModel.Offer.Choices.Take(2).All(value =>
                value.Kind == RewardChoiceKind.ContentCard), Is.True,
                "The first two reward slots must both contain building cards.");
            Assert.That(offerViewModel.Offer.Choices.Take(2).Select(value => value.Name).Distinct().Count(), Is.EqualTo(2),
                "The two building reward slots must display different buildings.");
            Assert.That(offerViewModel.Offer.Choices.All(value =>
                !value.Name.Contains("card.") && !value.Details.Contains("resource.")), Is.True,
                "Reward labels must use player-facing names rather than stable content IDs: " +
                string.Join(" | ", offerViewModel.Offer.Choices.Select(value => $"{value.Name} :: {value.Details}")));
            var reinforcementChoiceIndex = offerViewModel.Offer.Choices.ToList().FindIndex(value =>
                value.Kind == RewardChoiceKind.ReinforcementItem);
            Assert.That(reinforcementChoiceIndex, Is.GreaterThanOrEqualTo(0));
            var choiceVisuals = GetField<ReinforcementCardVisual[]>(gameplay, "_choiceReinforcementVisuals");
            Assert.That(choiceVisuals[reinforcementChoiceIndex].gameObject.activeSelf, Is.True);
            Assert.That(choiceVisuals[reinforcementChoiceIndex].UnitIcons.Count(value => value.gameObject.activeSelf),
                Is.EqualTo(offerViewModel.Offer.Choices[reinforcementChoiceIndex].ReinforcementUnits.Count));
            yield return CaptureValidationScreenshot("reward-choice-1920x1080.png", 1920, 1080);
            yield return null;
            GetField<Button[]>(gameplay, "_choiceOptions")[reinforcementChoiceIndex].onClick.Invoke();
            yield return null;
            var reinforcementViewModel = GetField<GameplayViewModel>(gameplay, "_viewModel");
            Assert.That(reinforcementViewModel.Tab, Is.EqualTo(GameplayCardTab.Items));
            Assert.That(GetField<Button>(gameplay, "_previousSoldierPageButton").transform.parent.gameObject.activeSelf, Is.False,
                "Soldier pager must not overlap ItemTab cards or intercept their touch targets.");
            var reinforcementIndex = reinforcementViewModel.ItemHand.ToList().FindIndex(value => value.Type == CardType.ReinforcementItem);
            Assert.That(reinforcementIndex, Is.GreaterThanOrEqualTo(0));
            var reinforcementCard = reinforcementViewModel.ItemHand[reinforcementIndex];
            var itemVisuals = GetField<ReinforcementCardVisual[]>(gameplay, "_itemReinforcementVisuals");
            Assert.That(itemVisuals[reinforcementIndex].gameObject.activeSelf, Is.True);
            Assert.That(cardArt[reinforcementIndex + 4].gameObject.activeSelf, Is.False);
            Assert.That(itemVisuals[reinforcementIndex].UnitIcons.Count(value => value.gameObject.activeSelf),
                Is.EqualTo(reinforcementCard.ReinforcementUnits.Count));
            var playerHand = (HandAndOfferSystem)sceneFlow.ActiveSceneSystems.Single(value =>
                value.GetType() == typeof(HandAndOfferSystem));
            Assert.That(playerHand.TryDeployReinforcement(reinforcementCard.Id, training, 0, 0), Is.False);
            Assert.That(GetField<GameplayViewModel>(gameplay, "_viewModel").ItemHand.Any(value => value.Id.Equals(reinforcementCard.Id)), Is.True);
            cardButtons[reinforcementIndex + 4].onClick.Invoke();
            yield return null;
            var reinforcementPoint = RectTransformUtility.WorldToScreenPoint(null, deploymentRect.TransformPoint(deploymentRect.rect.center));
            deploymentInput.OnPointerClick(new PointerEventData(EventSystem.current) { position = reinforcementPoint });
            yield return null;
            Assert.That(GetField<GameplayViewModel>(gameplay, "_viewModel").ItemHand.Any(value => value.Id.Equals(reinforcementCard.Id)), Is.False,
                "A legally deployed reinforcement card was not consumed.");
            Assert.That(GetField<Text[]>(gameplay, "_resourceTexts")[0].text, Does.Contain("食物"));

            SetField(combat, "_enemyWallHealth", 0);
            combat.SimulateTick(9999);

            yield return WaitForPanel<ResultPanel>(10f);
            yield return WaitForAudioPlayback("bgm_result_victory_rampart_triumph", 10f);
            var result = Object.FindFirstObjectByType<ResultPanel>(FindObjectsInactive.Include);
            Assert.IsTrue(result.IsOpen);
            var scroll = result.GetComponentInChildren<ScrollRect>(true);
            Assert.That(scroll, Is.Not.Null, "Result timeline ScrollRect binding is missing.");
            Assert.That(scroll.content, Is.EqualTo(GetField<Text>(result, "_summary").rectTransform));
            Assert.That(scroll.viewport, Is.EqualTo(scroll.GetComponent<RectTransform>()));
            var resultSummary = GetField<Text>(result, "_summary").text;
            foreach (var label in new[] { "战场：", "局时：", "Boss归属", "城墙：", "交换：", "断点：",
                         "战况分析：", "金币明细：", "总金币" })
                Assert.That(resultSummary, Does.Contain(label), label);
            GetField<Button>(result, "_returnButton").onClick.Invoke();

            yield return WaitForScene("Selection", 15f);
            yield return WaitForAudioPlayback("bgm_selection_fortress_war_table", 10f);
            selection = Object.FindFirstObjectByType<SelectionPanel>(FindObjectsInactive.Include);
            Assert.IsNotNull(selection);
            GetField<Button>(selection, "_settingsButton").onClick.Invoke();
            yield return WaitForPanel<SettingsPanel>(10f);
            settings = Object.FindFirstObjectByType<SettingsPanel>(FindObjectsInactive.Include);
            GetField<Slider>(settings, "_masterVolumeSlider").value = originalSettings.MasterVolumePercent;
            GetField<Slider>(settings, "_musicVolumeSlider").value = originalSettings.MusicVolumePercent;
            GetField<Slider>(settings, "_sfxVolumeSlider").value = originalSettings.SfxVolumePercent;
            GetField<Toggle>(settings, "_muteToggle").isOn = originalSettings.Muted;
            GetField<Button>(settings, "_applyButton").onClick.Invoke();
            yield return null;
            Screen.SetResolution(1920, 1080, false);
            yield return null;
        }


        [UnityTest]
        public IEnumerator BootSafeArea_UsesNormalizedAnchors()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return null;
            var root = Object.FindFirstObjectByType<UIRootView>(FindObjectsInactive.Include);
            Assert.IsNotNull(root);
            Assert.That(root.SafeAreaRoot.anchorMin.x, Is.InRange(0f, 1f));
            Assert.That(root.SafeAreaRoot.anchorMin.y, Is.InRange(0f, 1f));
            Assert.That(root.SafeAreaRoot.anchorMax.x, Is.InRange(0f, 1f));
            Assert.That(root.SafeAreaRoot.anchorMax.y, Is.InRange(0f, 1f));
        }

        [UnityTest]
        public IEnumerator GameplayArcherHit_InterruptsWallLockAndVictimReturnsFire()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return WaitForPanel<BootPanel>(15f);
            var boot = Object.FindFirstObjectByType<BootPanel>(FindObjectsInactive.Include);
            var bootReadyDeadline = Time.realtimeSinceStartup + 15f;
            while (GetField<Text>(boot, "_statusText").text != "整备完成" &&
                   Time.realtimeSinceStartup < bootReadyDeadline)
                yield return null;
            Assert.That(GetField<Text>(boot, "_statusText").text, Is.EqualTo("整备完成"));
            GetField<Button>(boot, "_startButton").onClick.Invoke();
            yield return WaitForScene("Selection", 15f);
            var selection = Object.FindFirstObjectByType<SelectionPanel>(FindObjectsInactive.Include);
            Assert.That(selection, Is.Not.Null);
            GetField<Button>(selection, "_startButton").onClick.Invoke();
            yield return WaitForScene("Gameplay", 15f);

            var global = Object.FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            Assert.That(global, Is.Not.Null);
            var sceneFlow = GetField<SceneFlowSystem>(global, "_sceneFlow");
            var systems = sceneFlow.ActiveSceneSystems;
            var simulation = systems.OfType<FixedSimulationSystem>().Single();
            var economy = systems.OfType<EconomySystem>().Single(value => value is not EnemyEconomySystem);
            var enemyEconomy = systems.OfType<EnemyEconomySystem>().Single();
            var buildings = systems.OfType<BuildingSystem>().Single(value => value is not EnemyBuildingSystem);
            var enemyBuildings = systems.OfType<EnemyBuildingSystem>().Single();
            var training = systems.OfType<TrainingSystem>().Single(value => value is not EnemyTrainingSystem);
            var enemyTraining = systems.OfType<EnemyTrainingSystem>().Single();
            var gatherers = systems.OfType<PlayerGathererSystem>().Single();
            var combat = systems.OfType<CombatSystem>().Single();
            var presentation = systems.OfType<GameplayWorldPresentationSystem>().Single();
            simulation.SetPaused(true);

            foreach (var gatherer in gatherers.GetSnapshot().ToArray())
                Assert.That(gatherers.Kill(gatherer.Id), Is.True);
            var snapshot = global.CurrentMatchSnapshot;
            var archer = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.archer");
            var shield = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.shield-guard");
            Assert.That(buildings.TryBuild(0, new BuildingId("building.archer-camp"), out _), Is.True);
            Assert.That(enemyBuildings.GetSnapshot().Any(value => value.BuildingId?.Value == "building.shield-camp"), Is.True);
            foreach (var cost in archer.TrainingCosts)
                Assert.That(economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
            foreach (var cost in shield.TrainingCosts)
                Assert.That(enemyEconomy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);

            var wall = snapshot.Combat.PlayerWall.Gate;
            Assert.That(enemyTraining.TryCreateOrder(shield.Id, 1,
                DeploymentPoint.World(wall.X + 10, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick <= shield.TrainingTicks; tick++) enemyTraining.SimulateTick(tick);
            combat.SimulateTick(0);
            var victim = combat.GetUnits().Single(value => value.Faction == MatchFaction.Enemy);
            Assert.That(victim.LockedTargetKind, Is.EqualTo(CombatTargetKind.Wall));

            Assert.That(training.TryCreateOrder(archer.Id, 1,
                DeploymentPoint.World(wall.X + 150, wall.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick <= archer.TrainingTicks; tick++) training.SimulateTick(tick);
            var attacker = combat.GetUnits().Single(value => value.Faction == MatchFaction.Player);

            var sawRetaliation = false;
            var sawReturnDamage = false;
            var sawProjectileSnapshot = false;
            var sawProjectileView = false;
            for (var tick = 1; tick < 100; tick++)
            {
                combat.SimulateTick(tick);
                if (!sawProjectileSnapshot && combat.GetProjectiles().Count > 0)
                {
                    sawProjectileSnapshot = true;
                    for (var presentationAttempt = 0; presentationAttempt < 20 && !sawProjectileView; presentationAttempt++)
                    {
                        presentation.Tick(0.11f);
                        yield return new WaitForSecondsRealtime(0.05f);
                        sawProjectileView = Object.FindObjectsByType<GameplayWorldEntityView>(FindObjectsInactive.Exclude,
                                FindObjectsSortMode.None)
                            .Any(value => value.name.Contains("projectile", System.StringComparison.OrdinalIgnoreCase) ||
                                          value.GetComponent<Image>()?.sprite?.name == "projectile_arrow");
                    }
                }
                var units = combat.GetUnits();
                var currentVictim = units.FirstOrDefault(value => value.Id == victim.Id);
                var currentAttacker = units.FirstOrDefault(value => value.Id == attacker.Id);
                sawRetaliation |= currentVictim != null && currentVictim.DamageRevision > victim.DamageRevision &&
                                  currentVictim.LockedTargetKind == CombatTargetKind.Unit &&
                                  currentVictim.LockedTargetId == attacker.Id;
                sawReturnDamage |= currentAttacker != null && currentAttacker.DamageRevision > attacker.DamageRevision;
                if (sawRetaliation && sawReturnDamage) break;
            }

            yield return null;
            Assert.That(sawRetaliation, Is.True);
            Assert.That(sawReturnDamage, Is.True);
            Assert.That(sawProjectileSnapshot, Is.True, "The authoritative archer projectile snapshot was never published.");
            Assert.That(sawProjectileView, Is.True, "The projectile snapshot never produced a pooled world arrow view.");

            foreach (var unit in combat.GetUnits().ToArray()) combat.TryDamageUnit(unit.Id, int.MaxValue);
            for (var tick = 100; tick < 120 && combat.GetProjectiles().Count > 0; tick++) combat.SimulateTick(tick);
            presentation.Tick(0.11f);
            yield return null;
            Assert.That(combat.GetProjectiles(), Is.Empty);
            Assert.That(Object.FindObjectsByType<GameplayWorldEntityView>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None).Any(value =>
                    value.name.Contains("projectile", System.StringComparison.OrdinalIgnoreCase) ||
                    value.GetComponent<Image>()?.sprite?.name == "projectile_arrow"),
                Is.False, "An active arrow view remained after every authoritative projectile was recycled.");
        }

        [UnityTest]
        public IEnumerator GameplayCannonProjectile_StartsAtPrefabPoint()
        {
            yield return SceneManager.LoadSceneAsync("Boot", LoadSceneMode.Single);
            yield return WaitForPanel<BootPanel>(15f);
            var boot = Object.FindFirstObjectByType<BootPanel>(FindObjectsInactive.Include);
            var bootReadyDeadline = Time.realtimeSinceStartup + 15f;
            while (GetField<Text>(boot, "_statusText").text != "整备完成" &&
                   Time.realtimeSinceStartup < bootReadyDeadline)
                yield return null;
            Assert.That(GetField<Text>(boot, "_statusText").text, Is.EqualTo("整备完成"));
            GetField<Button>(boot, "_startButton").onClick.Invoke();
            yield return WaitForScene("Selection", 15f);
            var selection = Object.FindFirstObjectByType<SelectionPanel>(FindObjectsInactive.Include);
            GetField<Button>(selection, "_startButton").onClick.Invoke();
            yield return WaitForScene("Gameplay", 15f);

            var global = Object.FindFirstObjectByType<GlobalManager>(FindObjectsInactive.Include);
            var sceneFlow = GetField<SceneFlowSystem>(global, "_sceneFlow");
            var systems = sceneFlow.ActiveSceneSystems;
            var simulation = systems.OfType<FixedSimulationSystem>().Single();
            var economy = systems.OfType<EconomySystem>().Single(value => value is not EnemyEconomySystem);
            var buildings = systems.OfType<BuildingSystem>().Single(value => value is not EnemyBuildingSystem);
            var training = systems.OfType<TrainingSystem>().Single(value => value is not EnemyTrainingSystem);
            var combat = systems.OfType<CombatSystem>().Single();
            var presentation = systems.OfType<GameplayWorldPresentationSystem>().Single();
            simulation.SetPaused(true);

            var snapshot = global.CurrentMatchSnapshot;
            var cannon = snapshot.Combat.Units.Single(value => value.Id.Value == "unit.cannon");
            foreach (var cost in cannon.TrainingCosts)
                Assert.That(economy.TryAdd(cost.ResourceId, cost.Amount, out _), Is.True);
            Assert.That(buildings.TryBuild(0, new BuildingId("building.cannon-camp"), out _), Is.True);

            var spawn = snapshot.Combat.PlayerWall.Gate;
            Assert.That(training.TryCreateOrder(cannon.Id, 1,
                DeploymentPoint.World(spawn.X + 150, spawn.Y, 1), out _), Is.EqualTo(TrainingFailure.None));
            for (var tick = 0; tick <= cannon.TrainingTicks; tick++) training.SimulateTick(tick);
            var source = combat.GetUnits().Single(value =>
                value.Faction == MatchFaction.Player && value.UnitId.Equals(cannon.Id));

            GameplayWorldEntityView cannonView = null;
            for (var attempt = 0; attempt < 20 && cannonView == null; attempt++)
            {
                presentation.Tick(0.11f);
                yield return new WaitForSecondsRealtime(0.05f);
                cannonView = Object.FindObjectsByType<GameplayWorldEntityView>(FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(value => value.transform.Find("point") != null && value.FacingDirection == 1);
            }
            Assert.That(cannonView, Is.Not.Null, "The player cannon view with a point child was not spawned.");
            yield return new WaitForSecondsRealtime(0.1f);

            CombatProjectileSnapshot projectile = null;
            for (var tick = 1; tick < 600 && projectile == null; tick++)
            {
                combat.SimulateTick(tick);
                projectile = combat.GetProjectiles().FirstOrDefault(value =>
                    value.ProjectileKind == UnitProjectileKind.Cannonball && value.SourceUnitHandle == source.Id);
            }
            Assert.That(projectile, Is.Not.Null, "The cannon never published an authoritative cannonball snapshot.");
            Assert.That(projectile.SourceUnitId, Is.EqualTo(cannon.Id));

            var world = Object.FindFirstObjectByType<GameplayWorldContext>(FindObjectsInactive.Include);
            Assert.That(world, Is.Not.Null);
            Assert.That(cannonView.TryGetProjectileOrigin(world.WorldEffectsOverlay, out var expectedOrigin), Is.True);
            presentation.Tick(0.11f);

            GameplayWorldEntityView projectileView = null;
            var projectileDeadline = Time.realtimeSinceStartup + 2f;
            while (projectileView == null && Time.realtimeSinceStartup < projectileDeadline)
            {
                yield return null;
                projectileView = Object.FindObjectsByType<GameplayWorldEntityView>(FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(value => value.GetComponent<Image>()?.sprite?.name == "projectile_cannonball");
            }
            Assert.That(projectileView, Is.Not.Null, "The cannonball snapshot did not spawn its pooled view.");
            Assert.That(((RectTransform)projectileView.transform).anchoredPosition.x,
                Is.EqualTo(expectedOrigin.x).Within(0.1f));
            Assert.That(((RectTransform)projectileView.transform).anchoredPosition.y,
                Is.EqualTo(expectedOrigin.y).Within(0.1f));
        }

        private static bool IsFullyInside(RectTransform child, RectTransform parent)
        {
            var corners = new Vector3[4];
            child.GetWorldCorners(corners);
            return corners.All(value => RectTransformUtility.RectangleContainsScreenPoint(parent,
                RectTransformUtility.WorldToScreenPoint(null, value), null));
        }

        private static bool WorldRectsOverlap(RectTransform first, RectTransform second)
        {
            Assert.That(first, Is.Not.Null);
            Assert.That(second, Is.Not.Null);
            var firstCorners = new Vector3[4];
            var secondCorners = new Vector3[4];
            first.GetWorldCorners(firstCorners);
            second.GetWorldCorners(secondCorners);
            var firstRect = Rect.MinMaxRect(firstCorners[0].x, firstCorners[0].y,
                firstCorners[2].x, firstCorners[2].y);
            var secondRect = Rect.MinMaxRect(secondCorners[0].x, secondCorners[0].y,
                secondCorners[2].x, secondCorners[2].y);
            return firstRect.Overlaps(secondRect);
        }

        private static IEnumerator WaitForScene(string sceneName, float timeout)
        {
            var elapsed = 0f;
            while (SceneManager.GetActiveScene().name != sceneName && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(sceneName, SceneManager.GetActiveScene().name);
        }

        private static IEnumerator WaitForPanel<TPanel>(float timeout) where TPanel : UIPanelBase
        {
            var elapsed = 0f;
            while (elapsed < timeout)
            {
                var panel = Object.FindFirstObjectByType<TPanel>(FindObjectsInactive.Include);
                if (panel != null && panel.IsOpen) yield break;
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.Fail($"Panel {typeof(TPanel).Name} did not open within {timeout} seconds.");
        }

        private static IEnumerator WaitForActive(GameObject target, float timeout)
        {
            var elapsed = 0f;
            while (!target.activeInHierarchy && elapsed < timeout)
            {
                elapsed += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.That(target.activeInHierarchy, Is.True, $"{target.name} did not become active within {timeout} seconds.");
        }

        private static IEnumerator WaitForAudioPlayback(string expectedClipName, float timeout)
        {
            var deadline = Time.realtimeSinceStartup + timeout;
            Transform root = null;
            AudioSource[] sources = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                var roots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                    .Where(value => value.name == "[AudioPlayback]").ToArray();
                if (roots.Length == 1)
                {
                    root = roots[0];
                    sources = root.GetComponentsInChildren<AudioSource>(true);
                    if (sources.Any(value => value.clip != null && value.clip.name == expectedClipName)) break;
                }
                yield return null;
            }

            Assert.That(root, Is.Not.Null, "The persistent [AudioPlayback] root was not created.");
            var allRoots = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(value => value.name == "[AudioPlayback]").ToArray();
            Assert.That(allRoots, Has.Length.EqualTo(1), "Scene transitions created duplicate audio systems.");
            sources = allRoots[0].GetComponentsInChildren<AudioSource>(true);
            Assert.That(sources.Count(value => value.name.StartsWith("Music-")), Is.EqualTo(2));
            Assert.That(sources.Count(value => value.name.StartsWith("UnitHit-")), Is.EqualTo(4));
            Assert.That(sources.Count(value => value.name.StartsWith("GatherComplete-")), Is.EqualTo(2));
            Assert.That(sources.Any(value => value.clip != null && value.clip.name == expectedClipName), Is.True,
                $"Expected music clip was not active or transitioning: {expectedClipName}");
        }

        private static IEnumerator CaptureValidationScreenshot(string fileName, int width, int height)
        {
#if UNITY_EDITOR
            var gameViewType = System.Type.GetType("UnityEditor.GameView,UnityEditor");
            Assert.That(gameViewType, Is.Not.Null, "Unity Editor GameView type was not found.");
            var gameViews = Resources.FindObjectsOfTypeAll(gameViewType);
            Assert.That(gameViews, Is.Not.Empty, "No Unity Editor GameView is open.");
            var setCustomResolution = gameViewType.GetMethod("SetCustomResolution",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(setCustomResolution, Is.Not.Null, "GameView.SetCustomResolution is unavailable.");
            setCustomResolution.Invoke(gameViews[0], new object[]
            {
                new Vector2(width, height), $"SchemaV14-{width}x{height}"
            });
#else
            Screen.SetResolution(width, height, false);
#endif
            yield return null;
            var resolutionDeadline = Time.realtimeSinceStartup + 10f;
            while ((Screen.width != width || Screen.height != height) && Time.realtimeSinceStartup < resolutionDeadline)
                yield return null;
            Assert.That((Screen.width, Screen.height), Is.EqualTo((width, height)),
                $"GameView resolution did not become {width}x{height}.");
            yield return new WaitForEndOfFrame();
            var directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Docs",
                "ValidationScreenshots", "SchemaV14"));
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, fileName);
            if (File.Exists(path)) File.Delete(path);
            ScreenCapture.CaptureScreenshot(path);
            var deadline = Time.realtimeSinceStartup + 10f;
            while (!File.Exists(path) && Time.realtimeSinceStartup < deadline)
                yield return null;
            Assert.That(File.Exists(path), Is.True, $"Validation screenshot was not written: {path}");
            using var stream = File.OpenRead(path);
            var header = new byte[24];
            Assert.That(stream.Read(header, 0, header.Length), Is.EqualTo(header.Length));
            var capturedWidth = (header[16] << 24) | (header[17] << 16) | (header[18] << 8) | header[19];
            var capturedHeight = (header[20] << 24) | (header[21] << 16) | (header[22] << 8) | header[23];
            Assert.That((capturedWidth, capturedHeight), Is.EqualTo((width, height)),
                $"Captured PNG dimensions do not match {width}x{height}.");
        }

        private static T GetField<T>(object target, string name)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {name} on {target.GetType().Name}.");
            return (T)field.GetValue(target);
        }

        private static void AssertSelectionCardSprites(SelectionPanel selection)
        {
            var viewModel = GetField<SelectionViewModel>(selection, "_viewModel");
            var sprites = GetField<IGameplaySpriteResolver>(selection, "_sprites");
            var cardImages = GetField<Image[]>(selection, "_cardImages");
            Assert.That(cardImages, Has.Length.EqualTo(8));
            for (var index = 0; index < cardImages.Length; index++)
            {
                if (index < viewModel.Cards.Count)
                {
                    var card = viewModel.Cards[index];
                    Assert.That(card.ArtKey.Value, Is.Not.Empty, $"Selection card {card.Id} has no ArtKey.");
                    Assert.That(cardImages[index].sprite, Is.EqualTo(sprites.Resolve(card.ArtKey)),
                        $"Selection slot {index} displays the wrong Sprite for {card.Id}.");
                    Assert.That(cardImages[index].enabled, Is.True);
                }
                else
                {
                    Assert.That(cardImages[index].sprite, Is.Null,
                        $"Hidden Selection slot {index} retained a stale Sprite.");
                    Assert.That(cardImages[index].enabled, Is.False);
                }
            }
        }

        private static void SetField(object target, string name, object value)
        {
            var field = target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field, $"Missing field {name} on {target.GetType().Name}.");
            field.SetValue(target, value);
        }
    }
}
