using System.IO;
using UnityEngine;

namespace Hollow.Data
{
    /// <summary>Where the game gets its starting profile: the shipped baseline, or a profile saved from the in-game panel.</summary>
    public static class BalanceProfileStore
    {
        public const string ActiveProfileFileName = "active.json";

        public static string ActiveProfilePath => Path.Combine(Application.persistentDataPath, "balance", ActiveProfileFileName);

        /// <summary>
        /// In the Editor the baseline asset is always used (the Editor window saves straight into it).
        /// In builds a profile saved from the F9 panel takes precedence when present and valid.
        /// </summary>
        public static BalanceService CreateService()
        {
            var baseline = BalanceService.LoadBaselineFromResources();
#if !UNITY_EDITOR
            if (File.Exists(ActiveProfilePath))
            {
                try
                {
                    var active = BalanceJson.FromJson(File.ReadAllText(ActiveProfilePath));
                    AddCompanionRegeneration(active, baseline);
                    return new BalanceService(baseline, active);
                }
                catch (System.Exception e) { Debug.LogWarning($"[Hollow] Ignoring saved profile: {e.Message}"); }
            }
#endif
            return new BalanceService(baseline);
        }

        /// <summary>Older profiles lack the newly added recovery fields; retain every other saved balance choice.</summary>
        public static void AddCompanionRegeneration(BalanceProfile profile, BalanceProfile baseline)
        {
            if (profile.Revision >= baseline.Revision) return;
            if (profile.Revision < 4)
            {
                profile.Player.RegenerationDelay = baseline.Player.RegenerationDelay;
                profile.Player.RegenerationRate = baseline.Player.RegenerationRate;
                profile.Player.DashMineDelay = baseline.Player.DashMineDelay;
                profile.Player.DashMineRadius = baseline.Player.DashMineRadius;
                if (profile.Skills.Find(s => s.Id == SkillId.DashMine) == null)
                    profile.Skills.Add(baseline.Skill(SkillId.DashMine));
                var phaseTwo = profile.CompanionFor(2);
                if (phaseTwo != null)
                {
                    phaseTwo.MaxHealth = baseline.CompanionFor(2).MaxHealth;
                    phaseTwo.DamageTakenMultiplier = baseline.CompanionFor(2).DamageTakenMultiplier;
                }
            }
            if (profile.Revision < 5)
                profile.Spawning.SafeIntervalSeconds = baseline.Spawning.SafeIntervalSeconds;
            if (profile.Revision < 6)
            {
                // The push pulse was removed from the game; stronger shot shake and the new melee, dash and death kicks.
                profile.Items.RemoveAll(i => i.Id == ItemId.PushPulse);
                profile.Feedback.ShakeOnShot = baseline.Feedback.ShakeOnShot;
                profile.Feedback.ShakeOnMelee = baseline.Feedback.ShakeOnMelee;
                profile.Feedback.ShakeOnDash = baseline.Feedback.ShakeOnDash;
                profile.Feedback.ShakeOnPlayerDeath = baseline.Feedback.ShakeOnPlayerDeath;
                profile.Feedback.DeathSparks = baseline.Feedback.DeathSparks;
                profile.Feedback.DeathGore = baseline.Feedback.DeathGore;
            }
            if (profile.Revision < 7)
            {
                // Upright hit capsules: shots aimed at a body's chest or head now connect.
                foreach (var e in profile.Enemies)
                    if (baseline.Enemies.Find(b => b.Id == e.Id) is { } be) e.HitHeight = be.HitHeight;
                foreach (var b in profile.Bosses)
                    if (baseline.Bosses.Find(x => x.Id == b.Id) is { } bb) b.HitHeight = bb.HitHeight;
            }
            if (profile.Revision < 8)
            {
                // D86 difficulty pass: boss armour, health and new moves; automatic rifles toned down with spread bloom.
                var fresh = BalanceJson.Clone(baseline);
                profile.Bosses = fresh.Bosses;
                profile.Weapons = fresh.Weapons;
                profile.BuildIndex();
            }
            if (profile.Revision < 3)
            {
                profile.Companion.RegenerationDelay = baseline.Companion.RegenerationDelay;
                profile.Companion.RegenerationRate = baseline.Companion.RegenerationRate;
            }
            profile.Revision = baseline.Revision;
        }
    }
}
