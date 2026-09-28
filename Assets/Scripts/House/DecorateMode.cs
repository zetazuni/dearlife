using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Decorate mode (key M or the top bar), the way the Sims 4 does it: click a piece once to pick it up, it follows the
    /// surface under the mouse (a mug can be carried from the island to the floor, a lamp put on a table), click again to
    /// put it down. R turns it (Shift for the other way), Esc or right click puts it back. (Holding the button, dragging
    /// and letting go works too.) Everything you do can be undone and redone (Ctrl+Z, Ctrl+Y, or the buttons in the top bar).
    /// A piece cannot be dropped inside a wall or another piece; it goes back to the last free spot instead.
    /// Layouts are saved automatically and come back next time.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class DecorateMode : MonoBehaviour
    {
        public static DecorateMode Instance { get; private set; }
        public static bool Active { get; private set; }

        public float snap = 0.05f;
        public float turnStep = 15f;
        [Tooltip("see-through material the people and pets get while decorating")] public Material ghostMaterial;
        bool ghosted;
        bool placing, placingExisting, enteredByPlace;
        float grabbedAt;

        // ---- undo and redo
        class Op
        {
            public string what; public Furniture f;
            public bool a0 = true, a1 = true;                // active before and after (a sale hides the piece, a purchase shows it)
            public Vector3 p0, p1; public Quaternion r0, r1;
            public bool moves = true, tints;
            public string ta0 = "", tb0 = "", ta1 = "", tb1 = "";
            public int money;                                // paid going forward (negative: earned)
            public readonly List<(Furniture f, Vector3 p0, Quaternion r0, Vector3 p1, Quaternion r1)> riders = new List<(Furniture, Vector3, Quaternion, Vector3, Quaternion)>();
        }
        readonly List<Op> undoStack = new List<Op>(), redoStack = new List<Op>();
        Op lastMove;
        public bool CanUndo => undoStack.Count > 0;
        public bool CanRedo => redoStack.Count > 0;
        public string NextUndo => undoStack.Count > 0 ? undoStack[undoStack.Count - 1].what : "";

        Camera cam;
        Furniture held;
        Vector2 grabOffset;
        Vector3 startPos, lastValidPos;
        Quaternion startRot, lastValidRot;
        bool valid = true;
        string toast;
        float toastUntil;
        readonly List<(Rigidbody rb, bool kinematic)> frozen = new List<(Rigidbody, bool)>();
        // things standing on the held piece (cushions on a sofa, a mug on the island) ride along
        readonly List<(Furniture f, Vector3 localPos, Quaternion localRot, Vector3 startPos, Quaternion startRot)> riders =
            new List<(Furniture, Vector3, Quaternion, Vector3, Quaternion)>();
        static Texture2D white;

        public bool Holding => held != null || winWall != null;

        // a window being moved along its wall
        WindowWall winWall;
        int winIndex;
        float winStartCenter, winStartWidth;
        bool winValid = true;

        void Awake()
        {
            Instance = this;
            cam = Camera.main;
            white = Texture2D.whiteTexture;
        }

        void Start()
        {
            StartCoroutine(SettleWhenQuiet());
            int n = Furniture.LoadAll();
            if (n > 0) Say($"Welcome back, {n} piece{(n == 1 ? "" : "s")} of furniture are where you left them.");
        }

        /// <summary>Waits until the heavy pieces have come to rest (they push out of the floor a little at the start), then fixes the small things to them.</summary>
        System.Collections.IEnumerator SettleWhenQuiet()
        {
            yield return new WaitForSeconds(1.2f);
            Furniture.SettleSmallThings();
            Furniture.AttachSmallThings();
        }

        public void Toggle()
        {
            if (Active) { Drop(false); DropWindow(false); }
            Active = !Active;
            Say(Active ? "Decorate mode: click a piece to pick it up, click again to put it down. R turns it, Delete sells it, Esc puts it back." : "Decorate mode is off.");
        }

        public void ResetLayout()
        {
            Drop(false);
            DropWindow(true);
            Furniture.ResetAll();
            WindowWall.ResetAll();
            if (DearlifeNav.Instance) DearlifeNav.Instance.RequestRebuild();
            Say("Everything is back where it started, standing upright.");
        }

        void Say(string t) { toast = t; toastUntil = Time.time + 4f; }

        void OnDisable() { OrbitCamera.Blocked = false; Active = false; }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.P) && !SettingsWindow.Open && !MainMenu.Active) Toggle();
            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl) || Input.GetKey(KeyCode.LeftCommand) || Input.GetKey(KeyCode.RightCommand);
            if (ctrl && (Active || BuyMode.Active) && !BuildMode.Active && held == null)
            {
                bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (Input.GetKeyDown(KeyCode.Z)) { if (shift) Redo(); else Undo(); }
                else if (Input.GetKeyDown(KeyCode.Y)) Redo();
            }
            if (Active != ghosted) { ghosted = Active; Character.SetAllFrozen(Active, ghostMaterial); }
            if (!Active) { OrbitCamera.Blocked = false; return; }
            if (!cam) cam = Camera.main;

            if (winWall != null) { WindowUpdate(); return; }

            if (held != null && placing) { PlacingUpdate(); return; }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (held != null) { SellPiece(held); return; }
                // nothing in the hand: sell what the mouse is over
                var under = PieceUnderMouse();
                if (under != null && !under.pinned) { SellPiece(under); return; }
                if (under != null) Say($"The {under.Label.ToLower()} is built in, it cannot be sold.");
            }

            if (held == null)
            {
                if (Input.GetMouseButtonDown(0) && !(OrbitCamera.IsOverUi != null && OrbitCamera.IsOverUi(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y))))
                    TryGrab();
                return;
            }

            OrbitCamera.Blocked = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { Drop(true); return; }
            if (Input.GetKeyDown(KeyCode.R))
            {
                float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -turnStep : turnStep;
                held.transform.Rotate(0f, step, 0f, Space.World);
                MoveRiders();
            }
            if (!Input.GetMouseButton(0)) { Drop(false); return; }
            // hold the left button (dragging) and turn the wheel: one degree per notch (the camera zoom is blocked meanwhile)
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f)
            {
                held.transform.Rotate(0f, Mathf.Sign(wheel) * Mathf.Max(1f, Mathf.Round(Mathf.Abs(wheel))), 0f, Space.World);
                MoveRiders();
            }
            Drag();
        }

        // ---------- buying and selling ----------

        /// <summary>A piece just bought: it follows the mouse until you click to put it down.</summary>
        public void BeginPlace(Furniture f)
        {
            enteredByPlace = !Active;
            if (!Active) Toggle();
            cam = Camera.main;
            Grab(f, f.transform.position);
            placing = true;
            Say("Move it where you want it and click. R turns it, Esc puts it back.");
        }

        /// <summary>Move a piece that is already in the house (the pie menu): it follows the mouse until you click.</summary>
        public void StartMove(Furniture f)
        {
            if (f == null || f.pinned) return;
            enteredByPlace = !Active;
            if (!Active) Toggle();
            cam = Camera.main;
            while (f.attachedTo) f = f.attachedTo;
            Grab(f, f.transform.position);
            placing = true; placingExisting = true; grabbedAt = Time.unscaledTime;
            Say("Move it where you want it and click. R turns it, Esc puts it back.");
        }

        void PlacingUpdate()
        {
            OrbitCamera.Blocked = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { CancelPlacing(); return; }
            if (Input.GetKeyDown(KeyCode.Delete) || Input.GetKeyDown(KeyCode.Backspace))
            {
                if (placingExisting) SellPiece(held); else CancelPlacing();
                LeaveIfEntered();
                return;
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                float step = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) ? -turnStep : turnStep;
                held.transform.Rotate(0f, step, 0f, Space.World);
            }
            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f) held.transform.Rotate(0f, Mathf.Sign(wheel) * 5f, 0f, Space.World);
            Drag();
            bool overUi = OrbitCamera.IsOverUi != null && OrbitCamera.IsOverUi(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y));
            bool dragDrop = placingExisting && Input.GetMouseButtonUp(0) && Time.unscaledTime - grabbedAt > 0.35f;
            if ((Input.GetMouseButtonDown(0) && !overUi) || dragDrop)
            {
                if (!valid) { Say("There is no room for that here."); GameAudio.Play(GameAudio.Sfx.No); return; }
                var f = held;
                bool existing = placingExisting; placingExisting = false;
                int price = f.pendingPrice;
                if (price > 0 && !Household.Spend(price, f.Label.ToLower())) { CancelPlacing(); return; }
                f.pendingPrice = 0;
                placing = false;
                Drop(false);
                if (existing)
                {
                    if (f.bought) PurchaseSave.Record(f);
                    if (lastMove != null && (Vector3.Distance(lastMove.p0, lastMove.p1) > 0.004f || Quaternion.Angle(lastMove.r0, lastMove.r1) > 0.5f)) Push(lastMove);
                    GameAudio.Play(GameAudio.Sfx.Place); LeaveIfEntered(); return;
                }
                var bought = new Op { what = "Bought the " + f.Label.ToLower(), f = f, a0 = false, a1 = true, p0 = f.transform.position, r0 = f.transform.rotation, p1 = f.transform.position, r1 = f.transform.rotation, money = price, ta1 = f.tintA, tb1 = f.tintB };
                Push(bought);
                PurchaseSave.Record(f);
                GameAudio.Play(GameAudio.Sfx.Buy);
                Say($"Bought the {f.Label.ToLower()} for {Household.Currency} {price:N0}.");
                if (LiveMode.Selected && LiveMode.Selected.sim) LiveMode.Selected.sim.Report("buy");
                LeaveIfEntered();
            }
        }

        void LeaveIfEntered() { if (enteredByPlace && Active) { enteredByPlace = false; Toggle(); } }

        void CancelPlacing()
        {
            var f = held;
            bool existing = placingExisting; placingExisting = false;
            placing = false;
            Drop(true);
            if (existing) { Say("Put back."); LeaveIfEntered(); return; }
            if (f) Destroy(f.gameObject);
            Say("Put back, nothing was charged.");
            LeaveIfEntered();
        }

        public void SellPiece(Furniture f)
        {
            if (f == null) return;
            if (f == held)
            {
                bool wasNew = placing && !placingExisting;
                placing = false; placingExisting = false;
                Drop(true);
                if (wasNew) { Destroy(f.gameObject); return; }
            }
            int price = Household.SellPrice(f.name);
            Household.Earn(price, $"Sold the {f.Label.ToLower()}");
            PurchaseSave.Forget(f);
            Selected = null;
            Push(new Op { what = "Sold the " + f.Label.ToLower(), f = f, a0 = true, a1 = false, p0 = f.transform.position, r0 = f.transform.rotation, p1 = f.transform.position, r1 = f.transform.rotation, money = -price, ta0 = f.tintA, tb0 = f.tintB, ta1 = f.tintA, tb1 = f.tintB });
            f.gameObject.SetActive(false);
            GameAudio.Play(GameAudio.Sfx.Sell);
            Say($"Sold the {f.Label.ToLower()} for {Household.Currency} {price:N0}.");
            if (DearlifeNav.Instance) DearlifeNav.Instance.RequestRebuild();
        }

        // ---------- picking ----------

        /// <summary>The piece under the mouse (small things first), or null.</summary>
        Furniture PieceUnderMouse()
        {
            if (!cam) return null;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                if (IsArchitecture(h.collider)) continue;
                var f = h.collider.GetComponentInParent<Furniture>();
                if (!f) return null;
                while (f.attachedTo) f = f.attachedTo;
                return f;
            }
            return null;
        }

        void TryGrab()
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var part = h.collider.GetComponent<WallWindowPart>();
                if (part) { StartWindow(part); return; }
                if (IsArchitecture(h.collider)) continue;   // walls cut down or glass never block the pick
                var f = h.collider.GetComponentInParent<Furniture>();
                if (!f) return;
                while (f.attachedTo) f = f.attachedTo;   // small things are part of what they sit on                              // something solid that is not furniture is in front
                if (f.pinned) { Say($"The {f.Label.ToLower()} is built in, it cannot be moved."); return; }
                Grab(f, h.point);
                placing = true; placingExisting = true; grabbedAt = Time.unscaledTime;
                return;
            }
        }

        public Furniture Selected { get; private set; }

        /// <summary>The piece being carried or placed right now, or null.</summary>
        public Furniture HeldPiece => held;
        /// <summary>The window being slid along its wall right now (wall null when none).</summary>
        public (WindowWall wall, int index) HeldWindow => (winWall, winIndex);

        /// <summary>What a click would pick up at the mouse right now, the same way <see cref="TryGrab"/> decides, but without picking it up.</summary>
        public bool HoverTarget(out Furniture piece, out WallWindowPart window)
        {
            piece = null; window = null;
            if (!cam) cam = Camera.main;
            if (!cam) return false;
            var hits = Physics.RaycastAll(cam.ScreenPointToRay(Input.mousePosition), 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var h in hits)
            {
                var part = h.collider.GetComponent<WallWindowPart>();
                if (part) { window = part; return true; }
                if (IsArchitecture(h.collider)) continue;
                var f = h.collider.GetComponentInParent<Furniture>();
                if (!f) return false;
                while (f.attachedTo) f = f.attachedTo;
                if (f.pinned) return false;
                piece = f;
                return true;
            }
            return false;
        }

        /// <summary>Turns the last piece you picked up by the given angle (the HUD buttons). Refuses if it would hit something.</summary>
        public void RotateSelected(float deg)
        {
            if (Selected == null || held != null || winWall != null) return;
            Grab(Selected, Selected.transform.position);
            held.transform.Rotate(0f, deg, 0f, Space.World);
            MoveRiders();
            Physics.SyncTransforms();
            valid = IsFree(held.transform.position);
            lastValidPos = held.transform.position;
            lastValidRot = held.transform.rotation;
            if (!valid) Say("There is no room to turn it here.");
            Drop(!valid);
            if (valid && lastMove != null) Push(lastMove);
        }

        void Grab(Furniture f, Vector3 point)
        {
            held = f;
            Selected = f;
            OrbitCamera.Blocked = true;
            startPos = lastValidPos = f.transform.position;
            startRot = lastValidRot = f.transform.rotation;
            grabOffset = new Vector2(f.transform.position.x - point.x, f.transform.position.z - point.z);
            valid = true;
            frozen.Clear();
            riders.Clear();
            // everything else is fixed while this piece is carried, so nothing gets pushed when it touches other things
            foreach (var other in UnityEngine.Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Exclude))
            {
                if (other.transform.IsChildOf(f.transform) || other.isKinematic) continue;
                frozen.Add((other, other.isKinematic));
                other.linearVelocity = Vector3.zero;
                other.angularVelocity = Vector3.zero;
                other.isKinematic = true;
            }
            Freeze(f);
            var lb = f.LocalBounds;
            foreach (var o in Furniture.All)
            {
                if (o == f || o.pinned || o.attachedTo) continue;
                var l = f.transform.InverseTransformPoint(o.transform.position);
                bool on = Mathf.Abs(l.x - lb.center.x) < lb.extents.x && Mathf.Abs(l.z - lb.center.z) < lb.extents.z && l.y > 0.05f && l.y < lb.max.y + 0.06f;
                if (!on) continue;
                riders.Add((o, l, Quaternion.Inverse(f.transform.rotation) * o.transform.rotation, o.transform.position, o.transform.rotation));
                Freeze(o);
            }
        }

        void Freeze(Furniture f)
        {
            foreach (var rb in f.GetComponentsInChildren<Rigidbody>())
            {
                frozen.Add((rb, rb.isKinematic));
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
        }

        void MoveRiders()
        {
            foreach (var r in riders)
                r.f.transform.SetPositionAndRotation(held.transform.TransformPoint(r.localPos), held.transform.rotation * r.localRot);
        }

        static bool IsArchitecture(Collider c)
        {
            for (var t = c.transform; t != null; t = t.parent)
                if (t.name == "Walls" || t.name == "Roof") return true;
            return false;
        }

        // ---------- windows ----------

        void StartWindow(WallWindowPart part)
        {
            winWall = part.wall;
            winIndex = part.index;
            winStartCenter = winWall.windows[winIndex].center;
            winStartWidth = winWall.windows[winIndex].width;
            winValid = true;
            OrbitCamera.Blocked = true;
        }

        void WindowUpdate()
        {
            OrbitCamera.Blocked = true;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) { DropWindow(true); return; }
            if (Input.GetKeyDown(KeyCode.R))
            {
                bool back = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
                if (!winWall.CycleWidth(winIndex, back ? -1 : 1)) Say("That size does not fit here.");
            }
            if (!Input.GetMouseButton(0)) { DropWindow(false); return; }
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            if (winWall.WallPlane().Raycast(ray, out float e))
            {
                float u = winWall.AlongCoordinate(ray.GetPoint(e));
                u = Mathf.Round(u / 0.1f) * 0.1f;
                winValid = winWall.TryMove(winIndex, u);
            }
        }

        void DropWindow(bool cancel)
        {
            if (winWall == null) return;
            var w = winWall.windows[winIndex];
            if (cancel)
            {
                w.width = winStartWidth;
                w.center = winStartCenter;
                winWall.Rebuild();
            }
            winWall = null;
            OrbitCamera.Blocked = false;
            WindowWall.SaveAll();
        }

        // ---------- dragging ----------

        void Drag()
        {
            var ray = cam.ScreenPointToRay(Input.mousePosition);
            var hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            bool found = false;
            Vector3 p = default;
            foreach (var h in hits)
            {
                if (h.collider.transform.IsChildOf(held.transform)) continue;
                if (IsArchitecture(h.collider)) continue;
                if (h.normal.y < 0.6f) continue;             // only surfaces you could put something on
                p = h.point; found = true; break;
            }
            if (!found) return;

            bool free = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            float x = p.x + grabOffset.x, z = p.z + grabOffset.y;
            if (!free) { x = Mathf.Round(x / snap) * snap; z = Mathf.Round(z / snap) * snap; }
            var lotB = LotManager.Bounds;       // the plot of the lot you are on
            x = Mathf.Clamp(x, lotB.xMin, lotB.xMax);
            z = Mathf.Clamp(z, lotB.yMin, lotB.yMax);
            // the piece's own origin sits on its base, so it rests exactly on the surface
            var target = new Vector3(x, p.y + 0.003f, z);
            held.transform.position = Vector3.Lerp(held.transform.position, target, 1f - Mathf.Exp(-30f * Time.unscaledDeltaTime));
            MoveRiders();
            valid = IsFree(target);
            if (valid) { lastValidPos = target; lastValidRot = held.transform.rotation; }
        }

        /// <summary>True when the piece fits at this spot: no wall or other piece inside it.</summary>
        bool IsFree(Vector3 at)
        {
            var lb = held.LocalBounds;
            var rot = held.transform.rotation;
            Vector3 centre = at + rot * lb.center;
            Vector3 half = lb.extents;
            half = new Vector3(Mathf.Max(half.x - 0.03f, 0.01f), Mathf.Max(half.y - 0.06f, 0.01f), Mathf.Max(half.z - 0.03f, 0.01f));
            centre.y += 0.02f;
            float baseY = at.y;
            foreach (var c in Physics.OverlapBox(centre, half, rot, ~0, QueryTriggerInteraction.Ignore))
            {
                if (c.transform.IsChildOf(held.transform)) continue;
                if (c.bounds.max.y <= baseY + 0.045f) continue;   // the surface it stands on
                var other = c.GetComponentInParent<Furniture>();
                if (other != null && IsRider(other)) continue;    // things riding on this piece
                return false;
            }
            return true;
        }

        bool IsRider(Furniture o)
        {
            foreach (var r in riders) if (r.f == o) return true;
            return false;
        }

        void Drop(bool cancel)
        {
            if (held == null) return;
            var f = held;
            held = null;
            OrbitCamera.Blocked = false;
            if (cancel) f.Place(startPos, startRot);
            else if (!valid) { f.Place(lastValidPos, lastValidRot); Say("There is no room for that here."); }
            else f.Place(lastValidPos, f.transform.rotation);
            // the riders follow wherever the piece ended up (or go back to where they were)
            held = f;
            foreach (var r in riders)
            {
                if (cancel) r.f.Place(r.startPos, r.startRot);
                else r.f.Place(f.transform.TransformPoint(r.localPos), f.transform.rotation * r.localRot);
            }
            held = null;
            lastMove = null;
            if (!cancel)
            {
                lastMove = new Op { what = "Moved the " + f.Label.ToLower(), f = f, p0 = startPos, r0 = startRot, p1 = f.transform.position, r1 = f.transform.rotation, ta0 = f.tintA, tb0 = f.tintB, ta1 = f.tintA, tb1 = f.tintB };
                foreach (var r in riders) lastMove.riders.Add((r.f, r.startPos, r.startRot, r.f.transform.position, r.f.transform.rotation));
            }
            riders.Clear();
            foreach (var (rb, kin) in frozen) if (rb) rb.isKinematic = kin;
            frozen.Clear();
            Physics.SyncTransforms();
            Furniture.SaveAll();
            if (DearlifeNav.Instance) DearlifeNav.Instance.RequestRebuild();
        }

        // ---------- undo and redo ----------

        void Push(Op o)
        {
            undoStack.Add(o);
            if (undoStack.Count > 120) undoStack.RemoveAt(0);
            redoStack.Clear();
        }

        /// <summary>Colour changes go on the same history as moves and purchases.</summary>
        public void SetTint(Furniture f, int channel, string hex)
        {
            if (f == null) return;
            var o = new Op { what = "Recoloured the " + f.Label.ToLower(), f = f, moves = false, tints = true, ta0 = f.tintA, tb0 = f.tintB, a0 = true, a1 = true };
            f.SetTint(channel, hex);
            o.ta1 = f.tintA; o.tb1 = f.tintB;
            if (o.ta0 == o.ta1 && o.tb0 == o.tb1) return;
            Push(o);
            if (f.bought) PurchaseSave.Record(f);
            Furniture.SaveAll();
        }

        public void Undo()
        {
            if (undoStack.Count == 0) { Say("Nothing to undo."); return; }
            var o = undoStack[undoStack.Count - 1];
            if (o.money < 0 && !Household.Spend(-o.money, "Undid: " + o.what.ToLower())) { Say("You cannot afford to undo that right now."); GameAudio.Play(GameAudio.Sfx.No); return; }
            if (o.money > 0) Household.Earn(o.money, "Undid: " + o.what.ToLower());
            undoStack.RemoveAt(undoStack.Count - 1);
            Apply(o, false);
            redoStack.Add(o);
            Say("Undid: " + o.what.ToLower() + ".");
            GameAudio.Play(GameAudio.Sfx.Place);
        }

        public void Redo()
        {
            if (redoStack.Count == 0) { Say("Nothing to redo."); return; }
            var o = redoStack[redoStack.Count - 1];
            if (o.money > 0 && !Household.Spend(o.money, o.what)) { Say("You cannot afford that right now."); GameAudio.Play(GameAudio.Sfx.No); return; }
            if (o.money < 0) Household.Earn(-o.money, o.what);
            redoStack.RemoveAt(redoStack.Count - 1);
            Apply(o, true);
            undoStack.Add(o);
            Say("Did it again: " + o.what.ToLower() + ".");
            GameAudio.Play(GameAudio.Sfx.Place);
        }

        void Apply(Op o, bool forward)
        {
            var f = o.f;
            if (f == null) return;
            bool active = forward ? o.a1 : o.a0;
            if (f.gameObject.activeSelf != active) f.gameObject.SetActive(active);
            if (active && o.moves)
            {
                f.Place(forward ? o.p1 : o.p0, forward ? o.r1 : o.r0);
                foreach (var r in o.riders) if (r.f) r.f.Place(forward ? r.p1 : r.p0, forward ? r.r1 : r.r0);
            }
            if (o.tints) { f.SetTint(0, forward ? o.ta1 : o.ta0); f.SetTint(1, forward ? o.tb1 : o.tb0); }
            if (f.bought) { if (active) PurchaseSave.Record(f); else PurchaseSave.Forget(f); }
            else if (o.a0 != o.a1) { if (active) PurchaseSave.Unforget(f); else PurchaseSave.Forget(f); }
            else if (f.bought == false) { }
            Physics.SyncTransforms();
            Furniture.SaveAll();
            if (DearlifeNav.Instance) DearlifeNav.Instance.RequestRebuild();
        }

        // ---------- feedback ----------

        void OnGUI()
        {
            if (!Active && Time.time > toastUntil) return;
            Ui.Begin();
            float scale = Ui.Scale;

            if (held != null && cam)
            {
                var lb = held.LocalBounds;
                var col = valid ? new Color(0.45f, 1f, 0.6f, 0.95f) : new Color(1f, 0.35f, 0.35f, 0.95f);
                var t = held.transform;
                float y = lb.min.y + 0.01f;
                Vector2[] c = new Vector2[4];
                Vector3[] w =
                {
                    t.TransformPoint(new Vector3(lb.min.x, y, lb.min.z)), t.TransformPoint(new Vector3(lb.max.x, y, lb.min.z)),
                    t.TransformPoint(new Vector3(lb.max.x, y, lb.max.z)), t.TransformPoint(new Vector3(lb.min.x, y, lb.max.z)),
                };
                for (int i = 0; i < 4; i++)
                {
                    var s = cam.WorldToScreenPoint(w[i]);
                    if (s.z < 0f) return;
                    c[i] = new Vector2(s.x, Screen.height - s.y);
                }
                for (int i = 0; i < 4; i++) Line(c[i], c[(i + 1) % 4], col, 3f * scale);
                var top = cam.WorldToScreenPoint(t.TransformPoint(new Vector3(lb.center.x, lb.max.y, lb.center.z)));
                var r = new Rect(top.x - 100 * scale, Screen.height - top.y - 34 * scale, 200 * scale, 26 * scale);
                Ui.Tag(new Vector2(r.center.x, r.center.y), held.Label, 13f, true);
            }

            if (winWall != null && cam)
            {
                var wc = winWall.Corners(winIndex);
                var col = winValid ? new Color(0.45f, 1f, 0.6f, 0.95f) : new Color(1f, 0.35f, 0.35f, 0.95f);
                var sp = new Vector2[4];
                bool visible = true;
                for (int i = 0; i < 4; i++)
                {
                    var s = cam.WorldToScreenPoint(wc[i]);
                    if (s.z < 0f) visible = false;
                    sp[i] = new Vector2(s.x, Screen.height - s.y);
                }
                if (visible)
                {
                    for (int i = 0; i < 4; i++) Line(sp[i], sp[(i + 1) % 4], col, 3f * scale);
                    Ui.Tag(new Vector2(sp[3].x, sp[3].y - 18 * scale), "Window (R changes the size)", 13f, true);
                }
            }

            if (Time.time < toastUntil)
                Ui.Tag(new Vector2(Screen.width * 0.5f - (HouseHud.PanelOpen ? 175f * scale : 0f), 84f * scale), toast, 14f, false);
        }

        static void Line(Vector2 a, Vector2 b, Color c, float width)
        {
            var saved = GUI.matrix;
            var d = b - a;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(ang, a);
            var old = GUI.color;
            GUI.color = c;
            GUI.DrawTexture(new Rect(a.x, a.y - width / 2f, d.magnitude, width), white);
            GUI.color = old;
            GUI.matrix = saved;
        }
    }
}
