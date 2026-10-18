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
            // The tutorial's practice grenade target is outside the wave's reward budget: a kill, but no XP or coins.
            if (e.Id == w.TutorialGrenadeEnemyId) { w.Progress.Kills++; return; }
            var d = w.Profile.Enemy(e.Type);
            // Children pay their assigned share of the parent's pool; producers pay what was never assigned (§6).
            int xp = d.IsChild ? e.ShareXp - e.XpPaid : d.Xp - e.XpAllocated - e.XpPaid;
            int coins = d.IsChild ? e.ShareCoins - e.CoinsPaid : d.Coins - e.CoinsAllocated - e.CoinsPaid;
            xp = Mathf.Max(0, xp);
            coins = Mathf.Max(0, coins);
            e.XpPaid += xp;
            e.CoinsPaid += coins;
            if (e.EncounterGroup == BossState.EncounterGroup) { w.Boss.PoolXpPaid += xp; w.Boss.PoolCoinsPaid += coins; }
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
                Vector2 pos = w.Arena.NearestFree(at + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * dist, w.Profile.Economy.CoinRadius);
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
