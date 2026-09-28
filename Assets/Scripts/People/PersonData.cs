using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// One person made in the character creator (phase 4 of docs/CHARACTER_PLAN.md), small enough to keep in a save:
    /// name, body and face sliders, skin tone, hair style and colour, everyday and sleep clothes with their colours,
    /// traits and career. <see cref="PersonLook"/> puts it on a person.
    /// </summary>
    [System.Serializable]
    public class PersonData
    {
        [System.Serializable] public struct Tint { public string id; public Color colour; }

        public string name = "";
        public List<BodyShape.Value> sliders = new List<BodyShape.Value>();
        [Range(0f, 1f)] public float skin = 0.3f;           // fair to deep
        public string hair = "hair_long";
        public Color hairColour = new Color(0.12f, 0.08f, 0.06f);
        public List<string> everyday = new List<string> { "casual", "shoes" };
        public List<string> sleep = new List<string> { "pyjama_top", "pyjama_bottoms" };
        public List<Tint> colours = new List<Tint>();
        public List<string> traits = new List<string>();
        public string job = "";

        public float Slider(string n)
        {
            foreach (var v in sliders) if (v.name == n) return v.value;
            return 0f;
        }

        public void SetSlider(string n, float value)
        {
            value = Mathf.Clamp(value, -1f, 1f);
            for (int i = 0; i < sliders.Count; i++)
                if (sliders[i].name == n) { sliders[i] = new BodyShape.Value { name = n, value = value }; return; }
            sliders.Add(new BodyShape.Value { name = n, value = value });
        }

        public bool TryColour(string id, out Color c)
        {
            foreach (var t in colours) if (t.id == id) { c = t.colour; return true; }
            c = Color.white;
            return false;
        }

        public void SetColour(string id, Color c)
        {
            for (int i = 0; i < colours.Count; i++)
                if (colours[i].id == id) { colours[i] = new Tint { id = id, colour = c }; return; }
            colours.Add(new Tint { id = id, colour = c });
        }

        public PersonData Copy() => JsonUtility.FromJson<PersonData>(JsonUtility.ToJson(this));

        // ------------------------------------------------------------ choices offered by the creator

        public static readonly string[] Traits = { "Bookworm", "Cheerful", "Creative", "Foodie", "Handy", "Neat" };
        public static readonly string[] Jobs = { "", "Engineer", "Teacher", "Chef", "Artist", "Athlete" };
        public static readonly string[] BodySliders = { "gender", "height", "weight", "muscle", "proportions", "age" };
        public static readonly string[] FaceSliders = { "faceWidth", "faceLength", "jaw", "chin", "cheekbones", "eyeSize", "eyeSpacing", "noseWidth", "noseLength", "lips", "mouthWidth", "brows", "ears" };

        public static string Label(string slider)
        {
            switch (slider)
            {
                case "gender": return "Body";
                case "height": return "Height";
                case "weight": return "Weight";
                case "muscle": return "Muscle";
                case "proportions": return "Proportions";
                case "age": return "Age (18 to 60)";
                case "faceWidth": return "Face width";
                case "faceLength": return "Face length";
                case "jaw": return "Jaw";
                case "chin": return "Chin";
                case "cheekbones": return "Cheekbones";
                case "eyeSize": return "Eye size";
                case "eyeSpacing": return "Eye spacing";
                case "noseWidth": return "Nose width";
                case "noseLength": return "Nose length";
                case "lips": return "Lips";
                case "mouthWidth": return "Mouth width";
                case "brows": return "Brow height";
                case "ears": return "Ears";
            }
            return slider;
        }

        /// <summary>What the two ends of a slider do, for the small hint beside it.</summary>
        public static void Ends(string slider, out string low, out string high)
        {
            switch (slider)
            {
                case "gender": low = "feminine"; high = "masculine"; return;
                case "height": low = "short"; high = "tall"; return;
                case "weight": low = "slim"; high = "heavy"; return;
                case "muscle": low = "soft"; high = "muscular"; return;
                case "proportions": low = "ordinary"; high = "ideal"; return;
                case "age": low = "18"; high = "60"; return;
                case "faceWidth": case "noseWidth": case "mouthWidth": case "jaw": low = "narrow"; high = "wide"; return;
                case "faceLength": case "noseLength": low = "short"; high = "long"; return;
                case "chin": low = "soft"; high = "prominent"; return;
                case "cheekbones": low = "flat"; high = "high"; return;
                case "eyeSize": case "ears": low = "small"; high = "large"; return;
                case "eyeSpacing": low = "close"; high = "apart"; return;
                case "lips": low = "thin"; high = "full"; return;
                case "brows": low = "low"; high = "high"; return;
            }
            low = "less"; high = "more";
        }

        public static readonly (string name, Color c)[] HairColours =
        {
            ("Black", new Color(0.045f, 0.038f, 0.034f)), ("Dark brown", new Color(0.15f, 0.095f, 0.065f)),
            ("Brown", new Color(0.3f, 0.19f, 0.11f)), ("Auburn", new Color(0.42f, 0.17f, 0.09f)),
            ("Blonde", new Color(0.72f, 0.57f, 0.36f)), ("Grey", new Color(0.6f, 0.6f, 0.58f)),
        };

        public static readonly (string name, Color c)[] FabricColours =
        {
            ("Sage", new Color(0.46f, 0.55f, 0.5f)), ("Navy", new Color(0.16f, 0.2f, 0.34f)), ("Rose", new Color(0.72f, 0.46f, 0.5f)),
            ("Cream", new Color(0.88f, 0.84f, 0.76f)), ("Charcoal", new Color(0.2f, 0.2f, 0.22f)), ("Lilac", new Color(0.7f, 0.64f, 0.82f)),
            ("Mustard", new Color(0.78f, 0.62f, 0.25f)), ("Terracotta", new Color(0.66f, 0.36f, 0.26f)), ("White", new Color(0.93f, 0.93f, 0.93f)),
        };

        static readonly string[] WomenNames = { "Aina", "Sofea", "Hana", "Nadia", "Maya", "Iris", "Zara", "Lina", "Mei", "Priya" };
        static readonly string[] MenNames = { "Adam", "Irfan", "Danial", "Rayyan", "Haris", "Ethan", "Omar", "Luca", "Wei", "Arjun" };

        /// <summary>A believable random adult (the creator's Random button and the first person of a new household).</summary>
        public static PersonData Random(System.Random r, IEnumerable<string> taken = null)
        {
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var p = new PersonData();
            bool woman = r.NextDouble() < 0.5;
            p.SetSlider("gender", woman ? R(-1f, -0.75f) : R(0.75f, 1f));
            p.SetSlider("height", R(-0.6f, 0.6f));
            p.SetSlider("weight", R(-0.5f, 0.5f));
            p.SetSlider("muscle", R(-0.4f, 0.5f));
            p.SetSlider("proportions", R(0f, 0.6f));
            p.SetSlider("age", R(-0.4f, 0.4f));
            foreach (var f in FaceSliders) p.SetSlider(f, R(-0.45f, 0.45f));
            p.skin = R(0.05f, 0.9f);
            p.hair = woman ? "hair_long" : "hair_short";
            p.hairColour = HairColours[r.Next(HairColours.Length - 1)].c;
            bool modest = woman && r.NextDouble() < 0.5;
            p.everyday = modest ? new List<string> { "tunic", "trousers", "hijab", "shoes" } : r.NextDouble() < 0.5
                ? new List<string> { "casual", "shoes" } : new List<string> { "tunic", "trousers", "shoes" };
            p.sleep = new List<string> { "pyjama_top", "pyjama_bottoms" };
            foreach (var id in new[] { "tunic", "trousers", "hijab", "pyjama_top", "pyjama_bottoms" })
                p.SetColour(id, FabricColours[r.Next(FabricColours.Length)].c);
            var used = new HashSet<string>(taken ?? new string[0]);
            var pool = new List<string>(); foreach (var n in woman ? WomenNames : MenNames) if (!used.Contains(n)) pool.Add(n);
            p.name = pool.Count > 0 ? pool[r.Next(pool.Count)] : "Friend";
            var traits = new List<string>(Traits);
            for (int i = 0; i < 3; i++) { int k = r.Next(traits.Count); p.traits.Add(traits[k]); traits.RemoveAt(k); }
            p.job = Jobs[1 + r.Next(Jobs.Length - 1)];
            return p;
        }
    }

    /// <summary>The household made in the creator, kept in the save slot. Saves without one get the default household
    /// (Aina and Danial, Resources/People/DefaultHousehold.json, made with the creator's own sliders).</summary>
    [System.Serializable]
    public class HouseholdData
    {
        public const int MaxMembers = 4;
        public const string KeyBase = "dearlife.household";

        public List<PersonData> members = new List<PersonData>();

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
