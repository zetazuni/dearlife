using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// A place where a person can sit or lie on a piece of furniture. It is a child of the piece, so it moves with it.
    /// The object's own position is where the pelvis ends up (in the piece's space), yaw is the way the person faces
    /// relative to the piece, and approach is where they stand before they get on.
    /// </summary>
    public class UseSpot : MonoBehaviour
    {
        public static readonly List<UseSpot> All = new List<UseSpot>();

        public string label = "sit";
        public CharacterRig.Pose pose = CharacterRig.Pose.Sit;
        public float yaw;
        public Vector3 approachLocal;
        public Vector2 seconds = new Vector2(20f, 60f);
        [Header("How the body follows this piece")]
        [Tooltip("Sitting: torso lean back from upright, in degrees (the slant of the backrest)")] public float recline = 6f;
        [Tooltip("Sitting: lower leg angle from vertical, positive = feet forward of the knees")] public float shinAngle = -8f;
        [Tooltip("Sitting: height of what the feet rest on above the floor (a bar stool foot ring)")] public float footY;
        [Tooltip("Lying: how far the torso is raised from flat, in degrees (a lounger backrest)")] public float raise;
        [Tooltip("Lying: thighs raised from flat, in degrees (the ends of a hammock curve up)")] public float legRaise;
        [Tooltip("Lying: knee bend, negative = the lower legs rise further")] public float kneeBend = 6f;
        [System.NonSerialized] public Character occupant;

        /// <summary>The floor under the piece.</summary>
        public float FloorY => transform.parent ? transform.parent.position.y : 0f;

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public Vector3 ApproachWorld => transform.parent.TransformPoint(approachLocal);

        /// <summary>The figure's root position and rotation that put its pelvis on this spot.</summary>
        public void RootFor(CharacterRig rig, float scale, out Vector3 pos, out Quaternion rot)
        {
            var yawRot = Quaternion.Euler(0f, transform.parent.eulerAngles.y + yaw, 0f);
            rot = pose == CharacterRig.Pose.Lie ? yawRot * Quaternion.Euler(-90f, 0f, 0f) : yawRot;
            float hip = pose == CharacterRig.Pose.Sit ? 0.45f : rig.RestHip;
            pos = transform.position - rot * new Vector3(0f, hip * scale, 0f);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(transform.position, 0.08f);
        }
    }
}
