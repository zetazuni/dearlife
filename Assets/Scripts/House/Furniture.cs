using System;
using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Marks a piece the player can pick up and move in decorate mode. The builder adds it to every piece it
    /// places and gives it a stable key ("sofa#0"), which is what the saved layout is filed under.
    /// </summary>
    public class Furniture : MonoBehaviour
    {
        public static readonly List<Furniture> All = new List<Furniture>();

        [Tooltip("Stable id used in the saved layout, made by the builder.")]
        public string key;
        [Tooltip("Built in pieces (kitchen run, shower, lamps hung from the ceiling) cannot be moved.")]
        public bool pinned;

        [Tooltip("A small thing (cushion, book, mug, lamp): it becomes part of the piece it rests on.")]
        public bool small;
        [NonSerialized] public Furniture attachedTo;
        [NonSerialized] public bool bought;          // bought in buy mode (saved with the purchases, not with the layout)
        [NonSerialized] public int pendingPrice;      // charged when it is put down
        [NonSerialized] public Vector3 homePos;
        [NonSerialized] public Quaternion homeRot;
        Bounds local;
        bool haveLocal;

        // ---------- colours (like the swatches of the Sims 4): channel 0 is what is soft or painted, channel 1 is wood and stone ----------

        /// <summary>The colours to pick from (hex, no #). The first one of each row is "as it came".</summary>
        public static readonly string[] Fabrics =
        {
            "f3ead8", "ffffff", "f4a6b7", "d9607f", "f28b6b", "e6b23a", "a9c8a0", "4f7a58", "4aa8a0", "8fbfe8", "2f4a7a", "b9a4e0", "7a4a86", "c46a4a", "45464c", "1e1e22",
        };
        public static readonly string[] Woods =
        {
            "ffffff", "d9b48a", "b07850", "8a5a3a", "5e3d2a", "3a2a22", "cfcfd6", "7d7f86", "1f1f24",
        };

        [NonSerialized] public string tintA = "", tintB = "";
        struct Slot { public Material m; public string prop; public Color original; public int channel; }
        List<Slot> slots;

        static int ChannelOf(string material)
        {
            string n = material.Replace(" (Instance)", "");
            if (n.Contains("Walnut") || n.Contains("Teak") || n.Contains("Rattan") || n.Contains("Marble_main") || n.Contains("StoneGrey") || n.Contains("Ceramic")) return 1;
            if (n.Contains("Fabric") || n.Contains("Cushion") || n.Contains("Bedding") || n.Contains("Leather") || n == "Rug_main" || n.Contains("Mat_main") || n.Contains("Beanbag")
                || n.Contains("Umbrella") || n.Contains("Cabinet_main") || n.Contains("Planter_main") || n == "PaintRed" || n == "Orange" || n.Contains("Car_main") || n.Contains("Flamingo")) return 0;
            return -1;
        }

        void BuildSlots()
        {
            if (slots != null) return;
            slots = new List<Slot>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                if (r is ParticleSystemRenderer) continue;
                var mats = r.materials;                    // our own copies, so the shared ones are never changed
                foreach (var m in mats)
                {
                    int ch = m ? ChannelOf(m.name) : -1;
                    if (ch < 0) continue;
                    string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : m.HasProperty("baseColorFactor") ? "baseColorFactor" : null;   // HDRP Lit, or the glTF shader of imported models
                    if (prop == null) continue;
                    slots.Add(new Slot { m = m, prop = prop, original = m.GetColor(prop), channel = ch });
                }
            }
        }

        /// <summary>Whether this piece has anything that can be recoloured on that channel.</summary>
        public bool HasChannel(int ch)
        {
            BuildSlots();
            foreach (var s in slots) if (s.channel == ch) return true;
            return false;
        }

        public string TintOf(int ch) => ch == 0 ? tintA : tintB;

        /// <summary>Sets one channel to a colour ("" puts it back as it came).</summary>
        public void SetTint(int ch, string hex)
        {
            BuildSlots();
            if (ch == 0) tintA = hex ?? ""; else tintB = hex ?? "";
            Color c = default; bool have = !string.IsNullOrEmpty(hex) && ColorUtility.TryParseHtmlString("#" + hex, out c);
            foreach (var s in slots)
            {
                if (s.channel != ch) continue;
                s.m.SetColor(s.prop, have ? new Color(c.r, c.g, c.b, s.original.a) : s.original);
            }
        }

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            homePos = transform.position;
            homeRot = transform.rotation;
        }

        /// <summary>Exact bounding box in this piece's own space (from the meshes), so the outline follows its rotation.</summary>
        public Bounds LocalBounds
        {
            get
            {
                if (haveLocal) return local;
                bool first = true;
                foreach (var mf in GetComponentsInChildren<MeshFilter>())
                {
                    if (!mf.sharedMesh) continue;
                    var b = mf.sharedMesh.bounds;
                    for (int i = 0; i < 8; i++)
                    {
                        var c = new Vector3((i & 1) == 0 ? b.min.x : b.max.x, (i & 2) == 0 ? b.min.y : b.max.y, (i & 4) == 0 ? b.min.z : b.max.z);
                        var l = transform.InverseTransformPoint(mf.transform.TransformPoint(c));
                        if (first) { local = new Bounds(l, Vector3.zero); first = false; }
                        else local.Encapsulate(l);
                    }
                }
                if (first) local = new Bounds(Vector3.zero, Vector3.one * 0.3f);
                haveLocal = true;
                return local;
            }
        }

        public string Label
        {
            get
            {
                string n = key;
                int h = n.IndexOf('#');
                if (h > 0) n = n.Substring(0, h);
                n = n.Replace('_', ' ');
                return n.Length > 0 ? char.ToUpper(n[0]) + n.Substring(1) : n;
            }
        }

        // ---------- saved layout (PlayerPrefs, new fields must keep defaults, the key is never renamed) ----------

        static string PrefKey => SaveSystem.Key("dearlife.layout");

        [Serializable] class Entry { public string key; public Vector3 pos; public float yaw; public string host; public string ta, tb; }
        [Serializable] class Layout { public int version = 1; public List<Entry> items = new List<Entry>(); }

        /// <summary>The piece a small thing rests on (a cushion on a sofa), or null.</summary>
        static Furniture HostOf(Furniture f, bool includePinned = false)
        {
            Furniture best = null;
            float bestTop = -1f;
            foreach (var h in All)
            {
                if (h == f || (h.pinned && !includePinned) || h.small || h.attachedTo) continue;
                var lb = h.LocalBounds;
                var l = h.transform.InverseTransformPoint(f.transform.position);
                if (Mathf.Abs(l.x - lb.center.x) > lb.extents.x || Mathf.Abs(l.z - lb.center.z) > lb.extents.z) continue;
                if (l.y < lb.min.y + 0.05f || l.y > lb.max.y + 0.12f) continue;
                if (lb.max.y > bestTop) { best = h; bestTop = lb.max.y; }
            }
            return best;
        }

        public static void SaveAll()
        {
            var l = new Layout();
            foreach (var f in All)
            {
                if (f.pinned || f.attachedTo || f.bought) continue;
                bool tinted = !string.IsNullOrEmpty(f.tintA) || !string.IsNullOrEmpty(f.tintB);
                if (!tinted && (f.transform.position - f.homePos).sqrMagnitude < 1e-4f && Quaternion.Angle(f.transform.rotation, f.homeRot) < 0.1f) continue;
                var e = new Entry { key = f.key, pos = f.transform.position, yaw = f.transform.eulerAngles.y, ta = f.tintA, tb = f.tintB };
                if (f.GetComponent<StickyProp>())
                {
                    var host = HostOf(f);
                    if (host)
                    {
                        e.host = host.key;
                        e.pos = host.transform.InverseTransformPoint(f.transform.position);
                        e.yaw = Mathf.DeltaAngle(host.transform.eulerAngles.y, f.transform.eulerAngles.y);
                    }
                }
                l.items.Add(e);
            }
            PlayerPrefs.SetString(PrefKey, JsonUtility.ToJson(l));
            PlayerPrefs.Save();
        }

        /// <summary>Puts every piece where the player left it. Returns how many moved.</summary>
        public static int LoadAll()
        {
            Physics.SyncTransforms();
            int n = 0;
            if (PlayerPrefs.HasKey(PrefKey))
            {
                Layout l = null;
                try { l = JsonUtility.FromJson<Layout>(PlayerPrefs.GetString(PrefKey)); } catch { }
                if (l != null && l.items != null)
                {
                    var byKey = new Dictionary<string, Furniture>();
                    foreach (var f in All) byKey[f.key] = f;
                    // hosts and loose pieces first, then the small things that rest on them
                    for (int pass = 0; pass < 2; pass++)
                        foreach (var e in l.items)
                        {
                            bool sticky = !string.IsNullOrEmpty(e.host);
                            if (sticky != (pass == 1)) continue;
                            if (!byKey.TryGetValue(e.key, out var f) || f.pinned) continue;
                            if (!string.IsNullOrEmpty(e.ta)) f.SetTint(0, e.ta);
                            if (!string.IsNullOrEmpty(e.tb)) f.SetTint(1, e.tb);
                            if (sticky)
                            {
                                if (!byKey.TryGetValue(e.host, out var h)) continue;
                                f.Place(h.transform.TransformPoint(e.pos), Quaternion.Euler(0f, h.transform.eulerAngles.y + e.yaw, 0f));
                            }
                            else f.Place(e.pos, Quaternion.Euler(0f, e.yaw, 0f));
                            n++;
                        }
                }
            }
            Physics.SyncTransforms();
            return n;
        }

        /// <summary>Drops every small thing straight down onto what is under it, so nothing hangs in the air.</summary>
        public static void SettleSmallThings()
        {
            Physics.SyncTransforms();
            foreach (var st in UnityEngine.Object.FindObjectsByType<StickyProp>(FindObjectsInactive.Exclude)) st.Settle();
        }

        /// <summary>
        /// Makes every small thing part of the piece under it: it is parented to it and loses its own body, so its
        /// colliders join the piece's. From then on it can never move relative to the sofa, table or counter.
        /// </summary>
        public static void AttachSmallThings()
        {
            foreach (var f in new System.Collections.Generic.List<Furniture>(All))
            {
                if (f.attachedTo) continue;
                if (!f.key.ToLower().Contains("cushion")) continue;   // only cushions are part of the sofa, everything else can be picked up on its own
                var host = HostOf(f, true);
                if (!host) continue;
                foreach (var st in f.GetComponentsInChildren<StickyProp>()) Destroy(st);
                foreach (var rb in f.GetComponentsInChildren<Rigidbody>()) Destroy(rb);
                f.transform.SetParent(host.transform, true);
                f.attachedTo = host;
            }
        }

        public static void ResetAll()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            foreach (var f in All) if (!f.pinned && !f.attachedTo) { f.Place(f.homePos, f.homeRot); f.SetTint(0, ""); f.SetTint(1, ""); }
            Physics.SyncTransforms();
        }

        /// <summary>Moves the piece and calms every body in it.</summary>
        public void Place(Vector3 pos, Quaternion rot)
        {
            transform.SetPositionAndRotation(pos, rot);
            foreach (var st in GetComponentsInChildren<StickyProp>()) st.Anchor();   // small things take their new spot as home
            foreach (var rb in GetComponentsInChildren<Rigidbody>())
            {
                if (rb.isKinematic) continue;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        }
    }
}
