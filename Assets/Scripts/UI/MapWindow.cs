using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The map (key M), like the world map of the Sims 4: the streets round home, the home plot and the empty lots, with a card for each lot
    /// and a button to travel there. Everyone in the household goes along.
    /// </summary>
    public class MapWindow : MonoBehaviour
    {
        public static bool Open { get; private set; }
        public static Rect Box;
        static Lot selected;

        // the streets of the city (see CityBuilder): these lines are the ones in view
        static readonly float[] RoadX = { -79.4f, -19.4f, 40.6f, 100.6f };
        static readonly float[] RoadZ = { -40f, 50f, 140f };
        static readonly Rect World = new Rect(-112f, -46f, 246f, 170f);

        public static void Toggle()
        {
            Open = !Open;
            if (Open) selected = LotManager.Current;
            GameAudio.Play(GameAudio.Sfx.Click);
        }

        void Update()
        {
            if (Splash.Showing || SettingsWindow.Open) return;
            if (Input.GetKeyDown(KeyCode.M)) Toggle();
            if (Open && Input.GetKeyDown(KeyCode.Escape)) Open = false;
        }

        void OnGUI()
        {
            if (!Open || Splash.Showing) { Box = Rect.zero; return; }
            Ui.Begin();
            float w = Ui.W, h = Ui.H;
            Ui.Rect2(new Rect(0f, 0f, w, h), Ui.Dark ? new Color(0.02f, 0.015f, 0.06f, 0.72f) : new Color(0.19f, 0.11f, 0.08f, 0.38f));
            float bw = Mathf.Min(1000f, w - 40f), bh = Mathf.Min(560f, h - 40f);
            Box = new Rect(w * 0.5f - bw * 0.5f, h * 0.5f - bh * 0.5f, bw, bh);
            Ui.Box(Box, Ui.Card, Ui.Pink, 22f, 3f, true);
            Ui.Label(new Rect(Box.x + 24f, Box.y + 14f, 300f, 34f), "Map", 28f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(Box.x + 110f, Box.y + 20f, 420f, 24f), "Pick a lot and go there. Everybody comes along.", 13f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.Bold);
            if (Ui.Square(new Rect(Box.xMax - 54f, Box.y + 14f, 34f, 34f), "X")) Open = false;

            // ---- the map
            float mapW = Box.width - 24f - 320f - 24f, mapH = Box.height - 70f - 20f;
            var map = new Rect(Box.x + 24f, Box.y + 60f, mapW, mapH);
            float k = Mathf.Min(map.width / World.width, map.height / World.height);
            var drawn = new Rect(map.x + (map.width - World.width * k) * 0.5f, map.y + (map.height - World.height * k) * 0.5f, World.width * k, World.height * k);
            Ui.Round(drawn, Ui.Dark ? Ui.Hex("0e0a20") : Ui.Hex("f4ecd8"), 16f);
            Ui.Ring(drawn, Ui.Line, 2f, 16f);

            Rect ToMap(float x0, float z0, float x1, float z1)
            {
                // z runs up the map, like north on a paper map
                float ax = drawn.x + (x0 - World.x) * k, bx = drawn.x + (x1 - World.x) * k;
                float ay = drawn.yMax - (z1 - World.y) * k, by = drawn.yMax - (z0 - World.y) * k;
                return new Rect(Mathf.Min(ax, bx), Mathf.Min(ay, by), Mathf.Abs(bx - ax), Mathf.Abs(by - ay));
            }

            // blocks first (light), then the streets over them
            var blockCol = Ui.Dark ? Ui.Hex("1a1438") : Ui.Hex("e9dfc4");
            for (int i = -1; i < RoadX.Length; i++)
                for (int j = -1; j < RoadZ.Length; j++)
                {
                    float xa = (i < 0 ? World.x - 40f : RoadX[i]) + 6.4f, xb = (i + 1 >= RoadX.Length ? World.xMax + 40f : RoadX[i + 1]) - 6.4f;
                    float za = (j < 0 ? World.y - 40f : RoadZ[j]) + 6.4f, zb = (j + 1 >= RoadZ.Length ? World.yMax + 40f : RoadZ[j + 1]) - 6.4f;
                    var r = ToMap(Mathf.Max(xa, World.x), Mathf.Max(za, World.y), Mathf.Min(xb, World.xMax), Mathf.Min(zb, World.yMax));
                    if (r.width > 2f && r.height > 2f) Ui.Round(r, blockCol, 5f);
                }
            var roadCol = Ui.Dark ? Ui.Hex("3a2f66") : Ui.Hex("d3c5a5");
            foreach (float x in RoadX) { var r = ToMap(x - 3.2f, World.y, x + 3.2f, World.yMax); Ui.Rect2(new Rect(r.x, drawn.y + 1f, r.width, drawn.height - 2f), roadCol); }
            foreach (float z in RoadZ) { var r = ToMap(World.x, z - 3.2f, World.xMax, z + 3.2f); Ui.Rect2(new Rect(drawn.x + 1f, r.y, drawn.width - 2f, r.height), roadCol); }
            // the home plot and the pool of the house are drawn as a tiny plan
            foreach (var lot in LotManager.Lots)
            {
                var r = ToMap(lot.min.x, lot.min.y, lot.max.x, lot.max.y);
                bool sel = lot == selected, cur = lot == LotManager.Current, hover = Ui.Hover(r);
                Color fill = lot.home ? (Ui.Dark ? Ui.Hex("5a1f46") : Ui.Hex("f7c9d4")) : (Ui.Dark ? Ui.Hex("14483f") : Ui.Hex("bfe3c4"));
                Ui.Round(r, fill, 6f);
                Ui.Ring(r, sel ? Ui.Accent : hover ? Ui.Pink : Ui.Line, sel ? 3f : 2f, 6f);
                if (lot.home)
                {
                    var house = ToMap(0f, 0f, 30f, 8f);
                    Ui.Round(house, Ui.Dark ? Ui.Hex("ff8fc9") : Ui.Hex("b5566e"), 3f);
                }
                if (GUI.Button(Ui.S(r), GUIContent.none, GUIStyle.none)) { selected = lot; GameAudio.Play(GameAudio.Sfx.Click); }
                // the pin
                var c = r.center;
                Ui.Round(new Rect(c.x - 13f, c.y - 13f, 26f, 26f), cur ? Ui.Accent : Ui.Surface, 13f);
                Ui.Ring(new Rect(c.x - 13f, c.y - 13f, 26f, 26f), sel ? Ui.Accent : Ui.Pink, 2.5f, 13f);
                string mark = lot.home ? "H" : lot.order.ToString();
                Ui.Label(new Rect(c.x - 13f, c.y - 13f, 26f, 26f), mark, 13f, cur ? Ui.OnAccent : Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(c.x - 60f, r.yMax + 2f, 120f, 16f), lot.lotName, 11f, Ui.Ink, TextAnchor.UpperCenter, Ui.Weight.ExtraBold);
            }
            Ui.Label(new Rect(drawn.x + 8f, drawn.yMax - 22f, 200f, 18f), "north is up", 11f, Ui.Soft, TextAnchor.LowerLeft, Ui.Weight.ExtraBold);

            // ---- the cards
            float cx = Box.xMax - 24f - 320f, cy = Box.y + 60f;
            foreach (var lot in LotManager.Lots)
            {
                bool sel = lot == selected, cur = lot == LotManager.Current;
                float chH = sel ? 150f : 64f;
                var r = new Rect(cx, cy, 320f, chH);
                if (Ui.CardButton(new Rect(r.x, r.y, r.width, 64f), sel)) selected = lot;
                if (sel) { Ui.Round(new Rect(r.x, r.y + 20f, r.width, chH - 20f), Ui.Paper, 16f); Ui.Ring(new Rect(r.x, r.y, r.width, chH), Ui.Pink, 2f, 16f); }
                string mark = lot.home ? "H" : lot.order.ToString();
                Ui.Round(new Rect(r.x + 12f, r.y + 14f, 34f, 34f), cur ? Ui.Accent : Ui.Cream2, 17f);
                Ui.Label(new Rect(r.x + 12f, r.y + 14f, 34f, 34f), mark, 15f, cur ? Ui.OnAccent : Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(r.x + 56f, r.y + 10f, r.width - 66f, 22f), lot.lotName, 16f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(r.x + 56f, r.y + 32f, r.width - 66f, 18f), cur ? "You are here" : lot.home ? "Ready built" : $"{lot.Size.x:0} x {lot.Size.y:0} m, empty", 12f, cur ? Ui.Accent : Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                if (sel)
                {
                    Ui.Label(new Rect(r.x + 14f, r.y + 68f, r.width - 28f, 44f), lot.blurb, 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
                    if (Ui.Pill(new Rect(r.x + 14f, r.y + 112f, r.width - 28f, 30f), cur ? "You are here" : "Travel here", !cur, 13f, !cur && !LotManager.Travelling))
                    {
                        Open = false;
                        LotManager.Instance.Travel(lot);
                    }
                }
                cy += chH + 8f;
            }
            Ui.Label(new Rect(cx, Box.yMax - 40f, 320f, 32f), "Empty lots can be built on with the Build tool (V). The home lot can only be painted.", 11f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);

            if (Event.current.type == EventType.MouseDown && !Box.Contains(Ui.Mouse)) { Open = false; Event.current.Use(); }
        }
    }
}
