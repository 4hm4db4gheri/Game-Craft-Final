using System;

namespace Hollow.Core
{
    /// <summary>
    /// Advances the simulation in fixed steps, only in states where it runs (<see cref="ClockPolicy"/>).
    /// Real time keeps flowing outside it, so UI timers are unaffected by pauses.
    /// </summary>
    public sealed class SimDriver
    {
        readonly GameStateMachine states;
        readonly FixedStepAccumulator accumulator;
        readonly Action step;

        public SimDriver(GameStateMachine states, Action step, int maxStepsPerFrame = 5)
        {
            this.states = states;
            this.step = step;
            accumulator = new FixedStepAccumulator(maxStepsPerFrame);
        }

        public float InterpolationAlpha => accumulator.Alpha;

        /// <returns>The number of simulation steps executed this frame.</returns>
        public int Update(double realDelta)
        {
            if (!ClockPolicy.SimRuns(states.Current))
            {
                accumulator.Reset();
                return 0;
            }
            int steps = accumulator.Accumulate(realDelta);
            for (int i = 0; i < steps; i++)
            {
                step();
                // A step may open the XP card or end the encounter; stop stepping immediately.
                if (!ClockPolicy.SimRuns(states.Current)) { accumulator.Reset(); return i + 1; }
            }
            return steps;
        }
    }
}
