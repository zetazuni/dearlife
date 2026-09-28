using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Colour swatches for furniture, like the Sims 4: a small floating card with a row of colours for what is soft or painted and a row
    /// for wood and stone. Opened from the round menu ("Change colour"). The same rows are used in the shop.
    /// </summary>
    public class ColourPicker : MonoBehaviour
    {
        public static ColourPicker Instance { get; private set; }
        public static Rect Box;
        static Furniture target;
        static Vector2 at;

        void Awake() { Instance = this; }

        public static bool IsOpen => target != null;

        public static void Open(Furniture f, Vector2 screenPx)
        {
            if (f == null || (!f.HasChannel(0) && !f.HasChannel(1))) { Household.Toast("That cannot change colour."); GameAudio.Play(GameAudio.Sfx.No); return; }
            target = f; at = screenPx;
            GameAudio.Play(GameAudio.Sfx.Click);
        }

        public static void Close() { target = null; Box = Rect.zero; }

        void Update() { if (target != null && (Input.GetKeyDown(KeyCode.Escape) || !target)) Close(); }

        /// <summary>A row of round swatches. Returns the hex that was clicked ("" for "as it came"), or null.</summary>
        public static string Row(float x, float y, float width, string[] palette, string current, float size = 24f)
        {
            string picked = null;
            float gap = 6f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (size + gap)));
            for (int i = 0; i < palette.Length; i++)
            {
                var r = new Rect(x + (i % perRow) * (size + gap), y + (i / perRow) * (size + gap), size, size);
                Color c = Ui.Hex(palette[i]);
                bool sel = string.Equals(current, palette[i], System.StringComparison.OrdinalIgnoreCase);
                bool hover = Ui.Hover(r);
                if (sel || hover) Ui.Ring(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), sel ? Ui.Accent : Ui.Pink, 2.5f, size);
                Ui.Round(r, c, size);
                Ui.Ring(r, new Color(0f, 0f, 0f, 0.18f), 1.5f, size);
                if (GUI.Button(Ui.S(r), GUIContent.none, GUIStyle.none)) { GameAudio.Play(GameAudio.Sfx.Click); picked = palette[i]; }
            }
            return picked;
        }

        public static float RowHeight(int count, float width, float size = 24f)
        {
            float gap = 6f;
            int perRow = Mathf.Max(1, Mathf.FloorToInt((width + gap) / (size + gap)));
            return Mathf.Ceil(count / (float)perRow) * (size + gap);
        }

        void OnGUI()
        {
            if (target == null) { Box = Rect.zero; return; }
            if (!target) { Close(); return; }
            Ui.Begin();
            bool a = target.HasChannel(0), b = target.HasChannel(1);
            const float w = 292f;
            float h = 50f + (a ? 30f + RowHeight(Furniture.Fabrics.Length, w - 28f) : 0f) + (b ? 30f + RowHeight(Furniture.Woods.Length, w - 28f) : 0f) + 10f;
            var p = Ui.ToUi(at);
            Box = new Rect(Mathf.Clamp(p.x + 24f, 8f, Ui.W - w - 8f), Mathf.Clamp(p.y - h * 0.5f, 64f, Ui.H - h - 8f), w, h);
            Ui.Box(Box, Ui.Card, Ui.Pink, 18f, 2f, true);
            Ui.Label(new Rect(Box.x + 14f, Box.y + 10f, w - 60f, 24f), target.Label, 16f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            if (Ui.Square(new Rect(Box.xMax - 44f, Box.y + 8f, 30f, 28f), "X", false, 12f)) { Close(); return; }
            float y = Box.y + 44f;
            var dec = DecorateMode.Instance;
            if (a)
            {
                HouseHud.Kicker(Box.x + 14f, y, w, "Fabric and paint");
                if (Ui.Chip(new Rect(Box.xMax - 84f, y - 4f, 70f, 22f), "As it came", string.IsNullOrEmpty(target.tintA), 10f)) dec.SetTint(target, 0, "");
                y += 24f;
                var pick = Row(Box.x + 14f, y, w - 28f, Furniture.Fabrics, target.tintA);
                if (pick != null) dec.SetTint(target, 0, pick);
                y += RowHeight(Furniture.Fabrics.Length, w - 28f) + 6f;
            }
            if (b)
            {
                HouseHud.Kicker(Box.x + 14f, y, w, "Wood and stone");
                if (Ui.Chip(new Rect(Box.xMax - 84f, y - 4f, 70f, 22f), "As it came", string.IsNullOrEmpty(target.tintB), 10f)) dec.SetTint(target, 1, "");
                y += 24f;
                var pick = Row(Box.x + 14f, y, w - 28f, Furniture.Woods, target.tintB);
                if (pick != null) dec.SetTint(target, 1, pick);
            }
            if (Event.current.type == EventType.MouseDown && !Box.Contains(Ui.Mouse)) Close();
        }
    }
}
