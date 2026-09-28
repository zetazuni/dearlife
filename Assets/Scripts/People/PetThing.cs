using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Something the pets use (tools/blender_petstuff.py): the food and water bowls, or the pet bed. The food bowl holds a
    /// few servings; a person feeding a pet fills it (RM 5) and the pets eat from it when hungry. The kibble shows only
    /// while there is food. Added to the pieces by <see cref="InteractionSetup"/>, so pieces bought later get it too.
    /// </summary>
    public class PetThing : MonoBehaviour
    {
        public enum Kind { Bowls, Bed }
        public Kind kind;

        public static readonly List<PetThing> All = new List<PetThing>();
        public const int FullBowl = 3;
        const string FoodKey = "dearlife.petfood";

        /// <summary>Servings of food in the bowl, kept with the save (one bowl's worth is shared by every bowl).</summary>
        public static int Food
        {
            get => PlayerPrefs.GetInt(SaveSystem.Key(FoodKey), 2);
            set { PlayerPrefs.SetInt(SaveSystem.Key(FoodKey), Mathf.Clamp(value, 0, FullBowl)); foreach (var t in All) t.ShowFood(); }
        }

        [System.NonSerialized] public Character user;   // who is eating, drinking or lying here now
        Transform kibble;

        void OnEnable()
        {
            if (!All.Contains(this)) All.Add(this);
            if (kind == Kind.Bowls)
                foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.StartsWith("Kibble")) kibble = t;
            ShowFood();
        }

        void OnDisable() => All.Remove(this);

        void ShowFood() { if (kibble) kibble.gameObject.SetActive(Food > 0); }

        /// <summary>The food bowl is on the left of the mat, the water bowl on the right (as seen from the front).</summary>
        public Vector3 Bowl(bool water) => transform.TransformPoint(new Vector3(water ? -0.11f : 0.11f, 0f, 0f));

        /// <summary>Where a pet stands to eat or drink: in front of the bowl, its head over it.</summary>
        public Vector3 StandAt(bool water, float reach) => Bowl(water) + transform.forward * reach;

        /// <summary>The top of the bed's cushion, where a pet curls up.</summary>
        public Vector3 BedTop => transform.position + transform.up * 0.1f;

        /// <summary>Where a pet steps off the floor onto the bed and back.</summary>
        public Vector3 BedFront => transform.position + transform.forward * 0.5f;

        public static PetThing Nearest(Kind k, Vector3 from, Character forWho)
        {
            PetThing best = null; float bd = float.MaxValue;
            foreach (var t in All)
            {
                if (t.kind != k || !t.isActiveAndEnabled || (t.user && t.user != forWho)) continue;
                if (t.transform.position.y > 2.5f) continue;   // pets stay downstairs
                float d = (t.transform.position - from).sqrMagnitude;
                if (d < bd) { bd = d; best = t; }
            }
            return best;
        }
    }
}
