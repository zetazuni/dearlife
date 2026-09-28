using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A working TV: the screen part of the model (the material called "Screen") shows a moving picture and lights the room
    /// while it is on. People switch it on when they sit down in front of it and off when they leave, or you switch it from
    /// its pie menu. The picture is made up (colour scenes with a cinema border), never real footage.
    /// </summary>
    public class TvScreen : MonoBehaviour
    {
        public static readonly List<TvScreen> All = new List<TvScreen>();

        public bool on;
        public bool autoSwitched;      // a person turned it on by sitting down, so a person turns it off again

        const int W = 64, H = 36;
        Material mat;
        Texture2D tex;
        Color32[] px;
        Light glow;
        float nextFrame;
        static readonly int EmissiveMap = Shader.PropertyToID("_EmissiveColorMap");
        static readonly int EmissiveCol = Shader.PropertyToID("_EmissiveColor");
        static readonly int BaseCol = Shader.PropertyToID("_BaseColor");

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        void Awake()
        {
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                var shared = r.sharedMaterials;
                for (int i = 0; i < shared.Length; i++)
                {
                    if (!shared[i] || !shared[i].name.StartsWith("Screen")) continue;
                    var inst = r.materials;          // instances, so other screens in the house keep their look
                    mat = inst[i];
                    goto found;
                }
            }
            found:
            if (mat != null)
            {
                tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                px = new Color32[W * H];
                mat.SetColor(BaseCol, new Color(0.01f, 0.012f, 0.016f));
                mat.SetTexture(EmissiveMap, tex);
                mat.SetFloat("_UseEmissiveIntensity", 0f);
                mat.EnableKeyword("_EMISSIVE_COLOR_MAP");
            }
            glow = GetComponentInChildren<Light>(true);
            Apply();
        }

        public void SetOn(bool value, bool byPerson = false)
        {
            if (on == value) return;
            on = value;
            autoSwitched = value && byPerson;
            nextFrame = 0f;
            Apply();
        }

        public void Toggle() { SetOn(!on); autoSwitched = false; }

        /// <summary>The place a person stands to use the TV.</summary>
        public Vector3 StandPoint => transform.position + transform.forward * 1.5f;

        void Apply()
        {
            if (mat != null && !on) mat.SetColor(EmissiveCol, Color.black);
            if (glow) glow.enabled = on;
        }

        void Update()
        {
            if (!on || mat == null || Time.time < nextFrame) return;
            nextFrame = Time.time + 0.1f;
            Draw(Time.time, out var average);
            tex.SetPixels32(px);
            tex.Apply(false);
            float night = DayNightCycle.Instance ? DayNightCycle.Instance.Night01 : 0f;
            mat.SetColor(EmissiveCol, Color.white * Mathf.Lerp(320f, 26f, night));   // bright by day so it still shows, soft at night
            if (glow) glow.color = Color.Lerp(glow.color, average, 0.35f);
        }

        // made up channels, a new one every ten seconds
        void Draw(float t, out Color average)
        {
            int scene = Mathf.FloorToInt(t / 10f) % 3;
            float r = 0f, g = 0f, b = 0f;
            for (int y = 0; y < H; y++)
            {
                float v = y / (float)(H - 1);
                for (int x = 0; x < W; x++)
                {
                    float u = x / (float)(W - 1);
                    Color c;
                    if (v < 0.09f || v > 0.91f) c = Color.black;                   // cinema border
                    else if (scene == 0)                                             // a slow sunset by the sea
                    {
                        float horizon = 0.62f + 0.02f * Mathf.Sin(t * 0.6f + u * 5f);
                        if (v < horizon) c = Color.Lerp(new Color(1f, 0.55f, 0.25f), new Color(0.25f, 0.2f, 0.5f), Mathf.InverseLerp(horizon, 0.09f, v));
                        else c = Color.Lerp(new Color(0.9f, 0.5f, 0.3f), new Color(0.05f, 0.15f, 0.3f), Mathf.InverseLerp(horizon, 0.91f, v)) * (0.8f + 0.2f * Mathf.Sin(u * 40f + t * 2f));
                        float sun = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u * 1.78f, v), new Vector2(0.9f + 0.05f * Mathf.Sin(t * 0.2f), horizon - 0.12f)) * 7f);
                        c += new Color(1f, 0.9f, 0.6f) * sun;
                    }
                    else if (scene == 1)                                             // colour waves
                    {
                        float h = Mathf.Repeat(0.55f + 0.25f * Mathf.Sin(t * 0.4f + u * 3f) + v * 0.3f, 1f);
                        float lum = 0.55f + 0.45f * Mathf.Sin(u * 9f + v * 6f - t * 1.5f);
                        c = Color.HSVToRGB(h, 0.75f, lum);
                    }
                    else                                                             // a game show with moving bars
                    {
                        float band = Mathf.Repeat(u * 6f - t * 0.7f, 1f);
                        c = Color.HSVToRGB(Mathf.Repeat(Mathf.Floor(u * 6f - t * 0.7f) * 0.17f, 1f), 0.65f, 0.55f + 0.4f * (band < 0.5f ? 1f : 0.3f));
                        if (v > 0.72f) c = Color.Lerp(c, new Color(0.9f, 0.9f, 0.95f), 0.85f * (Mathf.Repeat(u * 12f - t * 3f, 1f) < 0.6f ? 1f : 0.25f));
                    }
                    px[y * W + x] = c;
                    r += c.r; g += c.g; b += c.b;
                }
            }
            float n = W * H;
            average = new Color(r / n, g / n, b / n);
            average = Color.Lerp(Color.white * 0.4f, average, 0.8f);
        }

        // ---------------------------------------------------------------- people watching

        /// <summary>The TV a seat looks at (in front of it, within 6 m), or null.</summary>
        public static TvScreen Facing(UseSpot spot)
        {
            if (spot == null || spot.pose != CharacterRig.Pose.Sit) return null;
            var dir = Quaternion.Euler(0f, (spot.transform.parent ? spot.transform.parent.eulerAngles.y : 0f) + spot.yaw, 0f) * Vector3.forward;
            TvScreen best = null; float bestD = 6f;
            foreach (var tv in All)
            {
                var to = tv.transform.position - spot.transform.position; to.y = 0f;
                float d = to.magnitude;
                if (d > bestD || d < 0.5f) continue;
                if (Vector3.Dot(dir, to / d) < 0.55f) continue;                        // the seat must face the TV
                if (Vector3.Dot(tv.transform.forward, -to / d) < 0.4f) continue;       // and the TV must face the seat
                best = tv; bestD = d;
            }
            return best;
        }

        public static bool AnyoneWatching(TvScreen tv, Character except)
        {
            foreach (var s in UseSpot.All)
                if (s.occupant != null && s.occupant != except && Facing(s) == tv) return true;
            return false;
        }
    }
}
