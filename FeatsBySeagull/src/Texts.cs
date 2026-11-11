using System;
using System.Globalization;
using HarmonyLib;
using Il2Cpp;

namespace FeatsBySeagull
{
    /// <summary>Russian and English texts, chosen by the game language.</summary>
    internal static class Texts
    {
        // Our texts live in the game's own string tables (like every vanilla text), so the game never gets a string from the mod
        // while it builds its windows. Entries are (re)written when the language table changes and when progress text changes.
        static readonly System.Collections.Generic.Dictionary<string, string> _written = new();
        static IntPtr _table;
        static float _nextCheck;
        internal static bool Dirty = true;

        internal static void Tick(float now)
        {
            if (now < _nextCheck) return;
            _nextCheck = now + 1f;
            try
            {
                var t = Localization.s_CurrentLanguageStringTable;
                if (t == null) return;
                if (t.Pointer != _table || !t.DoesKeyExist(Defs.All[0].KeyName)) { _table = t.Pointer; _written.Clear(); Dirty = true; }
                if (!Dirty) return;
                Dirty = false;
                bool ru = Ru();
                foreach (var d in Defs.All)
                {
                    Put(t, d.KeyName, ru ? d.NameRu : d.NameEn);
                    Put(t, d.KeyDesc, Description(d, ru));
                    Put(t, d.KeyBonus, ru ? d.BonusRu : d.BonusEn);
                }
                var fb = Localization.s_FallbackStringTable;
                if (fb != null && fb.Pointer != t.Pointer)
                    foreach (var d in Defs.All)
                    {
                        PutRaw(fb, d.KeyName, d.NameEn);
                        PutRaw(fb, d.KeyDesc, d.TaskEn);
                        PutRaw(fb, d.KeyBonus, d.BonusEn);
                    }
            }
            catch (Exception e) { FeatsMod.Error("texts: " + e.Message); _nextCheck = now + 30f; }
        }

        static void Put(StringTable t, string key, string text)
        {
            if (_written.TryGetValue(key, out var old) && old == text) return;
            PutRaw(t, key, text);
            _written[key] = text;
        }

        static void PutRaw(StringTable t, string key, string text)
        {
            var e = t.DoesKeyExist(key) ? t.GetEntryFromKey(key) : t.AddEntryForKey(key);
            if (e == null || e.m_Languages == null) return;
            var l = e.m_Languages;
            for (int i = 0; i < l.Count; i++) l[i] = text;
            if (l.Count == 0) l.Add(text);
        }

        internal static bool Ru()
        {
            try
            {
                var l = Localization.Language;
                return l != null && l.IndexOf("russian", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            catch { return false; }
        }

        static string Num(float v, int dec, bool ru)
        {
            // the mod runtime has no culture data (invariant only): format invariant, Russian gets a decimal comma
            var c = CultureInfo.InvariantCulture;
            string r = dec <= 0 ? Math.Floor(v).ToString("0", c) : v.ToString("0." + new string('#', dec), c);
            return ru ? r.Replace('.', ',') : r;
        }

        public static string Description(Def d, bool ru)
        {
            if (d == null) return "";
            string task = ru ? d.TaskRu : d.TaskEn;
            if (Progress.IsUnlocked(d)) return task;
            float v = Math.Min(Progress.Value(d), d.Target);
            string unit = ru ? d.UnitRu : d.UnitEn;
            string tail = string.IsNullOrEmpty(unit) ? "" : " " + unit;
            return task + "\n" + (ru ? "Прогресс: " : "Progress: ") + Num(v, d.Decimals, ru) + " / " + Num(d.Target, 0, ru) + tail;
        }

    }
}
