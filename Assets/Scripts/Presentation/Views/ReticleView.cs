using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>Visible aiming reticle at the mouse position (manual aim, no auto-target).</summary>
    public sealed class ReticleView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer reticle;

        void Awake() => Cursor.visible = false;
        void OnDestroy() => Cursor.visible = true;

        public void Sync(Vector2 aimWorld, bool visible)
        {
            reticle.enabled = visible;
            Cursor.visible = !visible;
            transform.position = new Vector3(aimWorld.x, aimWorld.y, 0);
        }
    }
}
