using System.Collections.Generic;
using UnityEngine;

namespace Tiramisu
{
    /// <summary>
    /// The careers: five to choose from, six ranks each. Working at the right things earns experience, experience earns promotions, and a
    /// promotion raises the pay for every second worked and comes with a bonus. Everybody keeps their rank in each career if they change.
    /// </summary>
    public static class Careers
    {
        public class Track
        {
            public string name, blurb;
            public string[] titles;
            public string[] jobs;          // the interaction ids that count as this work
        }

        /// <summary>Seconds of work needed to reach each rank.</summary>
        public static readonly float[] XpAt = { 0f, 240f, 700f, 1400f, 2400f, 3800f };

        public static readonly Track[] All =
        {
            new Track { name = "Engineer", blurb = "Design and build at the desk.", jobs = new[] { "work", "freelance" },
                titles = new[] { "Trainee engineer", "Junior engineer", "Engineer", "Senior engineer", "Lead engineer", "Chief engineer" } },
            new Track { name = "Teacher", blurb = "Mark work and plan lessons at the teacher's desk.", jobs = new[] { "work", "lesson" },
                titles = new[] { "Teaching assistant", "Trainee teacher", "Teacher", "Senior teacher", "Head of department", "Principal" } },
            new Track { name = "Chef", blurb = "Cook for customers in the kitchen or on the barbecue.", jobs = new[] { "cookjob" },
                titles = new[] { "Kitchen hand", "Line cook", "Sous chef", "Head chef", "Executive chef", "Celebrity chef" } },
            new Track { name = "Artist", blurb = "Sketch and paint commissions at a desk.", jobs = new[] { "sketchjob" },
                titles = new[] { "Street sketcher", "Illustrator", "Gallery artist", "Studio painter", "Art director", "Master artist" } },
            new Track { name = "Athlete", blurb = "Train on the treadmill, bike and bench.", jobs = new[] { "trainjob" },
                titles = new[] { "Club member", "Amateur", "Semi-pro", "Professional", "Champion", "Legend" } },
        };

        public static Track Get(string name) { foreach (var t in All) if (t.name == name) return t; return null; }

        public static int Level(float xp)
        {
            int l = 1;
            for (int i = 1; i < XpAt.Length; i++) if (xp >= XpAt[i]) l = i + 1;
            return l;
        }

        public static string Title(string career, float xp)
        {
            var t = Get(career);
            return t == null ? "" : t.titles[Mathf.Clamp(Level(xp) - 1, 0, t.titles.Length - 1)];
        }

        /// <summary>0 to 1 towards the next rank (1 at the top).</summary>
        public static float Progress(float xp)
        {
            int l = Level(xp);
            if (l >= XpAt.Length) return 1f;
            return Mathf.InverseLerp(XpAt[l - 1], XpAt[l], xp);
        }

        public static int Pay(int level) => 5 + 3 * level;
        public static int Bonus(int level) => 120 * level;

        public static bool Allows(string career, string defId)
        {
            var t = Get(career);
            if (t == null) return false;
            foreach (var j in t.jobs) if (j == defId) return true;
            return false;
        }
    }
}
