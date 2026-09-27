using UnityEngine;

namespace Tiramisu
{
    /// <summary>The opening of the 2D game: a dark indigo screen, "Tiramisu" in script that fades in, a rose to violet to cyan rule that draws itself, then "by Zetazuni". A click skips it.</summary>
    public class Splash : MonoBehaviour
    {
        const float Total = 4.6f;
        float t0;
        bool done;

        static Splash inst;

        void Awake() { inst = this; }

        void Start() { t0 = Time.unscaledTime; }

        void OnGUI()
        {
            if (done) return;
            float t = Time.unscaledTime - t0;
            if (t > Total || (Event.current.type == EventType.MouseDown && t > 0.4f)) { done = true; return; }
            GUI.depth = -950;        // over the loading screen, which waits underneath
            Ui.Begin();
            float fadeOut = 1f - Mathf.Clamp01((t - (Total - 0.7f)) / 0.7f);
            float w = Ui.W, h = Ui.H;
            var full = new Rect(0f, 0f, w, h);
            Ui.Rect2(full, new Color(0.086f, 0.067f, 0.19f, fadeOut));

            float a = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 1.6f));
            float rise = (1f - a) * 14f;
            var title = new Rect(0f, h * 0.5f - 96f + rise, w, 110f);
            Ui.Label(new Rect(title.x + 3f, title.y + 3f, title.width, title.height), "Tiramisu", 96f, new Color(0.9f, 0.4f, 0.6f, 0.18f * a * fadeOut), TextAnchor.MiddleCenter, Ui.Weight.Script);
            Ui.Label(title, "Tiramisu", 96f, new Color(1f, 0.93f, 0.95f, a * fadeOut), TextAnchor.MiddleCenter, Ui.Weight.Script);

            float ruleK = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 1.3f) / 1.1f));
            float rw = 260f * ruleK, rx = w * 0.5f - rw * 0.5f, ry = h * 0.5f + 32f;
            var cols = new[] { Ui.Hex("ff8fc8"), Ui.Hex("a78bfa"), Ui.Hex("57e6ff") };
            for (int i = 0; i < 3; i++) { var c = cols[i]; c.a = fadeOut; Ui.Round(new Rect(rx + rw / 3f * i, ry, rw / 3f + 1f, 3f), c, 1.5f); }

            float b = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 2.2f) / 1f));
            Ui.Label(new Rect(0f, ry + 14f, w, 30f), "by Zetazuni", 20f, new Color(0.85f, 0.8f, 0.95f, b * fadeOut), TextAnchor.UpperCenter, Ui.Weight.Bold);
            Ui.Label(new Rect(0f, h - 40f, w, 20f), "click to skip", 12f, new Color(0.7f, 0.65f, 0.85f, 0.55f * Mathf.Clamp01((t - 1f)) * fadeOut), TextAnchor.UpperCenter, Ui.Weight.Bold);
            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) Event.current.Use();
        }

        /// <summary>The intro covers everything, so nothing else should react to clicks until it has gone.</summary>
        public static bool Showing
        {
            get { return inst != null && !inst.done && Time.unscaledTime - inst.t0 <= Total; }
        }
    }
}
