using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// What the game needs before it is built as a program: names, window, icon, DirectX 12 only, shaders that are looked up by name kept in
    /// the build, then Dearlife > Build Windows game makes the exe in S:\Dearlife Builds.
    /// </summary>
    public static class BuildPrep
    {
        const string OutDir = @"S:\Dearlife Builds\Dearlife";

        [MenuItem("Dearlife/Prepare for build")]
        public static void Prepare()
        {
            PlayerSettings.companyName = "Zetazuni";
            PlayerSettings.productName = "Dearlife";
            PlayerSettings.bundleVersion = GameInfo.Version;
            PlayerSettings.fullScreenMode = FullScreenMode.FullScreenWindow;
            PlayerSettings.defaultScreenWidth = 1920;
            PlayerSettings.defaultScreenHeight = 1080;
            PlayerSettings.resizableWindow = true;
            PlayerSettings.runInBackground = true;
            PlayerSettings.allowFullscreenSwitch = true;
            PlayerSettings.SplashScreen.show = true;
            PlayerSettings.SplashScreen.showUnityLogo = true;

            // no code is stripped: HDRP finds its volume and sky types by looking through the assemblies while the game runs
            PlayerSettings.SetManagedStrippingLevel(NamedBuildTarget.Standalone, ManagedStrippingLevel.Disabled);

            // the people and pets: their meshes are copied while the game runs (Lily's glasses are cut out), which needs readable meshes
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Models/Characters" }))
            {
                var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }
            }

            // DirectX 12 only, the pipeline needs it
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12 });

            // the icon of the 2D game: SetIcons needs one texture per required size (1024 down to 16),
            // in order, or it silently keeps Unity's default icon - and each one has to be a real
            // imported asset (a Texture2D made only in memory doesn't stick), so they're written to disk
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Icon/icon.png");
            if (icon)
            {
                var mi = AssetImporter.GetAtPath("Assets/Art/Icon/icon.png") as TextureImporter;
                if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }

                const string genDir = "Assets/Art/Icon/Generated";
                Directory.CreateDirectory(genDir);
                var target = NamedBuildTarget.Standalone;
                foreach (var kind in new[] { IconKind.Application, IconKind.Any })
                {
                    var sizes = PlayerSettings.GetIconSizes(target, kind);
                    var set = new Texture2D[sizes.Length];
                    for (int i = 0; i < sizes.Length; i++)
                    {
                        var path = $"{genDir}/icon_{sizes[i]}.png";
                        if (!File.Exists(path))
                        {
                            File.WriteAllBytes(path, ResizeIcon(icon, sizes[i]).EncodeToPNG());
                            AssetDatabase.ImportAsset(path);
                        }
                        set[i] = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    }
                    PlayerSettings.SetIcons(target, set, kind);
                }
            }

            // shaders that are found by name while the game runs must not be stripped
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "HDRP/Lit", "HDRP/Unlit", "Hidden/Dearlife/Recolor" })
            {
                var sh = Shader.Find(name);
                if (!sh) continue;
                bool have = false;
                for (int i = 0; i < list.arraySize; i++) if (list.GetArrayElementAtIndex(i).objectReferenceValue == sh) have = true;
                if (have) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = sh;
            }
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();
            Debug.Log("Dearlife: build settings prepared.");
        }

        /// <summary>A resized copy of an icon texture, read back off the GPU so the source doesn't need Read/Write enabled.</summary>
        static Texture2D ResizeIcon(Texture2D src, int size)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            var prev = RenderTexture.active;
            Graphics.Blit(src, rt);
            RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            t.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            return t;
        }

        [MenuItem("Dearlife/Build Windows game")]
        public static void Build()
        {
            Prepare();
            // Unity's incremental player-build cache can report Succeeded without rewriting the exe
            // (seen after the Editor's own install path changed) - starting from nothing avoids that
            if (Directory.Exists(OutDir)) Directory.Delete(OutDir, true);
            Directory.CreateDirectory(OutDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = Path.Combine(OutDir, "Dearlife.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"Dearlife: build {report.summary.result}, {report.summary.totalSize / 1048576} MB, {report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, in {report.summary.totalTime}. {opts.locationPathName}");
        }
    }
}
