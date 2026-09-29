using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Dearlife
{
    /// <summary>One thing a person can do with an object: how long it takes, how they stand or sit, what it fills, what it costs.</summary>
    public class InteractionDef
    {
        public string id, label;
        public float seconds = 10f;
        public CharacterRig.Pose pose = CharacterRig.Pose.Stand;
        public bool seat;                                  // uses a seat or bed of the object (its UseSpot)
        public bool needsTv;                               // the seat must face a TV
        public bool sitNear;                               // done sitting on a chair beside the object when there is one (a meal, desk work)
        public int cost;
        public bool job;                                   // pays money while working
        public Skill? skill; public float xp;
        public string moodlet; public float moodValue = 10f; public float moodSeconds = 300f;
        public string wish;                                // wish key it counts for (default: the id)
        public readonly List<(Need need, float perSecond)> fills = new List<(Need, float)>();
        public string category = "";

        public InteractionDef Fill(Need n, float total) { fills.Add((n, total / Mathf.Max(seconds, 1f))); return this; }
        public bool Fills(Need n) { foreach (var f in fills) if (f.need == n && f.perSecond > 0f) return true; return false; }
        public float Total(Need n) { foreach (var f in fills) if (f.need == n) return f.perSecond * seconds; return 0f; }
    }

    /// <summary>All the things to do, by object. Ids are the model ids ("fridge", "toilet"...).</summary>
    public static class InteractionTable
    {
        static readonly Dictionary<string, List<InteractionDef>> table = new Dictionary<string, List<InteractionDef>>();

        static InteractionDef Def(string id, string label, float sec, CharacterRig.Pose pose, params string[] on)
        {
            var d = new InteractionDef { id = id, label = label, seconds = sec, pose = pose };
            foreach (var o in on)
            {
                if (!table.TryGetValue(o, out var l)) { l = new List<InteractionDef>(); table[o] = l; }
                l.Add(d);
            }
            return d;
        }

        static InteractionTable()
        {
            var P = CharacterRig.Pose.Stand;
            // ---- eating and drinking
            Def("snack", "Grab a snack", 6f, CharacterRig.Pose.Eat, "fridge").Fill(Need.Hunger, 28f).Moodlet("Nice snack", 6f);
            Def("cook", "Cook a meal", 22f, CharacterRig.Pose.Cook, "kitchenrun", "kitchenisland", "fridge").Fill(Need.Hunger, 75f).Skilled(Skill.Cooking, 14f).Moodlet("Tasty home cooking", 16f).Costs(20);
            Def("takeaway", "Order takeaway", 8f, CharacterRig.Pose.Eat, "fridge", "diningtable", "kitchenisland").Near().Fill(Need.Hunger, 62f).Costs(35).Moodlet("Takeaway treat", 8f);
            Def("coffee", "Make coffee", 7f, CharacterRig.Pose.Drink, "espresso", "kitchenrun").Fill(Need.Energy, 16f).Fill(Need.Fun, 6f).Moodlet("Coffee buzz", 8f);
            Def("meal", "Have a meal", 14f, CharacterRig.Pose.Eat, "diningtable", "longdining").Near().Fill(Need.Hunger, 55f).Fill(Need.Social, 6f).Costs(15).Moodlet("Tasty meal", 12f);
            Def("grill", "Grill some food", 20f, CharacterRig.Pose.Cook, "bbq", "bbqcounter").Fill(Need.Hunger, 65f).Fill(Need.Fun, 12f).Skilled(Skill.Cooking, 12f).Costs(25).Moodlet("Barbecue feast", 14f);
            // ---- bathroom
            Def("toilet", "Use the toilet", 8f, CharacterRig.Pose.Sit, "toilet").Seated().Fill(Need.Bladder, 100f);
            Def("shower", "Take a shower", 24f, CharacterRig.Pose.Wash, "shower").Fill(Need.Hygiene, 100f).Fill(Need.Energy, 4f).Moodlet("Fresh and clean", 10f);
            Def("bath", "Take a bath", 30f, CharacterRig.Pose.Lie, "bathtub").Seated().Fill(Need.Hygiene, 100f).Fill(Need.Fun, 14f).Fill(Need.Energy, 8f).Moodlet("Lovely soak", 14f).wish = "shower";
            Def("washface", "Wash up", 5f, CharacterRig.Pose.Wash, "vanity").Fill(Need.Hygiene, 25f);
            // ---- rest
            Def("sleep", "Sleep", 40f, CharacterRig.Pose.Lie, "platformbed", "platformbed_e").Seated().Fill(Need.Energy, 100f).Fill(Need.Bladder, -12f).Moodlet("Slept like a log", 14f);
            Def("nap", "Take a nap", 16f, CharacterRig.Pose.Lie, "platformbed", "platformbed_e", "lounger", "hammock").Seated().Fill(Need.Energy, 32f).Moodlet("Refreshing nap", 6f).wish = "sleep";
            Def("relax", "Relax", 14f, CharacterRig.Pose.Sit, "sofa", "modern_arm_chair_01", "beanbag", "outdoorsectional", "gardenbench").Seated().Fill(Need.Energy, 12f).Fill(Need.Fun, 8f);
            Def("tv", "Watch TV", 24f, CharacterRig.Pose.Sit, "sofa", "modern_arm_chair_01", "beanbag").Seated().TvOnly().Fill(Need.Fun, 42f).Fill(Need.Energy, 6f).Moodlet("Good show", 8f);
            Def("sunbathe", "Sunbathe", 22f, CharacterRig.Pose.Lie, "lounger").Seated().Fill(Need.Fun, 30f).Fill(Need.Energy, 14f).Moodlet("Sun on the skin", 10f);
            Def("chairsit", "Sit down", 12f, CharacterRig.Pose.Sit, "diningchair", "barstool", "officechair").Seated().Fill(Need.Energy, 6f);
            // ---- fun
            Def("read", "Read a book", 18f, CharacterRig.Pose.Read, "bookcase").Fill(Need.Fun, 34f).Skilled(Skill.Logic, 10f).Moodlet("Lost in a story", 10f);
            Def("cycle", "Ride the spin bike", 20f, CharacterRig.Pose.Sit, "spinbike").Seated().Fill(Need.Fun, 14f).Fill(Need.Energy, -14f).Fill(Need.Hygiene, -18f).Skilled(Skill.Fitness, 16f).Moodlet("Endorphins", 12f).wish = "workout";
            Def("workout", "Work out", 20f, CharacterRig.Pose.Exercise, "treadmill", "weightbench", "punchbag", "yogamat", "dumbbells").Fill(Need.Fun, 14f).Fill(Need.Energy, -14f).Fill(Need.Hygiene, -18f).Skilled(Skill.Fitness, 16f).Moodlet("Endorphins", 12f);
            Def("stargaze", "Look at the stars", 16f, CharacterRig.Pose.Read, "telescope").Fill(Need.Fun, 30f).Skilled(Skill.Creativity, 8f).Moodlet("Starstruck", 10f);
            Def("guitar", "Play the guitar", 32f, CharacterRig.Pose.Guitar, "guitar").Fill(Need.Fun, 52f).Fill(Need.Social, 6f).Skilled(Skill.Creativity, 16f).Moodlet("Made some music", 12f);
            Def("synth", "Play the synth", 32f, CharacterRig.Pose.Keys, "synth").Fill(Need.Fun, 52f).Fill(Need.Social, 6f).Skilled(Skill.Creativity, 16f).Skilled(Skill.Logic, 4f).Moodlet("Made some music", 12f);
            Def("tinker", "Tinker with a project", 20f, CharacterRig.Pose.Work, "printer3d", "robotarm", "workbench").Fill(Need.Fun, 24f).Skilled(Skill.Logic, 12f).Skilled(Skill.Creativity, 6f);
            Def("draw", "Draw a plan", 16f, CharacterRig.Pose.Work, "whiteboard", "chalkboard").Fill(Need.Fun, 22f).Skilled(Skill.Creativity, 14f);
            Def("fire", "Warm up by the fire", 18f, CharacterRig.Pose.Crouch, "firepit").Fill(Need.Fun, 22f).Fill(Need.Social, 6f).Moodlet("Cosy fire", 8f).wish = "sunbathe";
            Def("coin", "Toss a coin", 4f, CharacterRig.Pose.Happy, "fountain").Fill(Need.Fun, 10f).Costs(1).Moodlet("Made a wish", 6f);
            Def("mail", "Check the mail", 5f, P, "mailbox").Fill(Need.Fun, 6f);
            Def("swim", "Go for a swim", 24f, CharacterRig.Pose.Swim, "pool").Fill(Need.Fun, 45f).Fill(Need.Hygiene, 12f).Fill(Need.Energy, -8f).Skilled(Skill.Fitness, 14f).Moodlet("Splashing about", 12f);
            // ---- work
            Def("work", "Work", 36f, CharacterRig.Pose.Work, "officedesk", "teacherdesk").Near().AsJob().Fill(Need.Fun, -10f).Skilled(Skill.Logic, 8f).Fill(Need.Energy, -12f);
            Def("freelance", "Freelance on the computer", 24f, CharacterRig.Pose.Work, "officedesk").Near().AsJob(0.6f).Skilled(Skill.Logic, 12f).Fill(Need.Energy, -8f);
            Def("lesson", "Plan a lesson", 24f, CharacterRig.Pose.Work, "teacherdesk").Near().AsJob(0.6f).Skilled(Skill.Charisma, 10f).Fill(Need.Energy, -8f);
            Def("cookjob", "Cook for customers", 26f, CharacterRig.Pose.Cook, "kitchenrun", "kitchenisland", "bbqcounter").AsJob(0.9f).Skilled(Skill.Cooking, 12f).Fill(Need.Energy, -10f).Fill(Need.Fun, -6f);
            Def("sketchjob", "Sketch a commission", 26f, CharacterRig.Pose.Work, "officedesk", "teacherdesk").Near().AsJob(0.8f).Skilled(Skill.Creativity, 12f).Fill(Need.Energy, -8f);
            Def("trainjob", "Train for pay", 22f, CharacterRig.Pose.Exercise, "treadmill", "weightbench").AsJob(0.9f).Skilled(Skill.Fitness, 12f).Fill(Need.Energy, -14f).Fill(Need.Hygiene, -8f);
            Def("files", "Sort the files", 12f, CharacterRig.Pose.Crouch, "filecabinet").Skilled(Skill.Logic, 6f).Fill(Need.Fun, -4f);
            Def("laundry", "Do the laundry", 10f, CharacterRig.Pose.Crouch, "washer", "dryer").Fill(Need.Fun, -4f).Moodlet("Fresh laundry", 5f);
        }

        // small builder helpers on the last def
        static InteractionDef Skilled(this InteractionDef d, Skill s, float xp) { d.skill = s; d.xp = xp; return d; }
        static InteractionDef Costs(this InteractionDef d, int c) { d.cost = c; return d; }
        static InteractionDef Seated(this InteractionDef d) { d.seat = true; return d; }
        static InteractionDef TvOnly(this InteractionDef d) { d.needsTv = true; return d; }
        static InteractionDef Near(this InteractionDef d) { d.sitNear = true; return d; }
        static InteractionDef AsJob(this InteractionDef d, float pay = 1f) { d.job = true; d.moodValue = 6f; d.xp = Mathf.Max(d.xp, 6f); d.category = pay.ToString("0.0"); return d; }
        static InteractionDef Moodlet(this InteractionDef d, string t, float v) { d.moodlet = t; d.moodValue = v; return d; }

        public static string BaseId(string name)
        {
            int h = name.IndexOf('#'); if (h > 0) name = name.Substring(0, h);
            int sp = name.IndexOf(' '); if (sp > 0) name = name.Substring(0, sp);
            return name;
        }

        public static List<InteractionDef> For(string id) => table.TryGetValue(BaseId(id), out var l) ? l : null;

        /// <summary>Every (object id, interaction) that fills a need, for the autonomy of the people.</summary>
        public static IEnumerable<KeyValuePair<string, List<InteractionDef>>> All => table;
    }

    /// <summary>Put on a piece of furniture (or the pool) that offers things to do.</summary>
    public class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new List<Interactable>();

        public string id;
        public List<InteractionDef> defs = new List<InteractionDef>();
        public Vector3 customStand;                // for things without a shape (the pool)
        public bool hasCustomStand;
        public Vector3 customFace;
        public Vector2 poolHalf;                   // half the size of the pool (x, z), so people swim inside it
        public Character user;                     // somebody is using it now

        void OnEnable() { if (!All.Contains(this)) All.Add(this); }
        void OnDisable() => All.Remove(this);

        public static Interactable Attach(GameObject go, string modelId)
        {
            var defs = InteractionTable.For(modelId);
            if (defs == null) return null;
            var it = go.GetComponent<Interactable>();
            if (!it) it = go.AddComponent<Interactable>();
            it.id = InteractionTable.BaseId(modelId);
            it.defs = defs;
            // the bathroom pieces that come alive when used
            if (it.id == "bathtub" && !go.GetComponent<BathTub>()) go.AddComponent<BathTub>();
            if (it.id == "shower" && !go.GetComponent<ShowerStall>()) go.AddComponent<ShowerStall>();
            return it;
        }

        public Vector3 Centre
        {
            get
            {
                if (hasCustomStand) return customFace;
                var f = GetComponent<Furniture>();
                if (f) return transform.TransformPoint(f.LocalBounds.center);
                return transform.position;
            }
        }

        /// <summary>A free spot to stand at, in front first, then round the object; null when it is walled in.</summary>
        public bool StandPoint(Vector3 from, out Vector3 point)
        {
            var all = StandPoints(from);
            point = all.Count > 0 ? all[0] : default;
            return all.Count > 0;
        }

        /// <summary>Every spot to stand at, the best first: a side that turns out to be cut off (a path that does not get there) is skipped for the next.</summary>
        public List<Vector3> StandPoints(Vector3 from)
        {
            var list = new List<Vector3>();
            var filterP = new NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = ~(1 << DearlifeNav.StairsArea) };
            if (hasCustomStand)
            {
                list.Add(customStand);
                if (poolHalf.x > 0f)
                {
                    // round the pool's edge, nearest first
                    var ring = new List<(Vector3 p, float d)>();
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * 22.5f * Mathf.Deg2Rad;
                        var p = customFace + new Vector3(Mathf.Cos(a) * (poolHalf.x + 0.9f), 0f, Mathf.Sin(a) * (poolHalf.y + 0.9f));
                        p.y = customStand.y;
                        if (NavMesh.SamplePosition(p, out var h, 0.8f, filterP)) ring.Add((h.position, (h.position - from).sqrMagnitude));
                    }
                    ring.Sort((x, y) => x.d.CompareTo(y.d));
                    foreach (var r in ring) list.Add(r.p);
                }
                return list;
            }
            var found = new List<(Vector3 p, float d)>();
            var filter0 = new NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = ~(1 << DearlifeNav.StairsArea) };
            var stall = GetComponent<ShowerStall>();
            if (stall && NavMesh.SamplePosition(stall.Outside, out var sh, 0.4f, filter0)) { list.Add(sh.position); return list; }   // in front of the door
            var f = GetComponent<Furniture>();
            var lb = f ? f.LocalBounds : new Bounds(Vector3.zero, Vector3.one);
            var filter = new NavMeshQueryFilter { agentTypeID = DearlifeNav.AgentType, areaMask = ~(1 << DearlifeNav.StairsArea) };
            // a thing above the floor (a coffee machine on the counter, a printer on a desk, a board on the wall) is used from
            // the floor below it
            bool onTop = (f && f.small) || (NavMesh.SamplePosition(transform.position, out var below, 2.2f, filter) && transform.position.y - below.position.y > 0.3f);
            for (int k = 0; k < 8; k++)
            {
                float a = k * 45f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;                 // local: front first
                var worldDir = transform.rotation * dir;
                float reach = Mathf.Abs(dir.x) * lb.extents.x + Mathf.Abs(dir.z) * lb.extents.z + (onTop ? 0.75f : 0.55f);
                var p = transform.TransformPoint(lb.center) + worldDir * reach;
                p.y = transform.position.y - (onTop ? 0.9f : 0f);      // look for the floor, not the height of the thing
                if (!NavMesh.SamplePosition(p, out var hit, onTop ? 1.1f : 0.4f, filter)) continue;
                float dy = transform.position.y - hit.position.y;
                if (onTop ? (dy < -0.1f || dy > 1.9f) : Mathf.Abs(dy) > 0.5f) continue;
                found.Add((hit.position, (hit.position - from).sqrMagnitude + k * 0.5f));  // near, and front preferred
            }
            found.Sort((x, y) => x.d.CompareTo(y.d));
            foreach (var fp in found) list.Add(fp.p);
            return list;
        }
    }
}
