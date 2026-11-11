using System;
using System.IO;

namespace TLDTrainer.App
{
    // Запуск трейнера вместе с игрой: кладём в папку Mods игры файл с путём к TLDTrainer.exe,
    // мод трейнера при старте игры читает его и запускает трейнер в фоне.
    internal static class AutoStart
    {
        public const string FileName = "TLDTrainer.app.txt";

        public static void Update(Settings s)
        {
            try
            {
                if (!GameInstaller.IsGameFolder(s.GamePath)) return;
                string mods = Path.Combine(s.GamePath, "Mods");
                if (!Directory.Exists(mods)) return;
                string file = Path.Combine(mods, FileName);
                if (s.StartWithGame ?? true)
                {
                    string exe = Environment.ProcessPath;
                    if (string.IsNullOrEmpty(exe)) return;
                    if (!File.Exists(file) || File.ReadAllText(file).Trim() != exe) File.WriteAllText(file, exe);
                }
                else if (File.Exists(file)) File.Delete(file);
            }
            catch
            {
                // нет прав на папку игры — просто не будет автозапуска
            }
        }
    }
}
