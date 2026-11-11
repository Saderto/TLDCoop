using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using TLDCoop.Net;
using UnityEngine;

namespace TLDCoop.Game
{
    // Общий мир. Друг играет на копии сохранения хоста: хост сохраняется, присылает файл
    // сохранения кусками, друг кладёт его отдельным слотом «Кооп у <хост>» и загружает.
    // Так у обоих одинаковый мир со всем, что хост уже сделал (лут, контейнеры, двери, звери).
    // Дальнейшие изменения мира передаются отдельными сообщениями (следующие этапы).
    //
    // После загрузки друг — копия игрока хоста (его вещи, навыки, место). Поэтому сразу после
    // загрузки мод ставит другу его собственного игрока: новый выживший со стартовым набором
    // (позже — прогресс друга, который хранит хост).
    public static class WorldTransfer
    {
        private const int ChunkSize = 200 * 1024;   // надёжное сообщение Steam — до 512 КБ

        // ---------- Хост ----------

        private class Job
        {
            public Peer Peer;
            public float Started, NextTry;
            public string Path;
            public DateTime FileTimeBefore;
            public byte[] Data;
            public string Key;
            public int Next, Total;
            public uint Cut;          // номер события, на котором собрано отправляемое сохранение
            public bool ForcedSave;
        }

        private static readonly List<Job> Jobs = new();
        private const string CoopPrefix = "Кооп у ";   // начало имени слота с миром хоста (по нему же чистим старые)

        public static void HostRequest(Peer peer, string key)
        {
            // Друг просит снова, а мир ему уже идёт (он переподключился посреди передачи): шлём заново целиком.
            // Ещё ждём сохранения — его мир и так уйдёт следом.
            if (Jobs.Any(j => j.Peer == peer && j.Data == null)) return;
            if (Jobs.RemoveAll(j => j.Peer == peer) > 0) MelonLogger.Msg("[мир] друг просит мир заново посреди передачи, начинаю сначала");
            // Новая игра: файла сохранения ещё нет. Задачу всё равно заводим — TickHost ищет файл, пока игра его не запишет
            // (раньше запрос забывался, и друг минуту ждал повторного).
            string path = CurrentSlotFile();
            var job = new Job { Peer = peer, Started = Time.unscaledTime, Path = path, Key = key, FileTimeBefore = path != null ? File.GetLastWriteTimeUtc(path) : DateTime.MinValue };
            Jobs.Add(job);
            // Свежее сохранение, чтобы друг получил мир как он есть сейчас.
            try
            {
                if (GameApi.InGame()) GameManager.TriggerSurvivalSaveAndDisplayHUDMessage();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[мир] сохранение: " + e.Message);
            }
            MelonLogger.Msg("[мир] друг просит мир, сохраняюсь: " + (path ?? "файла сохранения ещё нет, жду его"));
        }

