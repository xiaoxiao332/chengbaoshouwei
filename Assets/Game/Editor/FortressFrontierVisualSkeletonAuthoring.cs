#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FortressFrontier.Bootstrap;
using FortressFrontier.Infrastructure.Resources;
using FortressFrontier.Presentation.Prototype;
using FortressFrontier.Presentation.UI;
using FortressFrontier.Runtime.Scenes;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    public static class FortressFrontierVisualSkeletonAuthoring
    {
        private const string ContentRoot = "Assets/Game/Content";
        private const string ConfigRoot = ContentRoot + "/Config";
        private const string PrefabRoot = ContentRoot + "/Prefabs/UI";
        private const string ArtRoot = "Assets/Game/Art/Formal/PNG";
        private const string SceneRoot = "Assets/Game/Scenes";
        private const string ResourceCatalogPath = ConfigRoot + "/ResourceCatalog.asset";
        private const string PanelCatalogPath = ConfigRoot + "/PanelCatalog.asset";
        private const string BootScenePath = SceneRoot + "/Boot.unity";
        private const string SelectionScenePath = SceneRoot + "/Selection.unity";
        private const string GameplayScenePath = SceneRoot + "/Gameplay.unity";
        private static readonly Color Ink = Hex("211815");
        private static readonly Color Wood = Hex("4C3224");
        private static readonly Color WoodLight = Hex("A66D3F");
        private static readonly Color Parchment = Hex("F2DDA9");
        private static readonly Color Gold = Hex("F6BC4B");
        private static readonly Color Orange = Hex("E96E27");
        private static readonly Color Blue = Hex("327BD1");
        private static readonly Color Purple = Hex("765187");
        private static Font _font;

        [MenuItem("Fortress Frontier/Visual Skeleton/Build All")]
        public static void BuildAll()
        {
            var previousScenePath = SceneManager.GetActiveScene().path;
            try
            {
                EnsureFolders();
                ImportSprites();
                AddressablesProjectTools.ConfigureLocalGroups();
                var assets = new Dictionary<string, string>
                {
                    ["ui.boot"] = CreateBootPrefab(),
                    ["ui.selection"] = CreateSelectionPrefab(),
                    ["ui.gameplay"] = CreateGameplayPrefab(),
                    ["ui.result"] = CreateResultPrefab(),
                    ["ui.loading"] = CreateLoadingPrefab(),
                    ["ui.fatal-error"] = CreateFatalPrefab()
                };

                GameContentConfigAuthoring.BuildBaseline();
                assets["config.game-content"] = GameContentConfigAuthoring.RootAssetPath;

                GetOrCreate<ResourceCatalog>(ResourceCatalogPath);
                GetOrCreate<PanelCatalog>(PanelCatalogPath);
                AssetDatabase.SaveAssets();

                CreateScene<SelectionInstaller>(SelectionScenePath);
                CreateScene<GameplayInstaller>(GameplayScenePath);
                assets["scene.selection"] = SelectionScenePath;
                assets["scene.gameplay"] = GameplayScenePath;

                // Editor scene creation can invalidate previously held UnityEngine.Object
                // wrappers. Reload persistent assets before constructing SerializedObject.
                var resourceCatalog = LoadRequired<ResourceCatalog>(ResourceCatalogPath);
                var panelCatalog = LoadRequired<PanelCatalog>(PanelCatalogPath);
                ConfigureResourceCatalog(resourceCatalog, assets);
                ConfigurePanelCatalog(panelCatalog);
                AssetDatabase.SaveAssets();

                CreateBootScene(resourceCatalog, panelCatalog);
                ConfigureAddressables(assets);
                StartupSettingsAuthoring.BuildAssets();
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(BootScenePath, true) };
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
                Debug.Log("FortressFrontier visual skeleton authored successfully.");
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrWhiteSpace(previousScenePath) &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(previousScenePath) != null)
                {
                    EditorSceneManager.OpenScene(previousScenePath, OpenSceneMode.Single);
                }

                Debug.LogException(exception);
                throw;
            }
        }

        [MenuItem("Fortress Frontier/Visual Skeleton/Validate")]
        public static void Validate()
        {
            var required = new[] { BootScenePath, SelectionScenePath, GameplayScenePath, ResourceCatalogPath, PanelCatalogPath, PrefabRoot + "/Selection.prefab", PrefabRoot + "/Gameplay.prefab", PrefabRoot + "/Result.prefab" };
            foreach (var path in required) if (AssetDatabase.LoadMainAssetAtPath(path) == null) throw new InvalidOperationException("Missing visual skeleton asset: " + path);
            var bootScene = SceneManager.GetActiveScene().path == BootScenePath
                ? SceneManager.GetActiveScene()
                : EditorSceneManager.OpenScene(BootScenePath, OpenSceneMode.Single);
            var globalManager = FindInScene<GlobalManager>(bootScene)
                ?? throw new InvalidOperationException("Boot scene has no GlobalManager.");
            var globalSerialized = new SerializedObject(globalManager);
            if (globalSerialized.FindProperty("_resourceCatalog").objectReferenceValue == null ||
                globalSerialized.FindProperty("_panelCatalog").objectReferenceValue == null ||
                globalSerialized.FindProperty("_uiRootView").objectReferenceValue == null)
            {
                throw new InvalidOperationException("Boot GlobalManager contains missing serialized references.");
            }
            foreach (var path in new[] { SelectionScenePath, GameplayScenePath })
            {
                var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                var count = 0;
                foreach (var root in scene.GetRootGameObjects()) count += root.GetComponentsInChildren<SceneContext>(true).Length;
                EditorSceneManager.CloseScene(scene, true);
                if (count != 1) throw new InvalidOperationException($"{path} must contain exactly one SceneContext; found {count}.");
            }
            Debug.Log("FortressFrontier visual skeleton assets validated.");
        }

        private static string CreateBootPrefab()
        {
            var root = PanelRoot("Boot", typeof(BootPanel));
            AddImage(root.transform, "Backdrop", Color.white, "backdrop_boot", true, Vector2.zero, Vector2.one);
            AddImage(root.transform, "BottomShade", new Color(0.08f, 0.06f, 0.05f, 0.58f), null, false, new Vector2(0, 0), new Vector2(1, .27f));
            var title = AddText(root.transform, "Title", "城垒争锋", 132, TextAnchor.MiddleCenter, Gold, new Vector2(.21f, .63f), new Vector2(.79f, .91f));
            title.gameObject.AddComponent<Outline>().effectColor = Ink;
            var frame = AddImage(root.transform, "ProgressFrame", Ink, null, false, new Vector2(.30f, .105f), new Vector2(.70f, .16f));
            var fill = AddImage(frame.transform, "Fill", Orange, null, false, new Vector2(.015f, .16f), new Vector2(.985f, .84f));
            fill.type = Image.Type.Filled; fill.fillMethod = Image.FillMethod.Horizontal; fill.fillAmount = .2f;
            var status = AddText(root.transform, "Status", "正在进入前线…", 34, TextAnchor.MiddleCenter, Parchment, new Vector2(.30f, .045f), new Vector2(.70f, .10f));
            SetRefs(root.GetComponent<BootPanel>(), ("_progressFill", fill), ("_statusText", status));
            return SavePrefab(root, PrefabRoot + "/Boot.prefab");
        }

        private static string CreateSelectionPrefab()
        {
            var root = PanelRoot("Selection", typeof(SelectionPanel));
            AddImage(root.transform, "Backdrop", Color.white, "backdrop_selection", false, Vector2.zero, Vector2.one);
            AddImage(root.transform, "TopBar", new Color(.07f, .055f, .045f, .98f), null, false, new Vector2(0, .91f), Vector2.one);
            AddText(root.transform, "GameTitle", "◈  城垒争锋", 42, TextAnchor.MiddleLeft, Parchment, new Vector2(.025f, .92f), new Vector2(.28f, .99f));
            var progress = AddText(root.transform, "Progress", "远征进度  2/8", 30, TextAnchor.MiddleCenter, Parchment, new Vector2(.37f, .925f), new Vector2(.61f, .985f));
            var gold = AddText(root.transform, "Gold", "1,280", 34, TextAnchor.MiddleRight, Gold, new Vector2(.78f, .925f), new Vector2(.90f, .985f));
            AddText(root.transform, "Settings", "⚙", 46, TextAnchor.MiddleCenter, Parchment, new Vector2(.925f, .92f), new Vector2(.985f, .99f));

            var categoryButtons = new Button[4]; var categoryFrames = new Image[4];
            var cats = new[] { "▦\n全部", "♜\n士兵", "⌂\n建筑营地", "▱\n战术" };
            for (var i = 0; i < 4; i++)
            {
                var yMax = .875f - i * .17f; var yMin = yMax - .145f;
                categoryButtons[i] = AddButton(root.transform, "Category" + i, cats[i], 27, i == 0 ? Blue : Ink, new Vector2(.025f, yMin), new Vector2(.135f, yMax));
                categoryFrames[i] = categoryButtons[i].GetComponent<Image>();
            }

            var cardButtons = new Button[8]; var cardFrames = new Image[8]; var cardImages = new Image[8]; var cardLabels = new Text[8];
            var art = new[] { "unit_shield_soldier_friendly", "unit_archer_friendly", "building_lumber_camp", "building_lumber_camp", "building_barracks", "prop_arrow_tower_site", "building_engineer_yard", "prop_castle" };
            for (var i = 0; i < 8; i++)
            {
                var col = i % 4; var row = i / 4;
                var x0 = .155f + col * .119f; var x1 = x0 + .108f; var y1 = .875f - row * .27f; var y0 = y1 - .245f;
                var image = AddImage(root.transform, "Card" + i, i == 0 ? Blue : WoodLight, "ui_card_frame", false, new Vector2(x0, y0), new Vector2(x1, y1));
                cardFrames[i] = image; cardButtons[i] = image.gameObject.AddComponent<Button>(); cardButtons[i].targetGraphic = image;
                var icon = AddImage(image.transform, "Art", Color.white, art[i], true, new Vector2(.15f, .32f), new Vector2(.85f, .92f)); icon.raycastTarget = false;
                cardImages[i] = icon;
                cardLabels[i] = AddText(image.transform, "Label", "CARD", 20, TextAnchor.LowerCenter, Ink, new Vector2(.04f, .02f), new Vector2(.96f, .36f));
            }
            var detailPanel = AddImage(root.transform, "CardDetail", Parchment, "ui_panel", false, new Vector2(.15f, .085f), new Vector2(.63f, .315f));
            var detailTitle = AddText(detailPanel.transform, "Title", "盾卫", 36, TextAnchor.MiddleLeft, Ink, new Vector2(.06f, .62f), new Vector2(.40f, .94f));
            var detailBody = AddText(detailPanel.transform, "Body", "坚固前排 · 守护城墙", 23, TextAnchor.UpperLeft, Ink, new Vector2(.06f, .08f), new Vector2(.94f, .64f));
            var unlock = AddButton(detailPanel.transform, "Unlock", "解锁", 22, Orange, new Vector2(.62f, .67f), new Vector2(.78f, .91f));
            var upgrade = AddButton(detailPanel.transform, "Upgrade", "升级", 22, Blue, new Vector2(.80f, .67f), new Vector2(.96f, .91f));

            var map = AddImage(root.transform, "BattlefieldPanel", Parchment, "ui_panel", false, new Vector2(.65f, .085f), new Vector2(.98f, .89f));
            AddText(map.transform, "MapTitle", "—◆  草原前线  ◆—", 38, TextAnchor.MiddleCenter, Ink, new Vector2(.04f, .89f), new Vector2(.96f, .98f));
            var preview = AddImage(map.transform, "MapPreview", Color.white, "backdrop_boot", false, new Vector2(.06f, .60f), new Vector2(.94f, .88f)); preview.raycastTarget = false;
            var mapTitle = map.transform.Find("MapTitle").GetComponent<Text>();
            var previousBattlefield = AddButton(map.transform, "PreviousBattlefield", "‹", 34, Blue, new Vector2(.02f, .90f), new Vector2(.10f, .975f));
            var nextBattlefield = AddButton(map.transform, "NextBattlefield", "›", 34, Blue, new Vector2(.90f, .90f), new Vector2(.98f, .975f));
            AddText(map.transform, "Power", "推荐战力  ⚔ 4,200        推荐等级  Lv.3", 24, TextAnchor.MiddleCenter, Ink, new Vector2(.06f, .54f), new Vector2(.94f, .60f));
            AddText(map.transform, "Boss", "首领预览     中立首领：岩石巨人", 25, TextAnchor.MiddleLeft, Ink, new Vector2(.07f, .44f), new Vector2(.94f, .54f));
            AddImage(map.transform, "BossArt", Color.white, "boss_stone_golem", true, new Vector2(.07f, .30f), new Vector2(.35f, .47f));
            AddText(map.transform, "BossInfo", "沉默的巨人，拥有极高生命与防御。\n击败后获得奖励核心。", 22, TextAnchor.MiddleLeft, Ink, new Vector2(.37f, .30f), new Vector2(.92f, .46f));
            var modeButtons = new Button[3]; var modeFrames = new Image[3]; var modeNames = new[] { "和平发展", "主动进攻", "噩梦" }; var modeColors = new[] { Blue, Orange, Purple };
            for (var i = 0; i < 3; i++) { modeButtons[i] = AddButton(map.transform, "Mode" + i, modeNames[i], 23, modeColors[i], new Vector2(.055f + i * .30f, .225f), new Vector2(.335f + i * .30f, .295f)); modeFrames[i] = modeButtons[i].GetComponent<Image>(); }
            var summary = AddText(map.transform, "ModeSummary", "和平发展 · 经济收益 120% · 奖励 ×1.20", 23, TextAnchor.MiddleCenter, Ink, new Vector2(.06f, .105f), new Vector2(.94f, .215f));
            var start = AddButton(map.transform, "Start", "开始战斗", 43, Orange, new Vector2(.17f, .015f), new Vector2(.83f, .105f));
            SetRefs(root.GetComponent<SelectionPanel>(), ("_goldText", gold), ("_progressText", progress), ("_detailTitle", detailTitle), ("_detailBody", detailBody), ("_modeSummary", summary), ("_battlefieldName", mapTitle), ("_startButton", start), ("_unlockButton", unlock), ("_upgradeButton", upgrade), ("_previousBattlefieldButton", previousBattlefield), ("_nextBattlefieldButton", nextBattlefield));
            SetArray(root.GetComponent<SelectionPanel>(), "_categoryButtons", categoryButtons); SetArray(root.GetComponent<SelectionPanel>(), "_categoryFrames", categoryFrames); SetArray(root.GetComponent<SelectionPanel>(), "_cardButtons", cardButtons); SetArray(root.GetComponent<SelectionPanel>(), "_cardFrames", cardFrames); SetArray(root.GetComponent<SelectionPanel>(), "_cardImages", cardImages); SetArray(root.GetComponent<SelectionPanel>(), "_cardLabels", cardLabels); SetArray(root.GetComponent<SelectionPanel>(), "_modeButtons", modeButtons); SetArray(root.GetComponent<SelectionPanel>(), "_modeFrames", modeFrames);
            return SavePrefab(root, PrefabRoot + "/Selection.prefab");
        }

        private static string CreateGameplayPrefab()
        {
            var root = PanelRoot("Gameplay", typeof(GameplayPanel));
            var world = AddImage(root.transform, "World", Color.white, "backdrop_gameplay", false, Vector2.zero, Vector2.one);
            var worldCanvas = world.gameObject.AddComponent<Canvas>();
            var worldCanvasSerialized = new SerializedObject(worldCanvas);
            worldCanvasSerialized.FindProperty("m_OverrideSorting").boolValue = true;
            worldCanvasSerialized.FindProperty("m_SortingOrder").intValue = 10;
            worldCanvasSerialized.ApplyModifiedPropertiesWithoutUndo();
            world.gameObject.AddComponent<GraphicRaycaster>();
            AddImage(root.transform, "FriendlyWall", Color.white, "prop_wall_friendly", false, new Vector2(.22f, 0), new Vector2(.27f, 1));
            AddImage(root.transform, "EnemyWall", Color.white, "prop_wall_enemy", false, new Vector2(.95f, 0), Vector2.one);
            var buildings = new[] { "building_lumber_camp", "building_lumber_camp", "building_quarry", "building_barracks", "building_farm", null, null, null, null };
            for (var i = 0; i < 9; i++)
            {
                var col = i % 3; var row = i / 3; var x0 = .018f + col * .067f; var x1 = x0 + .058f; var y1 = .79f - row * .185f; var y0 = y1 - .16f;
                AddImage(root.transform, "Slot" + i, Color.white, "prop_building_slot", false, new Vector2(x0, y0), new Vector2(x1, y1));
                if (buildings[i] != null) AddImage(root.transform, "Building" + i, Color.white, buildings[i], true, new Vector2(x0 + .004f, y0 + .014f), new Vector2(x1 - .004f, y1 - .008f));
            }
            var buildingButton = AddButton(root.transform, "BuildingMenuButton", "管理", 20, Wood, new Vector2(.165f, .55f), new Vector2(.215f, .62f));
            var buildingMenu = AddImage(root.transform, "BuildingMenu", Ink, "ui_panel", false, new Vector2(.165f, .32f), new Vector2(.26f, .55f)).gameObject;
            AddText(buildingMenu.transform, "Menu", "⬆  升级\n\nⅡ  暂停\n\n✖  拆除", 23, TextAnchor.MiddleCenter, Parchment, new Vector2(.08f, .08f), new Vector2(.92f, .92f));

            var grid = AddImage(root.transform, "DeployGrid", new Color(.15f, .8f, .67f, .20f), "ui_blueprint", false, new Vector2(.285f, .19f), new Vector2(.52f, .60f)).gameObject;
            var blueprintImage = grid.GetComponent<Image>();
            var blueprintButton = AddButton(root.transform, "Blueprint", "蓝图状态", 18, Wood, new Vector2(.285f, .60f), new Vector2(.365f, .655f));
            AddImage(root.transform, "TowerSite", Color.white, "prop_arrow_tower_site", true, new Vector2(.47f, .25f), new Vector2(.59f, .48f));
            AddImage(root.transform, "Boss", Color.white, "boss_stone_golem", true, new Vector2(.54f, .43f), new Vector2(.69f, .70f));
            var unitSprites = new[] { "unit_worker_friendly", "unit_shield_soldier_friendly", "unit_archer_friendly", "unit_shield_soldier_enemy", "unit_raider_enemy", "unit_raider_enemy", "unit_archer_enemy" };
            for (var i = 0; i < unitSprites.Length; i++) { var x = .36f + i * .075f; var y = .35f + (i % 3) * .10f; AddImage(root.transform, "Unit" + i, Color.white, unitSprites[i], true, new Vector2(x, y), new Vector2(x + .055f, y + .11f)); }

            for (var i = 0; i < 4; i++)
            {
                var resourceGroup = AddImage(root.transform, "ResourceGroup" + i, new Color(.08f, .065f, .05f, .88f), null, false, new Vector2(i * .077f, .90f), new Vector2(i * .077f + .075f, .995f));
                var labels = new[] { "食物  肉  酒\n860   620  240", "木材  木板\n980    420", "原石  石料\n760    560", "铁矿  铁锭\n540    330" };
                AddText(resourceGroup.transform, "Values", labels[i], 18, TextAnchor.MiddleCenter, Parchment, new Vector2(.02f, .02f), new Vector2(.98f, .98f));
            }
            AddText(root.transform, "FriendlyHp", "🛡 我方城墙   18,500 / 18,500", 22, TextAnchor.MiddleCenter, Parchment, new Vector2(.34f, .94f), new Vector2(.56f, .995f));
            AddText(root.transform, "Clock", "06:42\n争夺阶段", 24, TextAnchor.MiddleCenter, Color.white, new Vector2(.56f, .925f), new Vector2(.65f, .997f));
            AddText(root.transform, "EnemyHp", "敌方城墙   18,500 / 18,500", 22, TextAnchor.MiddleCenter, Parchment, new Vector2(.65f, .94f), new Vector2(.87f, .995f));
            AddText(root.transform, "BossHp", "中立 · 巨石守护者       9,800 / 9,800", 20, TextAnchor.MiddleCenter, Parchment, new Vector2(.40f, .84f), new Vector2(.72f, .89f));
            AddButton(root.transform, "Pause", "Ⅱ", 28, Ink, new Vector2(.87f, .92f), new Vector2(.91f, .985f));
            AddButton(root.transform, "Speed", "1×", 24, Ink, new Vector2(.915f, .92f), new Vector2(.955f, .985f));

            var tray = AddImage(root.transform, "CardTray", Ink, "ui_panel", false, new Vector2(.28f, .005f), new Vector2(.80f, .14f));
            var soldierTab = AddButton(tray.transform, "SoldierTab", "♜\n兵种", 24, Orange, new Vector2(.015f, .08f), new Vector2(.12f, .86f));
            var itemTab = AddButton(tray.transform, "ItemTab", "◉\n道具", 24, Ink, new Vector2(.88f, .08f), new Vector2(.985f, .86f));
            var soldierCards = AddRect(tray.transform, "SoldierCards", new Vector2(.13f, .04f), new Vector2(.875f, .96f)).gameObject;
            var itemCards = AddRect(tray.transform, "ItemCards", new Vector2(.13f, .04f), new Vector2(.875f, .96f)).gameObject;
            var cardButtons = new Button[7]; var cardRects = new RectTransform[7];
            var names = new[] { "盾卫\n营地 5/5\n队列 6 · 12秒", "弓手\n营地 4/5\n队列 5 · 8秒", "长矛兵\n营地 5/5\n队列 3 · 15秒", "重锤兵\n营地 3/5\n队列 2 · 18秒" };
            var cardArt = new[] { "unit_shield_soldier_friendly", "unit_archer_friendly", "unit_shield_soldier_friendly", "unit_worker_friendly" };
            for (var i = 0; i < 4; i++) { cardButtons[i] = AddButton(soldierCards.transform, "Soldier" + i, names[i], 17, i == 0 ? Orange : Parchment, new Vector2(i / 4f + .008f, 0), new Vector2((i + 1) / 4f - .008f, 1)); cardRects[i] = cardButtons[i].GetComponent<RectTransform>(); AddImage(cardButtons[i].transform, "Art", Color.white, cardArt[i], true, new Vector2(.05f, .22f), new Vector2(.43f, .90f)).raycastTarget = false; }
            var itemNames = new[] { "迅捷号角", "工程补给", "烈焰瓶" };
            for (var i = 0; i < 3; i++) { cardButtons[4 + i] = AddButton(itemCards.transform, "Item" + i, itemNames[i] + "\n一次性战术道具", 19, Parchment, new Vector2(i * .32f, .08f), new Vector2(i * .32f + .29f, .92f)); cardRects[4 + i] = cardButtons[4 + i].GetComponent<RectTransform>(); }
            itemCards.SetActive(false);
            var itemCount = AddText(tray.transform, "ItemCount", "4/6", 24, TextAnchor.MiddleCenter, Gold, new Vector2(.88f, 0), new Vector2(.985f, .18f));
            var useItem = AddButton(root.transform, "UseItem", "使用道具", 19, Purple, new Vector2(.86f, .13f), new Vector2(.945f, .19f));
            var researchButton = AddButton(root.transform, "ResearchButton", "研究", 18, Blue, new Vector2(.925f, .06f), new Vector2(.985f, .12f));
            var resultButton = AddButton(root.transform, "ResultButton", "结算演示", 17, Orange, new Vector2(.87f, .005f), new Vector2(.98f, .055f));



            var choice = AddImage(root.transform, "ChoicePanel", Parchment, "ui_panel", false, new Vector2(.34f, .28f), new Vector2(.72f, .72f)).gameObject;
            AddText(choice.transform, "Title", "战后整备 · 三选一", 34, TextAnchor.MiddleCenter, Ink, new Vector2(.08f, .79f), new Vector2(.92f, .96f));
            var choiceOptions = new Button[3]; var choiceLabels = new[] { "高效物流\n采集收益提升", "加固城墙\n提高耐久上限", "精锐先锋\n训练效率提升" };
            for (var i = 0; i < 3; i++) choiceOptions[i] = AddButton(choice.transform, "Choice" + i, choiceLabels[i], 22, i == 0 ? Blue : i == 1 ? WoodLight : Orange, new Vector2(.05f + i * .31f, .12f), new Vector2(.33f + i * .31f, .74f));
            choice.SetActive(false);
            var research = AddImage(root.transform, "ResearchPanel", Parchment, "ui_panel", false, new Vector2(.39f, .31f), new Vector2(.68f, .68f)).gameObject;
            AddText(research.transform, "Research", "研究点分配\n\n经济  ●●●○○\n训练  ●●○○○\n城防  ●●●●○\n\n示例数据 · 不消耗真实资源", 25, TextAnchor.MiddleCenter, Ink, new Vector2(.08f, .08f), new Vector2(.92f, .92f)); research.SetActive(false);
            buildingMenu.SetActive(false);
            var panel = root.GetComponent<GameplayPanel>();
            SetRefs(panel, ("_soldierTabButton", soldierTab), ("_itemTabButton", itemTab), ("_soldierTabFrame", soldierTab.GetComponent<Image>()), ("_itemTabFrame", itemTab.GetComponent<Image>()), ("_soldierCards", soldierCards), ("_itemCards", itemCards), ("_buildingButton", buildingButton), ("_buildingMenu", buildingMenu), ("_blueprintButton", blueprintButton), ("_blueprintImage", blueprintImage), ("_deploymentGrid", grid), ("_useItemButton", useItem), ("_itemCountText", itemCount), ("_choicePanel", choice), ("_researchButton", researchButton), ("_researchPanel", research), ("_resultButton", resultButton));
            SetArray(panel, "_cardButtons", cardButtons); SetArray(panel, "_cardRects", cardRects); SetArray(panel, "_choiceOptions", choiceOptions);
            return SavePrefab(root, PrefabRoot + "/Gameplay.prefab");
        }

        private static string CreateResultPrefab()
        {
            var root = PanelRoot("Result", typeof(ResultPanel));
            AddImage(root.transform, "Dim", new Color(.04f, .03f, .025f, .82f), null, false, Vector2.zero, Vector2.one);
            var panel = AddImage(root.transform, "Panel", Parchment, "ui_panel", false, new Vector2(.30f, .19f), new Vector2(.70f, .81f));
            AddImage(panel.transform, "Core", Color.white, "icon_reward_core", true, new Vector2(.80f, .80f), new Vector2(.94f, .95f));
            var title = AddText(panel.transform, "Title", "防线守住了！", 50, TextAnchor.MiddleCenter, Orange, new Vector2(.08f, .82f), new Vector2(.78f, .97f));
            var summary = AddText(panel.transform, "Summary", "战场与战况分析", 20, TextAnchor.UpperLeft, Ink, new Vector2(.08f, .20f), new Vector2(.92f, .78f));
            var timelineScroll = AddImage(panel.transform, "TimelineScroll", new Color(.12f, .10f, .08f, .12f), null, false, new Vector2(.08f, .20f), new Vector2(.92f, .78f));
            var mask = timelineScroll.gameObject.AddComponent<Mask>(); mask.showMaskGraphic = false;
            var scrollRect = timelineScroll.gameObject.AddComponent<ScrollRect>(); scrollRect.horizontal = false; scrollRect.vertical = true;
            summary.transform.SetParent(timelineScroll.transform, false);
            var summaryRect = summary.rectTransform; summaryRect.anchorMin = new Vector2(0, 1); summaryRect.anchorMax = new Vector2(1, 1); summaryRect.pivot = new Vector2(.5f, 1); summaryRect.offsetMin = new Vector2(12, -1600); summaryRect.offsetMax = new Vector2(-12, 0);
            scrollRect.viewport = timelineScroll.rectTransform; scrollRect.content = summaryRect;
            summary.horizontalOverflow = HorizontalWrapMode.Wrap; summary.verticalOverflow = VerticalWrapMode.Overflow;
            timelineScroll.rectTransform.anchorMin = new Vector2(.08f, .35f);
            var rewarded = AddButton(panel.transform, "RewardedAd", "同意隐私政策并观看 · 奖励金币", 24, Orange, new Vector2(.18f, .18f), new Vector2(.82f, .29f));
            var privacy = AddButton(panel.transform, "PrivacyPolicy", "查看隐私政策", 18, Blue, new Vector2(.36f, .13f), new Vector2(.64f, .18f));
            var adStatus = AddText(panel.transform, "RewardedAdStatus", string.Empty, 17, TextAnchor.MiddleCenter, Ink, new Vector2(.10f, .29f), new Vector2(.90f, .34f));
            rewarded.gameObject.SetActive(false); privacy.gameObject.SetActive(false); adStatus.gameObject.SetActive(false);
            var button = AddButton(panel.transform, "Return", "返回整备大厅", 27, Blue, new Vector2(.25f, .03f), new Vector2(.75f, .13f));
            SetRefs(root.GetComponent<ResultPanel>(), ("_title", title), ("_summary", summary), ("_returnButton", button),
                ("_rewardedAdButton", rewarded), ("_rewardedAdLabel", rewarded.GetComponentInChildren<Text>()),
                ("_rewardedAdStatus", adStatus), ("_privacyPolicyButton", privacy));
            return SavePrefab(root, PrefabRoot + "/Result.prefab");
        }

        private static string CreateLoadingPrefab()
        {
            var root = PanelRoot("Loading", typeof(LoadingOverlayPanel));
            AddImage(root.transform, "Blocker", new Color(.03f, .025f, .02f, .55f), null, false, Vector2.zero, Vector2.one);
            var panel = AddImage(root.transform, "Panel", Ink, "ui_panel", false, new Vector2(.39f, .42f), new Vector2(.61f, .58f));
            var label = AddText(panel.transform, "Label", "调度远征···", 30, TextAnchor.MiddleCenter, Parchment, new Vector2(.05f, .1f), new Vector2(.95f, .9f));
            SetRefs(root.GetComponent<LoadingOverlayPanel>(), ("_label", label));
            return SavePrefab(root, PrefabRoot + "/Loading.prefab");
        }

        private static string CreateFatalPrefab()
        {
            var root = PanelRoot("FatalError", typeof(FatalErrorPanel));
            AddImage(root.transform, "Dim", new Color(.05f, .02f, .02f, .72f), null, false, Vector2.zero, Vector2.one);
            var panel = AddImage(root.transform, "Panel", Hex("8E352F"), "ui_panel_danger", false, new Vector2(.30f, .30f), new Vector2(.70f, .70f));
            AddText(panel.transform, "Title", "无法继续调度", 40, TextAnchor.MiddleCenter, Parchment, new Vector2(.08f, .72f), new Vector2(.92f, .94f));
            var message = AddText(panel.transform, "Message", "错误详情", 23, TextAnchor.MiddleCenter, Parchment, new Vector2(.08f, .28f), new Vector2(.92f, .70f));
            var close = AddButton(panel.transform, "Close", "关闭", 28, Ink, new Vector2(.34f, .07f), new Vector2(.66f, .24f));
            SetRefs(root.GetComponent<FatalErrorPanel>(), ("_message", message), ("_closeButton", close));
            return SavePrefab(root, PrefabRoot + "/FatalError.prefab");
        }

        private static void CreateBootScene(ResourceCatalog resources, PanelCatalog panels)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            // NewScene can invalidate Unity object wrappers held by the caller. Resolve
            // persistent assets inside the new scene context immediately before wiring.
            resources = LoadRequired<ResourceCatalog>(ResourceCatalogPath);
            panels = LoadRequired<PanelCatalog>(PanelCatalogPath);
            AddCamera(true);
            var uiRoot = new GameObject("UIRoot", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(UIRootView));
            uiRoot.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = uiRoot.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = .5f;
            var safe = AddRect(uiRoot.transform, "SafeArea", Vector2.zero, Vector2.one);
            var bg = AddLayer(uiRoot.transform, "Bg", 0); var window = AddLayer(safe, "Window", 100); var pop = AddLayer(safe, "Pop", 200); var over = AddLayer(safe, "Over", 300);
            SetRefs(uiRoot.GetComponent<UIRootView>(), ("_bgRoot", bg), ("_windowRoot", window), ("_popRoot", pop), ("_overRoot", over), ("_safeAreaRoot", safe));
            var eventSystem = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule)); eventSystem.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var globalObject = new GameObject("GlobalManager", typeof(GlobalManager), typeof(GameLoopDriver));
            SetRefs(globalObject.GetComponent<GlobalManager>(), ("_resourceCatalog", resources), ("_panelCatalog", panels), ("_uiRootView", uiRoot.GetComponent<UIRootView>()));
            SetRefs(globalObject.GetComponent<GameLoopDriver>(), ("_globalManager", globalObject.GetComponent<GlobalManager>()));
            EditorSceneManager.SaveScene(scene, BootScenePath);
        }

        private static void CreateScene<TInstaller>(string path) where TInstaller : SceneSystemInstallerBase
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AddCamera(false);
            var contextObject = new GameObject("SceneContext"); var context = contextObject.AddComponent<SceneContext>(); var installer = contextObject.AddComponent<TInstaller>();
            SetArray(context, "_installers", new SceneSystemInstallerBase[] { installer });
            EditorSceneManager.SaveScene(scene, path);
        }

        private static void AddCamera(bool addAudioListener)
        {
            var cameraObject = new GameObject("Main Camera", typeof(Camera));
            if (addAudioListener) cameraObject.AddComponent<AudioListener>();
            cameraObject.tag = "MainCamera"; cameraObject.transform.position = new Vector3(0, 0, -10); var camera = cameraObject.GetComponent<Camera>(); camera.orthographic = true; camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Ink;
        }

        private static void ConfigureResourceCatalog(ResourceCatalog catalog, Dictionary<string, string> assets)
        {
            if (catalog == null) throw new InvalidOperationException($"ResourceCatalog is unavailable: {ResourceCatalogPath}");
            var so = new SerializedObject(catalog); var entries = so.FindProperty("_entries"); entries.arraySize = assets.Count; var i = 0;
            foreach (var pair in assets) { var item = entries.GetArrayElementAtIndex(i++); item.FindPropertyRelative("_id").stringValue = pair.Key; item.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue = AssetDatabase.AssetPathToGUID(pair.Value); item.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue = pair.Key == "config.game-content"; }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
        }

        private static void ConfigurePanelCatalog(PanelCatalog catalog)
        {
            if (catalog == null) throw new InvalidOperationException($"PanelCatalog is unavailable: {PanelCatalogPath}");
            var definitions = new[] { ("ui.boot", "ui.boot", 1, true), ("ui.selection", "ui.selection", 1, true), ("ui.gameplay", "ui.gameplay", 1, true), ("ui.result", "ui.result", 2, true), ("ui.loading", "ui.loading", 3, true), ("ui.fatal-error", "ui.fatal-error", 2, true) };
            var so = new SerializedObject(catalog); var panels = so.FindProperty("_panels"); panels.arraySize = definitions.Length;
            for (var i = 0; i < definitions.Length; i++) { var item = panels.GetArrayElementAtIndex(i); item.FindPropertyRelative("_id").stringValue = definitions[i].Item1; item.FindPropertyRelative("_resourceId").stringValue = definitions[i].Item2; item.FindPropertyRelative("_layer").enumValueIndex = definitions[i].Item3; item.FindPropertyRelative("_cacheWhenClosed").boolValue = definitions[i].Item4; }
            var states = so.FindProperty("_states"); var windowIds = new[] { "ui.boot", "ui.selection", "ui.gameplay", "ui.fatal-error" }; states.arraySize = 4;
            for (var i = 0; i < 4; i++) { var state = states.GetArrayElementAtIndex(i); state.FindPropertyRelative("_state").enumValueIndex = i; state.FindPropertyRelative("_backgroundPanelId").stringValue = string.Empty; state.FindPropertyRelative("_windowPanelId").stringValue = windowIds[i]; state.FindPropertyRelative("_overlayPanelIds").arraySize = 0; }
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(catalog);
        }

        private static void ConfigureAddressables(Dictionary<string, string> assets)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings ?? throw new InvalidOperationException("Addressables settings missing.");
            foreach (var pair in assets) { var groupName = pair.Key.StartsWith("scene.", StringComparison.Ordinal) ? "Local-Scenes" : pair.Key.StartsWith("config.", StringComparison.Ordinal) ? "Local-Core" : "Local-UI"; var group = settings.FindGroup(groupName) ?? throw new InvalidOperationException("Addressables local group missing."); var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(pair.Value), group, false, false); entry.address = pair.Key; }
            EditorUtility.SetDirty(settings);
        }

        private static void ImportSprites()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { ArtRoot }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid); if (AssetImporter.GetAtPath(path) is not TextureImporter importer) continue;
                importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single; importer.alphaIsTransparency = true; importer.mipmapEnabled = false; importer.textureCompression = TextureImporterCompression.Uncompressed; importer.maxTextureSize = 2048; importer.SaveAndReimport();
            }
        }

        private static void EnsureFolders()
        {
            EnsureFolder("Assets/Game", "Content"); EnsureFolder(ContentRoot, "Config"); EnsureFolder(ContentRoot, "Prefabs"); EnsureFolder(ContentRoot + "/Prefabs", "UI"); EnsureFolder("Assets/Game", "Scenes");
        }
        private static void EnsureFolder(string parent, string child) { var path = parent + "/" + child; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child); }
        private static T GetOrCreate<T>(string path) where T : ScriptableObject { var asset = AssetDatabase.LoadAssetAtPath<T>(path); if (asset != null) return asset; asset = ScriptableObject.CreateInstance<T>(); AssetDatabase.CreateAsset(asset, path); return asset; }
        private static T LoadRequired<T>(string path) where T : UnityEngine.Object =>
            AssetDatabase.LoadAssetAtPath<T>(path) ?? throw new InvalidOperationException($"Required asset could not be reloaded: {path}");
        private static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component != null) return component;
            }
            return null;
        }
        private static GameObject PanelRoot(string name, Type component) { var root = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup), component); Stretch(root.GetComponent<RectTransform>()); return root; }
        private static string SavePrefab(GameObject root, string path) { PrefabUtility.SaveAsPrefabAsset(root, path); UnityEngine.Object.DestroyImmediate(root); return path; }
        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>($"{ArtRoot}/{name}.png");

        private static Image AddImage(Transform parent, string name, Color color, string sprite, bool preserve, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image)); go.transform.SetParent(parent, false); var image = go.GetComponent<Image>(); image.color = color; image.preserveAspect = preserve; if (!string.IsNullOrEmpty(sprite)) image.sprite = Sprite(sprite); SetRect(image.rectTransform, min, max); return image;
        }
        private static Text AddText(Transform parent, string name, string value, int size, TextAnchor anchor, Color color, Vector2 min, Vector2 max)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Text)); go.transform.SetParent(parent, false); var text = go.GetComponent<Text>(); text.font = _font ??= Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); text.text = value; text.fontSize = size; text.alignment = anchor; text.color = color; text.raycastTarget = false; text.resizeTextForBestFit = true; text.resizeTextMinSize = Math.Max(12, size / 2); text.resizeTextMaxSize = size; SetRect(text.rectTransform, min, max); return text;
        }
        private static Button AddButton(Transform parent, string name, string label, int size, Color color, Vector2 min, Vector2 max)
        {
            var image = AddImage(parent, name, color, "ui_button_secondary", false, min, max); var button = image.gameObject.AddComponent<Button>(); button.targetGraphic = image; var text = AddText(image.transform, "Label", label, size, TextAnchor.MiddleCenter, color.grayscale > .55f ? Ink : Parchment, new Vector2(.06f, .08f), new Vector2(.94f, .92f)); text.raycastTarget = false; return button;
        }
        private static RectTransform AddRect(Transform parent, string name, Vector2 min, Vector2 max) { var go = new GameObject(name, typeof(RectTransform)); go.transform.SetParent(parent, false); var rect = go.GetComponent<RectTransform>(); SetRect(rect, min, max); return rect; }
        private static RectTransform AddLayer(Transform parent, string name, int order) { var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster)); go.transform.SetParent(parent, false); var rect = go.GetComponent<RectTransform>(); Stretch(rect); var canvas = go.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = order; return rect; }
        private static void Stretch(RectTransform rect) => SetRect(rect, Vector2.zero, Vector2.one);
        private static void SetRect(RectTransform rect, Vector2 min, Vector2 max) { rect.anchorMin = min; rect.anchorMax = max; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero; rect.localScale = Vector3.one; }
        private static Color Hex(string value) { ColorUtility.TryParseHtmlString("#" + value, out var color); return color; }
        private static void SetRefs(UnityEngine.Object target, params (string, UnityEngine.Object)[] values) { var so = new SerializedObject(target); foreach (var value in values) { var property = so.FindProperty(value.Item1) ?? throw new InvalidOperationException($"Missing serialized property {value.Item1} on {target.GetType().Name}"); property.objectReferenceValue = value.Item2; } so.ApplyModifiedPropertiesWithoutUndo(); }
        private static void SetArray<T>(UnityEngine.Object target, string name, T[] values) where T : UnityEngine.Object { var so = new SerializedObject(target); var property = so.FindProperty(name) ?? throw new InvalidOperationException($"Missing serialized array {name}"); property.arraySize = values.Length; for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).objectReferenceValue = values[i]; so.ApplyModifiedPropertiesWithoutUndo(); }
    }
}
#endif
