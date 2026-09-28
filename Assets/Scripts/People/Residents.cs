using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>
    /// Who lives in the house. A save made with the character creator has its own household (<see cref="HouseholdData"/>):
    /// its people are made from Resources/People/Person (an MPFB2 person with its shape, look and wardrobe) and
    /// Lily and James step aside. Saves without one keep Lily and James.
    /// </summary>
    public static class Residents
    {
        static readonly List<GameObject> spawned = new List<GameObject>();

        /// <summary>A person from the creator's data, standing still (for the creator) or living (a full Character).</summary>
        public static GameObject Make(PersonData p, Vector3 pos, Quaternion rot, bool living)
        {
            var prefab = Resources.Load<GameObject>("People/Person");
            if (!prefab) { Debug.LogWarning("Dearlife: Resources/People/Person is missing (Dearlife > Make person prefab)."); return null; }
            // made under a switched off holder, so components can be added before anything wakes up
            var holder = new GameObject("person (waking)");
            holder.SetActive(false);
            var go = Object.Instantiate(prefab, pos, rot, holder.transform);
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
            var look = go.GetComponent<PersonLook>();
            if (!look) look = go.AddComponent<PersonLook>();
            look.Apply(p);
            return go;
        }

        /// <summary>Called on every scene load: if this save has a household, it moves in (Lily and James step aside).</summary>
        public static void MoveInSaved()
        {
            var h = HouseholdData.Load();
            if (h != null) MoveIn(h);
        }

        public static void MoveIn(HouseholdData h)
        {
            foreach (var g in spawned) if (g) Object.Destroy(g);
            spawned.Clear();
            var group = GameObject.Find("People and pets");
            var starts = new List<(Vector3 p, Quaternion r)>();
            if (group)
                foreach (Transform t in group.transform)
                {
                    var c = t.GetComponent<Character>();
                    if (!c || c.isPet) continue;
                    starts.Add((t.position, t.rotation));
                    t.gameObject.SetActive(false);        // the default household steps aside
                }
            if (starts.Count == 0) starts.Add((new Vector3(5.6f, 0.02f, 5.6f), Quaternion.identity));
            var names = new List<string>();
            foreach (var m in h.members) names.Add(m.name);
            for (int i = 0; i < h.members.Count; i++)
            {
                var s = starts[i % starts.Count];
                var at = s.p + new Vector3(0.7f * (i / starts.Count), 0f, 0.5f * (i / starts.Count));
                var go = Make(h.members[i], at, s.r, true);
                if (!go) continue;
                if (group) go.transform.SetParent(group.transform, true);
                var sim = go.GetComponent<Sim>();
                if (sim) sim.SetupPerson(h.members[i], names);
                spawned.Add(go);
            }
        }

        /// <summary>While the creator is open nobody walks through the shot; afterwards everyone comes back.</summary>
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
