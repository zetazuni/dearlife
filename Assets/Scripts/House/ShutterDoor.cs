using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A garage roller shutter. The slats hang from the top of the opening and roll up into the housing above
    /// it (the panel squashes towards its top edge, which is the panel's origin).
    /// </summary>
    public class ShutterDoor : AutoDoor
    {
        public Transform panel;
        [Range(0.02f, 0.5f)] public float openScale = 0.08f;

        protected override void Apply(float eased)
        {
            if (!panel) return;
            var s = panel.localScale;
            s.y = Mathf.Lerp(1f, openScale, eased);
            panel.localScale = s;
        }
    }
}
