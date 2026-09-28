using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Materials and small textures for the bathroom effects (BathTub, ShowerStall), made here as assets in
    /// Resources/Effects so a built game has their shader variants: foam and bath water (HDRP Lit), and steam, water drops
    /// and soap bubbles for particles (HDRP Unlit, see-through). The textures are drawn in code.
    /// </summary>
    public static class WaterEffectsSetup
    {
        const string Dir = "Assets/Resources/Effects";

        [MenuItem("Dearlife/Make bathroom effects")]
        public static void Make()
        {
            Directory.CreateDirectory(Dir);
            var soft = Texture("soft_puff", 64, (u, v) => { float d = Mathf.Clamp01(Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f); float a = Mathf.Pow(1f - d, 2.2f); return new Color(1f, 1f, 1f, a); });
            var drop = Texture("water_drop", 32, (u, v) => { float x = Mathf.Abs(u - 0.5f) * 2f; float a = Mathf.Clamp01(1f - x * x) * Mathf.Clamp01(1f - Mathf.Abs(v - 0.5f) * 1.6f); return new Color(1f, 1f, 1f, a); });
            var bubble = Texture("soap_bubble", 64, (u, v) =>
            {
                float d = Vector2.Distance(new Vector2(u, v), new Vector2(0.5f, 0.5f)) * 2f;
                if (d > 1f) return new Color(1f, 1f, 1f, 0f);
                float rim = Mathf.Pow(d, 6f);                                          // bright thin edge, clear middle
                float glint = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.36f, 0.66f)) * 9f);
                float a = Mathf.Clamp01(0.12f + rim * 0.85f + glint);
                return new Color(0.92f + 0.08f * glint, 0.96f, 1f, a);
            });

            Unlit("steam", soft, new Color(1f, 1f, 1f, 0.14f));
            Unlit("drops", drop, new Color(0.9f, 0.95f, 1f, 0.7f));
            Unlit("bubbles", bubble, new Color(1f, 1f, 1f, 0.9f));
            Lit("foam", new Color(0.94f, 0.94f, 0.95f), 0.3f, false);
            Lit("bathwater", new Color(0.62f, 0.78f, 0.84f, 0.55f), 0.96f, true);
            AssetDatabase.SaveAssets();
            Debug.Log("Dearlife: bathroom effect materials made in " + Dir);
        }

        static Texture2D Texture(string name, int n, System.Func<float, float, Color> f)
        {
            string path = $"{Dir}/{name}.png";
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                    t.SetPixel(x, y, f((x + 0.5f) / n, (y + 0.5f) / n));
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Get(string name, string shader)
        {
            string path = $"{Dir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m) { m = new Material(Shader.Find(shader)); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find(shader);
            return m;
        }

        static void Unlit(string name, Texture2D tex, Color c)
        {
            var m = Get(name, "HDRP/Unlit");
            m.SetTexture("_UnlitColorMap", tex);
            m.SetColor("_UnlitColor", c);
            m.SetFloat("_SurfaceType", 1f);      // transparent
            m.SetFloat("_BlendMode", 0f);        // alpha
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_ZWrite", 0f);
            // drawn before refraction, or the shower's glass (a refractive surface) hides whatever is behind it
            HDMaterial.ValidateMaterial(m);
            m.renderQueue = PreRefraction;
            EditorUtility.SetDirty(m);
        }

        // HDRP's "before refraction" range of the render queue: see-through things drawn before glass that refracts
        const int PreRefraction = 2750;

        static void Lit(string name, Color c, float smooth, bool clear)
        {
            var m = Get(name, "HDRP/Lit");
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", 0f);
            if (clear)
            {
                m.SetFloat("_SurfaceType", 1f);
                m.SetFloat("_BlendMode", 0f);
                m.SetFloat("_ZWrite", 0f);
            }
            HDMaterial.ValidateMaterial(m);
            if (clear) m.renderQueue = PreRefraction;
            EditorUtility.SetDirty(m);
        }
    }
}
