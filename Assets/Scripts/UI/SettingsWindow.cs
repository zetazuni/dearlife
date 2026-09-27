using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The settings window, opened from the top bar like the 2D game's menu: tabs for Sound, Time, Graphics and Game (dark mode, hints).
    /// </summary>
    public class SettingsWindow : MonoBehaviour
    {
        public static bool Open { get; private set; }
        public static Rect Box;
        static int tab;
        float scroll;
        static int openDrop = -1;      // which dropdown is unfolded on the Graphics tab
        bool confirmExit;

        public static void Toggle() { Open = !Open; GameAudio.Play(GameAudio.Sfx.Click); }
        public static void ForceClose() { Open = false; }

        void Update() { if (Open && Input.GetKeyDown(KeyCode.Escape)) Open = false; }

        void OnGUI()
        {
            if (!Open || Splash.Showing) { Box = Rect.zero; return; }
            Ui.Begin();
            float w = Ui.W, h = Ui.H;
            var scrim = new Rect(0f, 0f, w, h);
            Ui.Rect2(scrim, Ui.Dark ? new Color(0.02f, 0.015f, 0.06f, 0.72f) : new Color(0.19f, 0.11f, 0.08f, 0.38f));
            Box = new Rect(w * 0.5f - 270f, Mathf.Max(20f, h * 0.5f - 300f), 540f, Mathf.Min(600f, h - 40f));
            Ui.Box(Box, Ui.Card, Ui.Pink, 22f, 3f, true);
            Ui.Label(new Rect(Box.x + 24f, Box.y + 14f, 300f, 34f), "Settings", 26f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            if (Ui.Square(new Rect(Box.xMax - 54f, Box.y + 14f, 34f, 34f), "X")) Open = false;

            string[] names = { "Sound", "Time", "Graphics", "Game" };
            float tw = (Box.width - 48f - 3f * 6f) / 4f;
            for (int i = 0; i < 4; i++)
                if (Ui.Chip(new Rect(Box.x + 24f + i * (tw + 6f), Box.y + 58f, tw, 30f), names[i], tab == i, 13f)) { tab = i; scroll = 0f; openDrop = -1; if (i == 2) DisplaySettings.Init(); }

            var inner = new Rect(Box.x + 24f, Box.y + 102f, Box.width - 48f, Box.height - 102f - 22f);
            scroll = Ui.Scroll(inner, scroll, tab == 2 ? 520f + (openDrop >= 0 ? 190f : 0f) : tab == 3 ? 480f : 420f, DrawTab);

            // a click on the dark outside closes it
            if (Event.current.type == EventType.MouseDown && !Box.Contains(Ui.Mouse) && !confirmExit) { Open = false; Event.current.Use(); }

            if (confirmExit) DrawExitConfirm(w, h);
        }

        void DrawExitConfirm(float w, float h)
        {
            Ui.Rect2(new Rect(0f, 0f, w, h), new Color(0.02f, 0.015f, 0.06f, 0.6f));
            var box = new Rect(w * 0.5f - 200f, h * 0.5f - 80f, 400f, 160f);
            Ui.Box(box, Ui.Card, Ui.Pink, 22f, 3f, true);
            Ui.Label(new Rect(box.x + 24f, box.y + 18f, box.width - 48f, 28f), "Quit Tiramisu?", 19f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(box.x + 24f, box.y + 50f, box.width - 48f, 40f), "Everything is saved already, so it's safe to close any time.", 13f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
            float bw = (box.width - 48f - 12f) / 2f;
            if (Ui.Pill(new Rect(box.x + 24f, box.yMax - 52f, bw, 38f), "Cancel", false, 14f)) confirmExit = false;
            if (Ui.Pill(new Rect(box.x + 24f + bw + 12f, box.yMax - 52f, bw, 38f), "Quit", true, 14f))
            {
                SaveSystem.SaveNow();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }

        void DrawTab(float w)
        {
            float y = 0f;
            switch (tab)
            {
                case 0:
                    string[] names = { "Everything", "Music", "Sounds", "Pets" };
                    float[] vals = { GameAudio.VolMaster, GameAudio.VolMusic, GameAudio.VolSfx, GameAudio.VolPet };
                    for (int i = 0; i < 4; i++)
                    {
                        Ui.Label(new Rect(0f, y, 110f, 28f), names[i], 14f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                        float nv = Ui.Slider(new Rect(110f, y, w - 110f - 50f, 28f), vals[i]);
                        if (!Mathf.Approximately(nv, vals[i])) GameAudio.SetVolume(i, nv);
                        Ui.Label(new Rect(w - 46f, y, 46f, 28f), Mathf.RoundToInt(vals[i] * 100f) + "%", 13f, Ui.Soft, TextAnchor.MiddleRight, Ui.Weight.ExtraBold);
                        y += 40f;
                    }
                    y += 6f;
                    float hw = (w - 8f) / 2f;
                    if (Ui.Pill(new Rect(0f, y, hw, 36f), GameAudio.Muted ? "Sound is off (F2)" : "Sound is on (F2)", GameAudio.Muted, 14f)) GameAudio.ToggleMute();
                    if (Ui.Pill(new Rect(hw + 8f, y, hw, 36f), GameAudio.MusicOn ? "Music is on (N)" : "Music is off (N)", !GameAudio.MusicOn, 14f)) GameAudio.ToggleMusic();
                    break;
                case 1:
                    var day = DayNightCycle.Instance;
                    if (day)
                    {
                        Ui.Label(new Rect(0f, y, w, 24f), $"{day.Phase}  ·  {day.Clock}", 16f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold); y += 28f;
                        float nh = Ui.Slider(new Rect(0f, y, w, 28f), day.hour, 0f, 24f);
                        if (Mathf.Abs(nh - day.hour) > 0.001f) day.SetHour(nh);
                        y += 40f;
                        float qw = (w - 18f) / 4f;
                        string[] pn = { "Morning", "Noon", "Sunset", "Night" }; float[] ph = { 8f, 13f, 18.5f, 22.5f };
                        for (int i = 0; i < 4; i++) if (Ui.Chip(new Rect(i * (qw + 6f), y, qw, 30f), pn[i], false, 13f)) day.SetHour(ph[i]);
                        y += 44f;
                        if (Ui.Pill(new Rect(0f, y, w, 36f), day.auto ? "Time is running" : "Let time run", day.auto, 14f)) day.auto = !day.auto;
                        y += 52f;
                    }
                    var sea = SeasonCycle.Instance;
                    if (sea)
                    {
                        HouseHud.Kicker(0f, y, w, "Season"); y += 22f;
                        float qw = (w - 18f) / 4f;
                        string[] sn = { "Spring", "Summer", "Autumn", "Winter" };
                        for (int i = 0; i < 4; i++) if (Ui.Chip(new Rect(i * (qw + 6f), y, qw, 30f), sn[i], (int)sea.season == i, 13f)) sea.Choose((Season)i);
                    }
                    break;
                case 2:
                    DrawGraphics(w);
                    break;
                default:
                    Ui.Label(new Rect(0f, y, w, 22f), "Look", 14f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold); y += 28f;
                    float dw = (w - 8f) / 2f;
                    if (Ui.Pill(new Rect(0f, y, dw, 36f), "Light mode", !Ui.Dark, 14f)) Ui.SetDark(false);
                    if (Ui.Pill(new Rect(dw + 8f, y, dw, 36f), "Dark mode", Ui.Dark, 14f)) Ui.SetDark(true);
                    y += 54f;
                    if (Ui.Pill(new Rect(0f, y, w, 36f), "Show the hints again (F1)", false, 14f)) { Tutorial.Restart(); Open = false; }
                    y += 52f;
                    var dec = DecorateMode.Instance;
                    if (dec && Ui.Pill(new Rect(0f, y, w, 36f), "Put all the furniture back where it started", false, 14f)) dec.ResetLayout();
                    y += 60f;

                    HouseHud.Kicker(0f, y, w, "Save"); y += 22f;
                    Ui.Label(new Rect(0f, y, w, 18f), SaveSystem.LastSavedText, 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold); y += 24f;
                    if (Ui.Pill(new Rect(0f, y, w, 36f), "Save now", false, 14f)) { SaveSystem.SaveNow(); Household.Toast("Game saved."); }
                    y += 60f;

                    HouseHud.Kicker(0f, y, w, "Game"); y += 22f;
                    if (Ui.Pill(new Rect(0f, y, w, 36f), "Back to title screen", false, 14f)) MainMenu.Open();
                    y += 44f;
                    if (Ui.Pill(new Rect(0f, y, w, 36f), "Exit game", false, 14f)) confirmExit = true;
                    y += 60f;

                    Ui.Label(new Rect(0f, y, w, 60f), $"Tiramisu 3D v{GameInfo.Version}  ·  {GameInfo.BuildDate}\nMade by Amir (Zetazuni) for Athirah.", 13f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
                    break;
            }
        }

        /// <summary>A drop-down: a wide button that unfolds a list under it. Returns the index chosen (or the current one).</summary>
        int Drop(ref float y, float w, int id, string label, string[] names, int cur, bool enabled = true)
        {
            Ui.Label(new Rect(0f, y, w, 20f), label, 12f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold); y += 22f;
            var head = new Rect(0f, y, w, 34f);
            bool open = openDrop == id && enabled;
            if (Ui.Pill(head, names[Mathf.Clamp(cur, 0, names.Length - 1)] + (open ? "   ▲" : "   ▼"), open, 14f) && enabled) openDrop = open ? -1 : id;
            y += 40f;
            int result = cur;
            if (open)
            {
                for (int i = 0; i < names.Length; i++)
                {
                    if (Ui.Chip(new Rect(12f, y, w - 24f, 28f), names[i], i == cur, 13f)) { result = i; openDrop = -1; GameAudio.Play(GameAudio.Sfx.Click); }
                    y += 32f;
                }
                y += 4f;
            }
            return result;
        }

        void DrawGraphics(float w)
        {
            DisplaySettings.Init();
            float y = 0f;
            int m = Drop(ref y, w, 0, "Window mode", DisplaySettings.ModeNames, DisplaySettings.Mode);
            if (m != DisplaySettings.Mode) DisplaySettings.SetMode(m);

            var sizes = DisplaySettings.Sizes;
            var names = new string[sizes.Count];
            int cur = 0;
            for (int i = 0; i < sizes.Count; i++) { names[i] = DisplaySettings.SizeName(sizes[i]); if (sizes[i].x == DisplaySettings.Width && sizes[i].y == DisplaySettings.Height) cur = i; }
            bool free = DisplaySettings.Mode != 1;         // borderless always uses the whole screen
            if (names.Length > 0)
            {
                int r = Drop(ref y, w, 1, free ? "Resolution" : "Resolution (borderless uses the whole screen)", names, cur, free);
                if (free && r != cur) DisplaySettings.SetSize(sizes[r]);
            }

            y += 4f;
            float hw = (w - 8f) / 2f;
            if (Ui.Pill(new Rect(0f, y, hw, 36f), DisplaySettings.VSync ? "V-Sync is on" : "V-Sync is off", DisplaySettings.VSync, 14f)) DisplaySettings.SetVSync(!DisplaySettings.VSync);
            y += 48f;
            int f = Drop(ref y, w, 2, DisplaySettings.VSync ? "Frame limit (V-Sync sets it)" : "Frame limit", DisplaySettings.FrameNames, DisplaySettings.Frame, !DisplaySettings.VSync);
            if (f != DisplaySettings.Frame) DisplaySettings.SetFrame(f);

            y += 6f;
            var gfx = GraphicsModes.Instance;
            if (gfx)
            {
                Ui.Label(new Rect(0f, y, w, 22f), "Graphics quality: " + gfx.Label, 14f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold); y += 30f;
                float qw = (w - 12f) / 3f;
                string[] gn = { "Ultra", "Quality", "Performance" };
                for (int i = 0; i < 3; i++) if (Ui.Chip(new Rect(i * (qw + 6f), y, qw, 32f), gn[i], (int)gfx.mode == i, 13f)) gfx.Apply((GraphicsModes.Mode)i);
                y += 42f;
                Ui.Label(new Rect(0f, y, w, 60f), "Ultra uses ray traced reflections. Performance turns off the heaviest effects. All three use DLSS when the graphics card has it.", 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
            }
        }
    }
}
