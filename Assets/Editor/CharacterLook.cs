using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Phase 1 of docs/CHARACTER_PLAN.md: realistic materials for the MPFB2 people, using only what HDRP ships and textures
    /// made here (rule 8, free only). Skin uses HDRP's Skin shader graph (subsurface scattering, the Skin diffusion profile)
    /// with a generated tiling pore map; hair, brows and lashes use HDRP's Hair shader; the eyes get their own material on
    /// HDRP's Eye shader, with iris and sclera textures cut from MakeHuman's (CC0) eye texture. Called by
    /// <see cref="FurnitureImport"/> for every character material whose model name starts with "mpfb".
    /// </summary>
    public static class CharacterLook
    {
        const string ShaderDir = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/ShaderGraph/";
        const string ProfileDir = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/";
        public const string TexDir = "Assets/Art/Textures/Characters";
        public const string PoreMap = TexDir + "/skin_pores_detail.png";

        static Shader Graph(string n) => AssetDatabase.LoadAssetAtPath<Shader>(ShaderDir + n + ".shadergraph");
        static DiffusionProfileSettings Profile(string n) => AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(ProfileDir + n + ".asset");

        /// <summary>Gives a freshly made character material its realistic shader, by what it is for. Returns true if it changed it.</summary>
        public static bool Upgrade(Material mat, string model, string part, Texture2D baseMap)
        {
            if (!model.StartsWith("mpfb")) return false;
            EnsureProfiles();
            if (part == "body") { Skin(mat, baseMap); return true; }
            if (part.StartsWith("cut_")) { Hair(mat, baseMap, part.Contains("eyebrow") || part.Contains("eyelash")); return true; }
            if (part.StartsWith("teeth")) { mat.SetFloat("_Smoothness", 0.78f); return true; }
            return false;
        }

        static void Skin(Material mat, Texture2D baseMap)
        {
            mat.shader = Graph("Skin");
            if (baseMap) mat.SetTexture("_Base_Map", baseMap);
            mat.SetColor("_Color", Color.white);
            mat.SetFloat("_Smoothness", 0.52f);
            var pores = AssetDatabase.LoadAssetAtPath<Texture2D>(MakePoreMap());
            mat.SetTexture("_Detail_Map", pores);
            mat.SetFloat("_Detail_Tiling", 38f);
            mat.SetFloat("_Detail_Normal_Strength", 0.9f);
            mat.SetFloat("_Detail_Albedo_Strength", 0.35f);
            mat.SetFloat("_Detail_Smoothness_Strength", 0.6f);
            mat.SetFloat("_Subsurface_Dimmer", 1f);
            HDMaterial.SetDiffusionProfileShaderGraph(mat, Profile("SkinDiffusionProfile"), "_SkinDiffusionProfile");
            HDMaterial.ValidateMaterial(mat);
        }

        static void Hair(Material mat, Texture2D baseMap, bool fine)
        {
            mat.shader = Graph("Hair");
            var colour = Color.white;
            // head hair: our own strand map without MakeHuman's painted shine, and its colour as a tint (the hair shader
            // makes its own highlights, and a tint is what the character creator will change)
            if (baseMap && !fine) baseMap = Strands(baseMap, out colour);
            if (baseMap) mat.SetTexture("_BaseColorMap", baseMap);
            mat.SetColor("_BaseColor", colour);
            HDMaterial.SetAlphaClipping(mat, true);
            mat.SetFloat("_AlphaClipThreshold", fine ? 0.35f : 0.5f);
            mat.SetFloat("_AlphaClipThresholdDepthPrepass", 0.8f);
            mat.SetFloat("_AlphaClipThresholdDepthPostpass", 0.2f);
            mat.SetFloat("_AlphaThresholdShadow", 0.5f);
            mat.SetFloat("_SmoothnessMin", 0.35f);
            mat.SetFloat("_SmoothnessMax", 0.72f);
            mat.SetColor("_SpecularColor", new Color(0.92f, 0.86f, 0.8f));
            // a soft sheen only: MakeHuman's card UVs do not run along the strands everywhere, so strong highlights turn
            // into bright bands across the crown (seen at 0.5 on the secondary highlight)
            mat.SetFloat("_Specular", fine ? 0.08f : 0.12f);
            mat.SetFloat("_SpecularShift", 0.1f);
            mat.SetFloat("_SecondarySpecular", 0.08f);
            mat.SetFloat("_SecondarySpecularShift", -0.1f);
            mat.SetColor("_TransmissionColor", new Color(0.55f, 0.35f, 0.25f));
            mat.SetFloat("_TransmissionRim", 0.15f);
            mat.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.ValidateMaterial(mat);
        }

        /// <summary>The eye material for a model: iris and sclera cut from its MakeHuman eye texture.</summary>
        public static Material MakeEye(string model, Texture2D makeHumanEye)
        {
            EnsureProfiles();
            string matPath = $"Assets/Art/Materials/Characters/{model}_eye.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (!mat) { mat = new Material(Graph("Eye")); AssetDatabase.CreateAsset(mat, matPath); }
            mat.shader = Graph("Eye");
            if (makeHumanEye)
            {
                var (iris, sclera) = CutEye(model, makeHumanEye);
                mat.SetTexture("Texture2D_D8BF6575", iris);        // Iris Texture
                mat.SetTexture("Texture2D_5F873FC1", sclera);      // Sclera Texture
            }
            mat.SetColor("Color_83777D09", new Color(0.05f, 0.035f, 0.03f));   // Iris Clamp Color: the dark ring round the iris
            mat.SetFloat("Vector1_F084AE9E", 0.9f);                // Sclera Smoothness
            mat.SetFloat("Vector1_8F0D1174", 1f);                  // Cornea Smoothness
            mat.SetFloat("Vector1_DFF948F3", 0.30f);               // Pupil Radius (MakeHuman pupils are about a third of the iris)
            mat.SetFloat("Vector1_FEA38ABB", 0.5f);                // Pupil Aperture
            mat.SetFloat("Vector1_A6DA845F", 1.6f);                // Limbal Ring Intensity
            mat.SetFloat("_Mesh_Scale", 1f);
            HDMaterial.SetDiffusionProfileShaderGraph(mat, Profile("ScleraDiffusionProfile 25mm"), "DiffusionProfile_261f48f1fbc94ccbafc421414859c159");
            HDMaterial.SetDiffusionProfileShaderGraph(mat, Profile("IrisDiffusionProfile 25mm"), "DiffusionProfile_bfbe0deb8ec4428a9cfcdb968651903c");
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------------ diffusion profiles

        /// <summary>HDRP only renders subsurface profiles that are in the default volume's Diffusion Profile List.</summary>
        static void EnsureProfiles()
        {
            var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset");
            if (!vp || !vp.TryGet<DiffusionProfileList>(out var list)) return;
            var cur = new System.Collections.Generic.List<DiffusionProfileSettings>(list.diffusionProfiles.value ?? new DiffusionProfileSettings[0]);
            bool changed = false;
            foreach (var n in new[] { "SkinDiffusionProfile", "IrisDiffusionProfile 25mm", "ScleraDiffusionProfile 25mm" })
            {
                var p = Profile(n);
                if (p && !cur.Contains(p)) { cur.Add(p); changed = true; }
            }
            if (!changed) return;
            list.diffusionProfiles.value = cur.ToArray();
            list.diffusionProfiles.overrideState = true;
            EditorUtility.SetDirty(vp);
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ generated textures

        /// <summary>
        /// A seamless 512 px skin detail map in HDRP's detail layout (R albedo, G normal Y, B smoothness, A normal X, 0.5 is
        /// neutral): thousands of small soft pits (pores) over fine noise, slightly darker and less shiny inside.
        /// </summary>
        public static string MakePoreMap()
        {
            if (File.Exists(PoreMap)) return PoreMap;
            Directory.CreateDirectory(TexDir);
            const int n = 512;
            var h = new float[n * n];
            var rnd = new System.Random(20260928);
            for (int p = 0; p < 9000; p++)
            {
                float cx = (float)rnd.NextDouble() * n, cy = (float)rnd.NextDouble() * n;
                float r = 1.1f + (float)rnd.NextDouble() * 1.6f, depth = 0.5f + (float)rnd.NextDouble() * 0.5f;
                int ri = Mathf.CeilToInt(r * 2f);
                for (int dy = -ri; dy <= ri; dy++)
                    for (int dx = -ri; dx <= ri; dx++)
                    {
                        float d = Mathf.Sqrt(dx * dx + dy * dy) / (r * 2f);
                        if (d >= 1f) continue;
                        int x = ((int)cx + dx + n) % n, y = ((int)cy + dy + n) % n;
                        float fall = 1f - d * d * (3f - 2f * d);
                        h[y * n + x] -= depth * fall;
                    }
            }
            // fine grain on top: a few octaves of tiled value noise
            for (int o = 0; o < 3; o++)
            {
                int cells = 32 << o;
                var g = new float[cells * cells];
                for (int i = 0; i < g.Length; i++) g[i] = (float)rnd.NextDouble() - 0.5f;
                float amp = 0.18f / (o + 1);
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float fx = x * cells / (float)n, fy = y * cells / (float)n;
                        int x0 = (int)fx, y0 = (int)fy, x1 = (x0 + 1) % cells, y1 = (y0 + 1) % cells;
                        float tx = fx - x0, ty = fy - y0;
                        tx = tx * tx * (3f - 2f * tx); ty = ty * ty * (3f - 2f * ty);
                        float v = Mathf.Lerp(Mathf.Lerp(g[y0 * cells + x0], g[y0 * cells + x1], tx), Mathf.Lerp(g[y1 * cells + x0], g[y1 * cells + x1], tx), ty);
                        h[y * n + x] += v * amp;
                    }
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true);
            const float strength = 2.2f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float hx = h[y * n + (x + 1) % n] - h[y * n + (x - 1 + n) % n];
                    float hy = h[((y + 1) % n) * n + x] - h[((y - 1 + n) % n) * n + x];
                    var nrm = new Vector3(-hx * strength, -hy * strength, 1f).normalized;
                    float pit = Mathf.Clamp01(-h[y * n + x]);
                    tex.SetPixel(x, y, new Color(0.5f - pit * 0.12f, nrm.y * 0.5f + 0.5f, 0.5f - pit * 0.25f, nrm.x * 0.5f + 0.5f));
                }
            File.WriteAllBytes(PoreMap, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(PoreMap);
            var imp = (TextureImporter)AssetImporter.GetAtPath(PoreMap);
            imp.sRGBTexture = false;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return PoreMap;
        }

        /// <summary>
        /// A neutral strand map from a MakeHuman hair texture: brightness divided by its own heavy blur keeps every strand
        /// and drops the broad painted shine and shading; the result is grey (tinted by the material) with the alpha kept.
        /// </summary>
        static Texture2D Strands(Texture2D src, out Color colour)
        {
            string srcPath = AssetDatabase.GetAssetPath(src);
            string path = srcPath.Replace(".png", "_strands.png").Replace("Models/Characters", "Textures/Characters");
            var imp = (TextureImporter)AssetImporter.GetAtPath(srcPath);
            if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
            int w = src.width, h = src.height;
            var px = src.GetPixels();
            var lum = new float[w * h]; var wt = new float[w * h];
            double r = 0, g = 0, b = 0, n = 0;
            for (int i = 0; i < px.Length; i++)
            {
                float a = px[i].a > 0.5f ? 1f : 0f;
                lum[i] = px[i].grayscale * a; wt[i] = a;
                if (a > 0f) { r += px[i].r; g += px[i].g; b += px[i].b; n++; }
            }
            colour = n > 0 ? new Color((float)(r / n), (float)(g / n), (float)(b / n)) : Color.white;
            var bl = Blur(lum, w, h, 40); var bw = Blur(wt, w, h, 40);
            var outPx = new Color[px.Length];
            for (int i = 0; i < px.Length; i++)
            {
                float mean = bw[i] > 1e-4f ? bl[i] / bw[i] : 0.5f;
                float d = mean > 1e-4f ? Mathf.Clamp(px[i].grayscale / mean, 0.35f, 1.7f) : 1f;
                float v = Mathf.Clamp01(d * 0.72f);
                outPx[i] = new Color(v, v, v, px[i].a);
            }
            // the material multiplies this grey (about 0.72 on average) by the tint, so lift the tint to keep the brightness
            colour = new Color(Mathf.Clamp01(colour.r / 0.72f), Mathf.Clamp01(colour.g / 0.72f), Mathf.Clamp01(colour.b / 0.72f));
            Directory.CreateDirectory(TexDir);
            var t = new Texture2D(w, h, TextureFormat.RGBA32, true);
            t.SetPixels(outPx); t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.alphaIsTransparency = true;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Separable box blur with running sums (radius in pixels, edges clamped).</summary>
        static float[] Blur(float[] src, int w, int h, int rad)
        {
            var tmp = new float[w * h]; var dst = new float[w * h];
            for (int y = 0; y < h; y++)
            {
                float s = 0f; int row = y * w;
                for (int x = -rad; x <= rad; x++) s += src[row + Mathf.Clamp(x, 0, w - 1)];
                for (int x = 0; x < w; x++)
                {
                    tmp[row + x] = s / (2 * rad + 1);
                    s += src[row + Mathf.Min(x + rad + 1, w - 1)] - src[row + Mathf.Max(x - rad, 0)];
                }
            }
            for (int x = 0; x < w; x++)
            {
                float s = 0f;
                for (int y = -rad; y <= rad; y++) s += tmp[Mathf.Clamp(y, 0, h - 1) * w + x];
                for (int y = 0; y < h; y++)
                {
                    dst[y * w + x] = s / (2 * rad + 1);
                    s += tmp[Mathf.Min(y + rad + 1, h - 1) * w + x] - tmp[Mathf.Max(y - rad, 0) * w + x];
                }
            }
            return dst;
        }

        /// <summary>
        /// MakeHuman eye textures show two eyeballs from the front. The lower left one is cut twice: the iris alone (the
        /// texture edge is the iris edge, as HDRP's eye shader wants) and the whole front of the eyeball for the sclera, scaled
        /// so its iris lands where the shader puts the iris (radius 0.22 on a 0.44 eyeball, see Eyes.cs).
        /// </summary>
        static (Texture2D iris, Texture2D sclera) CutEye(string model, Texture2D src)
        {
            string srcPath = AssetDatabase.GetAssetPath(src);
            var imp = (TextureImporter)AssetImporter.GetAtPath(srcPath);
            if (!imp.isReadable) { imp.isReadable = true; imp.SaveAndReimport(); }
            float s = src.width / 1024f;
            var c = new Vector2(297f, 297f) * s;              // centre of the lower left eye, from the bottom left corner
            // find the iris edge: walk out from the pupil until the colour turns into the pale sclera
            float total = 0f; int rays = 16;
            for (int k = 0; k < rays; k++)
            {
                var dir = new Vector2(Mathf.Cos(k * Mathf.PI * 2f / rays), Mathf.Sin(k * Mathf.PI * 2f / rays));
                float r = 60f * s;
                for (; r < 220f * s; r += 1f)
                {
                    var p = c + dir * r;
                    var col = src.GetPixel((int)p.x, (int)p.y);
                    if (col.grayscale > 0.55f) break;
                }
                total += r;
            }
            float irisR = total / rays * 0.95f;                // the walk overshoots into the soft edge; the shader adds its own limbal ring
            var iris = Cut(src, c, irisR, 512, $"{TexDir}/{model}_iris.png");
            var sclera = Cut(src, c, irisR * 2f, 1024, $"{TexDir}/{model}_sclera.png");
            return (iris, sclera);
        }

        static Texture2D Cut(Texture2D src, Vector2 centre, float halfSize, int size, string path)
        {
            Directory.CreateDirectory(TexDir);
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = centre.x + ((x + 0.5f) / size * 2f - 1f) * halfSize;
                    float v = centre.y + ((y + 0.5f) / size * 2f - 1f) * halfSize;
                    t.SetPixel(x, y, src.GetPixelBilinear(u / src.width, v / src.height));
                }
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var ti = (TextureImporter)AssetImporter.GetAtPath(path);
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
