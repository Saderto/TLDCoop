using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using CoopByEMPEROR.Net;
using UnityEngine;
using KV = System.Collections.Generic.KeyValuePair<string, string>;

namespace CoopByEMPEROR.Game
{
    // Прогресс друзей хранит хост. Друг играет на своей копии мира хоста, и каждый раз, когда
    // его игра сохраняется, мод берёт из его сохранения всё, что относится к самому игроку
    // (вещи, здоровье, потребности, болезни, навыки, записки, чертежи, карта, место),
    // и отправляет хосту. Хост кладёт это в UserData/CoopByEMPEROR/progress/<сохранение хоста>/<друг>.json.
    // Когда друг подключается снова, хост вставляет его данные в копию мира, которую отправляет,
    // и друг появляется там, где вышел, со своими вещами.
    //
    // Там же хост хранит события в локациях, где сейчас никого нет (pending.bin): иначе они
    // пропали бы после выхода из игры.
    public static class PlayerProgress
    {
        // Ключи «global» в сохранении, которые относятся к игроку, а не к миру.
        private static readonly HashSet<string> PlayerKeys = new()
        {
            "m_Condition_Serialized", "m_Encumber_Serialized", "m_Hunger_Serialized", "m_Thirst_Serialized",
            "m_Fatigue_Serialized", "m_Freezing_Serialized", "m_Willpower_Serialized", "m_Inventory_Serialized",
            "m_PlayerManagerSerialized", "m_PlayerClimbRopeSerialized", "m_PlayerSkillsSerialized",
            "m_PlayerGameStatsSerialized", "m_HypothermiaSerialized", "m_WellFedSerialized", "m_FrostbiteSerialized",
            "m_FoodPoisoningSerialized", "m_DysenterySerialized", "m_SprainPainSerialized", "m_SprainedAnkleSerialized",
            "m_SprainedWristSerialized", "m_SprainsSerialized", "m_BurnsSerialized", "m_BurnsElectricSerialized",
            "m_BloodLossSerialized", "m_BrokenRibSerialized", "m_InfectionSerialized", "m_InfectionRiskSerialized",
            "m_LogSerialized", "m_RestSerialized", "m_PlayerMovementSerialized", "m_PanelStatsSerialized",
            "m_EmergencyStimSerialized", "m_EnergyBoostSerialized", "m_HeadacheSerialized", "m_CabinFeverSerialized",
            "m_IntestinalParasitesSerialized", "m_PlayerAnimationSerialized", "m_SkillsManagerSerialized",
            "m_MapDataSerialized", "m_KnowledgeManagerSerialized", "m_UnlockedBlueprintsSerialized",
            "m_CollectionManagerSerialized", "m_BigCarrySystemSerialized", "m_FearSerialized", "m_AnxietySerialized",
            "m_SuffocatingSerialized", "m_NotificationFlagSerialized", "m_InsomniaSerialized", "m_RecipeBookState",
            "m_FoodMaxStatBuffEffects", "m_ChemicalPoisoningSerialized", "m_ScurvySerialized",
            "m_PlayerNutritionSerialized", "m_LifeAfterDeathSerialized", "m_CheatDeathAfflictionSerialized",
            "m_DiminishedStateAfflictionSerialized", "m_SevereLacerationSerialized", "m_TempInventoryPlacementListSerialized",
            // Задания (Tales from the Far Territory и другие) у каждого свои: ход заданий, цели, вышки-передатчики.
            "m_SandboxManagerSerialized", "m_StoryMissionDataSerialized", "m_MissionObjectiveTableSerialized", "m_TransmitterSerialized", "m_TrackableItemSerialized",
            // Выбранные подвиги (значки) у каждого свои (Game/FeatsChoice.cs).
            "m_FeatsEnabledSerialized",
        };

        // Личное состояние модов игрока в копии мира для друга: своя запись в m_Dict рядом с "global".
        // Сейчас это эффекты еды FoodPackByEMPEROR: мод хранит их в файле .moddata слота, а слот у друга
        // при каждом входе новый (Game/WorldTransfer.cs), поэтому они едут вместе с прогрессом.
        internal const string ModsKey = "tldcoop_mods";
        private const string FoodMod = "FoodPackBySeagull";
        private const string ModDataPrefix = "ModData:";   // + имя записи ModData (Game/ModDataLink.cs)

        // Где игрок находится: поля m_SceneTransitionDataSerialized внутри m_GameManagerSerialized.
        private static readonly string[] SceneKeys = { "m_SceneSaveFilenameCurrent", "m_SceneSaveFilenameNextLoad", "m_LastOutdoorScene" };

        private static DateTime _lastFileTime;
        private static string _watchedSave = "";
        private static float _nextWatch, _saveSeen;
        private static string _hostLoadedFor = "";

        // ---------- Чтение и запись сохранения ----------

        private static string Decode(string rawBase64String) =>
            Encoding.UTF8.GetString(Lzf.Decompress(Convert.FromBase64String(SaveJson.Unquote(rawBase64String))));

        private static string Encode(string json) =>
            SaveJson.Quote(Convert.ToBase64String(Lzf.StoreOnly(Encoding.UTF8.GetBytes(json))));

