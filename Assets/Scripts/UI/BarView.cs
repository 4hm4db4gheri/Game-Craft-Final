using UnityEngine;
using UnityEngine.UI;

namespace Hollow.UI
{
    /// <summary>A filled bar (health, XP, reload, boss).</summary>
    public sealed class BarView : MonoBehaviour
    {
        Image fill;
        public void Init(Image f) => fill = f;
        public void Set(float fraction) { if (fill != null) fill.fillAmount = Mathf.Clamp01(fraction); }
        public void SetColor(Color c) { if (fill != null) fill.color = c; }
    }
}
