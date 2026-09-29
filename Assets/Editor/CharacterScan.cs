using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Phase 3 of docs/CHARACTER_PLAN.md: the clipping scan. In Play mode, puts a test person (mpfb_test) through every
    /// outfit in wardrobe.json, every body slider at both ends plus a few mixed bodies, and several poses, and measures
    /// two kinds of clipping on the skinned result:
    /// - fabric under visible skin (a garment vertex behind the body surface that is not hidden by the wardrobe);
    /// - an inner layer through an outer one (trousers through a tunic's hem, a tunic through a hijab).
    /// A case passes when nothing goes deeper than <see cref="Deep"/> and under <see cref="Share"/> of the garment's
    /// vertices go deeper than <see cref="Tolerance"/>. The report goes to the Console and Logs/character_scan.txt.
    /// </summary>
    public static class CharacterScan
    {
        const float Tolerance = 0.002f;   // skinning is linear, fabric may sit a hair inside at a bent joint
        const float Deep = 0.008f;
        const float Share = 0.005f;
        const float Reach = 0.02f;        // only surfaces this close count (anything further is a different part of the body)
        const float Settle = 0.7f;
        const float Near = 0.06f;         // how close two surfaces were at rest to be compared at all
        const float Fit = 0.04f;          // closer than this at rest: a garment and what it lies on. Further: two body parts
                                          // pressing together in a pose (arm on torso, belly on thigh), reported as contact        // seconds for the pose to blend in before measuring

        static readonly string[] Poses = { "Stand", "Walk", "Sit", "Crouch", "Exercise", "Wave" };
        static readonly string[] SwimPoses = { "Stand", "Walk", "Swim" };
        static readonly Dictionary<string, int> Layer = new Dictionary<string, int>
        {
            { "feet", 0 }, { "bottom", 1 }, { "top", 2 }, { "outfit", 2 }, { "head", 3 }
        };

        class Case { public string outfit, body, pose; public Dictionary<string, float> sliders; }

        static List<Case> cases;
        static int index;
        static double waitUntil;
        static GameObject person;
        static StringBuilder report;
        static int failed, passed;

        [MenuItem("Dearlife/Scan characters")]
        static void Start()
        {
            if (!Application.isPlaying) { Debug.LogWarning("Dearlife: enter Play mode first, the scan needs the animation running."); return; }
            cases = new List<Case>();
            var bodies = new List<(string, Dictionary<string, float>)> { ("neutral", new Dictionary<string, float>()) };
            foreach (var s in new[] { "gender", "weight", "muscle", "height", "proportions", "age" })
                foreach (var v in new[] { -1f, 1f })
                    bodies.Add(($"{s} {(v < 0 ? "low" : "high")}", new Dictionary<string, float> { { s, v } }));
            bodies.Add(("heavy woman", new Dictionary<string, float> { { "gender", -1f }, { "weight", 1f } }));
            bodies.Add(("strong tall man", new Dictionary<string, float> { { "gender", 1f }, { "muscle", 1f }, { "height", 1f } }));
            bodies.Add(("slim older woman", new Dictionary<string, float> { { "gender", -1f }, { "weight", -1f }, { "age", 1f } }));
            bodies.Add(("heavy short man", new Dictionary<string, float> { { "gender", 1f }, { "weight", 1f }, { "height", -1f } }));
            // the face sliders (phase 4): the hijab's opening and the hair must follow the face at its extremes
            bodies.Add(("broad face", new Dictionary<string, float> { { "gender", -1f }, { "faceWidth", 1f }, { "jaw", 1f }, { "cheekbones", 1f }, { "ears", 1f }, { "chin", 1f } }));
            bodies.Add(("long narrow face", new Dictionary<string, float> { { "gender", 1f }, { "faceLength", 1f }, { "faceWidth", -1f }, { "jaw", -1f }, { "brows", 1f }, { "eyeSize", 1f } }));
            foreach (var o in Wardrobe.Catalogue.outfits)
                foreach (var (name, sliders) in bodies)
                    // swimwear is only worn for a swim: it is checked standing, walking and swimming
                    foreach (var p in o.name.StartsWith("swim") ? SwimPoses : Poses)
                        cases.Add(new Case { outfit = o.name, body = name, pose = p, sliders = sliders });
            index = -1;
            ownerCache.Clear();
            failed = passed = 0;
            report = new StringBuilder();
            report.AppendLine($"Dearlife character clipping scan, {System.DateTime.Now:yyyy-MM-dd HH:mm}, v{GameInfo.Version}");
            report.AppendLine($"pass: nothing deeper than {Deep * 1000:0} mm and under {Share * 100:0.#}% of a garment's vertices deeper than {Tolerance * 1000:0} mm");
            report.AppendLine();
            Spawn();
            Next();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log($"Dearlife: clipping scan started, {cases.Count} cases (about {cases.Count * Settle / 60f:0} minutes).");
        }

        static void Spawn()
        {
            if (person) Object.Destroy(person);
            var src = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Art/Models/Characters/mpfb_test.fbx");
            person = Object.Instantiate(src);
            person.SetActive(false);
            person.name = "ClipScan";
            person.transform.position = new Vector3(0f, -50f, 0f);   // out of sight, below the lot
            var bs = person.AddComponent<BodyShape>();
            bs.shapeData = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Art/Models/Characters/mpfb_test.bodyshape.json");
            person.AddComponent<Eyes>();
            person.AddComponent<Wardrobe>().outfit = "";
            person.AddComponent<CharacterRig>().kind = "mpfb";
            person.SetActive(true);
        }

        static void Next()
        {
            index++;
            if (index >= cases.Count) { Finish(); return; }
            var c = cases[index];
            var w = person.GetComponent<Wardrobe>();
            if (index == 0 || cases[index - 1].outfit != c.outfit || !w.Worn.GetEnumerator().MoveNext()) w.Wear(c.outfit);
            var bs = person.GetComponent<BodyShape>();
            bs.values.Clear();
            foreach (var kv in c.sliders) bs.Set(kv.Key, kv.Value);
            bs.Apply();
            var rig = person.GetComponent<CharacterRig>();
            rig.pose = (CharacterRig.Pose)System.Enum.Parse(typeof(CharacterRig.Pose), c.pose);
            rig.walkSpeed = c.pose == "Walk" ? 1.2f : 0f;
            waitUntil = EditorApplication.timeSinceStartup + Settle;
        }

        static void Tick()
        {
            if (!Application.isPlaying) { EditorApplication.update -= Tick; if (report != null) Finish(); return; }
            if (!person) { Spawn(); index--; Next(); return; }   // something in the game removed the test person: start the case again
            if (EditorApplication.timeSinceStartup < waitUntil) return;
            Measure(cases[index]);
            Next();
        }

        static void Finish()
        {
            EditorApplication.update -= Tick;
            if (person) Object.Destroy(person);
            report.AppendLine();
            report.AppendLine($"{passed} passed, {failed} failed");
            Directory.CreateDirectory("Logs");
            File.WriteAllText("Logs/character_scan.txt", report.ToString());
            Debug.Log($"Dearlife: clipping scan done, {passed} passed, {failed} failed. Full report: Logs/character_scan.txt");
            report = null;
            cases = null;
        }

        // ------------------------------------------------------------------ measuring

        static readonly Dictionary<string, int[]> ownerCache = new Dictionary<string, int[]>();

        static int BitOf(string id)
        {
            foreach (var it in Wardrobe.Catalogue.items) if (it.id == id) return it.bit;
            return -1;
        }

        /// <summary>
        /// For every vertex of every piece worn, the cover bits of the body vertex it was made over (the nearest one at
        /// rest): which garments are meant to lie over that spot.
        /// </summary>
        static Dictionary<string, int[]> OwnerBits(SkinnedMeshRenderer body, List<(string id, string slot, SkinnedMeshRenderer r)> wear)
        {
            var result = new Dictionary<string, int[]>();
            Vector3[] bv = null; int[] bb = null;
            Dictionary<Vector3Int, List<int>> grid = null;
            const float cell = 0.03f;
            foreach (var x in wear)
            {
                if (ownerCache.TryGetValue(x.id, out var cached)) { result[x.id] = cached; continue; }
                if (bv == null)
                {
                    bv = body.sharedMesh.vertices;
                    var uv = new List<Vector2>();
                    body.sharedMesh.GetUVs(1, uv);
                    bb = new int[bv.Length];
                    for (int i = 0; i < uv.Count && i < bb.Length; i++) bb[i] = Mathf.RoundToInt(uv[i].x);
                    grid = new Dictionary<Vector3Int, List<int>>();
                    for (int i = 0; i < bv.Length; i++)
                    {
                        var k = new Vector3Int(Mathf.FloorToInt(bv[i].x / cell), Mathf.FloorToInt(bv[i].y / cell), Mathf.FloorToInt(bv[i].z / cell));
                        if (!grid.TryGetValue(k, out var l)) grid[k] = l = new List<int>();
                        l.Add(i);
                    }
                }
                var gv = x.r.sharedMesh.vertices;
                var own = new int[gv.Length];
                for (int i = 0; i < gv.Length; i++)
                {
                    var k0 = new Vector3Int(Mathf.FloorToInt(gv[i].x / cell), Mathf.FloorToInt(gv[i].y / cell), Mathf.FloorToInt(gv[i].z / cell));
                    float best = float.MaxValue; int bi = -1;
                    for (int a = -2; a <= 2; a++)
                        for (int b = -2; b <= 2; b++)
                            for (int c = -2; c <= 2; c++)
                                if (grid.TryGetValue(new Vector3Int(k0.x + a, k0.y + b, k0.z + c), out var l))
                                    foreach (int j in l)
                                    {
                                        float d = (bv[j] - gv[i]).sqrMagnitude;
                                        if (d < best) { best = d; bi = j; }
                                    }
                    own[i] = bi >= 0 ? bb[bi] : 0;
                }
                ownerCache[x.id] = own;
                result[x.id] = own;
            }
            return result;
        }

        class Surface
        {
            public Vector3[] v, n, rest;   // skinned now (world), and at rest (the mesh as made, all pieces share that space)
            public int[] t;
            readonly Dictionary<Vector3Int, List<int>> grid = new Dictionary<Vector3Int, List<int>>();
            const float Cell = 0.03f;

            public Surface(SkinnedMeshRenderer r)
            {
                var m = new Mesh();
                r.BakeMesh(m, true);
                var w = r.transform.localToWorldMatrix;
                v = m.vertices; n = m.normals;
                for (int i = 0; i < v.Length; i++) { v[i] = w.MultiplyPoint3x4(v[i]); n[i] = w.MultiplyVector(n[i]).normalized; }
                t = m.triangles;
                Object.DestroyImmediate(m);
                rest = r.sharedMesh.vertices;
                for (int k = 0; k < t.Length; k += 3)
                {
                    var b = new Bounds(v[t[k]], Vector3.zero);
                    b.Encapsulate(v[t[k + 1]]); b.Encapsulate(v[t[k + 2]]);
                    var lo = Key(b.min); var hi = Key(b.max);
                    for (int x = lo.x; x <= hi.x; x++)
                        for (int y = lo.y; y <= hi.y; y++)
                            for (int z = lo.z; z <= hi.z; z++)
                            {
                                var c = new Vector3Int(x, y, z);
                                if (!grid.TryGetValue(c, out var l)) grid[c] = l = new List<int>();
                                l.Add(k);
                            }
                }
            }

            static Vector3Int Key(Vector3 p) => new Vector3Int(Mathf.FloorToInt(p.x / Cell), Mathf.FloorToInt(p.y / Cell), Mathf.FloorToInt(p.z / Cell));

            /// <summary>
            /// Signed distance of p from the surface along its normal (negative = behind), if a triangle lies right over or
            /// under it. Only triangles that were next to p when the person was made count (pRest, within Near): a sleeve
            /// resting on a trouser leg in a pose is two parts touching, not a garment that fails to fit.
            /// </summary>
            public int lastTri = -1;
            public float lastRest;

            public bool Depth(Vector3 p, Vector3 pRest, out float depth, out Vector3 normal)
            {
                depth = 0f; normal = Vector3.zero; lastTri = -1;
                float best = Reach;
                bool found = false;
                var c0 = Key(p);
                for (int x = -1; x <= 1; x++)
                    for (int y = -1; y <= 1; y++)
                        for (int z = -1; z <= 1; z++)
                        {
                            if (!grid.TryGetValue(new Vector3Int(c0.x + x, c0.y + y, c0.z + z), out var l)) continue;
                            foreach (int k in l)
                            {
                                if ((rest[t[k]] - pRest).sqrMagnitude > Near * Near) continue;
                                var q = Closest(p, v[t[k]], v[t[k + 1]], v[t[k + 2]], out var bary);
                                float d = (p - q).magnitude;
                                if (d >= best) continue;
                                var nn = (n[t[k]] * bary.x + n[t[k + 1]] * bary.y + n[t[k + 2]] * bary.z).normalized;
                                float along = Vector3.Dot(p - q, nn);
                                // only straight over or under the surface: beside an edge (a hem, the rim of a sleeve) is not clipping
                                if ((p - q - nn * along).magnitude > Mathf.Abs(along) * 0.5f + 0.001f) continue;
                                best = d; depth = along; found = true; normal = nn; lastTri = k; lastRest = (rest[t[k]] - pRest).magnitude;
                            }
                        }
                return found;
            }

            static Vector3 Closest(Vector3 p, Vector3 a, Vector3 b, Vector3 c, out Vector3 bary)
            {
                // Ericson, Real-Time Collision Detection 5.1.5
                Vector3 ab = b - a, ac = c - a, ap = p - a;
                float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
                if (d1 <= 0f && d2 <= 0f) { bary = new Vector3(1, 0, 0); return a; }
                Vector3 bp = p - b; float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
                if (d3 >= 0f && d4 <= d3) { bary = new Vector3(0, 1, 0); return b; }
                float vc = d1 * d4 - d3 * d2;
                if (vc <= 0f && d1 >= 0f && d3 <= 0f) { float s = d1 / (d1 - d3); bary = new Vector3(1 - s, s, 0); return a + s * ab; }
                Vector3 cp = p - c; float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
                if (d6 >= 0f && d5 <= d6) { bary = new Vector3(0, 0, 1); return c; }
                float vb = d5 * d2 - d1 * d6;
                if (vb <= 0f && d2 >= 0f && d6 <= 0f) { float s = d2 / (d2 - d6); bary = new Vector3(1 - s, 0, s); return a + s * ac; }
                float va = d3 * d6 - d5 * d4;
                if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f) { float s = (d4 - d3) / ((d4 - d3) + (d5 - d6)); bary = new Vector3(0, 1 - s, s); return b + s * (c - b); }
                float den = 1f / (va + vb + vc), sv = vb * den, sw = vc * den;
                bary = new Vector3(1 - sv - sw, sv, sw);
                return a + ab * sv + ac * sw;
            }
        }

        static void Measure(Case c)
        {
            SkinnedMeshRenderer body = null;
            var wear = new List<(string id, string slot, SkinnedMeshRenderer r)>();
            foreach (var r in person.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                if (r.name.EndsWith("_body")) body = r;
                else if (r.name.StartsWith("wear_") && r.enabled)
                {
                    string id = r.name.Substring(5);
                    foreach (var it in Wardrobe.Catalogue.items) if (it.id == id && it.slot != "hair") wear.Add((id, it.slot, r));
                }
            }
            if (!body) return;
            var skin = new Surface(body);
            var surfaces = new Dictionary<string, Surface>();
            foreach (var x in wear) surfaces[x.id] = new Surface(x.r);

            var bits = OwnerBits(body, wear);
            var line = new StringBuilder();
            bool ok = true;
            foreach (var x in wear)
            {
                var g = surfaces[x.id];
                var own = bits[x.id];
                int over = 0, contact = 0; float worst = 0f;
                string worstWhat = "";
                Vector3 worstAt = Vector3.zero, worstHit = Vector3.zero;
                for (int i = 0; i < g.v.Length; i++)
                {
                    bool bad = false, touch = false;
                    // under skin that shows
                    // (fabric facing the skin is a hem folded inwards or a lining: behind the skin it is never seen)
                    if (skin.Depth(g.v[i], g.rest[i], out float d, out var sn) && -d > Tolerance && Vector3.Dot(g.n[i], sn) > 0f && skin.lastRest > Fit)
                        touch = true;
                    else if (-d > Tolerance && Vector3.Dot(g.n[i], sn) > 0f && skin.lastTri >= 0)
                    {
                        bad = true;
                        if (-d > worst) { worst = -d; worstWhat = "skin"; worstAt = g.rest[i]; worstHit = skin.rest[skin.t[skin.lastTri]]; }
                    }
                    // through an outer layer, where that layer is meant to cover this one (both cover the same bit of body)
                    foreach (var y in wear)
                    {
                        if (!Layer.TryGetValue(x.slot, out int lx) || !Layer.TryGetValue(y.slot, out int ly) || ly <= lx) continue;
                        int ybit = BitOf(y.id);
                        if (ybit < 0 || (own[i] & (1 << ybit)) == 0) continue;
                        if (surfaces[y.id].Depth(g.v[i], g.rest[i], out float e, out var on) && e > Tolerance && Vector3.Dot(g.n[i], on) > 0f)
                        {
                            if (surfaces[y.id].lastRest > Fit) { touch = true; continue; }
                            bad = true;
                            if (e > worst) { worst = e; worstWhat = y.id; worstAt = g.rest[i]; var os = surfaces[y.id]; worstHit = os.rest[os.t[os.lastTri]]; }
                        }
                    }
                    if (bad) over++;
                    else if (touch) contact++;
                }
                float share = over / (float)Mathf.Max(1, g.v.Length);
                bool pass = worst <= Deep && share <= Share;
                ok &= pass;
                // where, in Blender's axes (x left to right of the person, y front to back, z up), to fix it in the tool
                string at = worstWhat == "" ? "" : $" ({worstWhat} at {-worstAt.x:0.00}, {-worstAt.z:0.00}, {worstAt.y:0.00}, its part at {-worstHit.x:0.00}, {-worstHit.z:0.00}, {worstHit.y:0.00})";
                line.Append($"  {x.id} {(pass ? "ok" : "CLIPS")} {share * 100:0.00}% worst {worst * 1000:0.0} mm{at}{(contact > 0 ? $", {contact} contact" : "")};");
            }
            if (ok) passed++; else failed++;
            report.AppendLine($"{(ok ? "PASS" : "FAIL")} {c.outfit} / {c.body} / {c.pose}:{line}");
        }
    }
}
