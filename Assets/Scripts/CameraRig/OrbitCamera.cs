using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Free 360 degree orbit camera around a pivot on the active floor.
    /// Mouse: left drag spins, right or middle drag pans, wheel zooms.
    /// Touch: one finger spins, two fingers pinch to zoom, move to pan and twist to spin.
    /// Keys: Q/E spin, WASD or arrows pan, +/- zoom, F fits the whole house.
    /// Everything eases toward its target so moves feel soft.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class OrbitCamera : MonoBehaviour
    {
        public static OrbitCamera Instance { get; private set; }

        [Header("Start view")]
        public Vector3 pivot = new Vector3(15f, 0f, 9f);
        public float yaw = 225f;
        public float pitch = 38f;
        public float distance = 36f;

        [Header("Limits")]
        public float minPitch = 12f;
        public float maxPitch = 85f;
        public float minDistance = 3f;
        public float maxDistance = 70f;
        public Vector2 pivotMin = new Vector2(-4f, -2f);
        public Vector2 pivotMax = new Vector2(34f, 22f);

        [Header("Feel")]
        public float spinSpeed = 0.3f;
        public float tiltSpeed = 0.2f;
        public float keySpinSpeed = 90f;
        public float keyPanSpeed = 1.2f;
        public float smoothing = 12f;
        public float dragThreshold = 6f;

        // targets the camera eases toward
        Vector3 tPivot;
        float tYaw, tPitch, tDist;

        // mouse state
        Vector3 lastMouse, downMouse;
        bool spinning, panning, midSpin;

        // touch state
        float lastPinch, lastTwist;
        Vector2 lastMid;

        /// <summary>Screen area covered by the HUD, so drags there do not move the camera.</summary>
        public static System.Func<Vector2, bool> IsOverUi;

        /// <summary>Set while something else (decorate mode dragging furniture) owns the mouse.</summary>
        public static bool Blocked;

        /// <summary>True for the frame a left click ended without dragging (for picking furniture later).</summary>
        public bool ClickedThisFrame { get; private set; }
        public bool RightClickedThisFrame { get; private set; }
        Vector3 rightDown;

        void Awake()
        {
            Instance = this;
            tPivot = pivot; tYaw = yaw; tPitch = pitch; tDist = distance;
            Apply();
        }

        public void FocusOn(Vector3 point, float dist)
        {
            tPivot = point;
            tDist = Mathf.Clamp(dist, minDistance, maxDistance);
        }

        /// <summary>Goes straight there (a trip to another lot) instead of gliding.</summary>
        public void JumpTo(Vector3 point, float dist)
        {
            tPivot = pivot = point;
            tDist = distance = Mathf.Clamp(dist, minDistance, maxDistance);
        }

        public void SetPivotHeight(float y) => tPivot.y = y;

        public void FitHouse(float floorY)
        {
            if (!LotManager.AtHome) { FocusOn(new Vector3(LotManager.Current.Centre.x, floorY, LotManager.Current.Centre.z), 34f); return; }
            FocusOn(new Vector3(15f, floorY, 9f), 38f);
            tPitch = 38f;
        }

        void Update()
        {
            ClickedThisFrame = false;
            RightClickedThisFrame = false;
            if (Input.touchSupported && Input.touchCount > 0) HandleTouch();
            else HandleMouse();
            HandleKeys();

            tPitch = Mathf.Clamp(tPitch, minPitch, maxPitch);
            tDist = Mathf.Clamp(tDist, minDistance, maxDistance);
            tPivot.x = Mathf.Clamp(tPivot.x, pivotMin.x, pivotMax.x);
            tPivot.z = Mathf.Clamp(tPivot.z, pivotMin.y, pivotMax.y);

            float k = 1f - Mathf.Exp(-smoothing * Time.unscaledDeltaTime);
            pivot = Vector3.Lerp(pivot, tPivot, k);
            yaw = Mathf.LerpAngle(yaw, tYaw, k);
            pitch = Mathf.Lerp(pitch, tPitch, k);
            distance = Mathf.Lerp(distance, tDist, k);
            Apply();
        }

        void Apply()
        {
            var rot = Quaternion.Euler(pitch, yaw, 0f);
            transform.SetPositionAndRotation(pivot + rot * new Vector3(0f, 0f, -distance), rot);
        }

        bool OverUi(Vector2 p) => IsOverUi != null && IsOverUi(new Vector2(p.x, Screen.height - p.y));

        void HandleMouse()
        {
            Vector3 m = Input.mousePosition;

            if (Input.GetMouseButtonDown(0) && !OverUi(m) && !Blocked) { downMouse = m; lastMouse = m; spinning = true; }
            if (Input.GetMouseButtonDown(1) && !OverUi(m)) { lastMouse = m; panning = true; rightDown = m; }
            if (Input.GetMouseButtonUp(1) && panning && (m - rightDown).magnitude <= dragThreshold) RightClickedThisFrame = true;
            if (Input.GetMouseButtonDown(2) && !OverUi(m)) { lastMouse = m; midSpin = true; }
            if (midSpin && Input.GetMouseButton(2)) { Vector3 md = m - lastMouse; tYaw += md.x * spinSpeed; tPitch -= md.y * tiltSpeed; }
            if (!Input.GetMouseButton(2)) midSpin = false;

            if (spinning && Input.GetMouseButton(0))
            {
                if ((m - downMouse).magnitude > dragThreshold)
                {
                    Vector3 d = m - lastMouse;
                    tYaw += d.x * spinSpeed;
                    tPitch -= d.y * tiltSpeed;
                }
            }
            if (panning && Input.GetMouseButton(1))
                Pan(m - lastMouse);

            if (Input.GetMouseButtonUp(0))
            {
                if (spinning && (m - downMouse).magnitude <= dragThreshold) ClickedThisFrame = true;
                spinning = false;
            }
            if (!Input.GetMouseButton(1)) panning = false;

            float wheel = Input.mouseScrollDelta.y;
            if (Mathf.Abs(wheel) > 0.01f && !OverUi(m) && !Blocked) tDist *= Mathf.Pow(0.88f, wheel);

            lastMouse = m;
        }

        void HandleTouch()
        {
            if (Input.touchCount == 1)
            {
                var t = Input.GetTouch(0);
                if (t.phase == TouchPhase.Began) { spinning = !OverUi(t.position); downMouse = t.position; }
                if (spinning && t.phase == TouchPhase.Moved && (t.position - (Vector2)downMouse).magnitude > dragThreshold)
                {
                    tYaw += t.deltaPosition.x * spinSpeed;
                    tPitch -= t.deltaPosition.y * tiltSpeed;
                }
                if (t.phase == TouchPhase.Ended && spinning && (t.position - (Vector2)downMouse).magnitude <= dragThreshold)
                    ClickedThisFrame = true;
                return;
            }

            spinning = false;
            var a = Input.GetTouch(0);
            var b = Input.GetTouch(1);
            float pinch = (a.position - b.position).magnitude;
            Vector2 dir = b.position - a.position;
            float twist = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            Vector2 mid = (a.position + b.position) * 0.5f;

            if (a.phase == TouchPhase.Began || b.phase == TouchPhase.Began)
            {
                lastPinch = pinch; lastTwist = twist; lastMid = mid;
                return;
            }
            if (lastPinch > 1f) tDist *= lastPinch / Mathf.Max(pinch, 1f);
            tYaw -= Mathf.DeltaAngle(lastTwist, twist);
            Pan(mid - lastMid);
            lastPinch = pinch; lastTwist = twist; lastMid = mid;
        }

        void HandleKeys()
        {
            float dt = Time.unscaledDeltaTime;
            if (Input.GetKey(KeyCode.Q)) tYaw += keySpinSpeed * dt;
            if (Input.GetKey(KeyCode.E)) tYaw -= keySpinSpeed * dt;

            Vector2 pan = Vector2.zero;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) pan.y -= 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) pan.y += 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) pan.x += 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) pan.x -= 1f;
            if (pan != Vector2.zero) Pan(pan * keyPanSpeed * 600f * dt);

            if (Input.GetKey(KeyCode.Equals) || Input.GetKey(KeyCode.KeypadPlus)) tDist *= 1f - 1.2f * dt;
            if (Input.GetKey(KeyCode.Minus) || Input.GetKey(KeyCode.KeypadMinus)) tDist *= 1f + 1.2f * dt;
        }

        /// <summary>Moves the pivot along the ground so the scene follows the finger or mouse.</summary>
        void Pan(Vector2 screenDelta)
        {
            var flatRight = Quaternion.Euler(0f, tYaw, 0f) * Vector3.right;
            var flatFwd = Quaternion.Euler(0f, tYaw, 0f) * Vector3.forward;
            float scale = tDist * 0.0016f;
            tPivot -= (flatRight * screenDelta.x + flatFwd * screenDelta.y) * scale;
        }
    }
}
