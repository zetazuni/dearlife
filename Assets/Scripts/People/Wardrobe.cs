using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Clothes and hair styles for the realistic MPFB2 people (phase 3 of docs/CHARACTER_PLAN.md), changed while the game
    /// runs. Every piece lives in Resources/Clothes as its own skinned mesh on the same skeleton (made by
    /// tools/blender_mpfb_body.py, listed in wardrobe.json). Putting one on binds its mesh to this person's bones and
    /// hands it to <see cref="BodyShape"/>, so it follows every body slider. The body is never cut: each body vertex
    /// carries a bit per garment that covers it (second UV channel), and the triangles under what is worn are left out,
    /// so skin cannot show through fabric.
    /// </summary>
    [DefaultExecutionOrder(-55)]
    public class Wardrobe : MonoBehaviour
    {
        [System.Serializable] public class Item { public string id, slot; public int bit; public bool hidesHair; }
        [System.Serializable] public class Outfit { public string name; public string[] items; }
        [System.Serializable] public class Manifest { public Item[] items; public Outfit[] outfits; }

        [Tooltip("Outfit from wardrobe.json worn at the start (casual, modest, sleep)")]
        public string outfit = "casual";

        static Manifest manifest;
        readonly Dictionary<string, SkinnedMeshRenderer> worn = new Dictionary<string, SkinnedMeshRenderer>();   // slot -> piece
        readonly Dictionary<string, Item> wornItems = new Dictionary<string, Item>();
        readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
        SkinnedMeshRenderer body;
        int[][] fullTris;          // the body's triangles per submesh, all of them
        int[] vertexBits;          // which garments cover each body vertex
        bool ready;

        public static Manifest Catalogue
        {
            get
            {
                if (manifest == null)
                {
                    var t = Resources.Load<TextAsset>("Clothes/wardrobe");
                    manifest = t ? JsonUtility.FromJson<Manifest>(t.text) : new Manifest { items = new Item[0], outfits = new Outfit[0] };
                }
                return manifest;
            }
        }

        public IEnumerable<string> Worn { get { foreach (var i in wornItems.Values) yield return i.id; } }

        void Awake() { Setup(); if (!string.IsNullOrEmpty(outfit)) Wear(outfit); }

        void Setup()
        {
            if (ready) return;
            ready = true;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.name.EndsWith("_body")) body = r;
                foreach (var b in r.bones) if (b) bones[b.name] = b;
            }
            if (!body) return;
            // BodyShape gives every person their own copy of the body mesh; without it, make one here (the triangles change)
            var shape = GetComponent<BodyShape>();
            if (shape) shape.EnsureCaptured(); else body.sharedMesh = Instantiate(body.sharedMesh);
            var mesh = body.sharedMesh;
            fullTris = new int[mesh.subMeshCount][];
            for (int s = 0; s < mesh.subMeshCount; s++) fullTris[s] = mesh.GetTriangles(s);
            var uv = new List<Vector2>();
            mesh.GetUVs(1, uv);
            vertexBits = new int[mesh.vertexCount];
            for (int i = 0; i < uv.Count && i < vertexBits.Length; i++) vertexBits[i] = Mathf.RoundToInt(uv[i].x);
        }

        static Item Find(string id)
        {
            foreach (var i in Catalogue.items) if (i.id == id) return i;
            return null;
        }

        readonly Dictionary<string, Color> colours = new Dictionary<string, Color>();

        /// <summary>Changes into exactly these pieces (a person's own everyday or sleep clothes): everything else comes off.</summary>
        public void Wear(IList<string> items)
        {
            Setup();
            outfit = "";
            foreach (var slot in new List<string>(worn.Keys)) Remove(slot, false);
            foreach (var id in items) PutOn(id, false);
            Refresh();
        }

        /// <summary>
        /// A colour for one piece (a garment, or the hair), kept while it is taken off and put on again. Fabrics are tinted
        /// (the colour replaces a plain garment's colour and multiplies a printed one); hair takes it as its tint.
        /// </summary>
        public void SetColour(string id, Color c)
        {
            colours[id] = c;
            foreach (var kv in wornItems)
                if (kv.Value.id == id && worn.TryGetValue(kv.Key, out var r) && r) Tint(r, c);
        }

        public bool TryGetColour(string id, out Color c) => colours.TryGetValue(id, out c);

        static void Tint(Renderer r, Color c)
        {
            foreach (var m in r.materials)          // this person's own copies
                if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        }

        /// <summary>Changes into a whole outfit from wardrobe.json: everything else comes off.</summary>
        public bool Wear(string outfitName)
        {
            Setup();
            Outfit o = null;
            foreach (var x in Catalogue.outfits) if (x.name == outfitName) o = x;
            if (o == null) return false;
            outfit = outfitName;
            foreach (var slot in new List<string>(worn.Keys)) Remove(slot, false);
            foreach (var id in o.items) PutOn(id, false);
            Refresh();
            return true;
        }

        /// <summary>Puts one piece on, taking off whatever it replaces (a full outfit replaces a top and a bottom and back).</summary>
        public bool PutOn(string id) => PutOn(id, true);

        bool PutOn(string id, bool refresh)
        {
            Setup();
            var item = Find(id);
            if (item == null || !body) return false;
            var prefab = Resources.Load<GameObject>("Clothes/" + id);
            if (!prefab) return false;
            foreach (var slot in Clashes(item.slot)) Remove(slot, false);

            var src = prefab.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (!src) return false;
            var go = new GameObject("wear_" + id);
            go.transform.SetParent(body.transform.parent, false);
            go.transform.localPosition = body.transform.localPosition;
            go.transform.localRotation = body.transform.localRotation;
            go.transform.localScale = body.transform.localScale;
            go.layer = body.gameObject.layer;
            var r = go.AddComponent<SkinnedMeshRenderer>();
            var mapped = new Transform[src.bones.Length];
            for (int i = 0; i < mapped.Length; i++)
                if (src.bones[i] && bones.TryGetValue(src.bones[i].name, out var b)) mapped[i] = b;
            r.sharedMesh = src.sharedMesh;
            r.bones = mapped;
            r.rootBone = src.rootBone && bones.TryGetValue(src.rootBone.name, out var rb) ? rb : body.rootBone;
            r.sharedMaterials = src.sharedMaterials;
            r.updateWhenOffscreen = body.updateWhenOffscreen;
            r.localBounds = body.localBounds;
            r.shadowCastingMode = body.shadowCastingMode;
            var shape = GetComponent<BodyShape>();
            if (shape) shape.AddSkin(r);     // its own copy of the mesh, shaped like the body
            else r.sharedMesh = Instantiate(r.sharedMesh);
            worn[item.slot] = r;
            wornItems[item.slot] = item;
            if (colours.TryGetValue(id, out var col)) Tint(r, col);
            if (refresh) Refresh();
            return true;
        }

        /// <summary>Takes off whatever is in a slot (top, bottom, outfit, head, feet, hair).</summary>
        public void TakeOff(string slot) => Remove(slot, true);

        void Remove(string slot, bool refresh)
        {
            if (!worn.TryGetValue(slot, out var r)) return;
            var shape = GetComponent<BodyShape>();
            if (shape) shape.RemoveSkin(r);
            if (r) { Destroy(r.sharedMesh); Destroy(r.gameObject); }
            worn.Remove(slot);
            wornItems.Remove(slot);
            if (refresh) Refresh();
        }

        static IEnumerable<string> Clashes(string slot)
        {
            yield return slot;
            if (slot == "outfit") { yield return "top"; yield return "bottom"; }
            if (slot == "top" || slot == "bottom") yield return "outfit";
        }

        /// <summary>Hides the body under what is worn, and the hair under a head covering.</summary>
        void Refresh()
        {
            if (!body || fullTris == null) return;
            int mask = 0;
            bool hideHair = false;
            foreach (var i in wornItems.Values)
            {
                if (i.bit >= 0) mask |= 1 << i.bit;
                hideHair |= i.hidesHair;
            }
            var mesh = body.sharedMesh;
            var keep = new List<int>();
            for (int s = 0; s < fullTris.Length; s++)
            {
                keep.Clear();
                var t = fullTris[s];
                for (int k = 0; k < t.Length; k += 3)
                {
                    // a triangle goes only when all three corners are under something worn
                    if ((vertexBits[t[k]] & mask) != 0 && (vertexBits[t[k + 1]] & mask) != 0 && (vertexBits[t[k + 2]] & mask) != 0) continue;
                    keep.Add(t[k]); keep.Add(t[k + 1]); keep.Add(t[k + 2]);
                }
                mesh.SetTriangles(keep, s, false);
            }
            // the whole object, not just the renderer: Character switches every renderer on and off for its floor view
            if (worn.TryGetValue("hair", out var hair) && hair) hair.gameObject.SetActive(!hideHair);
        }

        void OnDestroy()
        {
            foreach (var r in worn.Values) if (r) Destroy(r.sharedMesh);
        }
    }
}