        // Из файла сохранения друга: {"boot":…, "scene":{…}, "global":{…}}.
        public static string Extract(byte[] file)
        {
            var outer = SaveJson.Split(Encoding.UTF8.GetString(Lzf.Decompress(file)));
            var dict = SaveJson.Split(SaveJson.Get(outer, "m_Dict"));
            string boot = Decode(SaveJson.Get(dict, "boot"));
            var global = SaveJson.Split(Decode(SaveJson.Get(dict, "global")));

            var mine = global.Where(p => PlayerKeys.Contains(p.Key)).ToList();
            var gm = SaveJson.Split(SaveJson.Unquote(SaveJson.Get(global, "m_GameManagerSerialized")));
            var tr = SaveJson.Split(SaveJson.Unquote(SaveJson.Get(gm, "m_SceneTransitionDataSerialized")));
            var scene = tr.Where(p => SceneKeys.Contains(p.Key)).ToList();

            return SaveJson.Join(new List<KV>
            {
                new("boot", SaveJson.Quote(boot)),
                new("scene", SaveJson.Join(scene)),
                new("global", SaveJson.Join(mine)),
            });
        }

        // Хост: вставить прогресс друга в копию своего мира.
        public static byte[] Patch(byte[] file, string progress)
        {
            var p = SaveJson.Split(progress);
            var outer = SaveJson.Split(Encoding.UTF8.GetString(Lzf.Decompress(file)));
            var dict = SaveJson.Split(SaveJson.Get(outer, "m_Dict"));
            var global = SaveJson.Split(Decode(SaveJson.Get(dict, "global")));

            foreach (var kv in SaveJson.Split(SaveJson.Get(p, "global"))) SaveJson.Set(global, kv.Key, kv.Value);

            var gm = SaveJson.Split(SaveJson.Unquote(SaveJson.Get(global, "m_GameManagerSerialized")));
            var tr = SaveJson.Split(SaveJson.Unquote(SaveJson.Get(gm, "m_SceneTransitionDataSerialized")));
            foreach (var kv in SaveJson.Split(SaveJson.Get(p, "scene"))) SaveJson.Set(tr, kv.Key, kv.Value);
            SaveJson.Set(gm, "m_SceneTransitionDataSerialized", SaveJson.Quote(SaveJson.Join(tr)));
            SaveJson.Set(global, "m_GameManagerSerialized", SaveJson.Quote(SaveJson.Join(gm)));

            SaveJson.Set(dict, "global", Encode(SaveJson.Join(global)));
            SaveJson.Set(dict, "boot", Encode(SaveJson.Unquote(SaveJson.Get(p, "boot"))));
            // Эффекты модов друга (нет в прогрессе — убираем запись, если в мире хоста она откуда-то есть).
            dict.RemoveAll(kv => kv.Key == ModsKey);
            // Своих подвигов друга в прогрессе нет (прогресс до 0.19.49): пусть выберет их перед входом.
            dict.RemoveAll(kv => kv.Key == FeatsChoice.AskKey);
            if (!SaveJson.Split(SaveJson.Get(p, "global")).Any(kv => kv.Key == "m_FeatsEnabledSerialized"))
                SaveJson.Set(dict, FeatsChoice.AskKey, Encode("1"));
            string mods = SaveJson.Get(p, "mods");
            if (!string.IsNullOrEmpty(mods)) SaveJson.Set(dict, ModsKey, Encode(mods));
            SaveJson.Set(outer, "m_Dict", SaveJson.Join(dict));
            return Lzf.StoreOnly(Encoding.UTF8.GetBytes(SaveJson.Join(outer)));
        }

        // Друг: добавить к прогрессу личное состояние модов (эффекты еды). Мода нет — прогресс как был.
        private static string WithMods(string progress)
        {
            var mods = new List<KV>();
            string food = FoodPackLink.ExportPlayerState();
            if (food != null) mods.Add(new(FoodMod, SaveJson.Quote(food)));
            // Личные записи других модов в ModData (алкоголь, свои болезни).
            var md = ModDataLink.Export();
            if (md != null) foreach (var kv in md) mods.Add(new(ModDataPrefix + kv.Key, SaveJson.Quote(kv.Value)));
            if (mods.Count == 0) return progress;
            var p = SaveJson.Split(progress);
            SaveJson.Set(p, "mods", SaveJson.Join(mods));
            return SaveJson.Join(p);
        }

        // Друг: личные записи модов в ModData из присланной копии мира (нет — пусто: игрок без них).
        public static Dictionary<string, string> ModDataFromCopy(string json)
        {
            var d = new Dictionary<string, string>();
            try
            {
                var outer = SaveJson.Split(json);
                var dict = SaveJson.Split(SaveJson.Get(outer, "m_Dict"));
                string raw = SaveJson.Get(dict, ModsKey);
                if (string.IsNullOrEmpty(raw)) return d;
                foreach (var kv in SaveJson.Split(Decode(raw)))
                {
                    if (!kv.Key.StartsWith(ModDataPrefix, StringComparison.Ordinal)) continue;
                    string name = kv.Key.Substring(ModDataPrefix.Length);
                    if (Array.IndexOf(ModDataLink.Personal, name) >= 0 && !string.IsNullOrEmpty(kv.Value)) d[name] = SaveJson.Unquote(kv.Value);
                }
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] записи модов из копии мира: " + e.Message); }
            return d;
        }

        // Друг: в копии мира нет своих подвигов (хост поставил метку) — выбрать перед входом.
        public static bool AsksFeats(string json)
        {
            try
            {
                var dict = SaveJson.Split(SaveJson.Get(SaveJson.Split(json), "m_Dict"));
                return !string.IsNullOrEmpty(SaveJson.Get(dict, FeatsChoice.AskKey));
            }
            catch { return false; }
        }

