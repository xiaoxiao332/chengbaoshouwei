#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using FortressFrontier.Runtime.Content;
using UnityEditor;
using UnityEngine;

namespace FortressFrontier.Editor
{
    /// <summary>P1 Prototype authoring table. This is the only source of P1 prototype defaults.</summary>
    internal static class P1ContentConfigAuthoring
    {
        private static readonly string[] IntentIds = ContentConstants.P1AiIntentIds;
        private static readonly string[] RewardContentCardIds =
        {
            "card.building.pasture", "card.building.winery", "card.building.sawmill", "card.building.stoneworks",
            "card.building.iron-smelter", "card.building.warehouse", "card.building.shield-camp",
            "card.building.archer-camp", "card.building.ram-camp", "card.building.heavy-warrior-camp",
            "card.building.mage-camp", "card.building.longbow-camp", "card.building.cannon-camp",
            "card.building.research-lab", "card.building.gatherer-lodge", "card.building.wood-gatherer-camp",
            "card.building.stone-gatherer-camp", "card.building.iron-gatherer-camp", "card.battlefield.arrow-tower"
        };

        [MenuItem("Fortress Frontier/Content/Apply Schema v14 Config Only")]
        private static void ApplySchemaV13ConfigOnly()
        {
            T Load<T>(string path) where T : UnityEngine.Object => AssetDatabase.LoadAssetAtPath<T>(path)
                ?? throw new InvalidOperationException($"Missing content asset: {path}");
            Apply(
                Load<ResourceDefinitionCatalog>("Assets/Game/Content/Config/Resources/ResourceCatalog.asset"),
                Load<CardCatalog>("Assets/Game/Content/Config/Cards/CardCatalog.asset"),
                Load<BuildingCatalog>("Assets/Game/Content/Config/Buildings/BuildingCatalog.asset"),
                Load<UnitCatalog>("Assets/Game/Content/Config/Units/UnitCatalog.asset"),
                Load<BattlefieldCatalog>("Assets/Game/Content/Config/Battlefields/BattlefieldCatalog.asset"),
                Load<BossCatalog>("Assets/Game/Content/Config/Bosses/BossCatalog.asset"),
                Load<RewardCatalog>("Assets/Game/Content/Config/Rewards/RewardCatalog.asset"),
                Load<StageEffectCatalog>("Assets/Game/Content/Config/Stages/StageEffectCatalog.asset"),
                Load<PresentationCatalog>("Assets/Game/Content/Config/Presentation/PresentationCatalog.asset"));
            var root = Load<GameContentConfig>(GameContentConfigAuthoring.RootAssetPath);
            var rootObject = new SerializedObject(root);
            rootObject.FindProperty("_schemaVersion").intValue = ContentConstants.ExpectedSchemaVersion;
            rootObject.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(root);
            AssetDatabase.SaveAssets();
            Debug.Log("Schema v14 content config applied without rebuilding scenes or prefabs.");
        }

        public static void Apply(ResourceDefinitionCatalog resources, CardCatalog cards, BuildingCatalog buildings,
            UnitCatalog units, BattlefieldCatalog battlefields, BossCatalog bosses, RewardCatalog rewards,
            StageEffectCatalog stages, PresentationCatalog presentation)
        {
            ConfigureUnits(units);
            ConfigureBuildings(buildings);
            ConfigureCards(cards);
            ConfigureRewards(rewards);
            ConfigureBosses(bosses);
            ConfigureBattlefields(battlefields);
            ConfigureStages(stages);
            ConfigurePresentation(presentation);
        }

        private static void ConfigureUnits(UnitCatalog catalog)
        {
            var values = new[]
            {
                new UnitRow("unit.gatherer", "Gatherer", 120, 0, 3, 12, 0, 0, 0, 1, 0, 1000, false, 0f, "Worker"),
                new UnitRow("unit.lumberjack", "Lumberjack", 120, 0, 3, 12, 0, 0, 0, 1, 0, 1000, false, 0f, "Worker"),
                new UnitRow("unit.stonecutter", "Stonecutter", 140, 0, 3, 12, 0, 0, 0, 1, 0, 1000, false, 0f, "Worker"),
                new UnitRow("unit.iron-miner", "Iron Miner", 140, 0, 3, 12, 0, 0, 0, 1, 0, 1000, false, 0f, "Worker"),
                new UnitRow("unit.builder", "Builder", 140, 0, 4, 12, 0, 0, 0, 1, 0, 1000, false, 0f, "Builder"),
                new UnitRow("unit.shield-guard", "Shield Guard", 360, 24, 4, 18, 240, 360, 28, 10, 0, 1000, true, 8f, "Tank"),
                new UnitRow("unit.archer", "Archer", 160, 28, 4, 14, 260, 420, 180, 12, 16, 1000, true, 9f, "Ranged"),
                new UnitRow("unit.siege-ram", "Siege Ram", 480, 16, 3, 24, 180, 300, 28, 16, 0, 4000, true, 12f, "Siege"),
                new UnitRow("unit.heavy-warrior", "Heavy Warrior", 520, 36, 2, 20, 240, 360, 28, 14, 0, 1000, true, 13f, "Tank"),
                new UnitRow("unit.mage", "Mage", 140, 40, 4, 14, 300, 480, 170, 18, 12, 1000, true, 14f, "Magic"),
                new UnitRow("unit.longbow", "Longbow", 115, 38, 4, 14, 380, 520, 280, 18, 20, 1000, true, 12f, "Ranged"),
                new UnitRow("unit.cannon", "Cannon", 340, 54, 2, 24, 340, 440, 240, 25, 8, 1500, true, 18f, "Siege")
            };
            ConfigureList(catalog, "_definitions", values.Length, (item, index) =>
            {
                var row = values[index];
                String(item, "_id", row.Id); String(item, "_displayName", row.Name);
                String(item, "_presentationKey", "presentation." + row.Id);
                String(item, "_playerWorldPrefabPresentationKey", row.CanAttack
                    ? "presentation.world." + row.Id + ".player"
                    : string.Empty);
                String(item, "_enemyWorldPrefabPresentationKey", row.CanAttack
                    ? "presentation.world." + row.Id + ".enemy"
                    : string.Empty);
                Float(item, "_baseTrainingSeconds", row.TrainingSeconds);
                Strings(item.FindPropertyRelative("_roleTags"), new[] { row.Role });
                ResourceAmounts(item.FindPropertyRelative("_trainingCosts"), row.Id switch
                {
                    "unit.shield-guard" => new[] { ("resource.food", 20) },
                    "unit.archer" => new[] { ("resource.food", 12), ("resource.wine", 5) },
                    "unit.siege-ram" => new[] { ("resource.food", 35) },
                    "unit.heavy-warrior" => new[] { ("resource.food", 20), ("resource.meat", 10), ("resource.iron-ingot", 4) },
                    "unit.mage" => new[] { ("resource.food", 10), ("resource.wine", 10), ("resource.iron-ingot", 6) },
                    "unit.longbow" => new[] { ("resource.food", 10), ("resource.wine", 8) },
                    "unit.cannon" => new[] { ("resource.meat", 12), ("resource.iron-ingot", 18), ("resource.stone", 10) },
                    _ => Array.Empty<(string, int)>()
                });
                Int(item, "_maxHealth", row.Health); Int(item, "_attackDamage", row.Damage);
                Int(item, "_wallDamageMultiplierMilli", row.WallMultiplier); Int(item, "_movePerTick", row.Move);
                Int(item, "_collisionRadius", row.Collision); Int(item, "_acquireRadius", row.Acquire);
                Int(item, "_chaseRadius", row.Chase); Int(item, "_attackRange", row.Range);
                Int(item, "_attackIntervalTicks", row.Interval); Int(item, "_projectileSpeedPerTick", row.Projectile);
                Enum(item, "_targetPriority", row.Id == "unit.siege-ram" ? (int)UnitTargetPriority.StructuresOnly : (int)UnitTargetPriority.ThreatThenDistance);
                Bool(item, "_canAttack", row.CanAttack);
                var category = row.Id switch
                {
                    "unit.archer" or "unit.longbow" => ResearchCategory.Ranged,
                    "unit.mage" => ResearchCategory.Magic,
                    "unit.siege-ram" or "unit.cannon" => ResearchCategory.Siege,
                    _ => ResearchCategory.Melee
                };
                var projectileKind = row.Id switch
                {
                    "unit.archer" or "unit.longbow" => UnitProjectileKind.Arrow,
                    "unit.mage" => UnitProjectileKind.Fireball,
                    "unit.cannon" => UnitProjectileKind.Cannonball,
                    _ => UnitProjectileKind.None
                };
                Enum(item, "_researchCategory", (int)category);
                Enum(item, "_projectileKind", (int)projectileKind);
                Int(item, "_explosionRadius", row.Id == "unit.mage" ? 60 : row.Id == "unit.cannon" ? 80 : 0);
                Int(item, "_explosionSecondaryDamageMilli", row.Id == "unit.mage" ? 600 : row.Id == "unit.cannon" ? 650 : 0);
                String(item, "_projectilePresentationKey", projectileKind switch
                {
                    UnitProjectileKind.Arrow => "presentation.world.projectile.arrow",
                    UnitProjectileKind.Fireball => "presentation.world.projectile.fireball",
                    UnitProjectileKind.Cannonball => "presentation.world.projectile.cannonball",
                    _ => string.Empty
                });
            });
        }

