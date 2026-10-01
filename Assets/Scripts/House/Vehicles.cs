using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Cars (v0.53.0). A bought car used to be a free physics body like a chair: 140 kg with one box collider for each of
    /// its 120 to 200 parts. It never came to rest on all those boxes, crept across the garage floor (9 cm and most of a
    /// degree within seconds of loading a save), and a click gave it a shove. A parked car now has two boxes, the body
    /// and the cabin, and does not move unless you move it in decorate mode.
    /// </summary>
    public static class Vehicles
    {
        static readonly HashSet<string> Ids = new HashSet<string> { "sedan", "mpv", "avanza", "mazda3", "bmwm3", "porsche911" };

        public static bool Is(string id) => Ids.Contains(InteractionTable.BaseId(id));

        /// <summary>The two boxes of a car of this size (in the car's own space): the body up to the windows, and the cabin.</summary>
        public static void Boxes(Bounds lb, out Vector3 bodyCentre, out Vector3 bodySize, out Vector3 cabinCentre, out Vector3 cabinSize)
        {
            float h = lb.size.y;
            bool alongZ = lb.size.z >= lb.size.x;
            bodyCentre = new Vector3(lb.center.x, lb.min.y + h * 0.3f, lb.center.z);
            bodySize = new Vector3(lb.size.x, h * 0.6f, lb.size.z);
            cabinCentre = new Vector3(lb.center.x, lb.min.y + h * 0.79f, lb.center.z);
            cabinSize = alongZ ? new Vector3(lb.size.x * 0.86f, h * 0.42f, lb.size.z * 0.5f) : new Vector3(lb.size.x * 0.5f, h * 0.42f, lb.size.z * 0.86f);
        }

        /// <summary>Makes a car that was just bought (or loaded from a save) stand still on two boxes.</summary>
        public static void Park(Furniture f)
        {
            if (!f) return;
            var lb = f.LocalBounds;                       // from the meshes, before the part colliders go
            foreach (var c in f.GetComponentsInChildren<Collider>()) { c.enabled = false; Object.Destroy(c); }
            Boxes(lb, out var bc, out var bs, out var cc, out var cs);
            var body = f.gameObject.AddComponent<BoxCollider>(); body.center = bc; body.size = bs;
            var cabin = f.gameObject.AddComponent<BoxCollider>(); cabin.center = cc; cabin.size = cs;
            var rb = f.GetComponent<Rigidbody>();
            if (rb) rb.isKinematic = true;
        }
    }
}
