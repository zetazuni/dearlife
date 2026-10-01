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
    }
}
