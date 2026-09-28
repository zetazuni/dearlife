using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The main screen furniture, in the style of the 2D Tiramisu App: the cream top bar (title, money, date, speed), the round camera
    /// buttons on the left, the hint at the bottom and the side panel on the right with its tabs (Home, Family, Shop, Build, Settings).
    /// The shop and build tabs are drawn by <see cref="BuyMode"/> and <see cref="BuildMode"/>.
    /// </summary>
    public class HouseHud : MonoBehaviour
    {
        public enum Tab { Home, Family, Shop, Build }

        public static HouseHud Instance { get; private set; }
        public static Rect TopBar, CameraColumn, Side;
        public static bool PanelOpen = true;
        public static Tab Current = Tab.Home;

        const float BarH = 58f, SideW = 350f;
        float scrollHome, scrollFamily;
        public static Rect Handle;

        void Awake()
        {
            Instance = this;
            OrbitCamera.IsOverUi = p =>
            {
                var u = Ui.ToUi(p);
                return TopBar.Contains(u) || CameraColumn.Contains(u) || (PanelOpen && Side.Contains(u)) || Handle.Contains(u) || SettingsWindow.Open || MapWindow.Open || LoadingScreen.Blocking || ColourPicker.Box.Contains(u) || MainMenu.Busy;
            };
            if (!GetComponent<LoadingScreen>()) gameObject.AddComponent<LoadingScreen>();
            if (!GetComponent<Splash>()) gameObject.AddComponent<Splash>();
            if (!GetComponent<SettingsWindow>()) gameObject.AddComponent<SettingsWindow>();
            if (!GetComponent<ColourPicker>()) gameObject.AddComponent<ColourPicker>();
            if (!GetComponent<MainMenu>()) gameObject.AddComponent<MainMenu>();
        }

        void Update()
        {
            if (MainMenu.Busy) return;
            // the shop and the build tool own their tab while they are on
            if (BuyMode.Active && Current != Tab.Shop) { Current = Tab.Shop; PanelOpen = true; }
            else if (BuildMode.Active && Current != Tab.Build) { Current = Tab.Build; PanelOpen = true; }
            if (Current == Tab.Shop && !BuyMode.Active) Current = Tab.Home;
            if (Current == Tab.Build && !BuildMode.Active) Current = Tab.Home;
            if (Input.GetKeyDown(KeyCode.H) && !DecorateMode.Active && !SettingsWindow.Open) PanelOpen = !PanelOpen;
        }

        public static void Show(Tab t)
        {
            PanelOpen = true;
            if (t != Tab.Shop && BuyMode.Active) BuyMode.Toggle();
            if (t != Tab.Build && BuildMode.Active) BuildMode.Toggle();
            if (t == Tab.Shop && !BuyMode.Active) BuyMode.Toggle();
            if (t == Tab.Build && !BuildMode.Active) BuildMode.Toggle();
            Current = t;
        }

        // ------------------------------------------------------------ the screen

        void OnGUI()
        {
            Ui.Begin();
            float w = Ui.W, h = Ui.H;
            var view = HouseView.Instance; var cam = OrbitCamera.Instance;
            if (!view || !cam || MainMenu.Busy) return;

            DrawTopBar(w);
            DrawCameraColumn(view, cam);
            Side = PanelOpen ? new Rect(w - SideW, BarH, SideW, h - BarH) : Rect.zero;
            if (PanelOpen) DrawSide(view, cam);
            DrawHint(w, h);
            DrawHandle(w, h);
        }

        // ---- top bar

        void DrawTopBar(float w)
        {
            TopBar = new Rect(0f, 0f, w, BarH);
            Ui.Rect2(TopBar, Ui.Card.A(0.93f));
            Ui.Rect2(new Rect(0f, BarH - 2f, w, 2f), Ui.Dark ? Ui.Pink : Ui.Line);

            Ui.Label(new Rect(18f, 3f, 220f, 40f), "Dearlife", 34f, Ui.Rose, TextAnchor.UpperLeft, Ui.Weight.Script);
            Ui.Label(new Rect(20f, 37f, 220f, 16f), $"v{GameInfo.Version}", 11f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);

            float x = 200f, y = 12f, ph = 34f;
            var hh = Household.Instance; var dn = DayNightCycle.Instance; var sea = SeasonCycle.Instance;

            // money
            string money = $"{Household.Currency} {(hh ? hh.Funds : 0):N0}";
            float mw = Ui.TextWidth(money, 15f, Ui.Weight.ExtraBold) + 46f;
            var r = new Rect(x, y, mw, ph);
            Ui.Round(r, Ui.Surface, 17f); Ui.Ring(r, Ui.Line, 2f, 17f);
            Ui.Round(new Rect(r.x + 10f, r.y + 8f, 18f, 18f), Ui.Gold, 9f); Ui.Ring(new Rect(r.x + 10f, r.y + 8f, 18f, 18f), Ui.Hex("d99a25"), 2f, 9f);
            Ui.Label(new Rect(r.x + 10f, r.y + 8f, 18f, 18f), "$", 11f, Ui.Hex("a26f0b"), TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(r.x + 36f, r.y, mw - 40f, ph), money, 15f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            x += mw + 10f;

            // date and clock
            if (dn)
            {
                string date = $"Day {dn.DayCount + 1}  ·  {(sea ? sea.Label : "")}  ·  {dn.Clock}";
                float dw = Ui.TextWidth(date, 14f, Ui.Weight.ExtraBold) + 28f;
                r = new Rect(x, y, dw, ph);
                Ui.Round(r, Ui.Surface, 17f); Ui.Ring(r, Ui.Line, 2f, 17f);
                Ui.Label(r, date, 14f, Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                x += dw + 10f;
            }
            if (hh && w > 1250f)
            {
                string bills = $"Bills on day {hh.NextBillDay + 1}";
                float bw = Ui.TextWidth(bills, 13f, Ui.Weight.ExtraBold) + 28f;
                r = new Rect(x, y, bw, ph);
                Ui.Round(r, Ui.Surface, 17f); Ui.Ring(r, Ui.Line, 2f, 17f);
                Ui.Label(r, bills, 13f, Ui.Soft, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
            }

            // right side: speed, move, panel
            float rx = w - 12f;
            var setBtn = new Rect(rx - 92f, y, 92f, ph); rx -= 102f;
            if (Ui.Pill(setBtn, "Settings", SettingsWindow.Open, 13f)) SettingsWindow.Toggle();
            var mapBtn = new Rect(rx - 84f, y, 84f, ph); rx -= 94f;
            if (Ui.Pill(mapBtn, "Map (M)", MapWindow.Open, 13f)) MapWindow.Toggle();
            var dec = DecorateMode.Instance;
            if (dec)
            {
                var moveBtn = new Rect(rx - 88f, y, 88f, ph); rx -= 98f;
                if (Ui.Pill(moveBtn, "Move (P)", DecorateMode.Active, 13f)) dec.Toggle();
            }
            string[] names = { "II", "1x", "2x", "3x" };
            float gw = 44f * 4f + 4f;
            var group = new Rect(rx - gw, y, gw, ph);
            Ui.Round(group, Ui.Surface, 17f); Ui.Ring(group, Ui.Line, 2f, 17f);
            for (int i = 0; i < 4; i++)
            {
                var b = new Rect(group.x + 4f + i * 44f, y + 4f, 40f, ph - 8f);
                bool on = LiveMode.Speed == i;
                bool hov = Ui.Hover(b);
                if (on) Ui.OnFill(b, 13f); else Ui.Round(b, hov ? Ui.Pale : Color.clear, 13f);
                Ui.Label(b, names[i], 13f, on ? Ui.OnAccent : Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                if (GUI.Button(Ui.S(b), GUIContent.none, GUIStyle.none)) { LiveMode.SetSpeed(i); GameAudio.Play(GameAudio.Sfx.Click); }
            }

            // undo and redo while you are decorating, shopping or building (Ctrl+Z and Ctrl+Y do the same)
            if (DecorateMode.Active || BuyMode.Active || BuildMode.Active)
            {
                bool build = BuildMode.Active && BuildMode.Instance != null;
                bool canUndo = build ? BuildMode.Instance.CanUndo : dec != null && dec.CanUndo;
                bool canRedo = build ? BuildMode.Instance.CanRedo : dec != null && dec.CanRedo;
                float ux = group.x - 10f - 84f;
                if (Ui.Pill(new Rect(ux, y, 84f, ph), "Redo", false, 13f, canRedo)) { if (build) BuildMode.Instance.RedoBuild(); else dec.Redo(); }
                if (Ui.Pill(new Rect(ux - 92f, y, 84f, ph), "Undo", false, 13f, canUndo)) { if (build) BuildMode.Instance.UndoBuild(); else dec.Undo(); }
            }
        }

        // ---- the round camera buttons

        void DrawCameraColumn(HouseView view, OrbitCamera cam)
        {
            const float bw = 46f, bh = 40f, gap = 6f;
            float x = 12f, y = BarH + 12f;
            CameraColumn = new Rect(x - 4f, y - 4f, bw + 8f, 6f * (bh + gap) + 24f);
            if (Ui.Square(new Rect(x, y, bw, bh), "Gnd", view.view == HouseView.View.Ground)) view.SetView(HouseView.View.Ground); y += bh + gap;
            if (Ui.Square(new Rect(x, y, bw, bh), "Up", view.view == HouseView.View.Upper)) view.SetView(HouseView.View.Upper); y += bh + gap;
            if (Ui.Square(new Rect(x, y, bw, bh), "All", view.view == HouseView.View.Whole)) view.SetView(HouseView.View.Whole); y += bh + gap;
            Ui.Rect2(new Rect(x + 6f, y - 4f, bw - 12f, 2f), Ui.Line); y += 2f;
            if (Ui.Square(new Rect(x, y, bw, bh), "Fit")) cam.FitHouse(view.ActiveFloorY); y += bh + gap;
            string wm = view.wallMode == HouseView.WallMode.Auto ? "Auto" : view.wallMode == HouseView.WallMode.Up ? "Up" : "Down";
            if (Ui.Square(new Rect(x, y, bw, bh), wm)) view.CycleWallMode();
            Ui.Label(new Rect(x - 4f, y + bh + 1f, bw + 8f, 14f), "walls", 10f, Ui.Soft, TextAnchor.UpperCenter, Ui.Weight.ExtraBold);
        }

        // ---- the little vertical tab on the edge of the panel that hides and shows it (the 2D game's side button)

        void DrawHandle(float w, float h)
        {
            const float hw = 28f, hh = 132f;
            float x = PanelOpen ? Side.x - hw + 2f : w - hw;
            Handle = new Rect(x, h * 0.5f - hh * 0.5f, hw, hh);
            bool hover = Ui.Hover(Handle);
            var r = Handle;
            Ui.Round(new Rect(r.x, r.y, r.width + 12f, r.height), hover ? Ui.Pale : Ui.Card.A(0.97f), 12f);
            Ui.Ring(new Rect(r.x, r.y, r.width + 12f, r.height), hover ? Ui.Pink : Ui.Line, 2f, 12f);
            if (PanelOpen) Ui.Rect2(new Rect(r.xMax - 1f, r.y + 3f, 14f, r.height - 6f), Ui.Card.A(0.97f));
            // the words run up the tab
            if (Event.current.type == EventType.Repaint)
            {
                var saved = GUI.matrix;
                var pivot = new Vector2(r.center.x * Ui.Scale, r.center.y * Ui.Scale);
                GUIUtility.RotateAroundPivot(-90f, pivot);
                Ui.Label(new Rect(r.center.x - 60f, r.center.y - 11f, 120f, 22f), PanelOpen ? "Hide panel" : "Show panel", 13f, Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                GUI.matrix = saved;
            }
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && Handle.Contains(Ui.Mouse))
            {
                PanelOpen = !PanelOpen; GameAudio.Play(GameAudio.Sfx.Click); Event.current.Use();
            }
        }

        // ---- the hint

        void DrawHint(float w, float h)
        {
            string s = BuyMode.Active ? "Pick something in the shop, move it into place and click. R turns it, Esc puts it back."
                     : BuildMode.Active ? "Build mode: choose a tool on the right. Shift draws diagonal walls, Z undoes, Esc leaves."
                     : DecorateMode.Active ? "Move mode: click a piece to pick it up, click again to put it down. R turns it, Delete sells it."
                     : "Click the floor to walk  ·  click things for their menu  ·  WASD move  ·  M map  ·  P move furniture  ·  Space pause";
            float tw = Mathf.Min(Ui.TextWidth(s, 13f) + 26f, w - (PanelOpen ? SideW : 0f) - 40f);
            var r = new Rect(12f, h - 40f, tw, 28f);
            Ui.Round(r, Ui.Card.A(0.92f), 14f);
            Ui.Label(r, s, 13f, Ui.Soft, TextAnchor.MiddleCenter, Ui.Weight.Bold);
        }

        // ------------------------------------------------------------ the side panel

        void DrawSide(HouseView view, OrbitCamera cam)
        {
            Ui.Rect2(Side, Ui.Card.A(0.97f));
            Ui.Rect2(new Rect(Side.x, Side.y, 2f, Side.height), Ui.Line);

            string[] names = { "Home", "Family", "Shop", "Build" };
            float tx = Side.x + 8f, tw = (Side.width - 16f - 4f * 3f) / 4f, ty = Side.y + 8f, th = 30f;
            for (int i = 0; i < 4; i++)
            {
                var r = new Rect(tx + i * (tw + 4f), ty, tw, th);
                bool on = (int)Current == i;
                Ui.Round(new Rect(r.x, r.y, r.width, r.height + 12f), on ? Ui.Panel : Ui.Cream2, 12f);
                Ui.Ring(new Rect(r.x, r.y, r.width, r.height + 12f), on ? Ui.Pink : Ui.Line, 2f, 12f);
                Ui.Label(r, names[i], 12f, on ? Ui.Accent : Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                if (GUI.Button(Ui.S(r), GUIContent.none, GUIStyle.none)) { GameAudio.Play(GameAudio.Sfx.Click); Show((Tab)i); }
            }

            var card = new Rect(Side.x + 8f, ty + th, Side.width - 16f, Side.height - th - 16f);
            Ui.Round(card, Ui.Panel, 14f);
            Ui.Ring(card, Ui.Pink, 2f, 14f);
            var inner = new Rect(card.x + 12f, card.y + 12f, card.width - 24f, card.height - 24f);

            switch (Current)
            {
                case Tab.Home: scrollHome = Ui.Scroll(inner, scrollHome, HomeHeight, cw => DrawHome(cw, view, cam)); break;
                case Tab.Family: scrollFamily = Ui.Scroll(inner, scrollFamily, FamilyHeight, DrawFamily); break;
                case Tab.Shop: if (BuyMode.Instance) BuyMode.Instance.DrawShop(inner); break;
                default: if (BuildMode.Instance) BuildMode.Instance.DrawBuild(inner); break;
            }
        }

        public static void Kicker(float x, float y, float w, string s) => Ui.Label(new Rect(x, y, w, 16f), s.ToUpperInvariant(), 11f, Ui.Accent, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);

        // ---- Home

        float HomeHeight
        {
            get
            {
                if (!LotManager.AtHome) return 320f;
                var v = HouseView.Instance; int rooms = 0;
                if (v) foreach (var r in RoomMarker.All) if (r.floor == v.ActiveFloor) rooms++;
                return 46f + 20f + rooms * 50f + 6f + 66f + 66f + 40f + 20f;
            }
        }

        void DrawHome(float w, HouseView view, OrbitCamera cam)
        {
            float y = 0f;
            if (!LotManager.AtHome)
            {
                // on an empty lot: what this place is, the storeys, and the way back
                var lot = LotManager.Current;
                Kicker(0f, y, w, "You are at"); y += 20f;
                Ui.Label(new Rect(0f, y, w, 26f), lot.lotName, 20f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold); y += 28f;
                Ui.Label(new Rect(0f, y, w, 46f), lot.blurb + $" ({lot.Size.x:0} x {lot.Size.y:0} m)", 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true); y += 52f;
                Kicker(0f, y, w, "Storey"); y += 20f;
                float cw3 = (w - 12f) / 3f;
                if (Ui.Chip(new Rect(0f, y, cw3, 30f), "Ground", view.view == HouseView.View.Ground, 13f)) view.SetView(HouseView.View.Ground, false);
                if (Ui.Chip(new Rect(cw3 + 6f, y, cw3, 30f), "Upper", view.view == HouseView.View.Upper, 13f)) view.SetView(HouseView.View.Upper, false);
                if (Ui.Chip(new Rect((cw3 + 6f) * 2f, y, cw3, 30f), "Roofs too", view.view == HouseView.View.Whole, 13f)) view.SetView(HouseView.View.Whole, false);
                y += 46f;
                float hw2 = (w - 6f) / 2f;
                if (Ui.Pill(new Rect(0f, y, hw2, 34f), "Shop (B)", false, 13f)) Show(Tab.Shop);
                if (Ui.Pill(new Rect(hw2 + 6f, y, hw2, 34f), "Build (V)", false, 13f)) Show(Tab.Build);
                y += 46f;
                if (Ui.Pill(new Rect(0f, y, hw2, 34f), "Move (P)", DecorateMode.Active, 13f) && DecorateMode.Instance) DecorateMode.Instance.Toggle();
                if (Ui.Pill(new Rect(hw2 + 6f, y, hw2, 34f), "Map (M)", false, 13f)) MapWindow.Toggle();
                y += 46f;
                if (Ui.Pill(new Rect(0f, y, w, 34f), "Go home", false, 13f) && LotManager.Home != null) LotManager.Instance.Travel(LotManager.Home);
                return;
            }
            Kicker(0f, y, w, "Floors"); y += 20f;
            float cw = (w - 12f) / 3f;
            if (Ui.Chip(new Rect(0f, y, cw, 30f), "Ground", view.view == HouseView.View.Ground, 13f)) view.SetView(HouseView.View.Ground);
            if (Ui.Chip(new Rect(cw + 6f, y, cw, 30f), "Upper", view.view == HouseView.View.Upper, 13f)) view.SetView(HouseView.View.Upper);
            if (Ui.Chip(new Rect((cw + 6f) * 2f, y, cw, 30f), "Whole house", view.view == HouseView.View.Whole, 13f)) view.SetView(HouseView.View.Whole);
            y += 46f;

            Kicker(0f, y, w, "Rooms"); y += 20f;
            foreach (var r in RoomMarker.All)
            {
                if (r.floor != view.ActiveFloor) continue;
                var rect = new Rect(0f, y, w, 44f);
                if (Ui.CardButton(rect))
                {
                    if (view.view == HouseView.View.Whole) view.SetView(HouseView.View.Upper, false);
                    cam.FocusOn(r.transform.position, r.ViewDistance);
                }
                Ui.Label(new Rect(14f, y + 4f, w - 28f, 20f), r.displayName, 15f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(14f, y + 23f, w - 28f, 16f), "Show this room", 11f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold);
                y += 50f;
            }
            y += 6f;

            float hw = (w - 6f) / 2f;
            Kicker(0f, y, w, "Shop and building"); y += 20f;
            if (Ui.Pill(new Rect(0f, y, hw, 34f), "Shop (B)", false, 13f)) Show(Tab.Shop);
            if (Ui.Pill(new Rect(hw + 6f, y, hw, 34f), "Build (V)", false, 13f)) Show(Tab.Build);
            y += 46f;

            Kicker(0f, y, w, "Furniture"); y += 20f;
            var dec = DecorateMode.Instance;
            if (dec)
            {
                if (Ui.Pill(new Rect(0f, y, hw, 34f), "Move (P)", DecorateMode.Active, 13f)) dec.Toggle();
                if (Ui.Pill(new Rect(hw + 6f, y, hw, 34f), "Reset layout", false, 13f)) dec.ResetLayout();
            }
            y += 46f;
            var gfx = GraphicsModes.Instance;
            if (gfx && Ui.Pill(new Rect(0f, y, w, 34f), "Graphics: " + gfx.Label, false, 13f)) gfx.Apply((GraphicsModes.Mode)(((int)gfx.mode + 1) % 3));
        }

        // ---- Family

        float FamilyHeight { get { int n = 0; foreach (var c in Character.All) if (c != null && !string.IsNullOrEmpty(c.displayName)) n++; return n * 158f + 10f; } }

        void DrawFamily(float w)
        {
            float y = 0f;
            foreach (var c in Character.All)
            {
                if (c == null || string.IsNullOrEmpty(c.displayName)) continue;
                bool me = c == LiveMode.Selected;
                var rect = new Rect(0f, y, w, 150f);
                if (Ui.CardButton(rect, me))
                {
                    if (!c.isPet) LiveMode.Select(c);
                    OrbitCamera.Instance.FocusOn(c.transform.position, 7f);
                }
                var sim = c.sim;
                Ui.Round(new Rect(12f, y + 12f, 16f, 16f), sim ? sim.MoodColour : Ui.Line, 8f);
                Ui.Label(new Rect(36f, y + 8f, w - 130f, 22f), c.displayName, 17f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(w - 112f, y + 10f, 100f, 18f), sim ? sim.MoodName : "", 12f, Ui.Soft, TextAnchor.UpperRight, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(12f, y + 32f, w - 24f, 18f), c.Activity + (c.Queued > 0 ? $" (+{c.Queued})" : ""), 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold);
                if (sim != null)
                {
                    float by = y + 58f;
                    int slot = 0;
                    for (int i = 0; i < 6; i++)
                    {
                        if (sim.isPet && (i == (int)Need.Bladder || i == (int)Need.Hygiene || i == (int)Need.Social)) continue;
                        int col = slot % 2, row = slot / 2; slot++;
                        var cell = new Rect(12f + col * ((w - 24f) / 2f), by + row * 22f, (w - 24f) / 2f - 8f, 18f);
                        SimUi.DrawIcon(new Rect(cell.x, cell.y, 14f, 14f), i);
                        Ui.Bar(new Rect(cell.x + 20f, cell.y + 4f, cell.width - 24f, 8f), sim.needs[i] / 100f, SimUi.NeedColours[i]);
                    }
                    if (!c.isPet && Ui.Chip(new Rect(w - 78f, y + 120f, 66f, 24f), "Status", false, 12f)) { LiveMode.Select(c); SimUi.OpenStatus(); }
                    if (!c.isPet) Ui.Label(new Rect(12f, y + 124f, w - 100f, 18f), me ? "You are playing this person" : "Click to play as them", 11f, me ? Ui.Accent : Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                }
                y += 158f;
            }
        }
    }
}