        private static void ConfigureBuildings(BuildingCatalog catalog)
        {
            var ids = new[]
            {
                "building.pasture", "building.winery", "building.sawmill", "building.stoneworks", "building.iron-smelter",
                "building.warehouse", "building.shield-camp", "building.archer-camp", "building.ram-camp",
                "building.heavy-warrior-camp", "building.mage-camp", "building.longbow-camp", "building.cannon-camp",
                "building.research-lab", "building.arrow-tower", "building.gatherer-lodge",
                "building.wood-gatherer-camp", "building.stone-gatherer-camp", "building.iron-gatherer-camp"
            };
            ConfigureList(catalog, "_definitions", ids.Length, (item, index) =>
            {
                var id = ids[index];
                var category = index < 5 ? BuildingCategory.Processing : index == 5 ? BuildingCategory.Storage :
                    index < 13 ? BuildingCategory.SoldierCamp : index == 13 ? BuildingCategory.Research :
                    index == 14 ? BuildingCategory.BattlefieldStructure : BuildingCategory.Gathering;
                String(item, "_id", id); String(item, "_sourceCardId", id == "building.arrow-tower" ? "card.battlefield.arrow-tower" : "card." + id);
                Enum(item, "_category", (int)category);
                String(item, "_workerUnitId", id switch
                {
                    "building.arrow-tower" => "unit.builder",
                    "building.gatherer-lodge" => "unit.gatherer",
                    "building.wood-gatherer-camp" => "unit.lumberjack",
                    "building.stone-gatherer-camp" => "unit.stonecutter",
                    "building.iron-gatherer-camp" => "unit.iron-miner",
                    _ => string.Empty
                });
                String(item, "_activatedSoldierCardId", id switch
                {
                    "building.shield-camp" => "card.soldier.shield-guard",
                    "building.archer-camp" => "card.soldier.archer",
                    "building.ram-camp" => "card.soldier.siege-ram",
                    "building.heavy-warrior-camp" => "card.soldier.heavy-warrior",
                    "building.mage-camp" => "card.soldier.mage",
                    "building.longbow-camp" => "card.soldier.longbow",
                    "building.cannon-camp" => "card.soldier.cannon",
                    _ => string.Empty
                });
                Int(item, "_productionCycleTicks", index < 5 ? 50 : 80);
                Int(item, "_workerGatherTicks", category == BuildingCategory.Gathering ? 80 : 0);
                String(item, "_presentationKey", "presentation." + id);
                var inputs = index switch { 0 => new[] { ("resource.food", 2) }, 1 => new[] { ("resource.food", 2) }, 2 => new[] { ("resource.wood", 2) }, 3 => new[] { ("resource.raw-stone", 2) }, 4 => new[] { ("resource.iron-ore", 2) }, _ => Array.Empty<(string, int)>() };
                var outputs = index switch { 0 => new[] { ("resource.meat", 1) }, 1 => new[] { ("resource.wine", 1) }, 2 => new[] { ("resource.plank", 2) }, 3 => new[] { ("resource.stone", 2) }, 4 => new[] { ("resource.iron-ingot", 1) }, _ => Array.Empty<(string, int)>() };
                ResourceAmounts(item.FindPropertyRelative("_inputs"), inputs);
                ResourceAmounts(item.FindPropertyRelative("_outputs"), outputs);
                ResourceAmounts(item.FindPropertyRelative("_inputReserveFloors"), id switch
                {
                    "building.pasture" or "building.winery" => new[] { ("resource.food", 20) },
                    "building.sawmill" => new[] { ("resource.wood", 10) },
                    "building.stoneworks" => new[] { ("resource.raw-stone", 6) },
                    "building.iron-smelter" => new[] { ("resource.iron-ore", 5) },
                    _ => Array.Empty<(string, int)>()
                });
                Upgrades(item.FindPropertyRelative("_upgradeLevels"), category != BuildingCategory.BattlefieldStructure);
                Int(item, "_maxHealth", id == "building.arrow-tower" ? 650 : 0); Int(item, "_attackDamage", id == "building.arrow-tower" ? 30 : 0);
                Int(item, "_attackIntervalTicks", id == "building.arrow-tower" ? 10 : 1); Int(item, "_attackRange", id == "building.arrow-tower" ? 220 : 0);
                Int(item, "_projectileSpeedPerTick", id == "building.arrow-tower" ? 18 : 0); Int(item, "_constructionTicks", id == "building.arrow-tower" ? 120 : 0);
                ResourceAmounts(item.FindPropertyRelative("_constructionCosts"), id == "building.arrow-tower" ? new[] { ("resource.stone", 60) } : Array.Empty<(string, int)>());
                String(item, "_researchBagId", id == "building.research-lab" ? "research-bag.p1" : string.Empty);

                var allowed = id switch
                {
                    "building.gatherer-lodge" => new[] { "resource.food" },
                    "building.wood-gatherer-camp" => new[] { "resource.wood" },
                    "building.stone-gatherer-camp" => new[] { "resource.raw-stone" },
                    "building.iron-gatherer-camp" => new[] { "resource.iron-ore" },
                    _ => Array.Empty<string>()
                };
                Strings(item.FindPropertyRelative("_gathererAllowedResourceIds"), allowed);
                var dispatchCosts = id switch
                {
                    "building.gatherer-lodge" => new[] { ("resource.food", 1) },
                    "building.wood-gatherer-camp" => new[] { ("resource.food", 2) },
                    "building.stone-gatherer-camp" => new[] { ("resource.food", 2), ("resource.wood", 1) },
                    "building.iron-gatherer-camp" => new[] { ("resource.food", 3), ("resource.wood", 1) },
                    _ => Array.Empty<(string, int)>()
                };
                ResourceAmounts(item.FindPropertyRelative("_gathererDispatchCosts"), dispatchCosts);
                Int(item, "_gathererDispatchIntervalTicks", id switch
                {
                    "building.gatherer-lodge" => 180, "building.wood-gatherer-camp" => 200,
                    "building.stone-gatherer-camp" => 220, "building.iron-gatherer-camp" => 240, _ => 250
                });
                Int(item, "_gathererCarryAmount", id switch
                {
                    "building.gatherer-lodge" => 8, "building.wood-gatherer-camp" => 7,
                    "building.stone-gatherer-camp" => 6, "building.iron-gatherer-camp" => 5, _ => 3
                });
                Enum(item, "_gathererResourceSelectionPolicy", (int)GathererResourceSelectionPolicy.Fixed);
            });

            var so = Begin(catalog);
            var upgrades = so.FindProperty("_researchUpgrades"); upgrades.arraySize = 8;
            var categories = new[] { ResearchCategory.Melee, ResearchCategory.Melee, ResearchCategory.Ranged, ResearchCategory.Ranged, ResearchCategory.Magic, ResearchCategory.Magic, ResearchCategory.Siege, ResearchCategory.Siege };
            var properties = new[] { "damage", "health", "damage", "range", "damage", "health", "damage", "health" };
            var percents = new[] { 800, 1000, 800, 700, 1000, 800, 1000, 1000 };
            for (var i = 0; i < 8; i++)
            {
                var value = upgrades.GetArrayElementAtIndex(i);
                String(value, "_id", "research." + categories[i].ToString().ToLowerInvariant() + "." + properties[i]);
                Enum(value, "_targetRole", (int)categories[i]);
                var modifiers = value.FindPropertyRelative("_modifiers"); modifiers.arraySize = 1;
                String(modifiers.GetArrayElementAtIndex(0), "_propertyKey", properties[i]);
                Int(modifiers.GetArrayElementAtIndex(0), "_percentPerRankBasisPoints", percents[i]);
                Int(value, "_maxRank", 3);
                String(value, "_presentationKey", "presentation.research." + categories[i].ToString().ToLowerInvariant());
            }
            var bags = so.FindProperty("_researchBags"); bags.arraySize = 1;
            var bag = bags.GetArrayElementAtIndex(0); String(bag, "_id", "research-bag.p1");
            var upgradeIds = new string[8]; for (var i = 0; i < 8; i++) upgradeIds[i] = upgrades.GetArrayElementAtIndex(i).FindPropertyRelative("_id").stringValue;
            Strings(bag.FindPropertyRelative("_upgradeIds"), upgradeIds);
            ResourceAmounts(bag.FindPropertyRelative("_costs"), new[] { ("resource.wine", 12), ("resource.iron-ingot", 8) });
            Int(bag, "_researchTicks", 250); Int(bag, "_candidateCount", 3);
            End(so);
        }

