using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The look of every panel of the game (v0.51.0): clean see through glass over the scene, like inZOI. Smoked glass with
    /// white words in the dark mode, frosted white glass with dark words in the light one; thin soft borders, round pills,
    /// whatever is switched on filled solid. Nunito for the words and Great Vibes for the title. Everything is drawn with
    /// rounded rectangles (no texture files), and all rectangles you pass in are in "ui units" (the screen is 900 units high
    /// at the normal size), so text stays sharp at any resolution. The palette colours carry their own transparency, and
    /// <see cref="A"/> multiplies it, so a panel asked for at 0.96 stays glass.
    /// </summary>
    public static class Ui
    {
        // the palette of the 2D game (its :root variables), and its dark mode: a neon pink and ice blue night version
        public static bool Dark { get; private set; }
        public static void SetDark(bool on) { Dark = on; PlayerPrefs.SetInt("dearlife.dark", on ? 1 : 0); }
        /// <summary>Whether name tags show above people and pets. Always off on the title screen regardless of this.</summary>
        public static bool ShowNames { get; private set; } = true;
        public static void SetShowNames(bool on) { ShowNames = on; PlayerPrefs.SetInt("dearlife.showNames", on ? 1 : 0); }
        static Color G(float r, float g, float b, float a) => new Color(r, g, b, a);
        static readonly Color inkL = Hex("1b1e24"), inkD = G(1f, 1f, 1f, 0.96f), softL = G(0.1f, 0.12f, 0.15f, 0.62f), softD = G(1f, 1f, 1f, 0.62f);
        static readonly Color cardL = G(1f, 1f, 1f, 0.86f), cardD = G(0.07f, 0.08f, 0.10f, 0.8f), panelL = G(1f, 1f, 1f, 0.62f), panelD = G(0.06f, 0.07f, 0.09f, 0.55f);
        static readonly Color lineL = G(0f, 0f, 0f, 0.12f), lineD = G(1f, 1f, 1f, 0.16f), pinkL = G(0f, 0f, 0f, 0.4f), pinkD = G(1f, 1f, 1f, 0.6f);
        static readonly Color accL = Hex("2f6fd0"), accD = Hex("9fd0ff"), goldL = Hex("f2b84b"), goldD = Hex("ffd98a"), goldTL = Hex("a8730d"), goldTD = Hex("ffd98a");
        static readonly Color roseL = Hex("c2334d"), roseD = Hex("ff9aa8"), cream2L = G(1f, 1f, 1f, 0.3f), cream2D = G(1f, 1f, 1f, 0.04f);
        static readonly Color paleL = G(1f, 1f, 1f, 0.9f), paleD = G(1f, 1f, 1f, 0.22f), paperL = G(1f, 1f, 1f, 0.45f), paperD = G(1f, 1f, 1f, 0.07f);
        static readonly Color surfL = G(1f, 1f, 1f, 0.6f), surfD = G(1f, 1f, 1f, 0.10f), onL = Color.white, onD = Hex("14171c");
        static readonly Color fillL = G(0.1f, 0.12f, 0.15f, 0.9f), fillD = G(1f, 1f, 1f, 0.93f);
        public static Color Ink => Dark ? inkD : inkL;
        public static Color Soft => Dark ? softD : softL;
        /// <summary>The denser glass of windows and panels full of words (the status window, the side panel); the small
        /// floating pieces (the people bar, the needs) use the lighter <see cref="Panel"/>.</summary>
        public static Color Card => Dark ? cardD : cardL;
        public static Color Line => Dark ? lineD : lineL;
        public static Color Pink => Dark ? pinkD : pinkL;
        /// <summary>The colour of small highlights: a clear blue.</summary>
        public static Color Accent => Dark ? accD : accL;
        public static Color Gold => Dark ? goldD : goldL;
        public static Color GoldText => Dark ? goldTD : goldTL;
        public static Color Rose => Dark ? roseD : roseL;
        public static Color Cream2 => Dark ? cream2D : cream2L;
        public static Color Pale => Dark ? paleD : paleL;
        public static Color Paper => Dark ? paperD : paperL;
        /// <summary>What buttons and pills are filled with (white by day).</summary>
        public static Color Surface => Dark ? surfD : surfL;
        /// <summary>Text on a switched-on button.</summary>
        public static Color OnAccent => Dark ? onD : onL;
        /// <summary>The white of the big panel cards.</summary>
        public static Color Panel => Dark ? panelD : panelL;
        public static Color White => Color.white;
        public static readonly Color Green = Hex("bfe3c4"), GreenPale = Hex("f4fbf3");
        /// <summary>The smoked glass of the title screen's side, whichever mode is on.</summary>
        public static readonly Color Night = new Color(0.04f, 0.05f, 0.07f, 0.7f);

        public static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
        /// <summary>The colour more see through by this much (the palette's glass keeps its own transparency).</summary>
        public static Color A(this Color c, float a) { c.a *= a; return c; }

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
                Dark = PlayerPrefs.GetInt("dearlife.dark", 0) == 1;
                ShowNames = PlayerPrefs.GetInt("dearlife.showNames", 1) == 1;
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
            // glass has hairline borders: whatever width is asked for, the line stays thin
            float rad = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f) * Scale, bw = Mathf.Max(1f, Mathf.Min(width, 1.25f) * Scale);
            GUI.DrawTexture(S(r), white, ScaleMode.StretchToFill, true, 0f, c, new Vector4(bw, bw, bw, bw), new Vector4(rad, rad, rad, rad));
        }

        /// <summary>The fill of anything switched on: solid white on the smoked glass, solid dark on the frosted one.</summary>
        public static void OnFill(Rect r, float radius) => Round(r, Dark ? fillD : fillL, radius);

        /// <summary>A round picture (a face in the people bar), or a coloured disc with a letter while there is none.</summary>
        public static void Portrait(Rect r, Texture picture, string letter, Color disc, Color ring, float ringWidth = 2f)
        {
            if (Event.current.type != EventType.Repaint) return;
            float rad = r.height * 0.5f * Scale;
            if (picture) GUI.DrawTexture(S(r), picture, ScaleMode.ScaleAndCrop, false, 0f, Color.white, Vector4.zero, new Vector4(rad, rad, rad, rad));
            else
            {
                Round(r, disc, r.height);
                Label(r, letter, r.height * 0.42f, Color.white, TextAnchor.MiddleCenter, Weight.ExtraBold);
            }
            float bw = ringWidth * Scale;
            GUI.DrawTexture(S(r), white, ScaleMode.StretchToFill, true, 0f, ring, new Vector4(bw, bw, bw, bw), new Vector4(rad, rad, rad, rad));
        }

        /// <summary>A round gauge that fills from the bottom (a need in the people bar).</summary>
        public static void Gauge(Rect r, float value01, Color fill)
        {
            if (Event.current.type != EventType.Repaint) return;
            Round(r, Surface, r.height);
            float v = Mathf.Clamp01(value01), rad = r.height * 0.5f * Scale;
            if (v > 0.01f)
            {
                float cut = r.height * (1f - v);
                GUI.BeginGroup(S(new Rect(r.x, r.y + cut, r.width, r.height - cut)));
                GUI.DrawTexture(new Rect(0f, -cut * Scale, r.width * Scale, r.height * Scale), white, ScaleMode.StretchToFill, true, 0f, fill, Vector4.zero, new Vector4(rad, rad, rad, rad));
                GUI.EndGroup();
            }
            Ring(r, Line, 1f, r.height);
        }

        /// <summary>A card: a fill, a border, and optionally a soft shadow under it.</summary>
        public static void Box(Rect r, Color fill, Color border, float radius = 14f, float borderWidth = 2f, bool shadow = false)
        {
            if (Event.current.type != EventType.Repaint) return;
            // a shadow outside the card only: under see through glass it would darken the glass itself
            if (shadow) Ring(new Rect(r.x - 2f, r.y - 1f, r.width + 4f, r.height + 5f), new Color(0f, 0f, 0f, Dark ? 0.16f : 0.07f), 1.25f, radius + 2f);
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
            if (accent) OnFill(r, 11f); else Box(r, Card.A(0.94f), Line, 11f, 2f, false);
            Label(r, label, size, accent ? OnAccent : Ink, TextAnchor.MiddleCenter, Weight.ExtraBold);
        }

        /// <summary>A speech bubble like a chat message in the 2D game.</summary>
        public static void Bubble(Vector2 screenPx, string label)
        {
            var c = screenPx / Scale;
            float tw = Mathf.Min(220f, TextWidth(label, 13f) + 26f);
            float th = TextHeight(label, 13f, tw - 20f) + 14f;
            var r = new Rect(c.x - tw * 0.5f, c.y - th, tw, th);
            Box(r, Card, Pink, 14f, 2f, true);      // the card colour switches with dark mode, like the Ink text on it
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
            Color fill = hover ? Pale : Surface;
            Color border = hover ? Pink : Line;
            if (!enabled) { fill = Cream2; border = Line; }
            if (on) OnFill(r, r.height); else Round(r, fill, r.height);
            Ring(r, on ? new Color(1f, 1f, 1f, 0.4f) : border, 2f, r.height);
            Label(r, label, size, on ? OnAccent : enabled ? Ink : Soft, TextAnchor.MiddleCenter, Weight.ExtraBold);
            return enabled && Clicked(r);
        }

        /// <summary>A smaller rounded chip, used for categories and options.</summary>
        public static bool Chip(Rect r, string label, bool on = false, float size = 12f)
        {
            bool hover = Hover(r);
            if (on) OnFill(r, 12f); else Round(r, hover ? Pale : Surface, 12f);
            Ring(r, on ? new Color(1f, 1f, 1f, 0.4f) : hover ? Pink : Line, 2f, 12f);
            Label(r, label, size, on ? OnAccent : Ink, TextAnchor.MiddleCenter, Weight.ExtraBold);
            return Clicked(r);
        }

        /// <summary>A square-ish icon button (the camera column of the 2D game).</summary>
        public static bool Square(Rect r, string label, bool on = false, float size = 13f)
        {
            bool hover = Hover(r);
            if (on) OnFill(r, 12f); else Round(r, hover ? Pale : Card.A(0.95f), 12f);
            Ring(r, on || hover ? Pink : Line, 2f, 12f);
            Label(r, label, size, on ? OnAccent : Ink, TextAnchor.MiddleCenter, Weight.ExtraBold);
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
            Round(knob, Dark ? fillD : fillL, 9f);
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
