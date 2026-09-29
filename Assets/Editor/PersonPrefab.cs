using System.IO;
using UnityEditor;
using UnityEngine;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Makes Resources/People/Person.prefab: the MPFB2 person (mpfb_test) with everything a created person needs to be
    /// shaped, dressed and animated (BodyShape, Eyes, Wardrobe, CharacterRig for the MPFB skeleton, PersonLook). The
    /// character creator and <see cref="Residents"/> make every created person from it. Run again after the model changes.
    /// </summary>
    public static class PersonPrefab
    {
        const string Model = "Assets/Art/Models/Characters/mpfb_test.fbx";
        const string Shape = "Assets/Art/Models/Characters/mpfb_test.bodyshape.json";
        const string Path = "Assets/Resources/People/Person.prefab";

        [MenuItem("Dearlife/Make person prefab")]
        public static void Make()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            if (!model) { Debug.LogWarning("Dearlife: " + Model + " not found."); return; }
            Directory.CreateDirectory("Assets/Resources/People");
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = "Person";
            var bs = go.AddComponent<BodyShape>();
            bs.shapeData = AssetDatabase.LoadAssetAtPath<TextAsset>(Shape);
            go.AddComponent<Eyes>().eyeMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Characters/mpfb_test_eye.mat");
            go.AddComponent<Wardrobe>().outfit = "";
            go.AddComponent<CharacterRig>().kind = "mpfb";
            go.AddComponent<PersonLook>();
            PrefabUtility.SaveAsPrefabAsset(go, Path);
            Object.DestroyImmediate(go);
            MakeSkinLayers();
            Debug.Log("Dearlife: made " + Path);
        }

        const string LayersMat = "Assets/Resources/People/SkinLayers.mat";

        /// <summary>
        /// The material PersonLook draws the creator's details with (Hidden/Dearlife/SkinLayers), holding the three masks
        /// from tools/blender_skin_layers.py. It lives in Resources, which also keeps the shader in a built game.
        /// </summary>
        [MenuItem("Dearlife/Make skin layers material")]
        public static void MakeSkinLayers()
        {
            var shader = Shader.Find("Hidden/Dearlife/SkinLayers");
            if (!shader) { Debug.LogWarning("Dearlife: Hidden/Dearlife/SkinLayers is missing."); return; }
            var mat = AssetDatabase.LoadAssetAtPath<Material>(LayersMat);
            if (!mat) { mat = new Material(shader); AssetDatabase.CreateAsset(mat, LayersMat); }
            mat.shader = shader;
            string[] props = { "_LayersA", "_LayersB", "_LayersC" };
            string[] files = { "skin_layers_a", "skin_layers_b", "skin_layers_c" };
            for (int i = 0; i < 3; i++)
            {
                string tp = $"{CharacterLook.TexDir}/{files[i]}.png";
                var ti = AssetImporter.GetAtPath(tp) as TextureImporter;
                if (!ti) { Debug.LogWarning("Dearlife: " + tp + " not found (run tools/blender_skin_layers.py)."); continue; }
                if (ti.sRGBTexture || ti.textureCompression != TextureImporterCompression.CompressedHQ)
                {
                    ti.sRGBTexture = false;                                   // masks, not colours
                    ti.textureCompression = TextureImporterCompression.CompressedHQ;
                    ti.mipmapEnabled = false;                                 // only read at full size by the blit
                    ti.SaveAndReimport();
                }
                mat.SetTexture(props[i], AssetDatabase.LoadAssetAtPath<Texture2D>(tp));
            }
            EditorUtility.SetDirty(mat);
            AssetDatabase.SaveAssets();
        }
    }
}
