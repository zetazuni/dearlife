using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Builds HDRP/Lit materials from the texture sets in Assets/Art/Textures (see tools/fetch_textures.py).
    /// Every set tiles at its true real world size from textures.json.
    /// Mapping: Planar for floors (world XZ), Triplanar for walls and blocks (no stretching on the
    /// greybox cubes), UV0 for Blender models (their UVs are in metres).
    /// </summary>
    public static class MaterialLibrary
    {
        public enum Mapping { UV0 = 0, Planar = 4, Triplanar = 5 }

        const string TexDir = "Assets/Art/Textures";
        static Dictionary<string, Vector2> sizes;

        static Vector2 SizeOf(string id)
        {
            if (sizes == null)
            {
                sizes = new Dictionary<string, Vector2>();
                var json = System.IO.File.ReadAllText($"{TexDir}/textures.json");
                foreach (Match m in Regex.Matches(json, "\"(\\w+)\":\\s*\\{[^}]*?\"width_m\":\\s*([\\d.]+),\\s*\"height_m\":\\s*([\\d.]+)"))
                    sizes[m.Groups[1].Value] = new Vector2(float.Parse(m.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture),
                                                           float.Parse(m.Groups[3].Value, System.Globalization.CultureInfo.InvariantCulture));
            }
            return sizes.TryGetValue(id, out var s) ? s : Vector2.one * 2f;
        }

        static Texture2D Tex(string id, string suffix, bool normal, bool linear)
        {
            string path = $"{TexDir}/{id}/{id}_{suffix}";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!imp) return null;
            bool dirty = false;
            if (normal && imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; dirty = true; }
            if (linear && imp.sRGBTexture) { imp.sRGBTexture = false; dirty = true; }
            if (imp.anisoLevel != 8) { imp.anisoLevel = 8; dirty = true; }
            if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.CompressedHQ) { imp.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
            if (dirty) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Load(string path)
        {
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("HDRP/Lit");
            if (!m) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
            else if (m.shader != shader) m.shader = shader;
            return m;
        }

        /// <summary>A textured surface. smooth is the smoothness range the roughness map is remapped into.
        /// neutral uses the greyscale colour map so tint sets the real colour.</summary>
        public static Material Textured(string path, string texId, Mapping mapping, Color tint,
            Vector2 smooth, float metal = 0f, float normalScale = 1f, float scale = 1f, bool neutral = false)
        {
            var m = Load(path);
            // neutral = the greyscale copy, so the tint alone sets the colour and the texture only adds detail
            m.SetTexture("_BaseColorMap", Tex(texId, neutral ? "neutral.jpg" : "diff.jpg", false, false));
            m.SetTexture("_NormalMap", Tex(texId, "nor_gl.jpg", true, true));
            m.SetTexture("_MaskMap", Tex(texId, "mask.png", false, true));
            m.SetColor("_BaseColor", tint);
            m.SetFloat("_NormalScale", normalScale);
            m.SetFloat("_SmoothnessRemapMin", smooth.x);
            m.SetFloat("_SmoothnessRemapMax", smooth.y);
            m.SetFloat("_MetallicRemapMin", 0f);
            m.SetFloat("_MetallicRemapMax", metal);
            m.SetFloat("_AORemapMin", 0.1f);
            m.SetFloat("_AORemapMax", 1f);

            Vector2 size = SizeOf(texId) * scale;
            m.SetFloat("_UVBase", (float)mapping);
            if (mapping == Mapping.UV0)
            {
                m.SetTextureScale("_BaseColorMap", new Vector2(1f / size.x, 1f / size.y));
                m.SetFloat("_TexWorldScale", 1f);
            }
            else
            {
                m.SetTextureScale("_BaseColorMap", Vector2.one);
                m.SetFloat("_TexWorldScale", 1f / size.x);
                m.SetFloat("_ObjectSpaceUVMapping", 0f);
            }
            Finish(m);
            return m;
        }

        /// <summary>An untextured surface, for things like black steel or leaves until they get their own sets.</summary>
        public static Material Plain(string path, Color c, float smooth, float metal = 0f)
        {
            var m = Load(path);
            m.SetTexture("_BaseColorMap", null);
            m.SetTexture("_NormalMap", null);
            m.SetTexture("_MaskMap", null);
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            m.SetFloat("_UVBase", 0f);
            Finish(m);
            return m;
        }

        /// <summary>Thin architectural glass: refracts, reflects the room and the sky, barely tinted.</summary>
        public static Material Glass(string path, Color tint)
        {
            var m = Plain(path, tint, 0.97f);
            m.SetFloat("_SurfaceType", 1f);          // transparent
            m.SetFloat("_BlendMode", 0f);            // alpha
            m.SetFloat("_RefractionModel", 3f);      // thin
            m.SetFloat("_Ior", 1.5f);
            m.SetColor("_TransmittanceColor", new Color(0.93f, 0.97f, 0.98f));
            m.SetFloat("_ATDistance", 1f);
            m.SetFloat("_ReceivesSSRTransparent", 1f);
            m.SetFloat("_EnableBlendModePreserveSpecularLighting", 1f);
            m.SetFloat("_DoubleSidedEnable", 1f);
            Finish(m);
            return m;
        }

        /// <summary>Water with refraction, for the pool until the HDRP water system is set up.</summary>
        public static Material Water(string path)
        {
            var m = Glass(path, new Color(0.35f, 0.62f, 0.68f, 0.35f));
            m.SetFloat("_RefractionModel", 1f);      // planar (thick)
            m.SetFloat("_Ior", 1.33f);
            m.SetColor("_TransmittanceColor", new Color(0.55f, 0.85f, 0.88f));
            m.SetFloat("_ATDistance", 2.5f);
            m.SetFloat("_Smoothness", 0.98f);
            Finish(m);
            return m;
        }

        static void Finish(Material m)
        {
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(m);
            EditorUtility.SetDirty(m);
        }
    }
}
