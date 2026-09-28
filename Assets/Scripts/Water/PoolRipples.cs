using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The pool surface: a fine grid mesh driven by a wave equation, so ripples spread, bounce off the pool
    /// walls and fade out. Things that fall in make a splash scaled by how fast they hit, and every rigid body
    /// that is in the water gets a real buoyancy force and water drag, so cushions float and bob while heavy
    /// things sink slowly. A click on the water drops a stone in it. A light breeze keeps the surface alive.
    /// </summary>
    public class PoolRipples : MonoBehaviour
    {
        [Header("Pool (world space, metres)")]
        public Vector2 min = new Vector2(18.05f, 12.05f);
        public Vector2 max = new Vector2(25.95f, 15.95f);
        public float surfaceY = -0.42f;
        public float floorY = -1.7f;
        public Material material;
        [Tooltip("Round basins skip the corners of the grid.")] public bool round;
        [Tooltip("Off for decorative basins: no floating, no click splash.")] public bool buoyancy = true;

        [Header("Waves")]
        public float cell = 0.08f;
        [Range(0.9f, 1f)] public float damping = 0.985f;
        public float heightScale = 0.05f;   // metres of surface movement per unit of wave height
        public float normalGain = 3.2f;     // exaggerates the slope a little so reflections show the rings
        public float breeze = 1f;           // 0 turns the idle ripples off

        [Header("Buoyancy")]
        public float density = 1000f;
        public float fillFactor = 0.4f;     // how much of a body's bounding box is really solid
        public float waterDrag = 2.2f;

        int nx, nz;
        float[] cur, prev;
        Vector3[] verts, norms;
        Mesh mesh;
        float tick, nextDrip;
        readonly Dictionary<Rigidbody, float> lastDepth = new Dictionary<Rigidbody, float>();
        readonly HashSet<Rigidbody> seen = new HashSet<Rigidbody>();
        readonly Dictionary<Rigidbody, Bounds> boxes = new Dictionary<Rigidbody, Bounds>();
        readonly List<Rigidbody> gone = new List<Rigidbody>();
        Camera cam;

        void Awake()
        {
            cam = Camera.main;
            nx = Mathf.Max(8, Mathf.RoundToInt((max.x - min.x) / cell));
            nz = Mathf.Max(8, Mathf.RoundToInt((max.y - min.y) / cell));
            cur = new float[(nx + 1) * (nz + 1)];
            prev = new float[(nx + 1) * (nz + 1)];
            BuildMesh();
        }

        void BuildMesh()
        {
            int vc = (nx + 1) * (nz + 1);
            verts = new Vector3[vc];
            norms = new Vector3[vc];
            var uv = new Vector2[vc];
            var tris = new int[nx * nz * 6];
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    int i = z * (nx + 1) + x;
                    verts[i] = new Vector3(Mathf.Lerp(min.x, max.x, x / (float)nx), surfaceY, Mathf.Lerp(min.y, max.y, z / (float)nz));
                    norms[i] = Vector3.up;
                    uv[i] = new Vector2(x / (float)nx, z / (float)nz);
                }
            int t = 0;
            for (int z = 0; z < nz; z++)
                for (int x = 0; x < nx; x++)
                {
                    int a = z * (nx + 1) + x, b = a + 1, c = a + nx + 1, d = c + 1;
                    if (round)
                    {
                        float ux = (x + 0.5f) / nx * 2f - 1f, uz = (z + 0.5f) / nz * 2f - 1f;
                        if (ux * ux + uz * uz > 1f) { tris[t++] = a; tris[t++] = a; tris[t++] = a; tris[t++] = a; tris[t++] = a; tris[t++] = a; continue; }
                    }
                    tris[t++] = a; tris[t++] = c; tris[t++] = b;
                    tris[t++] = b; tris[t++] = c; tris[t++] = d;
                }
            mesh = new Mesh { name = "Pool surface", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.normals = norms;
            mesh.uv = uv;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(new Vector3((min.x + max.x) / 2f, surfaceY, (min.y + max.y) / 2f), new Vector3(max.x - min.x, 1f, max.y - min.y));

            var go = new GameObject("Pool surface");
            go.transform.SetParent(transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = material;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        // ---------- public helpers ----------

        /// <summary>Drops a splash at a world position. strength is roughly the impact speed in m/s.</summary>
        public void Splash(Vector3 world, float strength, float radius = 0.3f)
        {
            float gx = (world.x - min.x) / (max.x - min.x) * nx;
            float gz = (world.z - min.y) / (max.y - min.y) * nz;
            if (gx < 1 || gz < 1 || gx > nx - 1 || gz > nz - 1) return;
            float r = Mathf.Max(radius, cell * 2f) / cell;
            int ir = Mathf.CeilToInt(r);
            for (int z = Mathf.Max(1, (int)gz - ir); z <= Mathf.Min(nz - 1, (int)gz + ir); z++)
                for (int x = Mathf.Max(1, (int)gx - ir); x <= Mathf.Min(nx - 1, (int)gx + ir); x++)
                {
                    float d = Mathf.Sqrt((x - gx) * (x - gx) + (z - gz) * (z - gz)) / r;
                    if (d > 1f) continue;
                    float w = 0.5f * (1f + Mathf.Cos(d * Mathf.PI)); // smooth bump
                    cur[z * (nx + 1) + x] -= Mathf.Clamp(strength, -6f, 6f) * w * 0.35f;
                }
        }

        public bool Contains(Vector3 p) => p.x > min.x && p.x < max.x && p.z > min.y && p.z < max.y;

        // ---------- simulation ----------

        void Update()
        {
            // a stone in the water on click
            var orbit = OrbitCamera.Instance;
            if (buoyancy && orbit && orbit.ClickedThisFrame && cam)
            {
                var ray = cam.ScreenPointToRay(Input.mousePosition);
                var plane = new Plane(Vector3.up, new Vector3(0f, surfaceY, 0f));
                if (plane.Raycast(ray, out float e))
                {
                    var hit = ray.GetPoint(e);
                    // only when nothing solid is in front of the water along the ray
                    bool blocked = Physics.Raycast(ray, out var ph, e - 0.05f) && !ph.collider.isTrigger;
                    if (!blocked && Contains(hit)) Splash(hit, 3.2f, 0.32f);
                }
            }

            // a light breeze, small drips at random places
            if (breeze > 0f && Time.time > nextDrip)
            {
                nextDrip = Time.time + Random.Range(0.6f, 1.8f) / breeze;
                Splash(new Vector3(Random.Range(min.x + 0.5f, max.x - 0.5f), surfaceY, Random.Range(min.y + 0.5f, max.y - 0.5f)), Random.Range(0.15f, 0.5f), 0.16f);
            }

            tick += Time.deltaTime;
            int steps = 0;
            while (tick >= 1f / 60f && steps < 3) { Step(); tick -= 1f / 60f; steps++; }
            if (steps == 3) tick = 0f;
            Upload();
        }

        Vector2 SlopeAt(Vector3 world)
        {
            int w = nx + 1;
            int x = Mathf.Clamp(Mathf.RoundToInt((world.x - min.x) / (max.x - min.x) * nx), 1, nx - 1);
            int z = Mathf.Clamp(Mathf.RoundToInt((world.z - min.y) / (max.y - min.y) * nz), 1, nz - 1);
            int i = z * w + x;
            float dx = (max.x - min.x) / nx, dz = (max.y - min.y) / nz;
            return new Vector2((cur[i + 1] - cur[i - 1]) / (2f * dx), (cur[i + w] - cur[i - w]) / (2f * dz)) * heightScale;
        }

        void Step()
        {
            int w = nx + 1;
            for (int z = 1; z < nz; z++)
                for (int x = 1; x < nx; x++)
                {
                    int i = z * w + x;
                    float n = (cur[i - 1] + cur[i + 1] + cur[i - w] + cur[i + w]) * 0.5f - prev[i];
                    prev[i] = n * damping;
                }
            var t = prev; prev = cur; cur = t;
        }

        void Upload()
        {
            int w = nx + 1;
            float dx = (max.x - min.x) / nx, dz = (max.y - min.y) / nz;
            for (int z = 0; z <= nz; z++)
                for (int x = 0; x <= nx; x++)
                {
                    int i = z * w + x;
                    var v = verts[i];
                    v.y = surfaceY + cur[i] * heightScale;
                    verts[i] = v;
                    float hl = cur[z * w + Mathf.Max(x - 1, 0)], hr = cur[z * w + Mathf.Min(x + 1, nx)];
                    float hd = cur[Mathf.Max(z - 1, 0) * w + x], hu = cur[Mathf.Min(z + 1, nz) * w + x];
                    var n = new Vector3(-(hr - hl) * heightScale * normalGain / (2f * dx), 1f, -(hu - hd) * heightScale * normalGain / (2f * dz));
                    norms[i] = n.normalized;
                }
            mesh.vertices = verts;
            mesh.normals = norms;
        }

        // ---------- buoyancy and splashes ----------

        void FixedUpdate()
        {
            if (!buoyancy) return;
            var centre = new Vector3((min.x + max.x) / 2f, (surfaceY + floorY) / 2f + 1f, (min.y + max.y) / 2f);
            var half = new Vector3((max.x - min.x) / 2f, (surfaceY - floorY) / 2f + 1.2f, (max.y - min.y) / 2f);
            var cols = Physics.OverlapBox(centre, half, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            seen.Clear();
            boxes.Clear();
            foreach (var c in cols)
            {
                var rb = c.attachedRigidbody;
                if (!rb || rb.isKinematic) continue;
                if (boxes.TryGetValue(rb, out var bb)) { bb.Encapsulate(c.bounds); boxes[rb] = bb; }
                else boxes[rb] = c.bounds;
            }
            foreach (var kv in boxes)
            {
                var rb = kv.Key;
                var b = kv.Value;                          // the whole body, all its parts together
                seen.Add(rb);
                if (!Contains(b.center)) continue;
                float height = Mathf.Max(b.size.y, 0.02f);
                float depth = Mathf.Clamp(surfaceY - b.min.y, 0f, height);
                float frac = depth / height;
                lastDepth.TryGetValue(rb, out float before);

                if (frac > 0f)
                {
                    float volume = b.size.x * b.size.y * b.size.z * fillFactor;
                    // Archimedes: the water pushes up with the weight of the water it displaces
                    float displaced = volume * frac;
                    rb.AddForce(Vector3.up * (density * 9.81f * displaced), ForceMode.Force);
                    rb.AddForce(-rb.linearVelocity * (waterDrag * frac * rb.mass), ForceMode.Force);
                    rb.AddTorque(-rb.angularVelocity * (waterDrag * 0.3f * frac * rb.mass), ForceMode.Force);
                    if (rb.GetComponent<RollingBall>())
                    {
                        // the waves push it down their slope and roll it
                        var sl = SlopeAt(b.center);
                        var f = new Vector3(-sl.x, 0f, -sl.y) * (rb.mass * 9.81f * 3f);
                        f = Vector3.ClampMagnitude(f, 14f);
                        rb.AddForce(f, ForceMode.Force);
                        rb.AddTorque(Vector3.Cross(Vector3.up, f) * 0.12f, ForceMode.Force);
                    }
                }

                // a splash when it enters, and wakes while it moves through the surface
                var p = new Vector3(b.center.x, surfaceY, b.center.z);
                float radius = Mathf.Clamp(Mathf.Max(b.size.x, b.size.z) * 0.5f, 0.15f, 0.9f);
                if (before <= 0.001f && frac > 0.001f) Splash(p, Mathf.Abs(rb.linearVelocity.y) * 1.2f + 0.6f, radius);
                else if (frac > 0f && frac < 1f && rb.linearVelocity.sqrMagnitude > 0.02f) Splash(p, rb.linearVelocity.magnitude * 0.12f, radius * 0.7f);
                lastDepth[rb] = frac;
            }

            gone.Clear();
            foreach (var kv in lastDepth) if (!seen.Contains(kv.Key)) gone.Add(kv.Key);
            foreach (var g in gone) lastDepth.Remove(g);
        }
    }
}
