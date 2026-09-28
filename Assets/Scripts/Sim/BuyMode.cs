using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    [System.Serializable]
    public class CatalogEntry
    {
        public string id, name, category;
        public GameObject prefab;
        public float scale = 1f;
        public int Price => Household.PriceOf(id);
    }

    /// <summary>Seats and beds for pieces that are bought (the same places the builder gives the ones that come with the house).</summary>
    public static class SeatSpots
    {
        static void Spot(GameObject piece, string label, CharacterRig.Pose pose, Vector3 pelvis, float yaw, Vector3 approach, float recline = 6f, float shin = -8f, float footY = 0f, float raise = 0f, float legRaise = 0f, float knee = 6f)
        {
            var g = new GameObject("Use: " + label);
            g.transform.SetParent(piece.transform, false);
            g.transform.localPosition = pelvis;
            var sp = g.AddComponent<UseSpot>();
            sp.label = label; sp.pose = pose; sp.yaw = yaw; sp.approachLocal = approach;
            sp.recline = recline; sp.shinAngle = shin; sp.footY = footY; sp.raise = raise; sp.legRaise = legRaise; sp.kneeBend = knee;
            sp.seconds = pose == CharacterRig.Pose.Lie ? new Vector2(40f, 90f) : new Vector2(20f, 60f);
        }

        public static void Add(GameObject go, string id)
        {
            var sit = CharacterRig.Pose.Sit; var lie = CharacterRig.Pose.Lie;
            switch (id)
            {
                case "sofa": foreach (float x in new[] { -0.65f, 0f, 0.65f }) Spot(go, "sofa", sit, new Vector3(x, 0.62f, 0.02f), 0f, new Vector3(x, 0f, 1.1f), 16f, 12f); break;
                case "modern_arm_chair_01": Spot(go, "armchair", sit, new Vector3(0f, 0.52f, 0.05f), 0f, new Vector3(0f, 0f, 0.95f), 22f, 10f); break;
                case "diningchair": Spot(go, "dining chair", sit, new Vector3(0f, 0.52f, 0f), 0f, new Vector3(0.75f, 0f, 0f), 3f); break;
                case "barstool": Spot(go, "bar stool", sit, new Vector3(0f, 0.75f, 0f), 180f, new Vector3(0f, 0f, 0.8f), 0f, -5f, 0.27f); break;
                case "officechair": Spot(go, "office chair", sit, new Vector3(0f, 0.53f, 0.02f), 0f, new Vector3(0.8f, 0f, 0.1f), 8f); break;
                case "platformbed": case "platformbed_e": Spot(go, "bed", lie, new Vector3(0f, 0.7f, -0.25f), 0f, new Vector3(1.3f, 0f, 0f), knee: 5f); break;
                case "beanbag": Spot(go, "beanbag", sit, new Vector3(0f, 0.33f, 0f), 0f, new Vector3(0f, 0f, 0.95f), 38f, 22f); break;
                case "lounger": Spot(go, "lounger", lie, new Vector3(0f, 0.55f, -0.16f), 0f, new Vector3(0.9f, 0f, 0f), raise: 58f, knee: 4f); break;
                case "outdoorsectional": foreach (float x in new[] { -0.8f, 0f, 0.8f }) Spot(go, "outdoor sofa", sit, new Vector3(x, 0.56f, 0.08f), 0f, new Vector3(x, 0f, 1.0f), 12f, 10f); break;
                case "gardenbench": foreach (float x in new[] { -0.4f, 0.4f }) Spot(go, "bench", sit, new Vector3(x, 0.53f, 0f), 0f, new Vector3(x, 0f, 0.8f), 10f); break;
                case "hammock": Spot(go, "hammock", lie, new Vector3(0f, 0.85f, 0.2f), 0f, new Vector3(1.3f, 0f, 0f), raise: 14f, legRaise: 12f, knee: -14f); break;
            }
        }
    }

    /// <summary>Makes a piece from a catalogue model while the game runs: colliders, a body, furniture, seats, things to do.</summary>
    public static class FurnitureFactory
    {
        static int counter;
        static readonly HashSet<string> Small = new HashSet<string>
        {
            "fruitbowl", "candles", "mug", "globe", "bedlamp", "basket", "cuttingboard", "utensils", "herbs", "towelstack", "cardboardboxes", "paintcans",
            "book_encyclopedia_set_01", "ceramic_vase_03", "desk_lamp_arm_01", "potted_plant_04", "calathea_orbifolia_01", "espresso",
        };

        public static Furniture Create(GameObject prefab, string id, Vector3 pos, float yaw, string key = null, float scale = 1f)
        {
            var go = Object.Instantiate(prefab);
            go.name = id;
            if (!Mathf.Approximately(scale, 1f)) go.transform.localScale = Vector3.one * scale;
            if (id == "sofa") foreach (var t in go.GetComponentsInChildren<Transform>(true)) if (t && t != go.transform && t.name.StartsWith("seat cushion")) Object.Destroy(t.gameObject);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            Bounds all = new Bounds(pos, Vector3.zero); bool first = true;
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
            {
                if (!mf.sharedMesh) continue;
                var b = mf.sharedMesh.bounds;
                if (b.size.x < 0.03f && b.size.y < 0.03f && b.size.z < 0.03f) continue;
                var bc = mf.gameObject.AddComponent<BoxCollider>();
                bc.center = b.center; bc.size = b.size;
                var rend = mf.GetComponent<Renderer>();
                if (rend) { if (first) { all = rend.bounds; first = false; } else all.Encapsulate(rend.bounds); }
            }
            float volume = Mathf.Max(0.02f, all.size.x * all.size.y * all.size.z);
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = Mathf.Clamp(volume * 90f, 2f, 140f);
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = rb.mass < 8f ? CollisionDetectionMode.ContinuousDynamic : CollisionDetectionMode.Discrete;
            rb.linearDamping = 0.6f; rb.angularDamping = 3f;
            rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;   // stays upright
            var f = go.AddComponent<Furniture>();
            f.key = key ?? $"{id}#b{++counter}_{Random.Range(1000, 9999)}";
            f.small = Small.Contains(id);
            if (f.small)
            {
                rb.mass = Mathf.Min(rb.mass, 2.5f);
                rb.isKinematic = true;                      // small things stay exactly where they are put, until you pick them up
                go.AddComponent<StickyProp>();
            }
            f.bought = true;
            SeatSpots.Add(go, id);
            Interactable.Attach(go, id);
            if (id == "tvunit")
            {
                var lg = new GameObject("TV glow");
                lg.transform.SetParent(go.transform, false);
                lg.transform.localPosition = new Vector3(0f, 1.0f, 0.7f);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point; lg.AddComponent<UnityEngine.Rendering.HighDefinition.HDAdditionalLightData>();
                l.lightUnit = UnityEngine.Rendering.LightUnit.Lumen; l.color = new Color(0.6f, 0.75f, 1f); l.range = 6f; l.intensity = 700f;
                l.shadows = LightShadows.None; l.enabled = false;
                go.AddComponent<TvScreen>();
            }
            return f;
        }
    }

    /// <summary>What was bought and what was sold, kept between sessions.</summary>
    public static class PurchaseSave
    {
        static string Key => SaveSystem.Key("dearlife.bought");
        static string SoldKey => SaveSystem.Key("dearlife.sold");
        [System.Serializable] class Item { public string key, id; public Vector3 pos; public float yaw; public string ta, tb; }
        [System.Serializable] class Data { public List<Item> items = new List<Item>(); public List<string> sold = new List<string>(); }
        static Data data;

        /// <summary>Clears the in-memory cache so a slot switch (a scene reload keeps static fields) reads the new slot's data.</summary>
        public static void ResetCache() { data = null; }

        static Data Load()
        {
            if (data != null) return data;
            data = new Data();
            if (PlayerPrefs.HasKey(Key)) { try { data = JsonUtility.FromJson<Data>(PlayerPrefs.GetString(Key)) ?? new Data(); } catch { data = new Data(); } }
            return data;
        }

        static void Save() { PlayerPrefs.SetString(Key, JsonUtility.ToJson(Load())); PlayerPrefs.Save(); }

        public static void Record(Furniture f)
        {
            var d = Load();
            d.items.RemoveAll(i => i.key == f.key);
            d.items.Add(new Item { key = f.key, id = InteractionTable.BaseId(f.name), pos = f.transform.position, yaw = f.transform.eulerAngles.y, ta = f.tintA, tb = f.tintB });
            Save();
        }

        /// <summary>Undo of a sale: the piece is back, so it is no longer on the sold list.</summary>
        public static void Unforget(Furniture f)
        {
            var d = Load();
            d.sold.Remove(f.key);
            Save();
        }

        public static void Forget(Furniture f)
        {
            var d = Load();
            if (f.bought) d.items.RemoveAll(i => i.key == f.key);
            else if (!d.sold.Contains(f.key)) d.sold.Add(f.key);
            Save();
        }

        public static void Restore(Catalog cat)
        {
            var d = Load();
            foreach (var i in d.items)
            {
                var e = cat.Find(i.id);
                if (e == null) continue;
                var made = FurnitureFactory.Create(e.prefab, i.id, i.pos, i.yaw, i.key, e.scale);
                if (!string.IsNullOrEmpty(i.ta)) made.SetTint(0, i.ta);
                if (!string.IsNullOrEmpty(i.tb)) made.SetTint(1, i.tb);
            }
            foreach (var f in Furniture.All.ToArray())
                if (!f.bought && d.sold.Contains(f.key)) Object.Destroy(f.gameObject);
        }

        public static void Clear() { data = new Data(); PlayerPrefs.DeleteKey(Key); }
    }

    /// <summary>The catalogue panel (key B): categories, what things cost, click to buy and place. Selling is the Delete key while a piece is held.</summary>
    public class BuyMode : MonoBehaviour
    {
        public static BuyMode Instance { get; private set; }
        public static bool Active { get; private set; }

        string category;
        float scroll;
        public static string shopTintA = "", shopTintB = "";
        readonly Dictionary<string, Texture2D> thumbs = new Dictionary<string, Texture2D>();

        void Awake() { Instance = this; }

        public static void Toggle()
        {
            Active = !Active;
            GameAudio.Play(GameAudio.Sfx.Click);
            if (Active && BuildMode.Active) BuildMode.Toggle();
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.B) && !Input.GetKey(KeyCode.LeftControl)) Toggle();
            if (Active && Input.GetKeyDown(KeyCode.Escape) && !DecorateMode.Instance.Holding) Active = false;
        }

        Texture2D Thumb(string id)
        {
            if (!thumbs.TryGetValue(id, out var t)) thumbs[id] = t = Resources.Load<Texture2D>("Thumbs/" + id);
            return t;
        }

        /// <summary>The Shop tab of the side panel, like the 2D game's: category chips, then cards with a picture, a name and a price.</summary>
        public void DrawShop(Rect area)
        {
            if (Catalog.Instance == null) return;
            if (category == null) category = "Living";
            float w = area.width;

            // the money and the categories stay put, the cards scroll
            Ui.Label(new Rect(area.x, area.y, w, 22f), $"Funds  {Household.Currency} {(Household.Instance ? Household.Instance.Funds : 0):N0}", 15f, Ui.GoldText, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            float cx = area.x, cy = area.y + 28f;
            foreach (var c in Catalog.Instance.Categories())
            {
                float cw = Ui.TextWidth(c, 12f, Ui.Weight.ExtraBold) + 22f;
                if (cx + cw > area.xMax) { cx = area.x; cy += 28f; }
                if (Ui.Chip(new Rect(cx, cy, cw, 24f), c, c == category, 12f)) { category = c; scroll = 0f; }
                cx += cw + 5f;
            }
            cy += 34f;

            // the colour the next thing you buy will come in (like choosing a swatch before placing)
            HouseHud.Kicker(area.x, cy, w, "Colour to buy it in");
            if (Ui.Chip(new Rect(area.xMax - 84f, cy - 4f, 84f, 20f), "As it came", string.IsNullOrEmpty(shopTintA) && string.IsNullOrEmpty(shopTintB), 10f)) { shopTintA = ""; shopTintB = ""; }
            cy += 20f;
            var pa = ColourPicker.Row(area.x, cy, w, Furniture.Fabrics, shopTintA, 19f);
            if (pa != null) shopTintA = pa;
            cy += ColourPicker.RowHeight(Furniture.Fabrics.Length, w, 19f) + 2f;
            var pb = ColourPicker.Row(area.x, cy, w, Furniture.Woods, shopTintB, 19f);
            if (pb != null) shopTintB = pb == "ffffff" ? "" : pb;
            cy += ColourPicker.RowHeight(Furniture.Woods.Length, w, 19f) + 6f;

            var entries = new List<CatalogEntry>();
            foreach (var e in Catalog.Instance.entries) if (e.category == category) entries.Add(e);
            const float cardH = 156f, gap = 10f;
            int rows = (entries.Count + 1) / 2;
            var view = new Rect(area.x, cy, w, area.yMax - cy);
            scroll = Ui.Scroll(view, scroll, rows * (cardH + gap) + 6f, cw2 =>
            {
                float cardW = (cw2 - gap) / 2f;
                for (int i = 0; i < entries.Count; i++)
                {
                    var e = entries[i];
                    var r = new Rect((i % 2) * (cardW + gap), (i / 2) * (cardH + gap), cardW, cardH);
                    bool afford = Household.CanAfford(e.Price);
                    if (Ui.CardButton(r))
                    {
                        if (!afford) { Household.Toast($"You need {Household.Currency} {e.Price:N0} for the {e.name.ToLower()}."); GameAudio.Play(GameAudio.Sfx.No); }
                        else Buy(e);
                    }
                    var img = Thumb(e.id);
                    var pic = new Rect(r.x + (cardW - 96f) * 0.5f, r.y + 8f, 96f, 96f);
                    if (img != null && Event.current.type == EventType.Repaint) { var o = GUI.color; GUI.color = afford ? Color.white : new Color(1f, 1f, 1f, 0.5f); GUI.DrawTexture(Ui.S(pic), img, ScaleMode.ScaleToFit, true); GUI.color = o; }
                    else { Ui.Round(pic, Ui.Cream2, 16f); Ui.Label(pic, e.name.Substring(0, 1), 34f, Ui.Pink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold); }
                    if (img != null && !string.IsNullOrEmpty(shopTintA) && Event.current.type == EventType.Repaint) { var tc = Ui.Hex(shopTintA); Ui.Round(new Rect(pic.xMax - 22f, pic.y + 2f, 18f, 18f), tc, 9f); Ui.Ring(new Rect(pic.xMax - 22f, pic.y + 2f, 18f, 18f), new Color(0f, 0f, 0f, 0.25f), 1.5f, 9f); }
                    Ui.Label(new Rect(r.x + 4f, r.y + 106f, cardW - 8f, 20f), e.name, 13f, Ui.Ink, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                    Ui.Label(new Rect(r.x + 4f, r.y + 128f, cardW - 8f, 20f), $"{Household.Currency} {e.Price:N0}", 13f, afford ? Ui.GoldText : Ui.Soft, TextAnchor.MiddleCenter, Ui.Weight.ExtraBold);
                }
            });
        }

        void Buy(CatalogEntry e)
        {
            if (e.id == "adopt_dog") { if (PetShop.Instance != null) PetShop.Instance.Adopt(); return; }
            var cam = Camera.main;
            Vector3 at = OrbitCamera.Instance ? OrbitCamera.Instance.pivot : new Vector3(10f, 0f, 8f);
            if (cam && Physics.Raycast(cam.ScreenPointToRay(new Vector3(Screen.width * 0.5f, Screen.height * 0.55f, 0f)), out var hit, 300f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;
            at.y = Mathf.Max(at.y, -0.3f);
            var f = FurnitureFactory.Create(e.prefab, e.id, at + Vector3.up * 0.05f, 0f, null, e.scale);
            f.pendingPrice = e.Price;
            if (!string.IsNullOrEmpty(shopTintA)) f.SetTint(0, shopTintA);
            if (!string.IsNullOrEmpty(shopTintB)) f.SetTint(1, shopTintB);
            GameAudio.Play(GameAudio.Sfx.Click);
            DecorateMode.Instance.BeginPlace(f);
        }
    }
}
