using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// One straight wall line (all its pieces are children). When the wall stands between
    /// the camera and the spot it is looking at, it slides down to a short stub so the room
    /// behind it stays visible. Door lintels and anything above the stub simply hide.
    /// </summary>
    public class WallCutaway : MonoBehaviour
    {
        public static readonly List<WallCutaway> All = new List<WallCutaway>();

        public enum Axis { X, Z }

        [Tooltip("X: the wall runs along Z and sits at x = plane. Z: runs along X and sits at z = plane.")]
        public Axis axis;
        public float plane;
        public int floor;
        public float floorY;
        public float fullHeight = 3f;
        public float stubHeight = 0.25f;
        [Tooltip("Things hung on this wall (pictures, sconces). Hidden while the wall is cut down.")]
        public List<GameObject> attachments = new List<GameObject>();

        struct Piece { public Transform t; public Renderer r; public Collider c; public float bottom, top; }
        readonly List<Piece> pieces = new List<Piece>();
        float height, targetHeight;

        bool ready;

        void Awake()
        {
            Collect();
            height = targetHeight = fullHeight;
            ready = true;
        }

        void Collect()
        {
            pieces.Clear();
            foreach (Transform t in transform)
            {
                float h = t.localScale.y;
                pieces.Add(new Piece
                {
                    t = t,
                    r = t.GetComponent<Renderer>(),
                    c = t.GetComponent<Collider>(),
                    bottom = t.position.y - h * 0.5f,
                    top = t.position.y + h * 0.5f
                });
            }
        }

        /// <summary>Call after the wall's children were rebuilt (moving a window): picks up the new pieces.</summary>
        public void Refresh()
        {
            if (!ready) return;   // Awake will collect them
            Collect();
            Apply();
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        /// <summary>True when the camera and the focus point are on opposite sides of this wall.</summary>
        public bool IsBetween(Vector3 cam, Vector3 focus)
        {
            float c = axis == Axis.X ? cam.x : cam.z;
            float f = axis == Axis.X ? focus.x : focus.z;
            return (c - plane) * (f - plane) < 0f;
        }

        public void SetCut(bool cut) => targetHeight = cut ? stubHeight : fullHeight;

        float savedHeight;

        /// <summary>Puts the wall fully up for a moment (the navigation bake), and back to how it was afterwards.</summary>
        public void BakeMode(bool on)
        {
            if (!ready) return;
            if (on) { savedHeight = height; height = fullHeight; Apply(); }
            else { height = savedHeight; Apply(); }
        }

        void Update()
        {
            if (Mathf.Abs(height - targetHeight) < 0.001f) return;
            height = Mathf.MoveTowards(height, targetHeight, 9f * Time.unscaledDeltaTime);
            Apply();
        }

        void Apply()
        {
            float limit = floorY + height;
            bool wallUp = height > fullHeight - 0.3f;
            foreach (var a in attachments) if (a && a.activeSelf != wallUp) a.SetActive(wallUp);
            foreach (var p in pieces)
            {
                if (!p.t) continue;
                float top = Mathf.Min(p.top, limit);
                bool show = top - p.bottom > 0.01f;
                if (p.r) p.r.enabled = show;
                if (p.c) p.c.enabled = show;
                if (!show) continue;
                var s = p.t.localScale; s.y = top - p.bottom; p.t.localScale = s;
                var pos = p.t.position; pos.y = (p.bottom + top) * 0.5f; p.t.position = pos;
            }
        }
    }
}
