using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.Systems;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class SystemHostTests
    {
        private sealed class RecordingSystemA : RecordingSystem
        {
            public RecordingSystemA(List<string> calls) : base(calls, "A") { }
        }

        private sealed class RecordingSystemB : RecordingSystem
        {
            public RecordingSystemB(List<string> calls, bool fail) : base(calls, "B", fail) { }
        }

        private abstract class RecordingSystem : GameSystemBase
        {
            private readonly List<string> _calls;
            private readonly string _id;
            private readonly bool _fail;

            protected RecordingSystem(List<string> calls, string id, bool fail = false)
                : base(SystemLifetime.Global)
            {
                _calls = calls;
                _id = id;
                _fail = fail;
            }

            protected override Task OnInitializeAsync(GameContext context, CancellationToken cancellationToken)
            {
                _calls.Add($"init:{_id}");
                if (_fail) throw new InvalidOperationException("Expected test failure.");
                return Task.CompletedTask;
            }

            protected override Task OnShutdownAsync(CancellationToken cancellationToken)
            {
                _calls.Add($"shutdown:{_id}");
                return Task.CompletedTask;
            }
        }

        [Test]
        public void Register_DuplicateType_Throws()
        {
            var host = new SystemHost();
            host.Register(new RecordingSystemA(new List<string>()));

            Assert.Throws<InvalidOperationException>(() =>
                host.Register(new RecordingSystemA(new List<string>())));
        }

        [Test]
        public void Initialize_WhenLaterSystemFails_RollsBackEarlierSystem()
        {
            var calls = new List<string>();
            var host = new SystemHost();
            host.Register(new RecordingSystemA(calls));
            host.Register(new RecordingSystemB(calls, true));

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await host.InitializeAsync(new GameContext("test"), SystemLifetime.Global, CancellationToken.None));

            Assert.That(calls, Is.EqualTo(new[] { "init:A", "init:B", "shutdown:A" }));
        }

        [Test]
        public async Task Shutdown_UsesReverseRegistrationOrder()
        {
            var calls = new List<string>();
            var host = new SystemHost();
            host.Register(new RecordingSystemA(calls));
            host.Register(new RecordingSystemB(calls, false));
            await host.InitializeAsync(new GameContext("test"), SystemLifetime.Global, CancellationToken.None);

            await host.ShutdownAsync(SystemLifetime.Global, CancellationToken.None);

            Assert.That(calls, Is.EqualTo(new[]
            {
                "init:A", "init:B", "shutdown:B", "shutdown:A"
            }));
        }
    }
}
