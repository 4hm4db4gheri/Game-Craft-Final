using System;
using Hollow.Data;

namespace Hollow.Sim
{
    /// <summary>
    /// Player values after general XP skills. Rank values are absolute and relative to the baseline,
    /// never compounded (GDD §9); the base comes from the player data.
    /// </summary>
    public static class PlayerStats
    {
        public static float MoveSpeed(World w) => Skill(w, SkillId.MoveSpeed, w.Profile.Player.MoveSpeed);
        public static float DashCooldown(World w) => Skill(w, SkillId.DashCooldown, w.Profile.Player.DashCooldown);
        public static float DashDamage(World w) => Skill(w, SkillId.DashDamage, w.Profile.Player.DashDamage);
        public static float MaxHealth(World w) => Skill(w, SkillId.MaxHealth, w.Profile.Player.MaxHealth);
        public static float MagnetRadius(World w) => Skill(w, SkillId.MagnetRadius, w.Profile.Player.MagnetRadius);
        public static float WeaponDamageMultiplier(World w) => Skill(w, SkillId.WeaponDamage, 1f);
        public static float ReloadMultiplier(World w) => Skill(w, SkillId.ReloadMultiplier, 1f);

        public static float Skill(World w, SkillId id, float baseValue)
        {
            int rank = w.Player.SkillRank(id);
            if (rank <= 0) return baseValue;
            var ranks = w.Profile.Skill(id).Ranks;
            return ranks.Count == 0 ? baseValue : ranks[Math.Min(rank, ranks.Count) - 1];
        }

        public static bool IsInvulnerable(World w) =>
            w.Player.Dashing || w.Clock.Tick < w.Player.InvulnerableUntilTick;
    }
}
