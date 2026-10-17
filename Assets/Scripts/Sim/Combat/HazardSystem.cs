using System.Collections.Generic;
using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum HazardOwner { Enemy, Player }

    /// <summary>
    /// A marked ground zone that detonates later: grenadier landing zones and boss blasts (enemy-owned,
    /// hurt the player and companion), player grenades (player-owned, hurt enemies only, never the player
    /// or companion, §9). The marked zone is visible for the whole delay, so it is always telegraphed.
    /// </summary>
    public sealed class HazardState
    {
        public int Id;
        public HazardOwner Owner;
        public Vector2 Position;
        public float Radius;
        public float Damage;
        public long CreatedTick;
        public long DetonateTick;
        public EnemyId SourceType;
        public int SourceId;
        /// <summary>Optional flight: the visual travels from here to Position until it lands.</summary>
        public Vector2 LaunchFrom;
        public long LandTick;
        /// <summary>Cleared by a boss death; removed on the next step without going off.</summary>
        public bool Cancelled;
    }

    public static class HazardSystem
    {
        static readonly List<EnemyState> Candidates = new(32);

        public static HazardState Create(World w, HazardOwner owner, Vector2 from, Vector2 at, float radius, float damage, float delay, int sourceId = 0, EnemyId sourceType = default, float flight = 0f)
        {
            var h = new HazardState
            {
                Id = w.AllocateId(), Owner = owner, LaunchFrom = from, Position = at, Radius = radius, Damage = damage,
                CreatedTick = w.Clock.Tick, DetonateTick = w.Clock.Tick + System.Math.Max(1, w.Ticks(delay)),
                LandTick = w.Clock.Tick + w.Ticks(flight), SourceId = sourceId, SourceType = sourceType,
            };
            w.Hazards.Add(h);
            return h;
        }

        public static void Step(World w)
        {
            w.Hazards.RemoveAll(h => h.Cancelled);
            // Iterate over a snapshot: a detonation can end a boss fight, which cancels the remaining hazards.
            int count = w.Hazards.Count;
            for (int i = 0; i < count && i < w.Hazards.Count; i++)
            {
                var h = w.Hazards[i];
                if (h.Cancelled || w.Clock.Tick < h.DetonateTick) continue;
                h.Cancelled = true;
                Detonate(w, h);
            }
            w.Hazards.RemoveAll(h => h.Cancelled);
        }

        public static void Detonate(World w, HazardState h)
        {
            w.Emit(SimEventType.Explosion, h.Id, h.Position, default, h.Radius, (int)h.Owner);
            if (h.Owner == HazardOwner.Enemy)
            {
                if (!w.Player.Dead && Vector2.Distance(w.Player.Position, h.Position) <= h.Radius + w.Profile.Player.BodyRadius)
                    PlayerSystem.Damage(w, h.Damage, SimMath.SafeNormal(w.Player.Position - h.Position, Vector2.up));
                if (CompanionSystem.Active(w) && Vector2.Distance(w.Companion.Position, h.Position) <= h.Radius + w.Profile.Companion.BodyRadius)
                    CompanionSystem.Damage(w, h.Damage, SimMath.SafeNormal(w.Companion.Position - h.Position, Vector2.up));
                return;
            }

            var boss = w.Boss;
            if (boss.Active && !boss.Dead && Vector2.Distance(boss.Position, h.Position) <= h.Radius + boss.Radius)
                BossSystem.Damage(w, h.Damage, DamageSource.Explosion, SimMath.SafeNormal(boss.Position - h.Position, Vector2.up));
            w.Crowd.Rebuild(w.Enemies);
            w.Crowd.Query(h.Position, h.Radius + ProjectileSystem.MaxEnemyRadius(w), Candidates);
            foreach (var e in Candidates)
            {
                if (!e.Alive || Vector2.Distance(e.Position, h.Position) > h.Radius + e.Radius) continue;
                Combat.DamageEnemy(w, e, h.Damage, DamageSource.Explosion, SimMath.SafeNormal(e.Position - h.Position, Vector2.up));
            }
            if (w.Tutorial == TutorialStage.GrenadeThrown)
            {
                w.Inventory.Add(ItemId.Grenade, 1);
                w.Tutorial = TutorialStage.Grenade;
            }
        }
    }
}
