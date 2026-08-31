using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Content;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Runtime.Resources;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class ReinforcementCardContentTests
    {
        private const string RootPath = "Assets/Game/Content/Config/GameContentConfig.asset";
        private const string VisualPrefabPath = "Assets/Game/Content/Prefabs/UI/ReinforcementCardVisual.prefab";
        private const string GameplayPrefabPath = "Assets/Game/Content/Prefabs/UI/Gameplay.prefab";
        private readonly List<UnityEngine.Object> _clones = new();

        [TearDown]
        public void TearDown()
        {
            foreach (var clone in _clones) UnityEngine.Object.DestroyImmediate(clone);
            _clones.Clear();
        }

        [Test]
        public void EveryUniqueTemplate_HasExactlyOneRewardOnlyCardAndResolvablePresentation()
        {
            var root = LoadRoot();
            var templates = root.RewardCatalog.Definitions.SelectMany(value => value.ReinforcementTemplates)
                .GroupBy(value => value.Id, StringComparer.Ordinal).ToArray();
            var cards = root.CardCatalog.Definitions.Where(value => value.Type == CardType.ReinforcementItem).ToArray();
            var presentations = root.PresentationCatalog.Definitions.ToDictionary(value => value.Id, StringComparer.Ordinal);

            Assert.That(templates.Length, Is.EqualTo(10));
            Assert.That(cards.Length, Is.EqualTo(10));
            foreach (var template in templates)
            {
                var card = cards.Single(value => value.LinkedContentId == template.Key);
                Assert.That(card.Id, Is.EqualTo("card." + template.Key));
                Assert.That(card.MaxMetaLevel, Is.EqualTo(1));
                Assert.That(card.DefaultUnlocked, Is.False);
                Assert.That(card.UpgradeGoldCosts, Is.Empty);
                Assert.That(card.GrowthRules, Is.Empty);
                Assert.That(card.PrerequisiteCardIds, Is.Empty);
                Assert.That(card.OfferTags, Is.Empty);
                Assert.That(presentations.ContainsKey(card.PresentationKey), Is.True);
                Assert.That(presentations[card.PresentationKey].ResourceKey, Does.StartWith("art.unit."));
            }
        }

        [Test]
        public async Task SnapshotFreezesCardIds_AndProgressionExcludesRewardOnlyCards()
        {
            var system = new ContentConfigSystem(new AssetResourceService(LoadRoot()), new ResourceKey("config.game-content"));
            await system.InitializeAsync(new GameContext("reinforcement-card-contract"), CancellationToken.None);
            try
            {
                Assert.That(system.Cards.Any(value => value.Id.Value.StartsWith("card.reinforcement.", StringComparison.Ordinal)), Is.False);
                var snapshot = system.CreateMatchSnapshot(new BattlefieldId("battlefield.prologue"),
                    new MapModeId("mode.prologue.peaceful"), 4411);
                Assert.That(snapshot.HandAndOffers.ReinforcementTemplates.Count, Is.EqualTo(10));
                Assert.That(snapshot.HandAndOffers.ReinforcementTemplates.All(value =>
                    value.CardId.Value == "card." + value.Id.Value &&
                    snapshot.Presentation.CardArt.ContainsKey(value.CardId)), Is.True);
            }
            finally { await system.ShutdownAsync(CancellationToken.None); }
        }

        [Test]
        public async Task RewardChoiceAndHand_UseConfiguredCardIdTypeAndAggregatedUnits()
        {
            var system = new ContentConfigSystem(new AssetResourceService(LoadRoot()), new ResourceKey("config.game-content"));
            await system.InitializeAsync(new GameContext("reinforcement-card-runtime"), CancellationToken.None);
            try
            {
                var runtime = MatchRuntimeFactory.Create(system.CreateMatchSnapshot(new BattlefieldId("battlefield.prologue"),
                    new MapModeId("mode.prologue.peaceful"), 31337));
                foreach (var item in runtime.Systems) await item.InitializeAsync(new GameContext("reinforcement-runtime"), CancellationToken.None);
                try
                {
                    runtime.Hand.SimulateTick(600);
                    var choice = runtime.Hand.GetOffer().Choices.Single(value => value.Kind == RewardChoiceKind.ReinforcementItem);
                    Assert.That(choice.CardId.HasValue, Is.True);
                    Assert.That(runtime.Hand.TryReplaceAndChoose(choice.Id, runtime.Hand.GetHand().First().Id), Is.True);
                    var card = runtime.Hand.GetHand().Single(value => value.ReinforcementTemplateId.HasValue);
                    Assert.That(card.Id, Is.EqualTo(choice.CardId.Value));
                    Assert.That(card.Type, Is.EqualTo(CardType.ReinforcementItem));
                    Assert.That(card.ReinforcementUnits, Is.EqualTo(choice.Units));
                }
                finally { foreach (var item in runtime.Systems.Reverse()) await item.ShutdownAsync(CancellationToken.None); }
            }
            finally { await system.ShutdownAsync(CancellationToken.None); }
        }

        [Test]
        public void Prefabs_HaveFourChoices_AndOnlyChoice3KeepsReinforcementVisual()
        {
            var visual = AssetDatabase.LoadAssetAtPath<GameObject>(VisualPrefabPath);
            Assert.That(visual, Is.Not.Null);
            var visualComponent = visual.GetComponents<Component>().Single(value =>
                value.GetType().FullName == "FortressFrontier.Presentation.Prototype.ReinforcementCardVisual");
            var visualSerialized = new SerializedObject(visualComponent);
            Assert.That(visualSerialized.FindProperty("_unitIcons").arraySize, Is.EqualTo(3));
            Assert.That(visualSerialized.FindProperty("_quantityTexts").arraySize, Is.EqualTo(3));
            Assert.That(visualSerialized.FindProperty("_reinforcementLabel").objectReferenceValue, Is.Not.Null);
            Assert.That(visualSerialized.FindProperty("_titleText").objectReferenceValue, Is.Not.Null);

            var gameplay = AssetDatabase.LoadAssetAtPath<GameObject>(GameplayPrefabPath);
            Assert.That(gameplay.GetComponentsInChildren<Component>(true).Count(value =>
                value.GetType().FullName == "FortressFrontier.Presentation.Prototype.ReinforcementCardVisual"), Is.EqualTo(7));
            var panel = gameplay.GetComponents<Component>().Single(value =>
                value.GetType().FullName == "FortressFrontier.Presentation.Prototype.GameplayPanel");
            var panelSerialized = new SerializedObject(panel);
            AssertCompleteReferences(panelSerialized.FindProperty("_itemReinforcementVisuals"), 6);
            AssertCompleteReferences(panelSerialized.FindProperty("_choiceOptions"), 4);
            AssertCompleteReferences(panelSerialized.FindProperty("_choiceArtImages"), 4);
            var choiceVisuals = panelSerialized.FindProperty("_choiceReinforcementVisuals");
            Assert.That(choiceVisuals.arraySize, Is.EqualTo(4));
            for (var index = 0; index < 3; index++)
                Assert.That(choiceVisuals.GetArrayElementAtIndex(index).objectReferenceValue, Is.Null);
            Assert.That(choiceVisuals.GetArrayElementAtIndex(3).objectReferenceValue, Is.Not.Null);
            var choicePanel = gameplay.transform.Cast<Transform>().SelectMany(DescendantsAndSelf)
                .Single(value => value.name == "ChoicePanel");
            Assert.That(choicePanel.Find("Title")?.GetComponent<UnityEngine.UI.Text>()?.text,
                Is.EqualTo("战后整备 · 四选一"));
            Assert.That(choicePanel.parent.name, Is.EqualTo("ChoicePopCanvas"));
            Assert.That(choicePanel.parent.GetComponent<Canvas>()?.overrideSorting, Is.True);
            Assert.That(choicePanel.parent.GetComponent<Canvas>()?.sortingOrder, Is.EqualTo(200));
            Assert.That(choicePanel.parent.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
            var world = gameplay.transform.Find("World");
            Assert.That(world, Is.Not.Null);
            var worldCanvas = world.GetComponent<Canvas>();
            Assert.That(worldCanvas, Is.Not.Null);
            var worldCanvasSerialized = new SerializedObject(worldCanvas);
            Assert.That(worldCanvasSerialized.FindProperty("m_OverrideSorting").boolValue, Is.True);
            Assert.That(worldCanvasSerialized.FindProperty("m_SortingOrder").intValue, Is.EqualTo(10));
            Assert.That(world.GetComponent<UnityEngine.UI.GraphicRaycaster>(), Is.Not.Null);
        }

        [TestCase("missing-card")]
        [TestCase("missing-presentation")]
        [TestCase("duplicate-template")]
        [TestCase("invalid-progression")]
        public void InvalidReinforcementCards_AreRejected(string defect)
        {
            var root = CloneRootWithCatalog("_cardCatalog", LoadRoot().CardCatalog, out var catalog);
            var serialized = new SerializedObject(catalog);
            var definitions = serialized.FindProperty("_definitions");
            var first = FindDefinition(definitions, "card.reinforcement.shield-pair");
            if (defect == "missing-card") first.FindPropertyRelative("_type").enumValueIndex = (int)CardType.Tactic;
            else if (defect == "missing-presentation") first.FindPropertyRelative("_presentationKey").stringValue = "presentation.missing";
            else if (defect == "invalid-progression") first.FindPropertyRelative("_defaultUnlocked").boolValue = true;
            else FindDefinition(definitions, "card.reinforcement.archer-pair").FindPropertyRelative("_linkedContentId").stringValue = "reinforcement.shield-pair";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Assert.That(ContentConfigValidator.Validate(root).IsValid, Is.False);
        }

        [Test]
        public void ConflictingTemplateAcrossRewardTables_IsRejected()
        {
            var root = CloneRootWithCatalog("_rewardCatalog", LoadRoot().RewardCatalog, out var catalog);
            var serialized = new SerializedObject(catalog);
            serialized.FindProperty("_definitions").GetArrayElementAtIndex(1)
                .FindPropertyRelative("_reinforcementTemplates").GetArrayElementAtIndex(0)
                .FindPropertyRelative("_displayName").stringValue = "冲突名称";
            serialized.ApplyModifiedPropertiesWithoutUndo();

            var report = ContentConfigValidator.Validate(root);
            Assert.That(report.Issues.Any(value => value.Message.Contains("same name", StringComparison.Ordinal)), Is.True);
        }

        private static void AssertCompleteReferences(SerializedProperty property, int count)
        {
            Assert.That(property.arraySize, Is.EqualTo(count));
            for (var index = 0; index < count; index++) Assert.That(property.GetArrayElementAtIndex(index).objectReferenceValue, Is.Not.Null);
        }

        private static IEnumerable<Transform> DescendantsAndSelf(Transform root)
        {
            yield return root;
            foreach (Transform child in root)
                foreach (var descendant in DescendantsAndSelf(child)) yield return descendant;
        }

        private static SerializedProperty FindDefinition(SerializedProperty definitions, string id)
        {
            for (var index = 0; index < definitions.arraySize; index++)
            {
                var value = definitions.GetArrayElementAtIndex(index);
                if (value.FindPropertyRelative("_id").stringValue == id) return value;
            }
            throw new AssertionException("Missing card definition: " + id);
        }

        private GameContentConfig CloneRootWithCatalog<T>(string fieldName, T source, out T clone) where T : ScriptableObject
        {
            var root = UnityEngine.Object.Instantiate(LoadRoot()); clone = UnityEngine.Object.Instantiate(source);
            _clones.Add(root); _clones.Add(clone);
            var serialized = new SerializedObject(root);
            serialized.FindProperty(fieldName).objectReferenceValue = clone;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return root;
        }

        private static GameContentConfig LoadRoot() => AssetDatabase.LoadAssetAtPath<GameContentConfig>(RootPath)
            ?? throw new AssertionException("Missing GameContentConfig asset.");

        private sealed class AssetResourceService : IResourceService
        {
            private readonly GameContentConfig _asset;
            public AssetResourceService(GameContentConfig asset) => _asset = asset;
            public Task<IAssetLease<T>> AcquireAsync<T>(ResourceKey key, CancellationToken cancellationToken) where T : UnityEngine.Object =>
                Task.FromResult<IAssetLease<T>>(new Lease<T>(key, _asset as T));
            public Task<IInstanceLease> SpawnAsync(ResourceKey key, Transform parent, CancellationToken cancellationToken) => throw new NotSupportedException();
            public Task PreloadAsync(IReadOnlyCollection<ResourceKey> keys, CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class Lease<T> : IAssetLease<T> where T : UnityEngine.Object
        {
            public Lease(ResourceKey key, T asset) { Key = key; Asset = asset; }
            public ResourceKey Key { get; }
            public T Asset { get; }
            public void Dispose() { }
        }
    }
}
