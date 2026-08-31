using System;
using System.Threading;
using System.Threading.Tasks;
using FortressFrontier.Core.StateMachine;
using NUnit.Framework;

namespace FortressFrontier.Tests.EditMode
{
    public sealed class AsyncStateMachineTests
    {
        private enum TestStateId
        {
            First,
            Second
        }

        private sealed class TestState : IAsyncState<TestStateId>
        {
            private readonly bool _failOnEnter;

            public TestState(TestStateId id, bool failOnEnter = false)
            {
                Id = id;
                _failOnEnter = failOnEnter;
            }

            public TestStateId Id { get; }
            public int EnterCount { get; private set; }
            public int ExitCount { get; private set; }

            public Task EnterAsync(TestStateId? previousState, CancellationToken cancellationToken)
            {
                if (_failOnEnter)
                {
                    throw new InvalidOperationException("Expected test failure.");
                }

                EnterCount++;
                return Task.CompletedTask;
            }

            public Task ExitAsync(TestStateId nextState, CancellationToken cancellationToken)
            {
                ExitCount++;
                return Task.CompletedTask;
            }
        }

        [Test]
        public async Task Transition_EntersNextBeforeExitingPrevious()
        {
            var first = new TestState(TestStateId.First);
            var second = new TestState(TestStateId.Second);
            var machine = new AsyncStateMachine<TestStateId>();
            machine.Register(first);
            machine.Register(second);

            await machine.TransitionAsync(TestStateId.First, CancellationToken.None);
            await machine.TransitionAsync(TestStateId.Second, CancellationToken.None);

            Assert.That(machine.CurrentState, Is.EqualTo(TestStateId.Second));
            Assert.That(first.ExitCount, Is.EqualTo(1));
            Assert.That(second.EnterCount, Is.EqualTo(1));
        }

        [Test]
        public void Transition_WhenNextEnterFails_PreservesCurrentState()
        {
            var machine = new AsyncStateMachine<TestStateId>();
            machine.Register(new TestState(TestStateId.First));
            machine.Register(new TestState(TestStateId.Second, true));
            machine.TransitionAsync(TestStateId.First, CancellationToken.None).GetAwaiter().GetResult();

            Assert.ThrowsAsync<InvalidOperationException>(async () =>
                await machine.TransitionAsync(TestStateId.Second, CancellationToken.None));
            Assert.That(machine.CurrentState, Is.EqualTo(TestStateId.First));
        }

        [Test]
        public async Task Transition_ToCurrentState_IsNoOp()
        {
            var state = new TestState(TestStateId.First);
            var machine = new AsyncStateMachine<TestStateId>();
            machine.Register(state);

            await machine.TransitionAsync(TestStateId.First, CancellationToken.None);
            await machine.TransitionAsync(TestStateId.First, CancellationToken.None);

            Assert.That(state.EnterCount, Is.EqualTo(1));
            Assert.That(state.ExitCount, Is.Zero);
        }
    }
}
