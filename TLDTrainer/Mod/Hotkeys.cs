using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader;
using MelonLoader.Utils;
using TLDTrainer.Shared;
using UnityEngine;

namespace TLDTrainer
{
    // Горячие клавиши и сочетания. Назначаются в окне трейнера, хранятся в UserData/TLDTrainer.binds.json.
    internal static class Hotkeys
    {
        private sealed class Bind
        {
            public string Id;
            public KeyCode Key;
            public bool Ctrl, Shift, Alt;
        }

        private static readonly object Lock = new();
        private static Dictionary<string, string> _combos = new();
        private static List<Bind> _binds = new();

        private static string FilePath => Path.Combine(MelonEnvironment.UserDataDirectory, "TLDTrainer.binds.json");

        public static void Load()
        {
            var combos = Defaults();
            try
            {
                if (File.Exists(FilePath))
                    combos = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(FilePath)) ?? combos;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Не удалось прочитать клавиши: " + e.Message);
            }
            Apply(combos, save: false);
        }

        public static Dictionary<string, string> Defaults()
        {
            var d = new Dictionary<string, string>();
            foreach (var f in Features.All)
                if (Features.Bindable(f) && !string.IsNullOrEmpty(f.Hotkey)) d[f.Id] = f.Hotkey;
            return d;
        }

        public static Dictionary<string, string> Current()
        {
            lock (Lock) return new Dictionary<string, string>(_combos);
        }

        // Полностью заменяет назначения (вызывается из потока связи с окном).
        public static void Apply(Dictionary<string, string> combos, bool save = true)
        {
            var binds = new List<Bind>();
            var clean = new Dictionary<string, string>();
            foreach (var kv in combos)
            {
                var def = Features.Find(kv.Key);
                if (def == null || !Features.Bindable(def) || string.IsNullOrWhiteSpace(kv.Value)) continue;
                var b = Parse(kv.Key, kv.Value);
                if (b == null)
                {
                    MelonLogger.Warning($"Не понял клавишу «{kv.Value}» для {def.Name}");
                    continue;
                }
                binds.Add(b);
                clean[kv.Key] = kv.Value;
            }

            lock (Lock)
            {
                _combos = clean;
                _binds = binds;
            }

            if (!save) return;
            try
            {
                File.WriteAllText(FilePath, JsonSerializer.Serialize(clean));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Не удалось сохранить клавиши: " + e.Message);
            }
        }

        private static Bind Parse(string id, string combo)
        {
            var b = new Bind { Id = id };
            bool haveKey = false;
            foreach (var raw in combo.Split('+'))
            {
                var part = raw.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase)) b.Ctrl = true;
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) b.Shift = true;
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) b.Alt = true;
                else if (Enum.TryParse(part, true, out KeyCode k) && k != KeyCode.None)
                {
                    b.Key = k;
                    haveKey = true;
                }
                else return null;
            }
            return haveKey ? b : null;
        }

        public static void Handle()
        {
            List<Bind> binds;
            lock (Lock) binds = _binds;
            if (binds.Count == 0) return;

            bool ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            bool alt = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

            foreach (var b in binds)
            {
                if (!Input.GetKeyDown(b.Key)) continue;
                if (b.Ctrl != ctrl || b.Shift != shift || b.Alt != alt) continue;
                var def = Features.Find(b.Id);
                if (def == null) continue;
                if (def.Kind == FeatureKind.Toggle) State.Toggle(def.Id);
                else Run(def.Id);
            }
        }

        private static void Run(string id)
        {
            try
            {
                Cheats.RunAction(id);
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[{id}] {e.Message}");
                Toast.Show(e.Message);
            }
        }
    }
}
