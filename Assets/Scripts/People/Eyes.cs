using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Realistic eyes for the MPFB2 people (docs/CHARACTER_PLAN.md, phase 1). HDRP's Eye shader works in the eye's own
    /// object space: an eyeball whose cornea faces +Z, with the iris a disc of radius 0.22 on the plane z = -0.02 (see HDRP's
    /// EyeUtils.hlsl). MakeHuman's eyes are one skinned mesh for both, so they are hidden and two eyeballs of that exact
    /// shape are made here, sized and placed where MakeHuman's were, hung on the head bone. <see cref="BodyShape"/> calls
    /// <see cref="Refit"/> when the face changes shape.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class Eyes : MonoBehaviour
    {
        public Material eyeMaterial;

        // the eye in the shader's units: iris radius 0.22 is a real iris of about 6 mm, so 0.0367 units per mm
        const float Radius = 0.44f;          // a 12 mm eyeball
        const float CentreZ = -0.402f;       // the iris plane (z = -0.02) sits 10.4 mm in front of the centre
        const float CorneaRadius = 0.286f;   // the cornea is a 7.8 mm sphere...
        const float CorneaCentreZ = -0.174f; // ...bulging 3.6 mm in front of the iris
        const float CorneaTipZ = CorneaCentreZ + CorneaRadius;

        static Mesh ball;
        static Material recolour;
        Color iris = new Color(0f, 0f, 0f, 0f);    // clear: the eye material's own (brown) iris
        Material own;
        RenderTexture irisRT;
        readonly Transform[] eyes = new Transform[2];
        Transform head;
        SkinnedMeshRenderer source;
        Quaternion restLook;
        bool built;

        void Start() { Build(); }

        void Build()
        {
            if (built || !eyeMaterial) return;
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.name.Contains("high-poly")) source = r;
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name == "head") head = t;
            if (!source || !head) return;
            built = true;
            source.enabled = false;
            restLook = Quaternion.Inverse(head.rotation) * transform.rotation;   // at rest the eyes look where the body faces (+Z)
            if (!ball) ball = MakeBall();
            for (int i = 0; i < 2; i++)
            {
                var g = new GameObject(i == 0 ? "Eye L" : "Eye R");
                g.transform.SetParent(head, false);
                g.AddComponent<MeshFilter>().sharedMesh = ball;
                var mr = g.AddComponent<MeshRenderer>();
                mr.sharedMaterial = eyeMaterial;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eyes[i] = g.transform;
            }
            Refit();
            ApplyIris();
        }

        /// <summary>The eye colour (clear keeps the natural brown). The iris texture is recoloured into this person's own copy.</summary>
        public void SetIris(Color c)
        {
            iris = c;
            if (built) ApplyIris();
        }

        const string IrisTexture = "Texture2D_D8BF6575";

        void ApplyIris()
        {
            Material use = eyeMaterial;
            if (iris.a > 0.01f)
            {
                if (!recolour) recolour = Resources.Load<Material>("People/SkinLayers");
                var src = eyeMaterial.GetTexture(IrisTexture);
                if (recolour && src)
                {
                    if (!own) own = new Material(eyeMaterial) { name = eyeMaterial.name + " (own)" };
                    if (!irisRT)
                    {
                        irisRT = new RenderTexture(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { useMipMap = true, autoGenerateMips = true };
                        irisRT.Create();
                    }
                    recolour.SetColor("_Iris", iris);
                    Graphics.Blit(src, irisRT, recolour, 1);
                    own.SetTexture(IrisTexture, irisRT);
                    use = own;
                }
            }
            foreach (var e in eyes) if (e) e.GetComponent<MeshRenderer>().sharedMaterial = use;
        }

        void OnDestroy()
        {
            if (irisRT) { irisRT.Release(); Destroy(irisRT); }
            if (own) Destroy(own);
        }

        /// <summary>Puts the eyeballs where MakeHuman's (shape keyed) eyes are now, at their size.</summary>
        public void Refit()
        {
            if (!built) { Build(); return; }
            var baked = new Mesh();
            source.BakeMesh(baked, true);
            var verts = baked.vertices;
            var lo = new[] { Vector3.one * 1e9f, Vector3.one * 1e9f };
            var hi = new[] { Vector3.one * -1e9f, Vector3.one * -1e9f };
            var toRoot = transform.worldToLocalMatrix * source.transform.localToWorldMatrix;
            foreach (var v in verts)
            {
                var p = toRoot.MultiplyPoint3x4(v);
                int side = p.x < 0f ? 0 : 1;
                lo[side] = Vector3.Min(lo[side], p);
                hi[side] = Vector3.Max(hi[side], p);
            }
            Destroy(baked);
            for (int i = 0; i < 2; i++)
            {
                if (lo[i].x > hi[i].x) continue;
                // MakeHuman's eye is only the front of a ball (about 31 mm wide, 23 mm deep, the back cut off), so the size comes
                // from its width and the depth from its front: our cornea tip goes exactly where theirs is
                float r = Mathf.Max(hi[i].x - lo[i].x, hi[i].y - lo[i].y) * 0.5f;
                float scale = r / Radius;                                            // shader units to metres
                var originRoot = new Vector3((lo[i].x + hi[i].x) * 0.5f, (lo[i].y + hi[i].y) * 0.5f, hi[i].z - CorneaTipZ * scale);
                var e = eyes[i];
                e.position = transform.TransformPoint(originRoot);
                e.localRotation = restLook;
                e.localScale = Vector3.one * (scale * transform.lossyScale.x / Mathf.Max(head.lossyScale.x, 1e-6f));
            }
        }

        /// <summary>A sphere of radius 0.44 round (0, 0, -0.402) with the cornea cap pushed out in front, UVs a front projection.</summary>
        static Mesh MakeBall()
        {
            const int seg = 48, rings = 32;
            var v = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            var c = new Vector3(0f, 0f, CentreZ); var k = new Vector3(0f, 0f, CorneaCentreZ);
            for (int y = 0; y <= rings; y++)
            {
                float th = Mathf.PI * y / rings;                      // 0 at the front (+Z), pi at the back
                for (int x = 0; x <= seg; x++)
                {
                    float ph = 2f * Mathf.PI * x / seg;
                    var d = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Sin(th) * Mathf.Sin(ph), Mathf.Cos(th));
                    float t = Radius;
                    // where the ray from the eye's centre leaves the cornea sphere, if that is further out
                    var oc = c - k; float b = Vector3.Dot(oc, d), q = oc.sqrMagnitude - CorneaRadius * CorneaRadius, disc = b * b - q;
                    if (disc > 0f) t = Mathf.Max(t, -b + Mathf.Sqrt(disc));
                    var p = c + d * t;
                    v.Add(p);
                    uv.Add(new Vector2(p.x / (2f * Radius) + 0.5f, p.y / (2f * Radius) + 0.5f));
                }
            }
            for (int y = 0; y < rings; y++)
                for (int x = 0; x < seg; x++)
                {
                    int a = y * (seg + 1) + x, b2 = a + seg + 1;
                    tri.Add(a); tri.Add(a + 1); tri.Add(b2);
                    tri.Add(a + 1); tri.Add(b2 + 1); tri.Add(b2);
                }
            var m = new Mesh { name = "Eyeball" };
            m.SetVertices(v); m.SetUVs(0, uv); m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            // faces must point out of the eye; if the winding came out inside out, turn every triangle round
            int eq = (rings / 2) * (seg + 1) + seg / 4;
            if (Vector3.Dot(m.normals[eq], v[eq] - c) < 0f)
            {
                for (int i = 0; i < tri.Count; i += 3) { int s = tri[i + 1]; tri[i + 1] = tri[i + 2]; tri[i + 2] = s; }
                m.SetTriangles(tri, 0);
                m.RecalculateNormals();
            }
            m.RecalculateTangents(); m.RecalculateBounds();
            return m;
        }
    }
}
