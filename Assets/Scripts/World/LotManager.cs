using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>Knows every lot and where the people are: the map (key M) sends them from one lot to another.</summary>
    public class LotManager : MonoBehaviour
    {
        public static LotManager Instance { get; private set; }
        public static readonly List<Lot> Lots = new List<Lot>();
        public static Lot Current { get; private set; }
        public static bool Travelling { get; private set; }
        /// <summary>The storey you are looking at on an empty lot (0 ground, 1 upper).</summary>
        public static int Level { get; private set; }
        public const float StoreyHeight = 3.3f;

        bool haveHomeLimits; Vector2 homeMin, homeMax;

        public static void Register(Lot l)
        {
            if (!Lots.Contains(l)) Lots.Add(l);
            Lots.Sort((a, b) => a.order.CompareTo(b.order));
            if (l.home && Current == null) Current = l;
        }

        void Awake() { Instance = this; }
        void OnDestroy() { Lots.Clear(); Current = null; Level = 0; Travelling = false; if (Instance == this) Instance = null; }

        public static Lot Home { get { foreach (var l in Lots) if (l.home) return l; return null; } }
        public static bool AtHome => Current == null || Current.home;
        public static float LevelY => Level * StoreyHeight;

        public static Lot At(Vector3 p)
        {
            foreach (var l in Lots) if (l.Contains(p, 2f)) return l;
            return null;
        }

        /// <summary>The area you may build or move things in right now (the home plot, or the lot you are on).</summary>
        public static Rect Bounds
        {
            get
            {
                var l = Current;
                if (l == null) return new Rect(-2.7f, -4.7f, 36.6f, 27.4f);
                return new Rect(l.min.x, l.min.y, l.Size.x, l.Size.y);
            }
        }

        public static void SetLevel(int level)
        {
            Level = Mathf.Clamp(level, 0, 1);
            if (BuildMode.Instance) BuildMode.Instance.ApplyLevel();
            if (OrbitCamera.Instance) OrbitCamera.Instance.SetPivotHeight(LevelY);
        }

        public void Travel(Lot lot)
        {
            if (lot == null || Travelling || lot == Current) return;
            StartCoroutine(Go(lot));
        }

        IEnumerator Go(Lot lot)
        {
            Travelling = true;
            LoadingScreen.Begin(lot.home ? "Home" : lot.lotName, lot.home ? "Going back home" : "Off to " + lot.lotName);
            yield return new WaitForSecondsRealtime(0.4f);
            LoadingScreen.Progress(0.25f);

            if (BuildMode.Active) BuildMode.Toggle();
            if (DecorateMode.Active && DecorateMode.Instance) DecorateMode.Instance.Toggle();
            Current = lot; Level = 0;
            if (BuildMode.Instance) BuildMode.Instance.ApplyLevel();
            var hv = HouseView.Instance;
            if (hv && !lot.home) hv.SetView(HouseView.View.Ground, false);

            int n = 0;
            foreach (var c in Character.All.ToArray())
            {
                if (!c) continue;
                float side = (n % 2 == 0 ? 1f : -1f) * (0.9f + 0.5f * (n / 2));
                c.TeleportTo(lot.spawn + new Vector3(side, 0f, c.isPet ? -0.9f : 0f));
                n++;
            }
            var cam = OrbitCamera.Instance;
            if (cam)
            {
                if (!haveHomeLimits) { homeMin = cam.pivotMin; homeMax = cam.pivotMax; haveHomeLimits = true; }
                if (lot.home) { cam.pivotMin = homeMin; cam.pivotMax = homeMax; }
                else { cam.pivotMin = new Vector2(lot.min.x - 25f, lot.min.y - 25f); cam.pivotMax = new Vector2(lot.max.x + 25f, lot.max.y + 25f); }
                cam.JumpTo(lot.home ? new Vector3(15f, 0f, 9f) : lot.Centre, lot.home ? 38f : 32f);
            }
            LoadingScreen.Progress(0.6f);
            yield return new WaitForSecondsRealtime(0.7f);
            LoadingScreen.Progress(0.9f);
            yield return new WaitForSecondsRealtime(0.6f);
            LoadingScreen.End();
            Household.Toast(lot.home ? "Home again." : "Welcome to " + lot.lotName + ".");
            Travelling = false;
        }
    }
}
