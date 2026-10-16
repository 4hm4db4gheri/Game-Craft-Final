using Hollow.Sim;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Draws the player from simulation state. Purely visual: nothing here feeds back into gameplay,
    /// and the cosmetic numbers (bob, lean) live on the prefab so an artist can tune or replace them.
    /// </summary>
    public sealed class PlayerView : MonoBehaviour
    {
        [SerializeField] Transform body;
        [SerializeField] SpriteRenderer bodyRenderer;
        [SerializeField] Transform weaponPivot;
        [SerializeField] SpriteRenderer weaponRenderer;
        [SerializeField] SpriteRenderer shadow;
        [SerializeField] SpriteRenderer dashBarBack;
        [SerializeField] SpriteRenderer dashBarFill;
        [SerializeField] float dashBarWidth = 1.2f;
        [Header("Cosmetic")]
        [SerializeField] float bobHeight = 0.06f;
        [SerializeField] float bobSpeed = 3f;
        [SerializeField] float floatHeight = 0.35f;
        [SerializeField] float leanDegrees = 10f;
        [SerializeField] Color normalColor = new(0.85f, 0.95f, 1f);
        [SerializeField] Color hurtColor = new(1f, 0.35f, 0.35f);
        [SerializeField] Color dashColor = new(0.5f, 0.9f, 1f, 0.6f);

        WeaponVisual shownWeapon;
        SpriteSetAnimator anim;

        void Awake() => anim = GetComponent<SpriteSetAnimator>();

        public void Sync(World w, float alpha, WeaponVisual weapon)
        {
            if (weapon != null && weapon != shownWeapon && weaponRenderer != null)
            {
                shownWeapon = weapon;
                if (weapon.Sprite != null) weaponRenderer.sprite = weapon.Sprite;
                weaponRenderer.color = weapon.Color;
                var size = weaponRenderer.sprite.bounds.size;
                weaponRenderer.transform.localScale = new Vector3(weapon.Size.x / size.x, weapon.Size.y / size.y, 1);
                weaponRenderer.transform.localPosition = new Vector3(weapon.PivotAtGrip ? 0.2f : 0.2f + weapon.Size.x * 0.5f, 0, 0);
            }
            var p = w.Player;
            Vector2 pos = Vector2.Lerp(p.PrevPosition, p.Position, alpha);
            transform.position = new Vector3(pos.x, pos.y, 0);

            float bob = Mathf.Sin(Time.time * bobSpeed * Mathf.PI * 2f) * bobHeight;
            body.localPosition = new Vector3(0, floatHeight + bob, 0);
            float speed01 = Mathf.Clamp01(p.Velocity.magnitude / Mathf.Max(0.01f, w.Profile.Player.MoveSpeed));
            body.localRotation = Quaternion.Euler(0, 0, -Mathf.Sign(p.Velocity.x) * leanDegrees * speed01);

            Vector2 aimDir = p.Aim - (pos + Vector2.up * floatHeight);
            float angle = Mathf.Atan2(aimDir.y, aimDir.x) * Mathf.Rad2Deg;
            weaponPivot.localPosition = body.localPosition;
            // Reloading tilts the weapon down; purely visual, never delays input (§4).
            float reloadTilt = p.Loadout.Reloading ? -35f * Mathf.Sign(aimDir.x == 0 ? 1 : aimDir.x) : 0f;
            weaponPivot.localRotation = Quaternion.Euler(0, 0, angle + reloadTilt);
            if (weaponRenderer != null) weaponRenderer.flipY = aimDir.x < 0;

            bool hurt = w.Clock.Tick < p.InvulnerableUntilTick && !p.Dashing;
            bodyRenderer.color = p.Dashing ? dashColor : hurt && (Time.frameCount / 4) % 2 == 0 ? hurtColor : normalColor;
            if (p.Dead) bodyRenderer.color = Color.gray;
            if (anim != null) anim.SetState(p.Dead ? "death" : p.Velocity.sqrMagnitude > 0.25f ? "move" : "idle");

            // Dash readiness near the player: shown only while recharging.
            long cooldownTicks = w.Ticks(PlayerStats.DashCooldown(w));
            long remaining = p.DashReadyTick - w.Clock.Tick;
            bool recharging = remaining > 0 && cooldownTicks > 0;
            dashBarBack.enabled = dashBarFill.enabled = recharging;
            if (recharging)
            {
                float frac = 1f - Mathf.Clamp01(remaining / (float)cooldownTicks);
                dashBarFill.transform.localScale = new Vector3(dashBarWidth * frac, dashBarFill.transform.localScale.y, 1);
                float fullWidth = dashBarFill.sprite.bounds.size.x * dashBarWidth;
                dashBarFill.transform.localPosition = new Vector3(-fullWidth * 0.5f * (1f - frac), dashBarFill.transform.localPosition.y, 0);
            }
        }
    }
}
