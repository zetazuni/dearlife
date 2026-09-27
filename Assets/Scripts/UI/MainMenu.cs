using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Tiramisu
{
    /// <summary>
    /// The title screen: independent of whatever save is currently loaded, shown once at boot (after the splash and
    /// loading screen) and again from Settings' "Back to title screen". A panel on the left with Start, Load Game and
    /// Settings fades into the actual house on the right, seen through a slow cinematic camera that drifts from room
    /// to room on its own (built from <see cref="RoomMarker"/>, so it needs no hand tuned coordinates). Start and Load
    /// Game open a picker for one of ten save slots; choosing one reloads the scene into that slot, fresh.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public static bool Active { get; private set; } = true;
        public static MainMenu Instance { get; private set; }
        /// <summary>Set just before a slot switch reloads the scene, so the fresh load drops straight into play instead of showing the menu again.</summary>
        static bool pendingEnter;

        enum Page { Title, NewSlots, LoadSlots, Settings }
        Page page = Page.Title;
        int confirmSlot = -1;      // a used slot the player tapped on the New Game page, waiting to be confirmed
        bool confirmExit;

        // ---- the cinematic background

        struct Shot { public Vector3 pivot; public float yaw, pitch, dist; }
        readonly List<Shot> shots = new List<Shot>();
        int shotIndex;
        float shotT;
        float blendT = 1f;
        Shot from, to;
        const float HoldTime = 7.5f, BlendTime = 2.2f, DriftYaw = 2.6f, ZoomIn = 0.86f;

        // ---- a handful of soft floating sparkles on the panel, just for a bit of life
        struct Spark { public float x, y, phase, speed, size; }
        Spark[] sparks;

        void Awake()
        {
            Instance = this;
            if (pendingEnter) { pendingEnter = false; Active = false; }
            else Active = true;

            var rnd = new System.Random(7);
            sparks = new Spark[16];
            for (int i = 0; i < sparks.Length; i++)
                sparks[i] = new Spark { x = (float)rnd.NextDouble(), y = (float)rnd.NextDouble(), phase = (float)rnd.NextDouble() * 10f, speed = 0.02f + (float)rnd.NextDouble() * 0.03f, size = 3f + (float)rnd.NextDouble() * 5f };
        }

        void Start()
        {
            BuildShots();
            if (Active) EnterCamera();
        }

        void BuildShots()
        {
            shots.Clear();
            var home = LotManager.Home;
            var wideCentre = home ? home.Centre : new Vector3(15.6f, 0f, 9f);
            shots.Add(new Shot { pivot = wideCentre, yaw = 205f, pitch = 24f, dist = 42f });

            int n = 0;
            foreach (var r in RoomMarker.All)
            {
                if (r.floor != 0) continue;
                shots.Add(new Shot { pivot = r.transform.position, yaw = 40f + n * 95f, pitch = 16f + (n % 3) * 4f, dist = r.ViewDistance * 0.9f });
                n++;
                if (n >= 4) break;
            }
            if (shots.Count > 1) { from = shots[0]; to = shots[1 % shots.Count]; } else { from = to = shots[0]; }
            shotIndex = 0; shotT = 0f; blendT = 1f;
        }

        void EnterCamera()
        {
            var cam = OrbitCamera.Instance;
            if (!cam || shots.Count == 0) return;
            cam.ExternalControl = true;
            cam.SetImmediate(shots[0].pivot, shots[0].yaw, shots[0].pitch, shots[0].dist);
        }

        /// <summary>Re-opens the title screen from Settings ("Back to title screen"), without touching any save.</summary>
        public static void Open()
        {
            Active = true;
            if (Instance) { Instance.page = Page.Title; Instance.BuildShots(); Instance.EnterCamera(); }
            SettingsWindow.ForceClose();
        }

        void Update()
        {
            if (!Active) { return; }
            HandleEscape();
            AdvanceCamera();
        }

        void HandleEscape()
        {
            if (!Input.GetKeyDown(KeyCode.Escape)) return;
            if (confirmExit) { confirmExit = false; return; }
            if (page == Page.Settings && SettingsWindow.DropdownOpen) { SettingsWindow.CloseDropdowns(); return; }
            if (page == Page.NewSlots && confirmSlot >= 0) { confirmSlot = -1; return; }
            if (page != Page.Title) { page = Page.Title; confirmSlot = -1; confirmExit = false; SettingsWindow.CloseDropdowns(); }
        }

        void AdvanceCamera()
        {
            var cam = OrbitCamera.Instance;
            if (!cam || shots.Count == 0) return;
            float dt = Time.unscaledDeltaTime;
            if (blendT < 1f)
            {
                blendT = Mathf.Min(1f, blendT + dt / BlendTime);
                float k = Mathf.SmoothStep(0f, 1f, blendT);
                cam.SetImmediate(Vector3.Lerp(from.pivot, to.pivot, k), Mathf.LerpAngle(from.yaw, to.yaw, k), Mathf.Lerp(from.pitch, to.pitch, k), Mathf.Lerp(from.dist, to.dist, k));
                return;
            }
            shotT += dt / HoldTime;
            var cur = to;
            float drift = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(shotT));
            cam.SetImmediate(cur.pivot, cur.yaw + drift * DriftYaw, cur.pitch, cur.dist * Mathf.Lerp(1f, ZoomIn, drift));
            if (shotT >= 1f)
            {
                shotIndex = (shotIndex + 1) % shots.Count;
                from = new Shot { pivot = cur.pivot, yaw = cur.yaw + DriftYaw, pitch = cur.pitch, dist = cur.dist * ZoomIn };
                to = shots[shotIndex];
                shotT = 0f; blendT = 0f;
            }
        }

        // ------------------------------------------------------------ slots

        static bool AnySave() { for (int i = 1; i <= SaveSystem.MaxSlots; i++) if (SaveSystem.SlotHasSave(i)) return true; return false; }

        void EnterSlot(int slot, bool wipe)
        {
            SaveSystem.SetActiveSlot(slot);
            if (wipe) SaveSystem.NewGame(slot);
            pendingEnter = true;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        // ------------------------------------------------------------ the screen

        void OnGUI()
        {
            if (!Active) return;
            Ui.Begin();
            GUI.depth = -500;                 // over the ordinary HUD (which doesn't draw itself while Active anyway), under the loading screen and splash
            float w = Ui.W, h = Ui.H;

            float leftW = w * 0.23f, fadeW = w * 0.07f;
            Ui.Rect2(new Rect(0f, 0f, leftW, h), Ui.Night);
            DrawSparks(leftW, h);
            if (Event.current.type == EventType.Repaint)
                GUI.DrawTexture(Ui.S(new Rect(leftW, 0f, fadeW, h)), FadeTex(), ScaleMode.StretchToFill, true);

            switch (page)
            {
                case Page.Title: DrawTitle(leftW, w, h); break;
                case Page.NewSlots: DrawSlots(leftW, h, true); break;
                case Page.LoadSlots: DrawSlots(leftW, h, false); break;
                case Page.Settings: DrawSettings(leftW, h); break;
            }

            // developer signature and GitHub, bottom right, over the cinematic view, on every page
            float sx = w - 300f, sy = h - 56f;
            Ui.Box(new Rect(sx, sy, 284f, 40f), new Color(0.06f, 0.045f, 0.13f, 0.55f), new Color(1f, 1f, 1f, 0.12f), 20f, 1.5f, false);
            Ui.Label(new Rect(sx + 16f, sy, 140f, 40f), "by Zetazuni", 13f, new Color(1f, 0.95f, 0.97f, 0.9f), TextAnchor.MiddleLeft, Ui.Weight.Bold);
            if (Ui.Chip(new Rect(sx + 284f - 108f, sy + 6f, 96f, 28f), "GitHub  ↗", false, 12f)) Application.OpenURL(GameInfo.RepoUrl);

            if (confirmExit) DrawExitConfirm();
        }

        static Texture2D fadeTex;

        /// <summary>A smooth alpha ramp from Ui.Night to clear, sampled with bilinear filtering so stretching it never bands like a row of flat rects would.</summary>
        static Texture2D FadeTex()
        {
            if (fadeTex) return fadeTex;
            const int n = 64;
            fadeTex = new Texture2D(n, 1, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var night = Ui.Night;
            for (int i = 0; i < n; i++) { var c = night; c.a = 1f - i / (float)(n - 1); fadeTex.SetPixel(i, 0, c); }
            fadeTex.Apply();
            return fadeTex;
        }

        void DrawSparks(float leftW, float h)
        {
            if (Event.current.type != EventType.Repaint) return;
            float t = Time.unscaledTime;
            foreach (var s in sparks)
            {
                float y = Mathf.Repeat(s.y - t * s.speed, 1f) * h;
                float a = 0.15f + 0.2f * (0.5f + 0.5f * Mathf.Sin(t * 0.6f + s.phase));
                Ui.Round(new Rect(s.x * leftW - s.size * 0.5f, y - s.size * 0.5f, s.size, s.size), new Color(1f, 0.85f, 0.95f, a), s.size * 0.5f);
            }
        }

        // ---- the title page

        void DrawTitle(float leftW, float w, float h)
        {
            const float titleSize = 118f;
            float titleX = 44f, titleY = h * 0.14f - 8f;
            float titleW = Ui.TextWidth("Tiramisu", titleSize, Ui.Weight.Script);
            // the glow sits behind the middle of the rendered glyphs, not the label's oversized layout box
            var glowCentre = new Vector2(titleX + titleW * 0.5f, titleY + titleSize * 0.62f);
            Ui.Round(new Rect(glowCentre.x - 230f, glowCentre.y - 130f, 460f, 260f), new Color(0.65f, 0.4f, 0.9f, 0.10f), 130f);

            Ui.Label(new Rect(titleX + 4f, titleY - 4f, leftW + 200f, 150f), "Tiramisu", titleSize, new Color(1f, 0.93f, 0.95f, 0.15f), TextAnchor.UpperLeft, Ui.Weight.Script);
            Ui.Label(new Rect(titleX, titleY, leftW + 200f, 150f), "Tiramisu", titleSize, new Color(1f, 0.93f, 0.95f, 1f), TextAnchor.UpperLeft, Ui.Weight.Script);

            float ry = h * 0.14f + 108f;
            var cols = new[] { Ui.Hex("ff8fc8"), Ui.Hex("a78bfa"), Ui.Hex("57e6ff") };
            float rw = Mathf.Min(220f, leftW - 88f);
            for (int i = 0; i < 3; i++) Ui.Round(new Rect(44f + rw / 3f * i, ry, rw / 3f + 1f, 3f), cols[i], 1.5f);
            Ui.Label(new Rect(46f, ry + 14f, leftW - 40f, 26f), "A COSY LIFE, TOGETHER", 13f, new Color(0.86f, 0.6f, 0.75f, 0.9f), TextAnchor.UpperLeft, Ui.Weight.ExtraBold);

            float bw = Mathf.Min(300f, leftW - 88f), bx = 44f, by = h * 0.48f, bh = 54f, gap = 14f;
            if (BigButton(new Rect(bx, by, bw, bh), "Start", true)) { page = Page.NewSlots; confirmSlot = -1; }
            by += bh + gap;
            bool anySave = AnySave();
            if (BigButton(new Rect(bx, by, bw, bh), "Load Game", false, anySave)) page = Page.LoadSlots;
            if (!anySave) Ui.Label(new Rect(bx + 2f, by + bh + 6f, bw, 18f), "Load Game unlocks once you've played.", 11f, new Color(0.75f, 0.7f, 0.88f, 0.8f), TextAnchor.UpperLeft, Ui.Weight.Bold);
            by += bh + gap;
            if (BigButton(new Rect(bx, by, bw, bh), "Settings", false)) page = Page.Settings;
            by += bh + gap;
            if (BigButton(new Rect(bx, by, bw, bh), "Exit game", false)) confirmExit = true;

            // a little row of what the game is about, just above the version
            string[] features = { "Decorate", "Careers", "Pets", "Build your own home" };
            float fy = h - 96f, fx = 44f;
            foreach (var f in features)
            {
                float fw = Ui.TextWidth(f, 12f, Ui.Weight.ExtraBold) + 22f;
                if (fx + fw > leftW - 30f) { fx = 44f; fy += 30f; }
                var r = new Rect(fx, fy, fw, 24f);
                Ui.Round(r, new Color(1f, 1f, 1f, 0.07f), 12f);
                Ui.Ring(r, new Color(1f, 1f, 1f, 0.14f), 1.5f, 12f);
                Ui.Label(r, f, 12f, new Color(1f, 0.95f, 0.98f, 0.85f), TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                fx += fw + 8f;
            }

            Ui.Label(new Rect(44f, h - 34f, 260f, 20f), $"v{GameInfo.Version}", 12f, new Color(0.6f, 0.56f, 0.75f, 0.85f), TextAnchor.UpperLeft, Ui.Weight.Bold);
        }

        /// <summary>A left aligned button, bigger than the usual pill, with a soft glow when it is the main action.</summary>
        bool BigButton(Rect r, string label, bool primary, bool enabled = true)
        {
            bool hover = enabled && Ui.Hover(r);
            if (primary) Ui.OnFill(r, r.height * 0.5f);
            else Ui.Round(r, enabled ? (hover ? new Color(1f, 1f, 1f, 0.14f) : new Color(1f, 1f, 1f, 0.07f)) : new Color(1f, 1f, 1f, 0.03f), r.height * 0.5f);
            Ui.Ring(r, primary ? new Color(1f, 1f, 1f, 0.35f) : enabled ? new Color(1f, 1f, 1f, hover ? 0.35f : 0.18f) : new Color(1f, 1f, 1f, 0.08f), 2f, r.height * 0.5f);
            var textColour = primary ? Ui.OnAccent : enabled ? new Color(1f, 0.97f, 0.99f, 1f) : new Color(1f, 1f, 1f, 0.35f);
            Ui.Label(r, label, 17f, textColour, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
            if (!enabled) return false;
            bool clicked = Event.current.type == EventType.MouseDown && Ui.S(r).Contains(Event.current.mousePosition);
            if (clicked) { GameAudio.Play(GameAudio.Sfx.Click); Event.current.Use(); }
            return clicked;
        }

        void BackButton(float leftW)
        {
            if (Ui.Square(new Rect(20f, 20f, 40f, 34f), "←")) { page = Page.Title; confirmSlot = -1; confirmExit = false; SettingsWindow.CloseDropdowns(); }
        }

        // ---- New Game / Load Game slot pickers

        void DrawSlots(float leftW, float h, bool isNew)
        {
            BackButton(leftW);
            Ui.Label(new Rect(72f, 24f, leftW - 90f, 30f), isNew ? "Start a new game" : "Load a game", 22f, Color.white, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);

            float top = 70f, cw = leftW - 44f, ch = 84f, gap = 12f;
            for (int i = 0; i < SaveSystem.MaxSlots; i++)
            {
                int slot = i + 1;
                var r = new Rect(22f, top + i * (ch + gap), cw, ch);
                bool used = SaveSystem.SlotHasSave(slot);
                bool clickable = isNew || used;
                bool hover = clickable && Ui.Hover(r);
                Ui.Round(r, used ? new Color(1f, 1f, 1f, hover ? 0.16f : 0.1f) : hover && isNew ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 1f, 1f, 0.03f), 16f);
                Ui.Ring(r, hover && clickable ? Ui.Hex("ff8fc8") : new Color(1f, 1f, 1f, 0.14f), 2f, 16f);
                Ui.Label(new Rect(r.x + 18f, r.y + 12f, r.width - 32f, 24f), "Slot " + slot, 16f, Color.white, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                string sub = used ? SaveSystem.SlotSummary(slot) : "Empty";
                Ui.Label(new Rect(r.x + 18f, r.y + 40f, r.width - 32f, 20f), sub, 12f, new Color(0.85f, 0.8f, 0.95f, 0.85f), TextAnchor.UpperLeft, Ui.Weight.Bold);
                if (used)
                {
                    string saved = SaveSystem.SlotLastSaved(slot);
                    if (saved != "") Ui.Label(new Rect(r.x + 18f, r.y + 60f, r.width - 32f, 18f), "Saved " + saved, 11f, new Color(0.7f, 0.66f, 0.85f, 0.7f), TextAnchor.UpperLeft, Ui.Weight.Bold);
                }
                if (clickable && Event.current.type == EventType.MouseDown && Ui.S(r).Contains(Event.current.mousePosition))
                {
                    GameAudio.Play(GameAudio.Sfx.Click);
                    Event.current.Use();
                    if (isNew && used) confirmSlot = slot;
                    else EnterSlot(slot, isNew && !used);
                }
            }

            if (confirmSlot >= 0) DrawSlotConfirm(h);
        }

        void DrawSlotConfirm(float h)
        {
            var w = Ui.W;
            Ui.Rect2(new Rect(0f, 0f, w, h), new Color(0.02f, 0.015f, 0.06f, 0.6f));
            var box = new Rect(w * 0.5f - 220f, h * 0.5f - 90f, 440f, 180f);
            Ui.Box(box, Ui.Card, Ui.Pink, 22f, 3f, true);
            Ui.Label(new Rect(box.x + 24f, box.y + 20f, box.width - 48f, 30f), $"Overwrite Slot {confirmSlot}?", 20f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(box.x + 24f, box.y + 56f, box.width - 48f, 50f), "This erases what's saved there — money, furniture, careers, everything. Every other slot stays untouched.", 13f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
            float bw = (box.width - 48f - 12f) / 2f;
            if (Ui.Pill(new Rect(box.x + 24f, box.yMax - 56f, bw, 40f), "Cancel", false, 14f)) confirmSlot = -1;
            if (Ui.Pill(new Rect(box.x + 24f + bw + 12f, box.yMax - 56f, bw, 40f), "Erase & start", true, 14f)) EnterSlot(confirmSlot, true);
        }

        // ---- the title screen's own settings: only what matters before you're even in a game

        float settingsScroll;

        void DrawSettings(float leftW, float h)
        {
            BackButton(leftW);
            Ui.Label(new Rect(72f, 24f, leftW - 90f, 30f), "Settings", 22f, Color.white, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);

            var inner = new Rect(22f, 70f, leftW - 44f, h - 70f - 20f);
            settingsScroll = Ui.Scroll(inner, settingsScroll, 210f + GraphicsBlockHeight, DrawSettingsContent);
        }

        static float GraphicsBlockHeight => 480f + (SettingsWindow.DropdownOpen ? 190f : 0f);

        void DrawSettingsContent(float w)
        {
            float y = 0f;
            Ui.Label(new Rect(0f, y, w, 20f), "LOOK", 12f, new Color(0.86f, 0.6f, 0.75f, 0.9f), TextAnchor.UpperLeft, Ui.Weight.ExtraBold); y += 24f;
            float dw = (w - 8f) / 2f;
            if (Ui.Pill(new Rect(0f, y, dw, 36f), "Light mode", !Ui.Dark, 14f)) Ui.SetDark(false);
            if (Ui.Pill(new Rect(dw + 8f, y, dw, 36f), "Dark mode", Ui.Dark, 14f)) Ui.SetDark(true);
            y += 50f;

            Ui.Label(new Rect(0f, y, w, 20f), "SOUND", 12f, new Color(0.86f, 0.6f, 0.75f, 0.9f), TextAnchor.UpperLeft, Ui.Weight.ExtraBold); y += 24f;
            float nv = Ui.Slider(new Rect(0f, y, w - 50f, 28f), GameAudio.VolMaster);
            if (!Mathf.Approximately(nv, GameAudio.VolMaster)) GameAudio.SetVolume(0, nv);
            Ui.Label(new Rect(w - 46f, y, 46f, 28f), Mathf.RoundToInt(GameAudio.VolMaster * 100f) + "%", 13f, Ui.Soft, TextAnchor.MiddleRight, Ui.Weight.ExtraBold);
            y += 38f;
            if (Ui.Pill(new Rect(0f, y, w, 36f), GameAudio.Muted ? "Sound is off" : "Sound is on", GameAudio.Muted, 14f)) GameAudio.ToggleMute();
            y += 50f;

            Ui.Label(new Rect(0f, y, w, 20f), "GRAPHICS", 12f, new Color(0.86f, 0.6f, 0.75f, 0.9f), TextAnchor.UpperLeft, Ui.Weight.ExtraBold); y += 24f;
            DisplaySettings.Init();
            // SettingsWindow.DrawGraphics always draws from its own local y = 0, so it's nested in a group offset to here
            GUI.BeginGroup(Ui.S(new Rect(0f, y, w, GraphicsBlockHeight)));
            SettingsWindow.DrawGraphics(w);
            GUI.EndGroup();
        }

        void DrawExitConfirm()
        {
            var w = Ui.W; var h = Ui.H;
            Ui.Rect2(new Rect(0f, 0f, w, h), new Color(0.02f, 0.015f, 0.06f, 0.6f));
            var box = new Rect(w * 0.5f - 200f, h * 0.5f - 80f, 400f, 160f);
            Ui.Box(box, Ui.Card, Ui.Pink, 22f, 3f, true);
            Ui.Label(new Rect(box.x + 24f, box.y + 18f, box.width - 48f, 28f), "Quit Tiramisu?", 19f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(box.x + 24f, box.y + 50f, box.width - 48f, 40f), "Nothing is lost — a game only saves once you start or load one.", 13f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
            float bw = (box.width - 48f - 12f) / 2f;
            if (Ui.Pill(new Rect(box.x + 24f, box.yMax - 52f, bw, 38f), "Cancel", false, 14f)) confirmExit = false;
            if (Ui.Pill(new Rect(box.x + 24f + bw + 12f, box.yMax - 52f, bw, 38f), "Quit", true, 14f))
            {
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
            }
        }
    }
}
