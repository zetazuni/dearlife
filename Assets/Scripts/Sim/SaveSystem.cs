using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// Ten independent save slots. Most systems already save themselves the moment something changes (money,
    /// furniture, careers and so on each call PlayerPrefs on their own); the only thing they needed to become
    /// slot aware was to run their key through <see cref="Key"/> instead of using it bare. This is the one
    /// place that knows the whole list of state keys, so New Game can wipe one slot cleanly without touching
    /// the player's settings (audio, dark mode, graphics, display - none of those are per slot).
    /// </summary>
    public static class SaveSystem
    {
        public const int MaxSlots = 5;
        const string SlotPrefKey = "dearlife.slot";
        const string LastSavedKeyBase = "dearlife.lastSaved";
        const string FundsKeyBase = "dearlife.funds";

        /// <summary>The slot every other system reads and writes right now. Slot 1 keeps the old, unsuffixed keys, so saves made before slots existed still work.</summary>
        public static int ActiveSlot { get; private set; } = Mathf.Clamp(PlayerPrefs.GetInt(SlotPrefKey, 1), 1, MaxSlots);

        /// <summary>Game-state keys, wiped by New Game. Settings are not in here on purpose.</summary>
        static readonly string[] StateKeys =
        {
            "dearlife.funds", "dearlife.built", "dearlife.painted", "dearlife.bought", "dearlife.sold",
            "dearlife.layout", "dearlife.windows", "dearlife.dogs", "dearlife.hour", "dearlife.season",
            "dearlife.tutorial", "dearlife.career.James", "dearlife.career.Lily", HouseholdData.KeyBase,
        };

        /// <summary>Turns a base key into the active slot's key. Every system that saves game state should read and write through this.</summary>
        public static string Key(string baseKey) => ActiveSlot <= 1 ? baseKey : baseKey + ".slot" + ActiveSlot;
        static string KeyFor(string baseKey, int slot) => slot <= 1 ? baseKey : baseKey + ".slot" + slot;

        public static bool HasSave => SlotHasSave(ActiveSlot);
        public static bool SlotHasSave(int slot) => PlayerPrefs.HasKey(KeyFor(FundsKeyBase, slot));

        /// <summary>A one-line summary for the slot picker: what's in the save, or that it's empty.</summary>
        public static string SlotSummary(int slot)
        {
            if (!SlotHasSave(slot)) return "Empty";
            int funds = PlayerPrefs.GetInt(KeyFor(FundsKeyBase, slot), 0);
            string season = "Spring";
            if (PlayerPrefs.HasKey(KeyFor("dearlife.season", slot)))
            {
                var names = new[] { "Spring", "Summer", "Autumn", "Winter" };
                season = names[Mathf.Clamp(PlayerPrefs.GetInt(KeyFor("dearlife.season", slot), 0), 0, 3)];
            }
            string phase = "Day";
            if (PlayerPrefs.HasKey(KeyFor("dearlife.hour", slot)))
            {
                float hr = PlayerPrefs.GetFloat(KeyFor("dearlife.hour", slot));
                phase = hr < 5f || hr >= 21f ? "Night" : hr < 7.5f ? "Dawn" : hr < 17f ? "Day" : hr < 19.5f ? "Sunset" : "Evening";
            }
            return $"RM {funds:N0}  ·  {season}  ·  {phase}";
        }

        public static string SlotLastSaved(int slot)
        {
            var k = KeyFor(LastSavedKeyBase, slot);
            if (!PlayerPrefs.HasKey(k) || !System.DateTime.TryParse(PlayerPrefs.GetString(k), out var t)) return "";
            var span = System.DateTime.Now - t;
            if (span.TotalMinutes < 1) return "just now";
            if (span.TotalHours < 1) return Mathf.Max(1, (int)span.TotalMinutes) + " min ago";
            if (span.TotalDays < 1) return t.ToString("t");
            return t.ToString("d MMM");
        }

        public static void SetActiveSlot(int slot)
        {
            ActiveSlot = Mathf.Clamp(slot, 1, MaxSlots);
            PlayerPrefs.SetInt(SlotPrefKey, ActiveSlot);
            PlayerPrefs.Save();
            PurchaseSave.ResetCache();     // a plain static class, so it survives the scene reload that follows a slot switch - clear it so it re-reads
        }

        /// <summary>Flushes everything to disk right now and records when, for the Settings window.</summary>
        public static void SaveNow()
        {
            Furniture.SaveAll();
            WindowWall.SaveAll();
            foreach (var sm in Sim.All) sm.SaveCareer();
            PlayerPrefs.SetString(Key(LastSavedKeyBase), System.DateTime.Now.ToString("o"));
            PlayerPrefs.Save();
        }

        public static string LastSavedText
        {
            get
            {
                var t = SlotLastSaved(ActiveSlot);
                return t == "" ? "Not saved yet this session." : "Saved " + t + ".";
            }
        }

        /// <summary>Wipes one slot's save (not the player's settings), leaving every other slot untouched.</summary>
        public static void NewGame(int slot)
        {
            // the careers of a created household are saved under their own names
            var hk = KeyFor(HouseholdData.KeyBase, slot);
            if (PlayerPrefs.HasKey(hk))
            {
                var h = JsonUtility.FromJson<HouseholdData>(PlayerPrefs.GetString(hk));
                if (h != null) foreach (var m in h.members) PlayerPrefs.DeleteKey(KeyFor("dearlife.career." + m.name, slot));
            }
            foreach (var k in StateKeys) PlayerPrefs.DeleteKey(KeyFor(k, slot));
            PlayerPrefs.DeleteKey(KeyFor(LastSavedKeyBase, slot));
            PlayerPrefs.Save();
        }
    }
}
