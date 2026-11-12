using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using MelonLoader;
using MelonLoader.Utils;
using TrainerByEMPEROR.Shared;

namespace TrainerByEMPEROR
{
    // Текущие значения всех функций. Читается из игрового потока, меняется из потока связи с окном.
    internal static class State
    {
        private static readonly ConcurrentDictionary<string, float> Values = new();
        private static string FilePath => Path.Combine(MelonEnvironment.UserDataDirectory, "TLDTrainer.json");
        private static volatile bool _dirty;

        public static void Load()
        {
            foreach (var f in Features.All) Values[f.Id] = f.Default;
            try
            {
                if (!File.Exists(FilePath)) return;
                var saved = JsonSerializer.Deserialize<Dictionary<string, float>>(File.ReadAllText(FilePath));
                foreach (var kv in saved)
                {
                    var def = Features.Find(kv.Key);
                    if (def != null && def.Persist) Values[kv.Key] = kv.Value;
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Не удалось прочитать настройки: " + e.Message);
            }
        }

        public static void SaveIfDirty()
        {
            if (!_dirty) return;
            _dirty = false;
            try
            {
                var toSave = new Dictionary<string, float>();
                foreach (var f in Features.All)
                    if (f.Persist) toSave[f.Id] = Get(f.Id);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(toSave));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Не удалось сохранить настройки: " + e.Message);
            }
        }

        public static float Get(string id) => Values.TryGetValue(id, out var v) ? v : 0f;
        public static bool On(string id) => Get(id) > 0.5f;

        public static void Set(string id, float value)
        {
            var def = Features.Find(id);
            if (def == null) return;
            if (def.Kind == FeatureKind.Slider) value = Math.Clamp(value, def.Min, def.Max);
            Values[id] = value;
            _dirty = true;
        }

        public static void Toggle(string id)
        {
            Set(id, On(id) ? 0f : 1f);
            var def = Features.Find(id);
            Toast.Show($"{def?.Name}: {(On(id) ? L.T("ВКЛ", "ON") : L.T("выкл", "off"))}");
        }

        public static Dictionary<string, float> Snapshot() => new(Values);
    }
}
