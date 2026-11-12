using System;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using Il2CppTLD.IntBackedUnit;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Точное количество в котелке на огне (0.19.27). Котелок целиком рассылается только на ступеньках (пол-литра
    // снега или воды, сварилось, сгорело — Game/WorldExtra.cs). Между ними снег тает и вода кипит у каждого сама,
    // и количество чуть расходится. Теперь тот, кто ведёт котелок (последним его менял), раз в 3 с шлёт точное
    // количество снега и воды и сколько он уже на огне; у остальных котелок подправляется на месте, без замены.
    public static class PotAmounts
    {
        private const float Every = 3f;
        private static float _next;
        private static int _sent, _applied;
        private static float _nextLog;

        private static bool Active => Session.Mode != Mode.Off && Session.InSharedWorld && GameApi.InGame();

        public static void Tick(float now)
        {
            if (!Active || now < _next) return;
            _next = now + Every;
            if (!WorldStates.OthersNear) return;
            foreach (var pot in WorldExtra.Scan<CookingPotItem>())
            {
                if (pot == null) continue;
                var gi = pot.GetComponent<GearItem>();
                if (gi == null || !WorldExtra.InWorld(gi)) continue;
                long snow, water;
                try { snow = pot.m_LitersSnowBeingMelted.m_Units; water = pot.m_LitersWaterBeingBoiled.m_Units; } catch { continue; }
                bool food = pot.m_GearItemBeingCooked != null;
                if (snow <= 0 && water <= 0 && !food) continue;            // пустой — нечего выравнивать
                if (pot.m_FireBeingUsed == null && snow <= 0) continue;     // не на огне — ничего не меняется
                int owner = ItemSync.OwnerOf(gi);
                bool lead = owner == Session.LocalId || (owner < 0 && Session.Mode == Mode.Host);
                if (!lead) continue;
                PotFire.IAmLead(pot);
                var w = Msg.New(MsgType.PotAmounts);
                w.Write(ItemSync.SceneKey());
                w.Write(ItemSync.IdFor(gi));
                w.Write(ItemSync.PrefabName(gi));
                var p = gi.transform.position;
                w.Write(p.x); w.Write(p.y); w.Write(p.z);
                w.Write((byte)pot.m_CookingState);
                w.Write(snow);
                w.Write(water);
                w.Write(pot.m_CookingElapsedHours);
                w.Write(pot.m_GracePeriodElapsedHours);
                w.Write(pot.AttachedFireIsBurning());   // 0.19.52: горит ли огонь под ним у меня (Game/PotFire.cs)
                w.Write((byte)Session.LocalId);         // 0.19.57: кто ведёт (клиент иначе не знает, от кого отчёт)
                Session.SendFast(Msg.Done(w));
                _sent++;
            }
            if (now >= _nextLog && (_sent > 0 || _applied > 0))
            {
                _nextLog = now + 300f;
                MelonLogger.Msg($"[котелок] точное количество: отправил {_sent}, подправил {_applied}; {PotFire.Stats}");
            }
        }

        // Стадия у копии не та, что у ведущего (у одного готово, у другого ещё сырое): раньше молча ждали замену
        // котелка целиком. Теперь пишем, если расхождение держится дольше 5 с (замена обычно приходит за секунду).
        private static readonly Dictionary<IntPtr, float> StateDiffSince = new();
        private static readonly HashSet<IntPtr> TwoLeadsLogged = new();
        private static int _adopted;
        private static int _stateLogged;

        private static void NoteState(CookingPotItem pot, GearItem gi, int lead)
        {
            float now = Time.unscaledTime;
            if (StateDiffSince.Count > 100) StateDiffSince.Clear();
            if (!StateDiffSince.TryGetValue(pot.Pointer, out float since)) { StateDiffSince[pot.Pointer] = now; return; }
            if (since < 0f || now - since < 5f || _stateLogged >= 30) return;
            StateDiffSince[pot.Pointer] = -1f;
            _stateLogged++;
            string food = "";
            try { var c = pot.m_GearItemBeingCooked; if (c != null) food = " (" + ItemSync.PrefabName(c) + ")"; } catch { }
            MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}{food}: у ведущего стадия {(CookingPotItem.CookingState)lead}, у меня {pot.m_CookingState} уже {now - since:0} с; огонь у меня {(PotFire.OwnBurning(pot) ? "горит" : "не горит")}, готово {pot.m_PercentCooked:0}%");
        }

        private static string Who(int id) => Session.Players.TryGetValue(id, out var pl) ? pl.Name : "игрок " + id;

        public static void Receive(BinaryReader r)
        {
            string scene = r.ReadString();
            uint id = r.ReadUInt32();
            string prefab = r.ReadString();
            var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
            int state = r.ReadByte();
            long snow = r.ReadInt64(), water = r.ReadInt64();
            float elapsed = r.ReadSingle(), grace = r.ReadSingle();
            bool burning = r.BaseStream.Position < r.BaseStream.Length ? r.ReadBoolean() : grace <= 0f;
            int from = r.BaseStream.Position < r.BaseStream.Length ? r.ReadByte() : -1;
            if (!Active || scene != ItemSync.SceneKey() || ItemSync.Applying) return;
            try
            {
                var gi = ItemSync.FindShared(id, prefab, pos);
                var pot = gi != null ? gi.GetComponent<CookingPotItem>() : null;
                if (pot == null || !WorldExtra.InWorld(gi)) return;
                // 0.19.57. Двое ведущих одного котелка (друзья 11-10, банка с водой): друг поставил банку и вёл её, а хост
                // вернулся в локацию, банка у него загрузилась из сохранения без записи, чья она, и хост по правилу
                // «ничья — веду я» тоже стал слать свой отчёт. Каждый 3 с переписывал другому воду, время и отсчёт без огня,
                // у хоста вода вскипела, у друга ещё 1%. Теперь: у меня ведущий неизвестен, а отчёт прислал другой —
                // ведёт он. Если же оба считают котелок своим, пишем об этом в лог (один раз на котелок).
                if (from >= 0 && from != Session.LocalId)
                {
                    if (!ItemSync.HasSender(gi) && ItemSync.OwnerOf(gi) != from)
                    {
                        int was = ItemSync.OwnerOf(gi);
                        ItemSync.AdoptSender(gi, from);
                        if (_adopted++ < 20)
                            MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}: ведёт его {Who(from)} (у меня ведущий был {(was < 0 ? "неизвестен" : Who(was))}) — больше свой отчёт о нём не шлю");
                    }
                    else if (ItemSync.OwnerOf(gi) == Session.LocalId && TwoLeadsLogged.Add(pot.Pointer) && TwoLeadsLogged.Count < 50)
                        MelonLogger.Msg($"[котелок] {ItemSync.PrefabName(gi)}: его ведём оба — я и {Who(from)}");
                }
                PotFire.NoteLead(pot, burning);
                // Я сейчас у этого котелка (окно готовки, осмотр) — не трогаем: перемену разошлю я сам.
                if (WorldExtra.PotInUseByMe(gi, out _)) return;
                // Другая ступень (у нас уже сварилось / ещё нет) — это поправит замена котелка целиком, не мы.
                if ((int)pot.m_CookingState != state) { NoteState(pot, gi, state); return; }
                StateDiffSince.Remove(pot.Pointer);
                bool changed = false;
                long liter = ItemLiquidVolume.Liter.m_Units;
                long tol = Math.Max(1L, liter / 100);   // 10 мл
                var s = pot.m_LitersSnowBeingMelted;
                if (Math.Abs(s.m_Units - snow) > tol) { s.m_Units = snow; pot.m_LitersSnowBeingMelted = s; changed = true; }
                var wv = pot.m_LitersWaterBeingBoiled;
                if (Math.Abs(wv.m_Units - water) > tol) { wv.m_Units = water; pot.m_LitersWaterBeingBoiled = wv; changed = true; }
                if (Math.Abs(pot.m_CookingElapsedHours - elapsed) > 0.01f) { pot.m_CookingElapsedHours = elapsed; changed = true; }
                if (Math.Abs(pot.m_GracePeriodElapsedHours - grace) > 0.01f) { pot.m_GracePeriodElapsedHours = grace; changed = true; }
                if (changed) _applied++;
            }
            catch (Exception e) { MelonLogger.Warning("[котелок] точное количество (приём): " + e.Message); }
        }
    }
}
