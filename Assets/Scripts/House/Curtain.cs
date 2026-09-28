using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A pair of curtains on a rod. Click them (or the window) to draw them shut or open. Open, each panel
    /// bunches up at its side of the window.
    /// </summary>
    public class Curtain : MonoBehaviour
    {
        public Transform left, right;
        public WindowWall wall;
        public int index;
        public float openScale = 0.14f;
        public float seconds = 0.6f;

        float amount;      // 0 open, 1 closed
        bool closed;

        public bool Closed => closed;

        public void SetInstant(bool isClosed)
        {
            closed = isClosed;
            amount = closed ? 1f : 0f;
            Apply();
        }

        public void Toggle()
        {
            closed = !closed;
            if (wall) wall.windows[index].curtainClosed = closed;
        }

        void Update()
        {
            float target = closed ? 1f : 0f;
            if (Mathf.Approximately(amount, target)) return;
            amount = Mathf.MoveTowards(amount, target, Time.deltaTime / Mathf.Max(seconds, 0.05f));
            Apply();
        }

        void Apply()
        {
            float e = amount * amount * (3f - 2f * amount);
            float s = Mathf.Lerp(openScale, 1f, e);
            if (left) { var sc = left.localScale; sc.x = s; left.localScale = sc; }
            if (right) { var sc = right.localScale; sc.x = s; right.localScale = sc; }
        }
    }
}
