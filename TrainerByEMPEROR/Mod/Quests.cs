using System;
using System.Collections.Generic;
using Il2Cpp;
using Il2CppTLD.Missions;
using MelonLoader;

namespace TrainerByEMPEROR
{
    public sealed class ObjectiveView
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public bool Completed { get; set; }
        public bool Child { get; set; }
    }

    public sealed class MissionView
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Chapter { get; set; }
        public bool Side { get; set; }
        public List<ObjectiveView> Objectives { get; set; } = new();
    }

    // Задания (линейки «Сказаний» в выживании и сюжетные задания): список и завершение.
    internal static class Quests
    {
        public static List<MissionView> List()
        {
            var amm = ActiveMissionManager.Instance;
            if (amm == null) throw new InvalidOperationException("Журнал заданий ещё не загружен");

            var missions = new List<MissionView>();
            var byId = new Dictionary<string, MissionView>();
            var infos = amm.MissionInfoList;
            if (infos != null)
            {
                for (int i = 0; i < infos.Count; i++)
                {
                    var m = infos[i];
                    if (m == null || !m.isActive) continue;
                    var view = new MissionView
                    {
                        Id = m.missionID,
                        Name = Loc(m.nameLocID, m.missionID),
                        Chapter = Loc(m.chapterLocID, ""),
                        Side = m.isSideMission,
                    };
                    missions.Add(view);
                    byId[m.missionID] = view;
                }
            }

            var objectives = amm.ObjectiveInfoList;
            if (objectives != null)
            {
                for (int i = 0; i < objectives.Count; i++)
                {
                    var o = objectives[i];
                    if (o == null || o.invisibleInJournal || o.missionID == null) continue;
                    if (!byId.TryGetValue(o.missionID, out var view)) continue;
                    view.Objectives.Add(new ObjectiveView
                    {
                        Id = o.objectiveID,
                        Name = Loc(o.descriptionLocID, Loc(o.hudDescriptionLocID, o.objectiveID)),
                        Completed = o.completed,
                        Child = o.isChildObjective,
                    });
                }
            }
            return missions;
        }

        public static void CompleteObjective(string objectiveId)
        {
            // Сначала через встроенную команду разработчиков: она продвигает логику задания.
            // Если не сработала, отмечаем задачу выполненной в журнале напрямую.
            RunConsole("mission_objective_complete " + objectiveId);
            var amm = ActiveMissionManager.Instance;
            if (amm != null && !(amm.TryGetObjectiveIsComplete(objectiveId, out bool done) && done))
                amm.MarkObjectiveComplete(objectiveId, false);
        }

        public static void CompleteMission(string missionId)
        {
            RunConsole("mission_complete " + missionId);
            var amm = ActiveMissionManager.Instance;
            var info = amm?.GetMissionInfoFromID(missionId);
            if (amm != null && info != null && info.isActive && !info.completedSuccessfully)
                amm.MarkMissionComplete(missionId, true, false);
        }

        private static bool RunConsole(string command)
        {
            string name = command.Split(' ')[0];
            try
            {
                if (!uConsole.CommandIsRegistered(name)) ConsoleManager.RegisterCommands();
                if (!uConsole.CommandIsRegistered(name)) return false;
                uConsole.RunCommandSilent(command);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"Команда «{command}» не сработала: {e.Message}");
                return false;
            }
        }

        private static string Loc(string key, string fallback)
        {
            if (string.IsNullOrEmpty(key)) return fallback;
            try
            {
                var s = Localization.Get(key);
                return string.IsNullOrWhiteSpace(s) ? fallback : s;
            }
            catch
            {
                return fallback;
            }
        }
    }
}
