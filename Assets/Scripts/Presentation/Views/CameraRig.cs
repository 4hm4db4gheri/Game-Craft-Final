using Hollow.Sim;
using Unity.Cinemachine;
using UnityEngine;

namespace Hollow.Presentation
{
    /// <summary>
    /// Moves the Cinemachine follow target to the player plus a small look-ahead toward the aim (D12),
    /// and applies the framing values from the balance profile.
    /// </summary>
    public sealed class CameraRig : MonoBehaviour
    {
        [SerializeField] Transform followTarget;
        [SerializeField] CinemachineCamera virtualCamera;
        [SerializeField] CinemachinePositionComposer composer;

        CinemachineImpulseSource impulse;

        void Awake()
        {
            impulse = gameObject.AddComponent<CinemachineImpulseSource>();
            impulse.ImpulseDefinition.ImpulseType = CinemachineImpulseDefinition.ImpulseTypes.Uniform;
            impulse.ImpulseDefinition.ImpulseShape = CinemachineImpulseDefinition.ImpulseShapes.Bump;
        }

        /// <summary>Short camera kick (§14: short and adjustable; never a hit-stop). Offset in world units.</summary>
        public void Shake(Vector2 offset, float seconds)
        {
            impulse.ImpulseDefinition.ImpulseDuration = Mathf.Max(0.05f, seconds);
            impulse.GenerateImpulseWithVelocity(new Vector3(offset.x, offset.y, 0));
        }

        public void Sync(World w, float alpha)
        {
            var cam = w.Profile.Camera;
            var p = w.Player;
            Vector2 pos = Vector2.Lerp(p.PrevPosition, p.Position, alpha);
            Vector2 look = Vector2.ClampMagnitude((p.Aim - pos) * cam.LookAheadFactor, cam.LookAheadMax);
            followTarget.position = new Vector3(pos.x + look.x, pos.y + look.y, 0);

            if (virtualCamera != null && !Mathf.Approximately(virtualCamera.Lens.OrthographicSize, cam.OrthographicSize))
            {
                var lens = virtualCamera.Lens;
                lens.OrthographicSize = cam.OrthographicSize;
                virtualCamera.Lens = lens;
            }
            if (composer != null) composer.Damping = new Vector3(cam.FollowDamping, cam.FollowDamping, 0);
        }
    }
}
