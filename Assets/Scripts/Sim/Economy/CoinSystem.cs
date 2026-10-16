using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>Ground coins: attracted inside the magnet radius (never worth more), collected on touch (§9).</summary>
    public static class CoinSystem
    {
        public static void Step(World w)
        {
            var p = w.Player;
            if (p.Dead) return;
            float magnet = PlayerStats.MagnetRadius(w);
            float collect = w.Profile.Player.BodyRadius;
            float speed = w.Profile.Player.CoinPullSpeed;
            float dt = Core.SimClock.Dt;

            foreach (var c in w.Coins)
            {
                if (!c.Alive) continue;
                c.PrevPosition = c.Position;
                float dist = Vector2.Distance(c.Position, p.Position);
                if (dist <= magnet) c.Magnetized = true;
                if (c.Magnetized) c.Position = Vector2.MoveTowards(c.Position, p.Position, speed * dt);
                if (Vector2.Distance(c.Position, p.Position) <= collect)
                {
                    c.Alive = false;
                    w.Progress.Wallet += c.Value;
                    w.Progress.CoinsCollected += c.Value;
                    w.Emit(SimEventType.CoinCollected, c.Id, c.Position, default, c.Value);
                }
            }
            w.Coins.RemoveAll(c => !c.Alive);
        }
    }
}
