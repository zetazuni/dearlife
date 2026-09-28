using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife
{
    /// <summary>
    /// The Sims style hover glow: the thing under the mouse (something to do in live mode, anything movable in decorate mode) and the
    /// piece being carried get a soft white, slightly bigger see-through shell that eases in and out. The shell is a copy of the piece's
    /// meshes with no colliders, so the real piece, its physics and its neighbours are never touched.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class HoverHighlight : MonoBehaviour
    {
        public static HoverHighlight Instance { get; private set; }

        const float MaxAlpha = 0.30f;      // how white it gets
        const float Grow = 0.035f;         // how far the shell stands out, in metres
        const float Ease = 14f;            // higher = snappier fade

        class Glow
        {
            public object key;
            public System.Func<List<Renderer>> sources;
            public readonly List<(Renderer src, Transform dst)> parts = new List<(Renderer, Transform)>();
            public GameObject root;
            public Material mat;
            public float w;                // 0 hidden .. 1 fully lit
        }

        readonly Dictionary<object, Glow> glows = new Dictionary<object, Glow>();
        readonly List<object> dead = new List<object>();
        static Shader unlit;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Instance) return;
            var go = new GameObject("Hover highlight");
            DontDestroyOnLoad(go);
            go.AddComponent<HoverHighlight>();
        }

        void Awake() { Instance = this; }

        void LateUpdate()
        {
            object key = null; System.Func<List<Renderer>> sources = null;
            PickTarget(ref key, ref sources);

            if (key != null && !glows.ContainsKey(key) && sources != null)
                glows[key] = new Glow { key = key, sources = sources };

            float k = 1f - Mathf.Exp(-Ease * Time.unscaledDeltaTime);
            dead.Clear();
            foreach (var g in glows.Values)
            {
                bool on = Equals(g.key, key);
                g.w = Mathf.Lerp(g.w, on ? 1f : 0f, k);
                if (!on && g.w < 0.002f) { g.w = 0f; if (g.root) g.root.SetActive(false); if (IsGone(g.key)) dead.Add(g.key); continue; }
                if (!Follow(g)) dead.Add(g.key);
            }
            foreach (var d in dead) { if (glows.TryGetValue(d, out var g)) { if (g.root) Destroy(g.root); if (g.mat) Destroy(g.mat); } glows.Remove(d); }
        }

        static bool IsGone(object key)
        {
            if (key is Furniture f) return !f;
            if (key is System.ValueTuple<WindowWall, int> w) return !w.Item1;
            return true;
        }

        // ------------------------------------------------------------------ what is lit

        void PickTarget(ref object key, ref System.Func<List<Renderer>> sources)
        {
            if (MainMenu.Busy || Splash.Showing || SettingsWindow.Open || BuildMode.Active) return;
            var cam = Camera.main;
            if (!cam) return;

            var dec = DecorateMode.Instance;
            if (DecorateMode.Active && dec)
            {
                if (dec.HeldPiece) { SetPiece(dec.HeldPiece, ref key, ref sources); return; }
                var hw = dec.HeldWindow;
                if (hw.wall) { SetWindow(hw.wall, hw.index, ref key, ref sources); return; }
                if (OverUi()) return;
                if (dec.HoverTarget(out var piece, out var win))
                {
                    if (win) SetWindow(win.wall, win.index, ref key, ref sources);
                    else SetPiece(piece, ref key, ref sources);
                }
                return;
            }

            // live mode: only things that offer something to do, and the one whose round menu is open stays lit
            var menu = LiveMode.MenuPiece;
            if (menu) { SetPiece(menu, ref key, ref sources); return; }
            if (OverUi() || LiveMode.Instance == null) return;
            if (LiveMode.Instance.CharacterUnder(Input.mousePosition) != null) return;   // a click here goes to the person, not the furniture
            if (!Physics.Raycast(cam.ScreenPointToRay(Input.mousePosition), out var hit, 400f, ~0, QueryTriggerInteraction.Ignore)) return;
            Furniture target = null;
            var tv = hit.collider.GetComponentInParent<TvScreen>();
            if (tv) target = tv.GetComponentInParent<Furniture>();
            if (!target) target = LiveMode.SeatOwner(hit.collider.transform);
            if (target) SetPiece(target, ref key, ref sources);
        }

        static bool OverUi()
        {
            var m = Input.mousePosition;
            return OrbitCamera.IsOverUi != null && OrbitCamera.IsOverUi(new Vector2(m.x, Screen.height - m.y));
        }

        static void SetPiece(Furniture f, ref object key, ref System.Func<List<Renderer>> sources)
        {
            key = f;
            sources = () => PieceRenderers(f);
        }

        static void SetWindow(WindowWall wall, int index, ref object key, ref System.Func<List<Renderer>> sources)
        {
            key = (wall, index);
            sources = () => WindowRenderers(wall, index);
        }

        static List<Renderer> PieceRenderers(Furniture f)
        {
            var list = new List<Renderer>();
            if (!f || !f.gameObject.activeInHierarchy) return list;
            foreach (var r in f.GetComponentsInChildren<MeshRenderer>())
                if (r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh) list.Add(r);
            return list;
        }

        static List<Renderer> WindowRenderers(WindowWall wall, int index)
        {
            var list = new List<Renderer>();
            if (!wall) return list;
            foreach (var p in wall.GetComponentsInChildren<WallWindowPart>())
            {
                if (p.index != index) continue;
                foreach (var r in p.GetComponentsInChildren<MeshRenderer>())
                    if (r.enabled && r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh) list.Add(r);
            }
            return list;
        }

        // ------------------------------------------------------------------ the shell

        /// <summary>Keeps the shell on top of its piece. False when the piece is gone for good.</summary>
        bool Follow(Glow g)
        {
            bool stale = g.parts.Count == 0;
            foreach (var p in g.parts) if (!p.src || !p.src.enabled || !p.src.gameObject.activeInHierarchy) { stale = true; break; }
            if (stale && !Rebuild(g)) return false;

            if (!g.root.activeSelf) g.root.SetActive(true);
            // one bounding box round the whole piece: the shell grows about its middle
            var b = g.parts[0].src.bounds;
            for (int i = 1; i < g.parts.Count; i++) b.Encapsulate(g.parts[i].src.bounds);
            float ext = Mathf.Max(0.05f, Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)));
            float e = Mathf.SmoothStep(0f, 1f, g.w);
            float s = 1f + Mathf.Clamp(Grow / ext, 0.012f, 0.06f) * e;
            var c = b.center;
            foreach (var p in g.parts)
            {
                var st = p.src.transform;
                p.dst.SetPositionAndRotation(c + (st.position - c) * s, st.rotation);
                p.dst.localScale = st.lossyScale * s;
            }
            g.mat.SetColor("_UnlitColor", new Color(1f, 1f, 1f, MaxAlpha * e));
            return true;
        }

        bool Rebuild(Glow g)
        {
            if (g.root) Destroy(g.root);
            g.parts.Clear();
            var srcs = g.sources != null ? g.sources() : null;
            if (srcs == null || srcs.Count == 0) return false;
            if (!g.mat) g.mat = MakeMaterial();
            g.root = new GameObject("Glow");
            g.root.transform.SetParent(transform, false);
            foreach (var r in srcs)
            {
                var mf = r.GetComponent<MeshFilter>();
                var go = new GameObject(r.name);
                go.transform.SetParent(g.root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var mr = go.AddComponent<MeshRenderer>();
                var mats = new Material[Mathf.Max(1, mf.sharedMesh.subMeshCount)];
                for (int i = 0; i < mats.Length; i++) mats[i] = g.mat;
                mr.sharedMaterials = mats;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                mr.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
                mr.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
                mr.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
                g.parts.Add((r, go.transform));
            }
            return true;
        }

        static Material MakeMaterial()
        {
            if (!unlit) unlit = Shader.Find("HDRP/Unlit");
            var m = new Material(unlit) { name = "Hover glow" };
            HDMaterial.SetSurfaceType(m, true);
            m.SetColor("_UnlitColor", new Color(1f, 1f, 1f, 0f));
            HDMaterial.ValidateMaterial(m);
            return m;
        }
    }
}
