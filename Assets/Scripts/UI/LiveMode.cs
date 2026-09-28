using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Live mode, the way you play the house like The Sims: click a person to pick them (a green diamond floats over their head),
    /// click the floor to send them there, click furniture or another person for a round menu of what they can do,
    /// space pauses and 1, 2, 3 change the speed. Orders queue up: a person finishes what they were told before they go
    /// back to doing their own thing.
    /// </summary>
    [DefaultExecutionOrder(20)]
    public class LiveMode : MonoBehaviour
    {
        public static LiveMode Instance { get; private set; }
        public static Character Selected { get; private set; }

        [Tooltip("the green diamond over the picked person")] public Material plumbobMaterial;

        static readonly float[] Speeds = { 0f, 1f, 2.5f, 5f };
        public static int Speed { get; private set; } = 1;

        // ---- round menu ----
        struct Option { public string label; public System.Action act; public bool enabled; }
        readonly List<Option> options = new List<Option>();
        bool pieOpen;
        Vector2 pieCenter;   // GUI points (already divided by the GUI scale)
        const float PieDisc = 92f;
        float PieRadius => Mathf.Max(78f, options.Count * PieDisc * 1.05f / (2f * Mathf.PI));
        string pieTitle;
        Furniture menuPiece;

        /// <summary>The piece whose round menu is open right now (it stays highlighted while you choose), or null.</summary>
        public static Furniture MenuPiece => Instance != null && Instance.pieOpen ? Instance.menuPiece : null;

        Rect portraits, speedPanel;
        float scale = 1f;

        Transform plumbob;
        Vector3 markerAt; float markerUntil;
        float toastUntil; string toast;

        void Awake() { Instance = this; }

        void Start()
        {
            var previous = OrbitCamera.IsOverUi;
            OrbitCamera.IsOverUi = p => (previous != null && previous(p)) || portraits.Contains(p / scale) || PieContains(p);
            MakePlumbob();
        }

        void OnDestroy() { Time.timeScale = 1f; if (Instance == this) Instance = null; }

        // ---------------------------------------------------------------- input

        void Update()
        {
            if (Selected == null || !Selected) Selected = FirstPerson();
            if (MainMenu.Busy) return;       // the title screen or the character creator owns the mouse and keys

            if (Input.GetKeyDown(KeyCode.Space)) SetSpeed(Speed == 0 ? 1 : 0);
            if (Input.GetKeyDown(KeyCode.Alpha1)) SetSpeed(1);
            if (Input.GetKeyDown(KeyCode.Alpha2)) SetSpeed(2);
            if (Input.GetKeyDown(KeyCode.Alpha3)) SetSpeed(3);

            if (DecorateMode.Active) { pieOpen = false; UpdatePlumbob(); return; }

            if (pieOpen && (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))) pieOpen = false;
            var oc = OrbitCamera.Instance;
            if (oc && oc.ClickedThisFrame) HandleClick(Input.mousePosition, false);
            else if (oc && oc.RightClickedThisFrame) HandleClick(Input.mousePosition, true);
            UpdatePlumbob();
        }

        public static void SetSpeed(int s)
        {
            Speed = Mathf.Clamp(s, 0, 3);
            Time.timeScale = Speeds[Speed];
        }

        static Character FirstPerson()
        {
            foreach (var c in Character.All) if (!c.isPet) return c;
            return null;
        }

        public static void Select(Character c) { if (c && !c.isPet) Selected = c; }

        // ---------------------------------------------------------------- clicks

        /// <summary>The person or pet a click at this mouse position would pick (clicks go to them before furniture).</summary>
        public Character CharacterUnder(Vector3 mouse) => PickCharacter(mouse);

        Character PickCharacter(Vector3 mouse)
        {
            var cam = Camera.main;
            if (!cam) return null;
            Character best = null;
            float bestD = 46f * scale;
            foreach (var c in Character.All)
            {
                var r = c.GetComponentInChildren<Renderer>();
                if (r && !r.enabled) continue;
                float h = (c.isPet ? 0.25f : 0.95f) * c.scale;
                var s = cam.WorldToScreenPoint(c.transform.position + Vector3.up * h);
                if (s.z < 0f) continue;
                float d = Vector2.Distance(new Vector2(s.x, s.y), new Vector2(mouse.x, mouse.y));
                if (c.isPet) d *= 0.8f;
                if (d < bestD) { bestD = d; best = c; }
            }
            return best;
        }

        void HandleClick(Vector3 mouse, bool right)
        {
            pieOpen = false;
            if (Selected == null) return;
            var cam = Camera.main;
            if (!cam) return;

            var who = PickCharacter(mouse);
            if (who != null)
            {
                if (who == Selected) { OrbitCamera.Instance.FocusOn(who.transform.position, 8f); return; }
                OpenPersonMenu(who, mouse);
                return;
            }

            var hits = Physics.RaycastAll(cam.ScreenPointToRay(mouse), 400f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var tv = h.collider.GetComponentInParent<TvScreen>();
                if (tv != null) { OpenTvMenu(tv, mouse); return; }
                var piece = SeatOwner(h.collider.transform);
                if (piece != null && right && Cheats.Enabled && InteractionTable.BaseId(piece.name) == "mailbox") { cheatPiece = piece; OpenCheats(CheatPage.Main, mouse); return; }
                if (piece != null) { OpenFurnitureMenu(piece, h.point, mouse); return; }
                var pool = PoolAt(h.point);
                if (pool != null) { OpenThingMenu(pool, h.point, mouse); return; }
                if (right) { OpenGroundMenu(h.point, mouse); return; }
                Walk(h.point);
                return;
            }
        }

        /// <summary>The piece of furniture a click on this collider opens a menu for (it has a seat or things to do), or null.</summary>
        public static Furniture SeatOwner(Transform t)
        {
            foreach (var f in t.GetComponentsInParent<Furniture>())
                if (f.GetComponentInChildren<UseSpot>() != null || f.GetComponent<Interactable>() != null) return f;
            return null;
        }

        /// <summary>The pool: no collider on the water, so it is found by where the click landed.</summary>
        static Interactable PoolAt(Vector3 p)
        {
            foreach (var it in Interactable.All)
                {
                if (!it || !it.hasCustomStand) continue;
                var half = it.poolHalf.x > 0f ? it.poolHalf + new Vector2(0.25f, 0.25f) : new Vector2(4.2f, 2.2f);
                if (Mathf.Abs(p.x - it.customFace.x) < half.x && Mathf.Abs(p.z - it.customFace.z) < half.y) return it;
            }
            return null;
        }

        void Walk(Vector3 point)
        {
            Selected.GiveOrder(new Character.Order { kind = Character.Order.Kind.Go, point = point, label = "Walking over" });
            markerAt = point; markerUntil = Time.unscaledTime + 0.9f;
        }

        void OpenMenu(Vector3 mouse, string heading, Furniture piece = null)
        {
            pieCenter = new Vector2(mouse.x, Screen.height - mouse.y) / scale;
            ShowMenuHere(heading, piece);
        }

        /// <summary>Opens (or reopens) the round menu where it already is, so a sub menu or a repeated cheat does not jump around.</summary>
        void ShowMenuHere(string heading, Furniture piece)
        {
            pieOpen = true;
            pieTitle = heading;
            menuPiece = piece;
            // keep the whole ring on screen
            float m = PieRadius + PieDisc * 0.5f + 6f;
            pieCenter.x = Mathf.Clamp(pieCenter.x, m, Screen.width / scale - m);
            pieCenter.y = Mathf.Clamp(pieCenter.y, m, Screen.height / scale - m);
        }

        // ---------------------------------------------------------------- cheats (right click the mailbox, when switched on in Settings)

        enum CheatPage { Main, Money, Needs, Life, World }
        Furniture cheatPiece;

        void OpenCheats(CheatPage page, Vector3? mouse)
        {
            options.Clear();
            var sim = Selected != null && Selected ? Selected.sim : null;
            string who = Selected != null && Selected ? Selected.displayName : "";
            // every cheat reopens the same page afterwards, so it can be clicked again and again
            System.Action<System.Action> Keep = a => { a(); OpenCheats(page, null); };
            void Add(string label, System.Action act, bool enabled = true) => options.Add(new Option { label = label, enabled = enabled, act = act });
            void Go(string label, CheatPage p) => Add(label, () => OpenCheats(p, null));
            string heading = "Cheats";
            switch (page)
            {
                case CheatPage.Main:
                    Go("Money", CheatPage.Money);
                    Go("Needs", CheatPage.Needs);
                    Go("Skills, career and mood", CheatPage.Life);
                    Go("Time and season", CheatPage.World);
                    break;
                case CheatPage.Money:
                    heading = "Cheats: money";
                    Add($"+{Household.Currency} 1,000", () => Keep(() => Cheats.AddMoney(1000)));
                    Add($"+{Household.Currency} 10,000", () => Keep(() => Cheats.AddMoney(10000)));
                    Add($"+{Household.Currency} 50,000", () => Keep(() => Cheats.AddMoney(50000)));
                    Add($"+{Household.Currency} 250,000", () => Keep(() => Cheats.AddMoney(250000)));
                    Go("Back", CheatPage.Main);
                    break;
                case CheatPage.Needs:
                    heading = "Cheats: " + (who != "" ? who + "'s needs" : "needs");
                    Add("Fill all needs", () => Keep(() => Cheats.FillAll(sim)), sim);
                    foreach (Need n in System.Enum.GetValues(typeof(Need)))
                    {
                        var need = n;
                        Add("Fill " + Sim.NeedName(need).ToLower(), () => Keep(() => Cheats.FillNeed(sim, need)), sim);
                    }
                    Add("Fill everyone's needs", () => Keep(Cheats.FillEveryone));
                    Add(Cheats.FreezeSetting ? "Needs never drop: on" : "Needs never drop: off", () => Keep(Cheats.ToggleFreeze));
                    Go("Back", CheatPage.Main);
                    break;
                case CheatPage.Life:
                    heading = "Cheats: " + (who != "" ? who : "life");
                    Add("Max all skills", () => Keep(() => Cheats.MaxSkills(sim)), sim);
                    Add("Promote", () => Keep(() => Cheats.Promote(sim)), sim);
                    Add("Grant a wish", () => Keep(() => Cheats.GrantWish(sim)), sim);
                    Add("Feel fantastic", () => Keep(() => Cheats.MakeHappy(sim)), sim);
                    Add("Everyone best friends", () => Keep(Cheats.BestFriends));
                    Go("Back", CheatPage.Main);
                    break;
                case CheatPage.World:
                    heading = "Cheats: time";
                    Add("Morning", () => Keep(() => Cheats.SetHour(8f, "morning")));
                    Add("Noon", () => Keep(() => Cheats.SetHour(13f, "noon")));
                    Add("Sunset", () => Keep(() => Cheats.SetHour(18.5f, "sunset")));
                    Add("Night", () => Keep(() => Cheats.SetHour(22.5f, "night")));
                    Add("Next season", () => Keep(Cheats.NextSeason));
                    Go("Back", CheatPage.Main);
                    break;
            }
            if (mouse.HasValue) OpenMenu(mouse.Value, heading, cheatPiece);
            else ShowMenuHere(heading, cheatPiece);
        }

        void OpenPersonMenu(Character who, Vector3 mouse)
        {
            options.Clear();
            var me = Selected;
            if (who.isPet)
            {
                options.Add(new Option { label = "Pet " + who.displayName, enabled = true, act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Pet, target = who, label = "Going to pet " + who.displayName }) });
                options.Add(new Option { label = "Play with " + who.displayName, enabled = true, act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Pet, target = who, label = "Going to play with " + who.displayName }) });
                bool can = Household.CanAfford(5);
                options.Add(new Option { label = "Feed " + who.displayName + " (RM 5)", enabled = can, act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Feed, target = who, label = "Going to feed " + who.displayName }) });
            }
            else
            {
                bool free = who.CanChat;
                options.Add(new Option { label = "Talk to " + who.displayName, enabled = free, act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Talk, target = who, label = "Going to talk to " + who.displayName }) });
                options.Add(new Option { label = "Play as " + who.displayName, enabled = true, act = () => Select(who) });
            }
            options.Add(new Option { label = "Status", enabled = true, act = () => { Select(who.isPet ? Selected : who); SimUi.OpenStatus(); } });
            options.Add(new Option { label = "Go there", enabled = true, act = () => Walk(who.transform.position) });
            OpenMenu(mouse, who.displayName);
        }

        void OpenGroundMenu(Vector3 point, Vector3 mouse)
        {
            options.Clear();
            options.Add(new Option { label = "Go here", enabled = true, act = () => Walk(point) });
            OpenMenu(mouse, "Here");
        }

        /// <summary>A pie option for one interaction: the label shows the price, it greys out when it cannot be done.</summary>
        Option ThingOption(Interactable it, InteractionDef d)
        {
            var me = Selected;
            string label = d.label;
            bool enabled = true;
            if (d.cost > 0) { label += $" (RM {d.cost})"; if (!Household.CanAfford(d.cost)) enabled = false; }
            if (d.job) label += $" (RM {Household.PayPerSecond(me.sim)}/s)";
            if (d.seat)
            {
                bool any = false;
                foreach (var sp in it.GetComponentsInChildren<UseSpot>())
                {
                    if (sp.occupant != null && sp.occupant != me) continue;
                    if ((d.pose == CharacterRig.Pose.Lie) != (sp.pose == CharacterRig.Pose.Lie)) continue;
                    if (d.needsTv && TvScreen.Facing(sp) == null) continue;
                    any = true; break;
                }
                if (!any) { enabled = false; if (d.needsTv) label = d.label + " (no TV)"; }
            }
            if (d.job && !Careers.Allows(me.sim.job, d.id)) { enabled = false; label += " (not your career)"; }
            if (it.user != null && it.user != me) { enabled = false; label += " (busy)"; }
            var def = d;
            return new Option
            {
                label = label, enabled = enabled,
                act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Interact, thing = it, def = def, label = "Going to: " + def.label.ToLower() })
            };
        }

        void OpenThingMenu(Interactable it, Vector3 hit, Vector3 mouse)
        {
            options.Clear();
            foreach (var d in it.defs) options.Add(ThingOption(it, d));
            options.Add(new Option { label = "Go here", enabled = true, act = () => Walk(hit) });
            OpenMenu(mouse, it.id == "pool" ? "Pool" : it.id);
        }

        void OpenTvMenu(TvScreen tv, Vector3 mouse)
        {
            options.Clear();
            var me = Selected;
            options.Add(new Option
            {
                label = tv.on ? "Turn off" : "Turn on", enabled = true,
                act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Go, point = tv.StandPoint, label = "Going to the TV", after = () => tv.Toggle() })
            });
            var seat = BestWatchSeat(tv, me);
            options.Add(new Option
            {
                label = seat != null ? "Watch TV" : "No free seat", enabled = seat != null,
                act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Use, spot = seat, label = "Going to watch TV", after = () => { if (!tv.on) tv.SetOn(true); } })
            });
            OpenMenu(mouse, "TV", tv.GetComponentInParent<Furniture>());
        }

        /// <summary>The nearest free seat that looks at the TV.</summary>
        static UseSpot BestWatchSeat(TvScreen tv, Character who)
        {
            UseSpot best = null; float bestD = float.MaxValue;
            foreach (var s in UseSpot.All)
            {
                if (!s.gameObject.activeInHierarchy || (s.occupant != null && s.occupant != who)) continue;
                if (TvScreen.Facing(s) != tv) continue;
                float d = (s.transform.position - who.transform.position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = s; }
            }
            return best;
        }

        void OpenFurnitureMenu(Furniture piece, Vector3 hit, Vector3 mouse)
        {
            options.Clear();
            var me = Selected;
            var it = piece.GetComponent<Interactable>();
            bool hasSeatDef = false;
            if (it != null)
                foreach (var d in it.defs)
                {
                    if (d.id == "chairsit" && it.GetComponentInChildren<UseSpot>() == null) continue;
                    options.Add(ThingOption(it, d));
                    if (d.seat) hasSeatDef = true;
                }
            if (!hasSeatDef)
            {
                UseSpot best = null; float bestD = float.MaxValue;
                foreach (var s in piece.GetComponentsInChildren<UseSpot>())
                {
                    if (s.occupant != null && s.occupant != me) continue;
                    float d = (s.transform.position - hit).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = s; }
                }
                if (best != null)
                {
                    var spot = best; bool lie = spot.pose == CharacterRig.Pose.Lie;
                    options.Add(new Option
                    {
                        label = lie ? "Lie down" : "Sit down", enabled = true,
                        act = () => me.GiveOrder(new Character.Order { kind = Character.Order.Kind.Use, spot = spot, label = lie ? "Going to lie down" : "Going to sit" })
                    });
                }
            }
            options.Add(new Option { label = "Go here", enabled = true, act = () => Walk(hit) });
            if (piece.HasChannel(0) || piece.HasChannel(1))
            {
                var cp = piece; var at = new Vector2(mouse.x, Screen.height - mouse.y);
                options.Add(new Option { label = "Change colour", enabled = true, act = () => ColourPicker.Open(cp, at) });
            }
            if (!piece.pinned)
            {
                var pc = piece;
                options.Add(new Option { label = "Move", enabled = true, act = () => DecorateMode.Instance.StartMove(pc) });
                options.Add(new Option { label = $"Sell (RM {Household.SellPrice(pc.name):N0})", enabled = true, act = () => DecorateMode.Instance.SellPiece(pc) });
            }
            OpenMenu(mouse, piece.Label, piece);
        }

        // ---------------------------------------------------------------- the hexagon ring over the selected person

        Transform ring;
        Material[] atomMats;

        /// <summary>The game's own pastel palette (pink, lavender, ice blue, soft gold), cycled through slowly.</summary>
        static readonly Color[] PastelPalette =
        {
            new Color(1f, 0.72f, 0.86f), new Color(0.78f, 0.68f, 0.98f), new Color(0.6f, 0.88f, 0.95f), new Color(1f, 0.88f, 0.75f),
        };

        static Mesh Torus(float radius, float tube, int seg, int sides)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2f / seg;
                var c = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                for (int k = 0; k < sides; k++)
                {
                    float b = k * Mathf.PI * 2f / sides;
                    var n = new Vector3(Mathf.Cos(a) * Mathf.Cos(b), Mathf.Sin(b), Mathf.Sin(a) * Mathf.Cos(b));
                    v.Add(c + n * tube);
                }
            }
            for (int i = 0; i < seg; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = i * sides + k, b = i * sides + (k + 1) % sides, c = (i + 1) * sides + k, d = (i + 1) * sides + (k + 1) % sides;
                    t.Add(a); t.Add(c); t.Add(b); t.Add(b); t.Add(c); t.Add(d);
                }
            var m = new Mesh { name = "Ring" };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }

        /// <summary>A pastel hexagon ring, lit like anything else in the room, spinning gently over the person you are playing.</summary>
        void MakePlumbob()
        {
            var mats = new List<Material>();
            var root = new GameObject("Plumbob");
            root.transform.SetParent(transform, false);
            var ringMesh = Torus(0.15f, 0.02f, 6, 10);       // 6 straight sides = a hexagon path, a fat tube so the thickness reads clearly
            var g = new GameObject("HexRing");
            g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = ringMesh;
            var mr = g.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            if (plumbobMaterial) { var m = new Material(plumbobMaterial); mr.sharedMaterial = m; mats.Add(m); }
            ring = g.transform;
            ring.localRotation = Quaternion.Euler(55f, 0f, 0f);   // tilted like a jaunty halo, not lying dead flat
            plumbob = root.transform;
            atomMats = mats.ToArray();
        }

        void UpdatePlumbob()
        {
            if (!plumbob) return;
            bool show = Selected != null && Selected && !DecorateMode.Active;
            var body = Selected != null && Selected ? Selected.GetComponentInChildren<Renderer>() : null;
            show = show && (body == null || body.enabled);
            if (plumbob.gameObject.activeSelf != show) plumbob.gameObject.SetActive(show);
            if (!show) return;
            float t = Time.unscaledTime;
            var rg = Selected.GetComponent<CharacterRig>();
            var head = rg ? rg.HeadTop : Selected.transform.position + Vector3.up * (1.75f * Selected.scale);
            // lower than the old atom, and clear of the name tag above it
            plumbob.position = head + Vector3.up * (0.20f + 0.03f * Mathf.Sin(t * 2.4f));
            if (atomMats != null && atomMats.Length > 0)
            {
                // a slow lerp between the game's own pastel palette, not a raw rainbow
                float p = Mathf.Repeat(t * 0.08f, PastelPalette.Length);
                int a = (int)p, b = (a + 1) % PastelPalette.Length;
                var c = Color.Lerp(PastelPalette[a], PastelPalette[b], p - a);
                atomMats[0].SetColor("_BaseColor", c);
                atomMats[0].SetColor("_EmissiveColor", c * 0.5f);
            }
            plumbob.rotation = Quaternion.identity;
            if (ring) ring.localRotation = Quaternion.Euler(55f, t * 60f, 0f);
        }

        // ---------------------------------------------------------------- drawing

        bool PieContains(Vector2 screenPoint)
        {
            if (!pieOpen) return false;
            return Vector2.Distance(screenPoint / scale, pieCenter) < PieRadius + PieDisc * 0.5f + 6f;
        }

        void OnGUI()
        {
            Ui.Begin();
            scale = Ui.Scale;
            float w = Ui.W, h = Ui.H;
            speedPanel = Rect.zero;
            if (Splash.Showing) return;

            if (DecorateMode.Active) { portraits = Rect.zero; return; }

            // the people, bottom left under the needs panel: little cards like the 2D game's
            var people = new List<Character>();
            foreach (var c in Character.All) if (!c.isPet) people.Add(c);
            const float cardW = 178f, cardH = 54f;
            portraits = new Rect(12f, h - 108f, cardW * Mathf.Max(1, people.Count) + 8f * Mathf.Max(0, people.Count - 1), cardH);
            float px = portraits.x;
            foreach (var c in people)
            {
                var r = new Rect(px, portraits.y, cardW, cardH);
                bool me = c == Selected;
                Ui.Round(new Rect(r.x, r.y + 3f, r.width, r.height), new Color(0.47f, 0.23f, 0.16f, 0.09f), 16f);
                bool hover = Ui.Hover(r);
                Ui.Round(r, me ? Ui.Pale : Ui.Card.A(0.97f), 16f);
                Ui.Ring(r, me || hover ? Ui.Pink : Ui.Line, 2f, 16f);
                Ui.Round(new Rect(r.x + 10f, r.y + 10f, 34f, 34f), c.sim ? c.sim.MoodColour : Ui.Line, 17f);
                Ui.Label(new Rect(r.x + 10f, r.y + 10f, 34f, 34f), c.displayName.Substring(0, 1), 18f, Color.white, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(r.x + 52f, r.y + 7f, cardW - 60f, 20f), c.displayName, 15f, Ui.Ink, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
                Ui.Label(new Rect(r.x + 52f, r.y + 27f, cardW - 60f, 18f), c.Activity + (c.Queued > 0 ? $" (+{c.Queued})" : ""), 11f, Ui.Soft, TextAnchor.MiddleLeft, Ui.Weight.Bold);
                if (GUI.Button(Ui.S(r), GUIContent.none, GUIStyle.none))
                {
                    GameAudio.Play(GameAudio.Sfx.Click);
                    if (c == Selected) OrbitCamera.Instance.FocusOn(c.transform.position, 8f);
                    Select(c);
                }
                px += cardW + 8f;
            }

            if (Selected != null && (Selected.Queued > 0 || Selected.OnOrder))
            {
                if (Ui.Pill(new Rect(portraits.xMax + 8f, portraits.y + 12f, 64f, 30f), "Stop", false, 13f)) Selected.CancelOrders();
                portraits.width += 76f;
            }

            // where the last walk order went
            if (Time.unscaledTime < markerUntil && Camera.main)
            {
                var s = Camera.main.WorldToScreenPoint(markerAt);
                if (s.z > 0f)
                {
                    float k = (markerUntil - Time.unscaledTime) / 0.9f;
                    float r = 12f + 22f * (1f - k);
                    var c = new Rect(s.x / scale - r, (Screen.height - s.y) / scale - r, r * 2f, r * 2f);
                    Ui.Ring(c, Ui.Accent.A(k), 3f, r);
                }
            }

            DrawPie();
        }

        void DrawPie()
        {
            if (!pieOpen || options.Count == 0) return;
            var e = Event.current;
            var centerRect = new Rect(pieCenter.x - 22f, pieCenter.y - 22f, 44f, 44f);
            Ui.Box(centerRect, Ui.Card, Ui.Pink, 22f, 3f, true);
            Ui.Label(centerRect, "x", 16f, Ui.Accent, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
            // the name of what you clicked, in a little pill above the ring
            float tw = Ui.TextWidth(pieTitle, 13f, Ui.Weight.ExtraBold) + 28f;
            var tr = new Rect(pieCenter.x - tw * 0.5f, pieCenter.y - PieRadius - PieDisc * 0.5f - 34f, tw, 26f);
            Ui.OnFill(tr, 13f);
            Ui.Label(tr, pieTitle, 13f, Ui.OnAccent, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
            if (GUI.Button(Ui.S(centerRect), GUIContent.none, GUIStyle.none)) { pieOpen = false; return; }

            int n = options.Count;
            for (int i = 0; i < n; i++)
            {
                float a = (-90f + 360f * i / n) * Mathf.Deg2Rad;
                var c = pieCenter + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * PieRadius;
                var r = new Rect(c.x - PieDisc * 0.5f, c.y - PieDisc * 0.5f, PieDisc, PieDisc);
                var o = options[i];
                bool hover = o.enabled && Ui.Hover(r);
                Ui.Round(new Rect(r.x, r.y + 3f, r.width, r.height), new Color(0.47f, 0.23f, 0.16f, 0.12f), PieDisc);
                Ui.Round(r, !o.enabled ? Ui.Cream2.A(0.9f) : hover ? Ui.Pale : Ui.Card, PieDisc);
                Ui.Ring(r, !o.enabled ? Ui.Line : hover ? Ui.Accent : Ui.Pink, 3f, PieDisc);
                Ui.Label(new Rect(r.x + 9f, r.y + 9f, r.width - 18f, r.height - 18f), o.label, 12f, !o.enabled ? Ui.Soft : hover ? Ui.Accent : Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold, true);
                if (o.enabled && GUI.Button(Ui.S(r), GUIContent.none, GUIStyle.none)) { var act = o.act; pieOpen = false; GameAudio.Play(GameAudio.Sfx.Click); act?.Invoke(); return; }
            }
        }
    }
}
