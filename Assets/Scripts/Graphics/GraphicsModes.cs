using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife
{
    /// <summary>
    /// Three graphics modes (rule 6):
    ///   Ultra        DLSS Quality + ray traced reflections (RTX cards)
    ///   Quality      DLSS Quality + screen space reflections and GI (the default)
    ///   Performance  DLSS Balanced, no screen space GI, no volumetric fog, lighter shadows
    /// Without DLSS (non NVIDIA cards) the game renders at native resolution with TAA instead.
    /// The choice is remembered per PC. G cycles modes.
    /// </summary>
    public class GraphicsModes : MonoBehaviour
    {
        public enum Mode { Ultra, Quality, Performance }
        public static GraphicsModes Instance { get; private set; }

        public Volume volume;
        public Mode mode = Mode.Quality;
        const string PrefKey = "dearlife.graphicsMode";

        HDAdditionalCameraData cam;

        public bool DlssAvailable => HDDynamicResolutionPlatformCapabilities.DLSSDetected;
        public bool RayTracingAvailable => SystemInfo.supportsRayTracing;

        void Awake()
        {
            Instance = this;
            mode = (Mode)PlayerPrefs.GetInt(PrefKey, (int)Mode.Quality);
        }

        void Start()
        {
            var c = Camera.main;
            if (c) cam = c.GetComponent<HDAdditionalCameraData>();
            Apply(mode);
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.G)) Apply((Mode)(((int)mode + 1) % 3));
        }

        public string Label
        {
            get
            {
                string m = mode == Mode.Ultra ? "Ultra" : mode == Mode.Quality ? "Quality" : "Performance";
                return DlssAvailable ? $"{m} · DLSS" : m;
            }
        }

        public void Apply(Mode m)
        {
            if (m == Mode.Ultra && !RayTracingAvailable) m = Mode.Quality;
            mode = m;
            PlayerPrefs.SetInt(PrefKey, (int)m);

            if (cam)
            {
                bool dlss = DlssAvailable;
                cam.allowDynamicResolution = dlss;
                cam.allowDeepLearningSuperSampling = dlss;
                cam.deepLearningSuperSamplingUseCustomQualitySettings = true;
                cam.deepLearningSuperSamplingUseOptimalSettings = true;
                // NVIDIA quality modes: 0 max performance, 1 balanced, 2 max quality
                cam.deepLearningSuperSamplingQuality = (uint)(m == Mode.Performance ? 1 : 2);
                cam.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            }

            if (!volume || !volume.profile) return;
            var p = volume.profile;
            if (p.TryGet(out ScreenSpaceReflection ssr))
                ssr.tracing.Override(m == Mode.Ultra ? RayCastingMode.RayTracing : RayCastingMode.RayMarching);
            if (p.TryGet(out GlobalIllumination gi))
            {
                // SSGI was the single most expensive effect (6.4 ms at 1080p on an RTX 4050), so Quality
                // runs it at half resolution on the low preset; Ultra keeps it full resolution and high.
                gi.enable.Override(m != Mode.Performance);
                gi.fullResolutionSS.Override(m == Mode.Ultra);
                gi.quality.Override(m == Mode.Ultra ? (int)ScalableSettingLevelParameter.Level.High : (int)ScalableSettingLevelParameter.Level.Low);
            }
            if (p.TryGet(out Fog fog))
                fog.enableVolumetricFog.Override(m != Mode.Performance);
            if (p.TryGet(out ContactShadows cs))
                cs.enable.Override(m != Mode.Performance);
            if (p.TryGet(out HDShadowSettings sh))
                sh.cascadeShadowSplitCount.Override(m == Mode.Performance ? 2 : 4);
        }
    }
}
