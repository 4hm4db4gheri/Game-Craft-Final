using Hollow.Data;
using UnityEngine;

namespace Hollow.Sim
{
    public enum FailureReason { None, PlayerDied, CompanionDied }

    /// <summary>The Operator while he is still the companion (phases 1 and 2).</summary>
    public sealed class CompanionState
    {
        public const int EntityId = -1;

        public bool Present;
        public Vector2 Position;
        public Vector2 PrevPosition;
        public Vector2 Velocity;
        public Vector2 Facing = Vector2.down;
        public float Health;
        public float MaxHealth;
        public bool Dead;
        public double NextShotTime;
        public long LastHitTick = long.MinValue / 2;
        /// <summary>Enemies currently targeting the companion (recomputed every tick).</summary>
        public int Attackers;
    }

    /// <summary>
    /// Companion behaviour (GDD §4): follows at a limited distance beside/behind the player without blocking
    /// the player's path, fires limited automatic shots (kills still pay the player), takes damage, and its
    /// death fails the encounter in phases one and two. Phase-two entry restores it to the lower maximum.
    /// </summary>
    public static class CompanionSystem
    {
        public static bool Active(World w) => w.Companion.Present && !w.Companion.Dead;

        public static void SpawnForPhase(World w, Vector2 position)
        {
            var c = w.Companion;
            var phase = w.Profile.CompanionFor(w.Phase);
            if (phase == null) { c.Present = false; return; }
            c.Present = true;
            c.Dead = false;
            c.Position = c.PrevPosition = position;
            c.MaxHealth = phase.MaxHealth;
            c.Health = phase.MaxHealth;
            c.Velocity = Vector2.zero;
        }

        public static void Step(World w)
        {
            var c = w.Companion;
            c.PrevPosition = c.Position;
            if (!Active(w)) return;
            var data = w.Profile.Companion;
            var phase = w.Profile.CompanionFor(w.Phase);
            if (phase == null) return;

            Follow(w, c, data);
            Shoot(w, c, data, phase);

            c.Attackers = 0;
            foreach (var e in w.Enemies)
                if (e.IsActive && e.Target == TargetKind.Companion) c.Attackers++;
        }

        static void Follow(World w, CompanionState c, CompanionData data)
        {
            var p = w.Player;
            // Stand behind and to the side of the player's last movement direction, out of the path ahead.
            Vector2 back = -p.LastMoveDir;
            Vector2 side = new(-p.LastMoveDir.y, p.LastMoveDir.x);
            float mid = (data.FollowDistanceMin + data.FollowDistanceMax) * 0.5f;
            Vector2 slot = p.Position + SimMath.SafeNormal(back * (1f - data.FollowSideBias) + side * data.FollowSideBias, back) * mid;
            if (w.Arena.CircleOverlaps(slot, data.BodyRadius)) slot = p.Position + back * mid;

            float toPlayer = Vector2.Distance(c.Position, p.Position);
            float toSlot = Vector2.Distance(c.Position, slot);
            Vector2 desired = Vector2.zero;
            if (toPlayer > data.FollowDistanceMin || toSlot > data.FollowArriveRadius)
            {
                Vector2 dir = w.Arena.LineOfSight(c.Position, slot)
                    ? SimMath.SafeNormal(slot - c.Position, Vector2.zero)
                    : w.PlayerField.Direction(c.Position, p.Position);
                float speed = data.MoveSpeed * Mathf.Clamp01(toSlot / Mathf.Max(0.01f, data.FollowArriveRadius * 2f));
                if (toPlayer > data.FollowDistanceMax) speed = Mathf.Max(data.MoveSpeed, p.Velocity.magnitude * data.CatchUpSpeedFactor);
                desired = dir * speed;
            }

            // Never stand in the player's way: if inside the player's body + a margin ahead of their motion, step aside.
            Vector2 ahead = p.Velocity.sqrMagnitude > 0.01f ? p.Velocity.normalized : Vector2.zero;
            Vector2 rel = c.Position - p.Position;
            if (ahead != Vector2.zero && Vector2.Dot(rel, ahead) > 0 && Mathf.Abs(Cross(ahead, rel)) < data.BodyRadius + w.Profile.Player.BodyRadius + data.PathClearance)
                desired += new Vector2(-ahead.y, ahead.x) * Mathf.Sign(Cross(ahead, rel) == 0 ? 1 : Cross(ahead, rel)) * data.MoveSpeed;

            float cap = Mathf.Max(data.MoveSpeed, p.Velocity.magnitude * data.CatchUpSpeedFactor);
            c.Velocity = Vector2.ClampMagnitude(desired, cap);
            if (c.Velocity.sqrMagnitude > 0.01f) c.Facing = c.Velocity.normalized;
            c.Position = w.Arena.MoveCircle(c.Position, c.Velocity * Core.SimClock.Dt, data.BodyRadius);
        }

        static float Cross(Vector2 a, Vector2 b) => a.x * b.y - a.y * b.x;

        static void Shoot(World w, CompanionState c, CompanionData data, CompanionPhase phase)
        {
            if (w.Clock.Seconds < c.NextShotTime) return;
            EnemyState best = null;
            float bestDist = data.FireRange;
            foreach (var e in w.Enemies)
            {
                if (!e.IsActive) continue;
                float d = Vector2.Distance(c.Position, e.Position);
                if (d < bestDist && w.Arena.LineOfSight(c.Position, e.Position)) { best = e; bestDist = d; }
            }
            Vector2 targetPos;
            var boss = w.Boss;
            if (best != null) targetPos = best.Position;
            else if (boss.Active && !boss.Dead && Vector2.Distance(c.Position, boss.Position) < data.FireRange && w.Arena.LineOfSight(c.Position, boss.Position)) targetPos = boss.Position;
            else return;

            Vector2 dir = SimMath.SafeNormal(targetPos - c.Position, c.Facing);
            c.Facing = dir;
            w.Projectiles.Add(new ProjectileState
            {
                Id = w.AllocateId(), Owner = ProjectileOwner.Companion, SourceId = CompanionState.EntityId,
                Position = c.Position, PrevPosition = c.Position, Origin = c.Position,
                Velocity = dir * data.ProjectileSpeed, Damage = phase.ShotDamage, Range = data.ProjectileRange,
                Radius = w.Profile.Combat.PlayerProjectileRadius, TargetsLeft = 1,
            });
            c.NextShotTime = w.Clock.Seconds + phase.ShotInterval;
            w.Emit(SimEventType.ShotFired, CompanionState.EntityId, c.Position, dir, 1, -1);
        }

        public static bool Damage(World w, float amount, Vector2 travelDir)
        {
            var c = w.Companion;
            if (!Active(w) || amount <= 0) return false;
            c.Health -= amount;
            c.LastHitTick = w.Clock.Tick;
            w.Emit(SimEventType.CompanionHit, CompanionState.EntityId, c.Position, travelDir, amount);
            if (c.Health <= 0)
            {
                c.Health = 0;
                c.Dead = true;
                w.Emit(SimEventType.CompanionDied, CompanionState.EntityId, c.Position, travelDir);
                if (w.Failure == FailureReason.None) w.Failure = FailureReason.CompanionDied;
            }
            return true;
        }

        public static void Heal(World w, float amount)
        {
            var c = w.Companion;
            if (!Active(w)) return;
            c.Health = Mathf.Min(c.MaxHealth, c.Health + amount);
        }
    }
}
