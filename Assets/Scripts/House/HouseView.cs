using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Which part of the house you are looking at (ground floor, upper floor, or the whole house
    /// with its roof) and how the walls behave (automatic cutaway, always up, always down).
    /// </summary>
    public class HouseView : MonoBehaviour
    {
        public static HouseView Instance { get; private set; }

        public enum View { Ground = 0, Upper = 1, Whole = 2 }
        public enum WallMode { Auto, Up, Down }

        public GameObject upperFloor;
        public GameObject roof;
        public float upperFloorY = 3.3f;
        [Tooltip("Inside corners of the house footprint (x, z) used for the wall cutaway check.")]
        public Vector2 houseMin = new Vector2(0.5f, 0.5f);
        public Vector2 houseMax = new Vector2(29.5f, 7.5f);

        public View view = View.Ground;
        public WallMode wallMode = WallMode.Auto;

        public int ActiveFloor => view == View.Ground ? 0 : 1;
        public float ActiveFloorY => ActiveFloor == 0 ? 0f : upperFloorY;

        void Awake()
        {
            Instance = this;
            SetView(view, false);
        }

        public void SetView(View v, bool moveCamera = true)
        {
            view = v;
            if (!LotManager.AtHome) LotManager.SetLevel(v == View.Ground ? 0 : 1);
            if (upperFloor) upperFloor.SetActive(v != View.Ground);
            if (roof) roof.SetActive(v == View.Whole);
            var cam = OrbitCamera.Instance;
            if (cam && moveCamera)
            {
                if (v == View.Whole) cam.FitHouse(ActiveFloorY);
                else cam.SetPivotHeight(ActiveFloorY);
            }
        }

        public void CycleWallMode() => wallMode = (WallMode)(((int)wallMode + 1) % 3);

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.PageDown)) SetView(View.Ground);
            if (Input.GetKeyDown(KeyCode.PageUp)) SetView(View.Upper);
            if (Input.GetKeyDown(KeyCode.Home)) SetView(View.Whole);
            if (Input.GetKeyDown(KeyCode.Tab)) CycleWallMode();
            if (Input.GetKeyDown(KeyCode.F) && OrbitCamera.Instance && !MapWindow.Open) OrbitCamera.Instance.FitHouse(ActiveFloorY);

            var cam = OrbitCamera.Instance;
            if (!cam) return;
            Vector3 camPos = cam.transform.position;
            // keep the focus inside the house so looking from the garden still cuts the front glass
            Vector3 focus = cam.pivot;
            focus.x = Mathf.Clamp(focus.x, houseMin.x, houseMax.x);
            focus.z = Mathf.Clamp(focus.z, houseMin.y, houseMax.y);

            foreach (var w in WallCutaway.All)
            {
                bool cut;
                if (view == View.Whole || wallMode == WallMode.Up) cut = false;
                else if (wallMode == WallMode.Down) cut = true;
                else cut = w.IsBetween(camPos, focus);
                w.SetCut(cut);
            }
        }
    }
}
