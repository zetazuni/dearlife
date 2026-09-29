using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Chairs snap to tables while they are moved, like The Sims 4: a dining chair near a dining table, a bar stool near
    /// the kitchen island, an office chair near a desk jumps to the nearest free place at it and turns to face it. Places
    /// are spread along every side of the table (one at each end of a dining table). Hold Ctrl to place freely.
    /// </summary>
    public static class SeatSnap
    {
        // which seats go at which tables, and how far a seat's centre stands out from the table's edge
        static readonly Dictionary<string, (string[] tables, float gap)> Rules = new Dictionary<string, (string[], float)>
        {
            { "diningchair", (new[] { "diningtable", "longdining" }, 0.30f) },
            { "barstool", (new[] { "kitchenisland", "bbqcounter" }, 0.34f) },
            { "officechair", (new[] { "officedesk", "teacherdesk" }, 0.42f) },
        };

        const float Spacing = 0.62f;     // the least room each place along a side needs
        const float Reach = 0.5f;        // how close to a place a chair must be dragged to snap to it

        /// <summary>A place at a table for this seat near where it is being dragged, or false.</summary>
        public static bool Find(Furniture seat, Vector3 at, out Vector3 pos, out Quaternion rot)
        {
            pos = at; rot = Quaternion.identity;
            if (!Rules.TryGetValue(InteractionTable.BaseId(seat.name), out var rule)) return false;
            float best = Reach * Reach;
            bool found = false;
            foreach (var t in Furniture.All)
            {
                if (t == seat || !t || !t.isActiveAndEnabled) continue;
                if (System.Array.IndexOf(rule.tables, InteractionTable.BaseId(t.name)) < 0) continue;
                var flat = t.transform.position - at; flat.y = 0f;
                if (flat.sqrMagnitude > 16f || Mathf.Abs(t.transform.position.y - at.y) > 1.2f) continue;
                foreach (var (p, r) in Places(t, rule.gap))
                {
                    var d = p - at; d.y = 0f;
                    if (d.sqrMagnitude >= best || Taken(p, seat)) continue;
                    best = d.sqrMagnitude; pos = p; rot = r; found = true;
                }
            }
            return found;
        }

        /// <summary>Every place round a table: along each side, facing the table.</summary>
        static IEnumerable<(Vector3, Quaternion)> Places(Furniture table, float gap)
        {
            var lb = table.LocalBounds;
            var tr = table.transform;
            var sides = new[] { (Vector3.forward, lb.extents.z, lb.extents.x), (Vector3.back, lb.extents.z, lb.extents.x),
                                (Vector3.right, lb.extents.x, lb.extents.z), (Vector3.left, lb.extents.x, lb.extents.z) };
            foreach (var (outward, depth, halfLength) in sides)
            {
                var along = Vector3.Cross(Vector3.up, outward);
                int n = Mathf.Max(1, Mathf.FloorToInt((halfLength * 2f - 0.1f) / Spacing));
                if (halfLength * 2f < 0.5f) continue;                              // too short a side for anyone
                for (int i = 0; i < n; i++)
                {
                    float u = ((i + 0.5f) / n - 0.5f) * halfLength * 2f;   // spread evenly along the side
                    var local = new Vector3(lb.center.x, 0f, lb.center.z) + outward * (depth + gap) + along * u;
                    var world = tr.TransformPoint(local);
                    world.y = tr.position.y;
                    var face = tr.TransformDirection(-outward); face.y = 0f;
                    yield return (world, Quaternion.LookRotation(face.normalized));
                }
            }
        }

        /// <summary>Another seat already stands at this place.</summary>
        static bool Taken(Vector3 p, Furniture self)
        {
            foreach (var o in Furniture.All)
            {
                if (o == self || !o || !Rules.ContainsKey(InteractionTable.BaseId(o.name))) continue;
                var d = o.transform.position - p; d.y = 0f;
                if (d.sqrMagnitude < 0.3f * 0.3f) return true;
            }
            return false;
        }
    }
}
