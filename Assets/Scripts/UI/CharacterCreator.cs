using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The character creator (phase 4 of docs/CHARACTER_PLAN.md, our own design inspired by inZOI): opens after New Game.
    /// The household stands on the sunny deck in front of the living room, in the game's own light (the camera looks from
    /// the garden, with the house behind them), and the panel on the
    /// left shapes the chosen person: body and face sliders, skin tone, hair, everyday clothes and sleepwear with colours,
    /// name, traits and career. Up to four people; Move in saves them in the slot (<see cref="HouseholdData"/>) and they
    /// take over the house from Lily and James.
    /// </summary>
    public class CharacterCreator : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static CharacterCreator instance;

        enum Tab { Body, Face, Look, Details, Clothes, About }
        Tab tab = Tab.Body;
        HouseholdData house;
        int sel;
        readonly List<GameObject> previews = new List<GameObject>();
        readonly List<float> turn = new List<float>();
        bool faceView, sleepPreview;
        float scroll;
        string warning = "";
        float warnUntil;
        System.Random rnd;

        // camera, eased towards its goal
        Vector3 camPivot; float camDist = 3f, camPitch = 6f;
        Vector2 lastMouse; bool dragging;

        static readonly Vector3 Stage = new Vector3(3.9f, 0.05f, 9.3f);   // the deck in front of the living room, clear of the garden bench
        const float PanelW = 430f;

        public static void Begin()
        {
            if (instance) return;
            var go = new GameObject("Character creator");
            instance = go.AddComponent<CharacterCreator>();
        }

        void Awake()
        {
            IsOpen = true;
            rnd = new System.Random(System.Environment.TickCount);
            house = new HouseholdData();
            house.members.Add(PersonData.Random(rnd));
            Residents.HideEveryone(true);
            var cam = OrbitCamera.Instance;
            if (cam) cam.ExternalControl = true;
            if (DayNightCycle.Instance) DayNightCycle.Instance.hour = 10.5f;
            Rebuild();
            camPivot = Goal(out camDist, out camPitch);
        }

        void OnDestroy() { IsOpen = false; if (instance == this) instance = null; }

        // ------------------------------------------------------------ the people on the deck

        Vector3 Spot(int i) => Stage + new Vector3((i - (house.members.Count - 1) * 0.5f) * 1.05f, 0f, 0f);

        void Rebuild()
        {
            foreach (var p in previews) if (p) Destroy(p);
            previews.Clear();
            while (turn.Count < house.members.Count) turn.Add(0f);
            for (int i = 0; i < house.members.Count; i++)
            {
                var go = Residents.Make(house.members[i], Spot(i), Quaternion.Euler(0f, turn[i], 0f), false);
                if (go) { go.name = "Creator: " + house.members[i].name; if (sleepPreview && i == sel) go.GetComponent<PersonLook>().Dress(true); }
                previews.Add(go);
            }
        }

        PersonData Cur => house.members[sel];
        PersonLook CurLook => sel < previews.Count && previews[sel] ? previews[sel].GetComponent<PersonLook>() : null;

        void ShapeChanged() { var l = CurLook; if (l) { l.data = Cur; l.ApplyShape(); } }
        void TintsChanged() { var l = CurLook; if (l) { l.data = Cur; l.ApplyTints(); } }
        void ClothesChanged() { var l = CurLook; if (l) { l.data = Cur; l.ApplyTints(); l.Dress(sleepPreview); } }

        // ------------------------------------------------------------ every frame: camera and turntable

        Vector3 Goal(out float dist, out float pitch)
        {
            var at = sel < previews.Count && previews[sel] ? previews[sel].transform.position : Spot(sel);
            if (faceView)
            {
                var rig = sel < previews.Count && previews[sel] ? previews[sel].GetComponent<CharacterRig>() : null;
                float headY = rig ? rig.HeadTop.y - 0.3f : at.y + 1.5f;
                dist = 1.05f; pitch = 3f;
                return new Vector3(at.x + dist * 0.22f, headY, at.z);    // the camera looks back at the house, so +x is screen left: the face sits right of the panel
            }
            dist = 4.1f; pitch = 6f;
            return new Vector3(at.x + dist * 0.2f, at.y + 0.88f, at.z);
        }

        void Update()
        {
            var cam = OrbitCamera.Instance;
            var goal = Goal(out float gd, out float gp);
            float k = 1f - Mathf.Exp(-6f * Time.unscaledDeltaTime);
            camPivot = Vector3.Lerp(camPivot, goal, k); camDist = Mathf.Lerp(camDist, gd, k); camPitch = Mathf.Lerp(camPitch, gp, k);
            if (cam) { cam.ExternalControl = true; cam.SetImmediate(camPivot, 180f, camPitch, camDist); }

            // drag anywhere right of the panel to turn the person round
            Vector2 m = Input.mousePosition;
            bool overPanel = m.x / Mathf.Max(0.01f, Ui.Scale) < PanelW + 30f;
            if (Input.GetMouseButtonDown(0) && !overPanel) { dragging = true; lastMouse = m; }
            if (!Input.GetMouseButton(0)) dragging = false;
            if (dragging && sel < turn.Count)
            {
                turn[sel] -= (m.x - lastMouse.x) * 0.4f;
                lastMouse = m;
            }
            for (int i = 0; i < previews.Count; i++)
                if (previews[i]) previews[i].transform.rotation = Quaternion.Euler(0f, turn[i], 0f);
            if (Input.GetKeyDown(KeyCode.Escape)) Cancel();
        }

        // ------------------------------------------------------------ the panel

        void OnGUI()
        {
            Ui.Begin();
            GUI.depth = -450;
            float h = Ui.H;
            Ui.Box(new Rect(16f, 16f, PanelW, h - 32f), Ui.Panel.A(0.96f), Ui.Line, 22f, 2f, true);
            float x = 36f, w = PanelW - 40f, y = 30f;
            Ui.Label(new Rect(x, y, w, 30f), "Create your household", 22f, Ui.Ink); y += 34f;
            Ui.Label(new Rect(x, y, w, 20f), "Up to four people. Drag beside them to turn them round.", 12f, Ui.Soft); y += 26f;

            // who
            float cw = (w - 3 * 8f) / 4f;
            for (int i = 0; i < HouseholdData.MaxMembers; i++)
            {
                var r = new Rect(x + i * (cw + 8f), y, cw, 44f);
                if (i < house.members.Count)
                {
                    if (Ui.CardButton(r, i == sel)) { sel = i; sleepPreview = false; scroll = 0f; }
                    string n = house.members[i].name;
                    Ui.Label(new Rect(r.x, r.y, r.width, r.height), string.IsNullOrEmpty(n) ? "(no name)" : n, 13f, Ui.Ink, TextAnchor.MiddleCenter);
                }
                else if (i == house.members.Count)
                {
                    if (Ui.CardButton(r)) AddPerson();
                    Ui.Label(r, "+ Add", 13f, Ui.Soft, TextAnchor.MiddleCenter);
                }
            }
            y += 54f;
            if (house.members.Count > 1 && Ui.Chip(new Rect(x + w - 120f, y - 6f, 120f, 24f), "Remove " + Short(Cur.name))) { RemovePerson(); return; }
            y += house.members.Count > 1 ? 26f : 0f;

            // tabs
            string[] tabs = { "Body", "Face", "Skin & hair", "Details", "Clothes", "About" };
            float tw = (w - 5 * 5f) / 6f;
            for (int i = 0; i < tabs.Length; i++)
                if (Ui.Chip(new Rect(x + i * (tw + 5f), y, tw, 30f), tabs[i], (int)tab == i, 11f)) { tab = (Tab)i; scroll = 0f; faceView = tab == Tab.Face || tab == Tab.Details; }
            y += 42f;

            var view = new Rect(x - 4f, y, w + 8f, h - y - 110f);
            float content = ContentHeight(w);
            scroll = Ui.Scroll(view, scroll, content, cwid => DrawTab(cwid));

            // bottom
            float by = h - 86f;
            if (Ui.Pill(new Rect(x, by, 110f, 40f), "Random", false, 14f)) Randomise();
            if (Ui.Pill(new Rect(x + 120f, by, 100f, 40f), "Back", false, 14f)) Cancel();
            if (Ui.Pill(new Rect(x + w - 150f, by, 150f, 40f), "Move in", true, 15f)) MoveIn();
            if (Time.unscaledTime < warnUntil) Ui.Label(new Rect(x, by - 26f, w, 20f), warning, 12f, Ui.Rose);

            // view controls, top right
            float rx = Ui.W - 360f;
            if (Ui.Chip(new Rect(rx, 24f, 100f, 30f), "Full body", !faceView)) faceView = false;
            if (Ui.Chip(new Rect(rx + 106f, 24f, 70f, 30f), "Face", faceView)) faceView = true;
            string[] times = { "Morning", "Noon", "Evening", "Night" };
            float[] hours = { 8.5f, 12.5f, 18.6f, 22f };
            var dn = DayNightCycle.Instance;
            for (int i = 0; i < 4; i++)
                if (Ui.Chip(new Rect(rx + i * 86f, 60f, 80f, 26f), times[i], dn && Mathf.Abs(dn.hour - hours[i]) < 0.8f, 11f) && dn) dn.hour = hours[i];
        }

        static string Short(string n) => string.IsNullOrEmpty(n) ? "this person" : n;

        float ContentHeight(float w)
        {
            switch (tab)
            {
                case Tab.Body: return PersonData.BodySliders.Length * 52f + 10f;
                case Tab.Face: return PersonData.FaceSliders.Length * 52f + 10f;
                case Tab.Look: return 330f;
                case Tab.Details: return 900f;
                case Tab.Clothes: return 560f;
                default: return 420f + (house.members.Count - 1) * 100f + ((PersonData.Presets().Count + 2) / 3) * 34f + 40f;
            }
        }

        void DrawTab(float w)
        {
            float y = 4f;
            switch (tab)
            {
                case Tab.Body:
                case Tab.Face:
                {
                    var list = tab == Tab.Body ? PersonData.BodySliders : PersonData.FaceSliders;
                    foreach (var s in list)
                    {
                        Ui.Label(new Rect(4f, y, w, 18f), PersonData.Label(s), 13f, Ui.Ink);
                        PersonData.Ends(s, out var lo, out var hi);
                        Ui.Label(new Rect(4f, y, w - 12f, 18f), lo + "  ·  " + hi, 11f, Ui.Soft, TextAnchor.UpperRight);
                        float v = Cur.Slider(s);
                        float nv = Ui.Slider(new Rect(0f, y + 18f, w - 8f, 28f), v, -1f, 1f);
                        if (!Mathf.Approximately(nv, v)) { Cur.SetSlider(s, nv); ShapeChanged(); }
                        y += 52f;
                    }
                    break;
                }
                case Tab.Look:
                {
                    Ui.Label(new Rect(4f, y, w, 18f), "Skin tone", 13f, Ui.Ink); y += 20f;
                    for (int i = 0; i < 12; i++)
                        Ui.Round(new Rect(8f + i * (w - 16f) / 12f, y, (w - 16f) / 12f - 2f, 10f), SkinSwatch(i / 11f), 3f);
                    y += 12f;
                    float s = Ui.Slider(new Rect(0f, y, w - 8f, 28f), Cur.skin, 0f, 1f);
                    if (!Mathf.Approximately(s, Cur.skin)) { Cur.skin = s; TintsChanged(); }
                    y += 44f;
                    Ui.Label(new Rect(4f, y, w, 18f), "Hair", 13f, Ui.Ink); y += 24f;
                    string[] styles = { "hair_long", "hair_short", "" };
                    string[] names = { "Long", "Short", "None" };
                    for (int i = 0; i < 3; i++)
                        if (Ui.Pill(new Rect(4f + i * 104f, y, 98f, 34f), names[i], Cur.hair == styles[i], 13f)) { Cur.hair = styles[i]; ClothesChanged(); }
                    y += 50f;
                    Ui.Label(new Rect(4f, y, w, 18f), "Hair colour (brows and lashes follow)", 13f, Ui.Ink); y += 24f;
                    int col = 0;
                    foreach (var hc in PersonData.HairColours)
                    {
                        if (Swatch(new Rect(4f + col * 58f, y, 50f, 50f), hc.c, Cur.hairColour == hc.c)) { Cur.hairColour = hc.c; TintsChanged(); }
                        col++;
                    }
                    y += 60f;
                    break;
                }
                case Tab.Details: DrawDetails(w, ref y); break;
                case Tab.Clothes: DrawClothes(w, ref y); break;
                case Tab.About: DrawAbout(w, ref y); break;
            }
        }

        /// <summary>A row of colour swatches; returns the picked colour when one is clicked.</summary>
        bool SwatchRow(float w, ref float y, (string name, Color c)[] list, Color now, out Color picked, float size = 42f)
        {
            picked = now;
            bool changed = false;
            for (int i = 0; i < list.Length; i++)
                if (Swatch(new Rect(4f + i * (size + 6f), y, size, size), list[i].c.a < 0.01f ? new Color(0.36f, 0.22f, 0.12f) : list[i].c, now == list[i].c))
                { picked = list[i].c; changed = true; }
            y += size + 8f;
            return changed;
        }

        float Amount(float w, ref float y, string label, float v)
        {
            Ui.Label(new Rect(4f, y, w, 18f), label, 12f, Ui.Soft);
            float nv = Ui.Slider(new Rect(0f, y + 16f, w - 8f, 28f), v, 0f, 1f);
            y += 48f;
            return nv;
        }

        void DrawDetails(float w, ref float y)
        {
            Ui.Label(new Rect(4f, y, w, 18f), "Eye colour", 15f, Ui.Ink); y += 26f;
            if (SwatchRow(w, ref y, PersonData.EyeColours, Cur.eyeColour, out var eye)) { Cur.eyeColour = eye; TintsChanged(); }

            Ui.Label(new Rect(4f, y, w, 18f), "Makeup", 15f, Ui.Ink); y += 26f;
            Ui.Label(new Rect(4f, y, w, 18f), "Lipstick", 13f, Ui.Ink); y += 22f;
            if (SwatchRow(w, ref y, PersonData.LipColours, Cur.lipColour, out var lip, 36f)) { Cur.lipColour = lip; if (Cur.lipstick < 0.05f) Cur.lipstick = 0.5f; TintsChanged(); }
            float a = Amount(w, ref y, "How much", Cur.lipstick); if (!Mathf.Approximately(a, Cur.lipstick)) { Cur.lipstick = a; TintsChanged(); }
            Ui.Label(new Rect(4f, y, w, 18f), "Blush", 13f, Ui.Ink); y += 22f;
            if (SwatchRow(w, ref y, PersonData.BlushColours, Cur.blushColour, out var bl, 36f)) { Cur.blushColour = bl; if (Cur.blush < 0.05f) Cur.blush = 0.4f; TintsChanged(); }
            a = Amount(w, ref y, "How much", Cur.blush); if (!Mathf.Approximately(a, Cur.blush)) { Cur.blush = a; TintsChanged(); }
            Ui.Label(new Rect(4f, y, w, 18f), "Eyeshadow", 13f, Ui.Ink); y += 22f;
            if (SwatchRow(w, ref y, PersonData.ShadowColours, Cur.shadowColour, out var sh, 36f)) { Cur.shadowColour = sh; if (Cur.eyeshadow < 0.05f) Cur.eyeshadow = 0.45f; TintsChanged(); }
            a = Amount(w, ref y, "How much", Cur.eyeshadow); if (!Mathf.Approximately(a, Cur.eyeshadow)) { Cur.eyeshadow = a; TintsChanged(); }
            a = Amount(w, ref y, "Eyeliner", Cur.liner); if (!Mathf.Approximately(a, Cur.liner)) { Cur.liner = a; TintsChanged(); }

            Ui.Label(new Rect(4f, y, w, 18f), "Freckles", 15f, Ui.Ink); y += 22f;
            a = Amount(w, ref y, "None to many", Cur.freckles); if (!Mathf.Approximately(a, Cur.freckles)) { Cur.freckles = a; TintsChanged(); }

            Ui.Label(new Rect(4f, y, w, 18f), "Facial hair (in the hair colour)", 15f, Ui.Ink); y += 26f;
            for (int i = 0; i < PersonData.Beards.Length; i++)
                if (Ui.Pill(new Rect(4f + i * 96f, y, 90f, 32f), PersonData.Beards[i].name, Cur.beard == PersonData.Beards[i].id, 12f)) { Cur.beard = PersonData.Beards[i].id; TintsChanged(); }
            y += 46f;

            Ui.Label(new Rect(4f, y, w, 18f), "Tattoos", 15f, Ui.Ink); y += 26f;
            for (int i = 0; i < PersonData.Tattoos.Length; i++)
            {
                var t = PersonData.Tattoos[i];
                bool on = Cur.tattoos.Contains(t.id);
                if (Ui.Chip(new Rect(4f + i * 124f, y, 118f, 28f), t.name, on)) { if (on) Cur.tattoos.Remove(t.id); else Cur.tattoos.Add(t.id); TintsChanged(); }
            }
            y += 40f;
        }

        static Color SkinSwatch(float t)
        {
            var c = PersonLook.SkinTint(t) * new Color(0.86f, 0.66f, 0.55f);   // the base skin's own colour, roughly
            c.a = 1f;
            return c;
        }

        static bool Swatch(Rect r, Color c, bool on)
        {
            bool click = Ui.CardButton(r, on);
            var inner = new Rect(r.x + 6f, r.y + 6f, r.width - 12f, r.height - 12f);
            var cc = c; cc.a = 1f;
            Ui.Round(inner, cc, (r.width - 12f) * 0.5f);
            return click;
        }

        bool Has(string id) => Cur.everyday.Contains(id);

        void SetEveryday(params string[] items)
        {
            bool hijab = Has("hijab"), shoes = Has("shoes");
            Cur.everyday = new List<string>(items);
            if (hijab) Cur.everyday.Add("hijab");
            if (shoes) Cur.everyday.Add("shoes");
            ClothesChanged();
        }

        void Toggle(string id)
        {
            if (Has(id)) Cur.everyday.Remove(id); else Cur.everyday.Add(id);
            ClothesChanged();
        }

        void ColourRow(float w, ref float y, string label, string id)
        {
            Ui.Label(new Rect(4f, y, w, 18f), label, 13f, Ui.Ink); y += 22f;
            Cur.TryColour(id, out var now);
            int i = 0;
            foreach (var fc in PersonData.FabricColours)
            {
                if (Swatch(new Rect(4f + i * 42f, y, 38f, 38f), fc.c, now == fc.c)) { Cur.SetColour(id, fc.c); TintsChanged(); }
                i++;
            }
            y += 48f;
        }

        void DrawClothes(float w, ref float y)
        {
            Ui.Label(new Rect(4f, y, w, 18f), "Everyday", 15f, Ui.Ink); y += 26f;
            bool casual = Has("casual");
            if (Ui.Pill(new Rect(4f, y, 170f, 34f), "T-shirt and jeans", casual, 12f)) SetEveryday("casual");
            if (Ui.Pill(new Rect(182f, y, 170f, 34f), "Tunic and trousers", !casual, 12f)) SetEveryday("tunic", "trousers");
            y += 44f;
            if (Ui.Pill(new Rect(4f, y, 110f, 34f), "Hijab", Has("hijab"), 12f)) Toggle("hijab");
            if (Ui.Pill(new Rect(122f, y, 110f, 34f), "Shoes", Has("shoes"), 12f)) Toggle("shoes");
            y += 48f;
            if (!casual) { ColourRow(w, ref y, "Tunic colour", "tunic"); ColourRow(w, ref y, "Trousers colour", "trousers"); }
            if (Has("hijab")) ColourRow(w, ref y, "Hijab colour", "hijab");
            y += 6f;
            Ui.Label(new Rect(4f, y, w, 18f), "Sleepwear", 15f, Ui.Ink);
            if (Ui.Chip(new Rect(w - 150f, y - 4f, 146f, 26f), sleepPreview ? "Show everyday" : "Show sleepwear", sleepPreview))
            {
                sleepPreview = !sleepPreview;
                if (CurLook) CurLook.Dress(sleepPreview);
            }
            y += 28f;
            ColourRow(w, ref y, "Pyjama top", "pyjama_top");
            ColourRow(w, ref y, "Pyjama bottoms", "pyjama_bottoms");
        }

        static GUIStyle nameStyle;

        void DrawAbout(float w, ref float y)
        {
            Ui.Label(new Rect(4f, y, w, 18f), "Name", 13f, Ui.Ink); y += 22f;
            if (nameStyle == null) nameStyle = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 4, 4) };
            nameStyle.fontSize = Mathf.RoundToInt(16f * Ui.Scale);
            var r = new Rect(4f, y, w - 12f, 36f);
            Ui.Box(r, Ui.Surface, Ui.Line, 10f, 2f);
            var n = GUI.TextField(Ui.S(r), Cur.name, 16, nameStyle);
            if (n != Cur.name) Cur.name = n;
            y += 48f;
            Ui.Label(new Rect(4f, y, w, 18f), "Traits (pick three)", 13f, Ui.Ink); y += 24f;
            for (int i = 0; i < PersonData.Traits.Length; i++)
            {
                string t = PersonData.Traits[i];
                bool on = Cur.traits.Contains(t);
                if (Ui.Chip(new Rect(4f + (i % 3) * 124f, y + (i / 3) * 34f, 118f, 28f), t, on))
                {
                    if (on) Cur.traits.Remove(t);
                    else if (Cur.traits.Count < 3) Cur.traits.Add(t);
                    else Warn("Three traits at most: take one off first.");
                }
            }
            y += 80f;
            Ui.Label(new Rect(4f, y, w, 18f), "Career", 13f, Ui.Ink); y += 24f;
            for (int i = 0; i < PersonData.Jobs.Length; i++)
            {
                string j = PersonData.Jobs[i];
                if (Ui.Chip(new Rect(4f + (i % 3) * 124f, y + (i / 3) * 34f, 118f, 28f), j == "" ? "No job" : j, Cur.job == j)) Cur.job = j;
            }
            y += 80f;

            // how this person is related to each of the others
            for (int o = 0; o < house.members.Count; o++)
            {
                if (o == sel) continue;
                Ui.Label(new Rect(4f, y, w, 18f), "With " + Short(house.members[o].name), 13f, Ui.Ink); y += 24f;
                string now = house.KindOf(sel, o);
                for (int k = 0; k < HouseholdData.Kinds.Length; k++)
                {
                    string kind = HouseholdData.Kinds[k].kind;
                    if (Ui.Chip(new Rect(4f + (k % 3) * 124f, y + (k / 3) * 34f, 118f, 28f), kind, now == kind)) house.SetKind(sel, o, kind);
                }
                y += 76f;
            }

            // sharing: a code to copy and paste, and presets kept on this computer
            Ui.Label(new Rect(4f, y, w, 18f), "Share", 15f, Ui.Ink); y += 26f;
            if (Ui.Chip(new Rect(4f, y, 118f, 28f), "Copy person")) { GUIUtility.systemCopyBuffer = Cur.ToCode(); Warn("Copied: paste the code in anyone's creator."); }
            if (Ui.Chip(new Rect(128f, y, 118f, 28f), "Paste person")) PastePerson();
            if (Ui.Chip(new Rect(252f, y, 118f, 28f), "Save preset")) { Cur.SavePreset(); Warn("Saved " + Short(Cur.name) + " as a preset."); }
            y += 34f;
            if (Ui.Chip(new Rect(4f, y, 118f, 28f), "Copy household")) { GUIUtility.systemCopyBuffer = PersonData.HouseholdCode(house); Warn("Copied the whole household."); }
            if (Ui.Chip(new Rect(128f, y, 118f, 28f), "Paste household")) PasteHousehold();
            y += 40f;
            var presets = PersonData.Presets();
            if (presets.Count > 0)
            {
                Ui.Label(new Rect(4f, y, w, 18f), "Presets (click to use for this person)", 12f, Ui.Soft); y += 22f;
                for (int i = 0; i < presets.Count; i++)
                    if (Ui.Chip(new Rect(4f + (i % 3) * 124f, y + (i / 3) * 34f, 118f, 28f), presets[i].name))
                    {
                        var p = PersonData.FromCode(System.IO.File.ReadAllText(presets[i].path));
                        if (p != null) { house.members[sel] = p; Rebuild(); } else Warn("That preset could not be read.");
                    }
                y += ((presets.Count + 2) / 3) * 34f;
            }
        }

        void PastePerson()
        {
            var p = PersonData.FromCode(GUIUtility.systemCopyBuffer);
            if (p == null) { Warn("No person code to paste (copy one first)."); return; }
            house.members[sel] = p;
            sleepPreview = false;
            Rebuild();
        }

        void PasteHousehold()
        {
            var h = PersonData.HouseholdFromCode(GUIUtility.systemCopyBuffer);
            if (h == null) { Warn("No household code to paste (copy one first)."); return; }
            house = h;
            turn.Clear();
            sel = 0; sleepPreview = false;
            Rebuild();
        }

        void Warn(string s) { warning = s; warnUntil = Time.unscaledTime + 3f; }

        // ------------------------------------------------------------ actions

        void AddPerson()
        {
            if (house.members.Count >= HouseholdData.MaxMembers) return;
            var names = new List<string>(); foreach (var m in house.members) names.Add(m.name);
            house.members.Add(PersonData.Random(rnd, names));
            turn.Add(0f);
            sel = house.members.Count - 1;
            sleepPreview = false;
            Rebuild();
        }

        void RemovePerson()
        {
            if (house.members.Count <= 1) return;
            house.RemoveMember(sel);
            turn.RemoveAt(sel);
            sel = Mathf.Clamp(sel, 0, house.members.Count - 1);
            Rebuild();
        }

        void Randomise()
        {
            var names = new List<string>(); for (int i = 0; i < house.members.Count; i++) if (i != sel) names.Add(house.members[i].name);
            house.members[sel] = PersonData.Random(rnd, names);
            sleepPreview = false;
            Rebuild();
        }

        void MoveIn()
        {
            var seen = new HashSet<string>();
            foreach (var m in house.members)
            {
                m.name = (m.name ?? "").Trim();
                if (m.name == "") { Warn("Everyone needs a name (the About tab)."); return; }
                if (m.name == "Bedah" || !seen.Add(m.name)) { Warn($"Two people can't share the name {m.name}."); return; }
            }
            house.Save();
            PlayerPrefs.SetInt(SaveSystem.Key("dearlife.funds"), Household.Instance ? Household.Instance.Funds : 15000);   // the slot now counts as a save
            SaveSystem.SaveNow();
            Close();
            Residents.MoveIn(house);
            var cam = OrbitCamera.Instance;
            if (cam) { cam.ExternalControl = false; cam.FocusOn(new Vector3(8f, 0f, 6f), 26f); }
        }

        void Cancel()
        {
            Close();
            Residents.MoveInSaved();      // the house is not left empty behind the title screen
            MainMenu.Open();
        }

        void Close()
        {
            foreach (var p in previews) if (p) Destroy(p);
            previews.Clear();
            Residents.HideEveryone(false);
            Destroy(gameObject);
            IsOpen = false;
        }
    }
}
