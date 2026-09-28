using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    public enum Need { Hunger, Bladder, Energy, Fun, Social, Hygiene }
    public enum Skill { Cooking, Fitness, Logic, Charisma, Creativity }

    /// <summary>A short lived feeling ("Tasty meal", "Nice chat") with a mood value and an end time.</summary>
    public class Moodlet
    {
        public string text;
        public float value;      // -30 .. +30
        public float until;      // Time.time
    }

    public class Wish
    {
        public string key;       // interaction id or event name that fulfils it ("cook", "chat", "swim", "buy")
        public string text;
        public int reward;       // RM
        public bool done;
    }

    /// <summary>
    /// The inner life of a person or pet: six needs that slowly run down and are filled by using things, a mood that follows them,
    /// moodlets, traits, skills, wishes and friendships. Gentle by design: nobody is ever hurt, an empty bar only makes them grumpy,
    /// and a person who is too tired simply lies down where they are.
    /// </summary>
    public class Sim : MonoBehaviour
    {
        public static readonly List<Sim> All = new List<Sim>();

        public string displayName = "";
        public bool isPet;
        public readonly float[] needs = { 90f, 90f, 90f, 80f, 80f, 90f };
        public readonly float[] skillXp = new float[5];
        public readonly List<string> traits = new List<string>();
        public readonly List<Moodlet> moodlets = new List<Moodlet>();
        public readonly List<Wish> wishes = new List<Wish>();
        public readonly Dictionary<string, float> friendship = new Dictionary<string, float>();
        public string job = "";          // the career (see Careers), or empty
        public readonly Dictionary<string, float> careerXp = new Dictionary<string, float>();
        /// <summary>Seconds worked in the current career.</summary>
        public float jobXp
        {
            get { return job != "" && careerXp.TryGetValue(job, out var x) ? x : 0f; }
            set { if (job != "") careerXp[job] = value; }
        }
        public int JobLevel => Careers.Level(jobXp);
        public string JobTitle => Careers.Title(job, jobXp);
        public int wishesDone;

        // how fast each need falls, per second at normal speed (so a bar lasts a few minutes)
        static readonly float[] Rates = { 0.11f, 0.085f, 0.05f, 0.12f, 0.075f, 0.065f };
        static readonly string[] LowText = { "Hungry", "Needs the bathroom", "Tired", "Bored", "Lonely", "Feeling grubby" };
        static readonly string[] HighText = { "Well fed", "Comfortable", "Well rested", "Entertained", "Well connected", "Fresh and clean" };

        public float Mood { get; private set; }          // -100 .. 100
        public string MoodName => Mood < -55f ? "Miserable" : Mood < -20f ? "Grumpy" : Mood < 15f ? "Okay" : Mood < 50f ? "Happy" : "Radiant";
        public Color MoodColour => Mood < -20f ? new Color(0.85f, 0.35f, 0.35f) : Mood < 15f ? new Color(0.95f, 0.8f, 0.35f) : new Color(0.4f, 0.85f, 0.5f);

        public float Get(Need n) => needs[(int)n];
        public static string NeedName(Need n) => n.ToString();

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        // ------------------------------------------------------------ traits and starting looks

        public void Setup(string who, bool pet)
        {
            displayName = who; isPet = pet;
            switch (who)
            {
                case "James": traits.AddRange(new[] { "Bookworm", "Foodie", "Handy" }); job = "Engineer"; break;
                case "Lily": traits.AddRange(new[] { "Creative", "Cheerful", "Neat" }); job = "Teacher"; break;
                default: traits.AddRange(new[] { "Cuddly", "Curious" }); break;
            }
            if (!pet) { NewWish(); NewWish(); NewWish(); LoadCareer(); }
            friendship["James"] = friendship["Lily"] = friendship["Bedah"] = 0f;
            friendship[who] = 100f;
            if (who == "James") { friendship["Lily"] = 70f; friendship["Bedah"] = 40f; }
            if (who == "Lily") { friendship["James"] = 70f; friendship["Bedah"] = 50f; }
            if (who == "Bedah") { friendship["James"] = 40f; friendship["Lily"] = 50f; }
        }

        public bool Has(string trait) => traits.Contains(trait);

        // ------------------------------------------------------------ careers

        public void SetCareer(string name)
        {
            if (job == name) return;
            job = name;
            SaveCareer();
            Household.Toast($"{displayName} is now working as a {JobTitle.ToLower()} ({name.ToLower()}).");
            GameAudio.Play(GameAudio.Sfx.Chime);
        }

        /// <summary>A second of work: it can earn a promotion.</summary>
        public void WorkXp(float dt)
        {
            int before = JobLevel;
            jobXp += dt;
            if (JobLevel > before)
            {
                int level = JobLevel, bonus = Careers.Bonus(level);
                Household.Earn(bonus, $"{displayName}'s promotion bonus");
                Household.Toast($"{displayName} was promoted to {JobTitle.ToLower()}! New pay: {Household.Currency} {Careers.Pay(level)} a second, and a bonus of {Household.Currency} {bonus}.");
                GameAudio.Play(GameAudio.Sfx.Level);
                AddMoodlet("Proud of the promotion", 16f, 300f);
                Report("promotion");
                SaveCareer();
            }
        }

        string CareerKey => SaveSystem.Key("dearlife.career." + displayName);

        public void SaveCareer()
        {
            if (isPet || displayName == "") return;
            var sb = new System.Text.StringBuilder(job);
            foreach (var kv in careerXp) sb.Append('|').Append(kv.Key).Append(':').Append(kv.Value.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture));
            PlayerPrefs.SetString(CareerKey, sb.ToString());
        }

        void LoadCareer()
        {
            if (!PlayerPrefs.HasKey(CareerKey)) return;
            var parts = PlayerPrefs.GetString(CareerKey).Split('|');
            if (parts.Length > 0 && Careers.Get(parts[0]) != null) job = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                var kv = parts[i].Split(':');
                if (kv.Length == 2 && float.TryParse(kv[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v)) careerXp[kv[0]] = v;
            }
        }


        // ------------------------------------------------------------ the clock

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < 6 && !Cheats.NeedsFrozen; i++)
            {
                if (isPet && (i == (int)Need.Bladder || i == (int)Need.Hygiene || i == (int)Need.Social)) continue;
                float rate = Rates[i];
                if (i == (int)Need.Hunger && Has("Foodie")) rate *= 1.35f;
                if (i == (int)Need.Fun && Has("Cheerful")) rate *= 0.75f;
                if (i == (int)Need.Hygiene && Has("Neat")) rate *= 1.4f;
                if (i == (int)Need.Social && Has("Cuddly")) rate *= 1.3f;
                needs[i] = Mathf.Max(0f, needs[i] - rate * dt);
            }
            moodlets.RemoveAll(m => Time.time > m.until);
            RecomputeMood();
        }

        void RecomputeMood()
        {
            float sum = 0f; int n = 0;
            for (int i = 0; i < 6; i++)
            {
                if (isPet && (i == (int)Need.Bladder || i == (int)Need.Hygiene || i == (int)Need.Social)) continue;
                float v = needs[i];
                sum += (v - 55f) * 1.1f; n++;
                if (v < 22f) sum -= 25f;
            }
            float m = n > 0 ? sum / n : 0f;
            foreach (var ml in moodlets) m += ml.value * 0.6f;
            Mood = Mathf.Clamp(m, -100f, 100f);
        }

        /// <summary>The feelings to list in the status panel: the needs that are very low or very high, and the timed ones.</summary>
        public List<(string text, float value)> Feelings()
        {
            var l = new List<(string, float)>();
            for (int i = 0; i < 6; i++)
            {
                if (isPet && (i == (int)Need.Bladder || i == (int)Need.Hygiene || i == (int)Need.Social)) continue;
                if (needs[i] < 25f) l.Add((LowText[i], -(25f - needs[i]) * 0.9f - 6f));
                else if (needs[i] > 88f) l.Add((HighText[i], 8f));
            }
            foreach (var m in moodlets) l.Add((m.text, m.value));
            return l;
        }

        public void AddMoodlet(string text, float value, float seconds)
        {
            foreach (var m in moodlets) if (m.text == text) { m.until = Time.time + seconds; return; }
            moodlets.Add(new Moodlet { text = text, value = value, until = Time.time + seconds });
        }

        public void Give(Need n, float amount) => needs[(int)n] = Mathf.Clamp(needs[(int)n] + amount, 0f, 100f);

        /// <summary>The need with the lowest value, and that value.</summary>
        public Need Lowest(out float value)
        {
            int best = 0; float v = 999f;
            for (int i = 0; i < 6; i++)
            {
                if (isPet && (i == (int)Need.Bladder || i == (int)Need.Hygiene || i == (int)Need.Social)) continue;
                if (needs[i] < v) { v = needs[i]; best = i; }
            }
            value = v;
            return (Need)best;
        }

        // ------------------------------------------------------------ skills

        public int Level(Skill s) => Mathf.Min(10, Mathf.FloorToInt(Mathf.Sqrt(skillXp[(int)s] / 20f)));
        public float LevelProgress(Skill s)
        {
            int l = Level(s); float a = l * l * 20f, b = (l + 1) * (l + 1) * 20f;
            return Mathf.InverseLerp(a, b, skillXp[(int)s]);
        }

        public void AddXp(Skill s, float xp)
        {
            if (s == Skill.Logic && Has("Bookworm")) xp *= 1.4f;
            if (s == Skill.Creativity && Has("Creative")) xp *= 1.5f;
            if (s == Skill.Fitness && Has("Handy")) xp *= 1.1f;
            int before = Level(s);
            skillXp[(int)s] += xp;
            if (Level(s) > before) { Household.Toast($"{displayName}'s {s} skill is now level {Level(s)}!"); GameAudio.Play(GameAudio.Sfx.Level); AddMoodlet($"Proud of their {s}", 12f, 240f); }
        }

        // ------------------------------------------------------------ friendship

        public void Befriend(string other, float amount)
        {
            friendship.TryGetValue(other, out float f);
            friendship[other] = Mathf.Clamp(f + amount, 0f, 100f);
        }

        // ------------------------------------------------------------ wishes

        static readonly (string key, string text)[] WishPool =
        {
            ("cook", "Cook a proper meal"), ("shower", "Take a shower"), ("chat", "Have a long chat"), ("swim", "Go for a swim"),
            ("read", "Read a good book"), ("workout", "Work out"), ("tv", "Watch some TV"), ("pet", "Play with Bedah"),
            ("buy", "Buy something new for the house"), ("sleep", "Get a good night's sleep"), ("coffee", "Have a coffee"), ("sunbathe", "Relax by the pool"),
            ("stargaze", "Look at the stars"), ("work", "Do some work"),
        };

        public void NewWish()
        {
            for (int tries = 0; tries < 30; tries++)
            {
                var w = WishPool[Random.Range(0, WishPool.Length)];
                if (wishes.Exists(x => x.key == w.key && !x.done)) continue;
                wishes.Add(new Wish { key = w.key, text = w.text, reward = 200 + 50 * Random.Range(0, 4) });
                return;
            }
        }

        /// <summary>Something happened (an interaction id or "chat", "buy", "pet"): tick off a matching wish.</summary>
        public void Report(string key)
        {
            foreach (var w in wishes)
            {
                if (w.done || w.key != key) continue;
                w.done = true; wishesDone++;
                Household.Earn(w.reward, $"{displayName}'s wish came true: {w.text}");
                AddMoodlet("A wish came true!", 25f, 420f);
                GameAudio.Play(GameAudio.Sfx.Chime);
                wishes.Remove(w);
                NewWish();
                return;
            }
        }
    }
}
