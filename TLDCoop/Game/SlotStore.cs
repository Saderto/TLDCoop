using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;

namespace TLDCoop.Game
{
    // Данные мода прямо в слоте сохранения игры (своя запись «tldcoop» в m_Dict, рядом с «global» и локациями).
    // Едут вместе с сохранением: скопировал слот, переименовал, переслал другу — они при нём.
    //   id=…    постоянный номер мира. Раньше папка прогресса друзей считалась по имени слота, номеру игры и
    //           зерну — если хоть что-то из этого менялось (зерна ещё нет в начале загрузки), прогресс
    //           «терялся». Теперь при первом сохранении номер записывается в слот и дальше не меняется;
    //           для старых миров это тот же ключ, что был, поэтому их папки подхватываются.
    //   pack=…  рюкзаки на месте смерти (Game/DeathPacks.cs), plant=… отросшие растения (Game/PlantRegrow.cs).
    // Формат: строки «ключ=значение», сжатые так же, как остальные записи слота (Lzf).
    public static class SlotStore
    {
        public const string Key = "tldcoop";

        private sealed class Part
        {
            public string Prefix;
            public Func<IEnumerable<string>> Save;
            public Action<List<string>> Load;
        }

        private static readonly List<Part> Parts = new();
        private static string _idFor = "", _id = "";

        public static void Register(string prefix, Func<IEnumerable<string>> save, Action<List<string>> load)
        {
            Parts.Add(new Part { Prefix = prefix, Save = save, Load = load });
        }

        // Номер мира для этого слота (null — ещё не знаем, считаем по-старому).
        public static string WorldId(string saveName) => saveName == _idFor && _id.Length > 0 ? _id : null;

        internal static void OnSave(SlotData slot)
        {
            if (slot == null) return;
            try
            {
                string name = SaveGameSystem.GetCurrentSaveName() ?? "";
                if (name.Length == 0) return;
                if (_idFor != name || _id.Length == 0)
                {
                    _id = PlayerProgress.LegacyWorldKey() ?? "";
                    _idFor = name;
                }
                var sb = new StringBuilder();
                if (_id.Length > 0) sb.Append("id=").Append(Clean(_id)).Append('\n');
                foreach (var p in Parts)
                {
                    try
                    {
                        foreach (var line in p.Save()) if (!string.IsNullOrEmpty(line)) sb.Append(p.Prefix).Append('=').Append(Clean(line)).Append('\n');
                    }
                    catch (Exception e) { MelonLogger.Warning($"[слот] {p.Prefix}: " + e.Message); }
                }
                slot.m_Dict[Key] = new Il2CppStructArray<byte>(Lzf.StoreOnly(Encoding.UTF8.GetBytes(sb.ToString())));
            }
            catch (Exception e) { MelonLogger.Warning("[слот] записать данные мода: " + e.Message); }
        }

        internal static void OnRestore(string name)
        {
            var lines = new Dictionary<string, List<string>>();
            string id = "";
            try
            {
                var slot = string.IsNullOrEmpty(name) ? null : SaveGameSlots.GetSaveSlotFromName(name);
                Il2CppStructArray<byte> arr = null;
                if (slot != null && slot.m_Dict.TryGetValue(Key, out arr) && arr != null && arr.Length > 0)
                {
                    string text = Encoding.UTF8.GetString(Lzf.Decompress(arr));
                    foreach (var raw in text.Split('\n'))
                    {
                        int eq = raw.IndexOf('=');
                        if (eq <= 0) continue;
                        string k = raw.Substring(0, eq), v = raw.Substring(eq + 1);
                        if (k == "id") { id = v; continue; }
                        if (!lines.TryGetValue(k, out var list)) lines[k] = list = new List<string>();
                        list.Add(v);
                    }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[слот] прочитать данные мода: " + e.Message); }
            // При каждом переходе игра восстанавливает слот autosave (свою рабочую копию) — мир тот же,
            // номер мира остаётся за настоящим слотом, иначе при следующем сохранении он сменился бы.
            if (!IsAutosave(name))
            {
                _idFor = name ?? "";
                _id = id;
                if (id.Length > 0) MelonLogger.Msg($"[слот] {name}: мир {id}");
            }
            foreach (var p in Parts)
            {
                try { p.Load(lines.TryGetValue(p.Prefix, out var l) ? l : new List<string>()); }
                catch (Exception e) { MelonLogger.Warning($"[слот] {p.Prefix}: " + e.Message); }
            }
        }

        // Новая игра (GameManager.LaunchSandbox): игра слот не восстанавливает, поэтому данные мода прошлого мира
        // (рюкзаки смерти, отросшие растения, номер мира) забываем сами, иначе они запишутся в слот нового мира.
        internal static void ForgetWorld()
        {
            _idFor = "";
            _id = "";
            foreach (var p in Parts)
            {
                try { p.Load(new List<string>()); }
                catch (Exception e) { MelonLogger.Warning($"[слот] {p.Prefix}: " + e.Message); }
            }
        }

        private static bool IsAutosave(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            try { return name == SaveGameSlots.AUTOSAVE_SLOT_NAME; } catch { return name == "autosave"; }
        }

        private static string Clean(string s) => s.Replace('\n', ' ').Replace('\r', ' ');
    }

    [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
    internal static class Patch_ModSlotSave
    {
        private static void Postfix(SlotData slot) => SlotStore.OnSave(slot);
    }

    [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.RestoreGlobalData))]
    internal static class Patch_ModSlotRestore
    {
        private static void Postfix(string name) => SlotStore.OnRestore(name);
    }
}
