using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The loading screen, in the way of the Sims 4: a soft full-screen picture with the name of the place, a bar that really fills, and a tip.
    /// It covers the start of the game (until the walkable ground and the music are ready) and every trip to another lot.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        public static bool Showing => inst != null && inst.alpha > 0.001f;
        /// <summary>True while it is solid enough that nothing behind it should react to the mouse.</summary>
        public static bool Blocking => inst != null && (inst.wanted || inst.alpha > 0.3f);

        static LoadingScreen inst;

        static readonly string[] Tips =
        {
            "Press M to open the map and visit an empty lot.",
            "Press P to move and decorate. Click a piece to pick it up, click again to put it down.",
            "Ctrl+Z and Ctrl+Y undo and redo in decorate and build mode.",
            "The house came ready built, so only its paint can change. Build your own on a new lot.",
            "Right-click a person to see what they can do.",
            "Everyone keeps their rank in each career, even if they change jobs.",
            "Press V to build. Rooms, pools, stairs and roofs all have their own tool.",
            "Press H to hide the side panel and see more of the world.",
            "Press G to change the picture quality if the game feels slow.",
            "A dog from the pet shop needs feeding and playing with, just like a cat.",
            "Pieces you sell come back at a fraction of the price. Undo gives it all back.",
        };

        bool wanted, startup = true;
        float alpha = 1f, progress, shownAt, tipAt, splashEnd = -1f;
        string title = "Tiramisu", sub = "Getting the house ready";
        int tip;

        void Awake() { inst = this; tip = Random.Range(0, Tips.Length); }

        /// <summary>Show it for a trip or a load. Call End when done.</summary>
        public static void Begin(string title, string sub)
        {
            if (!inst) return;
            inst.title = title; inst.sub = sub; inst.progress = 0f;
            inst.wanted = true; inst.shownAt = Time.unscaledTime;
            inst.tip = (inst.tip + 1 + Random.Range(0, Tips.Length - 1)) % Tips.Length;
        }

        public static void Progress(float p) { if (inst) inst.progress = Mathf.Max(inst.progress, Mathf.Clamp01(p)); }
        public static void End() { if (inst) { inst.progress = 1f; inst.wanted = false; } }

        void Update()
        {
            if (startup)
            {
                // the opening: waits for the intro, the walkable ground, and the music
                float ground = TiramisuNav.Ready ? 1f : 0f;
                var ga = GameAudio.Instance;
                float music = ga == null || ga.MusicReady ? 1f : 0f;
                if (!Splash.Showing && splashEnd < 0f) splashEnd = Time.unscaledTime;
                float held = splashEnd < 0f ? 0f : Mathf.Clamp01((Time.unscaledTime - splashEnd) / 1.6f);
                progress = Mathf.Max(progress, Mathf.Min(0.9f, 0.1f + 0.4f * ground + 0.3f * music + 0.1f * held));
                if (ground > 0f && music > 0f && held >= 1f) { progress = 1f; startup = false; wanted = false; }
                else wanted = true;
            }
            float target = wanted ? 1f : 0f;
            alpha = Mathf.MoveTowards(alpha, target, Time.unscaledDeltaTime / (wanted ? 0.25f : 0.6f));
            if (Time.unscaledTime - tipAt > 5f) { tipAt = Time.unscaledTime; if (Showing) tip = (tip + 1) % Tips.Length; }
        }

        void OnGUI()
        {
            if (alpha <= 0.001f) return;
            GUI.depth = -900;        // over the game, under the intro
            Ui.Begin();
            float w = Ui.W, h = Ui.H, a = alpha;
            Ui.Rect2(new Rect(0f, 0f, w, h), new Color(0.086f, 0.067f, 0.19f, a));
            // a soft glow behind the name, the colours of the rule under the 2D title
            var glow = new Rect(w * 0.5f - 360f, h * 0.5f - 250f, 720f, 420f);
            Ui.Round(glow, new Color(0.65f, 0.4f, 0.9f, 0.045f * a), 210f);

            float t = Time.unscaledTime;
            Ui.Label(new Rect(0f, h * 0.5f - 140f, w, 110f), title, 86f, new Color(1f, 0.93f, 0.95f, a), TextAnchor.MiddleCenter, Ui.Weight.Script);
            Ui.Label(new Rect(0f, h * 0.5f - 30f, w, 30f), sub, 20f, new Color(0.85f, 0.8f, 0.95f, a), TextAnchor.MiddleCenter, Ui.Weight.Bold);

            // the bar
            float bw = Mathf.Min(460f, w - 80f), bx = w * 0.5f - bw * 0.5f, by = h * 0.5f + 22f;
            Ui.Round(new Rect(bx, by, bw, 8f), new Color(1f, 1f, 1f, 0.14f * a), 4f);
            float fill = Mathf.Max(0.02f, progress) * bw;
            var cols = new[] { Ui.Hex("ff8fc8"), Ui.Hex("a78bfa"), Ui.Hex("57e6ff") };
            for (int i = 0; i < 3; i++)
            {
                float x0 = bx + bw / 3f * i, x1 = Mathf.Min(bx + fill, x0 + bw / 3f + 1f);
                if (x1 <= x0) continue;
                var c = cols[i]; c.a = a;
                Ui.Round(new Rect(x0, by, x1 - x0, 8f), c, 4f);
            }
            // three small dots that pulse, so it is clear it is still working
            for (int i = 0; i < 3; i++)
            {
                float k = 0.5f + 0.5f * Mathf.Sin(t * 4f - i * 0.8f);
                Ui.Round(new Rect(w * 0.5f - 22f + i * 16f, by + 26f, 10f, 10f), new Color(1f, 0.85f, 0.95f, (0.25f + 0.6f * k) * a), 5f);
            }

            // the tip
            Ui.Label(new Rect(0f, h - 120f, w, 20f), "DID YOU KNOW", 12f, new Color(0.9f, 0.55f, 0.75f, a), TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(w * 0.5f - 340f, h - 96f, 680f, 50f), Tips[tip], 17f, new Color(0.9f, 0.86f, 0.97f, a), TextAnchor.UpperCenter, Ui.Weight.Bold, true);
            Ui.Label(new Rect(0f, h - 30f, w, 18f), $"Tiramisu 3D v{GameInfo.Version}  ·  by Zetazuni", 12f, new Color(0.7f, 0.65f, 0.85f, 0.6f * a), TextAnchor.MiddleCenter, Ui.Weight.Bold);

            if (Event.current.type == EventType.MouseDown || Event.current.type == EventType.MouseUp) Event.current.Use();
        }
    }
}
