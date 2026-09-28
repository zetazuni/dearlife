using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A concrete wall line that has movable windows. It builds its own pieces (solid wall around the door
    /// gaps and the windows, plus each window's glass, slim black frame and oak sill) and rebuilds them when a
    /// window is moved or resized in decorate mode. Window positions are saved in PlayerPrefs.
    /// </summary>
    public class WindowWall : MonoBehaviour
    {
        public static readonly List<WindowWall> All = new List<WindowWall>();

        [Serializable]
        public class Win
        {
            public float center;          // along the wall, metres
            public float width = 1.2f;
            public float sill = 1f;       // height of the bottom above the floor
            public float top = 2.3f;      // height of the top above the floor
            public bool curtainClosed;
            [NonSerialized] public float homeCenter, homeWidth;
        }

        /// <summary>A stretch of the wall with its own interior paint (one per room).</summary>
        [Serializable]
        public class Zone
        {
            public float from, to;
            public Material mat;
        }

        public WallCutaway.Axis axis;
        public float at, thick;
        public float from, to;
        public float floorY;
        public float wallHeight = 3f;
        public float doorHeight = 2.3f;
        public Vector2[] doors = new Vector2[0];
        public int interiorSide = 1;                 // which side of the wall the room is on (+1 or -1)
        public List<Win> windows = new List<Win>();
        public Material wallMat, glassMat, frameMat, sillMat, curtainMat;
        public List<Zone> zones = new List<Zone>();

        readonly List<GameObject> curtainObjects = new List<GameObject>();

        public static readonly float[] Widths = { 0.8f, 1.2f, 1.6f, 2.2f };
        const float MinGap = 0.2f;

        string Key => gameObject.name + "@" + floorY.ToString("0.0");

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            foreach (var w in windows) { w.homeCenter = w.center; w.homeWidth = w.width; }
            // the curtains were built in the editor: pick them up again so clicks can toggle them
            if (transform.parent)
                foreach (var c in transform.parent.GetComponentsInChildren<Curtain>(true))
                {
                    if (c.wall != this) continue;
                    while (curtainObjects.Count <= c.index) curtainObjects.Add(null);
                    curtainObjects[c.index] = c.gameObject;
                }
            if (LoadSaved()) Rebuild();
        }

        // ---------- moving ----------

        public bool CanPlace(int index, float center, float width)
        {
            float a = center - width * 0.5f, b = center + width * 0.5f;
            if (a < from + MinGap || b > to - MinGap) return false;
            foreach (var d in doors) if (b > d.x - MinGap && a < d.y + MinGap) return false;
            for (int i = 0; i < windows.Count; i++)
            {
                if (i == index) continue;
                var o = windows[i];
                if (b > o.center - o.width * 0.5f - MinGap && a < o.center + o.width * 0.5f + MinGap) return false;
            }
            return true;
        }

        public bool TryMove(int index, float center)
        {
            var w = windows[index];
            if (Mathf.Abs(center - w.center) < 0.0005f) return true;
            if (!CanPlace(index, center, w.width)) return false;
            w.center = center;
            Rebuild();
            return true;
        }

        /// <summary>Steps the width through the presets. Returns false if the wider window would not fit.</summary>
        public bool CycleWidth(int index, int dir)
        {
            var w = windows[index];
            int cur = 0;
            for (int i = 0; i < Widths.Length; i++) if (Mathf.Abs(Widths[i] - w.width) < 0.05f) cur = i;
            int next = (cur + dir + Widths.Length) % Widths.Length;
            if (!CanPlace(index, w.center, Widths[next])) return false;
            w.width = Widths[next];
            Rebuild();
            return true;
        }

        /// <summary>Screen rectangle corners of a window in world space (for the outline).</summary>
        public Vector3[] Corners(int index)
        {
            var w = windows[index];
            float u0 = w.center - w.width * 0.5f, u1 = w.center + w.width * 0.5f;
            Vector3 P(float u, float h) => axis == WallCutaway.Axis.X ? new Vector3(at + thick * 0.5f, floorY + h, u) : new Vector3(u, floorY + h, at + thick * 0.5f);
            return new[] { P(u0, w.sill), P(u1, w.sill), P(u1, w.top), P(u0, w.top) };
        }

        public float AlongCoordinate(Vector3 world) => axis == WallCutaway.Axis.X ? world.z : world.x;
        public Plane WallPlane() => new Plane(axis == WallCutaway.Axis.X ? Vector3.right : Vector3.forward, axis == WallCutaway.Axis.X ? new Vector3(at + thick * 0.5f, 0f, 0f) : new Vector3(0f, 0f, at + thick * 0.5f));

        // ---------- building ----------

        public void Rebuild()
        {
            // clear old pieces (detached first so the cut-away sees only the new ones straight away)
            var old = new List<GameObject>();
            foreach (Transform c in transform) old.Add(c.gameObject);
            foreach (var g in old)
            {
                g.SetActive(false);
                g.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(g); else DestroyImmediate(g);
            }

            var cutWall = GetComponent<WallCutaway>();
            foreach (var c in curtainObjects)
            {
                if (!c) continue;
                if (cutWall) cutWall.attachments.Remove(c);
                c.SetActive(false);
                c.transform.SetParent(null, false);
                if (Application.isPlaying) Destroy(c); else DestroyImmediate(c);
            }
            curtainObjects.Clear();

            var cuts = new List<(float a, float b, Win w, bool door)>();
            foreach (var d in doors) cuts.Add((d.x, d.y, null, true));
            foreach (var w in windows) cuts.Add((w.center - w.width * 0.5f, w.center + w.width * 0.5f, w, false));
            cuts.Sort((p, q) => p.a.CompareTo(q.a));

            float cursor = from;
            int n = 0;
            foreach (var c in cuts)
            {
                if (c.a > cursor) Solid($"{name} {n++}", cursor, c.a, 0f, wallHeight);
                if (c.door) Solid($"{name} lintel {n++}", c.a, c.b, doorHeight, wallHeight);
                else
                {
                    var w = c.w;
                    int idx = windows.IndexOf(w);
                    if (w.sill > 0.01f) Solid($"{name} under window {n++}", c.a, c.b, 0f, w.sill);
                    if (w.top < wallHeight - 0.01f) Solid($"{name} over window {n++}", c.a, c.b, w.top, wallHeight);
                    Glazing(idx, w);
                    BuildCurtain(idx, w);
                }
                cursor = c.b;
            }
            if (to > cursor) Solid($"{name} {n}", cursor, to, 0f, wallHeight);

            var cut = GetComponent<WallCutaway>();
            if (cut) cut.Refresh();
        }

        GameObject Box(string nm, Vector3 min, Vector3 max, Material m, bool shadows = true)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cube);
            g.name = nm;
            g.transform.SetParent(transform, false);
            g.transform.position = (min + max) * 0.5f;
            g.transform.localScale = max - min;
            var r = g.GetComponent<Renderer>();
            r.sharedMaterial = m;
            if (!shadows) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return g;
        }

        Vector3 P(float along, float h, float depth) =>
            axis == WallCutaway.Axis.X ? new Vector3(depth, floorY + h, along) : new Vector3(along, floorY + h, depth);

        /// <summary>Concrete wall: a plain outer skin, and on the room side a thicker layer in the room's own paint.</summary>
        void Solid(string nm, float a, float b, float h0, float h1)
        {
            if (zones.Count == 0 || interiorSide < 0)
            {
                Box(nm, P(a, h0, at), P(b, h1, at + thick), wallMat);
                return;
            }
            float split = at + thick * 0.55f;
            int k = 0;
            float cur = a;
            while (cur < b - 0.0005f)
            {
                Zone z = null;
                foreach (var zz in zones) if (cur >= zz.from - 0.0005f && cur < zz.to - 0.0005f) { z = zz; break; }
                float end = z != null ? Mathf.Min(b, z.to) : b;
                if (z == null) foreach (var zz in zones) if (zz.from > cur) end = Mathf.Min(end, zz.from);
                if (end <= cur + 0.0005f) break;
                Box($"{nm} outer {k}", P(cur, h0, at), P(end, h1, split), wallMat);
                Box($"{nm} paint {k}", P(cur, h0, split), P(end, h1, at + thick), z != null && z.mat ? z.mat : wallMat);
                cur = end;
                k++;
            }
        }

        void Glazing(int idx, Win w)
        {
            float a = w.center - w.width * 0.5f, b = w.center + w.width * 0.5f;
            float mid = at + thick * 0.5f;
            const float f = 0.05f, t = 0.012f;
            void Part(string nm, float u0, float h0, float u1, float h1, float d0, float d1, Material m, bool shadows = true)
            {
                var g = Box(nm, P(u0, h0, d0), P(u1, h1, d1), m, shadows);
                var part = g.AddComponent<WallWindowPart>();
                part.wall = this; part.index = idx;
            }
            Part($"Window {idx} glass", a, w.sill, b, w.top, mid - t * 0.5f, mid + t * 0.5f, glassMat, false);
            float d0 = mid - 0.03f, d1 = mid + 0.03f;
            Part($"Window {idx} frame bottom", a, w.sill, b, w.sill + f, d0, d1, frameMat);
            Part($"Window {idx} frame top", a, w.top - f, b, w.top, d0, d1, frameMat);
            Part($"Window {idx} frame left", a, w.sill, a + f, w.top, d0, d1, frameMat);
            Part($"Window {idx} frame right", b - f, w.sill, b, w.top, d0, d1, frameMat);
            if (w.width > 1.3f) Part($"Window {idx} mullion", w.center - f * 0.5f, w.sill, w.center + f * 0.5f, w.top, d0, d1, frameMat);
            // oak sill on the room side, sticking out a little
            float inner = interiorSide > 0 ? at + thick : at;
            float sillOut = interiorSide > 0 ? inner + 0.08f : inner - 0.08f;
            Part($"Window {idx} sill", a - 0.04f, w.sill - 0.03f, b + 0.04f, w.sill, Mathf.Min(inner, sillOut), Mathf.Max(inner, sillOut), sillMat);
        }

        // ---------- curtains ----------

        public void ToggleCurtain(int index)
        {
            if (index < 0 || index >= curtainObjects.Count || !curtainObjects[index]) return;
            var c = curtainObjects[index].GetComponent<Curtain>();
            if (c) c.Toggle();
        }

        void BuildCurtain(int idx, Win w)
        {
            if (!curtainMat) return;
            float inner = interiorSide > 0 ? at + thick : at;
            float depth = inner + interiorSide * 0.17f;
            float a = w.center - w.width * 0.5f, b = w.center + w.width * 0.5f;
            float top = w.top + 0.16f, bottom = Mathf.Max(0.04f, w.sill - 0.18f);

            // a rod with two brackets, part of the wall so it hides with it
            Box($"Curtain rod {idx}", P(a - 0.22f, top + 0.02f, depth - 0.015f), P(b + 0.22f, top + 0.06f, depth + 0.015f), frameMat);

            var root = new GameObject($"Curtain {idx}");
            root.transform.SetParent(transform.parent, false);
            bool xWall = axis == WallCutaway.Axis.X;
            var rot = xWall ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;   // local x runs along the wall, local z into the room
            float dsign = xWall ? -1f : 1f;                                           // local z sign that points into the room
            var cur = root.AddComponent<Curtain>();
            cur.wall = this;
            cur.index = idx;
            cur.left = MakePanel(root.transform, $"Left", P(a - 0.12f, 0f, depth), rot, +1f, (b - a) * 0.5f + 0.17f, bottom, top, dsign);
            cur.right = MakePanel(root.transform, $"Right", P(b + 0.12f, 0f, depth), rot, -1f, (b - a) * 0.5f + 0.17f, bottom, top, dsign);
            cur.SetInstant(w.curtainClosed);
            while (curtainObjects.Count <= idx) curtainObjects.Add(null);
            curtainObjects[idx] = root;
            var cutW = GetComponent<WallCutaway>();
            if (cutW) cutW.attachments.Add(root);
        }

        /// <summary>A gathered curtain panel: many round, overlapping folds that alternate in depth, wider at the bottom, so it looks soft and full.</summary>
        Transform MakePanel(Transform parent, string nm, Vector3 pivot, Quaternion rot, float dir, float length, float bottom, float top, float dsign)
        {
            var p = new GameObject($"Panel {nm}");
            p.transform.SetParent(parent, false);
            p.transform.SetPositionAndRotation(pivot, rot);
            int n = Mathf.Max(7, Mathf.CeilToInt(length / 0.055f));
            float sw = length / n;
            float h = top - bottom, cy = floorY + (bottom + top) * 0.5f - pivot.y;
            var rnd = new System.Random(idxSeed++);
            for (int i = 0; i < n; i++)
            {
                var g = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                g.name = $"Fold {i}";
                g.transform.SetParent(p.transform, false);
                float wave = Mathf.Sin(i * 2.1f) * 0.5f + (i % 2 == 0 ? 0.5f : -0.5f);
                float thick = 0.11f + 0.04f * (float)rnd.NextDouble();
                g.transform.localPosition = new Vector3(dir * (i + 0.5f) * sw, cy, wave * 0.085f * dsign);
                // a capsule is 2 m tall and 1 m wide: scale to the curtain height, keep the folds thick and round
                g.transform.localScale = new Vector3(sw * 1.9f, h * 0.5f, thick * 2.2f);
                g.GetComponent<Renderer>().sharedMaterial = curtainMat;
                if (Application.isPlaying) Destroy(g.GetComponent<Collider>()); else DestroyImmediate(g.GetComponent<Collider>());
            }
            // a soft hem at the bottom and a gathered heading at the top
            var hem = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hem.name = "Heading";
            hem.transform.SetParent(p.transform, false);
            hem.transform.localPosition = new Vector3(dir * length * 0.5f, floorY + top - 0.05f - pivot.y, 0f);
            hem.transform.localScale = new Vector3(length, 0.1f, 0.1f);
            hem.GetComponent<Renderer>().sharedMaterial = curtainMat;
            if (Application.isPlaying) Destroy(hem.GetComponent<Collider>()); else DestroyImmediate(hem.GetComponent<Collider>());
            var box = p.AddComponent<BoxCollider>();
            box.center = new Vector3(dir * length * 0.5f, cy, 0f);
            box.size = new Vector3(length, h, 0.14f);
            return p.transform;
        }

        static int idxSeed = 3;

        // ---------- saving ----------

        static string PrefKey => SaveSystem.Key("dearlife.windows");

        [Serializable] class Entry { public string wall; public int i; public float center, width; }
        [Serializable] class Layout { public int version = 1; public List<Entry> items = new List<Entry>(); }

        public static void SaveAll()
        {
            var l = new Layout();
            foreach (var ww in All)
                for (int i = 0; i < ww.windows.Count; i++)
                {
                    var w = ww.windows[i];
                    if (Mathf.Abs(w.center - w.homeCenter) < 0.001f && Mathf.Abs(w.width - w.homeWidth) < 0.001f) continue;
                    l.items.Add(new Entry { wall = ww.Key, i = i, center = w.center, width = w.width });
                }
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(l));
            PlayerPrefs.Save();
        }

        bool LoadSaved()
        {
            if (!PlayerPrefs.HasKey(PrefKey)) return false;
            Layout l;
            try { l = JsonUtility.FromJson<Layout>(PlayerPrefs.GetString(PrefKey)); } catch { return false; }
            if (l == null || l.items == null) return false;
            bool any = false;
            foreach (var e in l.items)
            {
                if (e.wall != Key || e.i < 0 || e.i >= windows.Count) continue;
                windows[e.i].center = e.center;
                windows[e.i].width = e.width;
                any = true;
            }
            return any;
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            foreach (var ww in All)
            {
                foreach (var w in ww.windows) { w.center = w.homeCenter; w.width = w.homeWidth; }
                ww.Rebuild();
            }
        }
    }
}
