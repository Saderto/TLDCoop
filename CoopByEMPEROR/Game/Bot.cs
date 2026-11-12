using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using CoopByEMPEROR.Net;
using UnityEngine;

namespace CoopByEMPEROR.Game
{
    // Бот для проверки без второго человека. Это вторая копия игры на том же ПК
    // (её делает Mods/CoopByEMPEROR/TLD-bot.bat), в её папке UserData лежит файл CoopByEMPEROR-bot.txt.
    // Бот сам подключается к хосту по 127.0.0.1, загружается в его мир, держится рядом
    // и по очереди выбрасывает и поднимает вещи — так, как это делает игра, без нажатий клавиш.
    // Что он сделал, он пишет в чат, а проверка синхронизации (SyncCheck) сравнивает мир.
    //
    // Steam у бота выключен: аккаунт тот же, что у хоста, и SteamID совпал бы.
    // Сохранения бота лежат в его собственной папке, чтобы не трогать сохранения игрока.
    public static class Bot
    {
        public static bool Active { get; private set; }
        private static string _ip = "127.0.0.1";
        private static int _port = Protocol.DefaultPort;
        private static float _next, _nextJoin, _nextAction, _nextCheap, _nextSave;
        private static int _step;
        private static bool _pathsMoved;

        public static void Detect()
        {
            try
            {
                string f = Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop-bot.txt");
                if (!File.Exists(f)) return;
                Active = true;
                foreach (var line in File.ReadAllLines(f))
                {
                    var kv = line.Split('=', 2);
                    if (kv.Length != 2) continue;
                    string k = kv[0].Trim().ToLowerInvariant(), v = kv[1].Trim();
                    if (k == "host" && v.Length > 0) _ip = v;
                    if (k == "port" && int.TryParse(v, out int p)) _port = p;
                }
                MelonLogger.Msg($"[бот] режим бота: подключаюсь к {_ip}:{_port}");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[бот] " + e.Message);
            }
        }

        // Главное меню загрузилось: свои сохранения, ник.
        public static void OnReady()
        {
            if (!Active) return;
            Config.Name = "Бот";
            MovePaths();
        }

        // Игра хранит пути к сохранениям в статических полях PersistentDataPath.
        // Меняем их на папку бота (UserData/BotSaves) и перечитываем список сохранений.
        private static void MovePaths()
        {
            if (_pathsMoved) return;
            try
            {
                string root = PersistentDataPath.m_Path ?? "";
                MelonLogger.Msg("[бот] папка сохранений игры: " + root);
                if (root.Length == 0) return;
                string sep = root.EndsWith("/") ? "/" : root.EndsWith("\\") ? "\\" : "";
                string mine = Path.Combine(MelonEnvironment.UserDataDirectory, "BotSaves").Replace('\\', '/') + sep;
                string Move(string p) => !string.IsNullOrEmpty(p) && p.StartsWith(root) ? mine + p.Substring(root.Length) : p;
                PersistentDataPath.m_PathSurvival = Move(PersistentDataPath.m_PathSurvival);
                PersistentDataPath.m_PathNoSync = Move(PersistentDataPath.m_PathNoSync);
                PersistentDataPath.m_PathNoSyncSurvival = Move(PersistentDataPath.m_PathNoSyncSurvival);
                PersistentDataPath.m_Path = Move(root);
                foreach (var d in new[] { PersistentDataPath.m_Path, PersistentDataPath.m_PathSurvival, PersistentDataPath.m_PathNoSync, PersistentDataPath.m_PathNoSyncSurvival })
                    if (!string.IsNullOrEmpty(d)) Directory.CreateDirectory(d);
                CopySettings(root, PersistentDataPath.m_Path);
                _pathsMoved = true;
                SaveGameSlots.LoadAllSavedGameFiles();
                MelonLogger.Msg("[бот] сохранения бота: " + PersistentDataPath.m_Path + " (GetPersistentDataPath = " + PersistentDataPath.GetPersistentDataPath() + ")");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[бот] не удалось перенести сохранения: " + e.Message);
            }
        }

