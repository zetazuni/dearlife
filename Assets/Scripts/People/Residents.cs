using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>
    /// Who lives in the house. A save made with the character selection has its own household (<see cref="HouseholdData"/>);
    /// any other save gets the default household (Aina and Danial). People are ready made models (<see cref="PersonModel"/>
    /// prefabs in any Resources/People folder, made by Dearlife > Import people). The models themselves are downloaded
    /// characters kept out of the repo (Assets/Local): a copy of the project without them gets a plain stand-in figure.
    /// </summary>
    public static class Residents
    {
        static readonly List<GameObject> spawned = new List<GameObject>();
        static List<PersonModel> models;

        /// <summary>Every person the selection can offer, women first.</summary>
        public static List<PersonModel> Models
        {
            get
            {
                if (models != null && (models.Count == 0 || models[0])) return models;
                models = new List<PersonModel>();
                foreach (var go in Resources.LoadAll<GameObject>("People"))
                {
                    var m = go.GetComponent<PersonModel>();
                    if (m && !string.IsNullOrEmpty(m.id)) models.Add(m);
                }
                models.Sort((a, b) => a.feminine != b.feminine ? b.feminine.CompareTo(a.feminine) : string.CompareOrdinal(a.id, b.id));
                return models;
            }
        }

        /// <summary>The model a person uses: their own, or for a household with no models in it (the default couple, a save
        /// from before the selection) a woman for the first, third... and a man for the second, fourth..., however many
        /// characters are installed.</summary>
        public static PersonModel ModelOf(PersonData p, int place)
        {
            var list = Models;
            foreach (var m in list) if (m.id == p.model) return m;
            if (list.Count == 0) return null;
            place = Mathf.Abs(place);
            bool woman = place % 2 == 0;
            var fit = list.FindAll(m => (m.feminine >= 0.5f) == woman);
            return fit.Count > 0 ? fit[(place / 2) % fit.Count] : list[place % list.Count];
        }

        /// <summary>A person standing still (for the selection screen) or living (a full Character).</summary>
        public static GameObject Make(PersonData p, Vector3 pos, Quaternion rot, bool living, int place = 0)
        {
            var model = ModelOf(p, place);
            // made under a switched off holder, so components can be added before anything wakes up
            var holder = new GameObject("person (waking)");
            holder.SetActive(false);
            GameObject go;
            if (model) go = Object.Instantiate(model.gameObject, pos, rot, holder.transform);
            else
            {
                go = StandIn();
                go.transform.SetParent(holder.transform, false);
                go.transform.SetPositionAndRotation(pos, rot);
            }
            go.name = string.IsNullOrEmpty(p.name) ? "Person" : p.name;
            if (living)
            {
                var agent = go.AddComponent<NavMeshAgent>();
                agent.enabled = false;
                go.AddComponent<DoorOpener>();
                var c = go.AddComponent<Character>();
                c.displayName = p.name;
            }
            go.transform.SetParent(null, true);
            Object.Destroy(holder);
            go.SetActive(true);
            return go;
        }

        /// <summary>A plain figure for a copy of the project with no models imported: a body and a head, no animation.</summary>
        static GameObject StandIn()
        {
            Debug.LogWarning("Dearlife: no people models found. Prepare one with tools/blender_person.py and run Dearlife > Import people.");
            var go = new GameObject("Stand-in");
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Object.Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(go.transform, false);
            body.transform.localPosition = new Vector3(0f, 0.75f, 0f);
            body.transform.localScale = new Vector3(0.42f, 0.75f, 0.3f);
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.Destroy(head.GetComponent<Collider>());
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.58f, 0f);
            head.transform.localScale = Vector3.one * 0.23f;
            go.AddComponent<CharacterRig>();
            return go;
        }

        /// <summary>Called on every scene load: this save's own household moves in, or the default one.</summary>
        public static void MoveInSaved()
        {
            var h = HouseholdData.Load() ?? HouseholdData.Default();
            if (h != null) MoveIn(h);
        }

        // where people start: the living room and the kitchen, then a step further along for a third and fourth
        static readonly (Vector3 p, float yaw)[] Starts = { (new Vector3(5.6f, 0.02f, 5.6f), 200f), (new Vector3(11.5f, 0.02f, 6.6f), 20f) };

        public static void MoveIn(HouseholdData h)
        {
            foreach (var g in spawned) if (g) Object.Destroy(g);
            spawned.Clear();
            var group = GameObject.Find("People and pets");
            if (group)
                foreach (Transform t in group.transform)
                {
                    var c = t.GetComponent<Character>();
                    if (c && !c.isPet && !spawned.Contains(t.gameObject)) t.gameObject.SetActive(false);   // anyone left from an older scene
                }
            var names = new List<string>();
            foreach (var m in h.members) names.Add(m.name);
            for (int i = 0; i < h.members.Count; i++)
            {
                var s = Starts[i % Starts.Length];
                var at = s.p + new Vector3(0.7f * (i / Starts.Length), 0f, 0.5f * (i / Starts.Length));
                var go = Make(h.members[i], at, Quaternion.Euler(0f, s.yaw, 0f), true, i);
                if (!go) continue;
                if (group) go.transform.SetParent(group.transform, true);
                var sim = go.GetComponent<Sim>();
                if (sim) sim.SetupPerson(h.members[i], names, h);
                spawned.Add(go);
            }
        }

        /// <summary>While the selection is open nobody walks through the shot; afterwards everyone comes back.</summary>
        public static void HideEveryone(bool hide)
        {
            var group = GameObject.Find("People and pets");
            if (!group) return;
            foreach (Transform t in group.transform)
            {
                var c = t.GetComponent<Character>();
                if (!c) continue;
                if (hide) { if (t.gameObject.activeSelf) { t.gameObject.SetActive(false); hidden.Add(t.gameObject); } }
            }
            if (!hide)
            {
                foreach (var g in hidden) if (g) g.SetActive(true);
                hidden.Clear();
            }
        }

        static readonly List<GameObject> hidden = new List<GameObject>();
    }
}
