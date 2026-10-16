using System;
using System.Collections.Generic;
using UnityEngine;

namespace Hollow.Presentation
{
    [Serializable]
    public sealed class SpriteState
    {
        public string State;
        public List<Sprite> Frames = new();
        public bool Loop = true;
    }

    /// <summary>
    /// Imported art for one entity: frames per state (idle, move, windup, attack, death, …), filled by
    /// Hollow > Art > Apply Incoming Art from files named per docs/ART_MASTER_BRIEF.md.
    /// </summary>
    [CreateAssetMenu(menuName = "Hollow/Sprite Set")]
    public sealed class SpriteSet : ScriptableObject
    {
        public float FramesPerSecond = 10f;
        public List<SpriteState> States = new();

        public SpriteState Find(string state)
        {
            foreach (var s in States) if (s.State == state && s.Frames.Count > 0) return s;
            return null;
        }

        /// <summary>Requested state, else idle, else anything that exists.</summary>
        public SpriteState Resolve(string state)
        {
            var s = Find(state) ?? Find("idle");
            if (s != null) return s;
            foreach (var any in States) if (any.Frames.Count > 0) return any;
            return null;
        }
    }
}