        public static void TickHost(float now)
        {
            for (int n = Jobs.Count - 1; n >= 0; n--)
            {
                var job = Jobs[n];
                if (job.Peer.Closed) { Jobs.RemoveAt(n); continue; }
                if (job.Data == null)
                {
                    if (job.Path == null)
                    {
                        if (now < job.NextTry) continue;
                        job.NextTry = now + 0.5f;
                        job.Path = CurrentSlotFile();
                        if (job.Path == null)
                        {
                            // Игра так и не записала файл: через 20 с сохраняемся силой, через 60 с бросаем (друг попросит снова).
                            if (!job.ForcedSave && now - job.Started >= 20f && !SafeAsyncRunning() && GameApi.InGame())
                            {
                                job.ForcedSave = true;
                                try { GameManager.ForceSaveGame(); MelonLogger.Msg("[мир] файла сохранения нет 20 с, сохраняюсь принудительно"); }
                                catch (Exception e) { MelonLogger.Warning("[мир] принудительное сохранение: " + e.Message); }
                            }
                            if (now - job.Started > 60f) { MelonLogger.Warning("[мир] не нашёл файл своего сохранения за 60 с"); Jobs.RemoveAt(n); }
                            continue;
                        }
                        // Файла не было: появившийся файл — уже свежее сохранение (FileTimeBefore = MinValue).
                        job.NextTry = 0f;
                        MelonLogger.Msg("[мир] файл сохранения появился: " + job.Path);
                    }
                    bool saved = File.GetLastWriteTimeUtc(job.Path) > job.FileTimeBefore;
                    bool busy = SafeAsyncRunning();
                    // Игра откладывает сохранение (меню, схватка, долгое действие): через 20 с сохраняем силой, ждём ещё 20 с.
                    if (!saved && !busy && !job.ForcedSave && now - job.Started >= 20f && GameApi.InGame())
                    {
                        job.ForcedSave = true;
                        try { GameManager.ForceSaveGame(); MelonLogger.Msg("[мир] игра не сохранилась за 20 с, сохраняюсь принудительно"); }
                        catch (Exception e) { MelonLogger.Warning("[мир] принудительное сохранение: " + e.Message); }
                        continue;
                    }
                    if (now - job.Started < 1.5f || busy || (!saved && now - job.Started < 40f)) continue;
                    try
                    {
                        job.Data = File.ReadAllBytes(job.Path);
                        SceneSnapshot.RefreshSavedScenes(job.Data);
                    }
                    catch (IOException)
                    {
                        continue;   // игра ещё пишет файл
                    }
                    // Друг уже играл в этом мире: его вещи, здоровье и место берём из его прогресса.
                    bool restored = false;
                    string progress = PlayerProgress.Load(job.Key);
                    if (progress != null)
                    {
                        try
                        {
                            job.Data = PlayerProgress.Patch(job.Data, progress);
                            restored = true;
                            MelonLogger.Msg("[мир] вставил в мир сохранённый прогресс друга " + job.Key);
                        }
                        catch (Exception e)
                        {
                            MelonLogger.Warning("[мир] прогресс друга не подошёл, он начнёт заново: " + e.Message);
                        }
                    }
                    if (!restored)
                    {
                        // Первый вход: журнал, карта, записки, рецепты и задания хоста другу не достаются.
                        try
                        {
                            job.Data = PlayerProgress.StripForNewcomer(job.Data, out int removed);
                            MelonLogger.Msg($"[мир] новый друг {job.Key}: убрал из копии мира личное хоста (журнал, карта, записки, задания), разделов {removed}");
                        }
                        catch (Exception e) { MelonLogger.Warning("[мир] убрать личное хоста: " + e.Message); }
                    }
                    // Последнее сохранение (свежее или старое) собрано на этом номере события: всё после него
                    // дошлём из журнала, когда уйдёт мир.
                    job.Cut = EventLog.SavedAt;
                    var info = Msg.New(MsgType.WorldInfo);
                    info.Write(restored);
                    info.Write(EventLog.LogId);
                    info.Write(job.Cut);
                    job.Peer.Send(Msg.Done(info));
                    job.Total = (job.Data.Length + ChunkSize - 1) / ChunkSize;
                    MelonLogger.Msg($"[мир] отправляю {job.Data.Length / 1024} КБ, {job.Total} частей" + (saved ? "" : " (сохраниться не удалось, шлю последнее сохранение)"));
                }
                if (now < job.NextTry) continue;
                // Steam сам ограничивает скорость; если буфер полон — пробуем чуть позже.
                // Куски мира не обгоняют очередь повторов Steam: события, ушедшие до них, должны прийти раньше.
                if (job.Peer.Queued > 0) { job.NextTry = now + 0.2f; continue; }
                for (int k = 0; k < 2 && job.Next < job.Total; k++)
                {
                    int off = job.Next * ChunkSize, len = Math.Min(ChunkSize, job.Data.Length - off);
                    var w = Msg.New(MsgType.WorldChunk);
                    w.Write(job.Next);
                    w.Write(job.Total);
                    w.Write(len);
                    w.Write(job.Data, off, len);
                    if (!job.Peer.TrySend(Msg.Done(w)))
                    {
                        job.NextTry = now + 0.2f;
                        break;
                    }
                    job.Next++;
                }
                if (job.Next >= job.Total)
                {
                    // Плюс события в локациях, где хост сейчас не был (их нет в сохранении).
                    int pending = 0;
                    var sentPending = new HashSet<(int, ulong)>();
                    foreach (var raw in ItemSync.AllPending()) { job.Peer.Send(raw); pending++; sentPending.Add(EventLog.Fingerprint(raw)); }
                    foreach (var raw in WorldObjects.AllPending()) { job.Peer.Send(raw); pending++; sentPending.Add(EventLog.Fingerprint(raw)); }
                    foreach (var raw in WorldEvents.AllPending()) { job.Peer.Send(raw); pending++; sentPending.Add(EventLog.Fingerprint(raw)); }
                    // И всё, что случилось после того, как игра собрала сохранение (друг это пропустил, пока грузился).
                    // То, что уже ушло среди отложенных, второй раз не шлём (только номер): подбор вещи без номера,
                    // применённый дважды, убирал вторую такую же вещь рядом.
                    int after = EventLog.HostResend(job.Peer, job.Key, job.Cut + 1, EventLog.Current, full: true, skip: sentPending);
                    MelonLogger.Msg("[мир] мир отправлен" + (pending > 0 ? $", отложенных событий {pending}" : "") + $", событий после сохранения (№{job.Cut}) {after}");
                    Jobs.RemoveAt(n);
                }
            }
        }

