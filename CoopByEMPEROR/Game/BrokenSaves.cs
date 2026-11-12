using System;
using System.IO;
using System.Text;
using MelonLoader;
using MelonLoader.Utils;

namespace CoopByEMPEROR.Game
{
    // 0.19.55. Хост на 0.19.49–0.19.53 клал в копию мира метку подвигов строкой «1» вместо base64, игра друга копию не
    // читала, а мод по запасному пути всё равно записывал её файлом в сохранения (WorldTransfer.LoadHostWorld). Друзья
    // 11-10: после таких попыток у друга игра перестала доходить до главного меню, в Survival лежал нечитаемый sandbox7.
    // При запуске (до главного меню, кодом игры не пользуемся) убираем из сохранений только файлы с этой самой меткой:
    // переносим в UserData\CoopByEMPEROR\broken-saves, не удаляем.
    internal static class BrokenSaves
    {
        private const string BadMark = "\"tldcoop_feats_ask\":\"1\"";

        internal static void Sweep()
        {
            try
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Hinterland", "TheLongDark", "Survival");
                if (!Directory.Exists(dir)) return;
                string to = Path.Combine(MelonEnvironment.UserDataDirectory, "TLDCoop", "broken-saves");
                foreach (var f in Directory.GetFiles(dir, "sandbox*"))
                {
                    if (Path.GetExtension(f).Length > 0) continue;
                    try
                    {
                        string json = Encoding.UTF8.GetString(Lzf.Decompress(File.ReadAllBytes(f)));
                        if (json.IndexOf(BadMark, StringComparison.Ordinal) < 0) continue;
                        Directory.CreateDirectory(to);
                        string dest = Path.Combine(to, Path.GetFileName(f) + "-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"));
                        File.Move(f, dest);
                        MelonLogger.Msg($"[мир] {Path.GetFileName(f)} в сохранениях — нечитаемая копия мира хоста (метка подвигов не base64, 0.19.53): перенёс в {dest}");
                    }
                    catch (Exception e) { MelonLogger.Warning($"[мир] проверка {Path.GetFileName(f)}: {e.Message}"); }
                }
            }
            catch (Exception e) { MelonLogger.Warning("[мир] проверка сохранений: " + e.Message); }
        }
    }
}
