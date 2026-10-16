using System;

namespace Hollow.Core
{
    /// <summary>
    /// Simulation time in integer ticks. <see cref="Tick"/> counts every simulated step;
    /// <see cref="ActiveTicks"/> counts only "active gameplay seconds" (frozen during safe intervals).
    /// Real time is never stored here: UI uses unscaled real time directly.
    /// </summary>
    [Serializable]
    public sealed class SimClock
    {
        public const int TicksPerSecond = 60;
        public const float Dt = 1f / TicksPerSecond;

        public long Tick;
        public long ActiveTicks;

        public double Seconds => Tick / (double)TicksPerSecond;
        public double ActiveSeconds => ActiveTicks / (double)TicksPerSecond;

        public void Step(bool activeTimeRunning)
        {
            Tick++;
            if (activeTimeRunning) ActiveTicks++;
        }

        public static long ToTicks(float seconds) => (long)Math.Round(seconds * TicksPerSecond);
    }

    /// <summary>A deadline on the active-seconds clock; plain data so it serialises as-is.</summary>
    [Serializable]
    public struct ActiveTimer
    {
        public long EndTick;
        public bool Running;

        public static ActiveTimer Start(SimClock clock, float seconds) =>
            new() { EndTick = clock.ActiveTicks + SimClock.ToTicks(seconds), Running = true };

        public bool IsDone(SimClock clock) => Running && clock.ActiveTicks >= EndTick;
        public float Remaining(SimClock clock) => Running ? Math.Max(0, EndTick - clock.ActiveTicks) * SimClock.Dt : 0f;
    }

    /// <summary>A deadline on the plain simulation clock (runs in safe intervals too, never in pauses).</summary>
    [Serializable]
    public struct SimTimer
    {
        public long EndTick;
        public bool Running;

        public static SimTimer Start(SimClock clock, float seconds) =>
            new() { EndTick = clock.Tick + SimClock.ToTicks(seconds), Running = true };

        public bool IsDone(SimClock clock) => Running && clock.Tick >= EndTick;
        public float Remaining(SimClock clock) => Running ? Math.Max(0, EndTick - clock.Tick) * SimClock.Dt : 0f;
    }

    /// <summary>Which game states advance the simulation. The sim decides itself whether active time runs.</summary>
    public static class ClockPolicy
    {
        public static bool SimRuns(GameState state) => state == GameState.Playing;
    }

    /// <summary>Turns variable real frame time into a whole number of fixed simulation steps.</summary>
    public sealed class FixedStepAccumulator
    {
        readonly int maxStepsPerFrame;
        double accumulated;

        public FixedStepAccumulator(int maxStepsPerFrame = 5)
        {
            this.maxStepsPerFrame = maxStepsPerFrame;
        }

        /// <summary>Fraction of the next step already elapsed; used to interpolate rendering.</summary>
        public float Alpha => (float)(accumulated / SimClock.Dt);

        public int Accumulate(double realDelta)
        {
            accumulated += Math.Max(0, realDelta);
            int steps = (int)(accumulated / SimClock.Dt);
            if (steps > maxStepsPerFrame)
            {
                // Drop the backlog after a hitch instead of spiralling.
                steps = maxStepsPerFrame;
                accumulated = 0;
            }
            else
            {
                accumulated -= steps * SimClock.Dt;
            }
            return steps;
        }

        public void Reset() => accumulated = 0;
    }
}
