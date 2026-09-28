using UnityEngine;

namespace Dearlife
{
    /// <summary>A door that slides along the wall on a black rail (glass or wood).</summary>
    public class SlidingDoor : AutoDoor
    {
        [Tooltip("The moving panel (its children move with it).")]
        public Transform panel;
        [Tooltip("How far the panel slides, in the panel's parent space.")]
        public Vector3 slide = new Vector3(0f, 0f, -2f);

        Vector3 closedPos;

        void Awake() { if (panel) closedPos = panel.localPosition; }

        protected override void Apply(float eased)
        {
            if (panel) panel.localPosition = closedPos + slide * eased;
        }
    }
}
