using System;
using System.IO;
using System.IO.Compression;
using System.Text;
using Il2Cpp;
using MelonLoader;
using MelonLoader.Utils;
using UnityEngine;

namespace TLDCoop.Game
{
    // Кнопка «Собрать логи»: лог MelonLoader (последний и предыдущие), настройки мода и версии —
    // одним архивом в UserData/TLDCoop/logs, чтобы прислать его целиком.
    public static class Diagnostics
    {
        public static string LastResult { get; private set; } = "";

        // Здоровье и потребности: тратятся ли они вообще (друг жаловался, что здоровье не убывает).
        // Громкость из настроек игры: при «голосе» на нуле молчат и свои фразы, и фразы других игроков.
        public static string Audio()
        {
            try
            {
                var st = Il2CppTLD.SaveState.BaseStateSingleton<Il2CppTLD.SaveState.SettingsState>.Instance;
                if (st == null) return "нет настроек";
                string r = $"общий {st.m_MasterVolume * 100f:0}%, эффекты {st.m_SoundVolume * 100f:0}%, музыка {st.m_MusicVolume * 100f:0}%, голос {st.m_VoiceVolume * 100f:0}%";
                try { r += ", голос персонажа " + PlayerManager.m_VoicePersona; } catch { }
                r += ", " + Speakers();
                return r;
            }
            catch (Exception e) { return "ошибка " + e.Message; }
        }

        // Фразы выживальщика у друга не звучали совсем, даже свои, хотя игра их запускала. Фразы — отдельные файлы
        // .wem (Media/English(US)), звук идёт в центральный канал: смотрим, сколько каналов у вывода, наушники ли,
        // язык звука и есть ли эти файлы (при нехватке — «Проверить целостность файлов» в Steam).
        public static string Speakers()
        {
            string r = "";
            try
            {
                var c = AkSoundEngine.GetSpeakerConfiguration();
                if (c != null) r += $"каналов {c.uNumChannels} (маска 0x{c.uChannelMask:X})";
            }
            catch (Exception e) { r += "каналы ? " + e.Message; }
            try { if (AkSoundEngine.GetPanningRule(out int pr) == AKRESULT.AK_Success) r += pr == 1 ? ", наушники" : ", колонки"; } catch { }
            try { r += ", язык звука " + AkSoundEngine.GetCurrentLanguage(); } catch { }
            try
            {
                string dir = System.IO.Path.Combine(UnityEngine.Application.streamingAssetsPath, "Audio", "GeneratedSoundBanks", "Windows", "Media", "English(US)");
                int n = System.IO.Directory.Exists(dir) ? System.IO.Directory.GetFiles(dir, "*.wem").Length : -1;
                r += n < 0 ? ", файлов фраз нет (папки Media/English(US) нет)" : $", файлов фраз {n}";
            }
            catch { }
            return r;
        }

        public static string Health()
        {
            try
            {
                var c = GameManager.GetConditionComponent();
                if (c == null) return "нет";
                string r = $"{c.m_CurrentHP:0.#}/{c.m_MaxHP:0.#}";
                if (c.m_Invulnerable) r += ", неуязвим (m_Invulnerable)";
                if (c.m_NeverDie) r += ", не умирает (m_NeverDie)";
                try { if (GameManager.GetPlayerManagerComponent().m_God) r += ", бог (m_God)"; } catch { }
                try { r += $", голод {GameManager.GetHungerComponent().m_CurrentReserveCalories:0}"; } catch { }
                try { r += $", жажда {GameManager.GetThirstComponent().m_CurrentThirst:0}"; } catch { }
                try { r += $", усталость {GameManager.GetFatigueComponent().m_CurrentFatigue:0}"; } catch { }
                try { r += $", холод {GameManager.GetFreezingComponent().m_CurrentFreezing:0}"; } catch { }
                return r;
            }
            catch (Exception e) { return "! " + e.Message; }
        }

        public static void CollectLogs()
        {
            try
            {
                string dir = Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "logs");
                Directory.CreateDirectory(dir);
                string zip = Path.Combine(dir, $"coop-logs-{DateTime.Now:yyyyMMdd_HHmmss}.zip");
                using (var fs = File.Create(zip))
                using (var a = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    var info = new StringBuilder();
                    info.AppendLine("Мод: " + Version.Text + ", протокол " + Net.Protocol.Version);
                    info.AppendLine("Игра: " + Application.version + ", Unity " + Application.unityVersion);
                    info.AppendLine("Режим: " + Net.Session.Mode + ", " + Net.Session.Status);
                    info.AppendLine("Персонажи: " + (Characters.Available ? Characters.State : "нет characters.bin"));
                    info.AppendLine("Проверка: " + SyncCheck.LastResult);
                    info.AppendLine("Нагрузка: " + Load.Summary + ", пинг " + Net.Session.PingMs() + " мс");
                    info.AppendLine("Здоровье: " + Health());
                    try { info.AppendLine("Управление: " + StruggleGuard.State()); } catch { }
                    info.AppendLine("Звук игры: " + Audio());
                    info.AppendLine("Время: " + DateTime.Now);
                    var e = a.CreateEntry("info.txt");
                    using (var w = new StreamWriter(e.Open(), Encoding.UTF8)) w.Write(info.ToString());
                    // Что рисуется вокруг (пропадающие постройки у друга, Game/RenderCheck.cs).
                    if (GameApi.InGame())
                    {
                        var re = a.CreateEntry("render.txt");
                        using var rw = new StreamWriter(re.Open(), Encoding.UTF8);
                        rw.Write(RenderCheck.Describe());
                    }

                    string ml = MelonEnvironment.MelonLoaderDirectory;
                    AddFile(a, Path.Combine(ml, "Latest.log"), "Latest.log");
                    string logs = Path.Combine(ml, "Logs");
                    if (Directory.Exists(logs))
                    {
                        var files = Directory.GetFiles(logs, "*.log");
                        Array.Sort(files, StringComparer.Ordinal);
                        for (int i = Math.Max(0, files.Length - 3); i < files.Length; i++) AddFile(a, files[i], "Logs/" + Path.GetFileName(files[i]));
                    }
                    AddFile(a, Path.Combine(MelonEnvironment.UserDataDirectory, "MelonPreferences.cfg"), "MelonPreferences.cfg");
                }
                LastResult = UI.L.T("Логи собраны: ", "Logs collected: ") + zip;
                MelonLogger.Msg("[логи] " + zip);
                try { Application.OpenURL("file:///" + dir.Replace('\\', '/')); } catch { }
            }
            catch (Exception e)
            {
                LastResult = UI.L.T("Не вышло собрать логи: ", "Could not collect logs: ") + e.Message;
                MelonLogger.Warning("[логи] " + e.Message);
            }
        }

        // Лог открыт игрой на запись: читаем с общим доступом.
        private static void AddFile(ZipArchive a, string path, string name)
        {
            if (!File.Exists(path)) return;
            var e = a.CreateEntry(name);
            using var src = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var dst = e.Open();
            src.CopyTo(dst);
        }
    }
}
