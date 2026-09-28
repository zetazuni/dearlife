using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>
    /// A piece of land you can stand on and (except the home lot) build on: the pre-built home, and the empty lots in the city. Each lot
    /// makes its own ground: a raised lawn pad of a stone body, with holes where pools were dug, and its own walkable surface for the people.
    /// </summary>
    public class Lot : MonoBehaviour
    {
        public string lotName = "Lot";
        [TextArea] public string blurb;
        [Tooltip("the house that came ready built: nothing can be built on it, only painted")] public bool home;
        [Tooltip("buildable area in world x and z")] public Vector2 min, max;
        public Vector3 spawn;
        [Tooltip("0 is home, then the empty lots in the order of the map")] public int order;
        public Material lawn, body, poolFloor;

        public readonly List<Rect> holes = new List<Rect>();
        Transform pad;
        NavMeshSurface surface;

        public Vector3 Centre => new Vector3((min.x + max.x) * 0.5f, GroundY, (min.y + max.y) * 0.5f);
        public Vector2 Size => max - min;
        public float GroundY => home ? -0.3f : 0f;
        public bool Contains(Vector3 p, float margin = 0f) => p.x >= min.x - margin && p.x <= max.x + margin && p.z >= min.y - margin && p.z <= max.y + margin;

        void Awake() { LotManager.Register(this); }

        void Start()
        {
            if (home) return;
            BuildGround();
            surface = gameObject.AddComponent<NavMeshSurface>();
            surface.agentTypeID = DearlifeNav.AgentType;
            surface.collectObjects = CollectObjects.Volume;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;
            surface.center = new Vector3(Centre.x, 4f, Centre.z);     // this object stays at the origin, so local is world
            surface.size = new Vector3(Size.x + 8f, 16f, Size.y + 8f);
            StartCoroutine(FirstBake());
        }

        IEnumerator FirstBake() { yield return new WaitForSeconds(2f); RebuildNav(); }

        /// <summary>Where the people walk. Everything built is switched on for a moment, so upper floors and roofs are included.</summary>
        public void RebuildNav()
        {
            if (!surface) return;
            var bm = BuildMode.Instance;
            if (bm) bm.SetAllVisible(true);
            Physics.SyncTransforms();
            surface.BuildNavMesh();
            if (bm) bm.RestoreVisibility();
        }

        // ------------------------------------------------------------ the ground

        /// <summary>The pad minus the pools: the ground is cut into boxes round the holes.</summary>
        public void SetHoles(IEnumerable<Rect> pools)
        {
            holes.Clear(); holes.AddRange(pools);
            if (!home) BuildGround();
        }

        static List<Rect> Slabs(Rect area, List<Rect> cut)
        {
            var xs = new SortedSet<float> { area.xMin, area.xMax };
            foreach (var h in cut) { xs.Add(Mathf.Clamp(h.xMin, area.xMin, area.xMax)); xs.Add(Mathf.Clamp(h.xMax, area.xMin, area.xMax)); }
            var list = new List<float>(xs);
            var result = new List<Rect>();
            for (int i = 0; i < list.Count - 1; i++)
            {
                float xa = list[i], xb = list[i + 1];
                if (xb - xa < 0.001f) continue;
                var zr = new List<Vector2>();
                foreach (var h in cut) if (h.xMin < xb - 0.001f && h.xMax > xa + 0.001f) zr.Add(new Vector2(h.yMin, h.yMax));
                zr.Sort((a, b) => a.x.CompareTo(b.x));
                float z = area.yMin;
                foreach (var r in zr)
                {
                    if (r.x > z + 0.001f) result.Add(new Rect(xa, z, xb - xa, Mathf.Min(r.x, area.yMax) - z));
                    z = Mathf.Max(z, r.y);
                }
                if (z < area.yMax - 0.001f) result.Add(new Rect(xa, z, xb - xa, area.yMax - z));
            }
            return result;
        }

        void BuildGround()
        {
            if (pad) Destroy(pad.gameObject);
            pad = new GameObject("Ground").transform;
            pad.SetParent(transform, false);
            pad.position = Vector3.zero;
            var area = new Rect(min.x - 1f, min.y - 1f, Size.x + 2f, Size.y + 2f);
            foreach (var r in Slabs(area, holes))
            {
                Cube("Body", new Vector3(r.xMin, -2f, r.yMin), new Vector3(r.xMax, -0.05f, r.yMax), body);
                Cube("Lawn", new Vector3(r.xMin, -0.05f, r.yMin), new Vector3(r.xMax, 0f, r.yMax), lawn);
            }
            foreach (var h in holes) Cube("Pool floor", new Vector3(h.xMin, -2f, h.yMin), new Vector3(h.xMax, -1.7f, h.yMax), poolFloor ? poolFloor : body);
        }

        void Cube(string name, Vector3 a, Vector3 b, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(pad, false);
            go.transform.position = (a + b) * 0.5f;
            go.transform.localScale = b - a;
            if (m) go.GetComponent<Renderer>().sharedMaterial = m;
            if (name == "Lawn") go.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
