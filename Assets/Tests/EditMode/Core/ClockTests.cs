using Hollow.Core;
using NUnit.Framework;

namespace Hollow.Tests.Core
{
    public class ClockTests
    {
        [Test]
        public void ActiveTimersFreezeWhilePausedButRealTimeKeepsRunning()
        {
            var sm = new GameStateMachine(GameState.Loading);
            sm.Transition(GameState.Playing);
            var clock = new SimClock();
            var driver = new SimDriver(sm, () => clock.Step(true));
            var timer = ActiveTimer.Start(clock, 2f);
            double realUiTime = 0;

            void Frame(double dt) { realUiTime += dt; driver.Update(dt); }

            for (int i = 0; i < 60; i++) Frame(1 / 60.0);   // 1 s of play
            Assert.AreEqual(1f, timer.Remaining(clock), 0.02f);

            sm.Transition(GameState.Paused);
            for (int i = 0; i < 600; i++) Frame(1 / 60.0);  // 10 s paused
            Assert.AreEqual(1f, timer.Remaining(clock), 0.02f, "active timer must not advance while paused");
            Assert.AreEqual(11.0, realUiTime, 1e-6, "real-time UI clock keeps running");

            sm.Resume();
            for (int i = 0; i < 70; i++) Frame(1 / 60.0);
            Assert.IsTrue(timer.IsDone(clock));
        }

        [TestCase(GameState.XPChoice)]
        [TestCase(GameState.Scene)]
        [TestCase(GameState.BossPrep)]
        [TestCase(GameState.PhaseTransition)]
        public void SimDoesNotRunOutsidePlaying(GameState frozen)
        {
            var sm = new GameStateMachine(GameState.Loading);
            sm.Transition(GameState.Playing);
            sm.Transition(frozen);
            var clock = new SimClock();
            var driver = new SimDriver(sm, () => clock.Step(true));
            for (int i = 0; i < 120; i++) driver.Update(1 / 60.0);
            Assert.AreEqual(0, clock.Tick);
        }

        [Test]
        public void SafeIntervalFreezesActiveTimeButNotSimTime()
        {
            var clock = new SimClock();
            var active = ActiveTimer.Start(clock, 1f);
            var sim = SimTimer.Start(clock, 1f);
            for (int i = 0; i < 60; i++) clock.Step(activeTimeRunning: false);
            Assert.IsTrue(sim.IsDone(clock));
            Assert.IsFalse(active.IsDone(clock));
        }

        [Test]
        public void SimStopsStepsTheMomentStateLeavesPlaying()
        {
            var sm = new GameStateMachine(GameState.Loading);
            sm.Transition(GameState.Playing);
            int steps = 0;
            var driver = new SimDriver(sm, () => { steps++; if (steps == 2) sm.Transition(GameState.XPChoice); });
            int ran = driver.Update(5 / 60.0 + 1e-9);
            Assert.AreEqual(2, ran);
            Assert.AreEqual(2, steps);
        }

        [Test]
        public void AccumulatorDropsBacklogAfterAHitch()
        {
            var acc = new FixedStepAccumulator(maxStepsPerFrame: 5);
            Assert.AreEqual(5, acc.Accumulate(1.0));
            Assert.AreEqual(0, acc.Accumulate(0.001));
        }
    }
}
