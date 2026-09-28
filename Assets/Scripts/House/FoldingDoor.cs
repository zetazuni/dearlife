using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A zig-zag (bi-fold) glass door. Half of the panels hang from each side of the opening. When it opens the
    /// panels fold up like an accordion at the sides, every second one turned the other way, so the doorway is free.
    /// Each panel is drawn from its hinge along the wall (left group along +along, right group along -along).
    /// </summary>
    public class FoldingDoor : AutoDoor
    {
        public Transform[] panels;
        public Vector3 leftHinge, rightHinge;      // local positions of the first hinge on each side
        public Vector3 along = Vector3.right;      // direction of the wall
        public float panelWidth = 0.67f;
        public int leftCount = 3;
        public float maxAngle = 84f;

        protected override void Apply(float eased)
        {
            float th = eased * maxAngle;
            float c = Mathf.Cos(th * Mathf.Deg2Rad);
            Vector3 pos = leftHinge;
            for (int i = 0; i < panels.Length; i++)
            {
                bool left = i < leftCount;
                if (i == leftCount) pos = rightHinge;
                int j = left ? i : i - leftCount;
                float sign = (j % 2 == 0 ? 1f : -1f) * (left ? 1f : -1f);
                var q = Quaternion.Euler(0f, sign * th, 0f);
                if (!panels[i]) continue;
                panels[i].localPosition = pos;
                panels[i].localRotation = q;
                pos += q * (along * (left ? 1f : -1f) * panelWidth);
            }
        }
    }
}
