using System;
using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using Il2Cpp;
using MelonLoader;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Какие места появления зверей включены — у всех как у хозяина животных локации.
    //
    // В игре включено ли место решает сама игра каждого игрока: бросок m_ChanceActive при загрузке (SpawnRegion.Start),
    // переброс раз в m_HoursReRollActive часов (MaybeReRollActive), случайные группы мест (RandomSpawnObject, напр.
    // Wolf_Packs/trackspack3), включение мест рядом (SpawnRegionManager.MaybeEnableSpawnRegionsInRange), сияние.
    // Раньше у друга выпадало своё: «наш» волк висел у него под выключенным местом, а проверка пумы
    // (BaseAiManager.MaybeSetAiToFleeOrDespawn) прогоняла одолженного зверя.
    //
    // Теперь хозяин локации рассылает каждое место: его состояние (Serialize — там и «включено», и убитые, и время
    // появления) плюс включены ли объекты над ним. Новичку в локации — все сразу, остальным — что изменилось, уходя —
    // всё; друг-хозяин ещё и всё целиком хосту через 10 секунд и раз в 10 минут (на случай вылета). Кто не хозяин,
    // сам места не перебрасывает, ставит как пришло и возвращает, если его игра переключила место сама (пума, сияние).
    // Кто не в этой локации, откладывает (WorldObjects), хост хранит отложенное с миром — пришедший позже возьмёт
    // последнее ещё при загрузке, до первого зверя, а время с тех пор игра досчитает сама, как в одиночной.
    public static partial class AnimalSync
    {
        private const char TreeMark = '\u001F';   // после него в состоянии места: включены ли объекты над ним
        private const float RegionCheckEvery = 2f;
        private const float RegionFullEvery = 600f;   // хозяин не-хост: всё целиком хосту на случай вылета

        private static readonly Dictionary<string, string> SentRegionSig = new();
        private static readonly Dictionary<string, string> PendingTree = new();   // взято при загрузке, ставим после
        private static readonly HashSet<int> RegionAudience = new();
        private static readonly Dictionary<IntPtr, bool> HoldsRegionsCache = new();
        private static readonly Dictionary<string, (SpawnRegion sr, bool on)> WantedRegion = new();   // как у хозяина
        private static HashSet<IntPtr> _randomChildren;
        private static List<SpawnRegion> _regionList;
        private static float _nextRegionCheck, _nextRegionFull, _regionListAt;
        private static bool _regionWasAuthority;
        private static float _regionsLeftAt = -10f;
        private static int _regionsOut, _regionsIn, _regionFlips, _groupFlips, _treeMismatch, _rollsBlocked, _regionKept, _syncLogs;
        internal static bool RegionLive;

        // Новая локация. PendingTree не трогаем: его заполняет загрузка (BeforeRegionDeserialize) раньше, чем Tick
        // замечает смену локации, а разбирает ApplyLateRegions.
        private static void ResetRegionSync()
        {
            SentRegionSig.Clear();
            RegionAudience.Clear();
            HoldsRegionsCache.Clear();
            WantedRegion.Clear();
            _randomChildren = null;
            _regionList = null;
            _regionWasAuthority = false;
            _nextRegionCheck = 0f;
        }

        private static List<SpawnRegion> Regions()
        {
            float now = Time.unscaledTime;
            if (_regionList != null && now - _regionListAt < 30f) return _regionList;
            _regionListAt = now;
            _regionList = new List<SpawnRegion>();
            foreach (var sr in UnityEngine.Object.FindObjectsOfType<SpawnRegion>(true)) if (sr != null) _regionList.Add(sr);
            return _regionList;
        }

        // ---------- Хозяин локации: рассылка ----------

        private static void TickRegionSync(float now, string scene)
        {
            if (now < _nextRegionCheck) return;
            _nextRegionCheck = now + RegionCheckEvery;
            if (!IsAuthority)
            {
                if (_regionWasAuthority) { _regionWasAuthority = false; RegionAudience.Clear(); }
                KeepRegions();
                return;
            }
            bool first = !_regionWasAuthority;
            if (first)
            {
                _regionWasAuthority = true;
                WantedRegion.Clear();
                // Хост хранит места в своём сохранении; друг-хозяин отдаёт хосту всё, когда игра уже бросила своё.
                _nextRegionFull = Session.Mode == Mode.Client ? now + 10f : float.MaxValue;
            }
            var here = PlayersHere(scene, now, false);
            bool newcomer = false;
            var ids = new HashSet<int>();
            foreach (var p in here) { ids.Add(p.id); if (RegionAudience.Add(p.id)) newcomer = true; }
            RegionAudience.RemoveWhere(id => !ids.Contains(id));
            bool full = newcomer;
            if (now >= _nextRegionFull) { _nextRegionFull = now + RegionFullEvery; full = true; }
            // Стали хозяином, тут никого: только запомнить, что есть, — разошлём, что изменится, и всё, уходя.
            if (first && !full) { SendRegionsNow(false, "", true); return; }
            SendRegionsNow(full, newcomer ? "пришёл игрок" : full ? "хосту на хранение" : "изменилось");
        }

        // Всё целиком (уходим, пришёл новый игрок) или только изменившиеся места. dry — только запомнить.
        private static void SendRegionsNow(bool full, string why, bool dry = false)
        {
            if (Session.Mode == Mode.Off || !Session.InSharedWorld) return;
            int sent = 0, on = 0, total = 0;
            foreach (var sr in Regions())
            {
                if (sr == null) continue;
                try
                {
                    total++;
                    if (sr.gameObject.activeInHierarchy) on++;
                    string id = WorldObjects.IdOf(sr);
                    string sig = RegionSig(sr);
                    if (dry) { SentRegionSig[id] = sig; continue; }
                    if (!full && SentRegionSig.TryGetValue(id, out var old) && old == sig) continue;
                    string json = sr.Serialize();
                    if (string.IsNullOrEmpty(json)) continue;
                    SentRegionSig[id] = sig;
                    var raw = WorldObjects.Build(WorldObjects.Kind.Region, sr, json + TreeMark + TreeOf(sr));
                    Session.SendGameEvent(raw);
                    sent++;
                }
                catch (Exception e) { if (_syncLogs++ < 20) MelonLogger.Warning("[звери] место появления: " + e.Message); }
            }
            _regionsOut += sent;
            if (dry) MelonLogger.Msg($"[звери] стал хозяином зверей локации: мест появления {total}, включено {on}");
            else if (sent > 0 && (full || _syncLogs++ < 60))
                MelonLogger.Msg($"[звери] места появления → другим ({why}): {sent}; включено у меня {on} из {total}");
        }

        // Что важно другим: включено ли место и объекты над ним, сколько убито и когда появятся новые.
        // Без часов игры, мест и числа зверей рядом — те меняются всё время, а сообщения о мире копятся в журнале хоста.
        // Где звери стоят, уходит со всем целиком (новичку, уходя).
        private static string RegionSig(SpawnRegion sr)
        {
            var sb = new StringBuilder();
            sb.Append(sr.gameObject.activeSelf ? '1' : '0').Append(TreeOf(sr)).Append('|');
            sb.Append(sr.m_NumRespawnsPending).Append('|').Append(Mathf.Round(sr.m_ElapasedHoursNextRespawnAllowed * 10f)).Append('|');
            sb.Append(Mathf.Round(sr.m_ElapsedHoursAtLastActiveReRoll * 10f)).Append('|').Append(sr.m_NumTrapped).Append('|');
            sb.Append(sr.m_SpawnGuidCounter).Append('|').Append(Mathf.Round(sr.m_CooldownTimerHours * 10f)).Append('|');
            sb.Append(sr.m_HasBeenDisabledByAurora ? '1' : '0').Append(sr.m_WasActiveBeforeAurora ? '1' : '0').Append('|');
            sb.Append((int)sr.m_WildlifeMode);
            return sb.ToString();
        }

        // Включены ли объекты над местом: от родителя к корню, '1' / '0'.
        private static string TreeOf(SpawnRegion sr)
        {
            var sb = new StringBuilder();
            for (var t = sr.transform.parent; t != null; t = t.parent) sb.Append(t.gameObject.activeSelf ? '1' : '0');
            return sb.ToString();
        }

        internal static string SplitRegion(string text, out string tree)
        {
            int i = text == null ? -1 : text.IndexOf(TreeMark);
            if (i < 0) { tree = null; return text; }
            tree = text.Substring(i + 1);
            return text.Substring(0, i);
        }

        // ---------- Остальные: приём ----------

        // Состояние места от хозяина локации, пока мы в ней (WorldObjects, Kind.Region).
        internal static void ApplyRegionLive(SpawnRegion sr, string state)
        {
            if (!AcceptRegion(sr)) return;
            string json = SplitRegion(state, out var tree);
            bool was = sr.gameObject.activeInHierarchy;
            if (!string.IsNullOrEmpty(json))
            {
                RegionLive = true;
                try { sr.Deserialize(json); }
                finally { RegionLive = false; }
                // Один тут (сейчас станем хозяином) — сохранённые звери появятся, как в одиночной.
                if (!MaySpawn) ForgetSavedSpawns(sr);
            }
            int groups = ApplyTree(sr, tree);
            _regionsIn++;
            try { WantedRegion[WorldObjects.IdOf(sr)] = (sr, sr.gameObject.activeSelf); } catch { }
            bool now = sr.gameObject.activeInHierarchy;
            if (was != now)
            {
                _regionFlips++;
                if (_syncLogs++ < 60)
                    MelonLogger.Msg($"[звери] место появления {sr.gameObject.name}: как у хозяина — {(now ? "включил" : "выключил")}{(groups > 0 ? $" (групп над ним: {groups})" : "")}");
            }
        }

        // Своя игра переключила место, которое хозяин прислал (пума, сияние, что-то ещё) — возвращаем как у хозяина.
        private static void KeepRegions()
        {
            if (WantedRegion.Count == 0 || !OthersRollRegions()) return;
            foreach (var w in WantedRegion.Values)
            {
                if (w.sr == null) continue;
                try
                {
                    var go = w.sr.gameObject;
                    if (go.activeSelf == w.on) continue;
                    go.SetActive(w.on);
                    _regionKept++;
                    if (_syncLogs++ < 60) MelonLogger.Msg($"[звери] место появления {go.name}: моя игра {(w.on ? "выключила" : "включила")} его сама — вернул как у хозяина");
                }
                catch { }
            }
        }

        // Не хозяин зверей не создаёт: сохранённые в состоянии звери у него остались бы в очереди игры
        // и вылезли бы двойниками, когда он станет хозяином (его звери к тому времени — копии хозяина).
        private static void ForgetSavedSpawns(SpawnRegion sr)
        {
            try
            {
                sr.m_DeferredSpawnsWithSavedPosition?.Clear();
                sr.m_DeferredSpawnsWithRandomPosition?.Clear();
                sr.m_PendingSerializedRespawnInfoQueue?.Clear();
            }
            catch { }
        }

        // Объекты над местом включаем и выключаем как у хозяина — только те, что переключает RandomSpawnObject.
        private static int ApplyTree(SpawnRegion sr, string tree)
        {
            if (string.IsNullOrEmpty(tree) || sr == null) return 0;
            var up = new List<Transform>();
            for (var t = sr.transform.parent; t != null; t = t.parent) up.Add(t);
            if (up.Count != tree.Length)
            {
                if (_treeMismatch++ < 10) MelonLogger.Msg($"[звери] место появления {sr.gameObject.name}: над ним у хозяина {tree.Length} объектов, у меня {up.Count} — группы не трогаю");
                return 0;
            }
            var random = RandomChildren();
            int flips = 0;
            for (int i = 0; i < up.Count; i++)
            {
                bool want = tree[i] == '1';
                var go = up[i].gameObject;
                if (go.activeSelf == want || !random.Contains(go.Pointer)) continue;
                go.SetActive(want);
                flips++;
                _groupFlips++;
                if (_syncLogs++ < 60) MelonLogger.Msg($"[звери] группа мест появления {go.name}: как у хозяина — {(want ? "включил" : "выключил")}");
            }
            return flips;
        }

        // Всё, что переключают RandomSpawnObject этой локации.
        private static HashSet<IntPtr> RandomChildren()
        {
            if (_randomChildren != null) return _randomChildren;
            _randomChildren = new HashSet<IntPtr>();
            try
            {
                foreach (var rso in UnityEngine.Object.FindObjectsOfType<RandomSpawnObject>(true))
                {
                    var list = rso?.m_ObjectList;
                    if (list == null) continue;
                    foreach (var go in list) if (go != null) _randomChildren.Add(go.Pointer);
                }
            }
            catch { }
            return _randomChildren;
        }

        // Состояния, взятые при загрузке локации (BeforeRegionDeserialize): группы над местами ставим после загрузки,
        // когда RandomSpawnObject уже выбрали своё.
        internal static void ApplyPendingTrees()
        {
            if (PendingTree.Count == 0) return;
            _randomChildren = null;   // локация могла смениться раньше, чем это заметил Tick
            int groups = 0, n = 0;
            foreach (var kv in PendingTree)
            {
                var sr = WorldObjects.Find<SpawnRegion>(kv.Key);
                if (sr == null) continue;
                try { groups += ApplyTree(sr, kv.Value); n++; } catch { }
            }
            PendingTree.Clear();
            MelonLogger.Msg($"[звери] места появления из отложенного: проверил группы над {n}, переключил {groups}");
        }

        // ---------- Кто не хозяин — сам места не перебрасывает ----------

        // Хозяин локации известен и это не мы.
        internal static bool OthersRollRegions() => Active && _authority >= 0 && !IsAuthority && GameApi.Scene() == _scene;

        internal static bool HoldsRegions(RandomSpawnObject rso)
        {
            if (rso == null) return false;
            if (HoldsRegionsCache.TryGetValue(rso.Pointer, out bool yes)) return yes;
            yes = false;
            try
            {
                var list = rso.m_ObjectList;
                if (list != null) foreach (var go in list) if (go != null && go.GetComponentInChildren<SpawnRegion>(true) != null) { yes = true; break; }
            }
            catch { }
            HoldsRegionsCache[rso.Pointer] = yes;
            return yes;
        }

        internal static bool BlockRoll()
        {
            if (!OthersRollRegions()) return false;
            _rollsBlocked++;
            return true;
        }

        private static string RegionSyncStats => $"мест появления разослал {_regionsOut}, принял {_regionsIn}, переключил мест {_regionFlips}, групп {_groupFlips}, сам не перебросил {_rollsBlocked}, вернул после своей игры {_regionKept}";
    }

    [HarmonyPatch(typeof(SpawnRegion), nameof(SpawnRegion.MaybeReRollActive))]
    internal static class Patch_RegionReRoll
    {
        private static bool Prefix() => !AnimalSync.BlockRoll();
    }

    [HarmonyPatch(typeof(RandomSpawnObject), nameof(RandomSpawnObject.MaybeReroll))]
    internal static class Patch_RegionGroupReRoll
    {
        private static bool Prefix(RandomSpawnObject __instance) => !(AnimalSync.HoldsRegions(__instance) && AnimalSync.BlockRoll());
    }

    [HarmonyPatch(typeof(SpawnRegionManager), nameof(SpawnRegionManager.MaybeEnableSpawnRegionsInRange))]
    internal static class Patch_RegionsInRange
    {
        private static bool Prefix() => !AnimalSync.BlockRoll();
    }
}
