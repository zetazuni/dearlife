using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// One person made in the character creator (phase 4 of docs/CHARACTER_PLAN.md), small enough to keep in a save:
    /// name, body and face sliders, skin tone, hair style and colour, eye colour, makeup, freckles, beard and tattoos,
    /// everyday and sleep clothes with their colours, traits and career. <see cref="PersonLook"/> puts it on a person.
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
        public List<string> swim;                                 // null or empty: swims in their everyday clothes
        public List<Tint> colours = new List<Tint>();
        public List<string> traits = new List<string>();
        public string job = "";

        // details (the creator's Details tab): drawn onto the skin by PersonLook, see tools/blender_skin_layers.py
        public Color eyeColour = new Color(0f, 0f, 0f, 0f);     // clear: the natural brown of the eye texture
        public Color lipColour = new Color(0.62f, 0.14f, 0.2f);
        [Range(0f, 1f)] public float lipstick;
        public Color blushColour = new Color(0.95f, 0.55f, 0.55f);
        [Range(0f, 1f)] public float blush;
        public Color shadowColour = new Color(0.45f, 0.32f, 0.28f);
        [Range(0f, 1f)] public float eyeshadow;
        [Range(0f, 1f)] public float liner;
        [Range(0f, 1f)] public float freckles;
        public string beard = "";                                 // "", stubble, goatee or full
        public List<string> tattoos = new List<string>();         // band, rose, star

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

        // ------------------------------------------------------------ sharing

        const string PersonPrefix = "DLP1:", HousePrefix = "DLH1:";

        /// <summary>A short code for this person that can be pasted into anyone's creator (the JSON, zipped, in base 64).</summary>
        public string ToCode() => PersonPrefix + Zip(JsonUtility.ToJson(this));

        public static PersonData FromCode(string code)
        {
            code = (code ?? "").Trim();
            if (!code.StartsWith(PersonPrefix)) return null;
            try { var p = JsonUtility.FromJson<PersonData>(Unzip(code.Substring(PersonPrefix.Length))); return p != null && p.sliders.Count > 0 ? p : null; }
            catch { return null; }
        }

        public static string HouseholdCode(HouseholdData h) => HousePrefix + Zip(JsonUtility.ToJson(h));

        public static HouseholdData HouseholdFromCode(string code)
        {
            code = (code ?? "").Trim();
            if (!code.StartsWith(HousePrefix)) return null;
            try { var h = JsonUtility.FromJson<HouseholdData>(Unzip(code.Substring(HousePrefix.Length))); return h != null && h.members.Count > 0 && h.members.Count <= HouseholdData.MaxMembers ? h : null; }
            catch { return null; }
        }

        static string Zip(string s)
        {
            var raw = System.Text.Encoding.UTF8.GetBytes(s);
            using (var ms = new System.IO.MemoryStream())
            {
                using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal)) gz.Write(raw, 0, raw.Length);
                return System.Convert.ToBase64String(ms.ToArray());
            }
        }

        static string Unzip(string b64)
        {
            var bytes = System.Convert.FromBase64String(b64);
            using (var ms = new System.IO.MemoryStream(bytes))
            using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress))
            using (var rd = new System.IO.StreamReader(gz, System.Text.Encoding.UTF8)) return rd.ReadToEnd();
        }

        /// <summary>Presets saved on this computer (one file per person), for the creator's preset list.</summary>
        public static string PresetDir => System.IO.Path.Combine(Application.persistentDataPath, "Presets");

        public void SavePreset()
        {
            System.IO.Directory.CreateDirectory(PresetDir);
            string safe = string.IsNullOrEmpty(name) ? "Person" : string.Concat(name.Split(System.IO.Path.GetInvalidFileNameChars()));
            System.IO.File.WriteAllText(System.IO.Path.Combine(PresetDir, safe + ".txt"), ToCode());
            presetCache = null;
        }

        static List<(string name, string path)> presetCache;
        static float presetTime = -10f;

        public static List<(string name, string path)> Presets()
        {
            // listed at most every two seconds (the creator asks every frame)
            if (presetCache != null && Time.unscaledTime - presetTime < 2f) return presetCache;
            presetTime = Time.unscaledTime;
            var list = presetCache = new List<(string name, string path)>();
            if (!System.IO.Directory.Exists(PresetDir)) return list;
            foreach (var f in System.IO.Directory.GetFiles(PresetDir, "*.txt")) list.Add((System.IO.Path.GetFileNameWithoutExtension(f), f));
            list.Sort((x, y) => string.Compare(x.name, y.name, System.StringComparison.OrdinalIgnoreCase));
            return list;
        }

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

        public static readonly (string name, Color c)[] EyeColours =
        {
            ("Brown", new Color(0f, 0f, 0f, 0f)), ("Dark brown", new Color(0.13f, 0.075f, 0.045f)), ("Hazel", new Color(0.36f, 0.25f, 0.1f)),
            ("Amber", new Color(0.55f, 0.33f, 0.08f)), ("Green", new Color(0.2f, 0.33f, 0.16f)), ("Blue", new Color(0.18f, 0.33f, 0.55f)),
            ("Grey", new Color(0.38f, 0.41f, 0.44f)),
        };

        public static readonly (string name, Color c)[] LipColours =
        {
            ("Nude", new Color(0.66f, 0.38f, 0.33f)), ("Rose", new Color(0.72f, 0.3f, 0.36f)), ("Berry", new Color(0.45f, 0.08f, 0.18f)),
            ("Red", new Color(0.66f, 0.06f, 0.08f)), ("Coral", new Color(0.86f, 0.36f, 0.3f)), ("Plum", new Color(0.36f, 0.12f, 0.2f)),
        };

        public static readonly (string name, Color c)[] BlushColours =
        {
            ("Pink", new Color(0.95f, 0.55f, 0.58f)), ("Peach", new Color(0.98f, 0.62f, 0.45f)), ("Rose", new Color(0.85f, 0.45f, 0.45f)),
        };

        public static readonly (string name, Color c)[] ShadowColours =
        {
            ("Brown", new Color(0.4f, 0.27f, 0.2f)), ("Taupe", new Color(0.45f, 0.38f, 0.34f)), ("Gold", new Color(0.72f, 0.56f, 0.3f)),
            ("Plum", new Color(0.4f, 0.24f, 0.36f)), ("Smoky", new Color(0.16f, 0.15f, 0.16f)),
        };

        public static readonly (string id, string name)[] Beards = { ("", "None"), ("stubble", "Stubble"), ("goatee", "Goatee"), ("full", "Full beard") };
        public static readonly (string id, string name)[] Tattoos = { ("band", "Forearm band"), ("rose", "Shoulder rose"), ("star", "Wrist star") };

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
            if (!modest)
            {
                double style = r.NextDouble();
                if (style < 0.2) p.everyday = new List<string> { "shirt", "slacks", "shoes" };
                else if (style < 0.3) p.everyday = new List<string> { "shirt", "slacks", "blazer", "shoes" };
                else if (style < 0.42) p.everyday = new List<string> { "tanktop", "shorts", "shoes" };
            }
            p.sleep = new List<string> { "pyjama_top", "pyjama_bottoms" };
            p.swim = modest ? new List<string>() : new List<string> { woman ? "swimsuit" : "swimshorts" };
            foreach (var id in new[] { "tunic", "trousers", "hijab", "pyjama_top", "pyjama_bottoms", "slacks", "blazer", "shorts", "swimsuit", "swimshorts" })
                p.SetColour(id, FabricColours[r.Next(FabricColours.Length)].c);
            p.SetColour("shirt", r.NextDouble() < 0.5 ? FabricColours[8].c : FabricColours[r.Next(FabricColours.Length)].c);   // shirts are often white
            p.SetColour("tanktop", FabricColours[r.Next(FabricColours.Length)].c);
            var used = new HashSet<string>(taken ?? new string[0]);
            var pool = new List<string>(); foreach (var n in woman ? WomenNames : MenNames) if (!used.Contains(n)) pool.Add(n);
            p.name = pool.Count > 0 ? pool[r.Next(pool.Count)] : "Friend";
            var traits = new List<string>(Traits);
            for (int i = 0; i < 3; i++) { int k = r.Next(traits.Count); p.traits.Add(traits[k]); traits.RemoveAt(k); }
            p.job = Jobs[1 + r.Next(Jobs.Length - 1)];
            // details: mostly brown eyes, now and then some makeup, a beard or freckles
            double e = r.NextDouble();
            p.eyeColour = e < 0.45 ? EyeColours[0].c : e < 0.75 ? EyeColours[1].c : EyeColours[2 + r.Next(EyeColours.Length - 2)].c;
            if (woman && r.NextDouble() < 0.6)
            {
                p.lipColour = LipColours[r.Next(LipColours.Length)].c; p.lipstick = R(0.25f, 0.7f);
                p.blushColour = BlushColours[r.Next(BlushColours.Length)].c; p.blush = R(0f, 0.5f);
                p.shadowColour = ShadowColours[r.Next(ShadowColours.Length)].c; p.eyeshadow = R(0f, 0.5f);
                p.liner = r.NextDouble() < 0.5 ? R(0.4f, 0.9f) : 0f;
            }
            if (!woman && r.NextDouble() < 0.5) p.beard = Beards[1 + r.Next(Beards.Length - 1)].id;
            if (r.NextDouble() < 0.15) p.freckles = R(0.3f, 0.8f);
            if (r.NextDouble() < 0.1) p.tattoos.Add(Tattoos[r.Next(Tattoos.Length)].id);
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
        public List<Bond> bonds = new List<Bond>();

        /// <summary>How two members of the household are related (by their places in the list).</summary>
        [System.Serializable] public class Bond { public int a, b; public string kind; }

        /// <summary>The relationships the creator offers, and how close the two start out.</summary>
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

        /// <summary>Someone left the household in the creator: the relationships of the people after them move up one place.</summary>
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