        // Друг: эффекты еды из присланной копии мира. "" — их нет (новый друг или прогресс без них): игрок чистый.
        public static string FoodStateFromCopy(string json)
        {
            try
            {
                var outer = SaveJson.Split(json);
                var dict = SaveJson.Split(SaveJson.Get(outer, "m_Dict"));
                string raw = SaveJson.Get(dict, ModsKey);
                if (string.IsNullOrEmpty(raw)) return "";
                string food = SaveJson.Get(SaveJson.Split(Decode(raw)), FoodMod);
                return string.IsNullOrEmpty(food) ? "" : SaveJson.Unquote(food);
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[прогресс] эффекты еды из копии мира: " + e.Message);
                return "";
            }
        }

        // Что у нового друга должно быть своим с нуля, а не взятым у хоста: журнал, карта, изученное, чертежи,
        // найденные записки и коллекции, рецепты, статистика и ход заданий «Сказаний». Вещи, навыки и болезни
        // друг сбрасывает сам после загрузки (WorldTransfer.FreshSurvivor). Раздела нет — игра начинает его заново.
        private static readonly string[] FreshKeys =
        {
            "m_LogSerialized", "m_MapDataSerialized", "m_KnowledgeManagerSerialized", "m_UnlockedBlueprintsSerialized",
            "m_CollectionManagerSerialized", "m_RecipeBookState", "m_PlayerGameStatsSerialized", "m_PanelStatsSerialized",
            "m_MissionObjectiveTableSerialized", "m_TransmitterSerialized", "m_TrackableItemSerialized",
            "m_BigCarrySystemSerialized",   // что хост тащил в момент сохранения
        };

        // Значения m_Dict игра читает как байты (в JSON — base64). Метка подвигов была строкой «1», и игра друга не могла
        // разобрать копию мира: SaveGameSlots.CreateSlotFromData → FormatException, «игра не видит новый слот» (друзья
        // 11-10, 0.19.53). Теперь метка записана как остальные значения (Encode), 0.19.54.
        // Хост: первый вход друга в этот мир — убираем из копии мира личное хоста.
        public static byte[] StripForNewcomer(byte[] file, out int removed)
        {
            var outer = SaveJson.Split(Encoding.UTF8.GetString(Lzf.Decompress(file)));
            var dict = SaveJson.Split(SaveJson.Get(outer, "m_Dict"));
            var global = SaveJson.Split(Decode(SaveJson.Get(dict, "global")));
            removed = global.RemoveAll(kv => FreshKeys.Contains(kv.Key));
            SaveJson.Set(dict, "global", Encode(SaveJson.Join(global)));
            removed += dict.RemoveAll(kv => kv.Key == ModsKey);   // эффекты еды прошлого друга, если мир был копией
            dict.RemoveAll(kv => kv.Key == FeatsChoice.AskKey);
            SaveJson.Set(dict, FeatsChoice.AskKey, Encode("1"));   // подвиги хоста не берём: друг выберет свои
            SaveJson.Set(outer, "m_Dict", SaveJson.Join(dict));
            return Lzf.StoreOnly(Encoding.UTF8.GetBytes(SaveJson.Join(outer)));
        }

        // ---------- Где хост хранит прогресс ----------

        // Мир определяется именем слота, номером игры и зерном мира (m_GameRandomSeed): новая игра
        // в том же слоте (sandbox3 удалили и начали заново) получает то же имя и номер, но другое зерно,
        // и не должна подхватить прогресс друзей и раскладки от прошлого мира.
        private static string _lastWorldKey;

        // Хост: pending.bin этого мира уже прочитан (до этого отложенных состояний ещё нет).
        public static bool HostPendingLoaded => Session.Mode != Mode.Host || _hostLoadedFor == (WorldKey() ?? "");

        private static string WorldKey()
        {
            // Хост уже в главном меню, а прогресс друга ещё идёт: кладём его в мир, из которого хост вышел.
            if (!GameApi.InGame()) return _lastWorldKey;
            return _lastWorldKey = CurrentWorldKey();
        }

        // Номер мира из слота сохранения (Game/SlotStore.cs); у мира без него — как раньше.
        private static string CurrentWorldKey()
        {
            string name = SaveGameSystem.GetCurrentSaveName();
            if (string.IsNullOrEmpty(name)) return null;
            return SlotStore.WorldId(name) ?? LegacyWorldKey();
        }

        internal static string LegacyWorldKey()
        {
            string name = SaveGameSystem.GetCurrentSaveName();
            if (string.IsNullOrEmpty(name)) return null;
            uint id = 0;
            try { id = SaveGameSlots.GetSaveSlotFromName(name)?.m_GameId ?? 0; } catch { }
            int seed = 0;
            try { seed = GameManager.m_SceneTransitionData?.m_GameRandomSeed ?? 0; } catch { }
            return name + "_" + id + (seed != 0 ? "_" + seed : "");
        }

        // Путь к папке мира без создания (для чтения: у одиночных сохранений папки нет и не нужно).
        internal static string WorldFolderPath()
        {
            string key = WorldKey();
            return key == null ? null : Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "progress", key);
        }

        internal static string WorldFolder()
        {
            string key = WorldKey();
            if (key == null) return null;
            string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "progress", key);
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string KeyOf(Peer peer, string name)
        {
            if (peer is SteamPeer sp) return "steam_" + sp.SteamId;
            var safe = new string((name ?? "").Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray());
            return "name_" + (safe.Length > 0 ? safe : "player");
        }

