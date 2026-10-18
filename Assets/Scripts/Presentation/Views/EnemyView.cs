using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Draws one enemy: body, optional front plate (frontal armour), health bar once damaged, spawn warning,
    /// and the attack telegraph in the reserved warning colour. Telegraphs are drawn above effects (§14).
    /// </summary>
    public sealed class EnemyView : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] SpriteRenderer[] bodyRenderers;
        [SerializeField] Transform frontPlate;
        [SerializeField] SpriteRenderer hpBack;
        [SerializeField] SpriteRenderer hpFill;
        [SerializeField] SpriteRenderer spawnRing;
        [SerializeField] SpriteRenderer telegraphLine;
        [SerializeField] ArcView telegraphArc;
        [SerializeField] TMPro.TextMeshPro countdown;
        [SerializeField] float hpBarWidth = 1.2f;
        /// <summary>Recoil of enemies that fire guns: the body (with its drawn gun) kicks back from the shot.</summary>
        [SerializeField] float recoilPushBack = 0.09f;
        [SerializeField] float recoilReturn = 14f;

        Color[] baseColors;
        float flashUntil, flashLength = 0.1f, squash;
        SpriteSetAnimator anim;
        int telegraphOrder = 500;
        Vector3 bodyBaseLocal;
        Vector2 kickDir;
        float recoil;
        string actState;
        float actUntil;

        void Awake()
        {
            anim = GetComponent<SpriteSetAnimator>();
            baseColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) baseColors[i] = bodyRenderers[i].color;
            bodyBaseLocal = body.localPosition;
        }

        public void ResetView()
        {
            flashUntil = 0;
            recoil = 0;
            actUntil = 0;
            telegraphLine.enabled = false;
            telegraphArc.Hide();
            if (countdown != null) countdown.enabled = false;
        }

        /// <summary>The body used for the corpse look (aftermath).</summary>
        public SpriteRenderer MainRenderer => bodyRenderers != null && bodyRenderers.Length > 0 ? bodyRenderers[0] : null;

        /// <summary>The enemy fired a gun along this direction.</summary>
        public void Kick(Vector2 shotDirection)
        {
            kickDir = shotDirection.normalized;
            recoil = 1f;
        }

        /// <summary>
        /// A support action the simulation does without a state of its own (a medic heals, a carrier or engineer
        /// makes a unit): plays the first of these drawn states once, if the art has it. Visual only.
        /// </summary>
        public void Act(params string[] states)
        {
            if (anim == null || anim.Set == null) return;
            foreach (var s in states)
            {
                var found = anim.Set.Find(s);
                if (found == null) continue;
                actState = s;
                actUntil = Time.time + Mathf.Max(0.5f, found.Frames.Count / Mathf.Max(1f, found.Rate(anim.Set)));
                return;
            }
        }

        /// <summary>Hit reaction: white flash plus a short squash. Visual only; the hitbox never changes (§14).</summary>
        public void Flash(float seconds, float squashAmount = 0f)
        {
            flashLength = Mathf.Max(0.01f, seconds);
            flashUntil = Time.time + flashLength;
            squash = squashAmount;
        }

        public void Sync(World w, EnemyState e, float alpha, Color telegraphColor)
        {
            var d = w.Profile.Enemy(e.Type);
            Vector2 pos = Vector2.Lerp(e.PrevPosition, e.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);

            bool facingLeft = e.Facing.x < -0.05f;
            float punch = squash * Mathf.Clamp01((flashUntil - Time.time) / flashLength);
            body.localScale = new Vector3((facingLeft ? -1 : 1) * (1f + punch), 1f - punch, 1);
            recoil *= Mathf.Exp(-recoilReturn * Time.deltaTime);
            body.localPosition = bodyBaseLocal - (Vector3)(kickDir * recoilPushBack * recoil);
            if (frontPlate != null)
            {
                float a = Mathf.Atan2(e.Facing.y, e.Facing.x) * Mathf.Rad2Deg;
                frontPlate.localRotation = Quaternion.Euler(0, 0, a);
            }

            bool spawning = e.Ai == EnemyAi.Spawning;
            bool flashing = Time.time < flashUntil;
            bool stunned = w.Clock.Tick < e.StunnedUntilTick;
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Color c = flashing ? Color.white : stunned ? Color.Lerp(baseColors[i], new Color(0.5f, 0.8f, 1f), 0.5f) : baseColors[i];
                if (spawning) c.a = 0.3f + 0.2f * Mathf.Sin(Time.time * 12f);
                bodyRenderers[i].color = c;
            }

            if (spawnRing != null)
            {
                spawnRing.enabled = spawning;
                if (spawning)
                {
                    float remaining = Mathf.Max(0, e.AiUntilTick - w.Clock.Tick) * Core.SimClock.Dt;
                    float total = Mathf.Max(0.01f, w.Profile.Spawning.SpawnWarning);
                    float s = Mathf.Lerp(0.6f, 1.6f, remaining / total) * e.Radius * 2f;
                    spawnRing.transform.localScale = new Vector3(s, s, 1);
                    spawnRing.color = new Color(telegraphColor.r, telegraphColor.g, telegraphColor.b, 0.7f);
                }
            }

            bool damaged = e.Health < e.MaxHealth && !spawning;
            hpBack.enabled = hpFill.enabled = damaged;
            if (damaged)
            {
                float frac = Mathf.Clamp01(e.Health / e.MaxHealth);
                float full = hpFill.sprite.bounds.size.x * hpBarWidth;
                hpFill.transform.localScale = new Vector3(hpBarWidth * frac, hpFill.transform.localScale.y, 1);
                hpFill.transform.localPosition = new Vector3(-full * 0.5f * (1f - frac), hpFill.transform.localPosition.y, 0);
            }

            if (anim != null) anim.SetState(Time.time < actUntil && e.Ai is not (EnemyAi.Windup or EnemyAi.Attack or EnemyAi.Charge or EnemyAi.Spawning)
                ? actState : AnimState(e));
            DrawTelegraph(w, e, d, pos, telegraphColor);
        }

        void DrawTelegraph(World w, EnemyState e, EnemyData d, Vector2 pos, Color color)
        {
            bool windup = e.Ai == EnemyAi.Windup;
            if (countdown != null) countdown.enabled = false;
            if (!windup) { telegraphLine.enabled = false; telegraphArc.Hide(); return; }

            long totalTicks = d.Role == EnemyRole.Explosive ? w.Ticks(d.FuseTime) : w.Ticks(d.AttackWindup);
            long total = System.Math.Max(1, totalTicks);
            long left = e.AiUntilTick - w.Clock.Tick;
            float progress = 1f - Mathf.Clamp01(left / (float)total);
            var c = color;
            c.a = Mathf.Lerp(0.12f, 0.38f, progress);

            // Only chargers draw a warning on the ground (their lane); aim lines and attack arcs of the other
            // enemies were removed as too loud. Their windup pose, sound and the shot itself still warn the player.
            telegraphLine.enabled = false;
            telegraphArc.Hide();
            switch (d.Role)
            {
                case EnemyRole.Charger:
                    Line(pos, e.AttackDir, e.ChargeLength, e.Radius * 2f, c);
                    break;
                case EnemyRole.Explosive:
                    if (countdown != null)
                    {
                        countdown.enabled = true;
                        countdown.text = PersianText.Number(Mathf.CeilToInt(left * Core.SimClock.Dt));
                        if (PersianText.Font != null && countdown.font != PersianText.Font) countdown.font = PersianText.Font;
                        countdown.color = new Color(color.r, color.g, color.b, 0.8f);
                    }
                    break;
                case EnemyRole.Support: // the pale heal aura growing, not a red aim warning
                    telegraphArc.Set(pos, Vector2.right, d.AuraRadius * progress, 360f, new Color(0.9f, 0.85f, 0.6f, 0.2f), telegraphOrder - 1);
                    break;
            }
        }

        static string AnimState(EnemyState e) => e.Ai switch
        {
            EnemyAi.Windup => "windup",
            EnemyAi.Attack or EnemyAi.Charge => "attack",
            EnemyAi.Recover => "recover",
            _ => e.Velocity.sqrMagnitude > 0.05f ? "move" : "idle",
        };

        void Line(Vector2 from, Vector2 dir, float length, float width, Color c)
        {
            telegraphLine.enabled = true;
            var t = telegraphLine.transform;
            t.position = new Vector3(from.x, from.y, 0) + (Vector3)(dir * length * 0.5f);
            t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg);
            var size = telegraphLine.sprite.bounds.size;
            t.localScale = new Vector3(length / size.x, width / size.y, 1);
            telegraphLine.color = c;
            telegraphLine.sortingOrder = telegraphOrder;
        }    }
}
