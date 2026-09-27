using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The usual PC graphics options: window mode (windowed, borderless windowed, fullscreen), resolution, vertical sync and a frame limit.
    /// They are remembered on this PC and applied when the game opens (not in the editor, so the Game view is left alone).
    /// </summary>
    public static class DisplaySettings
    {
        public static readonly string[] ModeNames = { "Windowed", "Borderless windowed", "Fullscreen" };
        public static readonly int[] FrameLimits = { 30, 60, 120, 144, -1 };
        public static readonly string[] FrameNames = { "30", "60", "120", "144", "Unlimited" };

        const string KMode = "tiramisu.dispMode", KW = "tiramisu.resW", KH = "tiramisu.resH", KV = "tiramisu.vsync", KF = "tiramisu.fps";

        public static int Mode { get; private set; }         // index into ModeNames
        public static int Width { get; private set; }
        public static int Height { get; private set; }
        public static bool VSync { get; private set; }
        public static int Frame { get; private set; }         // index into FrameLimits

        static List<Vector2Int> sizes;

        /// <summary>Every resolution the screen offers, biggest first, one entry for each size.</summary>
        public static List<Vector2Int> Sizes
        {
            get
            {
                if (sizes != null) return sizes;
                sizes = new List<Vector2Int>();
                foreach (var r in Screen.resolutions)
                {
                    var v = new Vector2Int(r.width, r.height);
                    if (r.height < 600 || sizes.Contains(v)) continue;
                    sizes.Add(v);
                }
                var cur = new Vector2Int(Width, Height);
                if (Width > 0 && !sizes.Contains(cur)) sizes.Add(cur);
                sizes.Sort((a, b) => b.x != a.x ? b.x.CompareTo(a.x) : b.y.CompareTo(a.y));
                return sizes;
            }
        }

        public static string SizeName(Vector2Int s) => s.x + " x " + s.y;

        static bool loaded;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            var native = Screen.currentResolution;
            Mode = Mathf.Clamp(PlayerPrefs.GetInt(KMode, 1), 0, 2);          // borderless is the default
            Width = PlayerPrefs.GetInt(KW, Mathf.Min(native.width, 1920));
            Height = PlayerPrefs.GetInt(KH, Mathf.Min(native.height, 1080));
            VSync = PlayerPrefs.GetInt(KV, 1) == 1;
            Frame = Mathf.Clamp(PlayerPrefs.GetInt(KF, 4), 0, FrameLimits.Length - 1);
        }

        /// <summary>Read the saved choices so the settings window can show them (nothing is changed).</summary>
        public static void Init() { Load(); }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void OnStart()
        {
#if !UNITY_EDITOR
            Load();
            Apply();
#endif
        }

        public static void SetMode(int m) { Load(); Mode = m; Save(); Apply(); }
        public static void SetSize(Vector2Int s) { Load(); Width = s.x; Height = s.y; Save(); Apply(); }
        public static void SetVSync(bool on) { Load(); VSync = on; Save(); Apply(); }
        public static void SetFrame(int i) { Load(); Frame = i; Save(); Apply(); }

        static void Save()
        {
            PlayerPrefs.SetInt(KMode, Mode); PlayerPrefs.SetInt(KW, Width); PlayerPrefs.SetInt(KH, Height);
            PlayerPrefs.SetInt(KV, VSync ? 1 : 0); PlayerPrefs.SetInt(KF, Frame);
            PlayerPrefs.Save();
        }

        public static void Apply()
        {
            Load();
            QualitySettings.vSyncCount = VSync ? 1 : 0;
            Application.targetFrameRate = VSync ? -1 : FrameLimits[Frame];
#if UNITY_EDITOR
            return;      // the Game view keeps its own size
#else
            var native = Screen.currentResolution;
            if (Mode == 1) Screen.SetResolution(native.width, native.height, FullScreenMode.FullScreenWindow);
            else if (Mode == 2) Screen.SetResolution(Width, Height, FullScreenMode.ExclusiveFullScreen);
            else Screen.SetResolution(Mathf.Min(Width, native.width), Mathf.Min(Height, native.height - 80), FullScreenMode.Windowed);
#endif
        }
    }
}