        public static bool SafeAsyncRunning()
        {
            try { return SaveGameSystem.IsAsyncSaveRunning(); } catch { return false; }
        }

        // Файл текущего сохранения на диске (…\Hinterland\TheLongDark\Survival\sandboxN).
        public static string CurrentSlotFile()
        {
            string name = SaveGameSystem.GetCurrentSaveName();
            if (string.IsNullOrEmpty(name)) return null;
            try
            {
                var slot = SaveGameSlots.GetSaveSlotFromName(name);
                string f = slot?.m_Filename;
                if (!string.IsNullOrEmpty(f) && File.Exists(f)) return f;
            }
            catch { }
            foreach (var dir in SaveDirs())
            {
                string p = Path.Combine(dir, name);
                if (File.Exists(p)) return p;
            }
            return null;
        }

        private static IEnumerable<string> SaveDirs()
        {
            var list = new List<string>();
            try
            {
                string pdp = PersistentDataPath.GetPersistentDataPath();
                if (!string.IsNullOrEmpty(pdp)) { list.Add(Path.Combine(pdp, "Survival")); list.Add(pdp); }
            }
            catch { }
            if (Bot.SavesMoved)
            {
                // GetPersistentDataPath у бота всё ещё отвечает папкой на C:, а пишет игра в UserData/BotSaves.
                string bot = Path.Combine(MelonEnvironment.UserDataDirectory, "BotSaves");
                list.Insert(0, bot);
                list.Insert(0, Path.Combine(bot, "Survival"));
                return list.Distinct().Where(Directory.Exists);
            }
            string local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            list.Add(Path.Combine(local, "Hinterland", "TheLongDark", "Survival"));
            list.Add(Path.Combine(local, "Hinterland", "TheLongDark"));
            return list.Distinct().Where(Directory.Exists);
        }

        // ---------- Друг ----------

        private static byte[][] _parts;
        private static int _received;
        private static float _requested;
        public static string State { get; private set; } = "";
        public static bool Busy => _parts != null || LoadingHostWorld || FeatsChoice.Choosing;
        private static string _loadingSlot;
        private static bool _loadThrew;   // LoadSaveGameSlot бросил исключение: ждём, пошла ли загрузка
        // Идёт загрузка копии мира хоста (Game/FoodBalanceLink.cs). После упавшего вызова — нет, пока не увидим экран загрузки:
        // друг ещё в своём сохранении, события и рюкзаки мира хоста к нему не относятся.
        public static bool LoadingHostWorld => _loadingSlot != null && !_loadThrew;
        private static float _loadStarted;
        public static bool JustLoaded { get; private set; }
        // Хост вставил в мир наш сохранённый прогресс: свой выживший уже на месте.
        public static bool Restored { get; private set; }

        // Идёт мир хоста: события журнала пропускаем, после последней части хост дошлёт всё после сохранения.
        public static bool Receiving { get; private set; }
        // Попросили мир заново: куски прошлой передачи, ещё идущие по сети, не берём до нового WorldInfo.
        private static bool _needInfo;

        public static void ResetClient()
        {
            Receiving = false; _parts = null; _received = 0; _needInfo = false;
            FeatsChoice.Cancel();
            if (_loadThrew) { _loadThrew = false; _loadingSlot = null; FoodPackLink.SetOverride(null); }   // упавшая загрузка без коопа не доделается
        }

        public static void ClientInfo(BinaryReader r)
        {
            _needInfo = false;
            _parts = null;
            _received = 0;
            Restored = r.ReadBoolean();
            if (r.BaseStream.Position + 8 <= r.BaseStream.Length) EventLog.ClientStartAt(r.ReadInt32(), r.ReadUInt32());
        }

