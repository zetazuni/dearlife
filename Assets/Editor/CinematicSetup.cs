using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife.EditorTools
{
    /// <summary>
    /// The film look (rules 5 and 6) on HDRP: physically based sky with clouds, volumetric fog,
    /// camera-like auto exposure, screen space reflections, global illumination, ambient occlusion,
    /// contact shadows, physically based lights, filmic grade and depth of field.
    /// Called by GreyboxBuilder; the numbers are meant to be tuned by eye.
    /// </summary>
    public static class CinematicSetup
    {
        const string AssetPath = "Assets/Settings/Dearlife_HDRP.asset";
        const string ProfilePath = "Assets/Settings/Cinematic.asset";
        const string LightingPath = "Assets/Settings/Lighting.lighting";

        // ---------- pipeline ----------

        public static HDRenderPipelineAsset Pipeline()
        {
            System.IO.Directory.CreateDirectory("Assets/Settings");
            var asset = AssetDatabase.LoadAssetAtPath<HDRenderPipelineAsset>(AssetPath);
            if (!asset)
            {
                asset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                AssetDatabase.CreateAsset(asset, AssetPath);
            }

            var so = new SerializedObject(asset);
            const string s = "m_RenderPipelineSettings.";
            Set(so, s + "supportSSR", true);
            Set(so, s + "supportSSRTransparent", true);
            Set(so, s + "supportSSAO", true);
            Set(so, s + "supportSSGI", true);
            Set(so, s + "supportVolumetrics", true);
            Set(so, s + "supportVolumetricClouds", true);
            Set(so, s + "supportWater", true);
            Set(so, s + "supportDecals", true);
            Set(so, s + "supportDistortion", true);
            Set(so, s + "supportTransparentBackface", true);
            Set(so, s + "supportMotionVectors", true);
            Set(so, s + "lightLoopSettings.maxAreaLightsOnScreen", 512);        // default 64: more area lights than that on screen flicker
            Set(so, s + "lightLoopSettings.maxPunctualLightsOnScreen", 2048);
            Set(so, s + "supportRayTracing", true);                     // for the Ultra mode later, needs DX12
            Set(so, s + "hdShadowInitParams.maxDirectionalShadowMapResolution", 4096);
            Set(so, s + "hdShadowInitParams.directionalShadowFilteringQuality", 2); // high (PCSS soft shadows)
            Set(so, s + "hdShadowInitParams.punctualShadowFilteringQuality", 2);
            Set(so, s + "hdShadowInitParams.supportContactShadows", true);
            Set(so, s + "hdShadowInitParams.supportScreenSpaceShadows", true);
            // dynamic resolution with NVIDIA DLSS (the graphics modes pick the DLSS quality)
            Set(so, s + "dynamicResolutionSettings.enabled", true);
            Set(so, s + "dynamicResolutionSettings.enableDLSS", true);
            Set(so, s + "dynamicResolutionSettings.dynResType", 1);            // hardware
            Set(so, s + "dynamicResolutionSettings.DLSSUseOptimalSettings", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(asset);

            GraphicsSettings.defaultRenderPipeline = asset;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = asset;
            }
            QualitySettings.SetQualityLevel(current, false);
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D12 });
            AssetDatabase.SaveAssets();
            return asset;
        }

        static void Set(SerializedObject so, string path, object value)
        {
            var p = so.FindProperty(path);
            if (p == null) { Debug.LogWarning($"Dearlife: HDRP setting {path} not found, skipped."); return; }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break; // for enums this sets the value, not the list position
                case float f: p.floatValue = f; break;
            }
        }

        // ---------- volume: sky, fog, lighting effects and grade ----------

        public static Volume PostVolume()
        {
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath)) AssetDatabase.DeleteAsset(ProfilePath);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, ProfilePath);

            var env = Add<VisualEnvironment>(p);
            env.skyType.Override((int)SkyType.PhysicallyBased);
            env.cloudType.Override((int)CloudType.CloudLayer);
            env.skyAmbientMode.Override(SkyAmbientMode.Dynamic);

            var sky = Add<PhysicallyBasedSky>(p);
            sky.groundTint.Override(new Color(0.45f, 0.52f, 0.36f));

            var clouds = Add<CloudLayer>(p);
            clouds.opacity.Override(0.85f);

            var fog = Add<Fog>(p);
            fog.enabled.Override(true);
            fog.meanFreePath.Override(420f);
            fog.baseHeight.Override(0f);
            fog.maximumHeight.Override(80f);
            fog.enableVolumetricFog.Override(true);
            fog.albedo.Override(new Color(1f, 0.97f, 0.93f));
            fog.anisotropy.Override(0.6f);

            var exp = Add<Exposure>(p);
            exp.mode.Override(ExposureMode.AutomaticHistogram);
            exp.meteringMode.Override(MeteringMode.CenterWeighted);
            exp.limitMin.Override(9f);    // opens up for dark interiors and night
            exp.limitMax.Override(13.8f); // EV 15 is real full sun but looks gloomy after ACES, 13.6 to 13.8 reads right
            exp.compensation.Override(0f);
            exp.adaptationSpeedDarkToLight.Override(2f);
            exp.adaptationSpeedLightToDark.Override(1.5f);

            var shadows = Add<HDShadowSettings>(p);
            shadows.maxShadowDistance.Override(90f);
            shadows.cascadeShadowSplitCount.Override(4);

            var contact = Add<ContactShadows>(p);
            contact.enable.Override(true);
            contact.length.Override(0.2f);

            var ssr = Add<ScreenSpaceReflection>(p);
            ssr.enabled.Override(true);
            ssr.enabledTransparent.Override(true);

            var ssgi = Add<GlobalIllumination>(p);
            ssgi.enable.Override(true);

            var ao = Add<ScreenSpaceAmbientOcclusion>(p);
            ao.intensity.Override(1.1f);
            ao.radius.Override(1.5f);

            var tone = Add<Tonemapping>(p);
            tone.mode.Override(TonemappingMode.ACES);

            var bloom = Add<Bloom>(p);
            bloom.intensity.Override(0.18f);
            bloom.scatter.Override(0.72f);
            bloom.tint.Override(new Color(1f, 0.94f, 0.86f));

            var grade = Add<ColorAdjustments>(p);
            grade.contrast.Override(10f);
            grade.saturation.Override(6f);

            var wb = Add<WhiteBalance>(p);
            wb.temperature.Override(6f);
            wb.tint.Override(2f);

            var smh = Add<ShadowsMidtonesHighlights>(p);
            smh.shadows.Override(new Vector4(0.96f, 0.98f, 1.06f, 0f));
            smh.highlights.Override(new Vector4(1.05f, 1.0f, 0.94f, 0f));

            var vig = Add<Vignette>(p);
            vig.intensity.Override(0.22f);
            vig.smoothness.Override(0.45f);

            var grain = Add<FilmGrain>(p);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.1f);

            var dof = Add<DepthOfField>(p);
            // Manual ranges driven by CinematicFocus: sharp from a third of the way to the subject out to
            // 1.6x its distance, softening beyond. (Physical camera mode read the camera's own 10 m focus
            // and blurred the house, so it is not used.) No motion blur: it smeared every camera spin.
            dof.focusMode.Override(DepthOfFieldMode.Manual);
            dof.nearFocusStart.Override(0f);
            dof.nearFocusEnd.Override(12f);
            dof.farFocusStart.Override(58f);
            dof.farFocusEnd.Override(180f);

            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssets();

            var go = new GameObject("Film look (volume)");
            var vol = go.AddComponent<Volume>();
            vol.isGlobal = true;
            vol.sharedProfile = p;
            return vol;
        }

        static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(true);
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        // ---------- lights ----------

        public static Light Moon()
        {
            var go = new GameObject("Moon");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            var hdMoon = go.AddComponent<HDAdditionalLightData>();
            hdMoon.normalBias = 1.6f;
            hdMoon.slopeBias = 1.0f;
            l.lightUnit = LightUnit.Lux;
            l.intensity = 55f;
            l.useColorTemperature = true;
            l.colorTemperature = 8500f;
            l.color = Color.white;
            l.shadows = LightShadows.None;   // the day and night cycle turns them on when the sun is down
            l.enabled = false;
            go.transform.rotation = Quaternion.Euler(-40f, 140f, 0f);
            return l;
        }

        public static Light Sun()
        {
            var go = new GameObject("Sun");
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            var hdSun = go.AddComponent<HDAdditionalLightData>();
            hdSun.normalBias = 1.6f;   // stops the sawtooth shadow acne on doors and thin panels
            hdSun.slopeBias = 1.0f;
            l.lightUnit = LightUnit.Lux;
            l.intensity = 100000f;
            l.useColorTemperature = true;
            l.colorTemperature = 6000f;
            l.color = Color.white;
            l.shadows = LightShadows.Soft;
            go.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            RenderSettings.sun = l;
            return l;
        }

        public static void RoomLightAndProbe(Transform parent, string name, Vector3 floorCentre, Vector2 size, float ceiling, float kelvin, bool light, float boost = 1f)
        {
            if (light)
            {
                // several soft ceiling panels spread over the room, so the light is even and has no bright centre
                int nx = Mathf.Max(1, Mathf.RoundToInt(size.x / 3.5f)), nz = Mathf.Max(1, Mathf.RoundToInt(size.y / 3.5f));
                int n = nx * nz;
                var panel = new Vector2(Mathf.Clamp(size.x / nx * 0.5f, 0.6f, 2f), Mathf.Clamp(size.y / nz * 0.5f, 0.6f, 2f));
                for (int iz = 0; iz < nz; iz++)
                    for (int ix = 0; ix < nx; ix++)
                    {
                        var lg = new GameObject($"{name} light {ix + iz * nx + 1}");
                        lg.transform.SetParent(parent, false);
                        float ux = (ix + 0.5f) / nx - 0.5f, uz = (iz + 0.5f) / nz - 0.5f;
                        lg.transform.position = floorCentre + new Vector3(ux * size.x, ceiling - 0.06f, uz * size.y);
                        lg.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
                        var l = lg.AddComponent<Light>();
                        l.type = LightType.Rectangle;
                        l.areaSize = panel;
                        lg.AddComponent<HDAdditionalLightData>();
                        l.lightUnit = LightUnit.Lumen;
                        l.intensity = 500f / n;
                        l.useColorTemperature = true;
                        l.colorTemperature = kelvin;
                        l.color = Color.white;
                        l.range = Mathf.Max(size.x / nx, size.y / nz) * 1.5f;
                        l.shadows = LightShadows.None;
                        var sw = lg.AddComponent<SwitchableLight>();   // dimmer by day, cool and even at night
                        sw.day = 420f * boost / n;
                        sw.night = 1300f * boost / n;
                    }
            }

            var pg = new GameObject($"{name} reflections");
            pg.transform.SetParent(parent, false);
            pg.transform.position = floorCentre + Vector3.up * (ceiling * 0.5f);
            var probe = pg.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Baked;
            var hd = pg.AddComponent<HDAdditionalReflectionData>();
            hd.mode = ProbeSettings.Mode.Baked;
            hd.influenceVolume.shape = InfluenceShape.Box;
            hd.influenceVolume.boxSize = new Vector3(size.x, ceiling, size.y);
            hd.influenceVolume.boxBlendDistancePositive = Vector3.one * 0.4f;
            hd.influenceVolume.boxBlendDistanceNegative = Vector3.one * 0.4f;
        }

        // ---------- camera ----------

        public static void Camera(GameObject camGo, Volume volume)
        {
            var cam = camGo.GetComponent<Camera>();
            cam.usePhysicalProperties = true;
            cam.sensorSize = new Vector2(36f, 24f);
            cam.gateFit = UnityEngine.Camera.GateFitMode.Horizontal;
            cam.focalLength = 28f;
            var hd = camGo.AddComponent<HDAdditionalCameraData>();
            hd.antialiasing = HDAdditionalCameraData.AntialiasingMode.TemporalAntialiasing;
            hd.TAAQuality = HDAdditionalCameraData.TAAQualityLevel.High;
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Sky;
            hd.physicalParameters.aperture = 4.5f;
            hd.physicalParameters.bladeCount = 7;
            camGo.AddComponent<CinematicFocus>().volume = volume;
        }

        // ---------- baking ----------

        /// <summary>Bakes every reflection probe. No lightmaps, so it is quick.</summary>
        public static void Bake()
        {
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(LightingPath);
            if (!ls)
            {
                ls = new LightingSettings { name = "Lighting" };
                AssetDatabase.CreateAsset(ls, LightingPath);
            }
            ls.bakedGI = false;
            ls.realtimeGI = false;
            Lightmapping.lightingSettings = ls;
            Lightmapping.Bake();
        }
    }
}
