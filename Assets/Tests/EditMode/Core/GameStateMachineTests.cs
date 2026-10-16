using System;
using System.Collections.Generic;
using System.Linq;
using Hollow.Core;
using NUnit.Framework;

namespace Hollow.Tests.Core
{
    public class GameStateMachineTests
    {
        static readonly GameState[] AllStates = (GameState[])Enum.GetValues(typeof(GameState));

        // The specification, written out independently from the implementation table.
        static readonly HashSet<(GameState, GameState)> Legal = new()
        {
            (GameState.Boot, GameState.MainMenu),
            (GameState.MainMenu, GameState.Loading),
            (GameState.Loading, GameState.Playing), (GameState.Loading, GameState.XPChoice), (GameState.Loading, GameState.BossPrep),
            (GameState.Loading, GameState.Scene), (GameState.Loading, GameState.Ending), (GameState.Loading, GameState.MainMenu),
            (GameState.Playing, GameState.XPChoice), (GameState.Playing, GameState.Paused), (GameState.Playing, GameState.Scene),
            (GameState.Playing, GameState.BossPrep), (GameState.Playing, GameState.Failure), (GameState.Playing, GameState.PhaseTransition),
            (GameState.Playing, GameState.Ending),
            (GameState.XPChoice, GameState.Playing), (GameState.XPChoice, GameState.Paused),
            (GameState.Paused, GameState.Playing), (GameState.Paused, GameState.XPChoice), (GameState.Paused, GameState.BossPrep),
            (GameState.Paused, GameState.Loading), (GameState.Paused, GameState.MainMenu),
            (GameState.Scene, GameState.Playing), (GameState.Scene, GameState.PhaseTransition), (GameState.Scene, GameState.Ending),
            (GameState.BossPrep, GameState.Playing), (GameState.BossPrep, GameState.Scene), (GameState.BossPrep, GameState.Paused),
            (GameState.Failure, GameState.Loading), (GameState.Failure, GameState.MainMenu),
            (GameState.PhaseTransition, GameState.Playing), (GameState.PhaseTransition, GameState.Scene),
            (GameState.Ending, GameState.MainMenu), (GameState.Ending, GameState.Loading),
        };

        public static IEnumerable<TestCaseData> AllPairs() =>
            from a in AllStates from b in AllStates select new TestCaseData(a, b).SetName($"{a} -> {b}");

        [TestCaseSource(nameof(AllPairs))]
        public void EveryPairMatchesTheSpecification(GameState from, GameState to)
        {
            Assert.AreEqual(Legal.Contains((from, to)), GameStateMachine.IsAllowed(from, to));
        }

        [Test]
        public void IllegalTransitionThrowsAndKeepsState()
        {
            var sm = new GameStateMachine(GameState.MainMenu);
            Assert.Throws<InvalidOperationException>(() => sm.Transition(GameState.Playing));
            Assert.AreEqual(GameState.MainMenu, sm.Current);
            Assert.IsFalse(sm.TryTransition(GameState.Ending));
        }

        [TestCase(GameState.Playing)]
        [TestCase(GameState.XPChoice)]
        [TestCase(GameState.BossPrep)]
        public void PauseResumesOnlyIntoTheInterruptedState(GameState interrupted)
        {
            var sm = new GameStateMachine(GameState.Loading);
            sm.Transition(interrupted);
            sm.Transition(GameState.Paused);
            Assert.AreEqual(interrupted, sm.ResumeState);

            foreach (var other in new[] { GameState.Playing, GameState.XPChoice, GameState.BossPrep }.Where(s => s != interrupted))
                Assert.IsFalse(sm.CanTransition(other), $"Paused must not resume into {other}");

            Assert.IsTrue(sm.Resume());
            Assert.AreEqual(interrupted, sm.Current);
        }

        [Test]
        public void PausedCanAlwaysLeaveToLoadOrMenu()
        {
            var sm = new GameStateMachine(GameState.Loading);
            sm.Transition(GameState.Playing);
            sm.Transition(GameState.Paused);
            Assert.IsTrue(sm.CanTransition(GameState.Loading));
            Assert.IsTrue(sm.CanTransition(GameState.MainMenu));
        }

        [Test]
        public void ChangedEventReportsBothStates()
        {
            var sm = new GameStateMachine(GameState.Boot);
            (GameState, GameState) seen = default;
            sm.Changed += (a, b) => seen = (a, b);
            sm.Transition(GameState.MainMenu);
            Assert.AreEqual((GameState.Boot, GameState.MainMenu), seen);
        }
    }
}
