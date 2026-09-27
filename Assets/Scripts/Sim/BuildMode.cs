using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Build mode (key V), in the spirit of the Sims 4: a grid under the mouse, live measurements and prices while you drag, full, half
    /// and low walls, diagonal walls (hold Shift), rooms, floors, archways and windows in several sizes with a preview of where they will go,
    /// paint and an eyedropper, and knocking down what you built. Everything can be undone and redone (Z, Ctrl+Z, Ctrl+Y or the top bar) and
    /// is kept between sessions. Prices: walls RM 15 to 40 a metre, floors RM 12 a square metre, archways from RM 150, windows from RM 140.
    /// </summary>
    public class BuildMode : MonoBehaviour
    {
        public static BuildMode Instance { get; private set; }
        public static bool Active { get; private set; }

        public enum Tool { Wall, Room, Floor, Doorway, Window, Paint, Eyedropper, Demolish }

        [Tooltip("floor coverings, in the order of the palette")] public Material[] floorMaterials;
        public string[] floorNames;
        [Tooltip("wall finishes; more are made from the first one with colours")] public Material[] wallMaterials;
        public string[] wallNames;
        public Material previewMaterial;
        public Material glassMaterial;
        public Rect panel;

        Tool tool = Tool.Wall;
        int floorIdx, wallIdx, heightIdx, doorStyle, windowStyle;
        Transform root;
        Material frameMat;
        readonly List<Piece> pieces = new List<Piece>();

        const float Snap = 0.25f, Thick = 0.15f, Height = 3f;
        const float XMin = -2.7f, XMax = 33.9f, ZMin = -4.7f, ZMax = 22.7f;
        const int FloorPerSqm = 12;
        const string SaveKey = "tiramisu.built", PaintKey = "tiramisu.painted";

        static readonly (string name, float h)[] Heights = { ("Full wall", 3f), ("Half wall", 1.2f), ("Low wall", 0.6f) };
        static readonly (string name, float w, float sill, float top, int cost)[] Doors =
        {
            ("Archway", 1.0f, 0f, 2.1f, 150), ("Wide archway", 1.8f, 0f, 2.1f, 240), ("Tall archway", 1.0f, 0f, 2.6f, 190),
        };
        static readonly (string name, float w, float sill, float top, int cost)[] Windows =
        {
            ("Window", 1.4f, 0.9f, 2.1f, 200), ("Small window", 0.9f, 1.1f, 1.9f, 140), ("Wide window", 2.2f, 0.9f, 2.1f, 320), ("Floor window", 1.4f, 0f, 2.6f, 300),
        };
        static int PerMetre(float h) => h > 2f ? 40 : h > 1f ? 25 : 15;

        // ---- what is built
        [System.Serializable] public class Opening { public float u, w, sill, top; public bool window; }
        [System.Serializable] public class PieceData { public bool isFloor; public Vector3 a, b; public float y, h; public int mat; public List<Opening> openings = new List<Opening>(); }
        [System.Serializable] class SaveData { public List<PieceData> pieces = new List<PieceData>(); }
        [System.Serializable] class PaintEntry { public string path; public int mat; public bool floor; }
        [System.Serializable] class PaintData { public List<PaintEntry> items = new List<PaintEntry>(); }
        [System.Serializable] class Snapshot { public string built, paint; }

        public class Piece : MonoBehaviour { public PieceData d; public int cost; }

        PaintData painted = new PaintData();
        readonly Dictionary<string, Material> originalMat = new Dictionary<string, Material>();
        Dictionary<string, Renderer> pathMap;

        // ---- history
        class Op { public Snapshot before, after; public int money; public string what; }
        readonly List<Op> undoOps = new List<Op>(), redoOps = new List<Op>();
        public bool CanUndo => undoOps.Count > 0;
        public bool CanRedo => redoOps.Count > 0;

        // ---- input state
        bool dragging; Vector3 dragStart, dragEnd;
        GameObject preview, ghost;
        Camera cam;
        float scroll, contentH = 760f;
        string hint = "";
        string hoverNow, hoverPrev;
        bool cursorValid; Vector3 cursor;
        string dragCost = "";

        void Awake() { Instance = this; }

        public static void Toggle()
        {
            Active = !Active;
            GameAudio.Play(GameAudio.Sfx.Click);
            if (Active && BuyMode.Active) BuyMode.Toggle();
            if (!Active && Instance) { Instance.CancelDrag(); Instance.HideGhost(); }
        }

        void Start()
        {
            root = new GameObject("Player built").transform;
            // more wall colours from the plain white one
            if (wallMaterials != null && wallMaterials.Length > 0)
            {
                var list = new List<Material>(wallMaterials); var names = new List<string>(wallNames);
                (string n, Color c)[] extra =
                {
                    ("Blush", new Color(0.95f, 0.78f, 0.74f)), ("Sage", new Color(0.72f, 0.82f, 0.7f)), ("Sky", new Color(0.72f, 0.83f, 0.93f)), ("Sand", new Color(0.9f, 0.82f, 0.64f)),
                    ("Charcoal", new Color(0.24f, 0.25f, 0.28f)), ("Teal", new Color(0.3f, 0.55f, 0.55f)), ("Butter", new Color(0.97f, 0.9f, 0.6f)), ("Lilac", new Color(0.78f, 0.72f, 0.9f)),
                    ("Terracotta", new Color(0.78f, 0.45f, 0.34f)), ("Navy", new Color(0.18f, 0.26f, 0.42f)),
                };
                foreach (var e in extra) { var m = new Material(wallMaterials[0]) { name = e.n }; m.SetColor("_BaseColor", e.c); list.Add(m); names.Add(e.n); }
                wallMaterials = list.ToArray(); wallNames = names.ToArray();
                frameMat = new Material(wallMaterials[0]) { name = "Window frame" };
                frameMat.SetColor("_BaseColor", new Color(0.04f, 0.04f, 0.05f));
                if (frameMat.HasProperty("_Smoothness")) frameMat.SetFloat("_Smoothness", 0.6f);
            }
            pathMap = new Dictionary<string, Renderer>();
            foreach (var r in FindObjectsByType<Renderer>(FindObjectsInactive.Include)) pathMap[PathOf(r.transform)] = r;
            Load();
            ApplyPaint();
        }

        // ------------------------------------------------------------ history

        Snapshot Capture()
        {
            var sd = new SaveData();
            foreach (var p in pieces) if (p) sd.pieces.Add(p.d);
            return new Snapshot { built = JsonUtility.ToJson(sd), paint = JsonUtility.ToJson(painted) };
        }

        void Commit(Snapshot before, int moneySpent, string what)
        {
            undoOps.Add(new Op { before = before, after = Capture(), money = moneySpent, what = what });
            if (undoOps.Count > 80) undoOps.RemoveAt(0);
            redoOps.Clear();
            Save();
        }

        void Restore(Snapshot s)
        {
            foreach (var p in pieces.ToArray()) if (p) Destroy(p.gameObject);
            pieces.Clear();
            var sd = JsonUtility.FromJson<SaveData>(s.built);
            if (sd != null) foreach (var d in sd.pieces) Spawn(d, CostOf(d));
            var pd = JsonUtility.FromJson<PaintData>(s.paint) ?? new PaintData();
            // what is no longer painted goes back to how it was
            foreach (var e in painted.items)
                if (!pd.items.Exists(x => x.path == e.path) && originalMat.TryGetValue(e.path, out var om) && pathMap.TryGetValue(e.path, out var r) && r) r.sharedMaterial = om;
            painted = pd;
            ApplyPaintEntries();
            Save(); Nav();
        }

        public void UndoBuild()
        {
            if (undoOps.Count == 0) { hint = "Nothing to undo."; return; }
            var o = undoOps[undoOps.Count - 1];
            if (o.money < 0 && !Household.Spend(-o.money, "Undid: " + o.what)) { hint = "You cannot afford to undo that right now."; GameAudio.Play(GameAudio.Sfx.No); return; }
            if (o.money > 0) Household.Earn(o.money, "Undid: " + o.what);
            undoOps.RemoveAt(undoOps.Count - 1);
            Restore(o.before);
            redoOps.Add(o);
            hint = "Undid: " + o.what + ".";
            GameAudio.Play(GameAudio.Sfx.Sell);
        }

        public void RedoBuild()
        {
            if (redoOps.Count == 0) { hint = "Nothing to redo."; return; }
            var o = redoOps[redoOps.Count - 1];
            if (o.money > 0 && !Household.Spend(o.money, o.what)) { hint = "You cannot afford that right now."; GameAudio.Play(GameAudio.Sfx.No); return; }
            if (o.money < 0) Household.Earn(-o.money, o.what);
            redoOps.RemoveAt(redoOps.Count - 1);
            Restore(o.after);
            undoOps.Add(o);
            hint = "Did it again: " + o.what + ".";
            GameAudio.Play(GameAudio.Sfx.Place);
        }

        // ------------------------------------------------------------ input

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.V)) Toggle();
            if (!Active) return;
            if (!cam) cam = Camera.main;
            if (Input.GetKeyDown(KeyCode.Escape)) { if (dragging) CancelDrag(); else Toggle(); return; }
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            if (Input.GetKeyDown(KeyCode.Z)) { if (ctrl && shift) RedoBuild(); else UndoBuild(); }
            if (ctrl && Input.GetKeyDown(KeyCode.Y)) RedoBuild();

            bool overUi = OrbitCamera.IsOverUi != null && OrbitCamera.IsOverUi(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            OrbitCamera.Blocked = dragging;
            cursorValid = false;

            switch (tool)
            {
                case Tool.Wall: case Tool.Room: case Tool.Floor: DragTool(overUi); HideGhostIfNotHovering(); break;
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

        // ---- drawing walls, rooms and floors by dragging

        void DragTool(bool overUi)
        {
            if (!GroundPoint(out var p)) return;
            cursor = p; cursorValid = !overUi;
            if (!dragging)
            {
                hint = tool == Tool.Wall ? "Press and drag to draw a wall. Hold Shift for diagonal walls."
                     : tool == Tool.Room ? "Press and drag a box to make a room with its walls and floor."
                     : "Drag a box to lay a floor. A single click repaints the floor under it.";
                if (Input.GetMouseButtonDown(0) && !overUi) { dragging = true; dragStart = dragEnd = p; MakePreview(); }
                return;
            }
            OrbitCamera.Blocked = true;
            cursorValid = true;
            if (Input.GetMouseButtonDown(1)) { CancelDrag(); return; }
            dragEnd = p;
            if (tool == Tool.Wall)
            {
                var d = dragEnd - dragStart;
                if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
                {
                    // diagonal walls: the direction snaps to steps of 45 degrees
                    float ang = Mathf.Round(Mathf.Atan2(d.z, d.x) * Mathf.Rad2Deg / 45f) * 45f * Mathf.Deg2Rad;
                    float len = Mathf.Round(d.magnitude / Snap) * Snap;
                    dragEnd = dragStart + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * len;
                }
                else if (Mathf.Abs(d.x) >= Mathf.Abs(d.z)) dragEnd.z = dragStart.z; else dragEnd.x = dragStart.x;
            }
            UpdatePreview();
            if (Input.GetMouseButtonUp(0))
            {
                float dx = Mathf.Abs(dragEnd.x - dragStart.x), dz = Mathf.Abs(dragEnd.z - dragStart.z);
                if (tool == Tool.Wall) MakeWall(dragStart, dragEnd, wallIdx, HeightOptions.h, true);
                else if (tool == Tool.Room) MakeRoom(dragStart, dragEnd);
                else if (dx < 0.6f && dz < 0.6f) PaintFloorAtMouse();
                else MakeFloor(dragStart, dragEnd);
                CancelDrag();
            }
        }

        (string name, float h) HeightOptions => Heights[heightIdx];

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
            if (tool == Tool.Wall)
            {
                float len = Mathf.Max(Vector3.Distance(a, b), 0.05f); float h = HeightOptions.h;
                var dir = len > 0.06f ? (b - a).normalized : Vector3.right;
                preview.transform.SetPositionAndRotation(new Vector3(c.x, FloorY + h * 0.5f, c.z), Quaternion.LookRotation(dir));
                preview.transform.localScale = new Vector3(Thick, h, len);
                dragCost = len < 0.5f ? "Too short" : $"RM {Mathf.RoundToInt(len * PerMetre(h))}";
            }
            else
            {
                float sx = Mathf.Max(Mathf.Abs(b.x - a.x), Thick), sz = Mathf.Max(Mathf.Abs(b.z - a.z), Thick);
                bool room = tool == Tool.Room;
                float h = room ? HeightOptions.h : 0.08f;
                preview.transform.SetPositionAndRotation(new Vector3(c.x, FloorY + (room ? 0.04f : 0.04f), c.z), Quaternion.identity);
                preview.transform.localScale = new Vector3(sx, room ? 0.08f : 0.08f, sz);
                int cost = Mathf.RoundToInt(sx * sz * FloorPerSqm + (room ? (sx + sz) * 2f * PerMetre(HeightOptions.h) : 0f));
                dragCost = (room && (sx < 1f || sz < 1f)) ? "Too small" : $"RM {cost}";
            }
        }

        void CancelDrag()
        {
            dragging = false;
            OrbitCamera.Blocked = false;
            if (preview) Destroy(preview);
            dragCost = "";
        }

        // ---- the ghost that shows what a click would do

        void ShowGhost(Vector3 centre, Quaternion rot, Vector3 scale, bool ok)
        {
            if (!ghost)
            {
                ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(ghost.GetComponent<Collider>());
                var r = ghost.GetComponent<Renderer>();
                if (previewMaterial) r.material = previewMaterial;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(centre, rot);
            ghost.transform.localScale = scale;
            var mr = ghost.GetComponent<Renderer>();
            var col = ok ? new Color(0.45f, 1f, 0.6f, 0.6f) : new Color(1f, 0.35f, 0.35f, 0.6f);
            if (mr.material.HasProperty("_BaseColor")) mr.material.SetColor("_BaseColor", col);
        }

        void HideGhost() { if (ghost) ghost.SetActive(false); }
        void HideGhostIfNotHovering() { HideGhost(); }

        // ---- clicking on things that are already there

        Piece PieceUnderMouse(out RaycastHit hit, out RaycastHit[] all)
        {
            hit = default; all = null;
            if (!cam) return null;
            all = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(all, (x, y) => x.distance.CompareTo(y.distance));
            foreach (var h in all)
            {
                var p = h.collider.GetComponentInParent<Piece>();
                if (p) { hit = h; return p; }
                break;
            }
            return null;
        }

        void ClickTool(bool overUi)
        {
            HideGhost();
            hint = tool == Tool.Doorway ? "Move over a wall you built: the preview shows where the archway goes. Click to cut it."
                 : tool == Tool.Window ? "Move over a wall you built: the preview shows where the window goes. Click to put it in."
                 : tool == Tool.Paint ? "Click a wall (or a floor) to paint it with the chosen finish."
                 : tool == Tool.Eyedropper ? "Click a wall or a floor to pick up its finish."
                 : "Click something you built to knock it down (70% back).";
            if (overUi || !cam) return;

            var piece = PieceUnderMouse(out var pieceHit, out var hits);
            bool click = Input.GetMouseButtonDown(0);

            switch (tool)
            {
                case Tool.Doorway: case Tool.Window:
                {
                    if (piece == null || piece.d.isFloor) return;
                    bool window = tool == Tool.Window;
                    var spec = window ? Windows[windowStyle] : Doors[doorStyle];
                    var a = piece.d.a; var b = piece.d.b; float len = Vector3.Distance(a, b); var dir = (b - a).normalized;
                    float u = Mathf.Round(Vector3.Dot(pieceHit.point - a, dir) / Snap) * Snap;
                    u = Mathf.Clamp(u, spec.w * 0.5f + 0.15f, Mathf.Max(spec.w * 0.5f + 0.15f, len - spec.w * 0.5f - 0.15f));
                    string why = OpeningProblem(piece, u, spec.w, spec.top, len);
                    var c = a + dir * u;
                    ShowGhost(new Vector3(c.x, piece.d.y + (spec.sill + spec.top) * 0.5f, c.z), Quaternion.LookRotation(dir), new Vector3(Thick + 0.08f, spec.top - spec.sill, spec.w), why == null);
                    cursorValid = true; cursor = new Vector3(c.x, piece.d.y + spec.top, c.z);
                    dragCost = why ?? $"RM {spec.cost}";
                    if (why != null) hint = why;
                    if (click) { if (why != null) { GameAudio.Play(GameAudio.Sfx.No); return; } AddOpening(piece, u, window, spec); }
                    return;
                }
                case Tool.Demolish:
                    if (piece != null)
                    {
                        var bnd = piece.GetComponentInChildren<Collider>().bounds;
                        foreach (var col in piece.GetComponentsInChildren<Collider>()) bnd.Encapsulate(col.bounds);
                        ShowGhost(bnd.center, Quaternion.identity, bnd.size + Vector3.one * 0.04f, false);
                        dragCost = $"+ RM {Mathf.RoundToInt(piece.cost * 0.7f)}"; cursorValid = true; cursor = bnd.center + Vector3.up * bnd.extents.y;
                        if (click) Remove(piece);
                    }
                    return;
            }

            // paint and eyedropper work on what you built and on what the house came with
            foreach (var h in hits)
            {
                bool floorFace = h.normal.y > 0.8f, wallFace = Mathf.Abs(h.normal.y) < 0.4f;
                if (!floorFace && !wallFace) continue;
                var pc = h.collider.GetComponentInParent<Piece>();
                bool isFloor = floorFace;
                var rend = h.collider.GetComponent<Renderer>() ?? h.collider.GetComponentInChildren<Renderer>();
                if (rend == null && pc == null) continue;
                var bnd = h.collider.bounds;
                ShowGhost(bnd.center, Quaternion.identity, bnd.size + Vector3.one * 0.03f, true);
                cursorValid = true; cursor = h.point;
                if (!click) return;
                if (tool == Tool.Eyedropper) { Pick(pc, rend, isFloor); return; }
                var before = Capture();
                if (pc != null) { if (pc.d.isFloor != isFloor) continue; pc.d.mat = isFloor ? floorIdx : wallIdx; Rebuild(pc); }
                else if (!PaintRenderer(rend, isFloor)) return;
                Commit(before, 0, isFloor ? "painted a floor" : "painted a wall");
                GameAudio.Play(GameAudio.Sfx.Place);
                return;
            }
        }

        void Pick(Piece pc, Renderer rend, bool isFloor)
        {
            int found = -1;
            if (pc != null) found = pc.d.mat;
            else if (rend != null)
            {
                var arr = isFloor ? floorMaterials : wallMaterials;
                for (int i = 0; arr != null && i < arr.Length; i++) if (arr[i] == rend.sharedMaterial) found = i;
            }
            if (found < 0) { hint = "That finish is not in the palette."; GameAudio.Play(GameAudio.Sfx.No); return; }
            if (isFloor) floorIdx = found; else wallIdx = found;
            hint = "Picked up " + (isFloor ? floorNames[found] : wallNames[found]) + ".";
            GameAudio.Play(GameAudio.Sfx.Click);
        }

        void PaintFloorAtMouse()
        {
            var piece = PieceUnderMouse(out var hit, out var hits);
            var before = Capture();
            foreach (var h in hits)
            {
                if (h.normal.y < 0.8f) continue;
                var pc = h.collider.GetComponentInParent<Piece>();
                if (pc != null && pc.d.isFloor) { pc.d.mat = floorIdx; RebuildFloor(pc); Commit(before, 0, "changed a floor"); GameAudio.Play(GameAudio.Sfx.Place); return; }
                var rend = h.collider.GetComponent<Renderer>() ?? h.collider.GetComponentInChildren<Renderer>();
                if (rend != null && PaintRenderer(rend, true)) { Commit(before, 0, "painted a floor"); GameAudio.Play(GameAudio.Sfx.Place); }
                return;
            }
        }

        string OpeningProblem(Piece p, float u, float w, float top, float len)
        {
            float h = p.d.h > 0f ? p.d.h : Height;
            if (h < top + 0.05f) return "That wall is too low for it.";
            if (len < w + 0.3f) return "That wall is too short for it.";
            foreach (var o in p.d.openings) if (Mathf.Abs(o.u - u) < (o.w + w) * 0.5f + 0.05f) return "There is not enough room next to the other opening.";
            return null;
        }

        // ------------------------------------------------------------ making things

        Material WallMat(int i) => wallMaterials != null && wallMaterials.Length > 0 ? wallMaterials[Mathf.Clamp(i, 0, wallMaterials.Length - 1)] : null;
        Material FloorMat(int i) => floorMaterials != null && floorMaterials.Length > 0 ? floorMaterials[Mathf.Clamp(i, 0, floorMaterials.Length - 1)] : null;

        static float HeightOf(PieceData d) => d.h > 0f ? d.h : Height;

        static int CostOf(PieceData d)
        {
            if (d.isFloor) return Mathf.RoundToInt((d.b.x - d.a.x) * (d.b.z - d.a.z) * FloorPerSqm);
            int cost = Mathf.RoundToInt(Vector3.Distance(d.a, d.b) * PerMetre(HeightOf(d)));
            foreach (var o in d.openings) cost += o.window ? 200 : 150;
            return cost;
        }

        Piece MakeWall(Vector3 a, Vector3 b, int mat, float h, bool charge)
        {
            float len = Vector3.Distance(a, b);
            if (len < 0.5f) { hint = "That wall is too short."; return null; }
            int cost = Mathf.RoundToInt(len * PerMetre(h));
            var before = Capture();
            if (charge && !Household.Spend(cost, "a new wall")) { GameAudio.Play(GameAudio.Sfx.No); return null; }
            var d = new PieceData { a = a, b = b, y = a.y, h = h, mat = mat };
            var piece = Spawn(d, cost);
            if (charge) { Commit(before, cost, "a wall"); GameAudio.Play(GameAudio.Sfx.Place); Nav(); }
            return piece;
        }

        void MakeRoom(Vector3 a, Vector3 b)
        {
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
            if (x1 - x0 < 1f || z1 - z0 < 1f) { hint = "That room is too small."; return; }
            float h = HeightOptions.h;
            int floorCost = Mathf.RoundToInt((x1 - x0) * (z1 - z0) * FloorPerSqm);
            int wallCost = Mathf.RoundToInt(((x1 - x0) + (z1 - z0)) * 2f * PerMetre(h));
            var before = Capture();
            if (!Household.Spend(floorCost + wallCost, "a new room")) { GameAudio.Play(GameAudio.Sfx.No); return; }
            float y = a.y;
            Spawn(new PieceData { isFloor = true, a = new Vector3(x0, y, z0), b = new Vector3(x1, y, z1), y = y, mat = floorIdx }, floorCost);
            foreach (var pair in new[] { (new Vector3(x0, y, z0), new Vector3(x1, y, z0)), (new Vector3(x1, y, z0), new Vector3(x1, y, z1)), (new Vector3(x1, y, z1), new Vector3(x0, y, z1)), (new Vector3(x0, y, z1), new Vector3(x0, y, z0)) })
                Spawn(new PieceData { a = pair.Item1, b = pair.Item2, y = y, h = h, mat = wallIdx }, Mathf.RoundToInt(Vector3.Distance(pair.Item1, pair.Item2) * PerMetre(h)));
            Commit(before, floorCost + wallCost, "a room");
            GameAudio.Play(GameAudio.Sfx.Buy);
            Nav();
        }

        void MakeFloor(Vector3 a, Vector3 b)
        {
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), z0 = Mathf.Min(a.z, b.z), z1 = Mathf.Max(a.z, b.z);
            if (x1 - x0 < 0.5f || z1 - z0 < 0.5f) { hint = "That floor is too small."; return; }
            int cost = Mathf.RoundToInt((x1 - x0) * (z1 - z0) * FloorPerSqm);
            var before = Capture();
            if (!Household.Spend(cost, "a new floor")) { GameAudio.Play(GameAudio.Sfx.No); return; }
            Spawn(new PieceData { isFloor = true, a = new Vector3(x0, a.y, z0), b = new Vector3(x1, a.y, z1), y = a.y, mat = floorIdx }, cost);
            Commit(before, cost, "a floor");
            GameAudio.Play(GameAudio.Sfx.Place);
            Nav();
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
            float len = Vector3.Distance(a, b), H = HeightOf(p.d);
            var dir = (b - a).normalized;
            var mat = WallMat(p.d.mat);
            p.d.openings.Sort((x, y) => x.u.CompareTo(y.u));
            float u = 0f;
            foreach (var o in p.d.openings)
            {
                float sill = o.top > 0f ? o.sill : (o.window ? 0.9f : 0f), top = o.top > 0f ? o.top : 2.1f;   // walls saved before there were styles
                float o0 = Mathf.Clamp(o.u - o.w * 0.5f, 0f, len), o1 = Mathf.Clamp(o.u + o.w * 0.5f, 0f, len);
                if (o0 > u + 0.01f) Box(p, a, dir, len, u, o0, 0f, H, mat);
                if (sill > 0.01f) Box(p, a, dir, len, o0, o1, 0f, sill, mat);
                if (top < H - 0.01f) Box(p, a, dir, len, o0, o1, top, H, mat);
                if (o.window)
                {
                    if (glassMaterial) Box(p, a, dir, len, o0, o1, sill, top, glassMaterial, 0.03f, sill < 0.5f);
                    if (frameMat)
                    {
                        const float f = 0.05f, ft = Thick + 0.02f;
                        Box(p, a, dir, len, o0, o0 + f, sill, top, frameMat, ft, false);
                        Box(p, a, dir, len, o1 - f, o1, sill, top, frameMat, ft, false);
                        Box(p, a, dir, len, o0, o1, top - f, top, frameMat, ft, false);
                        Box(p, a, dir, len, o0, o1, sill, sill + f, frameMat, ft, false);
                    }
                }
                u = o1;
            }
            if (len > u + 0.01f) Box(p, a, dir, len, u, len, 0f, H, mat);
        }

        void Box(Piece p, Vector3 a, Vector3 dir, float len, float u0, float u1, float y0, float y1, Material m, float thick = Thick, bool collide = true)
        {
            if (u1 - u0 < 0.005f || y1 - y0 < 0.005f) return;
            // the ends of a wall reach half a thickness further so two walls meeting at a corner leave no notch
            if (thick >= Thick) { if (u0 <= 0.001f) u0 -= Thick * 0.5f; if (u1 >= len - 0.001f) u1 += Thick * 0.5f; }
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall part";
            go.transform.SetParent(p.transform, false);
            var c = a + dir * ((u0 + u1) * 0.5f);
            go.transform.SetPositionAndRotation(new Vector3(c.x, p.d.y + (y0 + y1) * 0.5f, c.z), Quaternion.LookRotation(dir));
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

        void AddOpening(Piece p, float u, bool window, (string name, float w, float sill, float top, int cost) spec)
        {
            var before = Capture();
            if (!Household.Spend(spec.cost, spec.name.ToLower())) { GameAudio.Play(GameAudio.Sfx.No); return; }
            p.d.openings.Add(new Opening { u = u, w = spec.w, sill = spec.sill, top = spec.top, window = window });
            p.cost += spec.cost;
            Rebuild(p);
            Commit(before, spec.cost, window ? "a window" : "an archway");
            GameAudio.Play(GameAudio.Sfx.Place);
            Nav();
        }

        void Remove(Piece p)
        {
            if (!p) return;
            var before = Capture();
            int refund = Mathf.RoundToInt(p.cost * 0.7f);
            Household.Earn(refund, "Knocked down what was built");
            pieces.Remove(p);
            Destroy(p.gameObject);
            Commit(before, -refund, "knocked something down");
            GameAudio.Play(GameAudio.Sfx.Sell);
            Nav();
        }

        static void Nav() { if (TiramisuNav.Instance) TiramisuNav.Instance.RequestRebuild(); }

        // ------------------------------------------------------------ painting what the house came with

        static string PathOf(Transform t)
        {
            var s = t.name + "@" + t.GetSiblingIndex();
            for (var p = t.parent; p != null; p = p.parent) s = p.name + "/" + s;
            return s;
        }

        bool PaintRenderer(Renderer r, bool floor)
        {
            if (!r) return false;
            var m = floor ? FloorMat(floorIdx) : WallMat(wallIdx);
            if (!m) return false;
            string path = PathOf(r.transform);
            if (!originalMat.ContainsKey(path)) originalMat[path] = r.sharedMaterial;
            r.sharedMaterial = m;
            painted.items.RemoveAll(i => i.path == path);
            painted.items.Add(new PaintEntry { path = path, mat = floor ? floorIdx : wallIdx, floor = floor });
            return true;
        }

        void ApplyPaint()
        {
            if (!PlayerPrefs.HasKey(PaintKey)) return;
            try { painted = JsonUtility.FromJson<PaintData>(PlayerPrefs.GetString(PaintKey)) ?? new PaintData(); } catch { painted = new PaintData(); }
            ApplyPaintEntries();
        }

        void ApplyPaintEntries()
        {
            foreach (var e in painted.items)
                if (pathMap.TryGetValue(e.path, out var r) && r)
                {
                    if (!originalMat.ContainsKey(e.path)) originalMat[e.path] = r.sharedMaterial;
                    var m = e.floor ? FloorMat(e.mat) : WallMat(e.mat);
                    if (m) r.sharedMaterial = m;
                }
        }

        // ------------------------------------------------------------ saving

        void Save()
        {
            var sd = new SaveData();
            foreach (var p in pieces) if (p) sd.pieces.Add(p.d);
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(sd));
            PlayerPrefs.SetString(PaintKey, JsonUtility.ToJson(painted));
            PlayerPrefs.Save();
        }

        void Load()
        {
            if (!PlayerPrefs.HasKey(SaveKey)) return;
            SaveData sd = null;
            try { sd = JsonUtility.FromJson<SaveData>(PlayerPrefs.GetString(SaveKey)); } catch { }
            if (sd == null) return;
            foreach (var d in sd.pieces) Spawn(d, CostOf(d));
            Nav();
        }

        public static void ClearAll() { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.DeleteKey(PaintKey); }

        // ------------------------------------------------------------ on the picture: the grid, the cursor and the measurements

        void OnGUI()
        {
            if (!Active || Splash.Showing || !cam) return;
            Ui.Begin();
            if (Event.current.type != EventType.Repaint) return;

            if (cursorValid)
            {
                // a grid of dots that fades away from the cursor, like the floor grid of build mode in the Sims 4
                float span = 1.5f;
                for (float gx = -span; gx <= span + 0.001f; gx += Snap)
                    for (float gz = -span; gz <= span + 0.001f; gz += Snap)
                    {
                        var wp = new Vector3(cursor.x + gx, FloorY + 0.03f, cursor.z + gz);
                        wp.x = Mathf.Round(wp.x / Snap) * Snap; wp.z = Mathf.Round(wp.z / Snap) * Snap;
                        float d = Vector2.Distance(new Vector2(gx, gz), Vector2.zero) / span;
                        if (d > 1f) continue;
                        var sp = cam.WorldToScreenPoint(wp);
                        if (sp.z < 0f) continue;
                        bool major = Mathf.Abs(wp.x - Mathf.Round(wp.x)) < 0.01f && Mathf.Abs(wp.z - Mathf.Round(wp.z)) < 0.01f;
                        float r = major ? 2.4f : 1.4f;
                        var c = Ui.Dark ? new Color(0.9f, 0.7f, 1f, (1f - d) * (major ? 0.8f : 0.45f)) : new Color(0.55f, 0.35f, 0.3f, (1f - d) * (major ? 0.7f : 0.4f));
                        var pt = new Vector2(sp.x, Screen.height - sp.y) / Ui.Scale;
                        Ui.Round(new Rect(pt.x - r, pt.y - r, r * 2f, r * 2f), c, r);
                    }
                var cs = cam.WorldToScreenPoint(new Vector3(cursor.x, FloorY + 0.03f, cursor.z));
                if (cs.z > 0f)
                {
                    var cp = new Vector2(cs.x, Screen.height - cs.y) / Ui.Scale;
                    Ui.Ring(new Rect(cp.x - 8f, cp.y - 8f, 16f, 16f), Ui.Accent, 2.5f, 8f);
                    if (!string.IsNullOrEmpty(dragCost))
                    {
                        bool bad = dragCost.StartsWith("Too") || dragCost.Contains("wall is") || dragCost.Contains("room") || dragCost.Contains("enough");
                        Ui.Tag(new Vector2(cs.x + 56f * Ui.Scale, cs.y * 0f + (Screen.height - cs.y) - 20f * Ui.Scale), dragCost, 13f, !bad);
                    }
                }
            }

            if (dragging)
            {
                if (tool == Tool.Wall)
                {
                    float len = Vector3.Distance(dragStart, dragEnd);
                    var mid = (dragStart + dragEnd) * 0.5f + Vector3.up * (FloorY + 0.4f);
                    Measure(mid, $"{len:0.00} m");
                }
                else
                {
                    float w = Mathf.Abs(dragEnd.x - dragStart.x), d = Mathf.Abs(dragEnd.z - dragStart.z);
                    float y = FloorY + 0.3f;
                    Measure(new Vector3((dragStart.x + dragEnd.x) * 0.5f, y, dragStart.z), $"{w:0.00} m");
                    Measure(new Vector3(dragEnd.x, y, (dragStart.z + dragEnd.z) * 0.5f), $"{d:0.00} m");
                    Measure(new Vector3((dragStart.x + dragEnd.x) * 0.5f, y, (dragStart.z + dragEnd.z) * 0.5f), $"{w * d:0.0} m²");
                }
            }
        }

        void Measure(Vector3 world, string text)
        {
            var s = cam.WorldToScreenPoint(world);
            if (s.z < 0f) return;
            Ui.Tag(new Vector2(s.x, Screen.height - s.y), text, 12f, false);
        }

        // ------------------------------------------------------------ the panel

        static readonly string[] ToolNames = { "Wall", "Room", "Floor", "Archway", "Window", "Paint", "Eyedropper", "Knock down" };
        static readonly string[] ToolNotes =
        {
            "Drag a wall.\nShift: diagonal.", "Drag a box: walls\nand a floor.", "Drag to lay a floor.\nRM 12 a m².", "Click a wall to\ncut an archway.",
            "Click a wall to\nput a window in.", "Click a wall or\nfloor to paint it.", "Click to pick up\na finish.", "Take away what\nyou built (70% back).",
        };

        static Color FloorColour(string n)
        {
            switch (n)
            {
                case "Oak": return new Color(0.78f, 0.6f, 0.4f); case "Dark oak": return new Color(0.4f, 0.27f, 0.18f); case "Marble": return new Color(0.9f, 0.9f, 0.88f);
                case "Grey tile": return new Color(0.55f, 0.57f, 0.6f); case "Concrete": return new Color(0.62f, 0.62f, 0.62f); case "Rubber": return new Color(0.2f, 0.2f, 0.22f);
                case "Deck": return new Color(0.62f, 0.45f, 0.3f); case "Paving": return new Color(0.7f, 0.66f, 0.6f); case "Slate": return new Color(0.35f, 0.37f, 0.4f);
                default: return new Color(0.7f, 0.7f, 0.7f);
            }
        }

        static Color WallColour(Material m) => m != null && m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : new Color(0.9f, 0.9f, 0.9f);

        bool Swatch(Rect r, Color c, bool on, string name)
        {
            bool hover = Ui.Hover(r);
            if (on || hover) Ui.Ring(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), on ? Ui.Accent : Ui.Pink, 2.5f, 12f);
            Ui.Round(r, c, 9f);
            Ui.Ring(r, new Color(0f, 0f, 0f, 0.2f), 1.5f, 9f);
            if (hover) hoverNow = name;
            return GUI.Button(Ui.S(r), GUIContent.none, GUIStyle.none);
        }

        /// <summary>The Build tab of the side panel: the tools as cards, their options, swatches for floors and walls, and a hint.</summary>
        public void DrawBuild(Rect area)
        {
            float w = area.width, y = area.y;
            hoverPrev = hoverNow; hoverNow = null;
            Ui.Label(new Rect(area.x, y, w, 22f), $"Funds  {Household.Currency} {(Household.Instance ? Household.Instance.Funds : 0):N0}", 15f, Ui.GoldText, TextAnchor.MiddleLeft, Ui.Weight.ExtraBold);
            y += 30f;
            scroll = Ui.Scroll(new Rect(area.x, y, w, area.yMax - y), scroll, contentH, cw =>
            {
                float yy = 0f;
                HouseHud.Kicker(0f, yy, cw, "Tools"); yy += 20f;
                float gap = 8f, tw = (cw - gap) / 2f, th = 66f;
                for (int i = 0; i < ToolNames.Length; i++)
                {
                    var r = new Rect((i % 2) * (tw + gap), yy + (i / 2) * (th + gap), tw, th);
                    bool on = (int)tool == i;
                    if (Ui.CardButton(r, on)) { tool = (Tool)i; CancelDrag(); HideGhost(); dragCost = ""; }
                    Ui.Label(new Rect(r.x + 10f, r.y + 6f, tw - 20f, 20f), ToolNames[i], 14f, on ? Ui.Accent : Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                    Ui.Label(new Rect(r.x + 10f, r.y + 26f, tw - 20f, 38f), ToolNotes[i], 11f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
                }
                yy += ((ToolNames.Length + 1) / 2) * (th + gap) + 6f;

                // options that belong to the tool
                if (tool == Tool.Wall || tool == Tool.Room)
                {
                    HouseHud.Kicker(0f, yy, cw, "Wall height"); yy += 20f;
                    float hw = (cw - 12f) / 3f;
                    for (int i = 0; i < Heights.Length; i++)
                        if (Ui.Chip(new Rect(i * (hw + 6f), yy, hw, 28f), Heights[i].name, heightIdx == i, 11f)) heightIdx = i;
                    yy += 40f;
                }
                if (tool == Tool.Doorway)
                {
                    HouseHud.Kicker(0f, yy, cw, "Archway style"); yy += 20f;
                    for (int i = 0; i < Doors.Length; i++)
                    {
                        if (Ui.CardButton(new Rect(0f, yy, cw, 40f), doorStyle == i)) doorStyle = i;
                        Ui.Label(new Rect(12f, yy + 4f, cw - 100f, 20f), Doors[i].name, 13f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                        Ui.Label(new Rect(12f, yy + 21f, cw - 100f, 16f), $"{Doors[i].w:0.0} m wide, {Doors[i].top:0.0} m tall", 11f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold);
                        Ui.Label(new Rect(cw - 92f, yy, 80f, 40f), $"RM {Doors[i].cost}", 13f, Ui.GoldText, TextAnchor.MiddleRight, Ui.Weight.ExtraBold);
                        yy += 46f;
                    }
                    yy += 4f;
                }
                if (tool == Tool.Window)
                {
                    HouseHud.Kicker(0f, yy, cw, "Window style"); yy += 20f;
                    for (int i = 0; i < Windows.Length; i++)
                    {
                        if (Ui.CardButton(new Rect(0f, yy, cw, 40f), windowStyle == i)) windowStyle = i;
                        Ui.Label(new Rect(12f, yy + 4f, cw - 100f, 20f), Windows[i].name, 13f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.ExtraBold);
                        Ui.Label(new Rect(12f, yy + 21f, cw - 100f, 16f), $"{Windows[i].w:0.0} m wide, from {Windows[i].sill:0.0} to {Windows[i].top:0.0} m", 11f, Ui.Soft, TextAnchor.UpperLeft, Ui.Weight.Bold);
                        Ui.Label(new Rect(cw - 92f, yy, 80f, 40f), $"RM {Windows[i].cost}", 13f, Ui.GoldText, TextAnchor.MiddleRight, Ui.Weight.ExtraBold);
                        yy += 46f;
                    }
                    yy += 4f;
                }
                bool floors = tool == Tool.Floor || tool == Tool.Room || tool == Tool.Paint || tool == Tool.Eyedropper;
                bool walls = tool == Tool.Wall || tool == Tool.Room || tool == Tool.Paint || tool == Tool.Eyedropper;
                if (floors && floorNames != null)
                {
                    HouseHud.Kicker(0f, yy, cw, "Floor:  " + (hoverPrev != null && System.Array.IndexOf(floorNames, hoverPrev) >= 0 ? hoverPrev : floorNames[Mathf.Clamp(floorIdx, 0, floorNames.Length - 1)])); yy += 22f;
                    float sx = 3f, sy = yy;
                    for (int i = 0; i < floorNames.Length; i++)
                    {
                        if (sx + 34f > cw) { sx = 3f; sy += 42f; }
                        if (Swatch(new Rect(sx, sy, 32f, 32f), FloorColour(floorNames[i]), floorIdx == i, floorNames[i])) floorIdx = i;
                        sx += 42f;
                    }
                    yy = sy + 46f;
                }
                if (walls && wallNames != null)
                {
                    HouseHud.Kicker(0f, yy, cw, "Wall finish:  " + (hoverPrev != null && System.Array.IndexOf(wallNames, hoverPrev) >= 0 ? hoverPrev : wallNames[Mathf.Clamp(wallIdx, 0, wallNames.Length - 1)])); yy += 22f;
                    float sx = 3f, sy = yy;
                    for (int i = 0; i < wallNames.Length; i++)
                    {
                        if (sx + 34f > cw) { sx = 3f; sy += 42f; }
                        if (Swatch(new Rect(sx, sy, 32f, 32f), WallColour(WallMat(i)), wallIdx == i, wallNames[i])) wallIdx = i;
                        sx += 42f;
                    }
                    yy = sy + 46f;
                }

                var box = new Rect(0f, yy, cw, 92f);
                Ui.Round(box, Ui.Pale, 14f);
                string floorName = HouseView.Instance && HouseView.Instance.ActiveFloor == 1 ? "upper" : "ground";
                Ui.Label(new Rect(10f, yy + 8f, cw - 20f, 80f), (string.IsNullOrEmpty(hint) ? "Pick a tool." : hint) + $"\nZ or the Undo button takes it back, Ctrl+Y does it again. You are on the {floorName} floor.", 12f, Ui.Ink, TextAnchor.UpperLeft, Ui.Weight.Bold, true);
                contentH = yy + 104f;
            });
        }
    }
}
