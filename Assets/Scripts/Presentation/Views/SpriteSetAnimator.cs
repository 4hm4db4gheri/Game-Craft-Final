using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Plays a <see cref="SpriteSet"/> on one renderer. Without a set the placeholder stays untouched.</summary>
    public sealed class SpriteSetAnimator : MonoBehaviour
    {
        [SerializeField] SpriteSet set;
        [SerializeField] SpriteRenderer target;

        string current;
        float startTime;

        public bool HasArt => set != null && target != null;
        public SpriteSet Set => set;

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

        void LateUpdate()
        {
            if (!HasArt) return;
            var s = set.Resolve(current ?? "idle");
            if (s == null) return;
            int frame = (int)((Time.time - startTime) * set.FramesPerSecond);
            frame = s.Loop ? frame % s.Frames.Count : Mathf.Min(frame, s.Frames.Count - 1);
            target.sprite = s.Frames[frame];
        }
    }
}
