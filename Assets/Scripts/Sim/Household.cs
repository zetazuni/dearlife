using System.Collections.Generic;
using UnityEngine;

namespace Dearlife
{
    /// <summary>
    /// The household money (RM, ringgit): funds shown at the top, what things cost and sell for, pay for work, and the bills that come every
    /// few days. Gentle: bills that cannot be paid are simply postponed and the game never ends because of money.
    /// </summary>
    public class Household : MonoBehaviour
    {
        public static Household Instance { get; private set; }
        public const string Currency = "RM";
        const string PrefKey = "dearlife.funds";

        public int Funds { get; private set; } = 15000;
        public int LastBill { get; private set; }
        public int NextBillDay { get; private set; } = 3;
        public readonly List<(string text, int amount, float time)> Ledger = new List<(string, int, float)>();

        static string toast; static float toastUntil;
        static readonly List<string> log = new List<string>();
        public static IReadOnlyList<string> Log => log;

        int lastDay = -1;

        void Awake()
        {
            Instance = this;
            if (PlayerPrefs.HasKey(SaveSystem.Key(PrefKey))) Funds = PlayerPrefs.GetInt(SaveSystem.Key(PrefKey));
        }

        void OnDestroy() { if (Instance == this) Instance = null; }

        public static bool CanAfford(int amount) => Instance != null && Instance.Funds >= amount;

        public static bool Spend(int amount, string why)
        {
            if (Instance == null || amount <= 0) return true;
            if (Instance.Funds < amount) { Toast($"Not enough money for {why} ({Currency} {amount})."); GameAudio.Play(GameAudio.Sfx.No); return false; }
            Instance.Funds -= amount;
            Instance.Record(why, -amount);
            return true;
        }

        public static void Earn(int amount, string why)
        {
            if (Instance == null || amount <= 0) return;
            Instance.Funds += amount;
            Instance.Record(why, amount);
            if (amount >= 50) Toast($"+{Currency} {amount}: {why}");
        }

        void Record(string why, int amount)
        {
            Ledger.Insert(0, (why, amount, Time.time));
            if (Ledger.Count > 30) Ledger.RemoveAt(Ledger.Count - 1);
            PlayerPrefs.SetInt(SaveSystem.Key(PrefKey), Funds);
        }

        // ---- messages at the bottom of the screen and a short history

        public static void Toast(string text)
        {
            toast = text; toastUntil = Time.unscaledTime + 4.5f;
            log.Insert(0, text);
            if (log.Count > 40) log.RemoveAt(log.Count - 1);
        }

        public static string CurrentToast => Time.unscaledTime < toastUntil ? toast : null;

        // ---- price list: what things cost, and what they sell for

        static readonly Dictionary<string, int> Prices = new Dictionary<string, int>
        {
            { "sofa", 2400 }, { "marbletable", 900 }, { "geomrug", 350 }, { "diningtable", 1600 }, { "diningchair", 220 }, { "barstool", 180 }, { "bookcase", 700 },
            { "nightstand", 240 }, { "officedesk", 900 }, { "officechair", 380 }, { "teacherdesk", 850 }, { "platformbed", 2600 }, { "platformbed_e", 2600 }, { "wardrobe", 1300 },
            { "uplight", 260 }, { "beanbag", 320 }, { "yogamat", 90 }, { "treadmill", 2200 }, { "weightbench", 700 }, { "spinbike", 1500 }, { "punchbag", 480 },
            { "dumbbells", 160 }, { "tvunit", 2800 }, { "washer", 1700 }, { "dryer", 1500 }, { "fridge", 2900 }, { "bathtub", 3200 }, { "vanity", 1400 },
            { "towelrack", 150 }, { "planter", 160 }, { "planterbox", 240 }, { "flowerbed", 380 }, { "gardenbench", 520 }, { "lounger", 780 }, { "hammock", 640 },
            { "parasol", 480 }, { "bbq", 1200 }, { "bbqcounter", 1900 }, { "firepit", 950 }, { "lantern", 90 }, { "outdoorsectional", 3400 }, { "longdining", 2300 },
            { "cooler", 140 }, { "telescope", 1100 }, { "gnome", 60 }, { "flamingo", 55 }, { "mailbox", 180 }, { "filecabinet", 420 }, { "printer3d", 1800 },
            { "robotarm", 4200 }, { "waterdispenser", 520 }, { "basket", 70 }, { "wheelbarrow", 210 }, { "wateringcan", 40 }, { "hosereel", 130 }, { "toolchest", 620 },
            { "workbench", 980 }, { "bicycle", 800 }, { "beachball", 30 }, { "espresso", 690 }, { "globe", 120 }, { "candles", 45 }, { "mug", 15 }, { "fruitbowl", 60 },
            { "modern_arm_chair_01", 1500 }, { "side_table_01", 380 }, { "potted_plant_01", 260 },
            { "potted_plant_04", 220 }, { "calathea_orbifolia_01", 180 }, { "ceramic_vase_03", 130 }, { "book_encyclopedia_set_01", 85 }, { "desk_lamp_arm_01", 190 },
            { "cuttingboard", 35 }, { "utensils", 40 }, { "herbs", 45 }, { "towelstack", 55 }, { "cardboardboxes", 60 }, { "paintcans", 50 }, { "sparetyres", 220 },
            { "guitar", 1400 }, { "synth", 2600 }, { "adopt_dog", 1500 },
            { "avanza", 28000 }, { "mazda3", 42000 }, { "bmwm3", 95000 }, { "porsche911", 240000 },
        };

        public static int PriceOf(string id)
        {
            int h = id.IndexOf('#'); if (h > 0) id = id.Substring(0, h);
            int sp = id.IndexOf(' '); if (sp > 0) id = id.Substring(0, sp);
            return Prices.TryGetValue(id, out int p) ? p : 300;
        }

        public static int SellPrice(string id) => Mathf.RoundToInt(PriceOf(id) * 0.6f);

        // ---- days and bills

        float nextSave;

        void Update()
        {
            if (Time.unscaledTime > nextSave) { nextSave = Time.unscaledTime + 30f; foreach (var sm in Sim.All) sm.SaveCareer(); PlayerPrefs.Save(); }
            var dn = DayNightCycle.Instance;
            if (dn == null) return;
            if (lastDay < 0) { lastDay = dn.DayCount; return; }
            if (dn.DayCount != lastDay)
            {
                lastDay = dn.DayCount;
                if (lastDay >= NextBillDay) PayBills();
            }
        }

        void PayBills()
        {
            NextBillDay = lastDay + 3;
            int bill = 180 + 3 * Furniture.All.Count + Sim.All.Count * 20;
            LastBill = bill;
            if (Funds >= bill) { Funds -= bill; Record("Household bills", -bill); Toast($"The bills came: {Currency} {bill} paid."); }
            else Toast($"The bills ({Currency} {bill}) will wait until there is enough money. No hurry.");
        }

        /// <summary>Pay for one second of work.</summary>
        public static int PayPerSecond(Sim s)
        {
            return s ? Careers.Pay(s.JobLevel) + s.Level(Skill.Logic) : 6;
        }
    }
}
