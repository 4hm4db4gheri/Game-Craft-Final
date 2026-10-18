using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Plays a <see cref="SpriteSet"/> on one renderer. Without a set the placeholder stays untouched.
    /// While the entity moves and its current state is a single drawn frame, the renderer steps along with a
    /// small hop and rock so it does not slide; real multi-frame strips play as drawn. Visual only.
    /// </summary>
    public sealed class SpriteSetAnimator : MonoBehaviour
    {
        [SerializeField] SpriteSet set;
        [SerializeField] SpriteRenderer target;

        [Header("Step motion for single-frame states")]
        [SerializeField] bool stepMotion = true;
        /// <summary>Distance covered per step, as a fraction of the sprite cell height.</summary>
        [SerializeField] float strideFraction = 0.35f;
        [SerializeField] float minStepsPerSecond = 1.6f;
        [SerializeField] float maxStepsPerSecond = 7f;
        /// <summary>Hop height as a fraction of the sprite cell height.</summary>
        [SerializeField] float hopFraction = 0.025f;
        [SerializeField] float rockDegrees = 4f;
        /// <summary>Slower than this (units per second) counts as standing.</summary>
        [SerializeField] float movingSpeed = 0.3f;
        /// <summary>How fast the motion fades in and out (per second).</summary>
        [SerializeField] float blendRate = 10f;

        string current;
        float startTime;
        Vector3 lastPosition;
        float speed, stepPhase, blend;
        bool hasLastPosition;

        public bool HasArt => set != null && target != null;
        public SpriteSet Set => set;
        /// <summary>The renderer the art is drawn on (dash afterimages copy it).</summary>
        public SpriteRenderer Target => target;

        /// <summary>Turns the step motion off (the hovering player drone has its own bob).</summary>
        public bool StepMotion { get => stepMotion; set => stepMotion = value; }

        /// <summary>Switches to another look (the Operator in phase two); keeps the current state.</summary>
        public void UseSet(SpriteSet other)
        {
            if (other == null || other == set) return;
            set = other;
        }

        public void SetState(string state)
        {
            if (!HasArt) return;
            if (state == current) return;
            current = state;
            startTime = Time.time;
        }

        void OnEnable()
        {
            // Pooled views reappear somewhere else: never read that jump as speed.
            hasLastPosition = false;
            speed = 0;
            blend = 0;
            stepPhase = Mathf.Repeat((GetHashCode() & 0xffff) * 0.618f, 1f); // neighbours do not step in sync
        }

        void LateUpdate()
        {
            if (!HasArt) return;
            var s = set.Resolve(current ?? "idle");
            if (s == null) return;
            int frame = (int)((Time.time - startTime) * s.Rate(set));
            frame = s.Loop ? frame % s.Frames.Count : Mathf.Min(frame, s.Frames.Count - 1);
            target.sprite = s.Frames[frame];
            Step(s.Frames.Count == 1 && current != "death");
        }

        void Step(bool singleFrame)
        {
            float dt = Time.deltaTime;
            Vector3 pos = transform.position;
            if (hasLastPosition && dt > 0)
            {
                float measured = ((Vector2)(pos - lastPosition)).magnitude / dt;
                speed = Mathf.Lerp(speed, measured, 1f - Mathf.Exp(-blendRate * dt));
            }
            lastPosition = pos;
            hasLastPosition = true;

            bool moving = stepMotion && singleFrame && speed > movingSpeed;
            blend = Mathf.MoveTowards(blend, moving ? 1f : 0f, blendRate * dt);

            var t = target.transform;
            if (blend <= 0f)
            {
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;
                return;
            }

            float height = target.sprite != null ? target.sprite.bounds.size.y : 1f;
            if (moving)
            {
                float stepsPerSecond = Mathf.Clamp(speed / Mathf.Max(0.01f, strideFraction * height), minStepsPerSecond, maxStepsPerSecond);
                stepPhase = Mathf.Repeat(stepPhase + stepsPerSecond * dt, 2f);
            }
            float wave = Mathf.Sin(stepPhase * Mathf.PI);
            t.localPosition = new Vector3(0, Mathf.Abs(wave) * hopFraction * height * blend, 0);
            t.localRotation = Quaternion.Euler(0, 0, wave * rockDegrees * blend);
        }
    }
}
