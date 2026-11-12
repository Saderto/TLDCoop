using System;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Звери и долгие действия.
    //
    // Долгое действие в коопе (разделка, крафт, чтение, сон отдельно — Game/ActionTime.cs): у делающего игра ускоряет
    // часы (TimeOfDay.Accelerate), и MoveAgent.Update, пока TimeOfDay.IsTimeLapseActive, умножает зверю скорость
    // навигации, разгон и скорость аниматора. У делающего его звери носились в разы быстрее, а остальные видели их
    // копии так же (игра на троих 11-09: медведь «с невероятной скоростью», копия у друга перескакивала на 13 м
    // ровно во время долгого действия хоста). Общее время за действие не проматывается — значит и звери идут
    // с обычной скоростью: внутри MoveAgent.Update ускорения для них нет. Своё ускорение (круг прогресса,
    // потребности) у игрока остаётся.
    //
    // И сторож: копия, у которой ведущий двигает зверя быстрее 20 м/с (так не бегает никто), — пишем в лог.
    public static partial class AnimalSync
    {
        private static bool _inMoveAgent, _lapseNoted;
        private static int _lapseKept, _lapseActions;
        private static int _fastSnaps, _fastSnapLogs;
        private static int _lapseFrame = -1;

        private static string TimeLapseStats => $"долгих действий без ускорения зверей {_lapseActions} (кадров {_lapseKept}), копий, которые ведущий двигал быстрее 20 м/с, {_fastSnaps}";

        internal static void MoveAgentEnter()
        {
            bool on = ActionTime.Active;
            if (!on && _lapseNoted) _lapseNoted = false;
            _inMoveAgent = on;
        }

        internal static void MoveAgentExit() => _inMoveAgent = false;

        internal static void KeepLapse(ref bool result)
        {
            if (!result || !_inMoveAgent) return;
            result = false;
            int frame = Time.frameCount;
            if (frame != _lapseFrame) { _lapseFrame = frame; _lapseKept++; }
            if (_lapseNoted) return;
            _lapseNoted = true;
            _lapseActions++;
            if (_lapseActions <= 20) MelonLogger.Msg("[звери] долгое действие: у меня звери идут с обычной скоростью (игра ускорила бы их вместе с часами, и остальные видели бы, как они носятся)");
        }

        // Из AddSnap: две точки ведущего подряд.
        private static void NoteSnapSpeed(Animal a, Snap last, Vector3 pos, float gap)
        {
            if (a.Dead || gap < 0.05f || gap > 2f) return;
            float d = Vector3.Distance(last.Pos, pos);
            if (d / gap <= 20f) return;
            _fastSnaps++;
            if (_fastSnapLogs++ < 20)
                MelonLogger.Msg($"[звери] {a.Prefab} у {NameOf(a.Controller)} двигается {d / gap:0} м/с ({d:0.0} м за {gap:0.00} с), режим у него {a.CtrlMode}");
        }
    }

    [HarmonyPatch(typeof(MoveAgent), nameof(MoveAgent.Update))]
    internal static class Patch_AnimalMoveNoLapse
    {
        private static void Prefix() => AnimalSync.MoveAgentEnter();
        // Finalizer, а не Postfix: флаг снимется, даже если Update упал (иначе ускорение пропало бы у всей игры).
        private static Exception Finalizer(Exception __exception) { AnimalSync.MoveAgentExit(); return __exception; }
    }

    [HarmonyPatch(typeof(TimeOfDay), nameof(TimeOfDay.IsTimeLapseActive))]
    internal static class Patch_AnimalLapse
    {
        private static void Postfix(ref bool __result) => AnimalSync.KeepLapse(ref __result);
    }
}
