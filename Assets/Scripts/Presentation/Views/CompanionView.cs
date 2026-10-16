using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// The Operator as companion: visibly weaker in phase two (§14), danger marker above the head after a hit,
    /// and a world position the HUD uses for the screen-edge arrow when off-screen (§4).
    /// </summary>
    public sealed class CompanionView : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] SpriteRenderer[] bodyRenderers;
        [SerializeField] SpriteRenderer danger;
        [SerializeField] Color phaseTwoTint = new(0.75f, 0.75f, 0.75f, 1f);
        [SerializeField] float phaseTwoScale = 0.92f;

        Color[] baseColors;
        SpriteSetAnimator anim;
        float flashUntil;

        void Awake()
        {
            anim = GetComponent<SpriteSetAnimator>();
            baseColors = new Color[bodyRenderers.Length];
            for (int i = 0; i < bodyRenderers.Length; i++) baseColors[i] = bodyRenderers[i].color;
        }

        public void Flash(float seconds) => flashUntil = Time.time + seconds;

        public void Sync(World w, float alpha)
        {
            var c = w.Companion;
            gameObject.SetActive(c.Present);
            if (!c.Present) return;
            Vector2 pos = Vector2.Lerp(c.PrevPosition, c.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);

            bool weaker = w.Phase >= 2;
            body.localScale = new Vector3(c.Facing.x < -0.05f ? -1 : 1, 1, 1) * (weaker ? phaseTwoScale : 1f);
            bool flashing = Time.time < flashUntil;
            for (int i = 0; i < bodyRenderers.Length; i++)
            {
                Color baseC = weaker ? baseColors[i] * phaseTwoTint : baseColors[i];
                bodyRenderers[i].color = c.Dead ? Color.gray : flashing ? Color.white : baseC;
            }

            if (anim != null) anim.SetState(c.Dead ? "death" : c.Velocity.sqrMagnitude > 0.05f ? "move" : "idle");
            float since = (w.Clock.Tick - c.LastHitTick) * Core.SimClock.Dt;
            bool showDanger = !c.Dead && since < w.Profile.Companion.DangerIndicatorTime;
            danger.enabled = showDanger;
            if (showDanger) danger.transform.localPosition = new Vector3(0, 1.85f + Mathf.Sin(Time.time * 10f) * 0.05f, 0);
        }
    }
}
