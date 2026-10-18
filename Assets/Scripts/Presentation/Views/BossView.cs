using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Draws the boss and its telegraphs (lane, strike area, burst cone, pounce and leap landing zones) in the reserved
    /// warning colour. Jumps only lift the body: the shadow stays on the ground and marks where the boss is (D86).
    /// </summary>
    public sealed class BossView : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] SpriteRenderer[] bodyRenderers;
        [SerializeField] SpriteRenderer laneLine;
        [SerializeField] ArcView area;
        /// <summary>Visual only: how high a leap carries the body (enough to leave the screen) and a pounce hop per unit of radius.</summary>
        [SerializeField] float leapLift = 16f;
        [SerializeField] float pounceLift = 0.9f;

        Transform shadow;
        Vector3 shadowScale;

        Color[] baseColors;
        SpriteSetAnimator anim;
        float flashUntil;

        void Awake()
        {
            anim = GetComponent<SpriteSetAnimator>();
            baseColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) baseColors[i] = bodyRenderers[i].color;
            shadow = transform.Find("Shadow");
            if (shadow != null) shadowScale = shadow.localScale;
        }

        public void Flash(float seconds) => flashUntil = Time.time + seconds;

        public void Sync(World w, float alpha, Color telegraph)
        {
            var b = w.Boss;
            Vector2 pos = Vector2.Lerp(b.PrevPosition, b.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);
            body.localScale = new Vector3(b.Facing.x < -0.05f ? -1 : 1, 1, 1);
            float leap = LeapHeight(b.Ai, BossSystem.MoveProgress(w)), hop = b.Ai == BossAi.Pouncing ? Hop(BossSystem.MoveProgress(w)) : 0f;
            body.localPosition = new Vector3(0, leap * leapLift + hop * pounceLift * b.Radius, 0);
            if (shadow != null) shadow.localScale = shadowScale * Mathf.Lerp(1f, 0.45f, Mathf.Max(leap, hop * 0.4f));
            bool robot = b.Id == BossId.IndustrialRobot;
            bool flashing = Time.time < flashUntil;
            bool stunned = w.Clock.Tick < b.StunnedUntilTick;
            for (int i = 0; i < bodyRenderers.Length; i++)
                bodyRenderers[i].color = flashing ? Color.white : stunned ? Color.Lerp(baseColors[i], new Color(0.5f, 0.8f, 1f), 0.5f) : baseColors[i];

            if (anim != null) anim.SetState(b.Ai switch
            {
                BossAi.ChargeWindup => "windup_charge", BossAi.Charging => "charge", BossAi.ChargeRecover => "recover",
                BossAi.StrikeWindup => "windup_strike", BossAi.StrikeRecover => "strike",
                BossAi.BurstWarning => "windup_burst", BossAi.Bursting => "burst", BossAi.BurstRecover => "recover",
                BossAi.BlastWarning => b.Id == BossId.FormerCompanion ? "throw" : "windup_blast",
                // The robot's blast frame is the slam itself, so it shows once the blast has gone off.
                BossAi.BlastRecover => b.Id == BossId.FormerCompanion ? "recover" : "blast",
                BossAi.PounceWindup => "windup_charge", BossAi.Pouncing => "charge", BossAi.PounceRecover => "recover",
                BossAi.LeapRise or BossAi.LeapAir or BossAi.LeapLock => robot ? "windup_blast" : "windup_strike",
                BossAi.LeapRecover => robot ? "blast" : "strike",
                _ => w.Clock.Tick < b.StunnedUntilTick ? "stunned" : "move",
            });
            var d = w.Profile.Boss(w.Phase);
            laneLine.enabled = false;
            area.Hide();
            long left = b.AiUntilTick - w.Clock.Tick;
            switch (b.Ai)
            {
                case BossAi.ChargeWindup:
                {
                    var c = Tint(telegraph, Progress(left, d.ChargeTelegraph, w));
                    laneLine.enabled = true;
                    var t = laneLine.transform;
                    t.position = new Vector3(pos.x, pos.y, 0) + (Vector3)(b.AttackDir * b.ChargeLength * 0.5f);
                    t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.AttackDir.y, b.AttackDir.x) * Mathf.Rad2Deg);
                    var size = laneLine.sprite.bounds.size;
                    t.localScale = new Vector3(b.ChargeLength / size.x, b.Radius * 2f / size.y, 1);
                    laneLine.color = new Color(c.r, c.g, c.b, c.a * 0.55f);
                    break;
                }
                case BossAi.StrikeWindup:
                    area.Set(pos, Vector2.right, d.StrikeRadius, 360f, Tint(telegraph, Progress(left, d.StrikeWindup, w)) * new Color(1, 1, 1, 0.6f), 500);
                    break;
                case BossAi.BurstWarning:
                    area.Set(pos, b.AttackDir, d.ProjectileRange, Cone(d), Tint(telegraph, Progress(left, d.BurstWarning, w)) * new Color(1, 1, 1, 0.45f), 500);
                    break;
                case BossAi.Bursting:
                    // The cone stays drawn (dimmer) while it fires, so a tracking sweep is always readable.
                    area.Set(pos, b.AttackDir, d.ProjectileRange, Cone(d), Tint(telegraph, 0f) * new Color(1, 1, 1, 0.6f), 500);
                    break;
                case BossAi.PounceWindup:
                    area.Set(b.AttackPoint, Vector2.right, d.PounceRadius, 360f, Tint(telegraph, 0.7f * Progress(left, d.PounceTelegraph, w)) * new Color(1, 1, 1, 0.7f), 500);
                    break;
                case BossAi.Pouncing:
                    area.Set(b.AttackPoint, Vector2.right, d.PounceRadius, 360f, Tint(telegraph, 0.7f + 0.3f * Progress(left, d.PounceFlightTime, w)) * new Color(1, 1, 1, 0.7f), 500);
                    break;
                case BossAi.LeapRise:
                case BossAi.LeapAir:
                    // The landing zone follows the player while the boss is off the map.
                    area.Set(pos, Vector2.right, d.LeapRadius, 360f, Tint(telegraph, 0f) * new Color(1, 1, 1, 0.55f), 500);
                    break;
                case BossAi.LeapLock:
                    area.Set(pos, Vector2.right, d.LeapRadius, 360f, Tint(telegraph, Progress(left, d.LeapLockTime, w)), 500);
                    break;
            }
        }

        /// <summary>Leap height, 0 on the ground to 1 off the map: up fast, comes down late in the lock, accelerating into the slam.</summary>
        static float LeapHeight(BossAi ai, float t)
        {
            switch (ai)
            {
                case BossAi.LeapRise: return t * t;
                case BossAi.LeapAir: return 1f;
                case BossAi.LeapLock:
                {
                    float fall = Mathf.Clamp01((t - 0.55f) / 0.45f);
                    return 1f - fall * fall;
                }
                default: return 0f;
            }
        }

        /// <summary>Pounce hop, peaking mid-flight.</summary>
        static float Hop(float t) => 4f * t * (1f - t);

        /// <summary>Whole angle the burst can cover: the sweep plus the fan's outer bullets.</summary>
        static float Cone(BossData d) => Mathf.Max(6f, d.BurstSpread + (Mathf.Max(1, d.BurstFanCount) - 1) * d.BurstFanSpacing);

        static float Progress(long ticksLeft, float total, World w) =>
            1f - Mathf.Clamp01(ticksLeft / (float)Mathf.Max(1, w.Ticks(total)));

        /// <summary>Kept soft so a long warning never glares (the boss is the one place red warnings remain).</summary>
        static Color Tint(Color c, float progress) => new(c.r, c.g, c.b, Mathf.Lerp(0.18f, 0.55f, progress));
    }
}
