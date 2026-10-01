using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Dearlife
{
    /// <summary>
    /// Fewer things to draw (v0.53.0). The house had over 5,500 separate renderers (one flower bed alone 1,075, a bought
    /// car 196), and the frame time followed that number, not the number of triangles: hiding 2,000 of them took a frame
    /// from 40 ms to 28. So the parts of a piece of furniture that never move on their own are joined into one mesh per
    /// material, in the piece's own space (it still moves, turns and is recoloured as one). The colliders, the piece's
    /// size and everything that has a script of its own (a lamp that glows at night, a door, a cushion that can be
    /// picked up) are left exactly as they were.
    /// </summary>
    public static class MeshMerge
    {
        /// <summary>A piece with fewer parts than this is not worth joining.</summary>
        const int Worth = 6;

        /// <summary>Pieces whose parts are shown, hidden or moved one by one by code elsewhere.</summary>
        static readonly HashSet<string> Never = new HashSet<string> { "guitar", "bathtub", "shower", "tvunit", "fountain", "shed", "pool" };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Boot()
        {
            SceneManager.sceneLoaded -= OnScene;
            SceneManager.sceneLoaded += OnScene;
        }

        // after every Awake of the scene and before any Start: the seasons (SeasonCycle.Start) then find the joined
        // renderers, with the materials they look for, instead of the thousand parts
        static void OnScene(Scene scene, LoadSceneMode mode)
        {
            int pieces = 0, before = 0, after = 0;
            foreach (var f in Object.FindObjectsByType<Furniture>(FindObjectsInactive.Include))
            {
                if (!f || f.gameObject.scene != scene) continue;
                if (Piece(f.gameObject, out int b, out int a)) { pieces++; before += b; after += a; }
            }
            if (pieces > 0) Debug.Log($"Dearlife: joined the parts of {pieces} pieces of furniture, {before} renderers became {after}.");
        }

        public static bool Piece(GameObject root) => Piece(root, out _, out _);

        /// <summary>Joins the still parts of one piece. False when there was nothing worth joining.</summary>
        public static bool Piece(GameObject root, out int before, out int after)
        {
            before = after = 0;
            if (!root || root.transform.Find("Joined") || Never.Contains(InteractionTable.BaseId(root.name))) return false;
            // a piece run by a script of its own keeps its parts: that script may reach for them
            foreach (var mb in root.GetComponents<MonoBehaviour>())
                if (mb && !(mb is Furniture) && !(mb is Interactable) && !(mb is StickyProp) && !(mb is PetThing)) return false;

            var groups = new Dictionary<Material, List<CombineInstance>>();
            var sources = new List<MeshRenderer>();
            var toRoot = root.transform.worldToLocalMatrix;
            bool shadows = false; int layer = root.layer;
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!Still(r, root.transform)) continue;
                var mf = r.GetComponent<MeshFilter>();
                var mesh = mf ? mf.sharedMesh : null;
                if (!mesh || !mesh.isReadable) continue;
                var m = toRoot * r.transform.localToWorldMatrix;
                if (m.determinant < 0f) continue;                 // a mirrored part would come out inside out
                var mats = r.sharedMaterials;
                bool usable = mats.Length > 0;
                for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++) if (!mats[i]) usable = false;
                if (!usable) continue;
                for (int i = 0; i < mats.Length && i < mesh.subMeshCount; i++)
                {
                    if (!groups.TryGetValue(mats[i], out var list)) groups[mats[i]] = list = new List<CombineInstance>();
                    list.Add(new CombineInstance { mesh = mesh, subMeshIndex = i, transform = m });
                }
                sources.Add(r);
                if (r.shadowCastingMode != ShadowCastingMode.Off) shadows = true;
                layer = r.gameObject.layer;
            }
            if (sources.Count < Worth || groups.Count >= sources.Count) return false;

            var holder = new GameObject("Joined");
            holder.transform.SetParent(root.transform, false);
            holder.layer = layer;
            foreach (var kv in groups)
            {
                long verts = 0;
                foreach (var ci in kv.Value) verts += ci.mesh.vertexCount;
                var mesh = new Mesh { name = root.name + " " + kv.Key.name, indexFormat = verts > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                var go = new GameObject(kv.Key.name);
                go.transform.SetParent(holder.transform, false);
                go.layer = layer;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = kv.Key;
                mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            }
            // the parts stay (their colliders and their size are still used), only what drew them goes
            foreach (var r in sources) { r.enabled = false; Object.Destroy(r); }
            before = sources.Count; after = groups.Count;
            return true;
        }

        /// <summary>A plain part: nothing between it and the piece carries a script, a body of its own, a light or an
        /// animation, and it is not a smaller piece standing on this one.</summary>
        static bool Still(MeshRenderer r, Transform root)
        {
            if (!r.enabled || r.name.StartsWith("G_")) return false;
            for (var t = r.transform; t != null && t != root; t = t.parent)
            {
                if (!t.gameObject.activeSelf || t.name == "Joined") return false;
                if (t.GetComponent<MonoBehaviour>() || t.GetComponent<Rigidbody>() || t.GetComponent<Animator>() || t.GetComponent<Light>() || t.GetComponent<ParticleSystem>()) return false;
            }
            return true;
        }
    }
}
