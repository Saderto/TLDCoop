using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Il2Cpp;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using MelonLoader;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Сохранение локации — хосту целиком. Друг был в локации без хоста: всё, что игра пишет в сохранение
    // локации (ящики, вещи на полу, туши, звери и места их появления, костры, следы и то, что мод отдельно
    // не синхронизирует), уходит хосту одним куском и подставляется в его сохранение — как будто хост был там сам.
    //  - друг: игра кладёт локацию в своё сохранение (SaveGameSlots.SaveDataToSlot) — берём оттуда готовые байты
    //    и шлём, если пробыли в ней хотя бы 10 с после загрузки (отложенные события уже применены). Так при
    //    каждом сохранении (уход из локации, выход в меню, сон, сохранение раз в 5 минут, сохранение по просьбе хоста);
    //  - хост: подставляет, только если сам не в этой локации и в ней больше никого нет. Если там остался
    //    кто-то ещё, подставится сохранение того, кто уйдёт последним. Отложенные кусочки этой локации
    //    выбрасывает — они уже внутри; то, что придёт после, ляжет сверху, как обычно;
    //  - куски идут в том же журнале, что и события мира (Net/ClientLog.cs), поэтому после обрыва ничего
    //    старое не придёт позже сохранения;
    //  - пока хост сам не сохранился, подставленное лежит ещё и в папке мира (scenes/), на случай вылета хоста.
    public static class SceneHandoff
    {
        private const int PartSize = 200 * 1024;
        private const float MinStay = 10f;
        private static readonly HashSet<string> NotScenes = new() { "boot", "global", "info", "sandbox_global", "screenshot", SlotStore.Key };

        // ---------- Друг ----------

        private static string _visitKey = "", _visitScene = "";
        private static float _settledAt = -1f;
        private static string _leftKey = "", _leftScene = "";
        private static float _leftAt = -1f, _leftStay;
        private static readonly Dictionary<string, int> SentHash = new();
        private static int _sent;
        private static bool _toldMissing;

        public static void Tick(float now)
        {
            if (Session.Mode == Mode.Host) { HostTick(); return; }
            if (Session.Mode != Mode.Client || !Session.InSharedWorld || !GameApi.InGame())
            {
                // Вышли из мира хоста: заход закончился (сохранение при выходе в меню ловим как «ушли» ещё 2 минуты).
                if (_visitKey.Length > 0) Left(now);
                return;
            }
            string key = ItemSync.SceneKey();
            if (key != _visitKey)
            {
                if (_visitKey.Length > 0) Left(now);
                _visitKey = key;
                _visitScene = GameApi.Scene();
                _settledAt = -1f;
            }
            if (_settledAt < 0f && ItemSync.Settled && !GameApi.IsLoading()) _settledAt = now;
        }

        private static void Left(float now)
        {
            if (_settledAt >= 0f && now - _settledAt >= MinStay)
            {
                _leftKey = _visitKey;
                _leftScene = _visitScene;
                _leftAt = now;
                _leftStay = now - _settledAt;
            }
            _visitKey = "";
            _visitScene = "";
            _settledAt = -1f;
        }

        // Игра положила кусок в своё сохранение (Patch_SlotSave).
        internal static void OnSlotSave(SlotData slot, string key)
        {
            if (Session.Mode != Mode.Client || slot == null || string.IsNullOrEmpty(key) || NotScenes.Contains(key)) return;
            float now = Time.unscaledTime;
            string scene;
            if (key == _visitKey && Session.InSharedWorld && _settledAt >= 0f && now - _settledAt >= MinStay) scene = _visitScene;
            else if (key == _leftKey && _leftAt >= 0f && now - _leftAt < 120f && _leftStay >= MinStay) scene = _leftScene;
            else return;
            // У меня нет DLC или модов хоста (бот): в моём сохранении локации нет их вещей — у хоста они бы пропали.
            if (ContentCheck.MissingHere.Count > 0)
            {
                if (!_toldMissing) MelonLogger.Msg($"[локация] не отправляю хосту сохранения локаций — у меня нет DLC/модов хоста: {string.Join(", ", ContentCheck.MissingHere)}");
                _toldMissing = true;
                return;
            }
            // Рядом есть другой игрок — хост всё равно не возьмёт: подставит тот, кто уйдёт последним.
            foreach (var pl in Session.Players.Values)
                if (pl.Id != Session.LocalId && pl.InWorld && (pl.Scene == scene || pl.Scene == key) && now - pl.LastState < 6f) return;

            byte[] data;
            try
            {
                Il2CppStructArray<byte> arr = null;
                if (!slot.m_Dict.TryGetValue(key, out arr) || arr == null || arr.Length == 0) return;
                data = arr;
            }
            catch (Exception e) { MelonLogger.Warning("[локация] своё сохранение локации: " + e.Message); return; }

            int hash = Hash(data);
            if (SentHash.TryGetValue(key, out var old) && old == hash) return;
            SentHash[key] = hash;
            int id = new System.Random().Next(), total = (data.Length + PartSize - 1) / PartSize;
            for (int i = 0; i < total; i++)
            {
                int off = i * PartSize, len = Math.Min(PartSize, data.Length - off);
                var w = Msg.New(MsgType.SceneSave);
                w.Write(id); w.Write(i); w.Write(total);
                w.Write(key); w.Write(scene);
                w.Write(len); w.Write(data, off, len);
                Session.SendGameEvent(Msg.Done(w));
            }
            _sent++;
            MelonLogger.Msg($"[локация] {key}: отправил хосту своё сохранение локации ({data.Length / 1024} КБ, частей {total})");
        }

        private static int Hash(byte[] d)
        {
            unchecked
            {
                int h = (int)2166136261;
                for (int i = 0; i < d.Length; i++) h = (h ^ d[i]) * 16777619;
                return h ^ d.Length;
            }
        }

        // ---------- Хост ----------

        private static readonly Dictionary<int, (int id, byte[][] parts, int got)> Parts = new();
        private static readonly List<(int from, string name, string key, string scene, byte[] data, string save, bool late)> Queue = new();
        private static int _accepted, _rejected;

        public static string Stats => $"локаций от друзей подставлено {_accepted}, не взято {_rejected}" + (_sent > 0 ? $", своих отправлено {_sent}" : "");

        public static void HostReceive(int from, string name, BinaryReader r)
        {
            int id = r.ReadInt32(), index = r.ReadInt32(), total = r.ReadInt32();
            string key = r.ReadString(), scene = r.ReadString();
            var bytes = r.ReadBytes(r.ReadInt32());
            if (total <= 0 || total > 100 || index < 0 || index >= total || string.IsNullOrEmpty(key) || NotScenes.Contains(key)) return;
            if (!Parts.TryGetValue(from, out var p) || p.id != id || p.parts.Length != total) p = (id, new byte[total][], 0);
            if (p.parts[index] == null) { p.parts[index] = bytes; p.got++; }
            Parts[from] = p;
            if (p.got < total) return;
            Parts.Remove(from);
            using var ms = new MemoryStream();
            foreach (var part in p.parts) ms.Write(part, 0, part.Length);
            // Сразу решаем, брать ли (кто где сейчас), а подставляем в Tick, когда игра не пишет сохранение.
            // Хост на экране загрузки (дверь, переезд): решим в Tick, когда загрузится, — второй раз друг это не пришлёт.
            string why = Lacks(from) ?? (GameApi.InGame() ? WhyNot(from, key, scene) : InMainMenu() ? "я не в игре" : null);
            if (why != null)
            {
                _rejected++;
                MelonLogger.Msg($"[локация] {key} от {name}: не беру сохранение — {why}");
                return;
            }
            Queue.RemoveAll(q => q.key == key);
            Queue.Add((from, name, key, scene, ms.ToArray(), SaveName(), !GameApi.InGame()));
        }

        private static bool InMainMenu()
        {
            try { return GameManager.IsMainMenuActive(); } catch { return true; }
        }

        private static string SaveName()
        {
            try { return SaveGameSystem.GetCurrentSaveName() ?? ""; } catch { return ""; }
        }

        private static string Lacks(int from) => ContentCheck.LacksHostContent(from) ? "у него нет DLC/модов хоста, в его сохранении нет их вещей" : null;

        private static string WhyNot(int from, string key, string scene)
        {
            if (Lacks(from) is string lacks) return lacks;
            if (!GameApi.InGame()) return "я не в игре";
            if (ItemSync.SceneKey() == key || GameApi.Scene() == scene) return "я сам в этой локации";
            float now = Time.unscaledTime;
            foreach (var pl in Session.Players.Values)
                if (pl.Id != Session.LocalId && pl.Id != from && pl.InWorld && (pl.Scene == scene || pl.Scene == key) && now - pl.LastState < 6f)
                    return $"там ещё {pl.Name}, возьму у того, кто уйдёт последним";
            return null;
        }

        private static void HostTick()
        {
            // Хост сам зашёл в подставленную локацию: дальше её пишет его игра, сверять байты в файле не нужно.
            if (PutKeys.Count > 0 && GameApi.InGame()) PutKeys.Remove(ItemSync.SceneKey());
            if (Queue.Count == 0 || !GameApi.InGame() || GameApi.IsLoading() || WorldTransfer.SafeAsyncRunning()) return;
            var list = new List<(int from, string name, string key, string scene, byte[] data, string save, bool late)>(Queue);
            Queue.Clear();
            foreach (var q in list)
            {
                // Пока ждали, туда мог зайти кто-то (или мы сами); пока грузились, мог смениться мир.
                string why = q.save != SaveName() ? "мир сменился" : WhyNot(q.from, q.key, q.scene);
                // Пришло, пока грузились: кусочки друга после сохранения уже лежат в отложенных, DropPieces их бы выбросил.
                // Друг ещё там — ждём: при уходе пришлёт новое (с ними) и оно заменит это; ушёл без изменений — берём это.
                if (why == null && q.late)
                {
                    if (!Session.Players.TryGetValue(q.from, out var pl)) why = "друг отключился, не уйдя из локации";
                    else if (pl.InWorld && (pl.Scene == q.scene || pl.Scene == q.key) && Time.unscaledTime - pl.LastState < 6f) { Queue.Add(q); continue; }
                }
                if (why != null) { _rejected++; MelonLogger.Msg($"[локация] {q.key} от {q.name}: не беру сохранение — {why}"); continue; }
                if (!Put(q.key, q.data)) { _rejected++; continue; }
                DropPieces(q.key);
                Backup(q.key, q.data);
                _accepted++;
                MelonLogger.Msg($"[локация] {q.key} от {q.name}: подставил сохранение ({q.data.Length / 1024} КБ)");
            }
        }

        // Подставленные локации, куда хост с тех пор не заходил: при сохранении сверяем их байты в файле.
        private static readonly HashSet<string> PutKeys = new();

        private static SlotData CurrentSlot()
        {
            string name = SaveGameSystem.GetCurrentSaveName();
            return string.IsNullOrEmpty(name) ? null : SaveGameSlots.GetSaveSlotFromName(name);
        }

        private static string AutosaveName()
        {
            try { return SaveGameSlots.AUTOSAVE_SLOT_NAME ?? "autosave"; } catch { return "autosave"; }
        }

        private static void PutInto(SlotData slot, string key, Il2CppStructArray<byte> arr)
        {
            slot.Lock();
            try { slot.m_Dict[key] = arr; }
            finally { slot.UnLock(); }
        }

        // Игра держит локации в слоте autosave в памяти: из него грузит их при переходе и его же копирует
        // в слот мира при каждом сохранении (SaveGameSystem.SaveGame). Поэтому кладём в оба.
        private static bool Put(string key, byte[] data)
        {
            try
            {
                var slot = CurrentSlot();
                if (slot == null) { MelonLogger.Warning($"[локация] {key}: нет своего сохранения, некуда подставить"); return false; }
                var arr = new Il2CppStructArray<byte>(data);
                var auto = SaveGameSlots.GetSaveSlotFromName(AutosaveName());
                if (auto != null && auto.Pointer != slot.Pointer) PutInto(auto, key, arr);
                PutInto(slot, key, arr);
                PutKeys.Add(key);
                return true;
            }
            catch (Exception e)
            {
                MelonLogger.Warning($"[локация] {key}: подставить сохранение: " + e.Message);
                return false;
            }
        }

        // Кусочки этой локации, отложенные до нашего прихода, уже внутри подставленного сохранения.
        private static void DropPieces(string key)
        {
            SceneSnapshot.HandedOver(key);   // первым: запоминает раскладку и кусочки для друзей, у кого локации нет
            ItemSync.ClearPending(key);
            WorldObjects.ClearPending(key);
            WorldEvents.ClearPending(key);
        }

        // ---------- Запас на случай вылета хоста ----------

        private static string Dir()
        {
            string w = PlayerProgress.WorldFolder();
            return w == null ? null : Path.Combine(w, "scenes");
        }

        private static string FileFor(string dir, string key)
        {
            foreach (char c in Path.GetInvalidFileNameChars()) key = key.Replace(c, '_');
            return Path.Combine(dir, key + ".bin");
        }

        private static void Backup(string key, byte[] data)
        {
            try
            {
                string dir = Dir();
                if (dir == null) return;
                Directory.CreateDirectory(dir);
                string f = FileFor(dir, key), tmp = f + ".tmp";
                using (var w = new BinaryWriter(File.Create(tmp))) { w.Write(key); w.Write(data.Length); w.Write(data); }
                File.Copy(tmp, f, true);
                File.Delete(tmp);
            }
            catch (Exception e) { MelonLogger.Warning("[локация] запас сохранения локации: " + e.Message); }
        }

        // Хост сохранился: подставленное теперь в его файле, запас не нужен. Удаляем, только если в файле
        // именно эти байты (или хост с тех пор сам был в локации); иначе подставляем ещё раз и ждём следующего сохранения.
        public static void HostSaved(DateTime slotTime, byte[] file)
        {
            try
            {
                string dir = Dir();
                if (dir == null || !Directory.Exists(dir)) return;
                List<KeyValuePair<string, string>> dict = null;
                bool noCheck = false;   // файл не разобрался — удаляем по времени, как раньше
                foreach (var f in Directory.GetFiles(dir, "*.bin"))
                {
                    if (File.GetLastWriteTimeUtc(f) >= slotTime) continue;
                    string key = ""; byte[] data = null;
                    try { using var r = new BinaryReader(File.OpenRead(f)); key = r.ReadString(); data = r.ReadBytes(r.ReadInt32()); }
                    catch { }
                    if (data != null && PutKeys.Contains(key) && !noCheck)
                    {
                        if (dict == null)
                        {
                            try { dict = SaveJson.Split(SaveJson.Get(SaveJson.Split(System.Text.Encoding.UTF8.GetString(Lzf.Decompress(file))), "m_Dict")); }
                            catch (Exception e) { MelonLogger.Warning("[локация] запас: разобрать сохранение: " + e.Message); noCheck = true; }
                        }
                        if (!noCheck && !InFile(dict, key, data))
                        {
                            // Сейчас подставлять нельзя (грузимся, пишем сохранение, там кто-то есть) — проверим при следующем.
                            if (GameApi.IsLoading() || WorldTransfer.SafeAsyncRunning() || WhyNot(-1, key, key) != null) continue;
                            MelonLogger.Warning($"[локация] {key}: сохранения от друга нет в файле — подставляю ещё раз");
                            if (Put(key, data)) Backup(key, data);
                            continue;
                        }
                        PutKeys.Remove(key);
                    }
                    File.Delete(f);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[локация] запас: " + e.Message); }
        }

        private static bool InFile(List<KeyValuePair<string, string>> dict, string key, byte[] data)
        {
            try
            {
                string raw = SaveJson.Get(dict, key);
                if (string.IsNullOrEmpty(raw)) return false;
                var bytes = Convert.FromBase64String(SaveJson.Unquote(raw));
                return bytes.AsSpan().SequenceEqual(data);
            }
            catch { return false; }
        }

        // Хост загрузил мир: запас новее файла сохранения — игра упала раньше, чем успела его записать. Ставим снова.
        public static void HostLoaded(DateTime slotTime, DateTime pendingTime)
        {
            Queue.Clear();
            Parts.Clear();
            PutKeys.Clear();
            try
            {
                string dir = Dir();
                if (dir == null || !Directory.Exists(dir)) return;
                foreach (var f in Directory.GetFiles(dir, "*.bin"))
                {
                    var t = File.GetLastWriteTimeUtc(f);
                    if (t < slotTime) { File.Delete(f); continue; }
                    string key; byte[] data;
                    using (var r = new BinaryReader(File.OpenRead(f))) { key = r.ReadString(); data = r.ReadBytes(r.ReadInt32()); }
                    if (key == ItemSync.SceneKey() || !Put(key, data)) continue;
                    // Кусочки из pending.bin старше запаса уже внутри; новее — легли бы сверху, их не трогаем.
                    if (pendingTime < t) DropPieces(key);
                    MelonLogger.Msg($"[локация] {key}: снова подставил сохранение от друга (игра не успела его записать)");
                }
            }
            catch (Exception e) { MelonLogger.Warning("[локация] запас при загрузке: " + e.Message); }
        }

        public static void Reset()
        {
            Parts.Clear();
            Queue.Clear();
            PutKeys.Clear();
            SentHash.Clear();
            _toldMissing = false;
            _visitKey = _visitScene = _leftKey = _leftScene = "";
            _settledAt = _leftAt = -1f;
        }
    }

    [HarmonyPatch(typeof(SaveGameSlots), nameof(SaveGameSlots.SaveDataToSlot))]
    internal static class Patch_SlotSave
    {
        private static void Postfix(SlotData slot, string key, bool __result)
        {
            if (!__result) return;
            try { SceneHandoff.OnSlotSave(slot, key); }
            catch (Exception e) { MelonLogger.Warning("[локация] сохранение локации: " + e.Message); }
        }
    }
}
