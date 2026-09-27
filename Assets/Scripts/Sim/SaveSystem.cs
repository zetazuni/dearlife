using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// Most of the game already saves itself the moment something changes (money, furniture, careers and so
    /// on each call PlayerPrefs on their own). This is the one place that knows the whole list, so the main
    /// menu can tell a fresh install from a returning one, "New Game" can wipe cleanly without touching the
    /// player's settings, and the Settings window can offer an explicit "Save now" with a "last saved" time.
    /// </summary>
    public static class SaveSystem
    {
        const string LastSavedKey = "tiramisu.lastSaved";

        /// <summary>Game-state keys, wiped by New Game. Settings (audio, dark mode, graphics, display) are not in here on purpose.</summary>
        static readonly string[] StateKeys =
        {
            "tiramisu.funds", "tiramisu.built", "tiramisu.painted", "tiramisu.bought", "tiramisu.sold",
            "tiramisu.layout", "tiramisu.windows", "tiramisu.dogs", "tiramisu.hour", "tiramisu.season",
            "tiramisu.tutorial", "tiramisu.career.Amir", "tiramisu.career.Athirah",
        };

        public static bool HasSave => PlayerPrefs.HasKey("tiramisu.funds");

        /// <summary>Flushes everything to disk right now and records when, for the Settings window.</summary>
        public static void SaveNow()
        {
            Furniture.SaveAll();
            WindowWall.SaveAll();
            foreach (var sm in Sim.All) sm.SaveCareer();
            PlayerPrefs.SetString(LastSavedKey, System.DateTime.Now.ToString("o"));
            PlayerPrefs.Save();
        }

        public static string LastSavedText
        {
            get
            {
                if (!PlayerPrefs.HasKey(LastSavedKey)) return "Not saved yet this session.";
                if (!System.DateTime.TryParse(PlayerPrefs.GetString(LastSavedKey), out var t)) return "Not saved yet this session.";
                var span = System.DateTime.Now - t;
                if (span.TotalSeconds < 30) return "Saved just now.";
                if (span.TotalMinutes < 60) return "Saved " + Mathf.Max(1, (int)span.TotalMinutes) + " min ago.";
                return "Saved at " + t.ToString("t");
            }
        }

        /// <summary>Wipes the save (not the player's settings) so the next boot starts fresh.</summary>
        public static void NewGame()
        {
            foreach (var k in StateKeys) PlayerPrefs.DeleteKey(k);
            PlayerPrefs.DeleteKey(LastSavedKey);
            PlayerPrefs.Save();
        }
    }
}
