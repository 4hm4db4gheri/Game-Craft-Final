using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Draws one projectile. The simulation moves shots along the ground; the view lifts them to body height
    /// (always straight up on screen, never rotated with the shot) and lets a shot start at the gun's muzzle,
    /// blending onto its true line within a moment. Purely visual: hits are decided by the simulation.
    /// </summary>
    public sealed class ProjectileView : MonoBehaviour
    {
        [SerializeField] SpriteRenderer core;
        [SerializeField] TrailRenderer trail;
        /// <summary>How far above its ground position a shot is drawn (3/4 view: shots fly at body height).</summary>
        [SerializeField] float height = 0.35f;
        /// <summary>Seconds over which a shot moves from the muzzle onto its drawn line.</summary>
        [SerializeField] float muzzleBlend = 0.1f;

        Vector2 muzzleOffset;
        float bornAt;
        Sprite placeholderSprite;
        Vector3 placeholderScale;
        Quaternion placeholderRotation;
        bool placeholderSaved;

        public Color Color { get; private set; }

        /// <param name="muzzle">Where the shot leaves the weapon on screen, when known (player and companion).</param>
        /// <param name="art">Imported projectile sprite (drawn at its own size, untinted); null keeps the placeholder.</param>
        public void Begin(ProjectileState p, Color color, Vector2? muzzle = null, Sprite art = null)
        {
            Color = color;
            bornAt = Time.time;
            Vector2 line = p.Origin + Vector2.up * height;
            muzzleOffset = muzzle.HasValue ? muzzle.Value - line : Vector2.zero;
            Place(p.Position, 1f);
            Rotate(p);
            if (core != null)
            {
                if (!placeholderSaved)
                {
                    placeholderSprite = core.sprite;
                    placeholderScale = core.transform.localScale;
                    placeholderRotation = core.transform.localRotation;
                    placeholderSaved = true;
                }
                core.sprite = art != null ? art : placeholderSprite;
                core.transform.localScale = art != null ? Vector3.one : placeholderScale;
                // The placeholder capsule is turned a quarter turn on the prefab; imported art already points along +x.
                core.transform.localRotation = art != null ? Quaternion.identity : placeholderRotation;
                core.color = art != null ? Color.white : color;
            }
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
            float t = muzzleBlend > 0f ? Mathf.Clamp01((Time.time - bornAt) / muzzleBlend) : 1f;
            Place(pos, 1f - t);
            Rotate(p);
        }

        void Place(Vector2 groundPos, float muzzleWeight)
        {
            Vector2 drawn = groundPos + Vector2.up * height + muzzleOffset * muzzleWeight;
            transform.position = new Vector3(drawn.x, drawn.y, 0);
        }

        void Rotate(ProjectileState p)
        {
            float a = Mathf.Atan2(p.Velocity.y, p.Velocity.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.Euler(0, 0, a);
        }
    }
}
