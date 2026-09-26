using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Build mode (key V): draw new walls and rooms on the floor you are looking at, cut doorways and windows into the walls you built,
    /// lay floors, paint walls and floors (the ones that came with the house too) and knock down what you built for a refund.
    /// Everything is kept between sessions. Walls cost RM 40 a metre, floors RM 12 a square metre, doorways RM 150 and windows RM 200.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        public static BuildMode Instance { get; private set; }
        public static bool Active { get; private set; }

        public enum Tool { Wall, Room, Doorway, Window, Floor, Paint, Demolish }

        [Tooltip("floor coverings, in the order of the palette")] public Material[] floorMaterials;
        public string[] floorNames;
        [Tooltip("wall finishes; more are made from the first one with colours")] public Material[] wallMaterials;
        public string[] wallNames;
        public Material previewMaterial;
        public Material glassMaterial;

        Tool tool = Tool.Wall;
        int floorIdx, wallIdx;
        Transform root;
        readonly List<Piece> pieces = new List<Piece>();
        readonly Stack<Piece> undo = new Stack<Piece>();

        const float Snap = 0.25f, Thick = 0.15f, Height = 3f;
        const float XMin = -2.7f, XMax = 33.9f, ZMin = -4.7f, ZMax = 22.7f;
        const int WallPerMetre = 40, FloorPerSqm = 12, DoorCost = 150, WindowCost = 200;
        const string SaveKey = "tiramisu.built", PaintKey = "tiramisu.painted";

        // ---- what is built
        [System.Serializable] public class Opening { public float u, w; public bool window; }
        [System.Serializable] public class PieceData { public bool isFloor; public Vector3 a, b; public float y; public int mat; public List<Opening> openings = new List<Opening>(); }
        [System.Serializable] class SaveData { public List<PieceData> pieces = new List<PieceData>(); }
        [System.Serializable] class PaintEntry { public string path; public int mat; public bool floor; }
        [System.Serializable] class PaintData { public List<PaintEntry> items = new List<PaintEntry>(); }

        public class Piece : MonoBehaviour { public PieceData d; public int cost; }

        PaintData painted = new PaintData();

        // ---- input state
        bool dragging; Vector3 dragStart, dragEnd;
        GameObject preview;
        Camera cam;
        float scroll;
        public Rect panel;
        string hint = "";

        void Awake() { Instance = this; }

        public static void Toggle()
        {
            Active = !Active;
            GameAudio.Play(GameAudio.Sfx.Click);
            if (Active && BuyMode.Active) BuyMode.Toggle();
            if (!Active && Instance) Instance.CancelDrag();
        }

        void Start()
        {
            root = new GameObject("Player built").transform;
            // more wall colours from the plain white one
            if (wallMaterials != null && wallMaterials.Length > 0)
            {
                var list = new List<Material>(wallMaterials); var names = new List<string>(wallNames);
                (string n, Color c)[] extra = { ("Blush", new Color(0.95f, 0.78f, 0.74f)), ("Sage", new Color(0.72f, 0.82f, 0.7f)), ("Sky", new Color(0.72f, 0.83f, 0.93f)), ("Sand", new Color(0.9f, 0.82f, 0.64f)), ("Charcoal", new Color(0.24f, 0.25f, 0.28f)), ("Teal", new Color(0.3f, 0.55f, 0.55f)), ("Butter", new Color(0.97f, 0.9f, 0.6f)) };
                foreach (var e in extra) { var m = new Material(wallMaterials[0]) { name = e.n }; m.SetColor("_BaseColor", e.c); list.Add(m); names.Add(e.n); }
                wallMaterials = list.ToArray(); wallNames = names.ToArray();
            }
            Load();
            ApplyPaint();
        }

        // ------------------------------------------------------------ input

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.V)) Toggle();
            if (!Active) { return; }
            if (!cam) cam = Camera.main;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (dragging) CancelDrag(); else Toggle(); return; }
            if (Input.GetKeyDown(KeyCode.Z) && undo.Count > 0) { Remove(undo.Pop(), true); }
            for (int i = 0; i < 7; i++) if (Input.GetKeyDown(KeyCode.Alpha1 + i) && false) tool = (Tool)i;

            bool overUi = OrbitCamera.IsOverUi != null && OrbitCamera.IsOverUi(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            OrbitCamera.Blocked = dragging;

            switch (tool)
            {
                case Tool.Wall: case Tool.Room: DrawTool(overUi); break;
                default: ClickTool(overUi); break;
            }
        }

        float FloorY => HouseView.Instance ? HouseView.Instance.ActiveFloorY : 0f;

        bool GroundPoint(out Vector3 p)
        {
            p = default;
            if (!cam) return false;
            var plane = new Plane(Vector3.up, new Vector3(0f, FloorY + 0.02f, 0f));
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (!plane.Raycast(ray, out float e)) return false;
            p = ray.GetPoint(e);
            p.x = Mathf.Clamp(Mathf.Round(p.x / Snap) * Snap, XMin, XMax);
            p.z = Mathf.Clamp(Mathf.Round(p.z / Snap) * Snap, ZMin, ZMax);
            p.y = FloorY;
            return true;
        }

        void DrawTool(bool overUi)
        {
            if (!GroundPoint(out var p)) return;
            if (!dragging)
            {
                hint = tool == Tool.Wall ? "Press and drag to draw a wall. Z undoes." : "Press and drag to make a room. Z undoes.";
                if (Input.GetMouseButtonDown(0) && !overUi) { dragging = true; dragStart = dragEnd = p; MakePreview(); }
                return;
            }
            OrbitCamera.Blocked = true;
            if (Input.GetMouseButtonDown(1)) { CancelDrag(); return; }
            dragEnd = p;
            if (tool == Tool.Wall)
            {
                // straight walls only: along whichever way you dragged further
                var d = dragEnd - dragStart;
                if (Mathf.Abs(d.x) >= Mathf.Abs(d.z)) dragEnd.z = dragStart.z; else dragEnd.x = dragStart.x;
            }
            UpdatePreview();
            if (Input.GetMouseButtonUp(0))
            {
                if (tool == Tool.Wall) MakeWall(dragStart, dragEnd, wallIdx, true);
                else MakeRoom(dragStart, dragEnd);
                CancelDrag();
            }
        }

        void MakePreview()
        {
            if (preview) Destroy(preview);
            preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(preview.GetComponent<Collider>());
            if (previewMaterial) preview.GetComponent<Renderer>().sharedMaterial = previewMaterial;
            preview.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        void UpdatePreview()
        {
            if (!preview) return;
            var a = dragStart; var b = dragEnd;
            var c = (a + b) * 0.5f;
            float sx = Mathf.Max(Mathf.Abs(b.x - a.x), Thick), sz = Mathf.Max(Mathf.Abs(b.z - a.z), Thick);
            if (tool == Tool.Wall) preview.transform.localScale = new Vector3(sx, Height, sz);
            else preview.transform.localScale = new Vector3(sx, 0.08f, sz);
            preview.transform.position = new Vector3(c.x, FloorY + (tool == Tool.Wall ? Height * 0.5f : 0.04f), c.z);
            hint = tool == Tool.Wall ? $"Wall {Mathf.Max(sx, sz):0.0} m · RM {Mathf.RoundToInt(Mathf.Max(sx, sz) * WallPerMetre)}" : $"Room {sx:0.0} x {sz:0.0} m · RM {Mathf.RoundToInt(sx * sz * FloorPerSqm + (sx + sz) * 2f * WallPerMetre)}";
        }

        void CancelDrag()
        {
            dragging = false;
            OrbitCamera.Blocked = false;
            if (preview) Destroy(preview);
        }

        void ClickTool(bool overUi)
        {
            hint = tool == Tool.Doorway ? "Click a wall you built to cut a doorway." : tool == Tool.Window ? "Click a wall you built to put a window in." :
                   tool == Tool.Floor ? "Click a floor to cover it with the chosen floor." : tool == Tool.Paint ? "Click a wall to paint it." : "Click something you built to knock it down (70% back).";
            if (!Input.GetMouseButtonDown(0) || overUi || !cam) return;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in hits)
            {
                var piece = h.collider.GetComponentInParent<Piece>();
                switch (tool)
                {
                    case Tool.Doorway: case Tool.Window:
                        if (piece && !piece.d.isFloor) { AddOpening(piece, h.point, tool == Tool.Window); return; }
                        break;
                    case Tool.Demolish:
                        if (piece) { Remove(piece, false); return; }
                        break;
                    case Tool.Floor:
                        if (h.normal.y > 0.8f) { if (piece && piece.d.isFloor) { piece.d.mat = floorIdx; RebuildFloor(piece); Save(); } else PaintRenderer(h.collider.GetComponentInChildren<Renderer>() ?? h.collider.GetComponent<Renderer>(), true); GameAudio.Play(GameAudio.Sfx.Place); return; }
                        break;
                    case Tool.Paint:
                        if (Mathf.Abs(h.normal.y) < 0.4f)
                        {
                            if (piece && !piece.d.isFloor) { piece.d.mat = wallIdx; Rebuild(piece); Save(); }
                            else PaintRenderer(h.collider.GetComponent<Renderer>() ?? h.collider.GetComponentInChildren<Renderer>(), false);
                            GameAudio.Play(GameAudio.Sfx.Place);
                            return;
                        }
                        break;
                }
            }
        }

        // ------------------------------------------------------------ making things

        Material WallMat(int i) => wallMaterials != null && wallMaterials.Length > 0 ? wallMaterials[Mathf.Clamp(i, 0, wallMaterials.Length - 1)] : null;
        Material FloorMat(int i) => floorMaterials != null && floorMaterials.Length > 0 ? floorMaterials[Mathf.Clamp(i, 0, floorMaterials.Length - 1)] : null;

        Piece MakeWall(Vector3 a, Vector3 b, int mat, bool charge)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.5f) { hint = "That wall is too short."; return null; }
            int cost = Mathf.RoundToInt(len * WallPerMetre);
            if (charge && !Household.Spend(cost, "a new wall")) return null;
            var d = new PieceData { a = a, b = b, y = a.y, mat = mat };
            var piece = Spawn(d, cost);
            if (charge) { undo.Push(piece); GameAudio.Play(GameAudio.Sfx.Place); Nav(); Save(); }
            return piece;
        }

        void MakeRoom(Vector3 a, Vector3 b)
        {
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
            if (x1 - x0 < 1f || z1 - z0 < 1f) { hint = "That room is too small."; return; }
            int cost = Mathf.RoundToInt((x1 - x0) * (z1 - z0) * FloorPerSqm + ((x1 - x0) + (z1 - z0)) * 2f * WallPerMetre);
            if (!Household.Spend(cost, "a new room")) return;
            float y = a.y;
            var fd = new PieceData { isFloor = true, a = new Vector3(x0, y, z0), b = new Vector3(x1, y, z1), y = y, mat = floorIdx };
            var f = Spawn(fd, Mathf.RoundToInt((x1 - x0) * (z1 - z0) * FloorPerSqm));
            undo.Push(f);
            foreach (var pair in new[] { (new Vector3(x0, y, z0), new Vector3(x1, y, z0)), (new Vector3(x1, y, z0), new Vector3(x1, y, z1)), (new Vector3(x1, y, z1), new Vector3(x0, y, z1)), (new Vector3(x0, y, z1), new Vector3(x0, y, z0)) })
            {
                var wd = new PieceData { a = pair.Item1, b = pair.Item2, y = y, mat = wallIdx };
                var w = Spawn(wd, Mathf.RoundToInt(Vector3.Distance(pair.Item1, pair.Item2) * WallPerMetre));
                undo.Push(w);
            }
            GameAudio.Play(GameAudio.Sfx.Buy);
            Nav(); Save();
        }

        Piece Spawn(PieceData d, int cost)
        {
            var go = new GameObject(d.isFloor ? "Built floor" : "Built wall");
            go.transform.SetParent(root, false);
            var p = go.AddComponent<Piece>();
            p.d = d; p.cost = cost;
            pieces.Add(p);
            Rebuild(p);
            return p;
        }

        void Rebuild(Piece p)
        {
            if (p.d.isFloor) { RebuildFloor(p); return; }
            foreach (Transform c in p.transform) Destroy(c.gameObject);
            var a = p.d.a; var b = p.d.b;
            float len = Vector3.Distance(a, b);
            var dir = (b - a).normalized;
            var mat = WallMat(p.d.mat);
            p.d.openings.Sort((x, y) => x.u.CompareTo(y.u));
            float u = 0f;
            foreach (var o in p.d.openings)
            {
                float o0 = Mathf.Clamp(o.u - o.w * 0.5f, 0f, len), o1 = Mathf.Clamp(o.u + o.w * 0.5f, 0f, len);
                if (o0 > u + 0.01f) Box(p, a, dir, u, o0, 0f, Height, mat);
                if (o.window)
                {
                    Box(p, a, dir, o0, o1, 0f, 0.9f, mat);
                    Box(p, a, dir, o0, o1, 2.1f, Height, mat);
                    if (glassMaterial) Box(p, a, dir, o0, o1, 0.9f, 2.1f, glassMaterial, 0.03f, false);
                }
                else Box(p, a, dir, o0, o1, 2.1f, Height, mat);
                u = o1;
            }
            if (len > u + 0.01f) Box(p, a, dir, u, len, 0f, Height, mat);
        }

        void Box(Piece p, Vector3 a, Vector3 dir, float u0, float u1, float y0, float y1, Material m, float thick = Thick, bool collide = true)
        {
            if (u1 - u0 < 0.005f || y1 - y0 < 0.005f) return;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall part";
            go.transform.SetParent(p.transform, false);
            var c = a + dir * ((u0 + u1) * 0.5f);
            go.transform.position = new Vector3(c.x, p.d.y + (y0 + y1) * 0.5f, c.z);
            go.transform.rotation = Quaternion.LookRotation(dir == Vector3.zero ? Vector3.forward : dir) * Quaternion.Euler(0f, 90f, 0f);
            go.transform.localScale = new Vector3(thick, y1 - y0, u1 - u0);
            go.transform.rotation = Quaternion.LookRotation(dir);
            go.transform.localScale = new Vector3(thick, y1 - y0, u1 - u0);
            if (m) go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collide) Destroy(go.GetComponent<Collider>());
        }

        void RebuildFloor(Piece p)
        {
            foreach (Transform c in p.transform) Destroy(c.gameObject);
            var a = p.d.a; var b = p.d.b;
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Floor slab";
            go.transform.SetParent(p.transform, false);
            go.transform.position = new Vector3((a.x + b.x) * 0.5f, p.d.y + 0.025f, (a.z + b.z) * 0.5f);
            go.transform.localScale = new Vector3(b.x - a.x, 0.05f, b.z - a.z);
            var m = FloorMat(p.d.mat);
            if (m) go.GetComponent<Renderer>().sharedMaterial = m;
        }

        void AddOpening(Piece p, Vector3 hit, bool window)
        {
            var a = p.d.a; var b = p.d.b;
            float len = Vector3.Distance(a, b);
            var dir = (b - a).normalized;
            float u = Vector3.Dot(hit - a, dir);
            float w = window ? 1.4f : 1.0f;
            u = Mathf.Round(u / 0.25f) * 0.25f;
            u = Mathf.Clamp(u, w * 0.5f + 0.15f, len - w * 0.5f - 0.15f);
            if (len < w + 0.3f) { hint = "That wall is too short for it."; GameAudio.Play(GameAudio.Sfx.No); return; }
            foreach (var o in p.d.openings) if (Mathf.Abs(o.u - u) < (o.w + w) * 0.5f + 0.05f) { hint = "There is not enough room next to the other opening."; GameAudio.Play(GameAudio.Sfx.No); return; }
            int cost = window ? WindowCost : DoorCost;
            if (!Household.Spend(cost, window ? "a window" : "a doorway")) return;
            p.d.openings.Add(new Opening { u = u, w = w, window = window });
            p.cost += cost;
            Rebuild(p);
            GameAudio.Play(GameAudio.Sfx.Place);
            Nav(); Save();
        }

        void Remove(Piece p, bool undone)
        {
            if (!p) return;
            int refund = Mathf.RoundToInt(p.cost * (undone ? 1f : 0.7f));
            Household.Earn(refund, undone ? "Undo" : "Knocked down what was built");
            pieces.Remove(p);
            Destroy(p.gameObject);
            GameAudio.Play(GameAudio.Sfx.Sell);
            Nav(); Save();
        }

        static void Nav() { if (TiramisuNav.Instance) TiramisuNav.Instance.RequestRebuild(); }

        // ------------------------------------------------------------ painting what the house came with

        static string PathOf(Transform t)
        {
            var s = t.name + "@" + t.GetSiblingIndex();
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        void PaintRenderer(Renderer r, bool floor)
        {
            if (!r) return;
            var m = floor ? FloorMat(floorIdx) : WallMat(wallIdx);
            if (!m) return;
            r.sharedMaterial = m;
            string path = PathOf(r.transform);
            painted.items.RemoveAll(i => i.path == path);
            painted.items.Add(new PaintEntry { path = path, mat = floor ? floorIdx : wallIdx, floor = floor });
            PlayerPrefs.SetString(PaintKey, JsonUtility.ToJson(painted));
        }

        void ApplyPaint()
        {
            if (!PlayerPrefs.HasKey(PaintKey)) return;
            try { painted = JsonUtility.FromJson<PaintData>(PlayerPrefs.GetString(PaintKey)) ?? new PaintData(); } catch { painted = new PaintData(); }
            var all = new Dictionary<string, Renderer>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include)) all[PathOf(r.transform)] = r;
            foreach (var e in painted.items)
                if (all.TryGetValue(e.path, out var r)) { var m = e.floor ? FloorMat(e.mat) : WallMat(e.mat); if (m) r.sharedMaterial = m; }
        }

        // ------------------------------------------------------------ saving

        void Save()
        {
            var sd = new SaveData();
            foreach (var p in pieces) if (p) sd.pieces.Add(p.d);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(sd));
            PlayerPrefs.Save();
        }

        void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            SaveData sd = null;
            try { sd = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)); } catch { }
            if (sd == null) return;
            foreach (var d in sd.pieces)
            {
                int cost = d.isFloor ? Mathf.RoundToInt((d.b.x - d.a.x) * (d.b.z - d.a.z) * FloorPerSqm) : Mathf.RoundToInt(Vector3.Distance(d.a, d.b) * WallPerMetre);
                foreach (var o in d.openings) cost += o.window ? WindowCost : DoorCost;
                Spawn(d, cost);
            }
            Nav();
        }

        public static void ClearAll() { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.DeleteKey(PaintKey); }

        // ------------------------------------------------------------ the panel

        static readonly string[] ToolNames = { "Wall", "Room", "Doorway", "Window", "Floor", "Paint", "Knock down" };
        static readonly string[] ToolNotes =
        {
            "Drag to draw a wall.\nRM 40 a metre", "Drag a box: walls and a floor.", "Click a wall to cut a door.\nRM 150", "Click a wall for a window.\nRM 200",
            "Drag to lay a new floor.\nRM 12 a square metre", "Click a wall or floor to paint it.", "Click what you built to take it away.",
        };

        /// <summary>The Build tab of the side panel: the tools as cards, then floors and wall finishes as chips, then a hint.</summary>
        public void DrawBuild(Rect area)
        {
            float w = area.width, y = area.y;
            Ui.Label(new Rect(area.x, y, w, 22f), $"Funds  {Household.Currency} {(Household.Instance ? Household.Instance.Funds : 0):N0}", 15f, Ui.GoldText, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            y += 30f;
            scroll = Ui.Scroll(new Rect(area.x, y, w, area.yMax - y), scroll, 640f, cw =>
            {
                float yy = 0f;
                HouseHud.Kicker(0f, yy, cw, "Tools"); yy += 20f;
                float gap = 8f, tw = (cw - gap) / 2f, th = 74f;
                for (int i = 0; i < ToolNames.Length; i++)
                {
                    var r = new Rect((i % 2) * (tw + gap), yy + (i / 2) * (th + gap), tw, th);
                    bool on = (int)tool == i;
                    if (Ui.CardButton(r, on)) { tool = (Tool)i; CancelDrag(); }
                    Ui.Label(new Rect(r.x + 10f, r.y + 6f, tw - 20f, 20f), ToolNames[i], 14f, on ? Ui.Accent : Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                    Ui.Label(new Rect(r.x + 10f, r.y + 26f, tw - 20f, 44f), ToolNotes[i], 11f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
                }
                yy += ((ToolNames.Length + 1) / 2) * (th + gap) + 8f;

                bool floors = tool == Tool.Floor || tool == Tool.Room;
                bool walls = tool == Tool.Wall || tool == Tool.Room || tool == Tool.Paint;
                if (floors)
                {
                    HouseHud.Kicker(0f, yy, cw, "Floor"); yy += 20f;
                    float cx = 0f;
                    for (int i = 0; floorNames != null && i < floorNames.Length; i++)
                    {
                        float chw = Ui.TextWidth(floorNames[i], 12f, Ui.Weight.ExtraBold) + 24f;
                        if (cx + chw > cw) { cx = 0f; yy += 30f; }
                        if (Ui.Chip(new Rect(cx, yy, chw, 26f), floorNames[i], floorIdx == i, 12f)) floorIdx = i;
                        cx += chw + 5f;
                    }
                    yy += 40f;
                }
                if (walls)
                {
                    HouseHud.Kicker(0f, yy, cw, "Wall finish"); yy += 20f;
                    float cx = 0f;
                    for (int i = 0; wallNames != null && i < wallNames.Length; i++)
                    {
                        float chw = Ui.TextWidth(wallNames[i], 12f, Ui.Weight.ExtraBold) + 24f;
                        if (cx + chw > cw) { cx = 0f; yy += 30f; }
                        if (Ui.Chip(new Rect(cx, yy, chw, 26f), wallNames[i], wallIdx == i, 12f)) wallIdx = i;
                        cx += chw + 5f;
                    }
                    yy += 40f;
                }
                var box = new Rect(0f, yy, cw, 96f);
                Ui.Round(box, Ui.Pale, 14f);
                string floorName = HouseView.Instance && HouseView.Instance.ActiveFloor == 1 ? "upper" : "ground";
                Ui.Label(new Rect(10f, yy + 8f, cw - 20f, 82f), (string.IsNullOrEmpty(hint) ? "Pick a tool." : hint) + $"\nZ undoes, Esc leaves build mode. You are on the {floorName} floor.", 12f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
            });
        }
    }
}
