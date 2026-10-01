using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// Brings in the ready made people (v0.49.0). Each folder in Assets/Local/People holds a rigged model prepared by
    /// tools/blender_person.py: the FBX, its textures and a .person.json (materials, and which bone is which). For each
    /// one this builds HDRP Lit materials (skin with subsurface scattering, hair cut out by its alpha, see through
    /// parts), makes the model Humanoid with a T pose so the CMU motion capture fits it, and saves a prefab with
    /// <see cref="PersonModel"/> and <see cref="CharacterRig"/> in Assets/Local/Resources/People, where
    /// <see cref="Residents"/> finds it. Assets/Local is gitignored: downloaded characters stay on this computer.
    /// </summary>
    public static class PersonImport
    {
        const string Root = "Assets/Local/People";
        const string PrefabDir = "Assets/Local/Resources/People";
        const string ProfileDir = "Packages/com.unity.render-pipelines.high-definition/Runtime/RenderPipelineResources/";

        [System.Serializable] class Bone { public string human, bone; }

        [System.Serializable]
        class Mat
        {
            public string name, @base, normal, mask, emissive, alpha, kind;
            public float opacity = 1f;
            public float[] color;
        }

        [System.Serializable]
        class Person
        {
            public string id, name, credit;
            public float feminine = 0.5f, height = 1.7f;
            public Bone[] bones;
            public Mat[] materials;
        }

        [MenuItem("Dearlife/Import people")]
        public static void Import()
        {
            if (!Directory.Exists(Root)) { Debug.LogWarning($"Dearlife: {Root} is not there. Prepare a person with tools/blender_person.py first."); return; }
            AssetDatabase.Refresh();
            EnsureProfile();
            Directory.CreateDirectory(PrefabDir);
            var log = new System.Text.StringBuilder();
            foreach (var d in Directory.GetDirectories(Root))
            {
                string dir = d.Replace("\\", "/");
                string id = Path.GetFileName(dir);
                string json = $"{dir}/{id}.person.json", fbx = $"{dir}/{id}.fbx";
                if (!File.Exists(json) || !File.Exists(fbx)) continue;
                var person = JsonUtility.FromJson<Person>(File.ReadAllText(json));
                log.AppendLine(One(dir, fbx, person));
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("Dearlife: people imported.\n" + log);
        }

        static string One(string dir, string fbx, Person person)
        {
            var imp = (ModelImporter)AssetImporter.GetAtPath(fbx);
            imp.globalScale = 1f;
            imp.useFileScale = true;
            imp.importAnimation = false;
            imp.importCameras = false;
            imp.importLights = false;
            imp.importBlendShapes = false;
            imp.isReadable = true;                       // the outline of the body is measured from the mesh below
            imp.meshCompression = ModelImporterMeshCompression.Off;
            imp.importNormals = ModelImporterNormals.Import;
            imp.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            foreach (var m in person.materials)
                imp.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), m.name), Material(dir, person.id, m));
            imp.SaveAndReimport();

            var map = new Dictionary<string, string>();
            foreach (var b in person.bones) map[b.human] = b.bone;
            bool ok = CharacterAnimation.MakeHumanoid(fbx, map, out var report);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
            go.name = person.id;
            var pm = go.AddComponent<PersonModel>();
            pm.id = person.id; pm.displayName = person.name; pm.feminine = person.feminine; pm.height = person.height; pm.credit = person.credit ?? "";
            go.AddComponent<CharacterRig>().kind = "person";
            Outline(go, map, pm);
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true)) r.updateWhenOffscreen = true;
            PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{person.id}.prefab");
            Object.DestroyImmediate(go);
            return $"{person.name} ({person.id}): {person.materials.Length} materials, {person.height:0.00} m, humanoid {(ok ? "ok" : "FAILED")}. {report}";
        }

        /// <summary>
        /// The outline of the body and clothes without the arms, slice by slice (see <see cref="PersonModel.bodyWide"/>).
        /// Every vertex counts for the bone that moves it most; those of the arms are left out.
        /// </summary>
        static void Outline(GameObject go, Dictionary<string, string> map, PersonModel pm)
        {
            var all = go.GetComponentsInChildren<Transform>(true);
            Transform Bone(string human) => map.TryGetValue(human, out var n) ? System.Array.Find(all, t => t.name == n) : null;
            var hips = Bone("Hips"); var la = Bone("LeftUpperArm"); var ra = Bone("RightUpperArm");
            if (!hips || !la || !ra) return;
            var arms = new HashSet<Transform>(la.GetComponentsInChildren<Transform>(true));
            arms.UnionWith(ra.GetComponentsInChildren<Transform>(true));
            var root = go.transform;
            var c = root.InverseTransformPoint(hips.position);
            float top = Mathf.Min(root.InverseTransformPoint(la.position).y, root.InverseTransformPoint(ra.position).y);
            int n = Mathf.Max(1, Mathf.CeilToInt(top / PersonModel.BodyStep));
            float[] wide = new float[n], front = new float[n], back = new float[n];
            foreach (var r in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh = r.sharedMesh;
                if (!mesh) continue;
                var verts = mesh.vertices; var bw = mesh.boneWeights; var binds = mesh.bindposes; var bones = r.bones;
                if (bw.Length != verts.Length) continue;
                var mats = new Matrix4x4[bones.Length];
                for (int b = 0; b < bones.Length && b < binds.Length; b++) if (bones[b]) mats[b] = root.worldToLocalMatrix * bones[b].localToWorldMatrix * binds[b];
                for (int i = 0; i < verts.Length; i++)
                {
                    int b = bw[i].boneIndex0;                    // the heaviest weight comes first
                    if (b < 0 || b >= bones.Length || !bones[b] || arms.Contains(bones[b])) continue;
                    var l = mats[b].MultiplyPoint3x4(verts[i]);
                    int k = Mathf.FloorToInt(l.y / PersonModel.BodyStep);
                    if (k < 0 || k >= n) continue;
                    wide[k] = Mathf.Max(wide[k], Mathf.Abs(l.x - c.x));
                    front[k] = Mathf.Max(front[k], l.z - c.z);
                    back[k] = Mathf.Max(back[k], c.z - l.z);
                }
            }
            // each slice also takes the widest of its neighbours: the body moves a little under the arms
            pm.bodyWide = new float[n]; pm.bodyFront = new float[n]; pm.bodyBack = new float[n];
            for (int k = 0; k < n; k++)
                for (int d = -1; d <= 1; d++)
                {
                    int q = Mathf.Clamp(k + d, 0, n - 1);
                    pm.bodyWide[k] = Mathf.Max(pm.bodyWide[k], wide[q]);
                    pm.bodyFront[k] = Mathf.Max(pm.bodyFront[k], front[q]);
                    pm.bodyBack[k] = Mathf.Max(pm.bodyBack[k], back[q]);
                }
        }

        // ------------------------------------------------------------------ materials

        static Texture2D Tex(string dir, string file, bool normal, bool linear, bool alpha = false)
        {
            if (string.IsNullOrEmpty(file)) return null;
            string path = $"{dir}/{file}";
            var imp = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!imp) return null;
            bool dirty = false;
            var type = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
            if (imp.textureType != type) { imp.textureType = type; dirty = true; }
            if (!normal && imp.sRGBTexture == linear) { imp.sRGBTexture = !linear; dirty = true; }
            if (imp.anisoLevel != 8) { imp.anisoLevel = 8; dirty = true; }
            if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; dirty = true; }
            if (imp.textureCompression != TextureImporterCompression.CompressedHQ) { imp.textureCompression = TextureImporterCompression.CompressedHQ; dirty = true; }
            if (alpha && !imp.alphaIsTransparency) { imp.alphaIsTransparency = true; dirty = true; }   // no dark fringe round the strands
            if (alpha && !imp.mipMapsPreserveCoverage) { imp.mipMapsPreserveCoverage = true; imp.alphaTestReferenceValue = 0.35f; dirty = true; }   // hair keeps its fullness far away
            if (dirty) imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Material(string dir, string id, Mat m)
        {
            string safe = string.Concat(m.name.Split(Path.GetInvalidFileNameChars()));
            var tint = m.color != null && m.color.Length >= 3 ? new Color(m.color[0], m.color[1], m.color[2]) : Color.white;
            bool skin = m.kind == "skin", hair = m.kind == "hair", eye = m.kind == "eye";
            var mat = MaterialLibrary.Plain($"{dir}/Materials/{id}_{safe}.mat", tint, eye ? 0.9f : skin ? 0.45f : hair ? 0.4f : 0.3f);
            bool clip = m.alpha == "clip", blend = m.alpha == "blend";

            var baseMap = Tex(dir, m.@base, false, false, clip);
            var normal = Tex(dir, m.normal, true, true);
            var mask = Tex(dir, m.mask, false, true);
            var glow = Tex(dir, m.emissive, false, false);
            mat.SetTexture("_BaseColorMap", baseMap);
            mat.SetTexture("_NormalMap", normal);
            mat.SetTexture("_MaskMap", mask);
            if (mask)
            {
                mat.SetFloat("_MetallicRemapMin", 0f); mat.SetFloat("_MetallicRemapMax", skin || hair ? 0f : 1f);
                mat.SetFloat("_SmoothnessRemapMin", 0f); mat.SetFloat("_SmoothnessRemapMax", skin ? 0.7f : 1f);
                mat.SetFloat("_AORemapMin", 1f); mat.SetFloat("_AORemapMax", 1f);
            }
            mat.SetFloat("_MaterialID", skin ? 0f : 1f);            // subsurface scattering for skin, standard otherwise
            if (skin)
            {
                var profile = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(ProfileDir + "SkinDiffusionProfile.asset");
                if (profile) HDMaterial.SetDiffusionProfile(mat, profile);
                mat.SetFloat("_SubsurfaceMask", 0.7f);
            }
            mat.SetFloat("_AlphaCutoffEnable", clip ? 1f : 0f);
            mat.SetFloat("_AlphaCutoff", 0.35f);
            mat.SetFloat("_DoubleSidedEnable", clip || blend ? 1f : 0f);    // hair cards and lashes are seen from both sides
            mat.SetFloat("_SurfaceType", blend ? 1f : 0f);
            if (blend)
            {
                mat.SetFloat("_BlendMode", 0f);
                tint.a = Mathf.Clamp01(m.opacity);
                mat.SetColor("_BaseColor", tint);
                mat.SetFloat("_Smoothness", 0.92f);          // the wet layer over an eye, a watch glass
            }
            if (glow)
            {
                mat.SetTexture("_EmissiveColorMap", glow);
                mat.SetFloat("_UseEmissiveIntensity", 0f);
                mat.SetColor("_EmissiveColor", Color.white);
            }
            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        /// <summary>HDRP only renders subsurface profiles that are in the default volume's Diffusion Profile List.</summary>
        static void EnsureProfile()
        {
            var vp = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/HDRPDefaultResources/DefaultSettingsVolumeProfile.asset");
            if (!vp || !vp.TryGet<DiffusionProfileList>(out var list)) return;
            var skin = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(ProfileDir + "SkinDiffusionProfile.asset");
            var cur = new List<DiffusionProfileSettings>(list.diffusionProfiles.value ?? new DiffusionProfileSettings[0]);
            if (!skin || cur.Contains(skin)) return;
            cur.Add(skin);
            list.diffusionProfiles.value = cur.ToArray();
            list.diffusionProfiles.overrideState = true;
            EditorUtility.SetDirty(vp);
            AssetDatabase.SaveAssets();
        }
    }
}
