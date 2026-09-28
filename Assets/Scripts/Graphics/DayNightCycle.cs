using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Dearlife
{
    /// <summary>
    /// Time of day: the sun crosses the sky, the moon and stars take over at night, the exposure opens up,
    /// room lights, pendants, lamps, the garden path and the pool lights fade on, and the baked reflection
    /// probes dim so the glass does not mirror a daylit house at midnight.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public static DayNightCycle Instance { get; private set; }

        public Light sun, moon;
        public Volume volume;
        [Range(0f, 24f)] public float hour = 15f;
        public bool auto;
        public float secondsPerHour = 20f;

        static string PrefKey => SaveSystem.Key("dearlife.hour");
        Exposure exposure;
        Bloom bloom;
        PhysicallyBasedSky sky;
        CloudLayer clouds;
        HDAdditionalLightData moonData;
        HDAdditionalReflectionData[] probes;
        float nightAmount;
        Fog fog;
        float baseFog = -1f;
        public int DayCount { get; private set; }

        public float Night01 => nightAmount;
        public string Clock => $"{Mathf.FloorToInt(hour) % 24:00}:{Mathf.FloorToInt((hour % 1f) * 60f):00}";
        public string Phase => hour < 5f || hour >= 21f ? "Night" : hour < 7.5f ? "Dawn" : hour < 17f ? "Day" : hour < 19.5f ? "Sunset" : "Evening";

        void Awake()
        {
            Instance = this;
            if (PlayerPrefs.HasKey(PrefKey)) hour = PlayerPrefs.GetFloat(PrefKey);
        }

        void Start()
        {
            if (volume && volume.profile)
            {
                if (volume.profile.TryGet(out exposure))
                {
                    // quick eyes: a jump from night to day should not take half a minute
                    exposure.adaptationSpeedDarkToLight.Override(9f);
                    exposure.adaptationSpeedLightToDark.Override(6f);
                }
                if (volume.profile.TryGet(out sky)) sky.spaceEmissionTexture.Override(MakeStars());
                if (sky != null) { sky.spaceEmissionMultiplier.Override(600f); }
                volume.profile.TryGet(out clouds);
            }
            if (moon)
            {
                // a visible moon disk in the sky (HDRP celestial body), bigger than the real one so it reads
                moonData = moon.GetComponent<HDAdditionalLightData>();
                if (moonData)
                {
                    moonData.interactsWithSky = true;
                    moonData.angularDiameter = 2.5f;
                    moonData.surfaceTexture = MakeMoon();
                    moonData.surfaceTint = new Color(0.012f, 0.012f, 0.011f);   // the disk is very bright at night exposure
                    moonData.distance = 384400000f;
                    moonData.earthshine = 0.4f;
                }
            }
            if (volume && volume.profile) volume.profile.TryGet(out bloom);
            probes = FindObjectsByType<HDAdditionalReflectionData>(FindObjectsInactive.Include);
            Apply();
        }

        public void SetHour(float h)
        {
            hour = Mathf.Repeat(h, 24f);
            Apply();
            PlayerPrefs.SetFloat(PrefKey, hour);
        }

        void Update()
        {
            if (auto)
            {
                float before = hour;
                hour = Mathf.Repeat(hour + Time.deltaTime / Mathf.Max(secondsPerHour, 1f), 24f);
                if (hour < before) DayCount++;
                Apply();
            }
            else if (SeasonCycle.Instance) Apply();     // the sun follows the season while the clock is stopped
        }

        void OnApplicationQuit() => PlayerPrefs.SetFloat(PrefKey, hour);

        // ---------- the sky ----------

        /// <summary>Sun elevation in degrees for an hour of the day. 6:00 sunrise, 18:00 sunset.</summary>
        static float Elevation(float h)
        {
            float rise = SeasonCycle.Sunrise, len = Mathf.Max(6f, SeasonCycle.Sunset - SeasonCycle.Sunrise);
            float t = Mathf.Repeat(h - rise, 24f);              // hours since sunrise
            if (t <= len) return SeasonCycle.SunPeak * Mathf.Sin(Mathf.PI * t / len);
            return -38f * Mathf.Sin(Mathf.PI * (t - len) / (24f - len));
        }

        static Vector3 Direction(float azimuthDeg, float elevationDeg)
        {
            float az = azimuthDeg * Mathf.Deg2Rad, el = elevationDeg * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(az) * Mathf.Cos(el), Mathf.Sin(el), Mathf.Cos(az) * Mathf.Cos(el));
        }

        void Apply()
        {
            float e = Elevation(hour);
            float dayLen = Mathf.Max(6f, SeasonCycle.Sunset - SeasonCycle.Sunrise);
            float az = 90f + 180f * Mathf.Repeat(hour - SeasonCycle.Sunrise, 24f) / dayLen;   // east at sunrise, west at sunset
            var toSun = Direction(az, e);
            float day = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-5f, 12f, e));
            nightAmount = 1f - day;

            float sunK = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-1.5f, 14f, e));
            if (sun)
            {
                sun.transform.rotation = Quaternion.LookRotation(-toSun, Vector3.up);
                float k = sunK;
                sun.intensity = 100000f * k * SeasonCycle.SunPower;
                sun.colorTemperature = Mathf.Lerp(2300f, 6000f, Mathf.InverseLerp(0f, 30f, e)) - SeasonCycle.Warm * Mathf.Clamp01(e / 30f + 0.3f);
                sun.enabled = k > 0.001f;
            }
            if (moon)
            {
                // the moon rides opposite the sun
                float me = -e;
                moon.transform.rotation = Quaternion.LookRotation(toSun, Vector3.up);
                float k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-3f, 10f, me));
                moon.intensity = 55f * k;
                moon.enabled = k > 0.001f;
                // only one directional light can cast shadows at a time, the sun wins while it is up
                moon.shadows = sunK > 0.02f ? LightShadows.None : LightShadows.Soft;
            }

            if (exposure != null)
            {
                exposure.limitMin.Override(Mathf.Lerp(3.4f, 9f, day));
                exposure.limitMax.Override(Mathf.Lerp(9f, 13.8f, day));
                exposure.compensation.Override(Mathf.Lerp(-0.2f, 0f, day));
            }
            if (sky != null) sky.multiplier.Override(Mathf.Lerp(0.01f, 1f, day));      // the atmosphere goes dark, only stars and the moon are left
            if (clouds != null) clouds.opacity.Override(Mathf.Clamp01(Mathf.Lerp(0.1f, 0.85f, day) * SeasonCycle.CloudScale));
            if (fog == null && volume && volume.profile) volume.profile.TryGet(out fog);
            if (fog != null)
            {
                if (baseFog < 0f) baseFog = fog.meanFreePath.value;
                fog.meanFreePath.Override(baseFog / SeasonCycle.HazeScale);
            }
            if (bloom != null) bloom.intensity.Override(Mathf.Lerp(0.02f, 0.18f, day));   // less glow at night
            if (probes != null)
                {
                    // the probes were baked in full daylight (thousands of nits), so at night they must almost vanish,
                    // otherwise every rough surface reflects a sunny sky and glows
                    float m = Mathf.Lerp(0.0002f, 1f, day * day);
                    foreach (var p in probes) if (p) p.multiplier = m;
                }
            foreach (var l in SwitchableLight.All) l.Apply(nightAmount);
            foreach (var g in NightGlow.All) g.Apply(nightAmount);
        }

        // ---------- stars and moon ----------

        static Texture2D MakeMoon()
        {
            const int n = 256;
            var t = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float mare = Mathf.PerlinNoise(x * 0.03f + 7f, y * 0.03f) * 0.6f + Mathf.PerlinNoise(x * 0.09f, y * 0.09f + 3f) * 0.3f;
                    float crater = Mathf.PerlinNoise(x * 0.4f, y * 0.4f) * 0.12f;
                    float g = Mathf.Clamp01(0.95f - Mathf.Max(0f, mare - 0.35f) * 0.9f - crater);
                    px[y * n + x] = new Color(g, g * 0.98f, g * 0.93f, 1f);
                }
            t.SetPixels(px);
            t.Apply(true, true);
            return t;
        }


        static Cubemap MakeStars()
        {
            const int size = 1024;
            var cm = new Cubemap(size, TextureFormat.RGBA32, false);
            var rnd = new System.Random(42);
            var pix = new Color[size * size];
            for (int face = 0; face < 6; face++)
            {
                for (int i = 0; i < pix.Length; i++) pix[i] = Color.black;
                int stars = 2200;
                for (int s = 0; s < stars; s++)
                {
                    int x = rnd.Next(2, size - 2), y = rnd.Next(2, size - 2);
                    float b = Mathf.Pow((float)rnd.NextDouble(), 4f) * 0.9f + 0.12f;
                    var tint = Color.Lerp(new Color(1f, 0.85f, 0.7f), new Color(0.75f, 0.85f, 1f), (float)rnd.NextDouble());
                    var c = tint * b;
                    c.a = 1f;
                    pix[y * size + x] = c;
                    if (b > 0.8f)
                    {
                        var d = c * 0.45f; d.a = 1f;
                        pix[y * size + x + 1] = d; pix[y * size + x - 1] = d; pix[(y + 1) * size + x] = d; pix[(y - 1) * size + x] = d;
                    }
                }
                cm.SetPixels(pix, (CubemapFace)face);
            }
            cm.Apply(false, true);
            return cm;
        }
    }
}
