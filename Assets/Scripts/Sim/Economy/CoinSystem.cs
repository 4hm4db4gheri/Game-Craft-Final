using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Ground coins (GDD §9): attracted inside the magnet radius (a bigger radius never raises their value),
    /// collected on touch, optional lifetime counted in active gameplay seconds (frozen in safe intervals,
    /// XP pauses, scenes and popups; still running while shopping), tutorial coins exempt.
    /// </summary>
    public static class CoinSystem
    {
        public static void Step(World w)
        {
            var p = w.Player;
            var eco = w.Profile.Economy;
            float magnet = PlayerStats.MagnetRadius(w);
            float collect = w.Profile.Player.BodyRadius;
            float speed = w.Profile.Player.CoinPullSpeed;
            float dt = Core.SimClock.Dt;
            long lifetimeTicks = w.Ticks(eco.CoinLifetime);

            foreach (var c in w.Coins)
            {
                if (!c.Alive) continue;
                c.PrevPosition = c.Position;

                if (eco.CoinLifetimeEnabled && !c.NeverExpires && w.Clock.ActiveTicks - c.DroppedActiveTick >= lifetimeTicks)
                {
                    c.Alive = false;
                    continue;
                }
                if (p.Dead) continue;

                float dist = Vector2.Distance(c.Position, p.Position);
                if (dist <= magnet) c.Magnetized = true;
                if (c.Magnetized) c.Position = Vector2.MoveTowards(c.Position, p.Position, speed * dt);
                if (Vector2.Distance(c.Position, p.Position) <= collect) Collect(w, c);
            }
            w.Coins.RemoveAll(c => !c.Alive);
        }

        public static void Collect(World w, CoinState c)
        {
            if (!c.Alive) return; // a coin is paid once, even if two paths reach it in the same tick
            c.Alive = false;
            w.Progress.Wallet += c.Value;
            w.Progress.CoinsCollected += c.Value;
            w.Emit(SimEventType.CoinCollected, c.Id, c.Position, default, c.Value);
        }

        /// <summary>Remaining lifetime in active seconds, or +inf when the coin cannot expire.</summary>
        public static float RemainingLifetime(World w, CoinState c)
        {
            var eco = w.Profile.Economy;
            if (!eco.CoinLifetimeEnabled || c.NeverExpires) return float.PositiveInfinity;
            long left = w.Ticks(eco.CoinLifetime) - (w.Clock.ActiveTicks - c.DroppedActiveTick);
            return Mathf.Max(0, left) * Core.SimClock.Dt;
        }

        /// <summary>Presentation helper: blink during the last CoinBlinkTime seconds.</summary>
        public static bool IsBlinkingOff(World w, CoinState c, float realTime)
        {
            float remaining = RemainingLifetime(w, c);
            return remaining <= w.Profile.Economy.CoinBlinkTime && Mathf.Repeat(realTime * 6f, 1f) < 0.35f;
        }
    }
}