        public static string Load(string key)
        {
            try
            {
                string dir = WorldFolder();
                string f = dir == null ? null : Path.Combine(dir, key + ".json");
                return f != null && File.Exists(f) ? File.ReadAllText(f, Encoding.UTF8) : null;
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[прогресс] чтение: " + e.Message);
                return null;
            }
        }

        // Хост: пришёл прогресс друга (старый вид — одной строкой).
        public static void Receive(string key, string name, BinaryReader r) => Store(key, name, r.ReadString());

        // Прогресс — JSON всех разделов игрока, в долгой игре он большой: друг сжимает его и шлёт кусками по 200 КБ
        // (сообщение больше 512 КБ Steam не берёт, по IP больше 1 МБ рвёт связь). Хост пишет файл, когда пришли все куски.
        private const int PartSize = 200 * 1024;
        private static readonly Dictionary<string, (int id, byte[][] parts, int got)> Parts = new();

        public static void ReceivePart(string key, string name, BinaryReader r)
        {
            int id = r.ReadInt32(), index = r.ReadInt32(), total = r.ReadInt32();
            var bytes = r.ReadBytes(r.ReadInt32());
            if (total <= 0 || total > 200 || index < 0 || index >= total) return;
            if (!Parts.TryGetValue(key, out var p) || p.id != id || p.parts.Length != total) p = (id, new byte[total][], 0);
            if (p.parts[index] == null) { p.parts[index] = bytes; p.got++; }
            Parts[key] = p;
            if (p.got < total) return;
            Parts.Remove(key);
            try
            {
                using var ms = new MemoryStream();
                foreach (var part in p.parts) ms.Write(part, 0, part.Length);
                ms.Position = 0;
                using var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var sr = new StreamReader(z, Encoding.UTF8);
                Store(key, name, sr.ReadToEnd());
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] сборка кусков: " + e.Message); }
        }

        // Хост: друг выбросил, поднял, подарил или обыскал — свежий рюкзак отдельно от полного прогресса.
        // Если игра друга упадёт до сохранения, при следующем входе вещь не окажется и на земле, и в рюкзаке.
        public static void ReceiveInventory(string key, string name, BinaryReader r)
        {
            string inv = r.ReadString();
            try
            {
                string dir = WorldFolder();
                string f = dir == null ? null : Path.Combine(dir, key + ".json");
                if (f == null || !File.Exists(f)) return;   // полного прогресса ещё нет — придёт с первым сохранением
                var p = SaveJson.Split(File.ReadAllText(f, Encoding.UTF8));
                var global = SaveJson.Split(SaveJson.Get(p, "global"));
                SaveJson.Set(global, "m_Inventory_Serialized", inv);
                SaveJson.Set(p, "global", SaveJson.Join(global));
                string tmp = f + ".tmp";
                File.WriteAllText(tmp, SaveJson.Join(p), Encoding.UTF8);
                File.Copy(tmp, f, true);
                File.Delete(tmp);
            }
            catch (Exception e) { MelonLogger.Warning($"[прогресс] рюкзак {name}: " + e.Message); }
        }

        // ---------- Своя копия прогресса (0.19.51) ----------
        // Прогресс друга хранит только тот хост, которому друг его слал. Хостом в том же мире может стать другой
        // игрок (взял свою копию мира хоста) — у него прогресса друга нет, и друг заходил новичком: стартовый набор,
        // подобранные вещи (штормовой фонарь) снова лежат на месте, перчатки и прочее пропали (11-10, друг
        // у GNOM). Теперь друг держит копию того же, что шлёт хосту: UserData/CoopByEMPEROR/own/<мир>.json. Хост в
        // приветствии называет мир; друг, у которого есть копия этого мира, шлёт её до запроса мира. Хост берёт её,
        // если своей нет или своя старше (по времени записи файла).

        internal static string HostWorldKey() => Session.Mode == Mode.Host ? WorldKey() : null;

