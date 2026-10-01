using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The character selection (v0.49.0, in place of the character creator): opens after New Game. The household stands on
    /// the sunny deck in front of the living room, in the game's own light, and the panel on the left picks who each
    /// member is from the ready made people (<see cref="PersonModel"/>), with a name, traits, career and how they are
    /// related. Up to four people; Move in saves them in the slot (<see cref="HouseholdData"/>).
    /// </summary>
    public class CharacterSelect : MonoBehaviour
    {
        public static bool IsOpen { get; private set; }
        static CharacterSelect instance;

        HouseholdData house;
        int sel;
        readonly List<GameObject> previews = new List<GameObject>();
        readonly List<float> turn = new List<float>();
        bool faceView;
        float scroll;
        string warning = "";
        float warnUntil;
        System.Random rnd;

        // camera, eased towards its goal
        Vector3 camPivot; float camDist = 3f, camPitch = 6f;
        Vector2 lastMouse; bool dragging;

        Vector3 Stage = new Vector3(3.9f, -0.06f, 9.3f);   // the deck in front of the living room, clear of the garden bench
        const float PanelW = 430f;

        public static void Begin()
        {
            if (instance) return;
            var go = new GameObject("Character selection");
            instance = go.AddComponent<CharacterSelect>();
        }

        void Awake()
        {
            IsOpen = true;
            rnd = new System.Random(System.Environment.TickCount);
            // the deck's own height, so nobody hovers over it or stands in it
            if (Physics.Raycast(Stage + Vector3.up * 1.5f, Vector3.down, out var deck, 4f)) Stage.y = deck.point.y;
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
                var go = Residents.Make(house.members[i], Spot(i), Quaternion.Euler(0f, turn[i], 0f), false, i);
                if (go) go.name = "Selection: " + house.members[i].name;
                previews.Add(go);
            }
        }

        PersonData Cur => house.members[sel];

        // ------------------------------------------------------------ every frame: camera and turntable

        Vector3 Goal(out float dist, out float pitch)
        {
            var at = sel < previews.Count && previews[sel] ? previews[sel].transform.position : Spot(sel);
            if (faceView)
            {
                var rig = sel < previews.Count && previews[sel] ? previews[sel].GetComponent<CharacterRig>() : null;
                float headY = rig ? rig.HeadTop.y - 0.2f : at.y + 1.5f;
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
            Ui.Label(new Rect(x, y, w, 30f), "Choose your household", 22f, Ui.Ink); y += 34f;
            Ui.Label(new Rect(x, y, w, 20f), "Up to four people. Drag beside them to turn them round.", 12f, Ui.Soft); y += 26f;

            // who
            float cw = (w - 3 * 8f) / 4f;
            for (int i = 0; i < HouseholdData.MaxMembers; i++)
            {
                var r = new Rect(x + i * (cw + 8f), y, cw, 44f);
                if (i < house.members.Count)
                {
                    if (Ui.CardButton(r, i == sel)) { sel = i; scroll = 0f; }
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

            var view = new Rect(x - 4f, y, w + 8f, h - y - 110f);
            scroll = Ui.Scroll(view, scroll, ContentHeight(), cwid => Draw(cwid));

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

        float ContentHeight()
        {
            int models = Mathf.Max(1, Residents.Models.Count);
            return 40f + ((models + 2) / 3) * 62f + 30f + 290f + (house.members.Count - 1) * 100f + 20f;
        }

        static GUIStyle nameStyle;

        void Draw(float w)
        {
            float y = 4f;

            // who they are: one card for each ready made person
            Ui.Label(new Rect(4f, y, w, 18f), "Character", 15f, Ui.Ink); y += 26f;
            var models = Residents.Models;
            if (models.Count == 0)
            {
                Ui.Label(new Rect(4f, y, w - 12f, 40f), "No characters are installed yet (Dearlife > Import people). A plain stand-in is used for now.", 12f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
                y += 62f;
            }
            else
            {
                var mine = Residents.ModelOf(Cur, sel);
                for (int i = 0; i < models.Count; i++)
                {
                    var m = models[i];
                    var r = new Rect(4f + (i % 3) * 124f, y + (i / 3) * 62f, 118f, 54f);
                    if (Ui.CardButton(r, m == mine) && m != mine) Pick(m);
                    Ui.Label(new Rect(r.x, r.y + 8f, r.width, 20f), m.displayName, 14f, Ui.Ink, TextAnchor.UpperCenter);
                    Ui.Label(new Rect(r.x, r.y + 30f, r.width, 16f), $"{m.height:0.00} m", 11f, Ui.Soft, TextAnchor.UpperCenter);
                }
                y += ((models.Count + 2) / 3) * 62f;
            }
            y += 14f;

            Ui.Label(new Rect(4f, y, w, 18f), "Name", 13f, Ui.Ink); y += 22f;
            if (nameStyle == null) nameStyle = new GUIStyle(GUI.skin.textField) { alignment = TextAnchor.MiddleLeft, padding = new RectOffset(12, 12, 4, 4) };
            nameStyle.fontSize = Mathf.RoundToInt(16f * Ui.Scale);
            var nr = new Rect(4f, y, w - 12f, 36f);
            Ui.Box(nr, Ui.Surface, Ui.Line, 10f, 2f);
            var n = GUI.TextField(Ui.S(nr), Cur.name, 16, nameStyle);
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
        }

        void Warn(string s) { warning = s; warnUntil = Time.unscaledTime + 3f; }

        // ------------------------------------------------------------ actions

        void Pick(PersonModel m)
        {
            // a name the game gave follows the new character (a woman's name for a woman); one typed in is kept
            var old = Residents.ModelOf(Cur, sel);
            bool wasWoman = !old || old.feminine >= 0.5f, isWoman = m.feminine >= 0.5f;
            Cur.model = m.id;
            if (wasWoman != isWoman) Cur.name = PersonData.RandomName(rnd, isWoman, Names(sel));
            Rebuild();
        }

        List<string> Names(int except)
        {
            var names = new List<string>();
            for (int i = 0; i < house.members.Count; i++) if (i != except) names.Add(house.members[i].name);
            return names;
        }

        List<string> ModelsTaken(int except)
        {
            var ids = new List<string>();
            for (int i = 0; i < house.members.Count; i++)
            {
                if (i == except) continue;
                var m = Residents.ModelOf(house.members[i], i);
                if (m) ids.Add(m.id);
            }
            return ids;
        }

        void AddPerson()
        {
            if (house.members.Count >= HouseholdData.MaxMembers) return;
            house.members.Add(PersonData.Random(rnd, Names(-1), ModelsTaken(-1)));
            turn.Add(0f);
            sel = house.members.Count - 1;
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
            house.members[sel] = PersonData.Random(rnd, Names(sel), ModelsTaken(sel));
            Rebuild();
        }

        void MoveIn()
        {
            var seen = new HashSet<string>();
            for (int i = 0; i < house.members.Count; i++)
            {
                var m = house.members[i];
                m.name = (m.name ?? "").Trim();
                if (m.name == "") { Warn("Everyone needs a name."); return; }
                if (m.name == "Bedah" || !seen.Add(m.name)) { Warn($"Two people can't share the name {m.name}."); return; }
                var model = Residents.ModelOf(m, i);
                if (model) m.model = model.id;       // kept in the save, so adding more characters later changes nobody
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
