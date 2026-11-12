using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Что лежит в ещё не открытом контейнере, игра решает сама при первом открытии (PopulateContents) — у каждого
    // игрока по-своему: у хоста в ящике кепка, у друга в том же ящике металлолом. Теперь у друга, если хост в той же
    // локации, игра не раскладывает своё: друг спрашивает хоста, хост раскладывает у себя (если ещё не) и рассылает
    // содержимое обычным состоянием контейнера. Хоста в локации нет — друг раскладывает сам, а при закрытии
    // содержимое уходит хосту как всегда.
    public static class ContainerLoot
    {
        private static readonly Dictionary<string, float> Asked = new();   // id → когда спросили
        private static readonly HashSet<string> Received = new();          // содержимое пришло по сети: своё не раскладываем
        private static int _calls;
        private static int _asks, _local, _answered;

        public static bool WaitingFor(string id) => Asked.ContainsKey(id);

        private static bool HostHere()
        {
            if (Session.Mode != Mode.Client || !Session.InSharedWorld || !ItemSync.Settled) return false;
            return Session.Players.TryGetValue(0, out var h) && h.Scene == GameApi.Scene() && Time.unscaledTime - h.LastState < 5f;
        }

        // Prefix PopulateContents: false — не раскладывать своё.
        internal static bool BeforePopulate(Container c)
        {
            if (c == null) return true;
            try
            {
                string id = WorldObjects.IdOf(c);
                if (_calls++ < 40)
                    MelonLogger.Msg($"[лут] игра раскладывает контейнер {id}: не разложен={c.m_NotPopulated}, обыскан={c.m_Inspected}, хост рядом={HostHere()}, пришёл по сети={Received.Contains(id ?? "")}");
                // «Пришло по сети» относится только к общему миру: в своём сохранении друг раскладывает ящики сам.
                if (Session.Mode == Mode.Off || !Session.InSharedWorld) return true;
                if (!string.IsNullOrEmpty(id) && Received.Contains(id)) return false;
                if (!c.m_NotPopulated || !HostHere()) return true;
                if (string.IsNullOrEmpty(id)) return true;
                if (Asked.ContainsKey(id)) return false;
                Asked[id] = Time.unscaledTime;
                var w = Msg.New(MsgType.ContainerAsk);
                w.Write(ItemSync.SceneKey());
                w.Write(id);
                w.Write(false);
                Session.SendGameEvent(Msg.Done(w));
                _asks++;
                MelonLogger.Msg($"[лут] контейнер {id} ещё не разложен: спросил хоста");
                return false;
            }
            catch (Exception e) { MelonLogger.Warning("[лут] " + e.Message); return true; }
        }

        // Пришло содержимое контейнера (от хоста по нашему вопросу или от того, кто обыскал его первым).
        internal static void Answered(string id, Container c)
        {
            Received.Add(id);
            if (!Asked.Remove(id)) return;
            _answered++;
            // Окно уже открыто (обыск закончился раньше ответа): перерисовываем список.
            try
            {
                if (InterfaceManager.TryGetPanel<Panel_Container>(out var panel) && panel != null && panel.IsEnabled()) panel.RefreshTables();
            }
            catch { }
            MelonLogger.Msg($"[лут] хост прислал содержимое контейнера {id}");
        }

        // Открываем ещё не обысканный контейнер, хост в той же локации: лут мог разложиться у нас ещё при загрузке
        // локации, а у хоста свой. Спрашиваем хоста и кладём его содержимое, пока идёт обыск.
        internal static void OnOpen(Container c)
        {
            // «Занято» не дал открыть (Game/Busy.cs) — ни заявки, ни вопроса хосту: ответ хоста ушёл бы всем
            // и попал бы к тому, кто сейчас обыскивает ящик, как «чужая версия».
            if (!Busy.AllowContainer(c)) return;
            try { WorldObjects.OpenedUninspected = c != null && !c.m_Inspected; } catch { }
            WorldObjects.NoteUncreatedNames(c);   // до обыска: что игра у нас не создаст, допишем в свои состояния ящика
            Claim(c);
            try
            {
                if (c == null || c.m_Inspected || !HostHere()) return;
                string id = WorldObjects.IdOf(c);
                if (string.IsNullOrEmpty(id) || Received.Contains(id) || Asked.ContainsKey(id)) return;
                Asked[id] = Time.unscaledTime;
                var w = Msg.New(MsgType.ContainerAsk);
                w.Write(ItemSync.SceneKey());
                w.Write(id);
                w.Write(false);
                Session.SendGameEvent(Msg.Done(w));
                _asks++;
                MelonLogger.Msg($"[лут] открываю необысканный контейнер {id}: спросил хоста");
            }
            catch (Exception e) { MelonLogger.Warning("[лут] " + e.Message); }
        }

        // Хост: друг спрашивает, что в контейнере.
        public static void HostAsk(string who, BinaryReader r, Action<byte[]> reply)
        {
            string scene = r.ReadString();
            string id = r.ReadString();
            Container c = null;
            if (GameApi.InGame() && ItemSync.Settled && scene == ItemSync.SceneKey()) c = WorldObjects.FindContainer(id);
            if (c == null)
            {
                // У меня этой локации нет (или контейнер не нашёлся): пусть раскладывает сам.
                var w = Msg.New(MsgType.ContainerAsk);
                w.Write(scene);
                w.Write(id);
                w.Write(true);
                reply(Msg.Done(w));
                MelonLogger.Msg($"[лут] {who} спросил про контейнер {id}: у меня его нет, раскладывает сам");
                return;
            }
            // Раскладывать заново не нужно: в 2.59 игра раскладывает ящик ещё при загрузке локации (Container.Start),
            // а m_NotPopulated после этого так и остаётся true. Повторный PopulateContents дописывал тот же список
            // вещей второй раз — в ящике всё было парами. Serialize уже несёт неразложенный список (m_GearToInstantiate).
            MelonLogger.Msg($"[лут] {who} спросил про контейнер {id}: отправляю своё");
            WorldObjects.Send(WorldObjects.Kind.Container, c, c.Serialize());
        }

        // Друг: хост ответил «раскладывай сам».
        public static void ClientSelf(BinaryReader r)
        {
            string scene = r.ReadString();
            string id = r.ReadString();
            if (!r.ReadBoolean() || !Asked.Remove(id)) return;
            PopulateHere(id, scene);
        }

        private static void PopulateHere(string id, string scene)
        {
            if (scene != ItemSync.SceneKey()) return;
            var c = WorldObjects.FindContainer(id);
            if (c == null) return;
            // Свой лут игра уже разложила при загрузке локации (Container.Start) тем же зерном мира, что и у хоста:
            // оставляем его как есть. Второй PopulateContents удвоил бы вещи.
            _local++;
            MelonLogger.Msg($"[лут] контейнер {id}: остаётся свой лут");
        }

        // Хост молчит 4 с — больше не ждём, в ящике остаётся свой лут (разложен игрой при загрузке локации).
        public static void Tick()
        {
            TickDenied();
            if (Asked.Count == 0) return;
            float now = Time.unscaledTime;
            List<string> late = null;
            foreach (var kv in Asked) if (now - kv.Value > 4f) (late ??= new List<string>()).Add(kv.Key);
            if (late == null) return;
            foreach (var id in late)
            {
                Asked.Remove(id);
                MelonLogger.Msg($"[лут] хост не ответил про контейнер {id}");
                PopulateHere(id, ItemSync.SceneKey());
            }
        }

        public static void Reset() { Asked.Clear(); Received.Clear(); Claims.Clear(); Denied.Clear(); }

        // ---------- Спор за ящик ----------
        // «Занято» (Game/Busy.cs) доходит до других с задержкой, и двое, открывшие ящик в одну и ту же долю секунды,
        // могли разобрать его оба — вещи удваивались. Теперь судья хост: кто первым начал обыск, того и ящик,
        // второму хост отвечает «занято», и у него окно ящика закрывается.

        private static readonly Dictionary<string, (int who, float at)> Claims = new();
        private static readonly Dictionary<string, (string holder, float at)> Denied = new();
        private static int _denied;

        private static void Claim(Container c)
        {
            if (c == null || Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            try
            {
                string id = WorldObjects.IdOf(c);
                if (string.IsNullOrEmpty(id)) return;
                string scene = ItemSync.SceneKey();
                if (Session.Mode == Mode.Host)
                {
                    if (!HostCheck(scene, id, 0, out string holder)) Deny(id, holder);
                    return;
                }
                var w = Msg.New(MsgType.ContainerClaim);
                w.Write(scene);
                w.Write(id);
                Session.SendGameEvent(Msg.Done(w));
            }
            catch (Exception e) { MelonLogger.Warning("[ящик] " + e.Message); }
        }

        private static bool HostCheck(string scene, string id, int who, out string holder)
        {
            holder = "";
            string busy = "box:" + id;
            float now = Time.unscaledTime;
            if (who != 0 && GameApi.InGame() && ItemSync.SceneKey() == scene && Busy.Current() == busy) { holder = Session.LocalName; return false; }
            foreach (var p in Session.Players.Values)
                if (p.Id != who && p.Id != Session.LocalId && p.Busy == busy && now - p.LastState < 3f) { holder = p.Name; return false; }
            string key = scene + "|" + id;
            if (Claims.TryGetValue(key, out var c) && c.who != who && now - c.at < 3f)
            {
                holder = c.who == 0 ? Session.LocalName : Session.Players.TryGetValue(c.who, out var pl) ? pl.Name : "другой игрок";
                return false;
            }
            Claims[key] = (who, now);
            return true;
        }

        public static void HostClaim(int who, BinaryReader r, Action<byte[]> reply)
        {
            string scene = r.ReadString();
            string id = r.ReadString();
            if (HostCheck(scene, id, who, out string holder)) return;
            var w = Msg.New(MsgType.ContainerClaim);
            w.Write(scene);
            w.Write(id);
            w.Write(holder);
            reply(Msg.Done(w));
            MelonLogger.Msg($"[ящик] {id}: второй обыск в ту же секунду, ящик у {holder} — отказал");
        }

        public static void ClientDenied(BinaryReader r)
        {
            string scene = r.ReadString();
            string id = r.ReadString();
            string holder = r.ReadString();
            if (scene != ItemSync.SceneKey()) return;
            Deny(id, holder);
        }

        private static void Deny(string id, string holder)
        {
            Denied[id] = (holder, Time.unscaledTime);
            _denied++;
            MelonLogger.Msg($"[ящик] {id} уже обыскивает {holder}: закрываю у себя");
        }

        // Каждый кадр: открытый у нас ящик, на который хост сказал «занято», закрываем.
        private static void TickDenied()
        {
            if (Denied.Count == 0) return;
            float now = Time.unscaledTime;
            var open = WorldObjects.OpenContainer;
            string openId = open != null ? WorldObjects.IdOf(open) : null;
            foreach (var kv in new List<KeyValuePair<string, (string holder, float at)>>(Denied))
            {
                if (now - kv.Value.at > 15f) { Denied.Remove(kv.Key); continue; }
                if (openId != kv.Key) continue;
                // Окно ящика сейчас спрятано окном «сколько штук»: закроем, когда оно вернётся. Раньше пометка «закрыть
                // молча» ставилась и без закрытия, и потом обычное закрытие уходило молча — положенное не доходило до других.
                if (!(InterfaceManager.TryGetPanel<Panel_Container>(out var panel) && panel != null && panel.IsEnabled())) continue;
                Denied.Remove(kv.Key);
                try
                {
                    // Своё (устаревшее) содержимое при этом закрытии не шлём: ящик обыскивает другой.
                    WorldObjects.SilentClose = kv.Key;
                    panel.Enable(false, false, null);
                }
                catch (Exception e) { MelonLogger.Warning("[ящик] закрыть: " + e.Message); }
                GameApi.HudMessage($"Этот ящик уже обыскивает {kv.Value.holder}");
            }
        }

        // Зерно для раскладки контейнера: зерно мира (одно у хоста и у друзей) + сцена + id контейнера.
        internal static int SeedOf(Container c)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return 0;
            try
            {
                int world = GameManager.m_SceneTransitionData?.m_GameRandomSeed ?? 0;
                string id = WorldObjects.IdOf(c);
                if (world == 0 || string.IsNullOrEmpty(id)) return 0;
                uint h = 2166136261;
                foreach (char ch in ItemSync.SceneKey() + "|" + id) { h ^= ch; h *= 16777619; }
                int seed = (int)(h ^ (uint)world);
                return seed == 0 ? 1 : seed;
            }
            catch { return 0; }
        }

        public static string Stats => $"лут контейнеров: спросил {_asks}, получил {_answered}, разложил сам {_local}";
    }

    [HarmonyPatch(typeof(Container), nameof(Container.BeginContainerOpen))]
    internal static class Patch_ContainerOpenAsk
    {
        private static void Prefix(Container __instance) => ContainerLoot.OnOpen(__instance);
    }

    [HarmonyPatch(typeof(Container), nameof(Container.PopulateContents))]
    internal static class Patch_ContainerPopulate
    {
        // Игра 2.59 сама PopulateContents не вызывает (Container.Start зовёт его части напрямую): запрет остаётся
        // на случай чужого вызова. Зерно — в Patch_ContainerRandomGear, внутри PopulateContents он сработает тоже.
        private static bool Prefix(Container __instance) => ContainerLoot.BeforePopulate(__instance);
    }

    // Случайный лут ящика (его зовёт Container.Start при загрузке локации). Игра бросает свои кости от места ящика,
    // а моды лута (FoodPackByEMPEROR — постфикс на этом методе) бросают UnityEngine.Random как есть, и у каждого
    // игрока выпадало своё. Засеваем генератор от зерна мира и самого контейнера: мир у всех один (друг грузит
    // сохранение хоста), поэтому у всех с одинаковыми DLC и модами выпадет одно и то же — в любой локации, даже куда
    // хост ещё не заходил. Наш префикс засевает, постфиксы модов бросают, наш Finalizer (после всех постфиксов)
    // возвращает генератор.
    [HarmonyPatch(typeof(Container), nameof(Container.PopulateWithRandomGear))]
    internal static class Patch_ContainerRandomGear
    {
        private static void Prefix(Container __instance, out UnityEngine.Random.State? __state)
        {
            __state = null;
            int seed = ContainerLoot.SeedOf(__instance);
            if (seed == 0) return;
            try
            {
                __state = UnityEngine.Random.state;
                UnityEngine.Random.InitState(seed);
            }
            catch { }
        }

        // Finalizer, а не Postfix: генератор вернётся на место, даже если раскладка (игра или постфикс мода) упала.
        private static Exception Finalizer(Exception __exception, UnityEngine.Random.State? __state)
        {
            if (__state.HasValue) UnityEngine.Random.state = __state.Value;
            return __exception;
        }
    }
}