        private static string OwnPath(string world)
        {
            var safe = new string(world.Select(c => char.IsLetterOrDigit(c) || c == '_' || c == '-' ? c : '_').ToArray());
            return Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "own", safe + ".json");
        }

        private static void KeepOwn(string json)
        {
            try
            {
                string world = CurrentWorldKey();
                if (string.IsNullOrEmpty(world) || string.IsNullOrEmpty(json)) return;
                string f = OwnPath(world);
                Directory.CreateDirectory(Path.GetDirectoryName(f));
                string tmp = f + ".tmp";
                File.WriteAllText(tmp, json, Encoding.UTF8);
                File.Copy(tmp, f, true);
                File.Delete(tmp);
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] своя копия: " + e.Message); }
        }

        public static void OfferOwn(string world, Action<byte[]> send)
        {
            try
            {
                string f = OwnPath(world);
                if (!File.Exists(f)) { MelonLogger.Msg($"[прогресс] своей копии прогресса в мире {world} нет"); return; }
                long stamp = File.GetLastWriteTimeUtc(f).Ticks;
                string json = File.ReadAllText(f, Encoding.UTF8);
                byte[] packed;
                using (var ms = new MemoryStream())
                {
                    using (var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                    {
                        var raw = Encoding.UTF8.GetBytes(json);
                        z.Write(raw, 0, raw.Length);
                    }
                    packed = ms.ToArray();
                }
                int id = new System.Random().Next(), total = (packed.Length + PartSize - 1) / PartSize;
                for (int i = 0; i < total; i++)
                {
                    int off = i * PartSize, len = Math.Min(PartSize, packed.Length - off);
                    var w = Msg.New(MsgType.OwnProgress);
                    w.Write(world); w.Write(stamp);
                    w.Write(id); w.Write(i); w.Write(total); w.Write(len); w.Write(packed, off, len);
                    send(Msg.Done(w));
                }
                MelonLogger.Msg($"[прогресс] отправил хосту свою копию прогресса (мир {world}, сохранена {new DateTime(stamp, DateTimeKind.Utc):yyyy-MM-dd HH:mm} UTC, {json.Length / 1024} КБ)");
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] своя копия хосту: " + e.Message); }
        }

        private static readonly Dictionary<string, (int id, byte[][] parts, int got)> OwnParts = new();

        public static void ReceiveOwn(string key, string name, BinaryReader r)
        {
            string world = r.ReadString();
            long stamp = r.ReadInt64();
            int id = r.ReadInt32(), index = r.ReadInt32(), total = r.ReadInt32();
            var bytes = r.ReadBytes(r.ReadInt32());
            if (total <= 0 || total > 200 || index < 0 || index >= total) return;
            if (!OwnParts.TryGetValue(key, out var p) || p.id != id || p.parts.Length != total) p = (id, new byte[total][], 0);
            if (p.parts[index] == null) { p.parts[index] = bytes; p.got++; }
            OwnParts[key] = p;
            if (p.got < total) return;
            OwnParts.Remove(key);
            try
            {
                string mine = WorldKey() ?? "";
                if (world != mine) { MelonLogger.Msg($"[прогресс] {name} принёс прогресс другого мира ({world}, у меня {mine}) — не беру"); return; }
                string dir = WorldFolder();
                if (dir == null) return;
                string f = Path.Combine(dir, key + ".json");
                if (File.Exists(f) && File.GetLastWriteTimeUtc(f).Ticks >= stamp)
                {
                    MelonLogger.Msg($"[прогресс] {name} принёс свою копию прогресса, у меня свежее — оставляю своё");
                    return;
                }
                using var ms = new MemoryStream();
                foreach (var part in p.parts) ms.Write(part, 0, part.Length);
                ms.Position = 0;
                using var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var sr = new StreamReader(z, Encoding.UTF8);
                string json = sr.ReadToEnd();
                if (json.Length == 0) return;
                bool had = File.Exists(f);
                Store(key, name, json);
                MelonLogger.Msg($"[прогресс] взял прогресс {name} из его копии ({(had ? "моя была старше" : "у меня его не было — сменился хост мира")})");
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] своя копия друга: " + e.Message); }
        }

        private static void Store(string key, string name, string json)
        {
            try
            {
                string dir = WorldFolder();
                if (dir == null) return;
                File.WriteAllText(Path.Combine(dir, key + ".json"), json, Encoding.UTF8);
                MelonLogger.Msg($"[прогресс] сохранил прогресс {name} ({json.Length / 1024} КБ)");
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[прогресс] запись: " + e.Message);
            }
        }

        // ---------- Каждый кадр ----------

        private static float _leftAt = -1f;

        public static void Tick(float now)
        {
            if (Session.Mode == Mode.Off || !GameApi.InGame() || !Session.InSharedWorld)
            {
                if (_watchedSave.Length > 0 && _leftAt < 0f)
                {
                    _leftAt = now;
                    // Хост вышел из мира: события в пустых локациях — на диск сразу, а не через минуту.
                    if (Session.Mode == Mode.Host) { try { SavePending(); } catch (Exception e) { MelonLogger.Warning("[прогресс] запись событий при выходе: " + e.Message); } }
                }
                // Друг вышел в меню: игра сохраняется при выходе — это сохранение тоже уходит хосту (ловим его ещё 20 с).
                if (Session.Mode == Mode.Client && _leftAt >= 0f && now - _leftAt < 20f && _watchedSave.Length > 0) { WatchSave(now); return; }
                _watchedSave = "";
                _leftAt = -1f;
                return;
            }
            _leftAt = -1f;
            if (Session.Mode == Mode.Host) { HostLoadPending(); TickPending(now); }
            if (Session.Mode == Mode.Client) { TickAutoSave(now); TickInventory(now); }
            WatchSave(now);
        }

        private static void WatchSave(float now)
        {
            if (now < _nextWatch) return;
            _nextWatch = now + 3f;

            string path = WorldTransfer.CurrentSlotFile();
            if (path == null)
            {
                if (!_noSlotLogged) { _noSlotLogged = true; MelonLogger.Warning("[прогресс] не нашёл файл своего сохранения, прогресс не отправится"); }
                return;
            }
            DateTime t;
            try { t = File.GetLastWriteTimeUtc(path); } catch { return; }
            if (path != _watchedSave) { _watchedSave = path; _lastFileTime = t; return; }
            if (t <= _lastFileTime) return;
            // Игра сохранилась. Ждём, пока файл допишется.
            if (WorldTransfer.SafeAsyncRunning()) return;
            if (_saveSeen == 0f) { _saveSeen = now; return; }
            if (now - _saveSeen < 2f) return;
            _saveSeen = 0f;
            _lastFileTime = t;
            OnOwnSave(path);
        }

        // Друг сохраняется сам раз в 5 минут, чтобы при вылете игры хоста прогресс пропал не больше чем за 5 минут.
        private static float _lastOwnSave = -1f;

        private static void TickAutoSave(float now)
        {
            if (_lastOwnSave < 0f) { _lastOwnSave = now; return; }
            if (now - _lastOwnSave < 300f) return;
            if (Death.IsDowned || ActionTime.Active || SleepSync.LocalResting || Activity.Current() != 0) return;
            _lastOwnSave = now;
            MelonLogger.Msg("[прогресс] 5 минут без сохранения — сохраняюсь, чтобы прогресс ушёл хосту");
            OnSaveRequest();
        }

        // Друг: рюкзак изменился (свой выброс, подбор, подарок, ящик) — через секунду шлём его хосту.
        private static float _invDirtyAt = -1f, _lastInvSend = -100f;

        public static void InventoryChanged()
        {
            if (Session.Mode == Mode.Client && _invDirtyAt < 0f) _invDirtyAt = Time.unscaledTime;
        }

        private static void TickInventory(float now)
        {
            if (_invDirtyAt < 0f || now - _invDirtyAt < 1f || now - _lastInvSend < 3f) return;
            _invDirtyAt = -1f;
            _lastInvSend = now;
            try
            {
                var inv = GameManager.GetInventoryComponent();
                if (inv == null) return;
                var w = Msg.New(MsgType.InvProgress);
                w.Write(SaveJson.Quote(inv.Serialize() ?? ""));
                Session.SendGameEvent(Msg.Done(w));
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] рюкзак: " + e.Message); }
        }

        private static float _lastSaveAsk = -1000f;
        private static bool _noSlotLogged;
        // Друг: когда свой прогресс в последний раз ушёл хосту.
        public static float ProgressSentAt { get; private set; } = -1f;

        private static void OnOwnSave(string path)
        {
            try
            {
                if (Session.Mode == Mode.Client)
                {
                    _lastOwnSave = Time.unscaledTime;
                    string json = Extract(File.ReadAllBytes(path));
                    // Прогресс уходит через 3–5 с после сохранения, а рюкзак за это время мог измениться (подобрал,
                    // выбросил): кладём живой рюкзак, иначе у хоста свежий затёрся бы рюкзаком на момент сохранения.
                    bool liveInv = WithLiveInventory(ref json);
                    json = WithMods(json);
                    byte[] packed;
                    using (var ms = new MemoryStream())
                    {
                        using (var z = new System.IO.Compression.DeflateStream(ms, System.IO.Compression.CompressionLevel.Optimal, true))
                        {
                            var raw = Encoding.UTF8.GetBytes(json);
                            z.Write(raw, 0, raw.Length);
                        }
                        packed = ms.ToArray();
                    }
                    int id = new System.Random().Next(), total = (packed.Length + PartSize - 1) / PartSize;
                    for (int i = 0; i < total; i++)
                    {
                        int off = i * PartSize, len = Math.Min(PartSize, packed.Length - off);
                        var w = Msg.New(MsgType.ProgressPart);
                        w.Write(id); w.Write(i); w.Write(total); w.Write(len); w.Write(packed, off, len);
                        Session.SendGameEvent(Msg.Done(w));
                    }
                    KeepOwn(json);
                    if (liveInv) _invDirtyAt = -1f;   // живой рюкзак уже в полном прогрессе
                    ProgressSentAt = Time.unscaledTime;
                    MelonLogger.Msg($"[прогресс] отправил свой прогресс хосту ({json.Length / 1024} КБ, сжато {packed.Length / 1024} КБ, частей {total})");
                }
                else
                {
                    var file = File.ReadAllBytes(path);
                    SceneSnapshot.RefreshSavedScenes(file);
                    SceneHandoff.HostSaved(File.GetLastWriteTimeUtc(path), file);
                    AppliedUnsaved.Clear();   // игра записала локации с применёнными событиями — повторять их не нужно
                    _appliedDirtyAt = -1f;
                    SavePending();
                    // Хост сохранился (сон, переход, выход): друзья сохраняются вместе с ним.
                    float now = Time.unscaledTime;
                    if (now - _lastSaveAsk > 60f)
                    {
                        _lastSaveAsk = now;
                        Session.SendGameEvent(Msg.Done(Msg.New(MsgType.SaveRequest)));
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[прогресс] " + e.Message);
            }
        }

        // Друг: m_Inventory_Serialized в извлечённом прогрессе — живой рюкзак (только в игре, в меню его нет).
        private static bool WithLiveInventory(ref string json)
        {
            if (!GameApi.InGame() || !Session.InSharedWorld) return false;
            try
            {
                var inv = GameManager.GetInventoryComponent();
                string live = inv != null ? inv.Serialize() : null;
                if (string.IsNullOrEmpty(live)) return false;
                var p = SaveJson.Split(json);
                var global = SaveJson.Split(SaveJson.Get(p, "global"));
                SaveJson.Set(global, "m_Inventory_Serialized", SaveJson.Quote(live));
                SaveJson.Set(p, "global", SaveJson.Join(global));
                json = SaveJson.Join(p);
                return true;
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] живой рюкзак: " + e.Message); return false; }
        }

        // ---------- Кнопка хоста «Сохранить» ----------

        public static void HostSaveAll()
        {
            if (Session.Mode != Mode.Host || !GameApi.InGame()) return;
            try { GameManager.TriggerSurvivalSaveAndDisplayHUDMessage(); } catch (Exception e) { MelonLogger.Warning("[прогресс] " + e.Message); }
            Session.SendGameEvent(Msg.Done(Msg.New(MsgType.SaveRequest)));
            Session.Log("Хост сохранил игру. Прогресс друзей сохраняется вместе с ней.");
        }

        // Друг: хост попросил сохраниться.
        public static void OnSaveRequest()
        {
            if (!GameApi.InGame() || !Session.InHostWorld) return;
            try { GameManager.TriggerSurvivalSaveAndDisplayHUDMessage(); return; }
            catch (Exception e) { MelonLogger.Warning("[прогресс] обычное сохранение не вышло (" + e.Message.Split('\n')[0] + "), сохраняю напрямую"); }
            // У бота обычное сохранение падает внутри игры (GameManager.SaveShouldBePending), сохраняем без проверок.
            try { GameManager.ForceSaveGame(); } catch (Exception e) { MelonLogger.Warning("[прогресс] сохранение: " + e.Message.Split('\n')[0]); }
        }

        // ---------- Резервная копия мира ----------
        // Хост загрузил мир — копируем его сохранение и прогресс друзей (папку мира мода) в
        // UserData/CoopByEMPEROR/backups/<мир>/<дата_время>. Хранятся последние 5 копий.

        private static void Backup(string key)
        {
            if (string.IsNullOrEmpty(key)) return;
            try
            {
                string root = Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "backups", key);
                string dest = Path.Combine(root, DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(dest);
                string slot = WorldTransfer.CurrentSlotFile();
                if (slot != null && File.Exists(slot)) File.Copy(slot, Path.Combine(dest, Path.GetFileName(slot)), true);
                string world = WorldFolder();
                if (world != null && Directory.Exists(world))
                {
                    string pdest = Path.Combine(dest, "progress");
                    Directory.CreateDirectory(pdest);
                    foreach (var f in Directory.GetFiles(world)) File.Copy(f, Path.Combine(pdest, Path.GetFileName(f)), true);
                }
                var all = Directory.GetDirectories(root).OrderBy(d => d, StringComparer.Ordinal).ToList();
                for (int i = 0; i < all.Count - 5; i++) { try { Directory.Delete(all[i], true); } catch { } }
                MelonLogger.Msg("[прогресс] резервная копия мира: " + dest);
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] резервная копия: " + e.Message); }
        }

        // ---------- События в пустых локациях ----------

        // Хост: события в пустых локациях пишем на диск и раз в минуту, если они изменились, а не только при
        // сохранении игры. Иначе после вылета игры хоста «ящик пуст» пропадал, а вещи из него оставались у друга — дюп.
        private static float _nextPendingWrite;
        private static long _pendingSig = -1;

        private static void TickPending(float now)
        {
            // События друзей, применённые в нашей локации (NoteFriendApplied): на диск через 2 с, пачкой.
            if (_appliedDirtyAt >= 0f && now - _appliedDirtyAt >= 2f)
            {
                _appliedDirtyAt = -1f;
                try { SavePending(); } catch (Exception e) { MelonLogger.Warning("[прогресс] запись событий: " + e.Message); }
            }
            if (now < _nextPendingWrite) return;
            _nextPendingWrite = now + 60f;
            try
            {
                long sig = 17;
                foreach (var raw in ItemSync.AllPending().Concat(WorldObjects.AllPending()).Concat(WorldEvents.AllPending()))
                {
                    sig = sig * 31 + raw.Length;
                    for (int i = 0; i < raw.Length; i += 7) sig = sig * 31 + raw[i];
                }
                sig = sig * 31 + SceneSnapshot.StoredCount + RadialSync.StoredCount * 7919L;
                if (sig == _pendingSig) return;
                bool first = _pendingSig == -1;
                _pendingSig = sig;
                if (first) return;   // только что загрузили — писать нечего
                SavePending();
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] запись событий: " + e.Message); }
        }

        // Хост: события, применённые при входе в локацию, но ещё не записанные игрой в сохранение. Если игра упадёт
        // до сохранения, после загрузки они применятся снова; если сохранение уже новее записи — нет (иначе удвоятся).
        private static readonly List<byte[]> AppliedUnsaved = new();
        private const int AppliedMark = 0x41505031;

        public static void NoteApplied(IEnumerable<byte[]> raws)
        {
            if (Session.Mode != Mode.Host) return;
            int before = AppliedUnsaved.Count;
            AppliedUnsaved.AddRange(raws);
            if (AppliedUnsaved.Count == before) return;
            try { SavePending(); } catch (Exception e) { MelonLogger.Warning("[прогресс] запись событий: " + e.Message); }
        }

        // Хост: событие друга (выброс, подбор, ящик) применено в нашей текущей локации. Рюкзак друга у нас записан
        // сразу, а локация — только при следующем сохранении игры: при вылете до него вещь оказалась бы в двух местах.
        // Кладём к применённым, но не записанным — после вылета они применятся снова (та же проверка времени слота).
        private static float _appliedDirtyAt = -1f;

        public static void NoteFriendApplied(byte[] raw)
        {
            if (Session.Mode != Mode.Host || raw == null || raw.Length == 0) return;
            try
            {
                // У ящика нужно только последнее состояние (открытый ящик шлёт его каждые 2 с).
                if ((MsgType)raw[0] == MsgType.ObjectState)
                {
                    var h = WorldObjects.Header(raw);
                    AppliedUnsaved.RemoveAll(o => o.Length > 0 && (MsgType)o[0] == MsgType.ObjectState && WorldObjects.Header(o) == h);
                }
            }
            catch { }
            AppliedUnsaved.Add(raw);
            if (_appliedDirtyAt < 0f) _appliedDirtyAt = Time.unscaledTime;
        }

        // Хост: друг отключился — его последние события в пустых локациях на диск сразу.
        public static void OnPeerLeft()
        {
            if (Session.Mode != Mode.Host || !GameApi.InGame()) return;
            try { SavePending(); } catch (Exception e) { MelonLogger.Warning("[прогресс] запись событий: " + e.Message); }
        }

        private static long SlotTicks()
        {
            try { string slot = WorldTransfer.CurrentSlotFile(); return slot != null && File.Exists(slot) ? File.GetLastWriteTimeUtc(slot).Ticks : 0; }
            catch { return 0; }
        }

        private static void SavePending()
        {
            string dir = WorldFolder();
            if (dir == null) return;
            var all = ItemSync.AllPending().Concat(WorldObjects.AllPending()).Concat(WorldEvents.AllPending()).ToList();
            // Пишем во временный файл и подменяем: вылет посреди записи не испортит прежний.
            string f = Path.Combine(dir, "pending.bin"), tmp = f + ".tmp";
            using (var w = new BinaryWriter(File.Create(tmp)))
            {
                w.Write(all.Count);
                foreach (var raw in all) { w.Write(raw.Length); w.Write(raw); }
                SceneSnapshot.Write(w);
                RadialSync.Write(w);
                w.Write(AppliedMark);
                w.Write(SlotTicks());
                w.Write(AppliedUnsaved.Count);
                foreach (var raw in AppliedUnsaved) { w.Write(raw.Length); w.Write(raw); }
            }
            File.Copy(tmp, f, true);
            File.Delete(tmp);
            if (all.Count > 0) MelonLogger.Msg($"[прогресс] сохранено событий в пустых локациях: {all.Count}");
        }

        private static void HostLoadPending()
        {
            if ((WorldKey() ?? "") == _hostLoadedFor) return;
            HostLoadPendingFile();
            // Сохранения локаций от друзей, которые игра хоста не успела записать (Game/SceneHandoff.cs).
            try
            {
                string dir = WorldFolder();
                string pf = dir == null ? null : Path.Combine(dir, "pending.bin");
                SceneHandoff.HostLoaded(new DateTime(SlotTicks(), DateTimeKind.Utc), pf != null && File.Exists(pf) ? File.GetLastWriteTimeUtc(pf) : DateTime.MinValue);
            }
            catch (Exception e) { MelonLogger.Warning("[прогресс] сохранения локаций: " + e.Message); }
        }

        private static void HostLoadPendingFile()
        {
            string save = WorldKey() ?? "";
            _hostLoadedFor = save;
            MelonLogger.Msg("[прогресс] мир хоста: " + save);
            Backup(save);
            WorldReset.Run("мир хоста загружен");
            try
            {
                string slot = WorldTransfer.CurrentSlotFile();
                if (slot != null) SceneSnapshot.RefreshSavedScenes(File.ReadAllBytes(slot));
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[прогресс] сохранение: " + e.Message);
            }
            try
            {
                string dir = WorldFolder();
                string f = dir == null ? null : Path.Combine(dir, "pending.bin");
                if (f == null || !File.Exists(f)) return;
                using var r = new BinaryReader(File.OpenRead(f));
                int n = r.ReadInt32();
                for (int i = 0; i < n; i++) Feed(r.ReadBytes(r.ReadInt32()));
                if (r.BaseStream.Position < r.BaseStream.Length) SceneSnapshot.Read(r);
                if (r.BaseStream.Position < r.BaseStream.Length) RadialSync.Read(r);
                MelonLogger.Msg($"[прогресс] загружено событий в пустых локациях: {n}");
                AppliedUnsaved.Clear();
                if (r.BaseStream.Length - r.BaseStream.Position >= 16 && r.ReadInt32() == AppliedMark)
                {
                    long ticks = r.ReadInt64();
                    int m = r.ReadInt32();
                    var applied = new List<byte[]>();
                    for (int i = 0; i < m; i++) applied.Add(r.ReadBytes(r.ReadInt32()));
                    if (m > 0 && SlotTicks() <= ticks)
                    {
                        // Игра упала между применением событий и сохранением: в файле мира их нет — применяем снова.
                        foreach (var raw in applied) Feed(raw);
                        MelonLogger.Msg($"[прогресс] событий, применённых, но не попавших в сохранение (игра закрылась раньше): {m} — применю снова");
                    }
                }
            }
            catch (Exception e)
            {
                MelonLogger.Warning("[прогресс] pending.bin: " + e.Message);
            }
        }

        private static void Feed(byte[] raw)
        {
            if (raw.Length == 0) return;
            var type = (MsgType)raw[0];
            if (type == MsgType.ObjectState) WorldObjects.Receive(raw);
            else if (type == MsgType.ItemDrop || type == MsgType.ItemPick || type == MsgType.ItemUpdate) ItemSync.Receive(type, raw);
            else if (type == MsgType.WorldEvent) WorldEvents.ReceiveRaw(raw);
        }

        public static void Reset()
        {
            AppliedUnsaved.Clear();
            _appliedDirtyAt = -1f;
            Parts.Clear();
            _watchedSave = "";
            _hostLoadedFor = "";
            _saveSeen = 0f;
        }
    }
}
