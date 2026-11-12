using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using Il2Cpp;

namespace FeatsByEMPEROR
{
    /// <summary>
    /// Progress of our feats, for the whole profile like the game's own feats (all saves count, unlocked stays unlocked).
    /// Kept in UserData/FeatsByEMPEROR.json next to MelonPreferences.
    /// </summary>
    internal static class Progress
    {
        sealed class Data
        {
            public Dictionary<string, float> Values { get; set; } = new();
            public List<string> Unlocked { get; set; } = new();
            public Dictionary<string, List<string>> Sets { get; set; } = new();     // profile-wide sets (regions seen, places slept)
            public Dictionary<string, float> Tally { get; set; } = new();           // profile-wide counters by key (nights per house)
            public Dictionary<string, World> Worlds { get; set; } = new();          // per game world (slot + seed)
        }

        static Data _d = new();
        static bool _dirty;
        static float _nextSave;

        static string FilePath => Path.Combine(MelonLoader.Utils.MelonEnvironment.UserDataDirectory, "FeatsBySeagull.json");

        public static void Load()
        {
            try
            {
                if (File.Exists(FilePath)) _d = JsonSerializer.Deserialize<Data>(File.ReadAllText(FilePath)) ?? new Data();
            }
            catch (Exception e) { FeatsMod.Error("progress file unreadable, starting clean: " + e.Message); _d = new Data(); }
            _d.Values ??= new(); _d.Unlocked ??= new(); _d.Sets ??= new(); _d.Tally ??= new(); _d.Worlds ??= new();
            FeatsMod.Log("прогресс загружен / progress loaded: " + string.Join(", ", _d.Values));
        }

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(_d, new JsonSerializerOptions { WriteIndented = true }));
                _dirty = false;
            }
            catch (Exception e) { FeatsMod.Error("progress save: " + e.Message); }
        }

        public static void Tick(float now)
        {
            if (_dirty && now >= _nextSave) { _nextSave = now + 20f; Save(); }
        }

        public static float Value(Def d) => _d.Values.TryGetValue(d.Id, out var v) ? v : 0f;
        public static bool IsUnlocked(Def d) => _d.Unlocked.Contains(d.Id);

        /// <summary>Game blocks feat progress in custom games (Feat.ShouldBlockIncrement); we do the same unless allowed in settings.</summary>
        static bool Blocked()
        {
            if (FeatsMod.CountInCustom.Value) return false;
            try { return GameManager.InCustomMode(); } catch { return false; }
        }

        public static void Add(string id, float amount)
        {
            if (amount <= 0f || float.IsNaN(amount)) return;
            var d = Defs.ById(id);
            if (d == null || IsUnlocked(d) || Blocked()) return;
            float v = Value(d) + amount;
            _d.Values[d.Id] = v;
            _dirty = true;
            Texts.Dirty = true;
            if (FeatsMod.DebugLog.Value) FeatsMod.Log($"{d.Id}: +{amount:0.###} = {v:0.###}/{d.Target}");
            if (v >= d.Target) Unlock(d, true);
        }

        /// <summary>Streak-style progress: keeps the best value reached.</summary>
        public static void Max(string id, float value)
        {
            if (float.IsNaN(value) || value <= 0f) return;
            var d = Defs.ById(id);
            if (d == null || IsUnlocked(d) || Blocked()) return;
            float v = Value(d);
            if (value <= v + 0.0005f) return;
            _d.Values[d.Id] = value;
            _dirty = true;
            Texts.Dirty = true;
            if (value >= d.Target) Unlock(d, true);
        }

        /// <summary>Adds a key to a profile-wide set; returns the set size, or -1 when the key was already there.</summary>
        public static int AddToSet(string set, string key)
        {
            if (!_d.Sets.TryGetValue(set, out var l)) _d.Sets[set] = l = new List<string>();
            if (l.Contains(key)) return -1;
            l.Add(key);
            _dirty = true;
            return l.Count;
        }

        public static float AddTally(string key, float amount)
        {
            _d.Tally.TryGetValue(key, out var v);
            v += amount;
            _d.Tally[key] = v;
            _dirty = true;
            return v;
        }

        public static void MarkDirty() => _dirty = true;
        public static bool CountsNow => !Blocked();

        public static World GetWorld(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            if (!_d.Worlds.TryGetValue(key, out var w) || w == null) { _d.Worlds[key] = w = new World(); _dirty = true; }
            w.Perfect ??= new(); w.HomeNights ??= new();
            return w;
        }

        public static void Set(Def d, float normalized)
        {
            normalized = Math.Clamp(normalized, 0f, 1f);
            _d.Values[d.Id] = normalized * d.Target;
            if (normalized >= 1f) Unlock(d, false);
            else _d.Unlocked.Remove(d.Id);
            Texts.Dirty = true;
            Save();
        }

        static void Unlock(Def d, bool kicker)
        {
            if (!_d.Unlocked.Contains(d.Id)) _d.Unlocked.Add(d.Id);
            _d.Values[d.Id] = Math.Max(Value(d), d.Target);
            Texts.Dirty = true;
            Save();
            FeatsMod.Msg($"подвиг открыт / feat unlocked: {d.Id}");
            if (kicker) FeatObjects.ShowUnlocked(d);
        }
    }

    /// <summary>State of one game world for the second batch of feats. Times are game hours played (TimeOfDay.GetHoursPlayedNotPaused).</summary>
    internal sealed class World
    {
        public double StartH { get; set; } = -1;
        public double LastIndoorH { get; set; } = -1;
        public double LastMeatH { get; set; } = -1;
        public double LastMatchH { get; set; } = -1;
        public double LastHeavyH { get; set; } = -1;
        public bool PhoenixUsed { get; set; }
        public double FortuneUntilH { get; set; } = -1;
        public int FortuneKind { get; set; } = -1;
        public double LastWakeH { get; set; } = -1000;
        public bool RitualCounted { get; set; }
        public string LastRegion { get; set; } = "";
        public Dictionary<string, int> HomeNights { get; set; } = new();
        public Dictionary<string, double> Perfect { get; set; } = new();
    }
}
