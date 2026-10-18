using Hollow.Core.Input;
using UnityEngine;

namespace Hollow.Sim
{
    /// <summary>
    /// Player movement, dash and health (GDD §4). Movement is independent of aim and never locked by
    /// firing, reloading or being hit; diagonal input is clamped so it is never faster.
    /// </summary>
    public static class PlayerSystem
    {
        public static void Spawn(World w, Vector2 position)
        {
            var p = w.Player;
            p.Position = p.PrevPosition = position;
            p.Velocity = Vector2.zero;
            p.Health = PlayerStats.MaxHealth(w);
            p.LastHitTick = long.MinValue / 2;
            p.Dead = false;
            p.Dashing = false;
        }

        public static void Step(World w, in PlayerCommand cmd)
        {
            var p = w.Player;
            p.PrevPosition = p.Position;
            p.Aim = cmd.AimWorld;
            if (p.Dead) { p.Velocity = Vector2.zero; return; }
            if (p.Health < PlayerStats.MaxHealth(w) && w.Clock.Tick - p.LastHitTick >= w.Ticks(w.Profile.Player.RegenerationDelay))
                p.Health = Mathf.Min(PlayerStats.MaxHealth(w), p.Health + w.Profile.Player.RegenerationRate * Core.SimClock.Dt);

            Vector2 move = Vector2.ClampMagnitude(cmd.Move, 1f);
            if (move.sqrMagnitude > 1e-4f) p.LastMoveDir = move.normalized;

            if (cmd.DashPressed && !p.Dashing && w.Clock.Tick >= p.DashReadyTick) StartDash(w, move);

            if (p.Dashing) StepDash(w);
            else StepWalk(w, move);
        }

        static void StepWalk(World w, Vector2 move)
        {
            var p = w.Player;
            var data = w.Profile.Player;
            Vector2 desired = move * PlayerStats.MoveSpeed(w);
            float rate = desired.sqrMagnitude > 1e-6f ? data.Acceleration : data.Deceleration;
            p.Velocity = Vector2.MoveTowards(p.Velocity, desired, rate * Core.SimClock.Dt);
            p.Position = w.Arena.MoveCircle(p.Position, p.Velocity * Core.SimClock.Dt, data.BodyRadius);
        }

        static void StartDash(World w, Vector2 move)
        {
            var p = w.Player;
            // Movement direction, or the last one when standing still; never the mouse aim (GDD §4, §17).
            p.DashDir = move.sqrMagnitude > 1e-4f ? move.normalized : p.LastMoveDir;
            p.Dashing = true;
            p.DashEndTick = w.Clock.Tick + DashTicks(w);
            p.DashReadyTick = w.Clock.Tick + w.Ticks(PlayerStats.DashCooldown(w));
            p.DashHitIds.Clear();
            WeaponSystem.OnDashStarted(w);
            w.Emit(SimEventType.PlayerDashStarted, PlayerState.EntityId, p.Position, p.DashDir);
        }

        public static long DashTicks(World w) => System.Math.Max(1, w.Ticks(w.Profile.Player.DashDuration));

        /// <summary>Speed chosen so the dash covers exactly DashDistance over its whole number of ticks.</summary>
        public static float DashSpeed(World w) => w.Profile.Player.DashDistance / (DashTicks(w) * Core.SimClock.Dt);

        static void StepDash(World w)
        {
            var p = w.Player;
            var data = w.Profile.Player;
            Vector2 from = p.Position;
            p.Velocity = p.DashDir * DashSpeed(w);
            p.Position = w.Arena.MoveCircle(from, p.Velocity * Core.SimClock.Dt, data.BodyRadius);

            // Passes through enemies and damages each one it touches once per dash.
            float damage = PlayerStats.DashDamage(w);
            foreach (var e in w.Enemies)
            {
                if (!e.Alive || p.DashHitIds.Contains(e.Id)) continue;
                float reach = e.Radius + data.BodyRadius;
                if (SimMath.SegmentPointDistanceSq(from, p.Position, e.Position) > reach * reach) continue;
                p.DashHitIds.Add(e.Id);
                Combat.DamageEnemy(w, e, damage, DamageSource.Dash, p.DashDir);
            }

            var boss = w.Boss;
            if (BossSystem.Targetable(boss) && !p.DashHitIds.Contains(BossState.EntityId))
            {
                float br = boss.Radius + data.BodyRadius;
                if (SimMath.SegmentPointDistanceSq(from, p.Position, boss.Position) <= br * br)
                {
                    p.DashHitIds.Add(BossState.EntityId);
                    BossSystem.Damage(w, damage, DamageSource.Dash, p.DashDir);
                }
            }

            if (w.Clock.Tick + 1 >= p.DashEndTick)
            {
                p.Dashing = false;
                p.Velocity = Vector2.zero; // ends predictably, no slide
                w.Emit(SimEventType.PlayerDashEnded, PlayerState.EntityId, p.Position, p.DashDir);
            }
        }

        /// <returns>True when the damage was taken (not blocked by invulnerability).</returns>
        public static bool Damage(World w, float amount, Vector2 travelDir)
        {
            var p = w.Player;
            if (p.Dead || amount <= 0 || w.GodMode || PlayerStats.IsInvulnerable(w)) return false;
            p.Health -= amount;
            p.LastHitTick = w.Clock.Tick;
            p.InvulnerableUntilTick = w.Clock.Tick + w.Ticks(w.Profile.Player.PostHitInvulnerability);
            w.Emit(SimEventType.PlayerHit, PlayerState.EntityId, p.Position, travelDir, amount);
            if (p.Health <= 0)
            {
                p.Health = 0;
                p.Dead = true;
                if (w.Failure == FailureReason.None) w.Failure = FailureReason.PlayerDied;
                w.Emit(SimEventType.PlayerDied, PlayerState.EntityId, p.Position, travelDir);
            }
            return true;
        }
    }
}
