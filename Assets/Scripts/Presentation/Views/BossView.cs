using Hollow.Data;
using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Draws the boss and its telegraphs (lane, strike area, burst cone) in the reserved warning colour.</summary>
    public sealed class BossView : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] SpriteRenderer[] bodyRenderers;
        [SerializeField] SpriteRenderer laneLine;
        [SerializeField] ArcView area;

        Color[] baseColors;
        float flashUntil;

        void Awake()
        {
            baseColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) baseColors[i] = bodyRenderers[i].color;
        }

        public void Flash(float seconds) => flashUntil = Time.time + seconds;

        public void Sync(World w, float alpha, Color telegraph)
        {
            var b = w.Boss;
            Vector2 pos = Vector2.Lerp(b.PrevPosition, b.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);
            body.localScale = new Vector3(b.Facing.x < -0.05f ? -1 : 1, 1, 1);
            bool flashing = Time.time < flashUntil;
            bool stunned = w.Clock.Tick < b.StunnedUntilTick;
            for (int i = 0; i < bodyRenderers.Length; i++)
                bodyRenderers[i].color = flashing ? Color.white : stunned ? Color.Lerp(baseColors[i], new Color(0.5f, 0.8f, 1f), 0.5f) : baseColors[i];

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
                    t.position = new Vector3(pos.x, pos.y, 0) + (Vector3)(b.AttackDir * d.ChargeDistance * 0.5f);
                    t.rotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.AttackDir.y, b.AttackDir.x) * Mathf.Rad2Deg);
                    var size = laneLine.sprite.bounds.size;
                    t.localScale = new Vector3(d.ChargeDistance / size.x, b.Radius * 2f / size.y, 1);
                    laneLine.color = new Color(c.r, c.g, c.b, c.a * 0.55f);
                    break;
                }
                case BossAi.StrikeWindup:
                    area.Set(pos, Vector2.right, d.StrikeRadius, 360f, Tint(telegraph, Progress(left, d.StrikeWindup, w)) * new Color(1, 1, 1, 0.6f), 500);
                    break;
                case BossAi.BurstWarning:
                    area.Set(pos, b.AttackDir, d.ProjectileRange, Mathf.Max(6f, d.BurstSpread), Tint(telegraph, Progress(left, d.BurstWarning, w)) * new Color(1, 1, 1, 0.45f), 500);
                    break;
            }
        }

        static float Progress(long ticksLeft, float total, World w) =>
            1f - Mathf.Clamp01(ticksLeft / (float)Mathf.Max(1, w.Ticks(total)));

        static Color Tint(Color c, float progress) => new(c.r, c.g, c.b, Mathf.Lerp(0.3f, 0.85f, progress));
    }
}
