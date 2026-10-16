using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    public sealed class ProjectileView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer core;
        [SerializeField] TrailRenderer trail;

        public void Begin(ProjectileState p, Color color)
        {
            transform.position = new Vector3(p.Position.x, p.Position.y, 0);
            if (core != null) core.color = color;
            if (trail != null)
            {
                trail.Clear();
                trail.startColor = color;
                trail.endColor = new Color(color.r, color.g, color.b, 0);
            }
        }

        public void Sync(ProjectileState p, float alpha)
        {
            Vector2 pos = Vector2.Lerp(p.PrevPosition, p.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);
            float a = Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, a);
        }
    }
}
