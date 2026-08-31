using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Saving;
using FortressFrontier.Infrastructure.Saving;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class SaveCoordinatorTests
    {
        [Serializable]
        private sealed class TestState
        {
            public int Value;
        }

        private sealed class Participant : ISaveParticipant
        {
            public SaveFileKind FileKind => SaveFileKind.Profile;
            public string SectionKey => "test";
            public int SectionVersion => 1;
            public Type StateType => typeof(TestState);
            public int Value { get; set; }
            public object CaptureState() => new TestState { Value = Value };
            public object CreateDefaultState() => new TestState();
            public void RestoreState(object state, int storedVersion) => Value = ((TestState)state).Value;
        }

        private string _directory;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), "FortressFrontierTests", Guid.NewGuid().ToString("N"));
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, true);
            }
        }

        [Test]
        public async Task SaveAndLoad_RestoresParticipantState()
        {
            var participant = new Participant { Value = 42 };
            var coordinator = new SaveCoordinator(_directory, "test", () => new[] { participant });
            await coordinator.SaveAsync(SaveFileKind.Profile, CancellationToken.None);
            participant.Value = 0;

            var result = await coordinator.LoadAsync(SaveFileKind.Profile, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(participant.Value, Is.EqualTo(42));
        }

        [Test]
        public async Task Load_WhenFilesDoNotExist_RestoresDefaultsWithoutError()
        {
            var participant = new Participant { Value = 99 };
            var coordinator = new SaveCoordinator(_directory, "test", () => new[] { participant });

            var result = await coordinator.LoadAsync(SaveFileKind.Profile, CancellationToken.None);

            Assert.That(result.Succeeded, Is.True);
            Assert.That(participant.Value, Is.Zero);
        }
    }
}
