#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using FortressFrontier.Infrastructure.Resources;
using FortressFrontier.Presentation.Prototype;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Editor
{
    internal static class VerticalSliceAuthoring
    {
        private const string GameplayPrefab = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";
        private const string WorldPrefabRoot = "Assets/Game/Content/Prefabs/World";
        private const string ResourceCatalogPath = "Assets/Game/Content/Config/ResourceCatalog.asset";
        private const string ArtRoot = "Assets/Game/Art/Formal/PNG/";
        private const string UnitSlotFramePath = ArtRoot + "ui_unit_slot_frame.png";
        private const string SoldierTabFramePath = ArtRoot + "ui_soldier_tab_frame.png";

        private static readonly (string key, string file, Color color, Vector2 size)[] WorldAssets =
        {
            ("world.resource.food", "prop_berry", Color.white, new Vector2(112, 112)),
            ("world.resource.wood", "prop_tree", Color.white, new Vector2(132, 156)),
            ("world.resource.raw-stone", "prop_stone", Color.white, new Vector2(118, 96)),
            ("world.resource.iron-ore", "prop_stone", new Color(0.72f, 0.82f, 0.92f, 1f), new Vector2(118, 96)),
            ("world.gatherer.player", "unit_worker_friendly", Color.white, new Vector2(68, 82)),
            ("world.gatherer.enemy", "unit_worker_enemy", Color.white, new Vector2(68, 82)),
            ("world.worker.food.player", "SchemaV12/worker_food_friendly", Color.white, new Vector2(68, 82)),
            ("world.worker.food.enemy", "SchemaV12/worker_food_enemy", Color.white, new Vector2(68, 82)),
            ("world.worker.wood.player", "SchemaV12/worker_wood_friendly", Color.white, new Vector2(68, 82)),
            ("world.worker.wood.enemy", "SchemaV12/worker_wood_enemy", Color.white, new Vector2(68, 82)),
            ("world.worker.stone.player", "SchemaV12/worker_stone_friendly", Color.white, new Vector2(68, 82)),
            ("world.worker.stone.enemy", "SchemaV12/worker_stone_enemy", Color.white, new Vector2(68, 82)),
            ("world.worker.iron.player", "SchemaV12/worker_iron_friendly", Color.white, new Vector2(68, 82)),
            ("world.worker.iron.enemy", "SchemaV12/worker_iron_enemy", Color.white, new Vector2(68, 82)),
            ("world.unit.shield.player", "unit_shield_soldier_friendly", Color.white, new Vector2(78, 92)),
            ("world.unit.shield.enemy", "unit_shield_soldier_enemy", Color.white, new Vector2(78, 92)),
            ("world.unit.archer.player", "unit_archer_friendly", Color.white, new Vector2(78, 92)),
            ("world.unit.archer.enemy", "unit_archer_enemy", Color.white, new Vector2(78, 92)),
            ("world.unit.ram.player", "unit_siege_ram_friendly", Color.white, new Vector2(104, 88)),
            ("world.unit.ram.enemy", "unit_siege_ram_enemy", Color.white, new Vector2(104, 88)),
            ("world.unit.heavy-warrior.player", "SchemaV12/unit_heavy_warrior_friendly", Color.white, new Vector2(88, 104)),
            ("world.unit.heavy-warrior.enemy", "SchemaV12/unit_heavy_warrior_enemy", Color.white, new Vector2(88, 104)),
            ("world.unit.mage.player", "SchemaV12/unit_mage_friendly", Color.white, new Vector2(78, 94)),
            ("world.unit.mage.enemy", "SchemaV12/unit_mage_enemy", Color.white, new Vector2(78, 94)),
            ("world.unit.longbow.player", "SchemaV12/unit_longbow_friendly", Color.white, new Vector2(76, 98)),
            ("world.unit.longbow.enemy", "SchemaV12/unit_longbow_enemy", Color.white, new Vector2(76, 98)),
            ("world.unit.cannon.player", "SchemaV12/unit_cannon_cart_friendly", Color.white, new Vector2(112, 82)),
            ("world.unit.cannon.enemy", "SchemaV12/unit_cannon_cart_enemy", Color.white, new Vector2(112, 82)),
            ("world.builder.player", "unit_builder_friendly", Color.white, new Vector2(72, 88)),
            ("world.builder.enemy", "unit_builder_enemy", Color.white, new Vector2(72, 88)),
            ("world.tower-site.player", "building_arrow_tower_site_friendly", Color.white, new Vector2(96, 112)),
            ("world.tower-site.enemy", "building_arrow_tower_site_enemy", Color.white, new Vector2(96, 112)),
            ("world.tower.player", "building_arrow_tower_friendly", Color.white, new Vector2(106, 142)),
            ("world.tower.enemy", "building_arrow_tower_enemy", Color.white, new Vector2(106, 142)),
            ("world.boss", "boss_stone_golem", Color.white, new Vector2(156, 174)),
            ("world.boss-core", "pickup_boss_supply", Color.white, new Vector2(74, 74)),
            ("world.projectile.arrow", "projectile_arrow", Color.white, new Vector2(58, 22)),
            ("world.projectile.fireball", "SchemaV12/projectile_fireball", Color.white, new Vector2(52, 30)),
            ("world.projectile.cannonball", "SchemaV12/projectile_cannonball", Color.white, new Vector2(48, 30)),
            ("world.boss-warning-zone", "boss_skill_warning_zone", Color.white, new Vector2(112, 112)),
            ("world.boss-meteor", "boss_meteor", Color.white, new Vector2(78, 96))
        };

        [MenuItem("Fortress Frontier/Vertical Slice/Build 6-Minute Slice")]
        public static void Build()
        {
            GameContentConfigAuthoring.BuildBaseline();
            P1WorldAuthoring.Build();
            EnsureFolder("Assets/Game/Content/Prefabs", "World");
            BuildWorldPrefabs();
            BindGameplayPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            GameContentConfigAuthoring.ValidateConfig();
            Debug.Log("FortressFrontier six-minute vertical slice assets were authored and validated.");
        }

        [MenuItem("Fortress Frontier/Vertical Slice/Rebind Gameplay Presentation")]
        public static void RebindGameplayPresentation()
        {
            BuildWorldPrefabs();
            BindGameplayPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        [MenuItem("Fortress Frontier/Vertical Slice/Rebind Unit Cards")]
        public static void RebindUnitCards()
        {
            ConfigureUnitSlotFrameImporter();
            ConfigureSoldierTabFrameImporter();
            var root = PrefabUtility.LoadPrefabContents(GameplayPrefab);
            try
            {
                ApplyUnitSlotFrame(root);
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebound soldier tab/card frames and cleared authoring-only soldier art defaults.");
        }

[MenuItem("Fortress Frontier/Vertical Slice/Rebuild Animated Unit Prefabs")]
        public static void RebuildAnimatedUnitPrefabs()
        {
            EnsureFolder("Assets/Game/Content/Prefabs", "World");
            foreach (var definition in WorldAssets)
            {
                if (!IsAnimatedActor(definition.key)) continue;
                var path = $"{WorldPrefabRoot}/{definition.key.Replace('.', '_')}.prefab";
                CreateWorldPrefab(path, definition.file, definition.color, definition.size, true);
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Rebuilt 8 animated unit and gatherer prefabs with VisualPivot.");
        }

        private static bool IsAnimatedActor(string key) =>
            key.StartsWith("world.gatherer.", StringComparison.Ordinal) ||
            key.StartsWith("world.worker.", StringComparison.Ordinal) ||
            key.StartsWith("world.unit.", StringComparison.Ordinal) ||
            key == "world.boss";


        internal static void BuildWorldPrefabs()
        {
            var catalog = AssetDatabase.LoadAssetAtPath<ResourceCatalog>(ResourceCatalogPath)
                ?? throw new InvalidOperationException($"Missing ResourceCatalog: {ResourceCatalogPath}");
            var catalogObject = new SerializedObject(catalog);
            var entries = catalogObject.FindProperty("_entries");
            var indexes = new Dictionary<string, int>(StringComparer.Ordinal);
            for (var index = 0; index < entries.arraySize; index++)
                indexes[entries.GetArrayElementAtIndex(index).FindPropertyRelative("_id").stringValue] = index;

            var settings = AddressableAssetSettingsDefaultObject.Settings
                ?? throw new InvalidOperationException("Addressables settings are missing.");
            var group = settings.FindGroup("Local-UI")
                ?? throw new InvalidOperationException("Addressables group 'Local-UI' is missing.");

            foreach (var definition in WorldAssets)
            {
                var path = $"{WorldPrefabRoot}/{definition.key.Replace('.', '_')}.prefab";
                CreateWorldPrefab(path, definition.file, definition.color, definition.size, IsAnimatedActor(definition.key));
                if (!indexes.TryGetValue(definition.key, out var index))
                {
                    index = entries.arraySize;
                    entries.InsertArrayElementAtIndex(index);
                    indexes.Add(definition.key, index);
                }
                var entry = entries.GetArrayElementAtIndex(index);
                entry.FindPropertyRelative("_id").stringValue = definition.key;
                var guid = AssetDatabase.AssetPathToGUID(path);
                entry.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue = guid;
                entry.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue = true;
                settings.CreateOrMoveEntry(guid, group, false, false).address = definition.key;
            }
            const string routeKey = "world.enemy-order-route";
            var routePath = $"{WorldPrefabRoot}/world_enemy-order-route.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(routePath) == null)
                throw new InvalidOperationException($"MCP-authored enemy route prefab is missing: {routePath}");
            if (!indexes.TryGetValue(routeKey, out var routeIndex))
            {
                routeIndex = entries.arraySize;
                entries.InsertArrayElementAtIndex(routeIndex);
            }
            var routeEntry = entries.GetArrayElementAtIndex(routeIndex);
            routeEntry.FindPropertyRelative("_id").stringValue = routeKey;
            var routeGuid = AssetDatabase.AssetPathToGUID(routePath);
            routeEntry.FindPropertyRelative("_reference").FindPropertyRelative("m_AssetGUID").stringValue = routeGuid;
            routeEntry.FindPropertyRelative("_excludeFromGameObjectPreload").boolValue = true;
            settings.CreateOrMoveEntry(routeGuid, group, false, false).address = routeKey;
            catalogObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(catalog);
            EditorUtility.SetDirty(settings);
        }

private static void CreateWorldPrefab(string path, string spriteName, Color color, Vector2 size, bool animatedActor)
        {
            var root = animatedActor
                ? new GameObject("WorldEntity", typeof(RectTransform), typeof(GameplayWorldEntityView))
                : new GameObject("WorldEntity", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(GameplayWorldEntityView));
            try
            {
                var rect = root.GetComponent<RectTransform>();
                rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.zero; rect.pivot = new Vector2(0.5f, 0.5f); rect.sizeDelta = size;

                RectTransform visualPivot;
                Image image;
                if (animatedActor)
                {
                    var visualObject = new GameObject("VisualPivot", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                    visualObject.transform.SetParent(root.transform, false);
                    visualPivot = visualObject.GetComponent<RectTransform>();
                    visualPivot.anchorMin = Vector2.zero; visualPivot.anchorMax = Vector2.one;
                    visualPivot.offsetMin = Vector2.zero; visualPivot.offsetMax = Vector2.zero;
                    visualPivot.pivot = new Vector2(0.5f, 0.5f);
                    image = visualObject.GetComponent<Image>();
                }
                else
                {
                    image = root.GetComponent<Image>();
                    visualPivot = image.rectTransform;
                }

                image.sprite = Sprite(spriteName); image.color = color; image.preserveAspect = true; image.raycastTarget = false;
                var labelObject = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                labelObject.transform.SetParent(root.transform, false);
                var labelRect = labelObject.GetComponent<RectTransform>();
                labelRect.anchorMin = new Vector2(-0.25f, -0.30f); labelRect.anchorMax = new Vector2(1.25f, 0.10f);
                labelRect.offsetMin = Vector2.zero; labelRect.offsetMax = Vector2.zero;
                var label = labelObject.GetComponent<Text>();
                label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); label.fontSize = 18;
                label.alignment = TextAnchor.MiddleCenter; label.color = new Color(1f, 0.95f, 0.72f, 1f);
                label.raycastTarget = false; label.resizeTextForBestFit = true;
                var view = new SerializedObject(root.GetComponent<GameplayWorldEntityView>());
                view.FindProperty("_visualPivot").objectReferenceValue = visualPivot;
                view.FindProperty("_icon").objectReferenceValue = image;
                view.FindProperty("_label").objectReferenceValue = label;
                view.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private static void BindGameplayPrefab()
        {
            ConfigureUnitSlotFrameImporter();
            ConfigureSoldierTabFrameImporter();
            var root = PrefabUtility.LoadPrefabContents(GameplayPrefab);
            try
            {
                var panel = root.GetComponent<GameplayPanel>()
                    ?? throw new InvalidOperationException("Gameplay prefab has no GameplayPanel.");
                var cardTray = FindTransform(root.transform, "CardTray").GetComponent<RectTransform>();
                cardTray.anchorMin = new Vector2(0.28f, 0.005f);
                cardTray.anchorMax = new Vector2(0.80f, 0.14f);
                cardTray.offsetMin = Vector2.zero;
                cardTray.offsetMax = Vector2.zero;
                ApplyUnitSlotFrame(root);
                DestroyIfPresent(root.transform, "BuildingMenuButton");
                DestroyIfPresent(root.transform, "Blueprint");
                ConfigureWall(root.transform, "FriendlyWall", new Vector2(.1825f, 0f), new Vector2(.3075f, 1f));
                ConfigureWall(root.transform, "EnemyWall", new Vector2(.9125f, 0f), new Vector2(1.0375f, 1f));

                var world = FindTransform(root.transform, "World");
                var worldButton = world.GetComponent<Button>() ?? world.gameObject.AddComponent<Button>();
                worldButton.targetGraphic = world.GetComponent<Image>();
                worldButton.transition = Selectable.Transition.None;

                var slotButtons = new Button[9];
                var slotFrames = new Image[9];
                for (var index = 0; index < slotButtons.Length; index++)
                {
                    var slot = FindTransform(root.transform, "Slot" + index);
                    slotFrames[index] = slot.GetComponent<Image>();
                    slotButtons[index] = slot.GetComponent<Button>() ?? slot.gameObject.AddComponent<Button>();
                    slotButtons[index].targetGraphic = slotFrames[index];
                    slotButtons[index].transition = Selectable.Transition.None;
                }
                var itemRoot = FindTransform(root.transform, "ItemCards");
                var template = FindTransform(itemRoot, "Item0").gameObject;
                for (var index = 0; index < 6; index++)
                {
                    var child = itemRoot.Find("Item" + index);
                    if (child == null)
                    {
                        child = UnityEngine.Object.Instantiate(template, itemRoot).transform;
                        child.name = "Item" + index;
                    }
                    var rect = child.GetComponent<RectTransform>();
                    rect.anchorMin = new Vector2(index / 6f + 0.008f, 0.06f);
                    rect.anchorMax = new Vector2((index + 1) / 6f - 0.008f, 0.94f);
                    rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
                    var frame = child.GetComponent<Image>();
                    if (frame != null) frame.preserveAspect = true;
                    var label = child.GetComponentInChildren<Text>(true);
                    if (label != null) label.gameObject.SetActive(false);
                    EnsureCardArt(child);
                }

                var soldierButtons = new Button[4];
                var itemButtons = new Button[6];
                for (var index = 0; index < 4; index++) soldierButtons[index] = FindTransform(root.transform, "Soldier" + index).GetComponent<Button>();
                for (var index = 0; index < 6; index++) itemButtons[index] = FindTransform(root.transform, "Item" + index).GetComponent<Button>();
                var buttons = new Button[10];
                Array.Copy(soldierButtons, 0, buttons, 0, 4); Array.Copy(itemButtons, 0, buttons, 4, 6);
                var artImages = new Image[buttons.Length];
                for (var index = 0; index < buttons.Length; index++)
                {
                    var art = buttons[index].transform.Find("Art");
                    if (art != null) artImages[index] = art.GetComponent<Image>();
                }

                var serialized = new SerializedObject(panel);
                var placementPreview = EnsureBuildingPlacementPreview(root);
                SetArray(serialized.FindProperty("_cardButtons"), buttons);
                SetArray(serialized.FindProperty("_cardRects"), Array.ConvertAll(buttons, value => value.GetComponent<RectTransform>()));
                SetArray(serialized.FindProperty("_cardArtImages"), artImages);
                SetArray(serialized.FindProperty("_buildingSlotButtons"), slotButtons);
                SetArray(serialized.FindProperty("_buildingSlotFrames"), slotFrames);
                serialized.FindProperty("_worldCancelButton").objectReferenceValue = worldButton;
                serialized.FindProperty("_buildingPlacementPreview").objectReferenceValue = placementPreview;
                serialized.FindProperty("_clockText").objectReferenceValue = FindComponentByName<Text>(root.transform, "Clock");
                serialized.FindProperty("_playerWallText").objectReferenceValue = FindComponentByName<Text>(root.transform, "FriendlyHp");
                serialized.FindProperty("_enemyWallText").objectReferenceValue = FindComponentByName<Text>(root.transform, "EnemyHp");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SaveAsPrefabAsset(root, GameplayPrefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        private static void ConfigureUnitSlotFrameImporter()
        {
            var importer = AssetImporter.GetAtPath(UnitSlotFramePath) as TextureImporter
                ?? throw new InvalidOperationException($"Missing unit slot frame texture: {UnitSlotFramePath}");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 100f;
            importer.spriteBorder = Vector4.zero;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ConfigureSoldierTabFrameImporter()
        {
            var importer = AssetImporter.GetAtPath(SoldierTabFramePath) as TextureImporter
                ?? throw new InvalidOperationException($"Missing soldier tab frame texture: {SoldierTabFramePath}");
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.spritePixelsPerUnit = 200f;
            importer.spriteBorder = Vector4.zero;
            importer.maxTextureSize = 512;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        private static void ApplyUnitSlotFrame(GameObject root)
        {
            var frame = AssetDatabase.LoadAssetAtPath<Sprite>(UnitSlotFramePath)
                ?? throw new InvalidOperationException($"Unit slot frame did not import as a Sprite: {UnitSlotFramePath}");
            var tabFrame = AssetDatabase.LoadAssetAtPath<Sprite>(SoldierTabFramePath)
                ?? throw new InvalidOperationException($"Soldier tab frame did not import as a Sprite: {SoldierTabFramePath}");
            var tray = FindTransform(root.transform, "CardTray");
            var soldierTab = tray.Find("SoldierTab")
                ?? throw new InvalidOperationException("Gameplay/CardTray/SoldierTab is missing.");
            ConfigureFixedFrame(soldierTab.GetComponent<Image>(), tabFrame);

            var soldierCards = tray.Find("SoldierCards")
                ?? throw new InvalidOperationException("Gameplay/CardTray/SoldierCards is missing.");
            for (var index = 0; index < 4; index++)
            {
                var card = soldierCards.Find("Soldier" + index)
                    ?? throw new InvalidOperationException($"Gameplay/CardTray/SoldierCards/Soldier{index} is missing.");
                var cardRect = card.GetComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(index / 4f + 0.008f, 0f);
                cardRect.anchorMax = new Vector2((index + 1) / 4f - 0.008f, 1f);
                cardRect.offsetMin = Vector2.zero;
                cardRect.offsetMax = Vector2.zero;
                ConfigureFixedFrame(card.GetComponent<Image>(), frame);
                var art = card.Find("Art")?.GetComponent<Image>()
                    ?? throw new InvalidOperationException($"Soldier{index}/Art is missing.");
                art.sprite = null;
                art.preserveAspect = true;
                art.raycastTarget = false;
                ConfigureSoldierCardLayout(card, art);
                card.gameObject.SetActive(false);
            }
        }

        private static void ConfigureFixedFrame(Image image, Sprite frame)
        {
            if (image == null) throw new InvalidOperationException("Unit tab frame target has no Image component.");
            image.sprite = frame;
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
        }

        private static void ConfigureSoldierCardLayout(Transform card, Image art)
        {
            var artRect = art.rectTransform;
            artRect.anchorMin = new Vector2(0.06f, 0.34f);
            artRect.anchorMax = new Vector2(0.40f, 0.90f);
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            var label = card.Find("Label")?.GetComponent<Text>()
                ?? throw new InvalidOperationException($"{card.name}/Label is missing.");
            var labelRect = label.rectTransform;
            labelRect.anchorMin = new Vector2(0.43f, 0.34f);
            labelRect.anchorMax = new Vector2(0.95f, 0.90f);
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            label.alignment = TextAnchor.MiddleLeft;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = 10;
            label.resizeTextMaxSize = 17;

            var controls = card.Find("CountControls") as RectTransform
                ?? throw new InvalidOperationException($"{card.name}/CountControls is missing.");
            controls.anchorMin = new Vector2(0.08f, 0.03f);
            controls.anchorMax = new Vector2(0.92f, 0.29f);
            controls.offsetMin = Vector2.zero;
            controls.offsetMax = Vector2.zero;
        }

        private static void ConfigureWall(Transform root, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var wall = FindTransform(root, name);
            var rect = wall.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin; rect.anchorMax = anchorMax; rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = wall.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
        }

        private static BuildingPlacementPreview EnsureBuildingPlacementPreview(GameObject root)
        {
            var existing = FindOptional(root.transform, "BuildingPlacementPreview");
            var value = existing != null ? existing.gameObject :
                new GameObject("BuildingPlacementPreview", typeof(RectTransform), typeof(CanvasRenderer),
                    typeof(Image), typeof(BuildingPlacementPreview));
            value.transform.SetParent(root.transform, false);
            value.transform.SetAsLastSibling();
            var rect = value.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = Vector2.zero;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(112f, 112f);
            var image = value.GetComponent<Image>();
            image.preserveAspect = true;
            image.raycastTarget = false;
            image.color = new Color(0.46f, 0.86f, 1f, 0.38f);
            var preview = value.GetComponent<BuildingPlacementPreview>();
            var serialized = new SerializedObject(preview);
            serialized.FindProperty("_previewRoot").objectReferenceValue = rect;
            serialized.FindProperty("_image").objectReferenceValue = image;
            serialized.FindProperty("_canvas").objectReferenceValue = root.GetComponentInChildren<Canvas>(true);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            value.SetActive(false);
            return preview;
        }

        private static void DestroyIfPresent(Transform root, string name)
        {
            var value = FindOptional(root, name);
            if (value != null) UnityEngine.Object.DestroyImmediate(value.gameObject);
        }

        private static Image EnsureCardArt(Transform card)
        {
            var existing = card.Find("Art");
            if (existing != null) return existing.GetComponent<Image>();
            var art = new GameObject("Art", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            art.transform.SetParent(card, false);
            var rect = art.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(.08f, .08f); rect.anchorMax = new Vector2(.92f, .92f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var image = art.GetComponent<Image>(); image.preserveAspect = true; image.raycastTarget = false;
            return image;
        }

        private static Transform FindTransform(Transform root, string name)
        {
            foreach (var value in root.GetComponentsInChildren<Transform>(true)) if (value.name == name) return value;
            throw new InvalidOperationException($"Missing transform '{name}' in Gameplay prefab.");
        }

        private static Transform FindOptional(Transform root, string name)
        {
            foreach (var value in root.GetComponentsInChildren<Transform>(true)) if (value.name == name) return value;
            return null;
        }

        private static T FindComponentByName<T>(Transform root, string name) where T : Component =>
            FindTransform(root, name).GetComponentInChildren<T>(true) ?? throw new InvalidOperationException($"'{name}' has no {typeof(T).Name}.");

        private static Sprite Sprite(string name) => AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + name + ".png")
            ?? throw new InvalidOperationException($"Missing sprite '{name}'.");

        private static void SetArray<T>(SerializedProperty property, T[] values) where T : UnityEngine.Object
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).objectReferenceValue = values[index];
        }


        private static void SetStrings(SerializedProperty property, string[] values)
        {
            property.arraySize = values.Length;
            for (var index = 0; index < values.Length; index++) property.GetArrayElementAtIndex(index).stringValue = values[index];
        }

        private static void EnsureFolder(string parent, string child)
        { var path = parent + "/" + child; if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder(parent, child); }
    }
}
#endif