        private static void ConfigureCards(CardCatalog catalog)
        {
            var reinforcementIds = new[]
            {
                "shield-pair", "archer-pair", "shield-archer", "ram-shield", "heavy-archers",
                "mage-shields", "longbows-shield", "elite-trio", "cannon-shields", "ram-archers"
            };
            var ids = new[]
            {
                "card.building.pasture", "card.building.winery", "card.building.sawmill", "card.building.stoneworks", "card.building.iron-smelter", "card.building.warehouse",
                "card.building.shield-camp", "card.building.archer-camp", "card.building.ram-camp", "card.building.heavy-warrior-camp", "card.building.mage-camp", "card.building.longbow-camp", "card.building.cannon-camp",
                "card.building.research-lab", "card.building.gatherer-lodge", "card.building.wood-gatherer-camp", "card.building.stone-gatherer-camp", "card.building.iron-gatherer-camp",
                "card.battlefield.arrow-tower", "card.tactic.field-rations", "card.tactic.emergency-supplies", "card.tactic.arrow-rain",
                "card.soldier.shield-guard", "card.soldier.archer", "card.soldier.siege-ram", "card.soldier.heavy-warrior", "card.soldier.mage", "card.soldier.longbow", "card.soldier.cannon"
            }.Concat(reinforcementIds.Select(value => "card.reinforcement." + value)).ToArray();
            var linked = new[]
            {
                "building.pasture", "building.winery", "building.sawmill", "building.stoneworks", "building.iron-smelter", "building.warehouse",
                "building.shield-camp", "building.archer-camp", "building.ram-camp", "building.heavy-warrior-camp", "building.mage-camp", "building.longbow-camp", "building.cannon-camp",
                "building.research-lab", "building.gatherer-lodge", "building.wood-gatherer-camp", "building.stone-gatherer-camp", "building.iron-gatherer-camp",
                "building.arrow-tower", "effect.field-rations", "effect.emergency-supplies", "effect.arrow-rain",
                "unit.shield-guard", "unit.archer", "unit.siege-ram", "unit.heavy-warrior", "unit.mage", "unit.longbow", "unit.cannon"
            }.Concat(reinforcementIds.Select(value => "reinforcement." + value)).ToArray();
            ConfigureList(catalog, "_definitions", ids.Length, (item, index) =>
            {
                var type = ids[index].StartsWith("card.building.", StringComparison.Ordinal) ? CardType.BuildingItem :
                    ids[index].StartsWith("card.battlefield.", StringComparison.Ordinal) ? CardType.BattlefieldItem :
                    ids[index].StartsWith("card.tactic.", StringComparison.Ordinal) ? CardType.Tactic :
                    ids[index].StartsWith("card.reinforcement.", StringComparison.Ordinal) ? CardType.ReinforcementItem : CardType.Soldier;
                String(item, "_id", ids[index]); Enum(item, "_type", (int)type); String(item, "_linkedContentId", linked[index]);
                String(item, "_activationCampBuildingId", linked[index] switch
                {
                    "unit.shield-guard" => "building.shield-camp", "unit.archer" => "building.archer-camp",
                    "unit.siege-ram" => "building.ram-camp", "unit.heavy-warrior" => "building.heavy-warrior-camp",
                    "unit.mage" => "building.mage-camp", "unit.longbow" => "building.longbow-camp",
                    "unit.cannon" => "building.cannon-camp", _ => string.Empty
                });
                var rewardOnly = type == CardType.ReinforcementItem;
                Bool(item, "_defaultUnlocked", !rewardOnly); Int(item, "_unlockGoldCost", 0);
                Strings(item.FindPropertyRelative("_prerequisiteCardIds"), Array.Empty<string>()); Int(item, "_maxMetaLevel", rewardOnly ? 1 : 10);
                Ints(item.FindPropertyRelative("_upgradeGoldCosts"), rewardOnly ? Array.Empty<int>() : new[] { 40, 60, 80, 110, 150, 200, 260, 330, 410 });
                var growth = item.FindPropertyRelative("_growthRules"); growth.arraySize = 1;
                if (rewardOnly) growth.arraySize = 0;
                else
                {
                    String(growth.GetArrayElementAtIndex(0), "_propertyKey", type == CardType.Soldier ? "health" : type == CardType.Tactic ? "effect" : "yield");
                    Int(growth.GetArrayElementAtIndex(0), "_percentPerLevelBasisPoints", 400);
                }
                Strings(item.FindPropertyRelative("_offerTags"), rewardOnly ? Array.Empty<string>() : new[] { type.ToString().ToLowerInvariant() });
                String(item, "_presentationKey", "presentation." + ids[index]);
            });
            var so = Begin(catalog); var effects = so.FindProperty("_tacticEffects"); effects.arraySize = 3;
            ConfigureEffect(effects.GetArrayElementAtIndex(0), "effect.field-rations", TacticEffectKind.AddResource, TacticTargetKind.None, 0, 0, new[] { ("resource.meat", 20) });
            ConfigureEffect(effects.GetArrayElementAtIndex(1), "effect.emergency-supplies", TacticEffectKind.AddResource, TacticTargetKind.None, 0, 0, new[] { ("resource.plank", 15), ("resource.stone", 10) });
            ConfigureEffect(effects.GetArrayElementAtIndex(2), "effect.arrow-rain", TacticEffectKind.AreaDamage, TacticTargetKind.BattlefieldArea, 120, 140, Array.Empty<(string, int)>());
            End(so);
        }

