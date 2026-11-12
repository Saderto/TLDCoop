using System;
using System.Diagnostics;
using System.Windows;

namespace TrainerByEMPEROR.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            // --background: трейнер запустила сама игра (мод). Окно не показываем, оно открывается клавишей поверх игры.
            bool background = Array.Exists(args, a => string.Equals(a, "--background", StringComparison.OrdinalIgnoreCase));
            bool open = Array.Exists(args, a => string.Equals(a, "--open", StringComparison.OrdinalIgnoreCase));
            CloseOldCopies(background);
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            Theme.Apply(app);
            app.DispatcherUnhandledException += (_, e) =>
            {
                MessageBox.Show(e.Exception.Message, "TrainerByEMPEROR", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            var window = new MainWindow(background, open);
            if (background)
            {
                app.MainWindow = window;
                app.Run();
            }
            else app.Run(window);
        }

        // Старый трейнер мог остаться в фоне спрятанным (без окна) и держать файл и порт.
        // Такие копии закрываем; если старая копия видна на экране, просто показываем её.
        private static void CloseOldCopies(bool background)
        {
            using var me = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == me.Id || !p.ProcessName.StartsWith("TrainerByEMPEROR", StringComparison.OrdinalIgnoreCase)) continue;
                    if (background)
                    {
                        // Трейнер уже запущен (открыт вручную) — второй не нужен.
                        Environment.Exit(0);
                    }
                    if (p.MainWindowHandle != IntPtr.Zero && Native.IsWindowVisible(p.MainWindowHandle))
                    {
                        Native.SetForegroundWindow(p.MainWindowHandle);
                        Environment.Exit(0);
                    }
                    p.Kill();
                    p.WaitForExit(3000);
                }
                catch
                {
                    // нет доступа или уже закрылся
                }
                finally
                {
                    p.Dispose();
                }
            }
        }
    }
}
