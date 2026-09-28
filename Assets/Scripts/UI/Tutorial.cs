using UnityEngine;

namespace Dearlife
{
    /// <summary>A few warm hints for the first minutes, as a card like the 2D game's guide (F1 shows them again). Skipping is remembered.</summary>
    public class Tutorial : MonoBehaviour
    {
        static string Key => SaveSystem.Key("dearlife.tutorial");
        static Tutorial instance;

        static readonly (string title, string text)[] Steps =
        {
            ("Welcome home", "Click someone on the Family tab (or in the house) to play as them. The little ring over their head shows who you are playing."),
            ("Walk and do things", "Click the floor and they walk there. Click furniture, a person or the pet, or right click, for a round menu of things to do: sit, cook, swim, work out, watch TV, chat."),
            ("Needs and mood", "The bars at the bottom left are their needs: hunger, bathroom, energy, fun, friends and hygiene. Keep them happy. Press C for the full status: skills, wishes, friends."),
            ("Money", "The money at the top pays for things and comes from work (the desks), wishes that come true and selling. Bills come every few days, and nothing bad ever happens if you cannot pay them yet."),
            ("Shop and build", "B opens the shop, V is build mode (the home can only be painted, so open the map with M and go to an empty lot to build your own house, pool and stairs), P moves furniture. Space pauses, 1 2 3 change the speed, the buttons on the left change floors."),
            ("Seasons and sound", "The seasons change as time passes (or pick one in Settings). F2 mutes the sound, N turns the music off. F1 shows these hints again. Have a lovely time together!"),
        };

        int step;
        bool open;
        float swap;
        Rect box;

        void Awake() { instance = this; }

        void Start() { open = PlayerPrefs.GetInt(Key, 0) == 0; }

        public static void Restart() { if (instance) { instance.open = true; instance.step = 0; instance.swap = 0f; } }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.F1)) Restart();
            if (swap > 0f) swap = Mathf.Max(0f, swap - Time.unscaledDeltaTime * 5f);
        }

        void OnGUI()
        {
            if (!open || Splash.Showing || MainMenu.Busy) { box = Rect.zero; return; }
            Ui.Begin();
            float w = Ui.W;
            float bw = 360f, bh = 190f;
            float cx = (w - (HouseHud.PanelOpen ? 350f : 0f)) * 0.5f;
            box = new Rect(cx - bw * 0.5f, Ui.H - bh - 60f, bw, bh);
            Ui.Box(box, Ui.Card, Ui.Pink, 22f, 3f, true);
            float dy = swap * 6f;
            var a = 1f - swap;
            Ui.Label(new Rect(box.x + 20f, box.y + 14f, bw, 16f), $"HINT {step + 1} OF {Steps.Length}", 11f, Ui.Accent.A(a), TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(box.x + 20f, box.y + 30f + dy, bw - 40f, 28f), Steps[step].title, 21f, Ui.Ink.A(a), TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
            Ui.Label(new Rect(box.x + 20f, box.y + 62f + dy, bw - 40f, 80f), Steps[step].text, 14f, Ui.Soft.A(a), TextAnchor.UpperLeft, Ui.Weight.Bold, true);

            // the dots
            float dx = box.x + 20f, dyy = box.yMax - 32f;
            for (int i = 0; i < Steps.Length; i++)
            {
                float dw = i == step ? 20f : 7f;
                Ui.Round(new Rect(dx, dyy + 9f, dw, 7f), i == step ? Ui.Accent : Ui.Line, 4f);
                dx += dw + 5f;
            }
            if (Ui.Pill(new Rect(box.xMax - 178f, box.yMax - 42f, 76f, 30f), "Skip", false, 13f)) { open = false; PlayerPrefs.SetInt(Key, 1); }
            if (Ui.Pill(new Rect(box.xMax - 96f, box.yMax - 42f, 82f, 30f), step < Steps.Length - 1 ? "Next" : "Got it", true, 13f))
            {
                if (step < Steps.Length - 1) { step++; swap = 1f; } else { open = false; PlayerPrefs.SetInt(Key, 1); }
            }
        }

        static Color Hex(string h) => Ui.Hex(h);

        public bool OverBox(Vector2 p) => open && box.Contains(Ui.ToUi(p));
    }
}