        private static void ConfigureRewards(RewardCatalog catalog)
        {
            ConfigureList(catalog, "_definitions", 2, (item, index) =>
            {
                var frontier = index == 1;
                String(item, "_id", frontier ? "reward.river-pass" : "reward.prologue");
                Int(item, "_completionGold", frontier ? 55 : 40); Int(item, "_victoryGold", frontier ? 80 : 60); Int(item, "_firstClearGold", frontier ? 140 : 100);
                var offers = item.FindPropertyRelative("_timedCardOffers"); offers.arraySize = ContentConstants.P1OfferSeconds.Length;
                for (var i = 0; i < offers.arraySize; i++)
                {
                    var offer = offers.GetArrayElementAtIndex(i); Int(offer, "_triggerSeconds", ContentConstants.P1OfferSeconds[i]); Int(offer, "_candidateCount", 4);
                    Strings(offer.FindPropertyRelative("_fallbackCardIds"), RewardContentCardIds);
                }
                Int(item, "_handLimit", 6); Bool(item, "_allowFullHandDiscard", true);
                ResourceAmount(item.FindPropertyRelative("_fullHandExchange"), string.Empty, 0);
                var rarityWeights = item.FindPropertyRelative("_rarityWeights"); rarityWeights.arraySize = 3;
                for (var rarity = 0; rarity < 3; rarity++)
                {
                    var rule = rarityWeights.GetArrayElementAtIndex(rarity); Enum(rule, "_rarity", rarity);
                    var weights = rule.FindPropertyRelative("_heatTierWeights"); weights.arraySize = ContentConstants.RewardRarityWeights.Length;
                    for (var heat = 0; heat < weights.arraySize; heat++)
                        weights.GetArrayElementAtIndex(heat).intValue = ContentConstants.RewardRarityWeights[heat][rarity];
                }
                String(item, "_buildingRewardPresentationKey", "presentation.reward.building");
                String(item, "_resourceRewardPresentationKey", "presentation.reward.resource");
                String(item, "_reinforcementRewardPresentationKey", "presentation.reward.reinforcement");
                var bundles = item.FindPropertyRelative("_processedResourceBundles"); bundles.arraySize = 9;
                var suffixes = new[] { string.Empty, ".rare", ".epic" };
                var sixAmounts = new[] { 6, 8, 9 }; var twelveAmounts = new[] { 12, 15, 18 };
                for (var rarity = 0; rarity < 3; rarity++)
                {
                    var offset = rarity * 3;
                    RewardBundle(bundles.GetArrayElementAtIndex(offset), "reward-bundle.meat-wine" + suffixes[rarity], "肉与酒",
                        (RewardRarity)rarity, new[] { ("resource.meat", sixAmounts[rarity]), ("resource.wine", sixAmounts[rarity]) });
                    RewardBundle(bundles.GetArrayElementAtIndex(offset + 1), "reward-bundle.plank-stone" + suffixes[rarity], "木板与石料",
                        (RewardRarity)rarity, new[] { ("resource.plank", twelveAmounts[rarity]), ("resource.stone", twelveAmounts[rarity]) });
                    RewardBundle(bundles.GetArrayElementAtIndex(offset + 2), "reward-bundle.wine-ingot" + suffixes[rarity], "酒与铁锭",
                        (RewardRarity)rarity, new[] { ("resource.wine", sixAmounts[rarity]), ("resource.iron-ingot", sixAmounts[rarity]) });
                }
                var reinforcements = item.FindPropertyRelative("_reinforcementTemplates");
                reinforcements.arraySize = 10;
                Reinforcement(reinforcements.GetArrayElementAtIndex(0), "reinforcement.shield-pair", "盾兵援军 ×2", 0, new[] { ("unit.shield-guard", 2) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(1), "reinforcement.archer-pair", "弓箭手援军 ×2", 0, new[] { ("unit.archer", 2) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(2), "reinforcement.shield-archer", "盾兵 ×2 + 弓箭手", 1, new[] { ("unit.shield-guard", 2), ("unit.archer", 1) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(3), "reinforcement.ram-shield", "破城槌 + 盾兵", 1, new[] { ("unit.siege-ram", 1), ("unit.shield-guard", 1) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(4), "reinforcement.heavy-archers", "重装 + 弓箭手 ×2", 2, new[] { ("unit.heavy-warrior", 1), ("unit.archer", 2) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(5), "reinforcement.mage-shields", "法师 + 盾兵 ×2", 2, new[] { ("unit.mage", 1), ("unit.shield-guard", 2) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(6), "reinforcement.longbows-shield", "长弓 ×2 + 盾兵", 2, new[] { ("unit.longbow", 2), ("unit.shield-guard", 1) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(7), "reinforcement.elite-trio", "重装 + 法师 + 长弓", 3, new[] { ("unit.heavy-warrior", 1), ("unit.mage", 1), ("unit.longbow", 1) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(8), "reinforcement.cannon-shields", "炮车 + 盾兵 ×2", 3, new[] { ("unit.cannon", 1), ("unit.shield-guard", 2) });
                Reinforcement(reinforcements.GetArrayElementAtIndex(9), "reinforcement.ram-archers", "破城槌 + 弓箭手 ×2", 3, new[] { ("unit.siege-ram", 1), ("unit.archer", 2) });
                BossRewards(item.FindPropertyRelative("_playerBossRewards"), false); BossRewards(item.FindPropertyRelative("_enemyBossRewards"), true);
                Int(item, "_bossRewardBudgetMilli", 700);
            });
        }

        private static void ConfigureBosses(BossCatalog catalog) => ConfigureList(catalog, "_definitions", 1, (item, _) =>
        {
            String(item, "_id", "boss.stone-golem"); String(item, "_rewardTableId", "reward.prologue"); String(item, "_presentationKey", "presentation.boss.stone-golem");
            Int(item, "_maxHealth", 3200); Int(item, "_armor", 20); Int(item, "_attackDamage", 45); Int(item, "_attackIntervalTicks", 14);
            Int(item, "_movePerTick", 3); Int(item, "_collisionRadius", 32); Int(item, "_acquireRadius", 220); Int(item, "_leashRadius", 480);
            Int(item, "_returnArmorPerTick", 4); Int(item, "_rewardCoreLifetimeTicks", 250);
        });

        private static void ConfigureBattlefields(BattlefieldCatalog catalog) => ConfigureList(catalog, "_definitions", 2, (item, index) =>
        {
            var frontier = index == 1;
            var prefix = frontier ? "river-pass" : "prologue";
            String(item, "_id", frontier ? "battlefield.river-pass" : "battlefield.prologue");
            String(item, "_displayName", frontier ? "河谷关隘" : "边境序章");
            String(item, "_sceneKey", "scene.gameplay");
            String(item, "_mapPresentationKey", frontier ? "presentation.map.river-pass" : "presentation.map.prologue");
            String(item, "_campaignStageId", frontier ? "stage.river-pass" : "stage.prologue");
            Strings(item.FindPropertyRelative("_mapModeIds"), new[]
            {
                $"mode.{prefix}.peaceful", $"mode.{prefix}.offensive", $"mode.{prefix}.nightmare"
            });
            String(item, "_bossId", "boss.stone-golem");
            String(item, "_rewardTableId", frontier ? "reward.river-pass" : "reward.prologue");
            var hand = item.FindPropertyRelative("_initialHand"); Int(hand, "_handSize", 6);
            Strings(hand.FindPropertyRelative("_guaranteedCardIds"), new[] { "card.building.gatherer-lodge", "card.building.wood-gatherer-camp", "card.building.winery", "card.building.sawmill", "card.building.shield-camp", "card.building.archer-camp" });
            Strings(hand.FindPropertyRelative("_fillerCardIds"), Array.Empty<string>());
            ResourceAmounts(item.FindPropertyRelative("_initialPlayerInventory"), Array.Empty<(string, int)>());
            Int(item, "_deploymentOrderTimeoutTicks", 300);
            Int(item, "_referenceWidth", 1920); Int(item, "_referenceHeight", 1080);
            Wall(item.FindPropertyRelative("_playerWall"), "wall.player", 470, 540); Wall(item.FindPropertyRelative("_enemyWall"), "wall.enemy", 1872, 540);
            var zones = item.FindPropertyRelative("_zones"); zones.arraySize = 8;
            Zone(zones.GetArrayElementAtIndex(0), "zone.player-deployment", ZoneKind.PlayerDeployment, 548, 80, 272, 920);
            Zone(zones.GetArrayElementAtIndex(1), "zone.enemy-deployment", ZoneKind.EnemyDeployment, 1536, 80, 258, 920);
            Zone(zones.GetArrayElementAtIndex(2), "zone.tower-buildable", ZoneKind.TowerBuildable, 518, 80, 1306, 920);
            Zone(zones.GetArrayElementAtIndex(3), "zone.enemy-wall-forbidden", ZoneKind.TowerForbidden, 1766, 0, 154, 1080);
            Zone(zones.GetArrayElementAtIndex(4), "zone.boss-a", ZoneKind.BossForbidden, frontier ? 970 : 850, frontier ? 220 : 260, 220, 220);
            Zone(zones.GetArrayElementAtIndex(5), "zone.boss-b", ZoneKind.BossForbidden, frontier ? 970 : 850, frontier ? 640 : 600, 220, 220);
            Zone(zones.GetArrayElementAtIndex(6), "zone.player-gate", ZoneKind.MainGate, 458, 500, 24, 80);
            Zone(zones.GetArrayElementAtIndex(7), "zone.enemy-gate", ZoneKind.MainGate, 1860, 500, 24, 80);
            var routes = item.FindPropertyRelative("_routes"); routes.arraySize = 3;
            Route(routes.GetArrayElementAtIndex(0), "route.upper", frontier ? 220 : 270); Route(routes.GetArrayElementAtIndex(1), "route.middle", 540); Route(routes.GetArrayElementAtIndex(2), "route.lower", frontier ? 860 : 810);
            var nodes = item.FindPropertyRelative("_resourceNodes"); nodes.arraySize = 12;
            var laneBaseY = frontier ? 220 : 270;
            var laneStepY = frontier ? 320 : 270;
            for (var lane = 0; lane < 3; lane++)
                Node(nodes.GetArrayElementAtIndex(lane), "resource-node.player-" + lane,
                    ResourceNodeSpawnGroup.PlayerSafe, "resource-node.enemy-" + lane,
                    frontier ? 720 : 680, laneBaseY + lane * laneStepY);
            for (var slot = 0; slot < 6; slot++)
                Node(nodes.GetArrayElementAtIndex(3 + slot), "resource-node.central-" + slot,
                    ResourceNodeSpawnGroup.Central, string.Empty, slot % 2 == 0 ? 1040 : 1300,
                    laneBaseY + (slot / 2) * laneStepY);
            for (var lane = 0; lane < 3; lane++)
                Node(nodes.GetArrayElementAtIndex(9 + lane), "resource-node.enemy-" + lane,
                    ResourceNodeSpawnGroup.EnemySafe, "resource-node.player-" + lane,
                    frontier ? 1622 : 1662, laneBaseY + lane * laneStepY);
            var gatherers = item.FindPropertyRelative("_gatherers"); gatherers.arraySize = 3;
            Gatherer(gatherers.GetArrayElementAtIndex(0), "gatherer-source.gate.upper", "route.upper",
                "unit.gatherer", new[] { "resource.food", "resource.wood", "resource.raw-stone" }, 3);
            Gatherer(gatherers.GetArrayElementAtIndex(1), "gatherer-source.gate.middle", "route.middle",
                "unit.gatherer", new[] { "resource.food", "resource.wood", "resource.raw-stone" }, 3);
            Gatherer(gatherers.GetArrayElementAtIndex(2), "gatherer-source.gate.lower", "route.lower",
                "unit.gatherer", new[] { "resource.food", "resource.wood", "resource.raw-stone" }, 3);
            Int(item, "_gathererDispatchIntervalMinTicks", 150);
            Int(item, "_gathererDispatchIntervalMaxTicks", 200);
            var spawns = item.FindPropertyRelative("_bossSpawns"); spawns.arraySize = 2;
            BossSpawn(spawns.GetArrayElementAtIndex(0), "boss-spawn.1", frontier ? 1080 : 960, frontier ? 330 : 370, 2580, 2700);
            BossSpawn(spawns.GetArrayElementAtIndex(1), "boss-spawn.2", frontier ? 1080 : 960, frontier ? 750 : 710, 6180, 6300);
            Int(item, "_minimumRoadWidth", 54); Int(item, "_maxConstructionSites", 2); Int(item, "_maxCompletedTowers", 3);
            Int(item, "_maxActiveBuilders", 1); Int(item, "_builderRespawnTicks", 80); Int(item, "_retainedConstructionProgressMilli", 500);
        });

private static void ConfigureStages(StageEffectCatalog catalog)
        {
            var so = Begin(catalog);
            var heatTiers = so.FindProperty("_heatTiers");
            heatTiers.arraySize = ContentConstants.HeatTierStartTicks.Length;
            for (var i = 0; i < heatTiers.arraySize; i++)
            {
                var tier = heatTiers.GetArrayElementAtIndex(i);
                Int(tier, "_startTick", ContentConstants.HeatTierStartTicks[i]);
                Int(tier, "_rewardCooldownSeconds", ContentConstants.OfferCooldownSeconds[i]);
                Int(tier, "_aiPressureIntervalMultiplierMilli", ContentConstants.AiPressureIntervalMultipliersMilli[i]);
                Int(tier, "_advancedUnitWeightMultiplierMilli", ContentConstants.AdvancedUnitWeightMultipliersMilli[i]);
            }
            var allCards = new[]
            {
                "card.building.pasture", "card.building.winery", "card.building.sawmill",
                "card.building.stoneworks", "card.building.iron-smelter", "card.building.warehouse",
                "card.building.shield-camp", "card.building.archer-camp", "card.building.ram-camp",
                "card.building.heavy-warrior-camp", "card.building.mage-camp", "card.building.longbow-camp", "card.building.cannon-camp",
                "card.building.research-lab", "card.building.gatherer-lodge", "card.building.wood-gatherer-camp",
                "card.building.stone-gatherer-camp", "card.building.iron-gatherer-camp",
                "card.battlefield.arrow-tower", "card.tactic.field-rations",
                "card.tactic.emergency-supplies", "card.tactic.arrow-rain",
                "card.soldier.shield-guard", "card.soldier.archer", "card.soldier.siege-ram",
                "card.soldier.heavy-warrior", "card.soldier.mage", "card.soldier.longbow", "card.soldier.cannon"
            };
            var campaignStages = so.FindProperty("_campaignStages");
            campaignStages.arraySize = 2;
            ConfigureCampaignStage(campaignStages.GetArrayElementAtIndex(0), "stage.prologue",
                string.Empty, "battlefield.prologue", allCards);
            ConfigureCampaignStage(campaignStages.GetArrayElementAtIndex(1), "stage.river-pass",
                "stage.prologue", "battlefield.river-pass", allCards);

            var mapModes = so.FindProperty("_mapModes");
            mapModes.arraySize = 6;
            for (var battlefield = 0; battlefield < 2; battlefield++)
                for (var mode = 0; mode < 3; mode++)
                    ConfigureMapMode(mapModes.GetArrayElementAtIndex(battlefield * 3 + mode),
                        battlefield == 0 ? "prologue" : "river-pass", mode);

            var profiles = so.FindProperty("_aiPhaseProfiles");
            profiles.arraySize = 1;
            var profile = profiles.GetArrayElementAtIndex(0);
            String(profile, "_id", "ai-phase.standard");
            Ints(profile.FindPropertyRelative("_phaseStartTicks"), new[] { 0, 3000, 6000 });
            Int(profile, "_firstProbeStartTick", 600);
            Int(profile, "_firstProbeEndTick", 800);
            Int(profile, "_publicAccelerationStartTick", 9000);
            Int(profile, "_publicProductionMultiplierMilli", 2000);

            var phaseList = profile.FindPropertyRelative("_phases");
            phaseList.arraySize = 3;
            var phaseIds = new[] { "phase.development", "phase.contest", "phase.decisive" };
            var allowedByPhase = new[]
            {
                new[] { "intent.develop", "intent.reserve", "intent.research", "intent.hold", "intent.assault" },
                IntentIds,
                new[] { "intent.hold", "intent.assault", "intent.raid-economy", "intent.research", "intent.build-tower", "intent.reserve" }
            };
            for (var i = 0; i < phaseList.arraySize; i++)
            {
                var phase = phaseList.GetArrayElementAtIndex(i);
                String(phase, "_id", phaseIds[i]);
                Int(phase, "_startTick", i * 3000);
                Strings(phase.FindPropertyRelative("_allowedIntentIds"), allowedByPhase[i]);
                IntentWeights(phase.FindPropertyRelative("_baseIntentWeights"), i);
                Strings(phase.FindPropertyRelative("_publicEventIds"), Array.Empty<string>());
            }

            var pools = so.FindProperty("_enemyUnitPools");
            pools.arraySize = 1;
            String(pools.GetArrayElementAtIndex(0), "_id", "enemy-unit-pool.prologue");
            Strings(pools.GetArrayElementAtIndex(0).FindPropertyRelative("_unitIds"),
                new[] { "unit.shield-guard", "unit.archer", "unit.siege-ram", "unit.heavy-warrior", "unit.mage", "unit.longbow", "unit.cannon" });

            var utilities = so.FindProperty("_aiUtilityProfiles");
            utilities.arraySize = 3;
            for (var i = 0; i < utilities.arraySize; i++)
                UtilityProfile(utilities.GetArrayElementAtIndex(i), i);

            var economies = so.FindProperty("_enemyEconomyProfiles");
            economies.arraySize = 3;
            for (var i = 0; i < economies.arraySize; i++)
                EconomyProfile(economies.GetArrayElementAtIndex(i), i);

            var doctrines = so.FindProperty("_aiDoctrines");
            doctrines.arraySize = 3;
            var doctrineIds = new[] { "doctrine.development", "doctrine.offensive", "doctrine.adaptive" };
            var doctrineNames = new[] { "发展", "进攻", "自适应" };
            for (var i = 0; i < doctrines.arraySize; i++)
            {
                var doctrine = doctrines.GetArrayElementAtIndex(i);
                String(doctrine, "_id", doctrineIds[i]);
                String(doctrine, "_displayName", doctrineNames[i]);
                DoctrineBiases(doctrine.FindPropertyRelative("_intentBiases"), i);
            }

            var difficulties = so.FindProperty("_difficultyRules");
            difficulties.arraySize = 3;
            var difficultyIds = new[] { "difficulty.standard", "difficulty.standard-fast", "difficulty.nightmare" };
            var mistakeMins = new[] { 600, 900, 0 };
            var mistakeMaxes = new[] { 900, 1200, 0 };
            for (var i = 0; i < difficulties.arraySize; i++)
            {
                var difficulty = difficulties.GetArrayElementAtIndex(i);
                String(difficulty, "_id", difficultyIds[i]);
                Int(difficulty, "_decisionQualityMilli", i == 2 ? 1100 : i == 1 ? 1050 : 1000);
                Int(difficulty, "_reactionDelayTicks", i == 0 ? 20 : 15);
                Int(difficulty, "_suboptimalIntervalMinTicks", mistakeMins[i]);
                Int(difficulty, "_suboptimalIntervalMaxTicks", mistakeMaxes[i]);
                Int(difficulty, "_trainingTimeMultiplierMilli", i == 2 ? 950 : 1000);
            }

            var waves = so.FindProperty("_resourceActivationWaves");
            waves.arraySize = 36;
            var modeIds = new[]
            {
                "mode.prologue.peaceful", "mode.prologue.offensive", "mode.prologue.nightmare",
                "mode.river-pass.peaceful", "mode.river-pass.offensive", "mode.river-pass.nightmare"
            };
            for (var i = 0; i < modeIds.Length; i++)
            {
                var offset = i * 6;
                ResourceWave(waves.GetArrayElementAtIndex(offset), $"resource-wave.{i}.opening",
                    modeIds[i], 0, 3,
                    new[] { ResourceNodeSpawnGroup.PlayerSafe, ResourceNodeSpawnGroup.EnemySafe },
                    new[] { "resource.food", "resource.wood", "resource.raw-stone" });
                ResourceWave(waves.GetArrayElementAtIndex(offset + 1), $"resource-wave.{i}.central-food-wood",
                    modeIds[i], 60, 2, new[] { ResourceNodeSpawnGroup.Central },
                    new[] { "resource.food", "resource.wood" });
                ResourceWave(waves.GetArrayElementAtIndex(offset + 2), $"resource-wave.{i}.central-stone",
                    modeIds[i], 120, 1, new[] { ResourceNodeSpawnGroup.Central },
                    new[] { "resource.raw-stone" });
                ResourceWave(waves.GetArrayElementAtIndex(offset + 3), $"resource-wave.{i}.central-iron",
                    modeIds[i], 180, 1, new[] { ResourceNodeSpawnGroup.Central },
                    new[] { "resource.iron-ore" });
                ResourceWave(waves.GetArrayElementAtIndex(offset + 4), $"resource-wave.{i}.central-weighted-a",
                    modeIds[i], 240, 1, new[] { ResourceNodeSpawnGroup.Central },
                    new[] { "resource.food", "resource.wood", "resource.raw-stone", "resource.iron-ore" });
                ResourceWave(waves.GetArrayElementAtIndex(offset + 5), $"resource-wave.{i}.central-weighted-b",
                    modeIds[i], 300, 1, new[] { ResourceNodeSpawnGroup.Central },
                    new[] { "resource.food", "resource.wood", "resource.raw-stone", "resource.iron-ore" });
            }

            End(so);
        }

        private static void ConfigurePresentation(PresentationCatalog catalog)
        {
            var ids = new List<string>
            {
                "presentation.resource.food", "presentation.resource.meat", "presentation.resource.wine", "presentation.resource.wood", "presentation.resource.plank", "presentation.resource.raw-stone", "presentation.resource.stone", "presentation.resource.iron-ore", "presentation.resource.iron-ingot", "presentation.resource.gold",
                "presentation.boss.stone-golem", "presentation.research.melee", "presentation.research.ranged", "presentation.research.magic", "presentation.research.siege",
                "presentation.map.prologue", "presentation.map.river-pass",
                "presentation.worker.food.player", "presentation.worker.food.enemy", "presentation.worker.wood.player", "presentation.worker.wood.enemy",
                "presentation.worker.stone.player", "presentation.worker.stone.enemy", "presentation.worker.iron.player", "presentation.worker.iron.enemy",
                "presentation.enemy-order-route"
            };
            ids.AddRange(new[] { "presentation.reward.building", "presentation.reward.resource", "presentation.reward.reinforcement" });
            foreach (var id in new[]
            {
                "unit.gatherer", "unit.lumberjack", "unit.stonecutter", "unit.iron-miner", "unit.builder", "unit.shield-guard", "unit.archer", "unit.siege-ram", "unit.heavy-warrior", "unit.mage", "unit.longbow", "unit.cannon",
                "building.pasture", "building.winery", "building.sawmill", "building.stoneworks", "building.iron-smelter", "building.warehouse", "building.shield-camp", "building.archer-camp", "building.ram-camp", "building.heavy-warrior-camp", "building.mage-camp", "building.longbow-camp", "building.cannon-camp", "building.research-lab", "building.arrow-tower", "building.gatherer-lodge", "building.wood-gatherer-camp", "building.stone-gatherer-camp", "building.iron-gatherer-camp",
                "card.building.pasture", "card.building.winery", "card.building.sawmill", "card.building.stoneworks", "card.building.iron-smelter", "card.building.warehouse", "card.building.shield-camp", "card.building.archer-camp", "card.building.ram-camp", "card.building.heavy-warrior-camp", "card.building.mage-camp", "card.building.longbow-camp", "card.building.cannon-camp", "card.building.research-lab", "card.building.gatherer-lodge", "card.building.wood-gatherer-camp", "card.building.stone-gatherer-camp", "card.building.iron-gatherer-camp",
                "card.battlefield.arrow-tower", "card.tactic.field-rations", "card.tactic.emergency-supplies", "card.tactic.arrow-rain", "card.soldier.shield-guard", "card.soldier.archer", "card.soldier.siege-ram", "card.soldier.heavy-warrior", "card.soldier.mage", "card.soldier.longbow", "card.soldier.cannon",
                "card.reinforcement.shield-pair", "card.reinforcement.archer-pair", "card.reinforcement.shield-archer", "card.reinforcement.ram-shield", "card.reinforcement.heavy-archers", "card.reinforcement.mage-shields", "card.reinforcement.longbows-shield", "card.reinforcement.elite-trio", "card.reinforcement.cannon-shields", "card.reinforcement.ram-archers"
            }) ids.Add("presentation." + id);
            var reinforcementArt = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["presentation.card.reinforcement.shield-pair"] = "art.unit.shield-guard",
                ["presentation.card.reinforcement.archer-pair"] = "art.unit.archer",
                ["presentation.card.reinforcement.shield-archer"] = "art.unit.shield-guard",
                ["presentation.card.reinforcement.ram-shield"] = "art.unit.siege-ram",
                ["presentation.card.reinforcement.heavy-archers"] = "art.unit.heavy-warrior",
                ["presentation.card.reinforcement.mage-shields"] = "art.unit.mage",
                ["presentation.card.reinforcement.longbows-shield"] = "art.unit.longbow",
                ["presentation.card.reinforcement.elite-trio"] = "art.unit.heavy-warrior",
                ["presentation.card.reinforcement.cannon-shields"] = "art.unit.cannon",
                ["presentation.card.reinforcement.ram-archers"] = "art.unit.siege-ram"
            };
            var unitWorldPrefabs = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["presentation.world.unit.shield-guard.player"] = "world.unit.shield.player",
                ["presentation.world.unit.shield-guard.enemy"] = "world.unit.shield.enemy",
                ["presentation.world.unit.archer.player"] = "world.unit.archer.player",
                ["presentation.world.unit.archer.enemy"] = "world.unit.archer.enemy",
                ["presentation.world.unit.siege-ram.player"] = "world.unit.ram.player",
                ["presentation.world.unit.siege-ram.enemy"] = "world.unit.ram.enemy",
                ["presentation.world.unit.heavy-warrior.player"] = "world.unit.heavy-warrior.player",
                ["presentation.world.unit.heavy-warrior.enemy"] = "world.unit.heavy-warrior.enemy",
                ["presentation.world.unit.mage.player"] = "world.unit.mage.player",
                ["presentation.world.unit.mage.enemy"] = "world.unit.mage.enemy",
                ["presentation.world.unit.longbow.player"] = "world.unit.longbow.player",
                ["presentation.world.unit.longbow.enemy"] = "world.unit.longbow.enemy",
                ["presentation.world.unit.cannon.player"] = "world.unit.cannon.player",
                ["presentation.world.unit.cannon.enemy"] = "world.unit.cannon.enemy",
                ["presentation.world.projectile.arrow"] = "world.projectile.arrow",
                ["presentation.world.projectile.fireball"] = "world.projectile.fireball",
                ["presentation.world.projectile.cannonball"] = "world.projectile.cannonball"
            };
            ids.AddRange(unitWorldPrefabs.Keys);
            ConfigureList(catalog, "_definitions", ids.Count, (item, index) =>
            {
                var id = ids[index];
                String(item, "_id", id);
                var resourceKey = id == "presentation.enemy-order-route"
                    ? "world.enemy-order-route"
                    : unitWorldPrefabs.TryGetValue(id, out var prefabKey)
                        ? prefabKey
                        : reinforcementArt.TryGetValue(id, out var reinforcementSprite)
                            ? reinforcementSprite
                        : id.StartsWith("presentation.card.soldier.", StringComparison.Ordinal)
                            ? "art.unit." + id["presentation.card.soldier.".Length..]
                            : id.StartsWith("presentation.card.building.", StringComparison.Ordinal)
                                ? "art.building." + id["presentation.card.building.".Length..]
                                : id == "presentation.card.battlefield.arrow-tower"
                                    ? "art.building.arrow-tower"
                                    : id.Replace("presentation.", "art.");
                String(item, "_resourceKey", resourceKey);
            });
        }

        private static void ConfigureEffect(SerializedProperty item, string id, TacticEffectKind kind, TacticTargetKind target, int magnitude, int radius, (string, int)[] resources)
        { String(item, "_id", id); Enum(item, "_kind", (int)kind); Enum(item, "_targetKind", (int)target); ResourceAmounts(item.FindPropertyRelative("_resourceAmounts"), resources); Int(item, "_magnitude", magnitude); Int(item, "_radius", radius); Int(item, "_durationTicks", 0); Int(item, "_perMatchLimit", 0); }
        private static void Wall(SerializedProperty item, string id, int x, int y) { String(item, "_id", id); Int(item, "_maxHealth", 5000); Point(item.FindPropertyRelative("_gate"), id + ".gate", x, y); }
        private static void Zone(SerializedProperty item, string id, ZoneKind kind, int x, int y, int w, int h) { String(item, "_id", id); Enum(item, "_kind", (int)kind); Int(item, "_x", x); Int(item, "_y", y); Int(item, "_width", w); Int(item, "_height", h); }
        private static void Route(SerializedProperty item, string id, int y) { String(item, "_id", id); var points = item.FindPropertyRelative("_points"); points.arraySize = 3; Point(points.GetArrayElementAtIndex(0), id + ".0", 518, y); Point(points.GetArrayElementAtIndex(1), id + ".1", 1120, y); Point(points.GetArrayElementAtIndex(2), id + ".2", 1824, y); }
        private static void Node(SerializedProperty item, string id, ResourceNodeSpawnGroup group, string mirror, int x, int y) { String(item, "_id", id); String(item, "_resourceId", string.Empty); Enum(item, "_spawnGroup", (int)group); String(item, "_mirrorNodeId", mirror); Strings(item.FindPropertyRelative("_allowedResourceIds"), new[] { "resource.food", "resource.wood", "resource.raw-stone", "resource.iron-ore" }); Point(item.FindPropertyRelative("_position"), id + ".point", x, y); Int(item, "_capacity", group == ResourceNodeSpawnGroup.Central ? 160 : 100); Int(item, "_respawnCapacity", group == ResourceNodeSpawnGroup.Central ? 0 : 30); Int(item, "_respawnDelayTicks", group == ResourceNodeSpawnGroup.Central ? 450 : 1800); }
private static void Gatherer(SerializedProperty item, string sourceId, string routeId, string unitId,
            string[] allowedResourceIds, int carryAmount)
        {
            String(item, "_sourceId", sourceId);
            String(item, "_routeId", routeId);
            String(item, "_unitId", unitId);
            Strings(item.FindPropertyRelative("_allowedResourceIds"), allowedResourceIds);
            Int(item, "_carryAmount", carryAmount);
            Int(item, "_gatherTicks", 80);
        }
private static void ResourceWave(SerializedProperty item, string id, string mapModeId,
            int seconds, int count, ResourceNodeSpawnGroup[] groups, string[] allowedResourceIds)
        {
            String(item, "_id", id);
            String(item, "_mapModeId", mapModeId);
            Int(item, "_triggerSeconds", seconds);
            Int(item, "_nodesPerGroup", count);
            var groupList = item.FindPropertyRelative("_groups");
            groupList.arraySize = groups.Length;
            for (var i = 0; i < groups.Length; i++)
                groupList.GetArrayElementAtIndex(i).enumValueIndex = (int)groups[i];
            Strings(item.FindPropertyRelative("_allowedResourceIds"), allowedResourceIds);
        }
        private static void ConfigureCampaignStage(SerializedProperty item, string id, string prerequisite, string battlefieldId, string[] cards) { String(item, "_id", id); String(item, "_prerequisiteStageId", prerequisite); Strings(item.FindPropertyRelative("_unlockedBattlefieldIds"), new[] { battlefieldId }); Strings(item.FindPropertyRelative("_purchasableCardIds"), cards); }
        private static void ConfigureMapMode(SerializedProperty item, string battlefield, int mode) { var suffixes = new[] { "peaceful", "offensive", "nightmare" }; String(item, "_id", $"mode.{battlefield}.{suffixes[mode]}"); Enum(item, "_kind", mode); String(item, "_aiDoctrineId", new[] { "doctrine.development", "doctrine.offensive", "doctrine.adaptive" }[mode]); String(item, "_difficultyRulesId", new[] { "difficulty.standard", "difficulty.standard-fast", "difficulty.nightmare" }[mode]); String(item, "_aiPhaseProfileId", "ai-phase.standard"); String(item, "_aiUtilityProfileId", $"ai-utility.{mode}"); String(item, "_enemyEconomyProfileId", $"enemy-economy.{mode}"); String(item, "_enemyUnitPoolId", "enemy-unit-pool.prologue"); String(item, "_rewardTableId", battlefield == "prologue" ? "reward.prologue" : "reward.river-pass"); Int(item, "_rewardMultiplierMilli", new[] { 1000, 1250, 1500 }[mode]); }
        private static void BossSpawn(SerializedProperty item, string id, int x, int y, int warning, int spawn) { String(item, "_id", id); Point(item.FindPropertyRelative("_position"), id + ".point", x, y); Int(item, "_warningTick", warning); Int(item, "_spawnTick", spawn); }
        private static void Point(SerializedProperty item, string id, int x, int y) { String(item, "_id", id); Int(item, "_x", x); Int(item, "_y", y); }
        private static void BossRewards(SerializedProperty property, bool enemy) { property.arraySize = 3; for (var i = 0; i < 3; i++) { var item = property.GetArrayElementAtIndex(i); String(item, "_id", (enemy ? "boss-reward.enemy." : "boss-reward.player.") + i); Enum(item, "_kind", i == 0 ? (int)BossRewardKind.ResourceBundle : enemy ? (int)BossRewardKind.EnemyUnitLevel + Math.Min(i - 1, 1) : i - 1); Int(item, "_weight", 100 - i * 10); Int(item, "_magnitude", i == 0 ? 25 : 100 + i * 50); Int(item, "_durationTicks", i == 2 ? 200 : 0); } }
        private static void RewardBundle(SerializedProperty property, string id, string displayName, RewardRarity rarity,
            (string id, int amount)[] amounts)
        { String(property, "_id", id); String(property, "_displayName", displayName); Enum(property, "_rarity", (int)rarity); ResourceAmounts(property.FindPropertyRelative("_amounts"), amounts); }
        private static void Reinforcement(SerializedProperty property, string id, string displayName, int minimumHeatTier,
            (string id, int quantity)[] units)
        {
            String(property, "_id", id); String(property, "_displayName", displayName); Int(property, "_minimumHeatTier", minimumHeatTier);
            Enum(property, "_rarity", minimumHeatTier == 0 ? (int)RewardRarity.Common : minimumHeatTier < 3 ? (int)RewardRarity.Rare : (int)RewardRarity.Epic);
            var list = property.FindPropertyRelative("_units"); list.arraySize = units.Length;
            for (var i = 0; i < units.Length; i++)
            { var unit = list.GetArrayElementAtIndex(i); String(unit, "_unitId", units[i].id); Int(unit, "_quantity", units[i].quantity); }
        }
private static void UtilityProfile(SerializedProperty item, int index)
        {
            String(item, "_id", "ai-utility." + index);
            Int(item, "_temperatureMilli", new[] { 900, 650, 450 }[index]);
            Int(item, "_decisionIntervalTicks", new[] { 100, 90, 80 }[index]);
            Int(item, "_pressureMinIntervalTicks", new[] { 550, 350, 300 }[index]);
            Int(item, "_pressureTargetIntervalTicks", new[] { 650, 450, 375 }[index]);
            Int(item, "_pressureMaxIntervalTicks", new[] { 750, 550, 450 }[index]);
            Int(item, "_activeUnitSoftCap", new[] { 22, 24, 26 }[index]);
            Int(item, "_queuedUnitSoftCap", new[] { 8, 10, 10 }[index]);
            Int(item, "_logisticsThreatMemoryTicks", 300);
            Int(item, "_maxConcurrentLogisticsResponses", 2);
            Int(item, "_emergencyDefenseOverflowUnits", 2);
            Int(item, "_towerEscalationKillCount", 2);

            var definitions = new List<(string feature, string intent, int coefficient)>();
            var featureIds = new[]
            {
                "feature.resource-pressure", "feature.enemy-wall-danger", "feature.player-wall-damage",
                "feature.reserve", "feature.boss-event", "feature.research-open", "feature.tower-gap"
            };
            for (var i = 0; i < IntentIds.Length; i++)
            {
                definitions.Add((featureIds[i], IntentIds[i], 160 + i * 15));
                definitions.Add((i is 2 or 3 ? "feature.boss-event" : "feature.reserve",
                    IntentIds[i], i is 2 or 6 ? 140 : 60));
            }
            definitions.Add(("feature.pressure-due", "intent.assault", 280));
            definitions.Add(("feature.pressure-due", "intent.raid-economy", 220));
            definitions.Add(("feature.pressure-due", "intent.reserve", 90));
            definitions.Add(("feature.recovery-needed", "intent.hold", 220));
            definitions.Add(("feature.recovery-needed", "intent.reserve", 260));
            definitions.Add(("feature.recovery-needed", "intent.develop", 160));
            definitions.Add(("feature.overextension", "intent.assault", -260));
            definitions.Add(("feature.overextension", "intent.raid-economy", -220));
            definitions.Add(("feature.overextension", "intent.hold", 240));

            var features = item.FindPropertyRelative("_featureCoefficients");
            features.arraySize = definitions.Count;
            for (var i = 0; i < definitions.Count; i++)
            {
                var feature = features.GetArrayElementAtIndex(i);
                String(feature, "_featureId", definitions[i].feature);
                String(feature, "_intentId", definitions[i].intent);
                Int(feature, "_coefficient", definitions[i].coefficient);
            }

            var commitments = item.FindPropertyRelative("_commitments");
            commitments.arraySize = IntentIds.Length;
            for (var i = 0; i < IntentIds.Length; i++)
            {
                var commitment = commitments.GetArrayElementAtIndex(i);
                String(commitment, "_intentId", IntentIds[i]);
                Int(commitment, "_minimumTicks", i == 0 ? 200 : i < 4 ? 150 : 80);
                Enum(commitment, "_policy", i < 4
                    ? (int)AiCommitmentPolicy.Duration
                    : i == 4 ? (int)AiCommitmentPolicy.ConstructionSiteCreated
                    : (int)AiCommitmentPolicy.OrderComplete);
            }
            Int(item, "_switchCost", 120);
            Int(item, "_repetitionPenalty", 180);
            Int(item, "_softmaxLookupVersion", 1);
        }
private static void EconomyProfile(SerializedProperty item, int index)
        {
            String(item, "_id", "enemy-economy." + index);
            Int(item, "_trainingTimeMultiplierMilli", index == 2 ? 950 : 1000);
            Int(item, "_economicEfficiencyMilli", new[] { 1000, 1050, 1100 }[index]);
            ResourceAmounts(item.FindPropertyRelative("_initialInventory"),
                new[] { ("resource.meat", 160), ("resource.food", 20), ("resource.wine", 60), ("resource.plank", 40), ("resource.stone", 100), ("resource.iron-ingot", 100) });

            var facilities = item.FindPropertyRelative("_facilities");
            facilities.arraySize = 4;
            var buildingIds = new[]
            {
                "building.gatherer-lodge", "building.wood-gatherer-camp", "building.winery", "building.iron-smelter"
            };
            for (var i = 0; i < facilities.arraySize; i++)
            {
                String(facilities.GetArrayElementAtIndex(i), "_buildingId", buildingIds[i]);
                Int(facilities.GetArrayElementAtIndex(i), "_level", 1);
            }

            var camps = item.FindPropertyRelative("_camps");
            camps.arraySize = 7;
            var units = new[] { "unit.shield-guard", "unit.archer", "unit.siege-ram", "unit.heavy-warrior", "unit.mage", "unit.longbow", "unit.cannon" };
            for (var i = 0; i < camps.arraySize; i++)
            {
                String(camps.GetArrayElementAtIndex(i), "_unitId", units[i]);
                Int(camps.GetArrayElementAtIndex(i), "_slotCount", 1);
            }

            Strings(item.FindPropertyRelative("_initialHandCardIds"), new[]
            {
                "card.building.gatherer-lodge", "card.building.wood-gatherer-camp", "card.building.winery",
                "card.building.sawmill", "card.building.shield-camp", "card.building.archer-camp"
            });

            var formations = item.FindPropertyRelative("_formations");
            formations.arraySize = 8;
            var formationIds = new[]
                { "formation.probe", "formation.shield-archer", "formation.economy-raid", "formation.siege-cover", "formation.magic", "formation.longbow", "formation.cannon", "formation.logistics-guard" };
            var formationUnits = new[]
            {
                new[] { "unit.archer" },
                new[] { "unit.shield-guard", "unit.archer" },
                new[] { "unit.archer", "unit.archer" },
                new[] { "unit.shield-guard", "unit.siege-ram" },
                new[] { "unit.heavy-warrior", "unit.mage" },
                new[] { "unit.shield-guard", "unit.longbow" },
                new[] { "unit.heavy-warrior", "unit.cannon" },
                new[] { "unit.shield-guard" }
            };
            var quantities = new[]
                { new[] { 1 }, new[] { 2, 2 }, new[] { 1, 2 }, new[] { 2, 1 }, new[] { 2, 1 }, new[] { 2, 2 }, new[] { 2, 1 }, new[] { 1 } };
            var allowedIntents = new[]
            {
                new[] { "intent.assault", "intent.hold" },
                new[] { "intent.assault", "intent.hold" },
                new[] { "intent.raid-economy" },
                new[] { "intent.assault" },
                new[] { "intent.assault", "intent.hold" },
                new[] { "intent.assault", "intent.hold" },
                new[] { "intent.assault" },
                new[] { "intent.hold" }
            };
            for (var i = 0; i < formations.arraySize; i++)
            {
                var formation = formations.GetArrayElementAtIndex(i);
                String(formation, "_id", formationIds[i]);
                Strings(formation.FindPropertyRelative("_unitIds"), formationUnits[i]);
                Ints(formation.FindPropertyRelative("_quantities"), quantities[i]);
                Strings(formation.FindPropertyRelative("_allowedIntentIds"), allowedIntents[i]);
            }

            String(item, "_defenseReserveFormationId", "formation.logistics-guard");

            Int(item, "_reserveRatioMilli", index == 0 ? 420 : index == 1 ? 220 : 180);
            Int(item, "_gatherCycleTicks", 70);
            Int(item, "_processingCycleTicks", 50);
            Int(item, "_builderRespawnTicks", 80);
        }
        private static void IntentWeights(SerializedProperty property, int phase) { var weights = phase == 0 ? new[] { 90, 25, 20, 15, 20, 75, 70 } : phase == 1 ? new[] { 35, 75, 55, 80, 70, 45, 30 } : new[] { 20, 85, 100, 55, 45, 25, 10 }; property.arraySize = IntentIds.Length; for (var i = 0; i < IntentIds.Length; i++) { String(property.GetArrayElementAtIndex(i), "_intentId", IntentIds[i]); Int(property.GetArrayElementAtIndex(i), "_weight", weights[i]); } }
        private static void DoctrineBiases(SerializedProperty property, int doctrine) { var weights = doctrine == 0 ? new[] { 35, 5, -15, -10, 5, 30, 35 } : doctrine == 1 ? new[] { -10, 15, 35, 30, 10, 0, -25 } : new[] { 0, 20, 25, 20, 15, 10, -10 }; property.arraySize = IntentIds.Length; for (var i = 0; i < IntentIds.Length; i++) { String(property.GetArrayElementAtIndex(i), "_intentId", IntentIds[i]); Int(property.GetArrayElementAtIndex(i), "_weight", weights[i]); } }
        private static void Upgrades(SerializedProperty property, bool enabled) { property.arraySize = enabled ? 2 : 0; for (var i = 0; i < property.arraySize; i++) { var value = property.GetArrayElementAtIndex(i); Int(value, "_level", i + 2); Int(value, "_requiredEffectiveWorkCount", i == 0 ? 4 : 10); String(value, "_requiredMatchPhaseId", string.Empty); String(value, "_paymentResourceId", ContentConstants.PlankResourceId); Int(value, "_cost", i == 0 ? 20 : 45); Float(value, "_durationSeconds", i == 0 ? 5f : 8f); Int(value, "_productionMultiplierMilli", i == 0 ? 1150 : 1300); Int(value, "_trainingTimeMultiplierMilli", i == 0 ? 850 : 700); } }
        private static void ResourceAmount(SerializedProperty item, string id, int amount) { String(item, "_resourceId", id); Int(item, "_amount", amount); }
        private static void ResourceAmounts(SerializedProperty property, (string id, int amount)[] values) { property.arraySize = values.Length; for (var i = 0; i < values.Length; i++) ResourceAmount(property.GetArrayElementAtIndex(i), values[i].id, values[i].amount); }
        private static void Strings(SerializedProperty property, string[] values) { property.arraySize = values.Length; for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).stringValue = values[i]; }
        private static void Ints(SerializedProperty property, int[] values) { property.arraySize = values.Length; for (var i = 0; i < values.Length; i++) property.GetArrayElementAtIndex(i).intValue = values[i]; }
        private static void ConfigureList(UnityEngine.Object target, string name, int count, Action<SerializedProperty, int> configure) { var so = Begin(target); var list = so.FindProperty(name); list.arraySize = count; for (var i = 0; i < count; i++) configure(list.GetArrayElementAtIndex(i), i); End(so); }
        private static SerializedObject Begin(UnityEngine.Object target) { Undo.RecordObject(target, "Configure P1 Prototype content"); return new SerializedObject(target); }
        private static void End(SerializedObject so) { so.ApplyModifiedProperties(); EditorUtility.SetDirty(so.targetObject); }
        private static void String(SerializedProperty parent, string name, string value) => parent.FindPropertyRelative(name).stringValue = value;
        private static void Int(SerializedProperty parent, string name, int value) => parent.FindPropertyRelative(name).intValue = value;
        private static void Float(SerializedProperty parent, string name, float value) => parent.FindPropertyRelative(name).floatValue = value;
        private static void Bool(SerializedProperty parent, string name, bool value) => parent.FindPropertyRelative(name).boolValue = value;
        private static void Enum(SerializedProperty parent, string name, int value) => parent.FindPropertyRelative(name).enumValueIndex = value;

        private readonly struct UnitRow
        {
            public UnitRow(string id, string name, int health, int damage, int move, int collision, int acquire, int chase, int range, int interval, int projectile, int wallMultiplier, bool canAttack, float trainingSeconds, string role) { Id = id; Name = name; Health = health; Damage = damage; Move = move; Collision = collision; Acquire = acquire; Chase = chase; Range = range; Interval = interval; Projectile = projectile; WallMultiplier = wallMultiplier; CanAttack = canAttack; TrainingSeconds = trainingSeconds; Role = role; }
            public readonly string Id, Name, Role; public readonly int Health, Damage, Move, Collision, Acquire, Chase, Range, Interval, Projectile, WallMultiplier; public readonly bool CanAttack; public readonly float TrainingSeconds;
        }
    }
}
#endif
