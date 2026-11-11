using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;

namespace TLDTrainer
{
    public sealed class SkillInfo
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Level { get; set; }
        public int MaxLevel { get; set; }
    }

    // Навыки по отдельности: уровень +1 / −1.
    internal static class Skills
    {
        private static IEnumerable<Skill> All()
        {
            var sm = GameManager.GetSkillsManager();
            if (sm == null) throw new InvalidOperationException("Загрузите сохранение");
            for (int i = 0; i < sm.GetNumSkills(); i++)
            {
                var s = sm.GetSkillFromIndex(i);
                if (s != null) yield return s;
            }
        }

        private static int MaxLevel(Skill s)
        {
            var tiers = s.m_TierPoints;
            return tiers != null && tiers.Length > 0 ? tiers.Length : 5;
        }

        public static List<SkillInfo> List()
        {
            var list = new List<SkillInfo>();
            foreach (var s in All())
            {
                string name = null;
                try
                {
                    name = s.m_LocalizedDisplayName?.Text();
                }
                catch
                {
                    // ниже имя по умолчанию
                }
                if (string.IsNullOrWhiteSpace(name)) name = s.m_DisplayName;
                if (string.IsNullOrWhiteSpace(name)) name = s.m_SkillType.ToString();
                list.Add(new SkillInfo
                {
                    Id = (int)s.m_SkillType,
                    Name = name,
                    Level = s.GetCurrentTierNumber() + 1,
                    MaxLevel = MaxLevel(s),
                });
            }
            return list;
        }

        // Сдвигает уровень навыка; очки ставятся ровно на начало нового уровня.
        public static void Change(int id, int delta)
        {
            foreach (var s in All())
            {
                if ((int)s.m_SkillType != id) continue;
                int tier = Math.Clamp(s.GetCurrentTierNumber() + delta, 0, MaxLevel(s) - 1);
                s.SetTier(tier, SkillsManager.PointAssignmentMode.AssignInAnyMode);
                MelonLogger.Msg($"Навык {s.m_SkillType}: уровень {tier + 1}");
                return;
            }
            throw new InvalidOperationException("Навык не найден");
        }
    }
}