        public static void Request(Peer host)
        {
            _parts = null;
            _received = 0;
            _requested = Time.unscaledTime;
            Restored = false;
            Receiving = true;
            _needInfo = true;
            WorldReset.Run("прошу мир у хоста");
            host.Send(Msg.Done(Msg.New(MsgType.WorldRequest)));
            State = UI.L.T("Хост сохраняется и отправляет мир…", "The host is saving and sending the world…");
            MelonLogger.Msg("[мир] прошу мир у хоста");
        }

        public static void ClientChunk(BinaryReader r, string hostName)
        {
            int index = r.ReadInt32(), total = r.ReadInt32(), len = r.ReadInt32();
            var bytes = r.ReadBytes(len);
            if (_needInfo || total <= 0 || index < 0 || index >= total) return;
            if (_parts == null || _parts.Length != total) { _parts = new byte[total][]; _received = 0; }
            if (_parts[index] == null) { _parts[index] = bytes; _received++; }
            State = UI.L.T("Получаю мир хоста: ", "Receiving the host's world: ") + $"{_received * 100 / total}%";
            if (_received < total) return;
            Receiving = false;

            var file = new byte[_parts.Sum(p => p.Length)];
            int off = 0;
            foreach (var p in _parts) { Buffer.BlockCopy(p, 0, file, off, p.Length); off += p.Length; }
            _parts = null;
            MelonLogger.Msg($"[мир] получено {file.Length / 1024} КБ за {Time.unscaledTime - _requested:0} с");
            try
            {
                LoadHostWorld(file, hostName);
            }
            catch (Exception e)
            {
                State = UI.L.T("Не удалось загрузить мир хоста: ", "Could not load the host's world: ") + e.Message;
                MelonLogger.Error("[мир] " + e);
            }
        }

        private static void LoadHostWorld(byte[] file, string hostName)
        {
            string json = Encoding.UTF8.GetString(Lzf.Decompress(file));
            SceneSnapshot.ClientCopyLoaded(json);
            string foodState = PlayerProgress.FoodStateFromCopy(json);
            var modData = PlayerProgress.ModDataFromCopy(json);
            uint id = SaveGameSlots.GetUnusedGameId();
            string name = SaveGameSlots.BuildSlotName(Episode.One, SaveSlotType.SANDBOX, id);
            json = ReplaceField(json, "m_InternalName", "\"" + name + "\"");
            json = ReplaceField(json, "m_GameId", id.ToString());
            json = ReplaceField(json, "m_DisplayName", "\"" + JsonEscape(CoopPrefix + hostName) + "\"");

            bool ok = false;
            try
            {
                ok = SaveGameSlots.CreateSlotFromData(json, name, file.Length) && SaveGameSlots.WriteSlotToDisk(name);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[мир] CreateSlotFromData: " + e.Message);
            }
            string fallbackFile = null;
            if (!ok)
            {
                // Запасной путь: положить файл рядом со своими сохранениями и перечитать список.
                string dir = SaveDirs().FirstOrDefault() ?? throw new IOException("не нашёл папку сохранений");
                fallbackFile = Path.Combine(dir, name);
                File.WriteAllBytes(fallbackFile, Lzf.StoreOnly(Encoding.UTF8.GetBytes(json)));
                SaveGameSlots.LoadAllSavedGameFiles();
                MelonLogger.Msg("[мир] слот записан файлом в " + dir);
            }

            SaveSlotInfo info = null;
            var slots = SaveGameSystem.GetSortedSaveSlots(Episode.One, SaveSlotType.SANDBOX);
            for (int i = 0; i < slots.Count; i++)
                if (slots[i].m_SaveSlotName == name) { info = slots[i]; break; }
            if (info == null)
            {
                // 0.19.55: игра этот мир не прочитала — файл в сохранениях не оставляем. Иначе он остаётся среди
                // сохранений навсегда (друзья 11-10: после неудачных входов на 0.19.53 игра у друга перестала
                // доходить до главного меню, в папке лежал нечитаемый sandbox7).
                if (fallbackFile != null)
                    try { File.Delete(fallbackFile); MelonLogger.Msg("[мир] нечитаемый файл " + name + " убрал из сохранений"); }
                    catch (Exception e) { MelonLogger.Warning("[мир] не смог убрать " + fallbackFile + ": " + e.Message); }
                throw new InvalidOperationException("игра не видит новый слот " + name);
            }

            // Подвиги: своих в копии мира нет — сначала окно выбора (режим хоста уже нужен: от него зависит, сколько
            // подвигов можно взять), потом загрузка. Окна нет — входим без подвигов, а не с подвигами хоста.
            FeatsChoice.Clear();
            if (PlayerProgress.AsksFeats(json))
            {
                UseSlotMode(info);
                if (FeatsChoice.Begin(() => StartLoad(info, name, foodState, modData)))
                {
                    State = UI.L.T("Выбери подвиги — потом загрузка мира хоста", "Choose your feats, then the host's world loads");
                    return;
                }
                FeatsChoice.UseNone();
            }
            StartLoad(info, name, foodState, modData);
        }

