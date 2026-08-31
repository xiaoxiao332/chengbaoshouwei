using FortressFrontier.Core.Identifiers;
using FortressFrontier.Presentation.Audio;
using FortressFrontier.Runtime.Audio;
using FortressFrontier.Core.Systems;
using FortressFrontier.Runtime.Gameplay;
using FortressFrontier.Tests.Shared;
using NUnit.Framework;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class AudioSystemTests
    {
        [TestCase("battlefield.prologue", "phase.development", "audio.bgm.prologue.development")]
        [TestCase("battlefield.prologue", "phase.contest", "audio.bgm.prologue.contest")]
        [TestCase("battlefield.prologue", "phase.decisive", "audio.bgm.prologue.decisive")]
        [TestCase("battlefield.river-pass", "phase.development", "audio.bgm.river-pass.development")]
        [TestCase("battlefield.river-pass", "phase.contest", "audio.bgm.river-pass.contest")]
        [TestCase("battlefield.river-pass", "phase.decisive", "audio.bgm.river-pass.decisive")]
        public void PhaseMusic_MapsBattlefieldAndPhase(string battlefield, string phase, string expected)
        {
            var key = GameplayAudioSystem.ResolvePhaseMusic(
                new BattlefieldId(battlefield), new MatchPhaseId(phase));
            Assert.That(key.Value, Is.EqualTo(expected));
        }

        [Test]
        public void StressRequests_AreCappedAndDroppedWithoutQueue()
        {
            var hit = new SfxAdmissionBudget(4, 6f, 3f, 3);
            var gather = new SfxAdmissionBudget(2, 3f, 2f, 2);
            var activeHit = 0;
            var activeGather = 0;
            var acceptedHit = 0;
            var acceptedGather = 0;

            for (var index = 0; index < 100; index++)
                if (hit.TryAdmit(activeHit, 10)) { activeHit++; acceptedHit++; }
            for (var index = 0; index < 50; index++)
                if (gather.TryAdmit(activeGather, 10)) { activeGather++; acceptedGather++; }

            Assert.That(acceptedHit, Is.EqualTo(3), "The hit burst and same-frame cap are both three.");
            Assert.That(acceptedGather, Is.EqualTo(2));
            Assert.That(activeHit, Is.LessThanOrEqualTo(4));
            Assert.That(activeGather, Is.LessThanOrEqualTo(2));
            Assert.That(hit.TryAdmit(0, 11), Is.False, "Dropped requests must not be queued into the next frame.");
            Assert.That(gather.TryAdmit(0, 11), Is.False, "Dropped requests must not be queued into the next frame.");

            hit.Tick(1f / 6f);
            gather.Tick(1f / 3f);
            Assert.That(hit.TryAdmit(0, 12), Is.True);
            Assert.That(gather.TryAdmit(0, 12), Is.True);
        }

        [Test]
        public async Task PositiveHarvest_EmitsGatherCompleteEvent()
        {
            var runtime = MatchRuntimeFactory.Create(SchemaV5TestSnapshotFactory.Create());
            foreach (var system in runtime.Systems)
                await system.InitializeAsync(new GameContext("audio-harvest-event"), CancellationToken.None);
            try
            {
                var events = 0;
                var totalAmount = 0;
                runtime.PlayerGatherers.HarvestCompleted += value =>
                {
                    events++;
                    totalAmount += value.Amount;
                };
                for (var tick = 0; tick < 300 && events == 0; tick++)
                {
                    runtime.ResourceNodes.SimulateTick(tick);
                    runtime.PlayerGatherers.SimulateTick(tick);
                }

                Assert.That(events, Is.EqualTo(1));
                Assert.That(totalAmount, Is.GreaterThan(0));
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse())
                    await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public async Task DepletedGatherTarget_DoesNotEmitGatherCompleteEvent()
        {
            var runtime = MatchRuntimeFactory.Create(SchemaV5TestSnapshotFactory.Create());
            foreach (var system in runtime.Systems)
                await system.InitializeAsync(new GameContext("audio-zero-harvest"), CancellationToken.None);
            try
            {
                var events = 0;
                runtime.PlayerGatherers.HarvestCompleted += _ => events++;
                GathererSnapshot gathering = null;
                var tick = 0;
                for (; tick < 200 && gathering == null; tick++)
                {
                    runtime.ResourceNodes.SimulateTick(tick);
                    runtime.PlayerGatherers.SimulateTick(tick);
                    gathering = runtime.PlayerGatherers.GetSnapshot()
                        .FirstOrDefault(value => value.State == GathererState.Gathering);
                }

                Assert.That(gathering, Is.Not.Null);
                Assert.That(runtime.ResourceNodes.Harvest(gathering.TargetNodeId, gathering.TargetSpawnRevision,
                    gathering.ResourceId, int.MaxValue), Is.GreaterThan(0));
                Assert.That(runtime.ResourceNodes.Harvest(gathering.TargetNodeId, gathering.TargetSpawnRevision,
                    gathering.ResourceId, 1), Is.Zero);
                runtime.PlayerGatherers.SimulateTick(tick);

                Assert.That(events, Is.Zero);
            }
            finally
            {
                foreach (var system in runtime.Systems.Reverse())
                    await system.ShutdownAsync(CancellationToken.None);
            }
        }

        [Test]
        public void MusicState_BossOverridesRestoresCurrentPhaseAndResultLocksSelection()
        {
            var state = new GameplayMusicState(new BattlefieldId("battlefield.river-pass"),
                new MatchPhaseId("phase.development"));
            Assert.That(state.CurrentKey, Is.EqualTo(GameAudioKeys.RiverPassDevelopment));
            Assert.That(state.SetBossActive(true), Is.True);
            Assert.That(state.CurrentKey, Is.EqualTo(GameAudioKeys.StoneGolemBoss));
            Assert.That(state.SetPhase(new MatchPhaseId("phase.decisive")), Is.False);
            Assert.That(state.CurrentKey, Is.EqualTo(GameAudioKeys.StoneGolemBoss));
            Assert.That(state.SetBossActive(false), Is.True);
            Assert.That(state.CurrentKey, Is.EqualTo(GameAudioKeys.RiverPassDecisive));
            Assert.That(state.SetMatchResult(true), Is.True);
            Assert.That(state.CurrentKey, Is.EqualTo(GameAudioKeys.Victory));
            Assert.That(state.SetPhase(new MatchPhaseId("phase.contest")), Is.False);
            Assert.That(state.SetBossActive(true), Is.False);
            Assert.That(state.CurrentKey, Is.EqualTo(GameAudioKeys.Victory));
        }
    }
}