        // Настройки игры (яркость, управление и т.п.) лежат в той же папке, что и сохранения, в файле user001.
        // Без него у бота при первом входе в мир открывается первоначальная настройка игры (чёрный экран
        // с настройкой яркости). Копируем только файлы настроек, не сохранения.
        private static readonly string[] SettingsFiles = { "user001", "SeenExpeditions", "FlairsUpdateData", "ConfiguratedBenefits" };

        private static void CopySettings(string from, string to)
        {
            try
            {
                if (!Directory.Exists(from)) return;
                foreach (var name in SettingsFiles)
                {
                    string src = Path.Combine(from, name), dst = Path.Combine(to, name);
                    if (!File.Exists(src) || File.Exists(dst)) continue;
                    File.Copy(src, dst);
                    MelonLogger.Msg("[бот] скопировал настройки игры: " + name);
                }
                // 0.7.13–0.7.15 копировали в папку бота все файлы, в том числе сохранения игрока.
                // Убираем из папки бота точные копии (то же имя, размер и время), кроме файлов настроек.
                foreach (var dst in Directory.GetFiles(to))
                {
                    string name = Path.GetFileName(dst), src = Path.Combine(from, name);
                    if (Array.IndexOf(SettingsFiles, name) >= 0 || !File.Exists(src)) continue;
                    var a = new FileInfo(src); var b = new FileInfo(dst);
                    if (a.Length != b.Length || a.LastWriteTimeUtc != b.LastWriteTimeUtc) continue;
                    File.Delete(dst);
                    MelonLogger.Msg("[бот] убрал из папки бота лишнюю копию: " + name);
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[бот] настройки игры: " + e.Message);
            }
        }

        public static bool SavesMoved => _pathsMoved;

        public static void Tick()
        {
            if (!Active) return;
            float now = Time.unscaledTime;
            if (now < _next) return;
            _next = now + 0.5f;

            if (now >= _nextCheap)
            {
                // Копия для проверки: без звука и с малой частотой кадров, чтобы не мешать игроку.
                _nextCheap = now + 5f;
                AudioListener.volume = 0f;
                Application.targetFrameRate = 30;
            }

            if (Session.Mode == Mode.Off)
            {
                if (now < _nextJoin) return;
                _nextJoin = now + 10f;
                Session.JoinIp(_ip, _port);
                return;
            }
            if (Session.Mode != Mode.Client || !GameApi.InGame() || !Session.InHostWorld) return;
            CheckFade(now);
            if (Death.ScreenOpen) { Death.ChooseDefault(); return; }
            if (_nextSave == 0f) _nextSave = now + 120f;
            else if (now >= _nextSave)
            {
                // Окно бота просто закрывают, без «Сохранить и выйти». Сохраняемся сами, чтобы хост получил прогресс.
                _nextSave = now + 120f;
                PlayerProgress.OnSaveRequest();
                MelonLogger.Msg("[бот] сохранился");
            }
            if (TickTrip(now)) return;
            if (BotTests.Running) { BotTests.Tick(now); return; }
            if (now < _farUntil)
            {
                if (now >= _nextAction) { _nextAction = now + 30f; Say("стою далеко. " + AnimalSync.Status); }
                return;
            }
            if (now < _nextAction) return;
            _nextAction = now + 12f;
            try
            {
                // Сам бот никуда не телепортируется: только по команде хоста («бот ко мне», «бот далеко», «бот уйди»).
                if (_step++ % 2 == 0) DropSomething();
                else PickNearest();
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[бот] " + e.Message);
            }
        }

        // У бота после входа в мир экран иногда остаётся чёрным (затемнение CameraFade не снято).
        // Пишем в лог, что с затемнением, и, если оно держится, снимаем его.
        private static float _nextFade, _darkSince, _nextDiag;

        // Что сейчас на экране у бота: включённые панели, камера, затемнение (для поиска чёрного экрана).
        private static void Diagnose()
        {
            try
            {
                var panels = new List<string>();
                foreach (var p in UnityEngine.Object.FindObjectsOfType<Panel_Base>())
                    if (p != null && p.gameObject.activeInHierarchy) panels.Add(p.GetType().Name + "/" + p.gameObject.name);
                var cam = Camera.main;
                var vp = GameManager.GetVpFPSCamera();
                MelonLogger.Msg($"[бот] экран: затемнение {CameraFade.CurrentAlpha:0.00}, timeScale {Time.timeScale:0.00}, " +
                    $"Camera.main {(cam == null ? "нет" : cam.name + (cam.enabled ? " вкл" : " выкл"))}, " +
                    $"vpCamera {(vp == null ? "нет" : vp.enabled ? "вкл" : "выкл")}, панели: {string.Join(", ", panels)}");
            }
            catch (Exception e) { MelonLogger.Warning("[бот] диагностика: " + e.Message); }
        }

        private static void CheckFade(float now)
        {
            if (now < _nextFade) return;
            _nextFade = now + 5f;
            if (now >= _nextDiag) { _nextDiag = now + 60f; Diagnose(); }
            try
            {
                float a = CameraFade.CurrentAlpha;
                bool fading = CameraFade.IsFading;
                if (a < 0.5f || fading || GameApi.IsLoading()) { _darkSince = 0f; return; }
                if (_darkSince == 0f) { _darkSince = now; MelonLogger.Msg($"[бот] экран затемнён: {a:0.00}"); return; }
                if (now - _darkSince < 8f) return;
                MelonLogger.Msg($"[бот] экран затемнён уже {now - _darkSince:0} с, снимаю затемнение");
                CameraFade.FadeIn(1f, 0f, null);
                _darkSince = 0f;
            }
            catch (Exception e) { MelonLogger.Warning("[бот] затемнение: " + e.Message); }
        }

        private static bool _awayDone;

        // ---------- Уйти в другую локацию (по команде хоста «бот уйди») ----------
        // Проверка случая «друг первым пришёл туда, где хоста нет»: бот уходит в другой регион и живёт там,
        // хост потом приходит к нему (кнопка «К нему») и проверка сравнивает.
        private static readonly string[] TripRegions =
        {
            "CoastalRegion", "RuralRegion", "MountainTownRegion", "MarshRegion", "CrashMountainRegion",
            "WhalingStationRegion", "RiverValleyRegion", "LakeRegion",
        };
        private static string _tripScene;
        private static float _tripTime;
        private static bool _tripPlaced;

        public static void OnChat(int from, string text)
        {
            if (!Active || from != 0 || text == null) return;
            string t = text.ToLowerInvariant();
            if (t.Contains("бот") && t.Contains("проверк")) BotTests.Start();
            else if (t.Contains("бот") && t.Contains("уйди")) StartTrip();
            else if (t.Contains("бот") && t.Contains("далеко")) GoFar();
            else if (t.Contains("бот") && (t.Contains("назад") || t.Contains("ко мне"))) { _farUntil = 0f; FollowHost(true); Say("я рядом с тобой"); }
        }

        // У бота нет DLC или модов мира, которые есть у хоста (TLD-bot.bat копирует только CoopByEMPEROR): вещей хоста из них
        // бот не видит, и его ящик или первая раскладка новой локации стёрли бы их у хоста. Такие проверки бот не делает.
        internal static bool ContentLikeHost()
        {
            var miss = ContentCheck.MissingHere;
            if (miss.Count == 0) return true;
            Say("не буду: у меня нет того, что есть у хоста (" + string.Join(", ", miss) + "), мой ящик или новая локация стёрли бы его вещи. Поставь боту те же DLC и моды");
            return false;
        }

        private static void StartTrip()
        {
            if (Session.Mode != Mode.Client || !GameApi.InGame()) return;
            if (!ContentLikeHost()) return;
            string mine = GameApi.Scene();
            string hostScene = Session.Players.TryGetValue(0, out var h) ? h.Scene : "";
            var choices = new List<string>();
            foreach (var r in TripRegions) if (r != mine && r != hostScene) choices.Add(r);
            if (choices.Count == 0) return;
            _tripScene = choices[UnityEngine.Random.Range(0, choices.Count)];
            _tripTime = Time.unscaledTime;
            _tripPlaced = false;
            Say("ухожу в " + _tripScene + ". Позови меня кнопкой «Ко мне» или приходи сам кнопкой «К нему»");
            GameApi.LoadScene(_tripScene);
        }

        // true — бот занят переходом, остальное не делаем.
        private static bool TickTrip(float now)
        {
            if (_tripScene == null) return false;
            if (now - _tripTime > 120f && !_tripPlaced) { Say("не получилось перейти в " + _tripScene); _tripScene = null; return false; }
            if (GameApi.Scene() != _tripScene)
            {
                // Хост позвал к себе («Ко мне»), пока бот был в походе.
                if (_tripPlaced) _tripScene = null;
                return !_tripPlaced;
            }
            if (_tripPlaced) return false;
            if (!ItemSync.Settled || now - _tripTime < 5f) return true;
            _tripPlaced = true;
            try
            {
                var sp = PlayerManager.PickRandomSpawnPoint();
                if (sp != null) GameManager.GetPlayerManagerComponent().TeleportPlayer(sp.transform.position, sp.transform.rotation);
            }
            catch (Exception e) { MelonLogger.Warning("[бот] точка появления: " + e.Message); }
            Say("я в " + _tripScene + ", тебя здесь нет. Буду тут выбрасывать и поднимать вещи");
            return false;
        }

        // Совместный сон: бот сам в меню отдыха не ходит — отмечается готовым и пропускает время.
        private static bool _sleepReady;

        public static void OnSleepWait(string waiting)
        {
            if (waiting.Length == 0) { _sleepReady = false; return; }
            if (_sleepReady || !waiting.Split(',').Any(n => n.Trim() == Session.LocalName)) return;
            _sleepReady = true;
            SleepSync.BotReady(true, 1);
            Say("готов спать, жду остальных");
        }

        public static void OnSleepStart(int hours)
        {
            _sleepReady = false;
            try
            {
                GameManager.GetPassTime().Begin(hours, null);
                Say($"сплю вместе со всеми: {hours} ч");
            }
            catch (Exception e)
            {
                Say("не смог уснуть: " + e.Message);
            }
        }

        // Отойти далеко (проверка животных вдали от хоста на той же карте): стоим там 5 минут или до «бот назад».
        private static float _farUntil;

        private static void GoFar()
        {
            if (!Session.Players.TryGetValue(0, out var host) || host.Scene != GameApi.Scene()) return;
            var rnd = new System.Random();
            for (int i = 0; i < 40; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, d = 500 + rnd.NextDouble() * 200;
                var p = new Vector3(host.X + (float)(Math.Cos(a) * d), host.Y + 50f, host.Z + (float)(Math.Sin(a) * d));
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 100f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                GameApi.Teleport(hit.position.x, hit.position.y + 0.05f, hit.position.z);
                _farUntil = Time.unscaledTime + 300f;
                Say($"отошёл от тебя на {Vector3.Distance(hit.position, new Vector3(host.X, host.Y, host.Z)):0} м, стою тут 5 минут. {AnimalSync.Status}");
                return;
            }
            Say("не нашёл, куда отойти далеко");
        }

        private static void GoAway()
        {
            if (_awayDone) return;
            _awayDone = true;
            if (!Session.Players.TryGetValue(0, out var host) || host.Scene != GameApi.Scene()) return;
            bool outdoors = false;
            try { outdoors = GameManager.IsOutDoorsScene(GameManager.m_ActiveScene); } catch { }
            if (!outdoors) return;
            var rnd = new System.Random();
            for (int i = 0; i < 30; i++)
            {
                double a = rnd.NextDouble() * Math.PI * 2, d = 120 + rnd.NextDouble() * 60;
                var p = new Vector3(host.X + (float)(Math.Cos(a) * d), host.Y + 50f, host.Z + (float)(Math.Sin(a) * d));
                if (!UnityEngine.AI.NavMesh.SamplePosition(p, out var hit, 80f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                GameApi.Teleport(hit.position.x, hit.position.y + 0.05f, hit.position.z);
                Say($"отошёл от тебя на {Vector3.Distance(hit.position, new Vector3(host.X, host.Y, host.Z)):0} м, проверяю вещи вокруг себя");
                return;
            }
        }

        private static void FollowHost(bool always = false)
        {
            if (!Session.Players.TryGetValue(0, out var host) || host.Scene != GameApi.Scene()) return;
            var me = GameManager.GetPlayerTransform().position;
            float dx = host.X - me.x, dz = host.Z - me.z;
            if (!always && dx * dx + dz * dz < 64f) return;
            // Встаём в 2 м сбоку от хоста, на землю (луч сверху вниз). Если там обрыв, встаём прямо на место хоста:
            // иначе бот падает и разбивается.
            float a = (float)(host.Yaw * Math.PI / 180.0);
            float x = host.X + (float)Math.Cos(a) * 2f, z = host.Z - (float)Math.Sin(a) * 2f, y = host.Y;
            if (Physics.Raycast(new Vector3(x, host.Y + 2f, z), Vector3.down, out var hit, 4f) && Math.Abs(hit.point.y - host.Y) < 1.5f) y = hit.point.y + 0.05f;
            else { x = host.X; z = host.Z; }
            GameApi.Teleport(x, y, z);
        }

        private static void DropSomething()
        {
            var inv = GameManager.GetInventoryComponent();
            var pm = GameManager.GetPlayerManagerComponent();
            if (inv == null || pm == null) return;
            var choices = new List<GearItem>();
            foreach (var o in inv.m_Items)
            {
                GearItem gi = o?.m_GearItem;
                if (gi == null || gi.m_CantDropItem) continue;
                if (gi.GetComponent<ClothingItem>() != null) continue;
                if (gi.GetComponent<WaterSupply>() != null || gi.GetComponent<LiquidItem>() != null) continue;
                if (pm.m_ItemInHands != null && pm.m_ItemInHands.Pointer == gi.Pointer) continue;
                choices.Add(gi);
            }
            if (choices.Count == 0) { Say("нечего выбросить"); return; }
            var pick = choices[UnityEngine.Random.Range(0, choices.Count)];
            string name = Pretty(pick);
            var dropped = pick.Drop(1, false, true, false);
            Say(dropped != null ? "выбросил " + name : "не смог выбросить " + name);
        }

        private static void PickNearest()
        {
            var me = GameManager.GetPlayerTransform().position;
            GearItem best = null;
            float bestD = 3.5f * 3.5f;
            foreach (var gi in UnityEngine.Object.FindObjectsOfType<GearItem>())
            {
                if (gi == null || gi.m_InPlayerInventory || gi.m_InsideContainer || !gi.gameObject.activeInHierarchy) continue;
                var p = gi.transform.position;
                float d = (p - me).sqrMagnitude;
                if (d < bestD) { bestD = d; best = gi; }
            }
            if (best == null) { Say("рядом ничего не лежит"); return; }
            string name = Pretty(best);
            // Предмет из стопки может слиться с такой же стопкой в инвентаре, тогда сам он исчезает.
            var added = GameManager.GetPlayerManagerComponent().AddItemToPlayerInventory(best, true, false);
            Say(added != null ? "поднял " + name : "не смог поднять " + name);
        }

        private static string Pretty(GearItem gi)
        {
            try { return gi.DisplayName ?? ItemSync.PrefabName(gi); } catch { return ItemSync.PrefabName(gi); }
        }

        // Как у бота применилось изменение мира от другого игрока. Костры и растения меняются часто,
        // о них пишем только в лог, чтобы не засорять чат.
        public static void Report(bool toChat, string text)
        {
            if (toChat) Say(text);
            else MelonLogger.Msg("[бот] " + text);
        }

        private static void Say(string text)
        {
            MelonLogger.Msg("[бот] " + text);
            Session.SendChat(text);
        }
    }
}
