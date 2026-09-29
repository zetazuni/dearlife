using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The life sim panels, in the cream and pink look of the 2D game: the needs and mood of the person you are playing (bottom left),
    /// the status window (key C: mood, feelings, traits, skills, wishes, friends, money) and the message at the top.
    /// The little icons are drawn in code.
    /// </summary>
    public class SimUi : MonoBehaviour
    {
        public static SimUi Instance { get; private set; }
        public static bool StatusOpen { get; private set; }

        Rect needsPanel, statusPanel;
        static readonly Texture2D[] icons = new Texture2D[6];
        public static readonly Color[] NeedColours =
        {
            Ui.Hex("f0a04b"), Ui.Hex("5aaee8"), Ui.Hex("8b7fe8"), Ui.Hex("f2b84b"), Ui.Hex("ee6f9b"), Ui.Hex("49c9b5"),
        };

        public static void OpenStatus() { StatusOpen = true; GameAudio.Play(GameAudio.Sfx.Click); }

        public static Texture2D Icon(int i) { if (icons[i] == null) icons[i] = MakeIcon(i); return icons[i]; }

        public static void DrawIcon(Rect r, int i)
        {
            if (Event.current.type != EventType.Repaint) return;
            var o = GUI.color; GUI.color = NeedColours[i]; GUI.DrawTexture(Ui.S(r), Icon(i)); GUI.color = o;
        }

        public bool OverPanels(Vector2 p) { var u = Ui.ToUi(p); return needsPanel.Contains(u) || (StatusOpen && statusPanel.Contains(u)); }

        void Awake() { Instance = this; }

        void Start()
        {
            var previous = OrbitCamera.IsOverUi;
            OrbitCamera.IsOverUi = p => (previous != null && previous(p)) || OverPanels(p) || Splash.Showing;
        }

        void Update()
        {
            if (MainMenu.Busy) return;
            if (Input.GetKeyDown(KeyCode.C)) { StatusOpen = !StatusOpen; GameAudio.Play(GameAudio.Sfx.Click); }
            if (StatusOpen && Input.GetKeyDown(KeyCode.Escape)) StatusOpen = false;
        }

        // ------------------------------------------------------------ icons

        static Texture2D MakeIcon(int kind)
        {
            const int N = 32;
            var t = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            for (int y = 0; y < N; y++)
                for (int x = 0; x < N; x++)
                {
                    float u = (x + 0.5f) / N * 2f - 1f, v = (y + 0.5f) / N * 2f - 1f;
                    float a = 0f;
                    switch (kind)
                    {
                        case 0: // hunger: an apple
                            a = Mathf.Max(Circle(u + 0.3f, v + 0.1f, 0.55f), Circle(u - 0.3f, v + 0.1f, 0.55f));
                            a = Mathf.Max(a, Circle(u - 0.35f, v - 0.75f, 0.24f) * (v > 0.5f ? 1f : 0f)) * (Mathf.Abs(u) < 0.06f && v > 0.5f ? 0.4f : 1f);
                            break;
                        case 1: // bladder: a drop
                            a = Mathf.Max(Circle(u, v + 0.25f, 0.55f), Mathf.Clamp01(1f - (Mathf.Abs(u) * 2.4f + (0.75f - v) * 0.5f)) * (v > -0.2f ? 1f : 0f) * ((0.85f - v) > Mathf.Abs(u) * 2.6f ? 1f : 0f));
                            break;
                        case 2: // energy: a bolt
                            a = Bolt(u, v);
                            break;
                        case 3: // fun: a star
                            a = Star(u, v);
                            break;
                        case 4: // social: two heads and shoulders
                            a = Mathf.Max(Mathf.Max(Circle(u + 0.42f, v - 0.35f, 0.3f), Circle(u - 0.42f, v - 0.35f, 0.3f)), Mathf.Max(Circle(u + 0.42f, v + 0.75f, 0.6f) * (v < 0.05f ? 1f : 0f), Circle(u - 0.42f, v + 0.75f, 0.6f) * (v < 0.05f ? 1f : 0f)));
                            break;
                        default: // hygiene: bubbles
                            a = Mathf.Max(Ring(u + 0.35f, v + 0.25f, 0.42f), Mathf.Max(Ring(u - 0.42f, v - 0.2f, 0.32f), Ring(u + 0.05f, v - 0.62f, 0.22f)));
                            break;
                    }
                    t.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01(a)));
                }
            t.Apply();
            return t;
        }

        static float Circle(float x, float y, float r) => Mathf.Clamp01((r - Mathf.Sqrt(x * x + y * y)) * 14f);
        static float Ring(float x, float y, float r) => Mathf.Clamp01((0.12f - Mathf.Abs(Mathf.Sqrt(x * x + y * y) - r)) * 14f);
        static float Bolt(float x, float y)
        {
            // a zig zag: two triangles
            float a = 0f;
            if (y > -0.05f && y < 0.95f && x > -0.45f + (0.95f - y) * 0.15f - 0.3f && x < 0.15f + (0.95f - y) * 0.15f) a = 1f;
            if (y < 0.1f && y > -0.95f && x > -0.15f - (y + 0.95f) * 0.1f && x < 0.45f - (y + 0.95f) * 0.1f + 0.05f) a = 1f;
            return a;
        }
        static float Star(float x, float y)
        {
            float ang = Mathf.Atan2(y, x), r = Mathf.Sqrt(x * x + y * y);
            float lim = 0.42f + 0.5f * Mathf.Pow(Mathf.Abs(Mathf.Cos(2.5f * (ang + Mathf.PI / 2f))), 1.6f);
            return Mathf.Clamp01((lim - r) * 14f);
        }

        // ------------------------------------------------------------ the screen

        void OnGUI()
        {
            if (MainMenu.Busy) return;
            Ui.Begin();
            float w = Ui.W, h = Ui.H;
            var who = LiveMode.Selected;

            // ---- needs of the person you play
            if (who != null && who.sim != null && !DecorateMode.Active && !Splash.Showing)
            {
                var sim = who.sim;
                float ch = 44f + 6f * 24f + (sim.wishes.Count > 0 ? 26f + sim.wishes.Count * 17f : 0f) + 10f;
                needsPanel = new Rect(12f, h - 112f - ch, 262f, ch);
                Ui.Box(needsPanel, Ui.Card.A(0.96f), Ui.Line, 18f, 2f, true);
                float x = needsPanel.x + 14f, y = needsPanel.y + 10f;
                Ui.Round(new Rect(x, y + 3f, 16f, 16f), sim.MoodColour, 8f);
                Ui.Label(new Rect(x + 22f, y, 140f, 22f), who.displayName, 16f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(x + 22f, y + 19f, 150f, 16f), sim.MoodName, 12f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                if (Ui.Chip(new Rect(needsPanel.xMax - 82f, y + 2f, 68f, 24f), "Status", false, 12f)) StatusOpen = true;
                y += 44f;
                for (int i = 0; i < 6; i++)
                {
                    DrawIcon(new Rect(x, y + 1f, 16f, 16f), i);
                    Ui.Label(new Rect(x + 22f, y, 70f, 18f), ((Need)i).ToString(), 12f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                    float v = sim.needs[i] / 100f;
                    Ui.Bar(new Rect(x + 92f, y + 4f, needsPanel.width - 92f - 28f, 10f), v, v < 0.22f ? Ui.Hex("e35d5d") : NeedColours[i]);
                    y += 24f;
                }
                y += 4f;
                if (sim.wishes.Count > 0) Ui.Label(new Rect(x, y, 200f, 16f), "WISHES", 11f, Ui.Accent, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                y += 18f;
                foreach (var wish in sim.wishes) { Ui.Label(new Rect(x, y, needsPanel.width - 24f, 16f), "· " + wish.text, 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold); y += 17f; }
            }
            else needsPanel = Rect.zero;

            if (StatusOpen && who != null && who.sim != null) DrawStatus(who, w, h);
            else statusPanel = Rect.zero;

            // ---- messages, like the 2D game's toasts
            string msg = Household.CurrentToast;
            if (!string.IsNullOrEmpty(msg) && !Splash.Showing)
            {
                float cx = (w - (HouseHud.PanelOpen ? 350f : 0f)) * 0.5f;
                float tw = Mathf.Min(Ui.TextWidth(msg, 14f, Ui.Weight.ExtraBold) + 36f, w - 420f);
                var r = new Rect(cx - tw * 0.5f, 70f, tw, 34f);
                Ui.Box(r, Ui.Card, Ui.Pink, 14f, 2f, true);
                Ui.Label(r, msg, 14f, Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
            }
        }

        // ------------------------------------------------------------ the status window

        void DrawStatus(Character who, float w, float h)
        {
            var sim = who.sim;
            float cx = (w - (HouseHud.PanelOpen ? 350f : 0f)) * 0.5f;
            statusPanel = new Rect(cx - 350f, Mathf.Max(64f, h * 0.5f - 315f), 700f, 630f);
            Ui.Box(statusPanel, Ui.Card, Ui.Pink, 22f, 3f, true);
            float x0 = statusPanel.x + 22f, y = statusPanel.y + 16f;
            Ui.Round(new Rect(x0, y + 6f, 18f, 18f), sim.MoodColour, 9f);
            Ui.Label(new Rect(x0 + 26f, y, 300f, 30f), who.displayName, 24f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(statusPanel.xMax - 320f, y, 250f, 30f), $"{sim.MoodName}  ·  mood {Mathf.RoundToInt(sim.Mood)}", 14f, Ui.Soft, TextAnchor.MiddleRight, Ui.Weight.ExtraBold);
            if (Ui.Square(new Rect(statusPanel.xMax - 50f, y - 2f, 34f, 34f), "X")) StatusOpen = false;
            y += 40f;
            Ui.Bar(new Rect(x0, y, statusPanel.width - 44f, 8f), Mathf.InverseLerp(-50f, 100f, sim.Mood), sim.MoodColour);
            y += 20f;

            float px = x0;
            foreach (var c in Character.All)
                if (!c.isPet)
                {
                    if (Ui.Chip(new Rect(px, y, 96f, 26f), c.displayName, c == who, 13f)) LiveMode.Select(c);
                    px += 102f;
                }
            y += 38f;

            float colW = 300f;
            HouseHud.Kicker(x0, y, colW, "Needs");
            float yy = y + 20f;
            for (int i = 0; i < 6; i++)
            {
                if (sim.isPet && (i == (int)Need.Bladder || i == (int)Need.Hygiene || i == (int)Need.Social)) continue;
                DrawIcon(new Rect(x0, yy + 1f, 16f, 16f), i);
                Ui.Label(new Rect(x0 + 22f, yy, 70f, 18f), ((Need)i).ToString(), 12f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                Ui.Bar(new Rect(x0 + 92f, yy + 4f, 160f, 10f), sim.needs[i] / 100f, NeedColours[i]);
                Ui.Label(new Rect(x0 + 258f, yy, 40f, 18f), Mathf.RoundToInt(sim.needs[i]).ToString(), 12f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                yy += 24f;
            }
            yy += 8f;
            HouseHud.Kicker(x0, yy, colW, "Feelings"); yy += 20f;
            int shown = 0;
            foreach (var f in sim.Feelings())
            {
                Ui.Label(new Rect(x0, yy, colW, 16f), $"{(f.value >= 0 ? "+" : "")}{Mathf.RoundToInt(f.value)}   {f.text}", 12f, f.value >= 0 ? Ui.Ink : Ui.Hex(Ui.Dark ? "ff8a8a" : "c0504d"), TextAnchor.UpperLeft, Ui.Weight.Bold);
                yy += 17f; if (++shown >= 5) break;
            }
            HouseHud.Kicker(x0, statusPanel.yMax - 190f, colW, "Traits");
            Ui.Label(new Rect(x0, statusPanel.yMax - 172f, colW, 34f), string.Join("  ·  ", sim.traits), 13f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold, true);

            // the career: choose one, see the rank, the pay and the way to the next promotion
            float cy = statusPanel.yMax - 124f;
            Ui.Rect2(new Rect(x0, cy - 10f, statusPanel.width - 44f, 2f), Ui.Line);
            HouseHud.Kicker(x0, cy, 200f, "Career");
            float chx = x0 + 70f;
            foreach (var tr in Careers.All)
            {
                float cw2 = Ui.TextWidth(tr.name, 12f, Ui.Weight.ExtraBold) + 26f;
                if (Ui.Chip(new Rect(chx, cy - 4f, cw2, 24f), tr.name, sim.job == tr.name, 12f)) sim.SetCareer(tr.name);
                chx += cw2 + 6f;
            }
            if (sim.job != "")
            {
                int lvl = sim.JobLevel;
                Ui.Label(new Rect(x0, cy + 26f, 330f, 22f), $"{sim.JobTitle}  ·  rank {lvl} of {Careers.XpAt.Length}", 15f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(x0, cy + 50f, statusPanel.width - 44f, 18f), $"Pays {Household.Currency} {Household.PayPerSecond(sim)} a second. {Careers.Get(sim.job).blurb} Work at the right desk or machine to move up.", 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold);
                var pb = new Rect(x0 + 350f, cy + 30f, statusPanel.width - 44f - 350f - 84f, 12f);
                Ui.Bar(pb, Careers.Progress(sim.jobXp), Ui.Hex("6cc287"));
                Ui.Label(new Rect(pb.xMax + 8f, cy + 24f, 80f, 22f), lvl >= Careers.XpAt.Length ? "Top rank" : $"{Mathf.RoundToInt(Careers.XpAt[lvl] - sim.jobXp)} s to go", 11f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            }

            float rx = x0 + colW + 30f, ry = y;
            HouseHud.Kicker(rx, ry, 300f, "Skills"); ry += 20f;
            foreach (Skill s in System.Enum.GetValues(typeof(Skill)))
            {
                Ui.Label(new Rect(rx, ry, 90f, 18f), s.ToString(), 12f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                Ui.Bar(new Rect(rx + 92f, ry + 4f, 140f, 10f), sim.LevelProgress(s), Ui.Hex("6cc287"));
                Ui.Label(new Rect(rx + 240f, ry, 60f, 18f), $"Level {sim.Level(s)}", 12f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                ry += 22f;
            }
            ry += 8f;
            HouseHud.Kicker(rx, ry, 300f, $"Wishes  ({sim.wishesDone} come true)"); ry += 20f;
            foreach (var wish in sim.wishes) { Ui.Label(new Rect(rx, ry, 300f, 32f), $"· {wish.text}  (+{Household.Currency} {wish.reward})", 12f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.Bold, true); ry += 32f; }
            ry += 4f;
            HouseHud.Kicker(rx, ry, 300f, "Friends"); ry += 20f;
            foreach (var kv in sim.friendship)
            {
                if (kv.Key == who.displayName) continue;
                Ui.Label(new Rect(rx, ry, 90f, 18f), kv.Key, 12f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                if (sim.relation.TryGetValue(kv.Key, out var rel) && rel != "Housemates")
                    Ui.Label(new Rect(rx + 92f, ry - 11f, 140f, 12f), rel, 10f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.Bold);
                Ui.Bar(new Rect(rx + 92f, ry + 4f, 140f, 10f), kv.Value / 100f, NeedColours[4]);
                Ui.Label(new Rect(rx + 240f, ry, 60f, 18f), Mathf.RoundToInt(kv.Value).ToString(), 12f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                ry += 22f;
            }
            ry += 6f;
            var hh = Household.Instance;
            if (hh != null)
            {
                HouseHud.Kicker(rx, ry, 300f, "Recent money"); ry += 20f;
                int n = 0;
                foreach (var l in hh.Ledger) { Ui.Label(new Rect(rx, ry, 300f, 16f), $"{(l.amount >= 0 ? "+" : "")}{l.amount}   {l.text}", 12f, l.amount >= 0 ? Ui.Hex(Ui.Dark ? "7de3a0" : "3f9a5b") : Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold); ry += 16f; if (++n >= 4) break; }
            }
            Ui.Label(new Rect(statusPanel.x, statusPanel.yMax - 22f, statusPanel.width, 16f), "C or Esc closes this window", 11f, Ui.Soft, TextAnchor.UpperCenter, Ui.Weight.Bold);
        }
    }
}
