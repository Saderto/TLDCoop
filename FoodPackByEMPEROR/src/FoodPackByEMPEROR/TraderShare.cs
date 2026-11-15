#nullable disable
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FoodPackByEMPEROR
{
    /// <summary>One queue for all EMPEROR mods at the trader (user's rule 11-16: at most six rows of ours on top of his own offers,
    /// and every kind of goods must get its turn). THE SAME FILE in FoodPack, Snacks, Noodles, Smokehouse, TeaCoffee and Smokes:
    /// each mod asks whether its kind of goods is in this aurora's six, and all of them compute the same answer from the aurora's
    /// number, the trust and the list of these mods that are loaded. A kind in the six shows one row.
    /// The kinds open at the current trust are put in an order that changes every cycle (one kind from each mod in turn, so a set is
    /// mixed), and each aurora takes the next six; a cycle is as many auroras as it takes to show every kind once.</summary>
    internal static class TraderShare
    {
        public const int MaxRows = 6;

        sealed class Kind
        {
            public string Mod, Name; public int Trust;
            public Kind(string mod, string name, int trust) { Mod = mod; Name = name; Trust = trust; }
        }

        // mod (assembly name), kind of goods, trust it opens at: the same table in the same order in every mod
        static readonly Kind[] All =
        {
            new Kind("FoodPackByEMPEROR", "can", 50), new Kind("FoodPackByEMPEROR", "snack", 50), new Kind("FoodPackByEMPEROR", "drink", 50),
            new Kind("FoodPackByEMPEROR", "ingredient", 50), new Kind("FoodPackByEMPEROR", "tea", 100), new Kind("FoodPackByEMPEROR", "alcohol", 150),
            new Kind("FoodPackByEMPEROR", "sake", 200),
            new Kind("SnacksByEMPEROR", "snack", 50), new Kind("SnacksByEMPEROR", "rare", 50),
            new Kind("NoodlesByEMPEROR", "noodle", 100),
            new Kind("SmokehouseByEMPEROR", "hot", 100), new Kind("SmokehouseByEMPEROR", "cold", 200),
            new Kind("TeaCoffeeByEMPEROR", "common", 50), new Kind("TeaCoffeeByEMPEROR", "good", 150), new Kind("TeaCoffeeByEMPEROR", "rare", 300),
            new Kind("SmokesByEMPEROR", "pack", 100), new Kind("SmokesByEMPEROR", "cigarillo", 150), new Kind("SmokesByEMPEROR", "lighter", 100),
            new Kind("SmokesByEMPEROR", "refill", 100), new Kind("SmokesByEMPEROR", "tube", 200), new Kind("SmokesByEMPEROR", "box", 200),
            new Kind("SmokesByEMPEROR", "pouch", 200), new Kind("SmokesByEMPEROR", "cutter", 200),
        };

        static uint Fnv(string s)
        {
            unchecked
            {
                uint h = 2166136261;
                foreach (char ch in s) { h ^= ch; h *= 16777619; }
                h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
                return h;
            }
        }

        static HashSet<string> loaded;

        /// <summary>Test hook: the mods taken as loaded instead of looking at the assemblies.</summary>
        public static void Assume(IEnumerable<string> mods) { loaded = new HashSet<string>(mods, StringComparer.Ordinal); key = null; }

        static bool Loaded(string mod)
        {
            if (loaded == null)
            {
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = null;
                    try { n = a.GetName().Name; } catch { }
                    if (n != null && n.EndsWith("ByEMPEROR", StringComparison.Ordinal)) set.Add(n);
                }
                loaded = set;
            }
            return loaded.Contains(mod);
        }

        static string key;
        static HashSet<string> on = new HashSet<string>();

        /// <summary>Whether this kind of goods of this mod is in the six of this aurora at this trust.</summary>
        public static bool On(string mod, string kind, int trust, int aurora)
        {
            string k = aurora.ToString(CultureInfo.InvariantCulture) + "|" + trust.ToString(CultureInfo.InvariantCulture);
            if (k != key) { on = Compute(trust, aurora); key = k; }
            return on.Contains(mod + "|" + kind);
        }

        static HashSet<string> Compute(int trust, int aurora)
        {
            var open = new List<Kind>();
            foreach (Kind c in All) if (trust >= c.Trust && Loaded(c.Mod)) open.Add(c);
            var res = new HashSet<string>(StringComparer.Ordinal);
            if (open.Count <= MaxRows)
            {
                foreach (Kind c in open) res.Add(c.Mod + "|" + c.Name);
                return res;
            }
            int sets = (open.Count + MaxRows - 1) / MaxRows;
            int n = aurora < 0 ? 0 : aurora;
            string cycle = (n / sets).ToString(CultureInfo.InvariantCulture);
            var mods = open.Select(c => c.Mod).Distinct().OrderBy(m => Fnv("mod|" + cycle + "|" + m)).ToList();
            var queues = mods.Select(m => new Queue<Kind>(open.Where(c => c.Mod == m).OrderBy(c => Fnv("kind|" + cycle + "|" + m + "|" + c.Name)))).ToList();
            var order = new List<Kind>();
            while (order.Count < open.Count)
                foreach (var q in queues)
                    if (q.Count > 0) order.Add(q.Dequeue());
            int start = (n % sets) * MaxRows;
            for (int j = 0; j < MaxRows; j++)
            {
                Kind c = order[(start + j) % order.Count];
                res.Add(c.Mod + "|" + c.Name);
            }
            return res;
        }

        /// <summary>For the log and the test: the six of this aurora.</summary>
        public static string Describe(int trust, int aurora)
        {
            var list = new List<string>();
            foreach (Kind c in All) if (On(c.Mod, c.Name, trust, aurora)) list.Add(c.Mod.Replace("ByEMPEROR", "") + ":" + c.Name);
            return string.Join(", ", list);
        }
    }
}
