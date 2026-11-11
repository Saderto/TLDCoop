using System;
using System.Collections.Generic;
using Il2Cpp;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Туша, лежащая на карте сама по себе, а не зверь мода: например олень, которого игра положила на локацию
    // заранее (часто обглоданный, пара кило мяса), или туша из сохранения, которой нет в списке зверей игры.
    // Её нет в учёте зверей, и разделку такой туши мод не рассылал: друг срезал с оленя 0,5 кг мяса и шкуру, а у
    // хоста осталось 1,1 кг и шкура (в логе друга о туше ни строки). Теперь после каждой разделки состояние такой
    // туши уходит остальным как состояние объекта (WorldObjects, вид «туша», номер «bh:» + номер объекта на карте):
    // кто в локации — применяет сразу, кто нет — когда придёт туда (как ящики и двери). Звери мода и туши-вещи
    // (заяц, четверть) сюда не попадают: их рассылают HarvestTick и SendCarcassItem.
    public static partial class AnimalSync
    {
        private const string MapCarcassMark = "bh:";
        private static readonly Dictionary<string, (string scene, string state)> MapCarcassWaiting = new();
        private static int _mapCarcassSent, _mapCarcassApplied, _mapCarcassMissed, _mapCarcassLogs;

        private static Animal AnimalOf(BodyHarvest bh)
        {
            if (bh == null) return null;
            var p = bh.gameObject.Pointer;
            foreach (var a in Animals.Values)
                if (a.Go != null && a.Go.Pointer == p) return a;
            return null;
        }

        private static bool IsMapCarcass(BodyHarvest bh) => bh != null && bh.GetComponent<GearItem>() == null && AnimalOf(bh) == null;

        // Postfix Panel_BodyHarvest.HarvestSuccessful: срезали с туши на карте — её состояние всем (уже без срезанного).
        internal static void OnMapCarcassHarvested(Panel_BodyHarvest panel)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld || panel == null) return;
            try
            {
                var bh = panel.m_BodyHarvest;
                if (!IsMapCarcass(bh)) return;
                string state = bh.Serialize();
                if (string.IsNullOrEmpty(state)) return;
                WorldObjects.Send(WorldObjects.Kind.Carcass, null, state, MapCarcassMark + WorldObjects.IdOf(bh));
                _mapCarcassSent++;
                if (_mapCarcassLogs++ < 30)
                    MelonLogger.Msg($"[звери] туша на карте {bh.gameObject.name} разделана, разослал: мяса {MeatKg(state)}{(bh.NoMoreResources() ? ", больше ничего нет" : "")}");
            }
            catch (Exception e) { if (_mapCarcassLogs++ < 30) MelonLogger.Warning("[звери] туша на карте: " + e.Message); }
        }

        // Из ApplyCarcass: состояние туши на карте от другого игрока. true — это было оно.
        private static bool ApplyMapCarcass(string id, string state)
        {
            if (!id.StartsWith(MapCarcassMark, StringComparison.Ordinal)) return false;
            if (string.IsNullOrEmpty(state)) return true;
            string objId = id.Substring(MapCarcassMark.Length);
            var bh = WorldObjects.Find<BodyHarvest>(objId);
            if (bh == null)
            {
                _mapCarcassMissed++;
                if (_mapCarcassLogs++ < 30) MelonLogger.Msg($"[звери] разделка туши на карте от другого игрока: у себя такой туши не нашёл ({objId})");
                return true;
            }
            // Мы сами сейчас разделываем эту тушу (так не должно быть: туша «занята» у другого) — после окна разделки.
            if (HarvestPanelOn(bh)) { MapCarcassWaiting[objId] = (ItemSync.SceneKey(), state); return true; }
            ApplyMapCarcassTo(bh, state);
            return true;
        }

        private static bool HarvestPanelOn(BodyHarvest bh)
        {
            try
            {
                return InterfaceManager.TryGetPanel<Panel_BodyHarvest>(out var p) && p != null && p.IsEnabled()
                    && p.m_BodyHarvest != null && p.m_BodyHarvest.Pointer == bh.Pointer;
            }
            catch { return false; }
        }

        private static void ApplyMapCarcassTo(BodyHarvest bh, string state)
        {
            try
            {
                var a = AnimalOf(bh);
                if (a != null) { ApplyHarvest(a, bh, state); return; }   // у нас это зверь мода — как состояние его туши
                if (bh.GetComponent<GearItem>() != null) return;
                string before = "?";
                try { before = MeatKg(bh.Serialize()); } catch { }
                string name = bh.gameObject.name;
                bh.Deserialize(state, false);   // ресурсов не осталось — игра сама уберёт тушу, как у разделавшего
                _mapCarcassApplied++;
                if (_mapCarcassLogs++ < 30) MelonLogger.Msg($"[звери] туша на карте {name}: разделка от другого игрока, мяса было {before}, стало {MeatKg(state)}");
            }
            catch (Exception e) { if (_mapCarcassLogs++ < 30) MelonLogger.Warning("[звери] туша на карте от другого: " + e.Message); }
        }

        // Из HarvestTick: пришедшее, пока у нас было открыто окно разделки этой туши.
        private static void ApplyWaitingMapCarcasses()
        {
            if (MapCarcassWaiting.Count == 0) return;
            string scene = ItemSync.SceneKey();
            foreach (var objId in new List<string>(MapCarcassWaiting.Keys))
            {
                var (sc, state) = MapCarcassWaiting[objId];
                if (sc != scene) { MapCarcassWaiting.Remove(objId); continue; }
                var bh = WorldObjects.Find<BodyHarvest>(objId);
                if (bh == null) { MapCarcassWaiting.Remove(objId); continue; }
                if (HarvestPanelOn(bh)) continue;
                MapCarcassWaiting.Remove(objId);
                ApplyMapCarcassTo(bh, state);
            }
        }

        // Мясо в состоянии туши записано целым числом (ItemWeight: миллиардные доли килограмма) — для лога в кг.
        private static string MeatKg(string state)
        {
            float v = MeatOf(state);
            if (float.IsNaN(v)) return "? кг";
            if (Math.Abs(v) > 1000f) v /= 1e9f;
            return $"{v:0.##} кг";
        }

        private static string MapCarcassStats => $"туш на карте: разослал разделку {_mapCarcassSent}, применил {_mapCarcassApplied}, не нашёл {_mapCarcassMissed}";
    }
}