        private static void StartLoad(SaveSlotInfo info, string name, string foodState, Dictionary<string, string> modData)
        {
            // ModData других модов — к новому слоту, со своими записями (иначе пишет в прежний, даже в личное сохранение).
            ModDataLink.BeforeLoad(name, modData);
            CleanupOldCoopSlots(name);
            MelonLogger.Msg("[мир] загружаю мир хоста как " + name);
            State = UI.L.T("Загружаю мир хоста…", "Loading the host's world…");
            _loadingSlot = name;
            _loadStarted = Time.unscaledTime;
            SaveGameSystem.SetCurrentSaveInfo(info.m_Episode, info.m_GameMode, info.m_GameId, info.m_SaveSlotName);
            UseSlotMode(info);
            // Эффекты еды друга: мод возьмёт их при загрузке вместо файла .moddata нового слота.
            FoodPackLink.SetOverride(foodState, info.m_SaveSlotName);
            _loadThrew = false;
            try { GameManager.LoadSaveGameSlot(info.m_SaveSlotName, info.m_SaveChangelistVersion); }
            catch
            {
                // Загрузку игра ставит в очередь последним шагом, экран загрузки появляется позже: решаем в TickClient,
                // пошла ли она всё же (тогда друг доделает вход как обычно) или нет (тогда сбрасываем).
                _loadThrew = true;
                throw;
            }
        }

        // Меню игры перед загрузкой ставит режим сложности из слота (Panel_MainMenu.OnLoadGame →
        // ExperienceModeManager.SetGameModeConfig), а сама загрузка режим не меняет. Мы грузим слот сами,
        // и без этого друг играл в мире хоста в своём последнем режиме: например, в «Пилигриме»
        // волки у него не нападали и сразу убегали, хотя у хоста «Сталкер».
        private static void UseSlotMode(SaveSlotInfo info)
        {
            try
            {
                var config = info.m_GameModeConfig ?? NewGame.FindMode((int)info.m_XPMode)?.Config;
                var m = GameManager.GetExperienceModeManagerComponent();
                if (config == null || m == null) { MelonLogger.Warning("[мир] не нашёл режим сложности мира хоста"); return; }
                m.SetGameModeConfig(config);
                MelonLogger.Msg($"[мир] режим сложности как у хоста: {GameApi.ModeType()}");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[мир] режим сложности хоста: " + e.Message);
            }
        }

        // Вызывается каждый кадр у друга: после загрузки ставит своего игрока.
        public static void TickClient(float now)
        {
            JustLoaded = false;
            if (Receiving && now - _requested > 300f)
            {
                // Недополученный мир не держим: иначе Busy навсегда, и новая попытка молча ничего не делает.
                Receiving = false; _parts = null; _received = 0; _needInfo = false;
                MelonLogger.Warning("[мир] мир хоста так и не пришёл за 5 минут");
            }
            if (_loadingSlot == null) return;
            if (now - _loadStarted > 240f) { State = UI.L.T("Мир хоста не загрузился за 4 минуты.", "The host's world did not load within 4 minutes."); _loadingSlot = null; FoodPackLink.SetOverride(null); FeatsChoice.Clear(); return; }
            if (_loadThrew)
            {
                if (GameApi.IsLoading()) { _loadThrew = false; MelonLogger.Warning("[мир] LoadSaveGameSlot упал, но загрузка пошла — доделываю вход"); }
                else { if (now - _loadStarted > 15f) { _loadThrew = false; _loadingSlot = null; FoodPackLink.SetOverride(null); } return; }
            }
            if (!GameApi.InGame() || now - _loadStarted < 3f) return;
            if (SaveGameSystem.GetCurrentSaveName() != _loadingSlot) return;
            _loadingSlot = null;
            State = "";
            FeatsChoice.Clear();
            FoodPackLink.AfterLoad();
            if (Restored) MelonLogger.Msg("[мир] ты там же, где вышел, со своими вещами");
            else FreshSurvivor();
            ClearGhost();
            JustLoaded = true;
        }

