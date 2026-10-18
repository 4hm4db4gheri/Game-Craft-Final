using System.Collections.Generic;
using UnityEngine;

namespace Hollow.UI
{
    [CreateAssetMenu(menuName = "Hollow/Dialogue/Operator Bubble Lines")]
    public sealed class OperatorBubbleLines : ScriptableObject
    {
        [Tooltip("Only nonempty entries are spoken. Edit, add, remove or reorder lines here.")]
        public List<string> Lines = new();
        [Min(1f)] public float MinimumGap = 28f;
        [Min(1f)] public float MaximumGap = 44f;
        [Min(0.5f)] public float DisplaySeconds = 3.5f;
    }
}
