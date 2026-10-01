using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Marks a ready made person the character selection can offer (v0.49.0). The prefabs are made by Dearlife > Import
    /// people from Assets/Local/People (a rigged model prepared by tools/blender_person.py) and live in a Resources/People
    /// folder; <see cref="Residents"/> lists every one it finds.
    /// </summary>
    public class PersonModel : MonoBehaviour
    {
        public string id = "";
        public string displayName = "";
        [Range(0f, 1f), Tooltip("1 walks the feminine walk, 0 the masculine one")]
        public float feminine = 0.5f;
        [Tooltip("metres, soles to the top of the head or hair")]
        public float height = 1.7f;
        [TextArea] public string credit = "";

        /// <summary>The outline of the body and its clothes without the arms (v0.52.0), measured from the mesh by the
        /// importer in slices of <see cref="BodyStep"/> from the soles up to the shoulders: how far it reaches to
        /// the side, to the front and to the back of the hip bone. <see cref="CharacterRig"/> keeps the arms outside it,
        /// so a hand does not swing through a wide skirt, a coat or broad hips.</summary>
        [HideInInspector] public float[] bodyWide, bodyFront, bodyBack;
        public const float BodyStep = 0.04f;

        /// <summary>How far out to the side the body reaches at this height and this far in front of the hip bone (in the
        /// model's own metres), or false where there is no body beside it.</summary>
        public bool BodyEdge(float y, float z, float margin, out float edge)
        {
            edge = 0f;
            if (bodyWide == null || bodyWide.Length == 0) return false;
            int k = Mathf.FloorToInt(y / BodyStep);
            if (k < 0 || k >= bodyWide.Length || bodyWide[k] <= 0f) return false;
            // each slice as an oval: widest beside the hip bone, nothing in front of the belly or behind the back
            float mid = (bodyFront[k] - bodyBack[k]) * 0.5f, deep = (bodyFront[k] + bodyBack[k]) * 0.5f + margin;
            float t = (z - mid) / Mathf.Max(deep, 0.01f);
            if (Mathf.Abs(t) >= 1f) return false;
            edge = bodyWide[k] * Mathf.Sqrt(1f - t * t) + margin;
            return true;
        }

        /// <summary>How far forward of the hip bone the body reaches at this height, this far to the side of it, or
        /// false beside the body (there the arm is swung out sideways instead).</summary>
        public bool FrontEdge(float y, float x, float margin, out float edge, out float across)
        {
            edge = 0f; across = 1f;
            if (bodyWide == null || bodyWide.Length == 0) return false;
            int k = Mathf.FloorToInt(y / BodyStep);
            if (k < 0 || k >= bodyWide.Length || bodyWide[k] <= 0f) return false;
            float t = x / (bodyWide[k] + margin);
            across = Mathf.Abs(t);                        // 0 in front of the middle of the body, 1 at its side
            if (across >= 0.85f) return false;
            edge = bodyFront[k] * Mathf.Sqrt(1f - t * t) + margin;
            return true;
        }

        /// <summary>The furthest the body reaches forward anywhere above this height (the belly, the chest, a coat).</summary>
        public float FrontAbove(float y)
        {
            float f = 0.12f;
            if (bodyFront == null) return f;
            for (int k = Mathf.Max(0, Mathf.FloorToInt(y / BodyStep)); k < bodyFront.Length; k++) f = Mathf.Max(f, bodyFront[k]);
            return f;
        }
    }
}
