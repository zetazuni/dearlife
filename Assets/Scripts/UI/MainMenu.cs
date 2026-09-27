using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The title screen, shown once at boot (after the splash and loading screen) and again if the player asks for it from
    /// Settings. A cream and pink panel on the left with Start, Load Game and Settings; the rest of the screen is the actual
    /// house, seen through a slow cinematic camera that drifts from room to room on its own (built from <see cref="RoomMarker"/>,
    /// so it needs no hand tuned coordinates). A soft fade blends the panel into the view.
    /// </summary>
    public class MainMenu : MonoBehaviour
    {
        public static bool Active { get; private set; } = true;
        public static MainMenu Instance { get; private set; }

        // ---- the cinematic background

        struct Shot { public Vector3 pivot; public float yaw, pitch, dist; }
        readonly List<Shot> shots = new List<Shot>();
        int shotIndex;
        float shotT;                 // 0..1 through the current shot (hold, including the drift)
        float blendT = 1f;           // 0..1 easing in from the previous shot
        Shot from, to;
        const float HoldTime = 7.5f, BlendTime = 2.2f, DriftYaw = 2.6f, ZoomIn = 0.86f;

        // ---- menu state

        bool hasSave;
        bool confirmNewGame;

        void Awake()
        {
            Instance = this;
            Active = true;      // every boot opens on the title screen (with domain reload off in the editor, this would otherwise carry over stale)
        }

        void Start()
        {
            hasSave = SaveSystem.HasSave;
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
                if (r.floor != 0) continue;          // the ground floor reads best in a slow establishing loop
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

        /// <summary>Re-opens the title screen from Settings ("Back to title"), without touching the save.</summary>
        public static void Open()
        {
            Active = true;
            if (Instance) { Instance.hasSave = SaveSystem.HasSave; Instance.BuildShots(); Instance.EnterCamera(); }
            SettingsWindow.ForceClose();
        }

        void Update()
        {
            if (!Active) return;
            if (Input.GetKeyDown(KeyCode.Escape) && confirmNewGame) confirmNewGame = false;

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

        // ------------------------------------------------------------ starting the game

        void BeginGame(bool wipe)
        {
            if (wipe) SaveSystem.NewGame();
            Active = false;
            confirmNewGame = false;
            var cam = OrbitCamera.Instance;
            if (cam)
            {
                cam.ExternalControl = false;
                cam.JumpTo(new Vector3(15f, 0f, 9f), 38f);
            }
            Household.Toast(wipe ? "New game — welcome home!" : "Welcome back!");
        }

        // ------------------------------------------------------------ the screen

        void OnGUI()
        {
            if (!Active) return;
            Ui.Begin();
            GUI.depth = -500;                 // over the ordinary HUD (which doesn't draw itself while Active anyway), under the loading screen and splash
            float w = Ui.W, h = Ui.H;

            float leftW = Mathf.Min(600f, w * 0.44f), fadeW = Mathf.Min(170f, w * 0.12f);
            Ui.Rect2(new Rect(0f, 0f, leftW, h), Ui.Night);
            int strips = 40;
            for (int i = 0; i < strips; i++)
            {
                float t = i / (float)(strips - 1);
                var c = Ui.Night; c.a = 1f - t;
                Ui.Rect2(new Rect(leftW + fadeW * t, 0f, fadeW / strips + 1f, h), c);
            }

            // title
            Ui.Label(new Rect(48f, 56f, leftW - 40f, 100f), "Tiramisu", 68f, new Color(1f, 0.93f, 0.95f, 1f), TextAnchor.UpperLeft, Ui.Weight.Script);
            Ui.Label(new Rect(52f, 132f, leftW - 40f, 26f), "A COSY LIFE, TOGETHER", 13f, new Color(0.86f, 0.6f, 0.75f, 0.9f), TextAnchor.UpperLeft, Ui.Weight.ExtraBold);

            // buttons, vertical, left middle
            float bw = Mathf.Min(300f, leftW - 96f), bx = 48f, by = h * 0.5f - 86f, bh = 54f, gap = 14f;
            string primaryLabel = hasSave ? "New Game" : "Start";
            if (BigButton(new Rect(bx, by, bw, bh), primaryLabel, true))
            {
                if (hasSave) confirmNewGame = true; else BeginGame(false);
            }
            by += bh + gap;
            if (BigButton(new Rect(bx, by, bw, bh), "Load Game", false, hasSave)) BeginGame(false);
            by += bh + gap;
            if (BigButton(new Rect(bx, by, bw, bh), "Settings", false)) SettingsWindow.Toggle();

            if (!hasSave) Ui.Label(new Rect(bx + 2f, by + bh + 10f, bw, 18f), "Load Game unlocks once you've played.", 11f, new Color(0.75f, 0.7f, 0.88f, 0.8f), TextAnchor.UpperLeft, Ui.Weight.Bold);

            // version, bottom left
            Ui.Label(new Rect(48f, h - 34f, 260f, 20f), $"v{GameInfo.Version}", 12f, new Color(0.6f, 0.56f, 0.75f, 0.85f), TextAnchor.UpperLeft, Ui.Weight.Bold);

            // developer signature and GitHub, bottom right, over the cinematic view
            float sx = w - 300f, sy = h - 56f;
            Ui.Box(new Rect(sx, sy, 284f, 40f), new Color(0.06f, 0.045f, 0.13f, 0.55f), new Color(1f, 1f, 1f, 0.12f), 20f, 1.5f, false);
            Ui.Label(new Rect(sx + 16f, sy, 140f, 40f), "by Zetazuni", 13f, new Color(1f, 0.95f, 0.97f, 0.9f), TextAnchor.MiddleLeft, Ui.Weight.Bold);
            if (Ui.Chip(new Rect(sx + 284f - 108f, sy + 6f, 96f, 28f), "GitHub  ↗", false, 12f)) Application.OpenURL(GameInfo.RepoUrl);

            if (confirmNewGame) DrawConfirm(w, h);
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

        void DrawConfirm(float w, float h)
        {
            Ui.Rect2(new Rect(0f, 0f, w, h), new Color(0.02f, 0.015f, 0.06f, 0.6f));
            var box = new Rect(w * 0.5f - 220f, h * 0.5f - 90f, 440f, 180f);
            Ui.Box(box, Ui.Card, Ui.Pink, 22f, 3f, true);
            Ui.Label(new Rect(box.x + 24f, box.y + 20f, box.width - 48f, 30f), "Start a new game?", 20f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(box.x + 24f, box.y + 56f, box.width - 48f, 50f), "This erases your current save — money, furniture, careers, everything. Your settings stay as they are.", 13f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
            float bw = (box.width - 48f - 12f) / 2f;
            if (Ui.Pill(new Rect(box.x + 24f, box.yMax - 56f, bw, 40f), "Cancel", false, 14f)) confirmNewGame = false;
            if (Ui.Pill(new Rect(box.x + 24f + bw + 12f, box.yMax - 56f, bw, 40f), "Erase & start", true, 14f)) BeginGame(true);
        }
    }
}
