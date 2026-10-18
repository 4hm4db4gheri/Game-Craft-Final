using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Visible aiming reticle at the mouse position (manual aim, no auto-target).</summary>
    public sealed class ReticleView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer reticle;
        /// <summary>The reticle opens up by this share of its size at full spread bloom (D86).</summary>
        [SerializeField] float bloomGrowth = 0.9f;
        Vector3 baseScale;

        void Start() => baseScale = reticle.transform.localScale;

        void Awake() => Cursor.visible = false;
        void OnDestroy() => Cursor.visible = true;

        /// <param name="bloom">Held-fire spread bloom, 0..1 of the weapon's maximum.</param>
        public void Sync(Vector2 aimWorld, bool visible, float bloom = 0f)
        {
            reticle.enabled = visible;
            if (baseScale != Vector3.zero) reticle.transform.localScale = baseScale * (1f + bloomGrowth * bloom);
            Cursor.visible = !visible;
            transform.position = new Vector3(aimWorld.x, aimWorld.y, 0);
        }
    }
}
