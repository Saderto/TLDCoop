using System;
using System.Collections.Generic;
using System.Linq;
using Il2Cpp;
using Il2CppTLD.Gameplay;
using Il2CppTLD.Scenes;
using MelonLoader;
using UnityEngine;

namespace TLDCoop.Game
{
    // Новая кампания прямо из окна мода, без меню игры: режим сложности + стартовый регион.
    // Повторяет то, что делает меню «Выживание → Новая игра».
    public static class NewGame
    {
        public class Mode
        {
            public GameModeConfig Config;
            public int Type;      // ExperienceModeType
            public string Name;
        }

        public class Region
        {
            public RegionSpecification Spec;
            public string Key;    // имя ассета, по нему регион узнаётся у хоста и у клиента
            public string Name;
        }

        public static List<Mode> Modes = new();
        public static List<Region> Regions = new();
        private static bool _logged;

        // Списки строятся в главном меню (там все режимы и регионы уже загружены игрой).
        public static void Refresh()
        {
            Modes = new List<Mode>();
            Regions = new List<Region>();
            try
            {
                var manager = GameManager.GetExperienceModeManagerComponent();
                var modes = manager?.GetAvailableGameModes();
                if (modes != null)
                {
                    var coll = modes.TryCast<Il2CppSystem.Collections.Generic.ICollection<GameModeConfig>>();
                    int count = coll != null ? coll.Count : 0;
                    for (int i = 0; i < count; i++)
                    {
                        var c = modes[i];
                        if (c == null || c.m_XPMode == null || c.m_SaveSlotType != SaveSlotType.SANDBOX) continue;
                        string name = SafeText(c.Name);
                        Modes.Add(new Mode { Config = c, Type = (int)c.m_XPMode.m_ModeType, Name = name.Length > 0 ? name : c.m_XPMode.m_ModeType.ToString() });
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[новая игра] режимы: " + e.Message);
            }
            try
            {
                foreach (var spec in Resources.FindObjectsOfTypeAll<RegionSpecification>())
                {
                    if (spec == null || Regions.Any(r => r.Key == spec.name)) continue;
                    string name = "";
                    try { name = spec.LocalizedRegionDescription; } catch { }
                    if (string.IsNullOrWhiteSpace(name)) name = spec.name;
                    if (name.Length > 40) name = name.Substring(0, 40) + "…";
                    Regions.Add(new Region { Spec = spec, Key = spec.name, Name = name });
                }
                Regions.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.CurrentCulture));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[новая игра] регионы: " + e.Message);
            }
            if (!_logged && (Modes.Count > 0 || Regions.Count > 0))
            {
                _logged = true;
                MelonLogger.Msg("[новая игра] режимы: " + string.Join(", ", Modes.Select(m => m.Type + "=" + m.Name)));
                MelonLogger.Msg("[новая игра] регионы: " + string.Join(", ", Regions.Select(r => r.Key + "=" + r.Name)));
            }
        }

        // Регион, в котором сейчас находится этот игрок (для хоста: куда звать друга).
        public static string CurrentRegionKey()
        {
            try
            {
                var set = GameManager.m_LastOutdoorSceneSet ?? GameManager.m_ActiveSceneSet;
                string scene = set != null ? set.name : GameManager.m_ActiveScene;
                string key = FindRegionKey(scene);
                // Регион мода (TheLongDevelopment: ModForsakenShore…) не в списке регионов меню: ключ — имя набора сцен,
                // оно одинаково у всех игроков. Имя ассета региона у таких модов то же, так что ключи совпадут,
                // даже если у кого-то регион мода всё же попал в список.
                if (key.Length == 0 && set != null) key = set.name ?? "";
                return key;
            }
            catch
            {
                return "";
            }
        }

        // Имя сцены → регион: имена ассетов регионов содержат имя сцены региона (LakeRegion, CoastalRegion…).
        public static string FindRegionKey(string scene)
        {
            if (string.IsNullOrEmpty(scene)) return "";
            if (Regions.Count == 0) Refresh();
            string s = Normalize(scene);
            foreach (var r in Regions)
            {
                string k = Normalize(r.Key);
                if (k.Length > 0 && (s.Contains(k) || k.Contains(s))) return r.Key;
            }
            return "";
        }

        private static string Normalize(string s)
        {
            s = (s ?? "").ToLowerInvariant();
            foreach (var junk in new[] { "regionspecification", "scenesetregion", "sceneset", "_sandbox", "sandbox", "_", " " })
                s = s.Replace(junk, "");
            return s;
        }

        public static Region PickRandomRegion()
        {
            if (Regions.Count == 0) Refresh();
            var open = Regions.Where(r => { try { return r.Spec.IsUnlocked(); } catch { return true; } }).ToList();
            if (open.Count == 0) open = Regions;
            return open.Count == 0 ? null : open[new System.Random().Next(open.Count)];
        }

        public static Mode FindMode(int type)
        {
            if (Modes.Count == 0) Refresh();
            return Modes.FirstOrDefault(m => m.Type == type);
        }

        // Друг в общем мире должен играть в режиме хоста: от режима зависит, нападают ли звери,
        // как быстро портятся вещи, тратятся силы и так далее. true — если пришлось менять.
        public static bool MatchHostMode(int type)
        {
            if (type < 0 || GameApi.ModeType() == type) return false;
            var mode = FindMode(type);
            var m = GameManager.GetExperienceModeManagerComponent();
            if (mode == null || m == null) return false;
            m.SetGameModeConfig(mode.Config);
            return true;
        }

        public static void Start(Mode mode, Region region, string displayName)
        {
            if (mode == null) throw new InvalidOperationException("Не выбран режим");
            region ??= PickRandomRegion();
            if (region == null) throw new InvalidOperationException("Игра не отдала список регионов");
            MelonLogger.Msg($"[новая игра] {mode.Name} / {region.Key}");
            SaveGameSystem.SetCurrentSaveInfo(Episode.One, SaveSlotType.SANDBOX, SaveGameSlots.GetUnusedGameId(), null);
            GameManager.GetExperienceModeManagerComponent().SetGameModeConfig(mode.Config);
            GameManager.m_StartRegion = region.Spec;
            if (!string.IsNullOrWhiteSpace(displayName))
                SaveGameSlots.SetSlotDisplayName(SaveGameSystem.GetCurrentSaveName(), displayName);
            GameManager.Instance().LaunchSandbox();
        }

        private static string SafeText(LocalizedString s)
        {
            try { return s?.Text() ?? ""; } catch { return ""; }
        }
    }
}
