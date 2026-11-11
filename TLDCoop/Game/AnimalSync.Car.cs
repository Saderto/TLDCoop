using System;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Игрок в машине (Game/Cars.cs) для зверей не цель: не нападают, не начинают схватку, не бегут к нему.
    //
    // В самой игре волки машину не проверяют. Поэтому в коопе игрок в машине для зверей «невидим»
    // (PlayerManager.PlayerIsInvisibleToAi — это смотрят TargetCanBeIgnored, ProcessAttack, ProcessStalking,
    // MaybeAttackPlayerWhenTryingToFlee): зверь бросает его, как бросил бы невидимого. Ближайшим его тоже
    // не считаем, если рядом есть кто-то ещё. Удар и схватка по нему отменяются на всякий случай.
    // Работает для любого игрока: свой — по Cars.LocalInside(), чужой — по Cars.InCar(p) и его режиму управления.
    public static partial class AnimalSync
    {
        private static int _carFrame = -1, _carBlocked, _carLogs;
        private static bool _carLocal;

        private static string CarStats => $"не дал тронуть игрока в машине {_carBlocked}";

        internal static bool LocalInCar()
        {
            if (!Active) return false;
            if (_carFrame == Time.frameCount) return _carLocal;
            _carFrame = Time.frameCount;
            _carLocal = false;
            try { _carLocal = Cars.LocalInside(); } catch { }
            if (!_carLocal)
            {
                try { var v = GameManager.GetPlayerInVehicle(); _carLocal = v != null && v.IsInside(); } catch { }
            }
            return _carLocal;
        }

        internal static bool InCar(int id)
        {
            if (id < 0) return false;
            if (id == Session.LocalId) return LocalInCar();
            if (SenseOf(id, out var s) && s.Control == (byte)PlayerControlMode.InVehicle) return true;
            try { return Session.Players.TryGetValue(id, out var pl) && Cars.InCar(pl); } catch { return false; }
        }

        // Не цель для зверей: в машине или (другой игрок) невидим для них.
        private static bool IgnoredByAnimals(int id)
        {
            if (InCar(id)) return true;
            return id != Session.LocalId && SenseOf(id, out var s) && s.Invisible;
        }

        // PlayerManager.PlayerIsInvisibleToAi: во время подмены — за того, про кого думает зверь, иначе — за себя.
        internal static void InvisibleToAi(ref bool result)
        {
            if (Borrowing && BorrowId >= 0)
            {
                if (Borrowed(out var s)) result = s.Invisible;
                if (!result && InCar(BorrowId)) result = true;
                return;
            }
            if (!result && LocalInCar()) result = true;
        }

        // Удар или схватка по игроку в машине — отменяем.
        internal static bool CarBlocks(string what, BaseAi ai)
        {
            if (!Active) return false;
            int who = Borrowing && BorrowId >= 0 ? BorrowId : Session.LocalId;
            if (!InCar(who)) return false;
            _carBlocked++;
            if (_carLogs++ < 20)
            {
                string name = "зверь";
                try { if (ai != null) name = ai.gameObject.name; } catch { }
                MelonLogger.Msg($"[звери] {name} хотел {what} {NameOf(who)}, а тот в машине — не дал");
            }
            return true;
        }
    }
}
