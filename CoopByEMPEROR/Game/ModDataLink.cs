using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Связь с ModData (файл Mods/ModData/<слот>.moddata для других модов) без жёсткой ссылки на его dll.
    // ModData привязывается к слоту один раз и отпускает его только при выходе в меню. Друг грузит мир хоста
    // прямо из игры (из своего мира или перезаход), и записи модов уходили в .moddata его ЛИЧНОГО сохранения.
    // Перед загрузкой копии мира перепривязываем ModData к новому слоту, а личные записи модов (алкоголь
    // AlcoholMod, свои болезни AfflictionComponent) едут вместе с прогрессом друга у хоста, как эффекты FoodPack.
    // В одиночной игре и у хоста ничего не делаем.
    internal static class ModDataLink
    {
        // Личное состояние игрока в ModData (имя записи = имя мода + "_" + суффикс).
        internal static readonly string[] Personal = { "AlcoholMod_alcohol", "AfflictionComponent" };

        private static bool _tried;
        private static MethodInfo _close, _init, _read, _write;
        private static PropertyInfo _slotName;
        private static FieldInfo _affManager;   // AfflictionComponent.Mod.afflictionManager
        private static Dictionary<string, string> _saved;   // снимок в момент сохранения игры
        private static Dictionary<string, string> _last;    // последнее известное (из копии мира или снимка)

        private static bool Present { get { Init(); return _close != null && _init != null && _read != null && _write != null; } }

        private static void Init()
        {
            if (_tried) return;
            _tried = true;
            try
            {
                var core = AccessTools.TypeByName("ModData.ModDataCore");
                if (core == null) return;
                const BindingFlags S = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
                _close = core.GetMethod("CloseModDataSaveSlot", S, null, Type.EmptyTypes, null);
                _init = core.GetMethod("InitModDataCore", S, null, new[] { typeof(string) }, null);
                _read = core.GetMethod("ReadEntry", S, null, new[] { typeof(string), typeof(string) }, null);
                _write = core.GetMethod("WriteEntry", S, null, new[] { typeof(string), typeof(string), typeof(string) }, null);
                _slotName = core.GetProperty("modDataSaveSlotName", S);
                var mod = AccessTools.TypeByName("AfflictionComponent.Mod");
                if (mod != null) _affManager = mod.GetField("afflictionManager", S);
                MelonLogger.Msg($"[моды] ModData найден: перепривязка слота {(Present ? "есть" : "нет")}, AfflictionComponent {(_affManager != null ? "есть" : "нет")}");
            }
            catch (Exception e) { MelonLogger.Warning("[моды] связь с ModData: " + e.Message); }
        }

        private static string Read(string entry)
        {
            try { return _read.Invoke(null, new object[] { entry, null }) as string; } catch { return null; }
        }

        // Друг, игра сохраняется (после записей модов): снимок личных записей. Прогресс уходит хосту позже,
        // часто уже в меню, где ModData закрыт.
        internal static void OnSave()
        {
            if (Session.Mode != Mode.Client || !Session.InSharedWorld || !Present) return;
            var d = new Dictionary<string, string>();
            foreach (var k in Personal) { var v = Read(k); if (v != null) d[k] = v; }
            _saved = d;
            _last = d;
        }

        // Личные записи для прогресса; null — ModData нет или о состоянии ничего не известно.
        internal static Dictionary<string, string> Export()
        {
            if (!Present) return null;
            return _saved ?? _last;
        }

        // Друг, перед загрузкой копии мира в слот slot: отвязать ModData от прежнего слота, начать новый
        // с чистого файла и положить в него свои записи из копии мира. Свои болезни AfflictionComponent
        // читает только при создании менеджера: убираем старый, мод создаст новый на первой сцене игры.
        internal static void BeforeLoad(string slot, Dictionary<string, string> carried)
        {
            if (!Present || string.IsNullOrEmpty(slot)) return;
            try
            {
                string prev = null;
                try { prev = _slotName?.GetValue(null) as string; } catch { }
                _close.Invoke(null, null);
                // Номер слота игра отдаёт заново: старый файл с тем же именем не должен подхватиться.
                DeleteFile(slot);
                _init.Invoke(null, new object[] { slot });
                _saved = null;
                _last = carried ?? new Dictionary<string, string>();
                foreach (var kv in _last) _write.Invoke(null, new object[] { kv.Key, kv.Value, null });
                if (_affManager != null && _affManager.GetValue(null) is Component m && m != null)
                {
                    _affManager.SetValue(null, null);
                    UnityEngine.Object.Destroy(m.gameObject);
                }
                MelonLogger.Msg($"[моды] ModData: слот {prev ?? "—"} → {slot}, своих записей {_last.Count}");
            }
            catch (Exception e) { MelonLogger.Warning("[моды] ModData, новый слот: " + e.Message); }
        }

        // Смерть в коопе: алкоголь AlcoholMod и свои болезни AfflictionComponent — как у нового выжившего.
        // Оба мода читают ModData только при загрузке, поэтому чистим их живое состояние; при следующем сохранении
        // они сами запишут чистое в ModData, и в прогресс друга уйдёт уже оно. Модов нет — ничего не делаем.
        internal static void ResetPersonal()
        {
            if (Session.Mode == Mode.Off) return;
            try
            {
                var mod = AccessTools.TypeByName("AlcoholMod.AlcoholMod");
                var stateType = AccessTools.TypeByName("AlcoholMod.Core.AlcoholState");
                var inst = mod != null ? AccessTools.Property(mod, "Instance")?.GetValue(null) : null;
                var sys = inst != null ? AccessTools.Property(mod, "System")?.GetValue(inst) : null;
                if (sys is UnityEngine.Object o && o != null && stateType != null)
                {
                    // Сеттер State сбрасывает и счётчики жажды/усталости мода.
                    AccessTools.Property(sys.GetType(), "State")?.SetValue(sys, Activator.CreateInstance(stateType));
                    MelonLogger.Msg("[моды] смерть: алкоголь AlcoholMod сброшен");
                }
            }
            catch (Exception e) { MelonLogger.Warning("[моды] смерть, AlcoholMod: " + e.Message); }
            try
            {
                Init();
                // Менеджер не пересоздаём: без него мод не сохранил бы пустой список, и болезни вернулись бы из ModData.
                if (_affManager != null && _affManager.GetValue(null) is Component m && m != null &&
                    AccessTools.Field(m.GetType(), "m_Afflictions")?.GetValue(m) is System.Collections.IList list)
                {
                    int n = list.Count;
                    list.Clear();
                    MelonLogger.Msg($"[моды] смерть: своих болезней AfflictionComponent снято {n}");
                }
            }
            catch (Exception e) { MelonLogger.Warning("[моды] смерть, AfflictionComponent: " + e.Message); }
        }

        // Удалённый слот: его .moddata тоже (иначе новая игра под этим номером получила бы чужие записи модов).
        internal static void DeleteFile(string slot)
        {
            if (!Present || string.IsNullOrEmpty(slot)) return;
            try
            {
                string f = Path.Combine(MelonEnvironment.ModsDirectory, "ModData", slot + ".moddata");
                if (File.Exists(f)) { File.Delete(f); MelonLogger.Msg("[моды] удалил " + f); }
            }
            catch (Exception e) { MelonLogger.Warning("[моды] ModData, удаление " + slot + ": " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(SaveGameSystem), nameof(SaveGameSystem.SaveGlobalData))]
    internal static class Patch_ModDataSnapshot
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix()
        {
            try { ModDataLink.OnSave(); } catch { }
        }
    }
}
