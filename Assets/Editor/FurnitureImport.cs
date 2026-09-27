using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Tiramisu.EditorTools
{
    /// <summary>
    /// Prepares every FBX in Assets/Art/Models: import settings, and each Blender material slot
    /// is swapped (by name) for a shared Unity material in Assets/Art/Materials/Furniture.
    /// Slot names ending in "_main" are the part the colour options will tint later.
    /// Menu: Tiramisu > Import furniture (the greybox builder also runs it).
    /// </summary>
    public static class FurnitureImport
    {
        public const string ModelDir = "Assets/Art/Models";
        const string MatDir = "Assets/Art/Materials/Furniture";

        // Blender slot name -> texture set (null = plain), tint, smoothness range, metallic, neutral
        // (neutral = greyscale detail map so the tint sets the colour; needed for anything the colour options tint).
        // Unknown names get a neutral grey on purpose so they are easy to spot.
        static readonly Dictionary<string, (string tex, Color tint, Vector2 smooth, float metal, bool neutral)> Looks =
            new Dictionary<string, (string, Color, Vector2, float, bool)>
        {
            { "Fabric_main",  ("rough_linen", new Color(0.92f, 0.88f, 0.80f), new Vector2(0f, 0.22f), 0f, true) },
            { "Walnut",       ("american_walnut_veneer", new Color(0.50f, 0.33f, 0.20f), new Vector2(0.3f, 0.55f), 0f, true) },
            { "Marble_main",  ("marble_01", new Color(0.97f, 0.96f, 0.94f), new Vector2(0.5f, 0.78f), 0f, true) },
            { "BlackSteel",   (null, new Color(0.04f, 0.04f, 0.045f), new Vector2(0.78f, 0.78f), 1f, false) },
            { "Rug_main",     ("poly_wool_herringbone", new Color(0.90f, 0.85f, 0.76f), new Vector2(0f, 0.18f), 0f, true) },
            { "RugBorder",    ("rough_linen", new Color(0.50f, 0.40f, 0.31f), new Vector2(0f, 0.2f), 0f, true) },
            { "Cabinet_main", ("white_plaster_02", new Color(0.16f, 0.165f, 0.175f), new Vector2(0.2f, 0.4f), 0f, true) },
            { "Stainless",    (null, new Color(0.72f, 0.73f, 0.75f), new Vector2(0.68f, 0.68f), 1f, false) },
            { "Brass",        (null, new Color(0.80f, 0.60f, 0.30f), new Vector2(0.62f, 0.62f), 1f, false) },
            { "GlassDark",    (null, new Color(0.015f, 0.018f, 0.022f), new Vector2(0.95f, 0.95f), 0f, false) },
            { "Bulb",         (null, new Color(1f, 0.9f, 0.7f), new Vector2(0.9f, 0.9f), 0f, false) },
            { "Ceramic",      (null, new Color(0.96f, 0.96f, 0.95f), new Vector2(0.72f, 0.72f), 0f, false) },
            { "ClearGlass",   (null, new Color(0.9f, 0.96f, 0.98f, 0.12f), new Vector2(0.97f, 0.97f), 0f, false) },
            { "Mirror",       (null, new Color(0.82f, 0.86f, 0.88f), new Vector2(1f, 1f), 1f, false) },
            { "Rattan",       ("rough_linen", new Color(0.66f, 0.5f, 0.32f), new Vector2(0.05f, 0.25f), 0f, true) },
            { "PaintRed",     (null, new Color(0.62f, 0.05f, 0.05f), new Vector2(0.65f, 0.65f), 0.2f, false) },
            { "Car_main",     (null, new Color(0.45f, 0.03f, 0.06f), new Vector2(0.55f, 0.55f), 0.3f, false) },
            { "CarSilver",    (null, new Color(0.7f, 0.72f, 0.75f), new Vector2(0.55f, 0.55f), 0.35f, false) },
            { "Tire",         (null, new Color(0.03f, 0.03f, 0.03f), new Vector2(0.35f, 0.35f), 0f, false) },
            { "CarGlass",     (null, new Color(0.04f, 0.06f, 0.08f, 0.85f), new Vector2(0.97f, 0.97f), 0f, false) },
            { "Headlight",    (null, new Color(0.95f, 0.97f, 1f), new Vector2(0.95f, 0.95f), 0f, false) },
            { "Taillight",    (null, new Color(0.7f, 0.02f, 0.02f), new Vector2(0.9f, 0.9f), 0f, false) },
            { "Apple",        (null, new Color(0.55f, 0.05f, 0.04f), new Vector2(0.7f, 0.7f), 0f, false) },
            { "Orange",       (null, new Color(0.9f, 0.38f, 0.04f), new Vector2(0.5f, 0.5f), 0f, false) },
            { "Lemon",        (null, new Color(0.95f, 0.82f, 0.12f), new Vector2(0.5f, 0.5f), 0f, false) },
            { "Bread",        (null, new Color(0.5f, 0.28f, 0.11f), new Vector2(0.15f, 0.15f), 0f, false) },
            { "Terracotta",   (null, new Color(0.6f, 0.28f, 0.18f), new Vector2(0.2f, 0.2f), 0f, false) },
            { "Leaf",         (null, new Color(0.14f, 0.38f, 0.1f), new Vector2(0.45f, 0.45f), 0f, false) },
            { "Cardboard",    (null, new Color(0.58f, 0.42f, 0.25f), new Vector2(0.1f, 0.1f), 0f, false) },
            { "Wax",          (null, new Color(0.93f, 0.9f, 0.82f), new Vector2(0.4f, 0.4f), 0f, false) },
            { "Flame",        (null, new Color(1f, 0.7f, 0.2f), new Vector2(0.5f, 0.5f), 0f, false) },
            { "Coffee",       (null, new Color(0.08f, 0.05f, 0.03f), new Vector2(0.8f, 0.8f), 0f, false) },
            { "Bedding_main", ("rough_linen", new Color(0.86f, 0.66f, 0.68f), new Vector2(0f, 0.25f), 0f, true) },
            { "Bedding_dark", ("rough_linen", new Color(0.13f, 0.13f, 0.15f), new Vector2(0f, 0.25f), 0f, true) },
            { "Beanbag_main", ("rough_linen", new Color(0.22f, 0.34f, 0.36f), new Vector2(0f, 0.25f), 0f, true) },
            { "Leather",      (null, new Color(0.09f, 0.06f, 0.05f), new Vector2(0.5f, 0.5f), 0f, false) },
            { "Screen",       (null, new Color(0.15f, 0.3f, 0.5f), new Vector2(0.9f, 0.9f), 0f, false) },
            { "TVBlack",      (null, new Color(0.02f, 0.02f, 0.022f), new Vector2(0.5f, 0.5f), 0f, false) },
            { "Chalk",        (null, new Color(0.05f, 0.08f, 0.07f), new Vector2(0.15f, 0.15f), 0f, false) },
            { "Whiteboard",   (null, new Color(0.94f, 0.94f, 0.95f), new Vector2(0.85f, 0.85f), 0f, false) },
            { "Rubber",       (null, new Color(0.04f, 0.04f, 0.045f), new Vector2(0.25f, 0.25f), 0f, false) },
            { "Plastic",      (null, new Color(0.09f, 0.09f, 0.1f), new Vector2(0.45f, 0.45f), 0f, false) },
            { "PlasticWhite", (null, new Color(0.92f, 0.92f, 0.93f), new Vector2(0.6f, 0.6f), 0f, false) },
            { "MapSea",       (null, new Color(0.22f, 0.45f, 0.7f), new Vector2(0.4f, 0.4f), 0f, false) },
            { "MapLand",      (null, new Color(0.33f, 0.55f, 0.28f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "Globe",        (null, new Color(0.18f, 0.38f, 0.7f), new Vector2(0.6f, 0.6f), 0f, false) },
            { "BookRed",      (null, new Color(0.5f, 0.1f, 0.08f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "BookBlue",     (null, new Color(0.12f, 0.2f, 0.45f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "BookGreen",    (null, new Color(0.12f, 0.35f, 0.2f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "BookCream",    (null, new Color(0.8f, 0.75f, 0.6f), new Vector2(0.25f, 0.25f), 0f, false) },
            { "BookDark",     (null, new Color(0.13f, 0.1f, 0.09f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "BlueWater",    (null, new Color(0.3f, 0.6f, 0.9f), new Vector2(0.9f, 0.9f), 0f, false) },
            { "Mat_main",     (null, new Color(0.22f, 0.5f, 0.5f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "Planter_main", ("white_plaster_02", new Color(0.86f, 0.84f, 0.8f), new Vector2(0.15f, 0.4f), 0f, true) },
            { "Soil",         ("forest_ground_06", new Color(0.5f, 0.38f, 0.27f), new Vector2(0f, 0.1f), 0f, true) },
            { "RoofMetal",    ("box_profile_metal_sheet", new Color(0.30f, 0.32f, 0.35f), new Vector2(0.35f, 0.7f), 0.6f, true) },
            { "Skin",         (null, new Color(0.85f, 0.64f, 0.5f), new Vector2(0.35f, 0.35f), 0f, false) },
            { "Hair",         (null, new Color(0.07f, 0.05f, 0.04f), new Vector2(0.5f, 0.5f), 0f, false) },
            { "Hijab_main",   ("rough_linen", new Color(0.93f, 0.72f, 0.76f), new Vector2(0f, 0.2f), 0f, true) },
            { "Abaya_main",   ("rough_linen", new Color(0.72f, 0.65f, 0.85f), new Vector2(0f, 0.2f), 0f, true) },
            { "Shirt_main",   ("rough_linen", new Color(0.35f, 0.5f, 0.65f), new Vector2(0f, 0.2f), 0f, true) },
            { "Pants",        (null, new Color(0.15f, 0.16f, 0.19f), new Vector2(0.2f, 0.2f), 0f, false) },
            { "Shoe",         (null, new Color(0.92f, 0.92f, 0.9f), new Vector2(0.45f, 0.45f), 0f, false) },
            { "Eye",          (null, new Color(0.02f, 0.02f, 0.02f), new Vector2(0.9f, 0.9f), 0f, false) },
            { "Blush",        (null, new Color(0.95f, 0.55f, 0.55f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "FurCat",       (null, new Color(0.88f, 0.52f, 0.22f), new Vector2(0.15f, 0.15f), 0f, false) },
            { "FurCatLight",  (null, new Color(0.97f, 0.9f, 0.8f), new Vector2(0.15f, 0.15f), 0f, false) },
            { "FurCatBlack",  (null, new Color(0.06f, 0.055f, 0.06f), new Vector2(0.15f, 0.15f), 0f, false) },
            { "FurDog",       (null, new Color(0.8f, 0.58f, 0.3f), new Vector2(0.15f, 0.15f), 0f, false) },
            { "FurDogLight",  (null, new Color(0.95f, 0.88f, 0.75f), new Vector2(0.15f, 0.15f), 0f, false) },
            { "PetNose",      (null, new Color(0.1f, 0.07f, 0.07f), new Vector2(0.6f, 0.6f), 0f, false) },
            { "PetPink",      (null, new Color(0.95f, 0.6f, 0.65f), new Vector2(0.4f, 0.4f), 0f, false) },
            { "Teak",         ("wood_floor_deck", new Color(0.62f, 0.44f, 0.28f), new Vector2(0.15f, 0.42f), 0f, true) },
            { "Umbrella",     ("rough_linen", new Color(0.93f, 0.55f, 0.42f), new Vector2(0f, 0.25f), 0f, true) },
            { "StoneGrey",    ("precast_stone_paving", new Color(0.72f, 0.72f, 0.7f), new Vector2(0.1f, 0.45f), 0f, true) },
            { "OutdoorFabric_main", ("rough_linen", new Color(0.9f, 0.87f, 0.8f), new Vector2(0f, 0.2f), 0f, true) },
            { "FlowerPink",   (null, new Color(0.95f, 0.45f, 0.62f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "FlowerYellow", (null, new Color(0.98f, 0.82f, 0.2f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "FlowerWhite",  (null, new Color(0.96f, 0.96f, 0.93f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "Flamingo",     (null, new Color(0.96f, 0.45f, 0.55f), new Vector2(0.4f, 0.4f), 0f, false) },
            { "GnomeSkin",    (null, new Color(0.88f, 0.68f, 0.58f), new Vector2(0.3f, 0.3f), 0f, false) },
            { "CoolerBlue",   (null, new Color(0.15f, 0.42f, 0.75f), new Vector2(0.5f, 0.5f), 0f, false) },
            { "FireGlow",     (null, new Color(1f, 0.45f, 0.08f), new Vector2(0.4f, 0.4f), 0f, false) },
            { "BallRed",      (null, new Color(0.85f, 0.1f, 0.1f), new Vector2(0.6f, 0.6f), 0f, false) },
            { "BallWhite",    (null, new Color(0.95f, 0.95f, 0.95f), new Vector2(0.6f, 0.6f), 0f, false) },
            { "BallBlue",     (null, new Color(0.15f, 0.35f, 0.8f), new Vector2(0.6f, 0.6f), 0f, false) },
            { "BallYellow",   (null, new Color(0.95f, 0.8f, 0.1f), new Vector2(0.6f, 0.6f), 0f, false) },
            { "Cushion_main", ("rough_linen", new Color(0.60f, 0.68f, 0.54f), new Vector2(0f, 0.25f), 0f, true) },
        };

        [MenuItem("Tiramisu/Import furniture")]
        public static void ImportAll()
        {
            System.IO.Directory.CreateDirectory(MatDir);
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Characters/")) continue;   // downloaded, rigged models with their own textures, see ImportCharacters
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!imp) continue;

                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.animationType = ModelImporterAnimationType.None;
                imp.importNormals = ModelImporterNormals.Import;
                imp.isReadable = true;      // the walkable surface is built from these colliders while the game runs, which needs readable meshes in a built game
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;

                foreach (var slot in SlotNames(path))
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), slot), Material(slot));
                imp.SaveAndReimport();
            }
            ImportCharacters();
            Debug.Log("Tiramisu: furniture models imported and materials linked.");
        }

        /// <summary>
        /// The people and the cat come from Sketchfab (credits in Assets/Art/Models/Characters/CREDITS.txt), rigged in Blender by
        /// tools/blender_rig.py. Each FBX has a .materials.json next to it (material name, texture file, colour): build an HDRP Lit material
        /// for every entry and link it.
        /// </summary>
        static void ImportCharacters()
        {
            const string dir = ModelDir + "/Characters";
            if (!System.IO.Directory.Exists(dir)) return;
            System.IO.Directory.CreateDirectory("Assets/Art/Materials/Characters");
            foreach (var fbx in System.IO.Directory.GetFiles(dir, "*.fbx"))
            {
                string path = fbx.Replace("\\", "/");
                var imp = AssetImporter.GetAtPath(path) as ModelImporter;
                if (!imp) continue;
                imp.globalScale = 1f;
                imp.useFileScale = true;
                imp.importAnimation = false;
                imp.importCameras = false;
                imp.importLights = false;
                imp.animationType = ModelImporterAnimationType.Generic;   // keeps the skin: bones stay ordinary transforms that CharacterRig drives
                imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
                string json = path.Replace(".fbx", ".materials.json");
                if (!System.IO.File.Exists(json)) { imp.SaveAndReimport(); continue; }
                string text = System.IO.File.ReadAllText(json);
                var rx = new System.Text.RegularExpressions.Regex(@"""([^""]+)"":\s*\{\s*""texture"":\s*(null|""[^""]+""),\s*""color"":\s*\[\s*([-\d.eE]+),\s*([-\d.eE]+),\s*([-\d.eE]+)");
                foreach (System.Text.RegularExpressions.Match m in rx.Matches(text))
                {
                    string mname = m.Groups[1].Value;
                    string tex = m.Groups[2].Value.Trim('"');
                    var inv = System.Globalization.CultureInfo.InvariantCulture;
                    var col = new Color(float.Parse(m.Groups[3].Value, inv), float.Parse(m.Groups[4].Value, inv), float.Parse(m.Groups[5].Value, inv));
                    string mp = $"Assets/Art/Materials/Characters/{System.IO.Path.GetFileNameWithoutExtension(fbx)}_{mname}.mat";
                    bool fur = mname.Contains("Fur");
                    if (mname == "NormalFur") col = new Color(0.94f, 0.92f, 0.88f);   // the white of the calico
                    var mat = MaterialLibrary.Plain(mp, tex == "null" ? col : Color.white, mname.Contains("Eye") ? 0.8f : fur ? 0.25f : 0.35f);
                    if (tex != "null")
                    {
                        var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{tex}");
                        if (t) { mat.SetTexture("_BaseColorMap", t); mat.SetColor("_BaseColor", Color.white); }
                    }
                    if (mname.Contains("EyeColor")) { mat.SetFloat("_UseEmissiveIntensity", 0f); mat.SetColor("_EmissiveColor", new Color(0.1f, 0.6f, 0.15f) * 0.6f); }
                    UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
                    EditorUtility.SetDirty(mat);
                    imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), mname), mat);
                }
                imp.SaveAndReimport();
            }
        }

        static IEnumerable<string> SlotNames(string path)
        {
            var names = new HashSet<string>();
            foreach (var o in AssetDatabase.LoadAllAssetsAtPath(path))
                if (o is Material m) names.Add(m.name);
            // materials already remapped are not sub assets any more, so read them from the renderers too
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go)
                foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                    foreach (var m in r.sharedMaterials)
                        if (m) names.Add(m.name);
            return names;
        }

        public static Material Material(string name)
        {
            string path = $"{MatDir}/{name}.mat";
            if (!Looks.TryGetValue(name, out var l))
                return MaterialLibrary.Plain(path, new Color(0.6f, 0.6f, 0.6f), 0.3f);
            if (name == "ClearGlass" || name == "CarGlass") return MaterialLibrary.Glass(path, l.tint);
            if (l.tex == null)
            {
                var pm = MaterialLibrary.Plain(path, l.tint, l.smooth.y, l.metal);
                Color? glow = name == "Bulb" ? new Color(1f, 0.78f, 0.45f) * 2.6f
                    : name == "Headlight" ? new Color(0.9f, 0.95f, 1f) * 1.5f
                    : name == "Screen" ? new Color(0.3f, 0.55f, 0.95f) * 0.8f
                    : name == "FireGlow" ? new Color(1f, 0.4f, 0.08f) * 3f
                    : name == "Flame" ? new Color(1f, 0.6f, 0.15f) * 8f
                    : name == "Taillight" ? new Color(1f, 0.04f, 0.02f) * 12f : (Color?)null;
                if (glow.HasValue) // bulbs and car lights glow
                {
                    pm.SetFloat("_UseEmissiveIntensity", 0f);
                    pm.SetColor("_EmissiveColor", glow.Value);
                    UnityEditor.EditorUtility.SetDirty(pm);
                }
                return pm;
            }
            return MaterialLibrary.Textured(path, l.tex, MaterialLibrary.Mapping.UV0, l.tint, l.smooth, l.metal, 1f, 1f, l.neutral);
        }
    }
}
