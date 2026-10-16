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
        [SerializeField] float hpBarWidth = 1.2f;

        Color[] baseColors;
        float flashUntil;
        int telegraphOrder = 500;

        void Awake()
        {
            baseColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) baseColors[i] = bodyRenderers[i].color;
        }

        public void ResetView()
        {
            flashUntil = 0;
            telegraphLine.enabled = false;
            telegraphArc.Hide();
        }

        public void Flash(float seconds) => flashUntil = Time.time + seconds;

        public void Sync(World w, EnemyState e, float alpha, Color telegraphColor)
        {
            var d = w.Profile.Enemy(e.Type);
            Vector2 pos = Vector2.Lerp(e.PrevPosition, e.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);

            bool facingLeft = e.Facing.x < -0.05f;
            body.localScale = new Vector3(facingLeft ? -1 : 1, 1, 1);
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

            DrawTelegraph(w, e, d, pos, telegraphColor);
        }

        void DrawTelegraph(World w, EnemyState e, EnemyData d, Vector2 pos, Color color)
        {
            bool windup = e.Ai == EnemyAi.Windup;
            if (!windup) { telegraphLine.enabled = false; telegraphArc.Hide(); return; }

            long total = System.Math.Max(1, w.Ticks(d.AttackWindup));
            float progress = 1f - Mathf.Clamp01((e.AiUntilTick - w.Clock.Tick) / (float)total);
            var c = color;
            c.a = Mathf.Lerp(0.25f, 0.75f, progress);

            if (d.Role == EnemyRole.Ranged)
            {
                telegraphArc.Hide();
                telegraphLine.enabled = true;
                float len = d.ProjectileRange;
                float angle = Mathf.Atan2(e.AttackDir.y, e.AttackDir.x) * Mathf.Rad2Deg;
                var t = telegraphLine.transform;
                t.position = new Vector3(pos.x, pos.y, 0) + (Vector3)(e.AttackDir * len * 0.5f);
                t.rotation = Quaternion.Euler(0, 0, angle);
                t.localScale = new Vector3(len / telegraphLine.sprite.bounds.size.x, Mathf.Lerp(0.04f, 0.12f, progress) / telegraphLine.sprite.bounds.size.y, 1);
                telegraphLine.color = c;
                telegraphLine.sortingOrder = telegraphOrder;
            }
            else
            {
                telegraphLine.enabled = false;
                float reach = d.AttackRange + e.Radius + w.Profile.Player.BodyRadius;
                telegraphArc.Set(pos, e.AttackDir, reach, w.Profile.Combat.EnemyMeleeHitHalfAngle * 2f, c, telegraphOrder);
            }
        }
    }
}
