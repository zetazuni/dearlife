using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife
{
    /// <summary>
    /// Press F9 (or call Run) to measure the frame rate of each graphics mode and of Quality with one
    /// effect switched off at a time. Each step warms up for 2 s and measures for 6 s. Results go to
    /// the Console and to benchmark.txt in the persistent data folder.
    /// </summary>
    public class FpsBenchmark : MonoBehaviour
    {
        public bool running;
        public string lastReport = "";

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F9) && !running) Run();
        }

        public void Run() => StartCoroutine(Go());

        IEnumerator Go()
        {
            running = true;
            QualitySettings.vSyncCount = 0;
            Application.targetFrameRate = -1;
            var g = GraphicsModes.Instance;
            var p = g.volume.profile;
            p.TryGet(out GlobalIllumination gi);
            p.TryGet(out Fog fog);
            p.TryGet(out ScreenSpaceReflection ssr);
            p.TryGet(out ScreenSpaceAmbientOcclusion ao);
            p.TryGet(out ContactShadows cs);
            p.TryGet(out CloudLayer clouds);

            var steps = new List<(string name, Action setup)>
            {
                ("Performance", () => g.Apply(GraphicsModes.Mode.Performance)),
                ("Quality", () => g.Apply(GraphicsModes.Mode.Quality)),
                ("Quality without SSGI", () => { g.Apply(GraphicsModes.Mode.Quality); gi.enable.value = false; }),
                ("Quality without volumetric fog", () => { g.Apply(GraphicsModes.Mode.Quality); fog.enableVolumetricFog.value = false; }),
                ("Quality without SSR", () => { g.Apply(GraphicsModes.Mode.Quality); ssr.enabled.value = false; }),
                ("Quality without AO", () => { g.Apply(GraphicsModes.Mode.Quality); ao.intensity.value = 0f; }),
                ("Quality without contact shadows", () => { g.Apply(GraphicsModes.Mode.Quality); cs.enable.value = false; }),
                ("Ultra", () => g.Apply(GraphicsModes.Mode.Ultra)),
            };

            var mode = g.mode;
            float aoWas = ao != null ? ao.intensity.value : 1f;
            var lines = new List<string> { $"Benchmark {Screen.width}x{Screen.height}, DLSS {(g.DlssAvailable ? "on" : "off")}, {SystemInfo.graphicsDeviceName}" };
            foreach (var s in steps)
            {
                if (ssr != null) ssr.enabled.value = true;
                if (ao != null) ao.intensity.value = aoWas;
                s.setup();
                yield return new WaitForSecondsRealtime(2f);
                int f0 = Time.frameCount; float t0 = Time.realtimeSinceStartup;
                yield return new WaitForSecondsRealtime(6f);
                float fps = (Time.frameCount - f0) / (Time.realtimeSinceStartup - t0);
                lines.Add($"{s.name}: {fps:0.0} fps ({1000f / fps:0.0} ms)");
                Debug.Log("Dearlife benchmark · " + lines[lines.Count - 1]);
            }
            if (ssr != null) ssr.enabled.value = true;
            if (ao != null) ao.intensity.value = aoWas;
            g.Apply(mode);
            lastReport = string.Join("\n", lines);
            System.IO.File.WriteAllText(System.IO.Path.Combine(Application.persistentDataPath, "benchmark.txt"), lastReport);
            Debug.Log("Dearlife benchmark done\n" + lastReport);
            running = false;
        }
    }
}
