using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Identifiers;
using FortressFrontier.Runtime.Flow;
using FortressFrontier.Runtime.Prototype;
using FortressFrontier.Runtime.Progression;
using FortressFrontier.Runtime.Content;
using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class VisualPrototypeProviderTests
    {
        [Test]
        public void StableIds_RejectEmptyValuesAndUseOrdinalEquality()
        {
            Assert.Throws<System.ArgumentException>(() => new CardId(" "));
            Assert.AreEqual(new BattlefieldId("battlefield.grassland-frontier"), new BattlefieldId("battlefield.grassland-frontier"));
            Assert.AreNotEqual(new MapModeId("mode.peaceful-growth"), new MapModeId("MODE.PEACEFUL-GROWTH"));
        }

        [Test]
        public async Task SelectionProvider_ProducesStableLaunchRequest()
        {
            var flow = new RecordingApplicationFlow();
            var provider = new SelectionPrototypeProvider(flow);
            provider.SelectMode(new MapModeId("mode.prologue.nightmare"));
            provider.SelectCategory(SelectionCategory.Soldiers);

            Assert.AreEqual(7, provider.Snapshot.Cards.Count);
            Assert.AreEqual(1, provider.Snapshot.CardPageCount);
            Assert.AreEqual("mode.prologue.nightmare", provider.Snapshot.ModeId.Value);

            await provider.StartMatchAsync(CancellationToken.None);
            Assert.IsTrue(flow.Request.HasValue);
            Assert.AreEqual("battlefield.prologue", flow.Request.Value.BattlefieldId.Value);
            Assert.AreEqual("mode.prologue.nightmare", flow.Request.Value.MapModeId.Value);
        }

        [Test]
        public void SelectionProvider_ShowsLockedConfiguredBattlefieldAndRejectsLaunch()
        {
            var flow = new RecordingApplicationFlow();
            var provider = new SelectionPrototypeProvider(flow, new LockedProgression(), null, new TwoBattlefields());

            provider.CycleBattlefield(1);
            Assert.That(provider.Snapshot.BattlefieldId.Value, Is.EqualTo("battlefield.river-pass"));
            Assert.That(provider.Snapshot.BattlefieldUnlocked, Is.False);
            Assert.That(provider.Snapshot.ModeIds.Select(value => value.Value), Is.EqualTo(new[]
            {
                "mode.river-pass.peaceful", "mode.river-pass.offensive", "mode.river-pass.nightmare"
            }));
            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await provider.StartMatchAsync(CancellationToken.None));
            Assert.That(flow.Request, Is.Null);
        }

        [Test]
        public void SelectionProvider_UsesConfiguredArtForEveryPageAndCategory()
        {
            var content = new TwoBattlefields();
            var provider = new SelectionPrototypeProvider(new RecordingApplicationFlow(), null, null, content);

            AssertCardArt(provider.Snapshot, content);
            provider.CycleCardPage(1);
            AssertCardArt(provider.Snapshot, content);

            foreach (var category in new[]
                     {
                         SelectionCategory.Soldiers, SelectionCategory.Camps, SelectionCategory.Tactics
                     })
            {
                provider.SelectCategory(category);
                AssertCardArt(provider.Snapshot, content);
            }
        }

        [Test]
        public void GameplayProvider_ExposesAllDemonstrationStates()
        {
            var provider = new GameplayPrototypeProvider();
            provider.SelectTab(GameplayCardTab.Items);
            provider.UseItem();
            provider.ToggleBuildingMenu();
            provider.CycleBlueprintState();

            var snapshot = provider.Snapshot;
            Assert.AreEqual(GameplayCardTab.Items, snapshot.Tab);
            Assert.AreEqual(3, snapshot.ItemCount);
            Assert.IsTrue(snapshot.BuildingMenuOpen);
            Assert.AreEqual(BlueprintVisualState.Blocked, snapshot.BlueprintState);
            Assert.IsFalse(snapshot.ChoiceOpen);
        }

        private sealed class RecordingApplicationFlow : IApplicationFlow
        {
            public MatchLaunchRequest? CurrentMatch => Request;
            public MatchLaunchRequest? Request { get; private set; }
            public Task StartMatchAsync(MatchLaunchRequest request, CancellationToken cancellationToken) { Request = request; return Task.CompletedTask; }
            public Task ReturnToSelectionAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        }

        private sealed class LockedProgression : IProgressionReader
        {
            public ProgressionSnapshot GetSnapshot() => new(200, new CampaignStageId("stage.prologue"),
                Array.Empty<CardProgressSnapshot>(), new[] { new BattlefieldId("battlefield.prologue") },
                Array.Empty<BattlefieldId>());
        }

        private sealed class TwoBattlefields : ISelectionContent
        {
            private static readonly string[] SelectionCardIds =
            {
                "card.soldier.shield-guard", "card.soldier.archer", "card.soldier.siege-ram",
                "card.soldier.heavy-warrior", "card.soldier.mage", "card.soldier.longbow",
                "card.soldier.cannon", "card.building.sawmill", "card.building.gatherer-lodge",
                "card.building.wood-gatherer-camp", "card.building.stone-gatherer-camp",
                "card.building.iron-gatherer-camp", "card.building.shield-camp",
                "card.building.research-lab", "card.battlefield.arrow-tower", "card.tactic.arrow-rain"
            };

            public IReadOnlyList<SelectionBattlefieldDefinition> Battlefields { get; } = new[]
            {
                new SelectionBattlefieldDefinition(new BattlefieldId("battlefield.prologue"), "边境序章", new[]
                {
                    new MapModeId("mode.prologue.peaceful"), new MapModeId("mode.prologue.offensive"), new MapModeId("mode.prologue.nightmare")
                }),
                new SelectionBattlefieldDefinition(new BattlefieldId("battlefield.river-pass"), "河谷关隘", new[]
                {
                    new MapModeId("mode.river-pass.peaceful"), new MapModeId("mode.river-pass.offensive"), new MapModeId("mode.river-pass.nightmare")
                })
            };

            public IReadOnlyDictionary<CardId, ResourceKey> CardArt { get; } = SelectionCardIds.ToDictionary(
                value => new CardId(value), value => new ResourceKey("test.art." + value));
        }

        private static void AssertCardArt(SelectionViewModel snapshot, ISelectionContent content)
        {
            Assert.That(snapshot.Cards, Is.Not.Empty);
            foreach (var card in snapshot.Cards)
            {
                Assert.That(card.ArtKey.Value, Is.Not.Empty, $"Selection art is empty for {card.Id}.");
                Assert.That(card.ArtKey, Is.EqualTo(content.CardArt[card.Id]),
                    $"Selection art does not match configured presentation for {card.Id}.");
            }
        }
    }
}
