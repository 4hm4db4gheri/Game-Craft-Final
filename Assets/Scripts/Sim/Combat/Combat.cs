using UnityEngine;

namespace Hollow.Sim
{
    public enum DamageSource { Weapon, Melee, Dash, Explosion, Companion, Enemy }

    /// <summary>Damage rules shared by every attacker (armour, death, events).</summary>
    public static class Combat
    {
        /// <summary>
        /// Frontal armour (GDD §5, D23): hits arriving inside the enemy's front cone lose the reduction share.
        /// <paramref name="travelDir"/> is the direction the damage travels (attacker → target).
        /// </summary>
        public static float ArmorMultiplier(World w, EnemyState e, Vector2 travelDir, bool ignoresArmor)
        {
            if (ignoresArmor || !w.Profile.Enemy(e.Type).FrontalArmor || travelDir.sqrMagnitude < 1e-8f) return 1f;
            // The hit comes from the front when it travels against the facing direction.
            float angle = Vector2.Angle(e.Facing, -travelDir.normalized);
            return angle <= w.Profile.Combat.FrontalArmorArc * 0.5f ? 1f - w.Profile.Combat.FrontalArmorReduction : 1f;
        }

        /// <returns>The damage actually dealt.</returns>
        public static float DamageEnemy(World w, EnemyState e, float amount, DamageSource source, Vector2 travelDir, bool ignoresArmor = false)
        {
            if (!e.Alive || amount <= 0) return 0;
            if (e.Id == w.TutorialGrenadeEnemyId && w.Tutorial == TutorialStage.Grenade && source != DamageSource.Explosion) return 0;
            if (e.Id == w.TutorialGrenadeEnemyId && w.Tutorial == TutorialStage.GrenadeThrown && source != DamageSource.Explosion) return 0;
            if (e.Id == w.TutorialGrenadeEnemyId && w.Tutorial == TutorialStage.GrenadeThrown)
                amount = Mathf.Max(amount, e.Health);
            float dealt = amount * ArmorMultiplier(w, e, travelDir, ignoresArmor);
            // Attacking an enemy draws it away from the companion (§4).
            if (source == DamageSource.Weapon || source == DamageSource.Melee || source == DamageSource.Dash) e.Target = TargetKind.Player;
            e.Health -= dealt;
            w.Emit(SimEventType.EnemyHit, e.Id, e.Position, travelDir, dealt, (int)source);
            if (e.Health <= 0)
            {
                e.Health = 0;
                e.Alive = false;
                w.Emit(SimEventType.EnemyDied, e.Id, e.Position, travelDir, 0, (int)e.Type);
                Rewards.OnEnemyKilled(w, e);
                if (e.Id == w.TutorialGrenadeEnemyId && w.Tutorial == TutorialStage.GrenadeThrown)
                    w.Tutorial = TutorialStage.SecondGroup;
                if (source == DamageSource.Dash && w.Phase >= 2 && w.Player.SkillRank(Hollow.Data.SkillId.DashMine) > 0)
                {
                    int rank = w.Player.SkillRank(Hollow.Data.SkillId.DashMine);
                    var skill = w.Profile.Skill(Hollow.Data.SkillId.DashMine);
                    float damage = skill.Ranks[Mathf.Min(rank, skill.Ranks.Count) - 1];
                    HazardSystem.Create(w, HazardOwner.Player, e.Position, e.Position,
                        w.Profile.Player.DashMineRadius, damage, w.Profile.Player.DashMineDelay);
                }
            }
            else if (source == DamageSource.Weapon || source == DamageSource.Companion)
            {
                EnemySystem.Knockback(w, e, travelDir, w.Profile.Feedback.HitKnockback);
            }
            return dealt;
        }
    }
}
