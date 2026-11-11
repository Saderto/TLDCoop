using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Растения отрастают по таймеру хоста (мод TinyTweaks-RespawnablePlants). Без кооп-поддержки этот мод
    // считал время у каждого своё: у одного шиповник уже отрос, у другого нет; сорванное другом у хоста вообще
    // не попадало в его таймер. Теперь:
    //   • хост помнит, когда сорвано каждое растение — им самим и друзьями, в любой локации (слот сохранения);
    //   • прошло столько дней, сколько стоит в настройке TinyTweaks, — растение отрастает у всех
    //     (сообщение «растение» с пометкой R|; у кого локация не загружена — когда туда придёт);
    //   • у друзей в мире хоста TinyTweaks сам растения не возвращает, только по слову хоста.
    // Нет TinyTweaks у хоста — растения не отрастают ни у кого, как в обычной игре.
    public static class PlantRegrow
    {
        public const string Mark = "R|";
        private static readonly Dictionary<string, float> Harvested = new();   // «сцена\nid» → часы игры хоста
        private static bool _applying;
        private static Type _ttSettings;
        private static bool _ttChecked;
        private static float _next;

        static PlantRegrow()
        {
            SlotStore.Register("plant", SaveLines, LoadLines);
        }

        public static void Init() { }

        // ---------- TinyTweaks ----------

        private static bool TinyTweaks()
        {
            if (_ttChecked) return _ttSettings != null;
            _ttChecked = true;
            try
            {
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = a.GetType("TinyTweaks.Extensions", false);
                    if (t == null) continue;
                    _ttSettings = a.GetType("TinyTweaks.Settings", false);
                    break;
                }
            }
            catch { }
            MelonLogger.Msg(_ttSettings != null ? "[растения] TinyTweaks-RespawnablePlants есть: растения отрастают по таймеру хоста" : "[растения] TinyTweaks-RespawnablePlants нет: растения не отрастают");
            return _ttSettings != null;
        }

        private static float RespawnHours()
        {
            int days = 45;
            try
            {
                var opt = _ttSettings?.GetField("options", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null);
                var f = opt?.GetType().GetField("respawnTime", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f != null) days = Math.Max(1, (int)f.GetValue(opt));
            }
            catch { }
            return days * 24f;
        }

        // Ставим свои префикс и постфикс на Extensions.Respawn у TinyTweaks (вызывается из CoopMod).
        public static void Patch(HarmonyLib.Harmony h)
        {
            try
            {
                MethodInfo m = null;
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    var t = a.GetType("TinyTweaks.Extensions", false);
                    if (t != null) { m = t.GetMethod("Respawn", BindingFlags.Static | BindingFlags.Public); break; }
                }
                if (m == null) return;
                h.Patch(m, prefix: new HarmonyMethod(typeof(PlantRegrow), nameof(RespawnPrefix)), postfix: new HarmonyMethod(typeof(PlantRegrow), nameof(RespawnPostfix)));
                MelonLogger.Msg("[растения] TinyTweaks: отрастание теперь решает хост");
            }
            catch (Exception e) { MelonLogger.Warning("[растения] патч TinyTweaks: " + e.Message); }
        }

        private static bool ClientInHostWorld => Session.Mode == Mode.Client && Session.InHostWorld;

        // У хоста растение, которое помнит наш таймер (сорвано в коопе), возвращаем только по нему: свой таймер
        // TinyTweaks не знает о сорванном другом и о нашем отрастании и вернул бы растение сразу после друга.
        // Свою запись TinyTweaks удаляет и при пропуске.
        private static bool RespawnPrefix(Harvestable h, out bool __state)
        {
            __state = Run(h);
            return __state;
        }

        private static bool Run(Harvestable h)
        {
            if (_applying) return true;
            if (ClientInHostWorld) return false;
            if (Session.Mode != Mode.Host || h == null) return true;
            try { return !Harvested.ContainsKey(ItemSync.SceneKey() + "\n" + WorldObjects.IdOf(h)); } catch { return true; }
        }

        // Постфикс Harmony идёт и при пропуске оригинала — тогда «отросло» не шлём.
        private static void RespawnPostfix(Harvestable h, bool __state)
        {
            if (!__state || _applying || Session.Mode != Mode.Host || h == null) return;
            try { Announce(ItemSync.SceneKey(), WorldObjects.IdOf(h), Mark + h.Serialize()); } catch { }
        }

        // ---------- Учёт сорванного (зовёт WorldObjects на своё и чужое «растение») ----------

        internal static void Note(string scene, string id, string state)
        {
            if (string.IsNullOrEmpty(scene) || string.IsNullOrEmpty(id)) return;
            string key = scene + "\n" + id;
            if (state != null && state.StartsWith(Mark, StringComparison.Ordinal)) { Harvested.Remove(key); return; }
            if (Session.Mode != Mode.Host || Harvested.ContainsKey(key)) return;
            float now;
            try { now = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); } catch { return; }
            Harvested[key] = now;
        }

        // Чужое «отросло» (WorldObjects.Apply).
        internal static void ApplyRegrow(Harvestable h)
        {
            if (h == null || !h.IsHarvested()) return;
            try { if (!h.RegisterAsPlantsHaversted) return; } catch { }   // не растение (TinyTweaks такие не возвращает)
            _applying = true;
            try
            {
                // То же, что делает TinyTweaks: растение снова на месте и не сорвано.
                h.gameObject.SetActive(true);
                h.m_Harvested = false;
                MelonLogger.Msg($"[растения] отросло: {WorldObjects.IdOf(h)}");
            }
            catch (Exception e) { MelonLogger.Warning("[растения] " + e.Message); }
            finally { _applying = false; }
        }

        // Хост: «отросло» всем, и себе — сейчас или когда придёт в ту локацию.
        private static void Announce(string scene, string id, string state)
        {
            var raw = WorldObjects.BuildFor(scene, WorldObjects.Kind.Harvest, id, state);
            Session.SendGameEvent(raw);
            Harvested.Remove(scene + "\n" + id);
        }

        public static void Tick(float now)
        {
            if (now < _next) return;
            _next = now + 30f;
            if (Session.Mode != Mode.Host || !GameApi.InGame() || Harvested.Count == 0 || !TinyTweaks()) return;
            float hours;
            try { hours = GameManager.GetTimeOfDayComponent().GetHoursPlayedNotPaused(); } catch { return; }
            float need = RespawnHours();
            var due = Harvested.Where(kv => hours - kv.Value >= need).Select(kv => kv.Key).Take(50).ToList();
            foreach (var key in due)
            {
                int nl = key.IndexOf('\n');
                string scene = key.Substring(0, nl), id = key.Substring(nl + 1);
                var raw = WorldObjects.BuildFor(scene, WorldObjects.Kind.Harvest, id, Mark);
                Session.SendGameEvent(raw);
                WorldObjects.Receive(raw);   // себе: сейчас, если мы там, иначе — при входе в локацию
                Harvested.Remove(key);
            }
            if (due.Count > 0) MelonLogger.Msg($"[растения] отросло по таймеру хоста: {due.Count}");
        }

        // ---------- Слот сохранения ----------

        private static IEnumerable<string> SaveLines()
        {
            foreach (var kv in Harvested)
            {
                int nl = kv.Key.IndexOf('\n');
                yield return kv.Key.Substring(0, nl) + "|" + kv.Value.ToString("0.##", CultureInfo.InvariantCulture) + "|" + kv.Key.Substring(nl + 1);
            }
        }

        private static void LoadLines(List<string> lines)
        {
            Harvested.Clear();
            foreach (var l in lines)
            {
                var a = l.Split(new[] { '|' }, 3);
                if (a.Length < 3) continue;
                if (float.TryParse(a[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float t)) Harvested[a[0] + "\n" + a[2]] = t;
            }
        }
    }
}
