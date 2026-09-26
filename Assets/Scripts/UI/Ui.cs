using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The look of the 2D Tiramisu App, for every panel of the game: warm cream cards, soft pink borders, round pills, Nunito for the words
    /// and Great Vibes for the title. Everything is drawn with rounded rectangles (no texture files), and all rectangles you pass in are in
    /// "ui units" (the screen is 900 units high at the normal size), so text stays sharp at any resolution.
    /// </summary>
    public static class Ui
    {
        // the palette of the 2D game (its :root variables)
        public static readonly Color Ink = Hex("5b4636"), Soft = Hex("8c7462"), Card = Hex("fff8ef"), Line = Hex("f0dcc6");
        public static readonly Color Pink = Hex("f4a6b7"), Accent = Hex("e98aa0"), Gold = Hex("f2b84b"), GoldText = Hex("c98a17");
        public static readonly Color Rose = Hex("b5566e"), Cream2 = Hex("fdeedd"), Pale = Hex("ffeef2"), Paper = Hex("fffaf4");
        public static readonly Color Green = Hex("bfe3c4"), GreenPale = Hex("f4fbf3"), Night = Hex("171130");
        public static readonly Color White = Color.white;

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
        public static Color A(this Color c, float a) { c.a = a; return c; }

        public static float Scale { get; private set; } = 1f;
        public static float W { get; private set; }
        public static float H { get; private set; }

        static Font body, bold, xbold, script;
        static GUIStyle text;
        static Texture2D white;

        /// <summary>Call first in every OnGUI. Works out the scale and loads the fonts.</summary>
        public static void Begin()
        {
            if (text == null)
            {
                body = Resources.Load<Font>("Fonts/Nunito-Regular");
                bold = Resources.Load<Font>("Fonts/Nunito-Bold");
                xbold = Resources.Load<Font>("Fonts/Nunito-ExtraBold");
                script = Resources.Load<Font>("Fonts/GreatVibes-Regular");
                white = Texture2D.whiteTexture;
                text = new GUIStyle(GUI.skin.label) { richText = false, clipping = TextClipping.Overflow, padding = new RectOffset(0, 0, 0, 0), margin = new RectOffset(0, 0, 0, 0) };
            }
            Scale = Mathf.Max(0.85f, Screen.height / 900f);
            W = Screen.width / Scale; H = Screen.height / Scale;
        }

        public static Rect S(Rect r) => new Rect(r.x * Scale, r.y * Scale, r.width * Scale, r.height * Scale);
        public static Vector2 Mouse => Event.current.mousePosition / Scale;
        public static bool Hover(Rect r) => r.Contains(Mouse);
        public static Vector2 ToUi(Vector2 screen) => screen / Scale;

        // ------------------------------------------------------------ shapes

        public static void Round(Rect r, Color fill, float radius)
        {
            if (Event.current.type != EventType.Repaint || fill.a <= 0f) return;
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f) * Scale;
            GUI.DrawTexture(S(r), white, ScaleMode.StretchToFill, true, 0f, fill, Vector4.zero, new Vector4(rad, rad, rad, rad));
        }

        public static void Ring(Rect r, Color c, float width, float radius)
        {
            if (Event.current.type != EventType.Repaint || c.a <= 0f) return;
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f) * Scale, bw = width * Scale;
            GUI.DrawTexture(S(r), white, ScaleMode.StretchToFill, true, 0f, c, new Vector4(bw, bw, bw, bw), new Vector4(rad, rad, rad, rad));
        }

        /// <summary>A card: a fill, a border, and optionally a soft shadow under it.</summary>
        public static void Box(Rect r, Color fill, Color border, float radius = 14f, float borderWidth = 2f, bool shadow = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            if (shadow)
            {
                Round(new Rect(r.x - 1f, r.y + 5f, r.width + 2f, r.height), new Color(0.47f, 0.23f, 0.16f, 0.07f), radius + 2f);
                Round(new Rect(r.x, r.y + 3f, r.width, r.height), new Color(0.47f, 0.23f, 0.16f, 0.09f), radius);
            }
            Round(r, fill, radius);
            if (borderWidth > 0f) Ring(r, border, borderWidth, radius);
        }

        public static void Rect2(Rect r, Color c) { if (Event.current.type == EventType.Repaint) GUI.DrawTexture(S(r), white, ScaleMode.StretchToFill, true, 0f, c, 0f, 0f); }

        // ------------------------------------------------------------ text

        public enum Weight { Regular, Bold, ExtraBold, Script }

        public static void Label(Rect r, string s, float size = 14f, Color? colour = null, TextAnchor anchor = TextAnchor.UpperLeft, Weight w = Weight.Bold, bool wrap = false)
        {
            if (string.IsNullOrEmpty(s) || Event.current.type != EventType.Repaint) return;
            text.font = w == Weight.Regular ? body : w == Weight.Bold ? bold : w == Weight.Script ? script : xbold;
            text.fontSize = Mathf.RoundToInt(size * Scale);
            text.alignment = anchor;
            text.wordWrap = wrap;
            text.normal.textColor = colour ?? Ink;
            GUI.Label(S(r), s, text);
        }

        public static float TextWidth(string s, float size, Weight w = Weight.Bold)
        {
            text.font = w == Weight.Regular ? body : w == Weight.Bold ? bold : w == Weight.Script ? script : xbold;
            text.fontSize = Mathf.RoundToInt(size * Scale);
            text.wordWrap = false;
            return text.CalcSize(new GUIContent(s)).x / Scale;
        }

        public static float TextHeight(string s, float size, float width, Weight w = Weight.Bold)
        {
            text.font = w == Weight.Regular ? body : w == Weight.Bold ? bold : w == Weight.Script ? script : xbold;
            text.fontSize = Mathf.RoundToInt(size * Scale);
            text.wordWrap = true;
            return text.CalcHeight(new GUIContent(s), width * Scale) / Scale;
        }

        /// <summary>A small cream tag centred on a point given in screen pixels (top left origin): names over heads, labels on held pieces.</summary>
        public static void Tag(Vector2 screenPx, string label, float size = 12f, bool accent = false)
        {
            if (string.IsNullOrEmpty(label)) return;
            var c = screenPx / Scale;
            float tw = TextWidth(label, size, Weight.ExtraBold) + 18f;
            var r = new Rect(c.x - tw * 0.5f, c.y - 11f, tw, 22f);
            Box(r, accent ? Accent : Card.A(0.94f), accent ? Accent : Line, 11f, accent ? 0f : 2f, false);
            Label(r, label, size, accent ? White : Ink, TextAnchor.MiddleCenter, Weight.ExtraBold);
        }

        /// <summary>A speech bubble like a chat message in the 2D game.</summary>
        public static void Bubble(Vector2 screenPx, string label)
        {
            var c = screenPx / Scale;
            float tw = Mathf.Min(220f, TextWidth(label, 13f) + 26f);
            float th = TextHeight(label, 13f, tw - 20f) + 14f;
            var r = new Rect(c.x - tw * 0.5f, c.y - th, tw, th);
            Box(r, Hex("fffaf2"), Pink, 14f, 2f, true);
            Label(new Rect(r.x + 10f, r.y + 7f, tw - 20f, th - 10f), label, 13f, Ink, TextAnchor.UpperCenter, Weight.Bold, true);
        }

        // ------------------------------------------------------------ controls

        static bool Clicked(Rect r)
        {
            bool hit = GUI.Button(S(r), GUIContent.none, GUIStyle.none);
            if (hit) GameAudio.Play(GameAudio.Sfx.Click);
            return hit;
        }

        /// <summary>The round "pill" button of the 2D game: white with a beige border, pink on hover, filled rose when it is on.</summary>
        public static bool Pill(Rect r, string label, bool on = false, float size = 14f, bool enabled = true)
        {
            bool hover = enabled && Hover(r);
            Color fill = on ? Accent : hover ? Pale : White;
            Color border = on ? Accent : hover ? Pink : Line;
            if (!enabled) { fill = Cream2; border = Line; }
            Round(new Rect(r.x, r.y, r.width, r.height), fill, r.height);
            Ring(r, on ? new Color(1f, 1f, 1f, 0.4f) : border, 2f, r.height);
            Label(r, label, size, on ? White : enabled ? Ink : Soft, TextAnchor.MiddleCenter, Weight.ExtraBold);
            return enabled && Clicked(r);
        }

        /// <summary>A smaller rounded chip, used for categories and options.</summary>
        public static bool Chip(Rect r, string label, bool on = false, float size = 12f)
        {
            bool hover = Hover(r);
            Round(r, on ? Accent : hover ? Pale : White, 12f);
            Ring(r, on ? Accent : hover ? Pink : Line, 2f, 12f);
            Label(r, label, size, on ? White : Ink, TextAnchor.MiddleCenter, Weight.ExtraBold);
            return Clicked(r);
        }

        /// <summary>A square-ish icon button (the camera column of the 2D game).</summary>
        public static bool Square(Rect r, string label, bool on = false, float size = 13f)
        {
            bool hover = Hover(r);
            Round(r, on ? Pink : hover ? Pale : Card.A(0.95f), 12f);
            Ring(r, on ? Pink : hover ? Pink : Line, 2f, 12f);
            Label(r, label, size, on ? White : Ink, TextAnchor.MiddleCenter, Weight.ExtraBold);
            return Clicked(r);
        }

        /// <summary>A card you can press (shop items, people).</summary>
        public static bool CardButton(Rect r, bool on = false, bool enabled = true)
        {
            bool hover = enabled && Hover(r);
            Round(new Rect(r.x, r.y + (hover ? -2f : 0f), r.width, r.height), on ? Pale : Paper, 16f);
            Ring(new Rect(r.x, r.y + (hover ? -2f : 0f), r.width, r.height), on || hover ? Pink : Line, 2f, 16f);
            return enabled && Clicked(r);
        }

        public static void Bar(Rect r, float value01, Color fill, Color? back = null)
        {
            Round(r, back ?? Line, r.height);
            float w = Mathf.Max(r.height, r.width * Mathf.Clamp01(value01));
            if (value01 > 0.005f) Round(new Rect(r.x, r.y, w, r.height), fill, r.height);
        }

        static int slider;

        /// <summary>A slider like the 2D game's range inputs: a thin track and a round handle.</summary>
        public static float Slider(Rect r, float value, float min = 0f, float max = 1f)
        {
            int id = GUIUtility.GetControlID(FocusType.Passive, S(r));
            var e = Event.current;
            var track = new Rect(r.x + 8f, r.center.y - 3f, r.width - 16f, 6f);
            float t = Mathf.InverseLerp(min, max, value);
            switch (e.type)
            {
                case EventType.MouseDown:
                    if (S(r).Contains(e.mousePosition)) { GUIUtility.hotControl = id; slider = id; e.Use(); goto case EventType.MouseDrag; }
                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        t = Mathf.Clamp01((e.mousePosition.x / Scale - track.x) / track.width);
                        value = Mathf.Lerp(min, max, t);
                        GUI.changed = true; e.Use();
                    }
                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id) { GUIUtility.hotControl = 0; e.Use(); }
                    break;
            }
            Round(track, Line, 3f);
            Round(new Rect(track.x, track.y, track.width * t, track.height), Pink, 3f);
            var knob = new Rect(track.x + track.width * t - 9f, r.center.y - 9f, 18f, 18f);
            Round(new Rect(knob.x, knob.y + 2f, 18f, 18f), new Color(0.4f, 0.2f, 0.1f, 0.12f), 9f);
            Round(knob, White, 9f);
            Ring(knob, Accent, 3f, 9f);
            return value;
        }

        /// <summary>A scrolling area: draws with the mouse wheel and a thin bar. The callback gets the width to draw in.</summary>
        public static float Scroll(Rect view, float pos, float contentHeight, System.Action<float> draw)
        {
            float max = Mathf.Max(0f, contentHeight - view.height);
            var e = Event.current;
            if (e.type == EventType.ScrollWheel && S(view).Contains(e.mousePosition)) { pos += e.delta.y * 36f; e.Use(); }
            pos = Mathf.Clamp(pos, 0f, max);
            bool bar = max > 0f;
            float w = view.width - (bar ? 10f : 0f);
            GUI.BeginGroup(S(view));
            GUI.BeginGroup(new Rect(0f, -pos * Scale, w * Scale, Mathf.Max(contentHeight, view.height) * Scale));
            draw(w);
            GUI.EndGroup();
            GUI.EndGroup();
            if (bar)
            {
                var track = new Rect(view.xMax - 6f, view.y, 5f, view.height);
                Round(track, Line.A(0.5f), 3f);
                float th = Mathf.Max(28f, view.height * view.height / contentHeight);
                Round(new Rect(track.x, track.y + (view.height - th) * (pos / max), 5f, th), Soft.A(0.7f), 3f);
            }
            return pos;
        }
    }
}