        // Новый выживший: свои вещи (стартовый набор), навыки с нуля, полное здоровье.
        public static void FreshSurvivor()
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                try { pm?.UnequipImmediate(false); } catch { }
                GameManager.GetInventoryComponent()?.DestroyAllGear();
                var config = ExperienceModeManager.GetCurrentGameModeConfig();
                if (config?.StartGear != null && pm != null) config.StartGear.AddStartingGearToPlayer(pm, null);
                else pm?.m_StartGear?.AddAllToInventory();

                var sm = GameManager.GetSkillsManager();
                for (int i = 0; i < sm.GetNumSkills(); i++)
                    sm.GetSkillFromIndex(i)?.SetTier(0, SkillsManager.PointAssignmentMode.AssignInAnyMode);

                ConsoleManager.CONSOLE_afflictions_cure();
                FoodPackLink.ImportPlayerState("");   // и эффекты нашей еды
                var cond = GameManager.GetConditionComponent();
                if (cond != null) { cond.m_CurrentHP = cond.m_MaxHP; cond.m_Invulnerable = false; cond.m_NeverDie = false; }
                if (pm != null) pm.m_God = false;   // флаги бессмертия хоста из его сохранения — не наши
                var fr = GameManager.GetFreezingComponent(); if (fr != null) fr.m_CurrentFreezing = 0f;
                var th = GameManager.GetThirstComponent(); if (th != null) th.m_CurrentThirst = 0f;
                var fa = GameManager.GetFatigueComponent(); if (fa != null) fa.m_CurrentFatigue = 0f;
                var hu = GameManager.GetHungerComponent(); if (hu != null) hu.m_CurrentReserveCalories = hu.m_MaxReserveCalories;
                MelonLogger.Msg("[мир] свой выживший: стартовый набор, навыки с нуля");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[мир] свой выживший: " + e);
            }
        }

        // «Невидимость для животных» (PlayerManager.m_Ghost, её ставит трейнер) пишется в сохранение. Новый друг
        // получал её из сохранения хоста (у хоста трейнер с невидимостью), а друг с прошлого раза — из своего
        // прогресса, даже если трейнер у него давно выключен: звери не видели этих игроков, кролики не пугались
        // (11-10). После входа в мир хоста снимаем; если у друга невидимость включена в трейнере, трейнер
        // поставит её снова сам.
        private static void ClearGhost()
        {
            try
            {
                var pm = GameManager.GetPlayerManagerComponent();
                if (pm == null || !pm.m_Ghost) return;
                pm.m_Ghost = false;
                MelonLogger.Msg("[мир] снял невидимость для зверей, пришедшую с сохранением (m_Ghost); включена в трейнере — он вернёт её сам");
            }
            catch (Exception e) { MelonLogger.Warning("[мир] невидимость: " + e.Message); }
        }

        // Каждое подключение создаёт новый слот; старые слоты «Кооп у …» удаляем, чтобы не копились.
        // Только если слот и сейчас наш: номер удалённого слота игра отдаёт новой игре, и под старым именем
        // может лежать уже личное сохранение друга. Несуществующие и чужие просто забываем.
        private static void CleanupOldCoopSlots(string keep)
        {
            var old = Config.CoopSlots.Where(s => s != keep).ToList();
            foreach (var s in old)
            {
                try
                {
                    var slot = SaveGameSlots.GetSaveSlotFromName(s);
                    if (slot == null) continue;
                    string shown = slot.m_DisplayName ?? "";
                    if (!shown.StartsWith(CoopPrefix, StringComparison.Ordinal))
                    {
                        MelonLogger.Msg($"[мир] слот {s} уже не кооп-слот («{shown}»), не удаляю");
                        continue;
                    }
                    SaveGameSlots.DeleteSlot(s);
                    ModDataLink.DeleteFile(s);
                    MelonLogger.Msg("[мир] удалил старый слот " + s);
                }
                catch (Exception e) { MelonLogger.Warning("[мир] удаление " + s + ": " + e.Message); }
            }
            Config.CoopSlots = new List<string> { keep };
        }

        private static string ReplaceField(string json, string field, string value)
        {
            var re = new Regex("\"" + field + "\":(\"(?:[^\"\\\\]|\\\\.)*\"|-?\\d+)");
            return re.Replace(json, "\"" + field + "\":" + value.Replace("$", "$$"), 1);
        }

        private static string JsonEscape(string s) => s.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
