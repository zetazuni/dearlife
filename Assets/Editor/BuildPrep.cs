using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

namespace Tiramisu.EditorTools
{
    /// <summary>
    /// What the game needs before it is built as a program: names, window, icon, DirectX 12 only, shaders that are looked up by name kept in
    /// the build, then Tiramisu > Build Windows game makes the exe in S:\Tiramisu Builds.
    /// </summary>
    public static class BuildPrep
    {
        const string OutDir = @"S:\Tiramisu Builds\Tiramisu 3D";

        [MenuItem("Tiramisu/Prepare for build")]
        public static void Prepare()
        {
            PlayerSettings.companyName = "Zetazuni";
            PlayerSettings.productName = "Tiramisu 3D";
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

            // the people and pets: their meshes are copied while the game runs (Athirah's glasses are cut out), which needs readable meshes
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Art/Models/Characters" }))
            {
                var mi = AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid)) as ModelImporter;
                if (mi != null && !mi.isReadable) { mi.isReadable = true; mi.SaveAndReimport(); }
            }

            // DirectX 12 only, the pipeline needs it
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12 });

            // the icon of the 2D game
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Icon/icon.png");
            if (icon) PlayerSettings.SetIcons(NamedBuildTarget.Standalone, new[] { icon }, IconKind.Application);

            // shaders that are found by name while the game runs must not be stripped
            var gs = AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/GraphicsSettings.asset");
            var so = new SerializedObject(gs);
            var list = so.FindProperty("m_AlwaysIncludedShaders");
            foreach (var name in new[] { "HDRP/Lit", "HDRP/Unlit", "Hidden/Tiramisu/Recolor" })
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
            Debug.Log("Tiramisu: build settings prepared.");
        }

        [MenuItem("Tiramisu/Build Windows game")]
        public static void Build()
        {
            Prepare();
            Directory.CreateDirectory(OutDir);
            var opts = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/Scenes/Main.unity" },
                locationPathName = Path.Combine(OutDir, "Tiramisu3D.exe"),
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(opts);
            Debug.Log($"Tiramisu: build {report.summary.result}, {report.summary.totalSize / 1048576} MB, {report.summary.totalErrors} errors, {report.summary.totalWarnings} warnings, in {report.summary.totalTime}. {opts.locationPathName}");
        }
    }
}
