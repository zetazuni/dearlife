using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// One person of the household, small enough to keep in a save: a name, which ready made model they are
    /// (<see cref="PersonModel"/>), traits and career. Chosen in the character selection after New Game.
    /// </summary>
    [System.Serializable]
    public class PersonData
    {
        public string name = "";
        public string model = "";             // a PersonModel id; empty in saves from before v0.49.0 (Residents picks one)
        public List<string> traits = new List<string>();
        public string job = "";

        public PersonData Copy() => JsonUtility.FromJson<PersonData>(JsonUtility.ToJson(this));

        public static readonly string[] Traits = { "Bookworm", "Cheerful", "Creative", "Foodie", "Handy", "Neat" };
        public static readonly string[] Jobs = { "", "Engineer", "Teacher", "Chef", "Artist", "Athlete" };

        static readonly string[] WomenNames = { "Aina", "Sofea", "Hana", "Nadia", "Maya", "Iris", "Zara", "Lina", "Mei", "Priya" };
        static readonly string[] MenNames = { "Adam", "Irfan", "Danial", "Rayyan", "Haris", "Ethan", "Omar", "Luca", "Wei", "Arjun" };

        /// <summary>A name that suits the model and nobody in the household has yet.</summary>
        public static string RandomName(System.Random r, bool woman, IEnumerable<string> taken = null)
        {
            var used = new HashSet<string>(taken ?? new string[0]);
            var pool = new List<string>();
            foreach (var n in woman ? WomenNames : MenNames) if (!used.Contains(n)) pool.Add(n);
            return pool.Count > 0 ? pool[r.Next(pool.Count)] : "Friend";
        }

        /// <summary>A new member for the selection screen: a model nobody has taken yet where there is one, a name to
        /// match, three traits and a career.</summary>
        public static PersonData Random(System.Random r, IEnumerable<string> takenNames = null, IEnumerable<string> takenModels = null)
        {
            var p = new PersonData();
            var models = Residents.Models;
            var used = new HashSet<string>(takenModels ?? new string[0]);
            PersonModel pick = null;
            foreach (var m in models) if (!used.Contains(m.id)) { pick = m; break; }
            if (!pick && models.Count > 0) pick = models[r.Next(models.Count)];
            p.model = pick ? pick.id : "";
            p.name = RandomName(r, !pick || pick.feminine >= 0.5f, takenNames);
            var traits = new List<string>(Traits);
            for (int i = 0; i < 3; i++) { int k = r.Next(traits.Count); p.traits.Add(traits[k]); traits.RemoveAt(k); }
            p.job = Jobs[1 + r.Next(Jobs.Length - 1)];
            return p;
        }
    }

    /// <summary>The household chosen after New Game, kept in the save slot. Saves without one get the default household
    /// (Aina and Danial, Resources/People/DefaultHousehold.json).</summary>
    [System.Serializable]
    public class HouseholdData
    {
        public const int MaxMembers = 4;
        public const string KeyBase = "dearlife.household";

        public List<PersonData> members = new List<PersonData>();
        public List<Bond> bonds = new List<Bond>();

        /// <summary>How two members of the household are related (by their places in the list).</summary>
        [System.Serializable] public class Bond { public int a, b; public string kind; }

        /// <summary>The relationships the selection offers, and how close the two start out.</summary>
        public static readonly (string kind, float friendship)[] Kinds =
        {
            ("Housemates", 45f), ("Friends", 62f), ("Partners", 82f), ("Married", 88f), ("Siblings", 68f), ("Family", 72f),
        };

        public string KindOf(int i, int j)
        {
            foreach (var bd in bonds) if ((bd.a == i && bd.b == j) || (bd.a == j && bd.b == i)) return bd.kind;
            return "Housemates";
        }

        public void SetKind(int i, int j, string kind)
        {
            bonds.RemoveAll(bd => (bd.a == i && bd.b == j) || (bd.a == j && bd.b == i));
            if (kind != "Housemates") bonds.Add(new Bond { a = i, b = j, kind = kind });
        }

        public static float StartFriendship(string kind)
        {
            foreach (var k in Kinds) if (k.kind == kind) return k.friendship;
            return 45f;
        }

        /// <summary>Someone left the household in the selection: the relationships of the people after them move up one place.</summary>
        public void RemoveMember(int index)
        {
            members.RemoveAt(index);
            bonds.RemoveAll(bd => bd.a == index || bd.b == index);
            foreach (var bd in bonds) { if (bd.a > index) bd.a--; if (bd.b > index) bd.b--; }
        }

        public static HouseholdData Load()
        {
            var k = SaveSystem.Key(KeyBase);
            if (!PlayerPrefs.HasKey(k)) return null;
            var h = JsonUtility.FromJson<HouseholdData>(PlayerPrefs.GetString(k));
            return h != null && h.members.Count > 0 ? h : null;
        }

        public static HouseholdData Default()
        {
            var t = Resources.Load<TextAsset>("People/DefaultHousehold");
            return t ? JsonUtility.FromJson<HouseholdData>(t.text) : null;
        }

        public void Save()
        {
            PlayerPrefs.SetString(SaveSystem.Key(KeyBase), JsonUtility.ToJson(this));
            PlayerPrefs.Save();
        }
    }
}
