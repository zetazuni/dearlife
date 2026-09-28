using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Cheats for fun, switched on in Settings (Game tab) and used from a round menu on the mailbox (right click).
    /// Every cheat can be used as many times as you like.
    /// </summary>
    public static class Cheats
    {
        const string EnabledKey = "dearlife.cheats", FreezeKey = "dearlife.cheats.freezeNeeds";
        static bool loaded, enabled, freeze;

        static void Load()
        {
            if (loaded) return;
            loaded = true;
            enabled = PlayerPrefs.GetInt(EnabledKey, 0) == 1;
            freeze = PlayerPrefs.GetInt(FreezeKey, 0) == 1;
        }

        public static bool Enabled { get { Load(); return enabled; } }

        public static void SetEnabled(bool on)
        {
            Load();
            enabled = on;
            PlayerPrefs.SetInt(EnabledKey, on ? 1 : 0);
            if (on) Household.Toast("Cheats are on. Right click the mailbox to use them.");
        }

        /// <summary>Needs stay where they are (they still fill up when used). Only while cheats are on.</summary>
        public static bool NeedsFrozen { get { Load(); return enabled && freeze; } }
        public static bool FreezeSetting { get { Load(); return freeze; } }

        public static void ToggleFreeze()
        {
            Load();
            freeze = !freeze;
            PlayerPrefs.SetInt(FreezeKey, freeze ? 1 : 0);
            Household.Toast(freeze ? "Needs no longer drop." : "Needs drop normally again.");
            GameAudio.Play(GameAudio.Sfx.Ding);
        }

        // ---- money

        public static void AddMoney(int amount)
        {
            Household.Earn(amount, "Cheat");
            GameAudio.Play(GameAudio.Sfx.Buy);
        }

        // ---- needs

        public static void FillNeed(Sim s, Need n)
        {
            if (!s) return;
            s.needs[(int)n] = 100f;
            Household.Toast($"{s.displayName}'s {Sim.NeedName(n).ToLower()} is full.");
            GameAudio.Play(GameAudio.Sfx.Ding);
        }

        public static void FillAll(Sim s)
        {
            if (!s) return;
            for (int i = 0; i < s.needs.Length; i++) s.needs[i] = 100f;
            Household.Toast($"All of {s.displayName}'s needs are full.");
            GameAudio.Play(GameAudio.Sfx.Chime);
        }

        public static void FillEveryone()
        {
            foreach (var s in Sim.All) for (int i = 0; i < s.needs.Length; i++) s.needs[i] = 100f;
            Household.Toast("Everyone's needs are full, pets too.");
            GameAudio.Play(GameAudio.Sfx.Chime);
        }

        // ---- life

        public static void MaxSkills(Sim s)
        {
            if (!s) return;
            const float top = 2000f;   // Sim.Level is sqrt(xp / 20), so 2000 xp is level 10
            for (int i = 0; i < s.skillXp.Length; i++) s.skillXp[i] = Mathf.Max(s.skillXp[i], top);
            s.AddMoodlet("Master of everything", 15f, 300f);
            Household.Toast($"{s.displayName} has every skill at level 10.");
            GameAudio.Play(GameAudio.Sfx.Level);
        }

        public static void Promote(Sim s)
        {
            if (!s) return;
            if (string.IsNullOrEmpty(s.job)) { Household.Toast($"{s.displayName} has no career yet. Pick one in the status window (C)."); GameAudio.Play(GameAudio.Sfx.No); return; }
            int level = s.JobLevel;
            if (level >= Careers.XpAt.Length) { Household.Toast($"{s.displayName} is already at the top of the {s.job.ToLower()} career."); GameAudio.Play(GameAudio.Sfx.No); return; }
            s.jobXp = Careers.XpAt[level] - 0.01f;
            s.WorkXp(0.02f);   // the normal promotion: bonus, toast, moodlet and save
        }

        public static void MakeHappy(Sim s)
        {
            if (!s) return;
            s.AddMoodlet("Feeling fantastic", 30f, 600f);
            Household.Toast($"{s.displayName} is feeling fantastic.");
            GameAudio.Play(GameAudio.Sfx.Chime);
        }

        public static void BestFriends()
        {
            foreach (var a in Sim.All)
                foreach (var b in Sim.All)
                    if (a != b) a.Befriend(b.displayName, 100f);
            Household.Toast("Everyone in the house is best friends now.");
            GameAudio.Play(GameAudio.Sfx.Chime);
        }

        public static void GrantWish(Sim s)
        {
            if (!s) return;
            var w = s.wishes.Find(x => !x.done);
            if (w == null) { s.NewWish(); w = s.wishes.Find(x => !x.done); }
            if (w != null) s.Report(w.key);
        }

        // ---- world

        public static void SetHour(float h, string what)
        {
            var day = DayNightCycle.Instance;
            if (!day) return;
            day.SetHour(h);
            Household.Toast($"It's {what} now.");
            GameAudio.Play(GameAudio.Sfx.Click);
        }

        public static void NextSeason()
        {
            var sea = SeasonCycle.Instance;
            if (!sea) return;
            var next = (Season)(((int)sea.season + 1) % 4);
            sea.Choose(next);
            Household.Toast($"It's {next.ToString().ToLower()} now.");
            GameAudio.Play(GameAudio.Sfx.Click);
        }
    }
}
