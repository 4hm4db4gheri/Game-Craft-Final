using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Kill rewards: XP is awarded automatically, coins fall on the ground (GDD §9). Producers pay only the
    /// part of their fixed pool that has not already been paid through their children (§6).
    /// </summary>
    public static class Rewards
    {
        public static void OnEnemyKilled(World w, EnemyState e)
        {
            var d = w.Profile.Enemy(e.Type);
            int xp = Mathf.Max(0, d.Xp - e.XpPaid);
            int coins = Mathf.Max(0, d.Coins - e.CoinsPaid);
            e.XpPaid += xp;
            e.CoinsPaid += coins;
            GrantXp(w, xp, e.Position);
            // Tutorial coins (the first group's) never expire (§6).
            bool tutorial = w.Tutorial == TutorialStage.FirstGroup || w.Tutorial == TutorialStage.Purchase;
            DropCoins(w, e.Position, coins, tutorial);
            w.Progress.Kills++;
        }

        public static void GrantXp(World w, int xp, Vector2 at)
        {
            if (xp <= 0) return;
            w.Progress.TotalXp += xp;
            w.Progress.XpTowardNext += xp;
            w.Emit(SimEventType.XpGained, PlayerState.EntityId, at, default, xp);
        }

        public static void DropCoins(World w, Vector2 at, int count, bool neverExpires = false)
        {
            float r = w.Profile.Economy.CoinScatterRadius;
            for (int i = 0; i < count; i++)
            {
                float angle = w.Rng.Range(0f, Mathf.PI * 2f);
                float dist = r * Mathf.Sqrt(w.Rng.Value);
                Vector2 pos = w.Arena.Resolve(at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist, 0.1f);
                w.Coins.Add(new CoinState
                {
                    Id = w.AllocateId(), Position = pos, PrevPosition = pos, Value = 1,
                    DroppedActiveTick = w.Clock.ActiveTicks, NeverExpires = neverExpires,
                });
                w.Progress.CoinsGenerated++;
                w.Emit(SimEventType.CoinDropped, 0, pos);
            }
        }
    }
}
