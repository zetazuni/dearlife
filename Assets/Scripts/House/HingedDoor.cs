using UnityEngine;

namespace Dearlife
{
    /// <summary>An ordinary hinged door (one or two leaves) that swings open on its hinges.</summary>
    public class HingedDoor : AutoDoor
    {
        [Tooltip("Each leaf is an empty at its hinge with the door as a child.")]
        public Transform[] leaves;
        [Tooltip("Open angle for each leaf in degrees (the sign gives the swing direction).")]
        public float[] angles;

        Quaternion[] closed;

        void Awake()
        {
            closed = new Quaternion[leaves.Length];
            for (int i = 0; i < leaves.Length; i++) closed[i] = leaves[i].localRotation;
        }

        protected override void Apply(float eased)
        {
            for (int i = 0; i < leaves.Length; i++)
                leaves[i].localRotation = closed[i] * Quaternion.Euler(0f, angles[i] * eased, 0f);
        }
    }
}
