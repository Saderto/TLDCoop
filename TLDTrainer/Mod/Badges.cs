using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.SaveState;
using MelonLoader;

namespace TLDTrainer
{
    public sealed class BadgeEntry
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public bool Unlocked { get; set; }
    }

    // Значки испытаний и подвиги (меню «Значки»). Хранятся в профиле игрока, а не в сохранении.
    internal static class Badges
    {
        private static readonly Dictionary<BadgeInfoType, string> Fallback = new()
        {
            [BadgeInfoType.Badge_Rescue] = "Спасение",
            [BadgeInfoType.Badge_HuntedPart1] = "Затравленный, часть 1",
            [BadgeInfoType.Badge_Hunted2] = "Затравленный, часть 2",
            [BadgeInfoType.Badge_Whiteout] = "Белая мгла",
            [BadgeInfoType.Badge_Nomad] = "Кочевник",
            [BadgeInfoType.Badge_Archivist] = "Архивариус",
            [BadgeInfoType.Badge_DeadMan] = "Мертвец",
            [BadgeInfoType.Badge_FourDaysOfNightHalf] = "Четыре дня ночи (половина)",
            [BadgeInfoType.Badge_FourDaysOfNightFull] = "Четыре дня ночи",
            [BadgeInfoType.Badge_FourDaysOfNight2019Half] = "Четыре дня ночи 2019 (половина)",
            [BadgeInfoType.Badge_FourDaysOfNight2019Full] = "Четыре дня ночи 2019",
            [BadgeInfoType.Badge_WintersEmbrace2020CanadianFeast] = "Объятия зимы 2020: канадский пир",
            [BadgeInfoType.Badge_WintersEmbrace2020WinterInJuly] = "Объятия зимы 2020: зима в июле",
            [BadgeInfoType.Badge_NowhereToHide2020Badge1] = "Негде спрятаться 2020, значок 1",
            [BadgeInfoType.Badge_NowhereToHide2020Badge2] = "Негде спрятаться 2020, значок 2",
            [BadgeInfoType.Badge_Tale01] = "Байки из дальних земель 1",
            [BadgeInfoType.Badge_Tale02] = "Байки из дальних земель 2",
            [BadgeInfoType.Badge_Tale03] = "Байки из дальних земель 3",
        };

        // Полученные значки хранит профиль игрока; поле m_Unlocked у описания значка заполняет только окно «Значки».
        private static HashSet<int> ProfileBadges()
        {
            var set = new HashSet<int>();
            try
            {
                var got = ProfileState.Instance?.m_UnlockedBadges;
                for (int i = 0; got != null && i < got.Count; i++) set.Add((int)got[i]);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("Значки: не удалось прочитать профиль: " + e.Message);
            }
            return set;
        }

        public static List<BadgeEntry> List()
        {
            var list = new List<BadgeEntry>();
            var got = ProfileBadges();
            foreach (BadgeInfoType type in Enum.GetValues(typeof(BadgeInfoType)))
            {
                if (type == BadgeInfoType.None) continue;
                BadgeUIInfo info = null;
                try
                {
                    info = BadgeUIInfo.GetBadgeUIInfo(type);
                }
                catch
                {
                    // значок без описания в игре
                }
                string name = Text(info?.m_BadgeName);
                string desc = Text(info?.m_Description);
                list.Add(new BadgeEntry
                {
                    Id = (int)type,
                    Name = string.IsNullOrWhiteSpace(name) ? (Fallback.TryGetValue(type, out var n) ? n : type.ToString()) : name,
                    Description = desc ?? "",
                    Unlocked = got.Contains((int)type) || (info != null && info.m_Unlocked),
                });
            }
            return list;
        }

        public static void Unlock(int id)
        {
            var type = (BadgeInfoType)id;
            if (!Enum.IsDefined(typeof(BadgeInfoType), type) || type == BadgeInfoType.None) throw new ArgumentOutOfRangeException(nameof(id));
            BadgeUIInfo.UnlockBadge(type, false);
            MelonLogger.Msg($"Значок выдан: {type}");
        }

        public static void UnlockAll()
        {
            var types = new List<BadgeInfoType>();
            foreach (BadgeInfoType type in Enum.GetValues(typeof(BadgeInfoType)))
                if (type != BadgeInfoType.None) types.Add(type);
            // Профиль сохраняем один раз, после последнего значка.
            for (int i = 0; i < types.Count; i++)
                BadgeUIInfo.UnlockBadge(types[i], i < types.Count - 1);
            MelonLogger.Msg("Выданы все значки испытаний");
        }

        // ---------- Подвиги (вкладка «Подвиги» в меню «Значки») ----------

        private static List<Feat> Feats()
        {
            var list = new List<Feat>();
            var feats = FeatsManager.m_Feats;
            if (feats == null || feats.Count == 0)
            {
                var mgr = UnityEngine.Object.FindObjectOfType<FeatsManager>();
                if (mgr != null)
                    for (int i = 0; i < mgr.GetNumFeats(); i++)
                    {
                        var f = mgr.GetFeatFromIndex(i);
                        if (f != null) list.Add(f);
                    }
                return list;
            }
            for (int i = 0; i < feats.Count; i++)
                if (feats[i] != null) list.Add(feats[i]);
            return list;
        }

        public static List<BadgeEntry> FeatList()
        {
            var list = new List<BadgeEntry>();
            foreach (var f in Feats())
            {
                string name = null, desc = null;
                bool unlocked = false;
                try
                {
                    name = f.DisplayName;
                }
                catch
                {
                    name = Text(f.m_LocalizedDisplayName);
                }
                try
                {
                    desc = f.GetDescription();
                }
                catch
                {
                    // без описания
                }
                try
                {
                    unlocked = f.IsUnlocked();
                }
                catch
                {
                    // неизвестно
                }
                list.Add(new BadgeEntry
                {
                    Id = (int)f.m_FeatType,
                    Name = string.IsNullOrWhiteSpace(name) ? f.m_FeatType.ToString() : name,
                    Description = desc ?? "",
                    Unlocked = unlocked,
                });
            }
            return list;
        }

        public static void UnlockFeat(int id)
        {
            foreach (var f in Feats())
            {
                if ((int)f.m_FeatType != id) continue;
                Complete(f);
                SaveGameSystem.SaveProfile();
                MelonLogger.Msg($"Подвиг открыт: {f.m_FeatType}");
                return;
            }
            throw new InvalidOperationException("Подвиг не найден — откройте главное меню или загрузите сохранение");
        }

        public static void UnlockAllFeats()
        {
            var feats = Feats();
            if (feats.Count == 0) throw new InvalidOperationException("Подвиги ещё не загружены — откройте главное меню или загрузите сохранение");
            foreach (var f in feats)
                if (!f.IsUnlocked()) Complete(f);
            SaveGameSystem.SaveProfile();
            MelonLogger.Msg("Открыты все подвиги");
        }

        // Прогресс подвига на 100 %: игра сама засчитывает его как полученный.
        private static void Complete(Feat f)
        {
            f.SetNormalizedProgress(1f);
            if (!f.IsUnlocked()) MelonLogger.Warning($"Подвиг {f.m_FeatType}: прогресс выставлен, но игра не считает его открытым");
        }

        private static string Text(LocalizedString s)
        {
            if (s == null) return null;
            try
            {
                return s.Text();
            }
            catch
            {
                return null;
            }
        }
    }
}
