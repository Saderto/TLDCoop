using System;
using System.Collections.Generic;
using HarmonyLib;
using Il2Cpp;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Машины и грузовики. Игра сажает игрока на место за дверью (VehicleDoor): камера встаёт в m_CameraDummy
    // этой двери, а сам игрок дальше «внутри» (PlayerInVehicle.m_InVehicle). Раньше кооп про это ничего не знал:
    // фигура друга стояла сквозь крышу, на одно место садились оба, а звери нападали как на открытом месте.
    //
    // Теперь:
    //   • своё место уходит с позицией как «занято» (car:<дверь>), другой на это место не сядет (Busy);
    //   • фигура того, кто в машине, сидит в кресле, голова там, где у него камера, вещь в руках не видна;
    //   • звери его не трогают (AnimalSync спрашивает Cars.InCar);
    //   • за ним не остаются следы, пока он внутри.
    // Место — это дверь: у каждой двери своя камера, поэтому водитель и пассажир сидят на разных местах.
    public static class Cars
    {
        public const string Prefix = "car:";

        // У дверей одной машины GUID часто общий (он у кузова), поэтому место узнаём по имени и месту двери.
        public static string DoorKey(VehicleDoor d) => d == null ? "" : Prefix + WorldObjects.PlaceId(d);

        public static PlayerInVehicle Piv()
        {
            try { return GameManager.GetPlayerInVehicle(); } catch { return null; }
        }

        // Мы сейчас в машине (или садимся).
        public static bool LocalInside()
        {
            var p = Piv();
            try { return p != null && (p.m_InVehicle || p.m_EnteringVehicle); } catch { return false; }
        }

        // Ключ нашего места («car:…») или пусто.
        public static string OwnKey()
        {
            var p = Piv();
            if (p == null) return "";
            try
            {
                if (!(p.m_InVehicle || p.m_EnteringVehicle)) return "";
                var d = p.m_VehicleDoorUsed;
                return d != null ? DoorKey(d) : "";
            }
            catch { return ""; }
        }

        public static bool InCar(Player p) => p != null && IsCarKey(p.Busy);
        public static bool IsCarKey(string busy) => !string.IsNullOrEmpty(busy) && busy.StartsWith(Prefix, StringComparison.Ordinal);

        // ---------- Где сидит другой игрок ----------

        private static readonly Dictionary<string, VehicleDoor> Doors = new();
        private static string _doorsScene = "";
        private static float _nextRescan;

        private static VehicleDoor FindDoor(string key)
        {
            string scene = GameApi.Scene();
            if (scene != _doorsScene) { Doors.Clear(); _doorsScene = scene; _nextRescan = 0f; }
            if (Doors.TryGetValue(key, out var d) && d != null) return d;
            float now = Time.unscaledTime;
            if (now < _nextRescan) return null;
            _nextRescan = now + 2f;
            Doors.Clear();
            foreach (var x in UnityEngine.Object.FindObjectsOfType<VehicleDoor>())
                if (x != null) Doors[DoorKey(x)] = x;
            return Doors.TryGetValue(key, out d) ? d : null;
        }

        // Глаза сидящего (камера двери) и куда смотрит место. false — такой двери у нас нет.
        public static bool Seat(string busy, out Vector3 eye, out float yaw)
        {
            eye = default;
            yaw = 0f;
            if (!IsCarKey(busy)) return false;
            try
            {
                var d = FindDoor(busy);
                if (d == null) return false;
                var cam = d.m_CameraDummy != null ? d.m_CameraDummy.transform : null;
                if (cam == null) return false;
                eye = cam.position;
                var f = cam.forward;
                f.y = 0f;
                if (f.sqrMagnitude < 0.01f)
                {
                    // Камера смотрит отвесно — берём «вперёд» самой машины (родителя двери).
                    var body = d.transform.parent != null ? d.transform.parent : d.transform;
                    f = body.forward;
                    f.y = 0f;
                }
                yaw = f.sqrMagnitude > 0.0001f ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : cam.eulerAngles.y;
                return true;
            }
            catch { return false; }
        }

        // Пока мы в машине, Busy ставит этот ключ первым (он важнее открытого багажника и прочего).
        // Сесть на место, где уже сидит другой, нельзя; выйти — всегда можно.
        public static bool AllowDoor(VehicleDoor d)
        {
            if (d == null || LocalInside()) return true;
            return Busy.AllowKey(DoorKey(d));
        }
    }

    [HarmonyPatch(typeof(VehicleDoor), nameof(VehicleDoor.PerformInteraction))]
    internal static class Patch_CarDoor
    {
        private static bool Prefix(VehicleDoor __instance, ref bool __result)
        {
            bool ok;
            try { ok = Cars.AllowDoor(__instance); } catch { ok = true; }
            if (!ok) __result = false;
            return ok;
        }
    }
}
