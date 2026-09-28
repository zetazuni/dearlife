using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Test tool for the physics: click (or tap) anything that can move and it gets a shove
    /// away from the camera, scaled by its mass so a cushion flies and a sofa only budges.
    /// Will make way for proper interactions once decorate mode arrives.
    /// </summary>
    public class PhysicsPoke : MonoBehaviour
    {
        public float strength = 0.7f;     // metres per second of nudge speed, a few centimetres of movement
        public float maxImpulse = 12f;
        Camera cam;

        void Awake() => cam = GetComponent<Camera>();

        void SettleLater() => Furniture.SettleSmallThings();

        // LateUpdate so the orbit camera has already decided whether this was a click or a drag
        void LateUpdate()
        {
            var orbit = OrbitCamera.Instance;
            if (!orbit || !orbit.ClickedThisFrame || DecorateMode.Active) return;
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit, 200f)) return;
            // a click on a curtain, or on a window (through its curtain), draws the curtains
            var curtain = hit.collider.GetComponentInParent<Curtain>();
            if (curtain) { curtain.Toggle(); return; }
            var part = hit.collider.GetComponent<WallWindowPart>();
            if (part) { part.wall.ToggleCurtain(part.index); return; }

            Vector3 flat = new Vector3(ray.direction.x, 0f, ray.direction.z).normalized;
            var sticky = hit.collider.GetComponentInParent<StickyProp>();
            if (sticky) { sticky.Nudge(flat); return; }   // small things only shift a little and settle back

            var rb = hit.rigidbody;
            if (!rb || rb.isKinematic) return;
            Vector3 dir = new Vector3(ray.direction.x, 0f, ray.direction.z).normalized;   // sideways only, never lifts or tips
            float impulse = Mathf.Min(rb.mass * strength, maxImpulse);
            rb.AddForceAtPosition(dir * impulse, hit.point, ForceMode.Impulse);
            CancelInvoke(nameof(SettleLater));
            Invoke(nameof(SettleLater), 1.2f);   // small things follow whatever they sit on
        }
    }
}
