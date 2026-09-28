using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// The modern city around the plot, so the camera never looks out on an empty lawn: streets in a grid, sidewalks, neighbouring houses,
    /// apartment blocks further out and glass towers on the horizon, street trees and street lamps. Everything is combined into one mesh
    /// per block and material (saved in Assets/Art/Meshes/City), windows come from a generated facade texture and light up at night
    /// (CityNight). The plot itself (fence, road, sidewalk) is left exactly as it is.
    /// </summary>
    public static class CityBuilder
    {
        const string MeshDir = "Assets/Art/Meshes/City";
        const string MatDir = "Assets/Art/Materials/City";

        // the roads: centre lines. x = 40.6 is the road that already runs past the garage
        static readonly float[] RoadX = { -139.4f, -79.4f, -19.4f, 40.6f, 100.6f, 160.6f };
        static readonly float[] RoadZ = { -130f, -40f, 50f, 140f };
        const float RoadHalf = 3.2f, Margin = 6.4f;          // road half width, and road + kerb + sidewalk
        const float Edge = 220f;                             // the city ends here (the ground goes to 230)
        // the plot and everything that must stay clear (fence to road kerb)
        static readonly Rect Plot = new Rect(-6f, -8f, 53f, 34f);
        static readonly Vector2 Centre = new Vector2(17f, 9f);
        /// <summary>The empty lots you can build on (x, z, width, depth): no buildings are put there, see GreyboxBuilder.BuildLotPads.</summary>
        public static readonly Rect[] BuildLots = { new Rect(50f, 62f, 40f, 32f), new Rect(-10f, 62f, 40f, 32f), new Rect(-72f, 0f, 40f, 32f) };

        class Batch
        {
            public readonly List<Vector3> v = new List<Vector3>(), n = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public void Box(Vector3 min, Vector3 max)
            {
                Vector3[] c = {
                    new Vector3(min.x, min.y, min.z), new Vector3(max.x, min.y, min.z), new Vector3(max.x, max.y, min.z), new Vector3(min.x, max.y, min.z),
                    new Vector3(min.x, min.y, max.z), new Vector3(max.x, min.y, max.z), new Vector3(max.x, max.y, max.z), new Vector3(min.x, max.y, max.z) };
                Quad(c[0], c[3], c[2], c[1], Vector3.back);
                Quad(c[5], c[6], c[7], c[4], Vector3.forward);
                Quad(c[4], c[7], c[3], c[0], Vector3.left);
                Quad(c[1], c[2], c[6], c[5], Vector3.right);
                Quad(c[3], c[7], c[6], c[2], Vector3.up);
                Quad(c[4], c[0], c[1], c[5], Vector3.down);
            }

            void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 nor)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                for (int k = 0; k < 4; k++) n.Add(nor);
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(0, 1)); uv.Add(new Vector2(1, 1)); uv.Add(new Vector2(1, 0));
                t.Add(i); t.Add(i + 1); t.Add(i + 2); t.Add(i); t.Add(i + 2); t.Add(i + 3);
            }

            public void Sphere(Vector3 c, float r, float sy)
            {
                const int seg = 8, rings = 5;
                int b = v.Count;
                for (int y = 0; y <= rings; y++)
                {
                    float pol = Mathf.PI * y / rings;
                    for (int x = 0; x <= seg; x++)
                    {
                        float az = 2f * Mathf.PI * x / seg;
                        var dir = new Vector3(Mathf.Sin(pol) * Mathf.Cos(az), Mathf.Cos(pol), Mathf.Sin(pol) * Mathf.Sin(az));
                        v.Add(c + new Vector3(dir.x * r, dir.y * r * sy, dir.z * r)); n.Add(dir); uv.Add(new Vector2(x / (float)seg, y / (float)rings));
                    }
                }
                for (int y = 0; y < rings; y++)
                    for (int x = 0; x < seg; x++)
                    {
                        int a = b + y * (seg + 1) + x, d = a + seg + 1;
                        t.Add(a); t.Add(a + 1); t.Add(d); t.Add(a + 1); t.Add(d + 1); t.Add(d);
                    }
            }

            public bool Empty => v.Count == 0;

            public Mesh ToMesh(string name)
            {
                var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v); m.SetNormals(n); m.SetUVs(0, uv); m.SetTriangles(t, 0);
                m.RecalculateBounds();
                return m;
            }
        }

        // -------------------------------------------------------------- textures and materials

        /// <summary>
        /// A 12.8 m tile with 4 x 4 cells of 3.2 m (one floor high). style 0: floor to ceiling glass panes with slim black mullions and a slab
        /// edge at every floor, like the garden facade of the house. style 1: vertical timber slats with one wide window per cell.
        /// style 2: white plaster with big square windows. The emissive map lights about a third of the windows warm.
        /// </summary>
        static Texture2D Facade(string path, bool emissive, int style)
        {
            if (System.IO.File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            const int size = 512, cell = size / 4;
            var rnd = new System.Random(77 + style);
            var px = new Color[size * size];
            bool[,] lit = new bool[4, 4]; float[,] curtain = new float[4, 4];
            for (int a = 0; a < 4; a++) for (int b = 0; b < 4; b++) { lit[a, b] = rnd.NextDouble() < 0.4; curtain[a, b] = (float)rnd.NextDouble(); }
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    int cx = x / cell, cy = y / cell;
                    float u = (x % cell) / (float)cell, v = (y % cell) / (float)cell;
                    bool glassArea; float gv, gu;                       // glass region and the position inside it
                    Color solid = Color.white; bool frame = false;
                    if (style == 0)
                    {
                        glassArea = u > 0.02f && u < 0.98f && v > 0.05f && v < 0.88f;
                        gu = Mathf.InverseLerp(0.02f, 0.98f, u); gv = Mathf.InverseLerp(0.05f, 0.88f, v);
                        frame = glassArea && (u < 0.045f || u > 0.955f || v < 0.075f || v > 0.86f || Mathf.Abs(u - 0.5f) < 0.006f);
                        float g = 0.78f + 0.08f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f);       // the slab edge: pale concrete
                        solid = new Color(g, g, g * 0.98f, 1f);
                    }
                    else if (style == 1)
                    {
                        glassArea = u > 0.1f && u < 0.9f && v > 0.22f && v < 0.84f;
                        gu = Mathf.InverseLerp(0.1f, 0.9f, u); gv = Mathf.InverseLerp(0.22f, 0.84f, v);
                        frame = glassArea && (u < 0.13f || u > 0.87f || v < 0.25f || v > 0.81f || Mathf.Abs(u - 0.5f) < 0.008f);
                        float slat = Mathf.Repeat(x * 0.55f, 1f);                                  // vertical boards
                        float t = 0.5f + 0.5f * Mathf.PerlinNoise(x * 0.03f, y * 0.4f);
                        var wood = Color.Lerp(new Color(0.36f, 0.22f, 0.13f), new Color(0.58f, 0.38f, 0.22f), t);
                        solid = slat < 0.1f ? wood * 0.45f : wood;
                        solid.a = 1f;
                    }
                    else
                    {
                        glassArea = u > 0.12f && u < 0.88f && v > 0.14f && v < 0.84f;
                        gu = Mathf.InverseLerp(0.12f, 0.88f, u); gv = Mathf.InverseLerp(0.14f, 0.84f, v);
                        frame = glassArea && (u < 0.15f || u > 0.85f || v < 0.17f || v > 0.81f);
                        float g = 0.9f + 0.05f * Mathf.PerlinNoise(x * 0.09f, y * 0.09f);
                        solid = new Color(g, g, g * 0.97f, 1f);
                    }
                    Color c;
                    if (!glassArea) c = emissive ? Color.black : solid;
                    else if (frame) c = emissive ? Color.black : new Color(0.06f, 0.065f, 0.07f, 1f);
                    else if (emissive)
                    {
                        // a warm room: brighter towards the ceiling, with a curtain over one side in some windows
                        float warm = 0.7f + 0.3f * (1f - gv);
                        bool curtained = curtain[cx, cy] > 0.6f && gu < 0.45f;
                        c = lit[cx, cy] ? new Color(1f, 0.8f + 0.1f * curtain[cx, cy], 0.5f, 1f) * (curtained ? warm * 0.35f : warm) : Color.black;
                    }
                    else
                    {
                        // dark reflective glass with a sky gradient and a diagonal streak
                        float streak = Mathf.Clamp01(1f - Mathf.Abs(Mathf.Repeat(gu + gv * 0.8f + curtain[cx, cy], 1f) - 0.5f) * 7f) * 0.12f;
                        c = Color.Lerp(new Color(0.07f, 0.1f, 0.14f), new Color(0.32f, 0.45f, 0.56f), Mathf.Pow(gv, 1.4f)) + new Color(streak, streak, streak);
                        c.a = 1f;
                    }
                    px[y * size + x] = c;
                }
            var t2 = new Texture2D(size, size, TextureFormat.RGBA32, true);
            t2.SetPixels(px); t2.Apply();
            System.IO.File.WriteAllBytes(path, t2.EncodeToPNG());
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = true; imp.mipmapEnabled = true; imp.anisoLevel = 8; imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material FacadeMat(string name, Color tint, float smooth, Texture2D diff, Texture2D emi)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseColorMap", diff);
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            m.SetFloat("_UVBase", 5f);                       // triplanar, in world metres
            m.SetFloat("_ObjectSpaceUVMapping", 0f);
            m.SetFloat("_TexWorldScale", 1f / 12.8f);
            m.SetTextureScale("_BaseColorMap", Vector2.one);
            m.SetTexture("_EmissiveColorMap", emi);
            m.SetFloat("_UseEmissiveIntensity", 0f);
            m.SetColor("_EmissiveColor", Color.black);
            HDMaterialFix(m);
            return m;
        }

        static Material Plain(string name, Color c, float smooth, float metal = 0f)
        {
            string path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find("HDRP/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetColor("_BaseColor", c); m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal);
            HDMaterialFix(m);
            return m;
        }

        static void HDMaterialFix(Material m)
        {
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
        }

        // -------------------------------------------------------------- the build

        struct Lot { public float x0, z0, x1, z1; }

        public static void Build(Material asphalt, Material stone, Material lineWhite, Material lawn)
        {
            System.IO.Directory.CreateDirectory(MeshDir);
            System.IO.Directory.CreateDirectory(MatDir);
            var glassD = Facade("Assets/Art/Textures/CityGlassFacade.png", false, 0); var glassE = Facade("Assets/Art/Textures/CityGlassFacadeLit.png", true, 0);
            var woodD = Facade("Assets/Art/Textures/CityWoodFacade.png", false, 1); var woodE = Facade("Assets/Art/Textures/CityWoodFacadeLit.png", true, 1);
            var whiteD = Facade("Assets/Art/Textures/CityWhiteFacade.png", false, 2); var whiteE = Facade("Assets/Art/Textures/CityWhiteFacadeLit.png", true, 2);
            var matGlass = FacadeMat("CityGlass", new Color(0.92f, 0.97f, 1f), 0.85f, glassD, glassE);
            var matGlassWarm = FacadeMat("CityGlassWarm", new Color(1f, 0.96f, 0.88f), 0.85f, glassD, glassE);
            var matWood = FacadeMat("CityWood", new Color(1f, 1f, 1f), 0.3f, woodD, woodE);
            var matWhite = FacadeMat("CityWhite", new Color(1f, 1f, 1f), 0.35f, whiteD, whiteE);
            var matSlab = Plain("CitySlab", new Color(0.86f, 0.86f, 0.85f), 0.3f);
            var matRoof = Plain("CityRoof", new Color(0.22f, 0.23f, 0.25f), 0.3f);
            var matTrunk = Plain("CityTrunk", new Color(0.25f, 0.17f, 0.11f), 0.2f);
            var matLeaf = Plain("CityLeaf", new Color(0.24f, 0.44f, 0.16f), 0.15f);
            var matLamp = Plain("CityLamp", new Color(0.05f, 0.05f, 0.055f), 0.6f, 1f);
            var matBulb = Plain("CityLampGlow", new Color(1f, 0.86f, 0.6f), 0.2f);
            matBulb.SetFloat("_UseEmissiveIntensity", 0f); matBulb.SetColor("_EmissiveColor", Color.black); HDMaterialFix(matBulb);
            var facades = new[] { matGlass, matGlassWarm, matWood, matWhite };

            var root = new GameObject("City").transform;
            var night = root.gameObject.AddComponent<CityNight>();
            night.lit = new List<Material>(facades);
            night.lamp = matBulb;

            var rnd = new System.Random(2026);
            float[] xs = new float[RoadX.Length + 2], zs = new float[RoadZ.Length + 2];
            xs[0] = -Edge - Margin + 0f; zs[0] = -Edge - Margin;
            for (int i = 0; i < RoadX.Length; i++) xs[i + 1] = RoadX[i];
            for (int i = 0; i < RoadZ.Length; i++) zs[i + 1] = RoadZ[i];
            xs[xs.Length - 1] = Edge + Margin; zs[zs.Length - 1] = Edge + Margin;

            // ---- roads (they run the whole way, the existing road is already there between z -30 and 50)
            float gy = -0.3f, top = gy + 0.02f;
            var road = new GameObject("Streets").transform; road.SetParent(root, false);
            var roadB = new Batch(); var walkB = new Batch(); var lineB = new Batch(); var lampB = new Batch(); var bulbB = new Batch();
            foreach (float x in RoadX)
            {
                float ex0 = -Edge, ex1 = Edge;
                if (Mathf.Approximately(x, 40.6f)) { AddRoadZ(roadB, walkB, lineB, x, -Edge, -30f, gy, top); AddRoadZ(roadB, walkB, lineB, x, 50f, Edge, gy, top); }
                else AddRoadZ(roadB, walkB, lineB, x, ex0, ex1, gy, top);
            }
            foreach (float z in RoadZ) AddRoadX(roadB, walkB, lineB, z, -Edge, Edge, gy, top);
            Emit(road, "Asphalt", roadB, asphalt, false);
            Emit(road, "Sidewalks", walkB, stone, false);
            Emit(road, "Road lines", lineB, lineWhite, false);

            // ---- lots and buildings, one batch per block so the camera can skip whole blocks
            for (int bx = 0; bx < xs.Length - 1; bx++)
                for (int bz = 0; bz < zs.Length - 1; bz++)
                {
                    float xa = xs[bx] + Margin, xb = xs[bx + 1] - Margin, za = zs[bz] + Margin, zb = zs[bz + 1] - Margin;
                    if (xb - xa < 8f || zb - za < 8f) continue;
                    var blk = new GameObject($"Block {bx}_{bz}").transform; blk.SetParent(root, false);
                    var byMat = new Dictionary<Material, Batch>();
                    var roofs = new Batch(); var slabs = new Batch(); var trees = new Batch(); var trunks = new Batch();
                    Batch B(Material m) { if (!byMat.TryGetValue(m, out var b)) { b = new Batch(); byMat[m] = b; } return b; }
                    var lots = new List<Lot>();
                    Subdivide(rnd, xa, za, xb, zb, lots, true);
                    foreach (var lot in lots)
                    {
                        // the buildings keep clear of the plot: a lot that runs into it is cut down to the free strips round it
                        foreach (var piece in Carve(lot)) Place(rnd, piece, B, roofs, slabs, facades);
                    }
                    // street trees along the kerbs of this block
                    for (float x = xa + 3f; x < xb - 1f; x += 9f + (float)rnd.NextDouble() * 4f)
                    {
                        Tree(trees, trunks, rnd, x, za - 3.6f); Tree(trees, trunks, rnd, x, zb + 3.6f);
                    }
                    for (float z = za + 3f; z < zb - 1f; z += 9f + (float)rnd.NextDouble() * 4f)
                    {
                        Tree(trees, trunks, rnd, xa - 3.6f, z); Tree(trees, trunks, rnd, xb + 3.6f, z);
                    }
                    bool near = Vector2.Distance(new Vector2((xa + xb) * 0.5f, (za + zb) * 0.5f), Centre) < 110f;
                    foreach (var kv in byMat) Emit(blk, kv.Key.name, kv.Value, kv.Key, near);
                    Emit(blk, "Roofs", roofs, matRoof, near);
                    Emit(blk, "Slabs", slabs, matSlab, near);
                    Emit(blk, "Trunks", trunks, matTrunk, false);
                    Emit(blk, "Tree crowns", trees, matLeaf, false);
                }

            // street lamps along the roads next to the plot and the nearest crossings
            foreach (float z in RoadZ) { }
            for (float z = -200f; z <= 200f; z += 22f)
            {
                if (z > -32f && z < 52f) continue;                       // the road past the plot has its own light
                foreach (float x in RoadX) Lamp(lampB, bulbB, x - RoadHalf - 1.2f, z);
            }
            for (float x = -200f; x <= 200f; x += 24f)
                foreach (float z in RoadZ) Lamp(lampB, bulbB, x, z - RoadHalf - 1.2f);
            Emit(root, "Lamp posts", lampB, matLamp, false);
            Emit(root, "Lamp lights", bulbB, matBulb, false);
            EditorUtility.SetDirty(root.gameObject);
            AssetDatabase.SaveAssets();
        }

        static void Emit(Transform parent, string name, Batch b, Material m, bool shadows)
        {
            if (b == null || b.Empty) return;
            string safe = (parent.name + "_" + name).Replace(' ', '_');
            var mesh = b.ToMesh(safe);
            string path = $"{MeshDir}/{safe}.asset";
            AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(mesh, path);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
            r.receiveShadows = true;
        }

        // ---- roads

        static void AddRoadZ(Batch road, Batch walk, Batch line, float cx, float z0, float z1, float gy, float top)
        {
            road.Box(new Vector3(cx - RoadHalf, gy, z0), new Vector3(cx + RoadHalf, top, z1));
            walk.Box(new Vector3(cx - Margin, gy, z0), new Vector3(cx - RoadHalf - 0.18f, gy + 0.1f, z1));
            walk.Box(new Vector3(cx + RoadHalf + 0.18f, gy, z0), new Vector3(cx + Margin, gy + 0.1f, z1));
            for (float z = Mathf.Ceil(z0 / 6f) * 6f; z < z1; z += 6f)
                line.Box(new Vector3(cx - 0.07f, top, z), new Vector3(cx + 0.07f, top + 0.005f, z + 3f));
        }

        static void AddRoadX(Batch road, Batch walk, Batch line, float cz, float x0, float x1, float gy, float top)
        {
            road.Box(new Vector3(x0, gy, cz - RoadHalf), new Vector3(x1, top + 0.001f, cz + RoadHalf));
            walk.Box(new Vector3(x0, gy, cz - Margin), new Vector3(x1, gy + 0.1f, cz - RoadHalf - 0.18f));
            walk.Box(new Vector3(x0, gy, cz + RoadHalf + 0.18f), new Vector3(x1, gy + 0.1f, cz + Margin));
            for (float x = Mathf.Ceil(x0 / 6f) * 6f; x < x1; x += 6f)
                line.Box(new Vector3(x, top + 0.002f, cz - 0.07f), new Vector3(x + 3f, top + 0.007f, cz + 0.07f));
        }

        static void Lamp(Batch pole, Batch bulb, float x, float z)
        {
            pole.Box(new Vector3(x - 0.06f, -0.3f, z - 0.06f), new Vector3(x + 0.06f, 5.6f, z + 0.06f));
            pole.Box(new Vector3(x - 0.06f, 5.5f, z - 0.06f), new Vector3(x + 0.9f, 5.62f, z + 0.06f));
            bulb.Box(new Vector3(x + 0.6f, 5.44f, z - 0.16f), new Vector3(x + 1.0f, 5.5f, z + 0.16f));
        }

        static void Tree(Batch crowns, Batch trunks, System.Random r, float x, float z)
        {
            if (Overlaps(new Lot { x0 = x - 2f, x1 = x + 2f, z0 = z - 2f, z1 = z + 2f }, Plot, 0.5f)) return;
            float h = 2.4f + (float)r.NextDouble() * 1.8f, rad = 1.5f + (float)r.NextDouble() * 1.1f;
            trunks.Box(new Vector3(x - 0.14f, -0.3f, z - 0.14f), new Vector3(x + 0.14f, h, z + 0.14f));
            crowns.Sphere(new Vector3(x, h + rad * 0.55f, z), rad, 0.85f);
        }

        // ---- lots

        /// <summary>A city lot with the plot and the empty building lots cut out of it: what is left is built on.</summary>
        static IEnumerable<Lot> Carve(Lot lot)
        {
            var cur = new List<Lot> { lot };
            var reserved = new List<Rect> { Plot };
            foreach (var r in BuildLots) reserved.Add(new Rect(r.x - 3.5f, r.y - 3.5f, r.width + 7f, r.height + 7f));
            foreach (var r in reserved)
            {
                var next = new List<Lot>();
                foreach (var c in cur)
                {
                    if (!Overlaps(c, r, 0.5f)) next.Add(c);
                    else next.AddRange(Strips(c, r));
                }
                cur = next;
            }
            return cur;
        }

        static IEnumerable<Lot> Strips(Lot l, Rect plot)
        {
            float px0 = plot.xMin - 0.5f, px1 = plot.xMax + 0.5f, pz0 = plot.yMin - 0.5f, pz1 = plot.yMax + 0.5f;
            var cand = new[]
            {
                new Lot { x0 = l.x0, z0 = l.z0, x1 = px0, z1 = l.z1 },        // west of the plot
                new Lot { x0 = px1, z0 = l.z0, x1 = l.x1, z1 = l.z1 },        // east
                new Lot { x0 = Mathf.Max(l.x0, px0), z0 = l.z0, x1 = Mathf.Min(l.x1, px1), z1 = pz0 },   // north (behind the house)
                new Lot { x0 = Mathf.Max(l.x0, px0), z0 = pz1, x1 = Mathf.Min(l.x1, px1), z1 = l.z1 },   // south (past the garden)
            };
            foreach (var c in cand) if (c.x1 - c.x0 >= 9f && c.z1 - c.z0 >= 9f) yield return c;
        }

        static bool Overlaps(Lot l, Rect r, float pad) => l.x0 < r.xMax + pad && l.x1 > r.xMin - pad && l.z0 < r.yMax + pad && l.z1 > r.yMin - pad;

        static void Subdivide(System.Random r, float x0, float z0, float x1, float z1, List<Lot> outLots, bool splitX)
        {
            float w = x1 - x0, d = z1 - z0;
            if (w <= 30f && d <= 30f || (w < 24f && d < 40f) || (d < 24f && w < 40f)) { outLots.Add(new Lot { x0 = x0, z0 = z0, x1 = x1, z1 = z1 }); return; }
            bool alongX = w >= d;
            float len = alongX ? w : d;
            float cut = len * (0.42f + (float)r.NextDouble() * 0.16f);
            if (alongX) { Subdivide(r, x0, z0, x0 + cut, z1, outLots, false); Subdivide(r, x0 + cut, z0, x1, z1, outLots, false); }
            else { Subdivide(r, x0, z0, x1, z0 + cut, outLots, true); Subdivide(r, x0, z0 + cut, x1, z1, outLots, true); }
        }

        /// <summary>
        /// Buildings in the style of the house: glass volumes with slim frames, timber or white boxes cantilevered over them, and thin
        /// white roof slabs that overhang. facades: 0 glass, 1 warm glass, 2 timber, 3 white plaster.
        /// </summary>
        static void Place(System.Random r, Lot lot, System.Func<Material, Batch> B, Batch roofs, Batch slabs, Material[] facades)
        {
            float cx = (lot.x0 + lot.x1) * 0.5f, cz = (lot.z0 + lot.z1) * 0.5f;
            float dist = Vector2.Distance(new Vector2(cx, cz), Centre);
            float inset = 1.5f + (float)r.NextDouble() * 2.2f;
            float x0 = lot.x0 + inset, x1 = lot.x1 - inset, z0 = lot.z0 + inset, z1 = lot.z1 - inset;
            if (x1 - x0 < 5f || z1 - z0 < 5f) return;
            const float F = 3.2f, y0 = -0.32f;
            Material Glass() => facades[r.NextDouble() < 0.7 ? 0 : 1];
            Material Solid() => facades[r.NextDouble() < 0.6 ? 2 : 3];

            if (dist < 85f)
            {
                // a house: a glass ground floor, a timber or white upper floor that hangs over one side, a thin roof slab
                float w = x1 - x0, d = z1 - z0;
                var g = Glass();
                B(g).Box(new Vector3(x0, y0, z0), new Vector3(x1, F, z1));
                slabs.Box(new Vector3(x0 - 0.15f, F, z0 - 0.15f), new Vector3(x1 + 0.15f, F + 0.32f, z1 + 0.15f));
                bool two = r.NextDouble() < 0.85;
                if (two)
                {
                    bool alongX = r.NextDouble() < 0.5;
                    float frac = 0.55f + (float)r.NextDouble() * 0.25f;
                    float over = 1.6f + (float)r.NextDouble() * 1.4f;
                    float ux0 = x0, ux1 = x1, uz0 = z0, uz1 = z1;
                    if (alongX) { if (r.NextDouble() < 0.5) { ux1 = x0 + w * frac + over; ux0 = x0 - 0.2f; } else { ux0 = x1 - w * frac - over; ux1 = x1 + 0.2f; } }
                    else { if (r.NextDouble() < 0.5) { uz1 = z0 + d * frac + over; uz0 = z0 - 0.2f; } else { uz0 = z1 - d * frac - over; uz1 = z1 + 0.2f; } }
                    var up = r.NextDouble() < 0.28 ? Glass() : Solid();
                    B(up).Box(new Vector3(ux0, F + 0.32f, uz0), new Vector3(ux1, F * 2f + 0.32f, uz1));
                    slabs.Box(new Vector3(ux0 - 0.7f, F * 2f + 0.32f, uz0 - 0.7f), new Vector3(ux1 + 0.7f, F * 2f + 0.66f, uz1 + 0.7f));       // the overhanging roof
                }
                roofs.Box(new Vector3(x0 + 0.5f, F + 0.32f, z0 + 0.5f), new Vector3(x0 + 1.6f, F + 0.75f, z0 + 1.6f));         // a plant box on the terrace roof
                return;
            }

            float floors; bool tower = false;
            if (dist < 145f) floors = 4 + r.Next(0, 6); else { floors = 9 + r.Next(0, 26); tower = true; }
            float h = floors * F;
            if (tower)
            {
                float s = 0.5f + (float)r.NextDouble() * 0.3f;
                float ccx = cx + ((float)r.NextDouble() - 0.5f) * 4f, ccz = cz + ((float)r.NextDouble() - 0.5f) * 4f;
                float hx = Mathf.Min((x1 - x0) * 0.5f, 26f) * s + 3f, hz = Mathf.Min((z1 - z0) * 0.5f, 26f) * s + 3f;
                x0 = ccx - hx; x1 = ccx + hx; z0 = ccz - hz; z1 = ccz + hz;
            }
            var main = r.NextDouble() < 0.75 ? Glass() : Solid();
            B(main).Box(new Vector3(x0, y0, z0), new Vector3(x1, h, z1));
            slabs.Box(new Vector3(x0 - 0.5f, h, z0 - 0.5f), new Vector3(x1 + 0.5f, h + 0.45f, z1 + 0.5f));
            // a timber or white core offset on one side, full height, and a glass pavilion on the roof
            if (r.NextDouble() < 0.7)
            {
                bool alongX = (x1 - x0) > (z1 - z0);
                float k = 0.22f + (float)r.NextDouble() * 0.12f;
                Vector3 a, b;
                if (alongX) { bool left = r.NextDouble() < 0.5; float wx = (x1 - x0) * k; a = new Vector3(left ? x0 - 0.8f : x1 - wx, y0, z0 - 0.8f); b = new Vector3(left ? x0 + wx : x1 + 0.8f, h + F * (float)r.Next(0, 3), z1 + 0.8f); }
                else { bool near = r.NextDouble() < 0.5; float wz = (z1 - z0) * k; a = new Vector3(x0 - 0.8f, y0, near ? z0 - 0.8f : z1 - wz); b = new Vector3(x1 + 0.8f, h + F * (float)r.Next(0, 3), near ? z0 + wz : z1 + 0.8f); }
                B(Solid()).Box(a, b);
                slabs.Box(new Vector3(a.x - 0.3f, b.y, a.z - 0.3f), new Vector3(b.x + 0.3f, b.y + 0.4f, b.z + 0.3f));
            }
            if (r.NextDouble() < 0.55)
            {
                float mx = (x0 + x1) * 0.5f, mz = (z0 + z1) * 0.5f, hw = (x1 - x0) * 0.22f, hd = (z1 - z0) * 0.22f;
                B(Glass()).Box(new Vector3(mx - hw, h + 0.45f, mz - hd), new Vector3(mx + hw, h + 0.45f + F, mz + hd));
                slabs.Box(new Vector3(mx - hw - 0.6f, h + 0.45f + F, mz - hd - 0.6f), new Vector3(mx + hw + 0.6f, h + 0.85f + F, mz + hd + 0.6f));
            }
            if (tower && r.NextDouble() < 0.5)
                roofs.Box(new Vector3((x0 + x1) * 0.5f - 0.15f, h + 0.45f, (z0 + z1) * 0.5f - 0.15f), new Vector3((x0 + x1) * 0.5f + 0.15f, h + 9f, (z0 + z1) * 0.5f + 0.15f));
        }
    }
}
