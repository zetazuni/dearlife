using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Places the photoscanned Poly Haven models (glTF, imported by glTFast) from tools/fetch_models.py.
    /// Many of those files hold several variants side by side (plant_a, plant_b...), so a placement can
    /// keep just one variant. The kept part is re-centred so its footprint centre sits on the given spot.
    /// </summary>
    public static class PropPlacer
    {
        const string Dir = "Assets/Art/Models/PolyHaven";

        public enum Body
        {
            Static,        // never moves; collider on the lowest part only (pot, trunk)
            Dynamic,       // one rigidbody with one box around the whole thing
            DynamicParts,  // every child is its own rigidbody (books on a shelf)
            None,          // purely visual (pictures on walls)
        }

        public struct Prop
        {
            public string id;        // Poly Haven id
            public string variant;   // keep only children whose name ends with this (null = keep all)
            public Vector3 at;       // footprint centre, y = the surface it stands on
            public float rot;        // degrees around Y (0 = faces +Z, the garden)
            public float scale;
            public Body body;
            public float mass;       // kg (per part for DynamicParts)
            public float colliderHeight; // Static: collider covers this many metres from the bottom (0 = all)
        }

        public static GameObject Place(Prop p, Transform parent)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>($"{Dir}/{p.id}/{p.id}.gltf");
            if (!asset) { Debug.LogWarning($"Dearlife: prop {p.id} not found, run tools/fetch_models.py."); return null; }

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            if (!string.IsNullOrEmpty(p.variant))
            {
                var drop = new List<GameObject>();
                foreach (Transform c in inst.transform)
                    if (!c.name.EndsWith(p.variant)) drop.Add(c.gameObject);
                foreach (var d in drop) Object.DestroyImmediate(d);
            }

            // wrap in a root whose origin is the footprint centre on the floor
            var root = new GameObject(string.IsNullOrEmpty(p.variant) ? p.id : $"{p.id} {p.variant.Trim('_')}");
            root.transform.SetParent(parent, false);
            inst.transform.localScale = Vector3.one * (p.scale <= 0f ? 1f : p.scale);
            var b = BoundsOf(inst);
            var trunk = TrunkCenter(inst);   // centre plants on the trunk or stem, not on the leaves, so they sit in the middle of their pot
            inst.transform.position -= new Vector3(trunk.HasValue ? trunk.Value.x : b.center.x, b.min.y, trunk.HasValue ? trunk.Value.z : b.center.z);
            inst.transform.SetParent(root.transform, true);
            root.transform.position = p.at;
            root.transform.rotation = Quaternion.Euler(0f, p.rot, 0f);

            // Foliage and wall decor are millions of triangles that never move; leaving them out of the
            // ray tracing structure keeps the Ultra mode affordable (they still show in screen space reflections).
            if (p.body == Body.Static || p.body == Body.None)
                foreach (var r in root.GetComponentsInChildren<Renderer>())
                    r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;

            AddPhysics(root, inst, p);
            return root;
        }

        static Vector3? TrunkCenter(GameObject go)
        {
            Bounds? t = null;
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                string n = r.name.ToLower();
                if (!(n.Contains("bark") || n.Contains("trunk") || n.Contains("stem"))) continue;
                if (t == null) t = r.bounds; else { var bb = t.Value; bb.Encapsulate(r.bounds); t = bb; }
            }
            return t.HasValue ? t.Value.center : (Vector3?)null;
        }

        static Bounds BoundsOf(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            var b = rs.Length > 0 ? rs[0].bounds : new Bounds(go.transform.position, Vector3.zero);
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        static void AddPhysics(GameObject root, GameObject inst, Prop p)
        {
            if (p.body == Body.None) return;
            var fabric = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Art/Physics/Fabric.asset");
            var wood = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Art/Physics/Wood.asset");

            if (p.body == Body.DynamicParts)
            {
                foreach (Transform c in inst.transform)
                {
                    var mf = c.GetComponentInChildren<MeshFilter>();
                    if (!mf) continue;
                    var bc = mf.gameObject.AddComponent<BoxCollider>();
                    bc.center = mf.sharedMesh.bounds.center;
                    bc.size = mf.sharedMesh.bounds.size;
                    bc.sharedMaterial = wood;
                    var rb = c.gameObject.AddComponent<Rigidbody>();
                    rb.mass = p.mass;
                    rb.interpolation = RigidbodyInterpolation.Interpolate;
                    rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
                    PhysicsSetup.Steady(rb);
                    PhysicsSetup.MakeSticky(rb, p.mass);
                }
                return;
            }

            // one box in the root's local space, around everything (or just the bottom for static plants)
            var world = BoundsOf(inst);
            var box = root.AddComponent<BoxCollider>();
            Vector3 size = root.transform.InverseTransformVector(world.size);
            size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            Vector3 center = root.transform.InverseTransformPoint(world.center);
            if (p.body == Body.Static && p.colliderHeight > 0f)
            {
                float h = Mathf.Min(p.colliderHeight, size.y);
                // trunks and pots are narrower than the canopy
                size = new Vector3(Mathf.Min(size.x, 0.6f), h, Mathf.Min(size.z, 0.6f));
                center = new Vector3(0f, h * 0.5f, 0f);
            }
            box.size = size;
            box.center = center;
            box.sharedMaterial = p.body == Body.Static ? null : fabric;

            if (p.body == Body.Dynamic)
            {
                var rb = root.AddComponent<Rigidbody>();
                rb.mass = p.mass;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = p.mass < 5f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
                PhysicsSetup.Steady(rb);
                PhysicsSetup.MakeSticky(rb, p.mass);
            }
        }
    }
}
