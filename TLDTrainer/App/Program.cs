using System;
using System.Diagnostics;
using System.Windows;

namespace TLDTrainer.App
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            CloseOldCopies();
            var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
            Theme.Apply(app);
            app.DispatcherUnhandledException += (_, e) =>
            {
                MessageBox.Show(e.Exception.Message, "TLD Trainer", MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
            app.Run(new MainWindow());
        }

        // Старый трейнер мог остаться в фоне спрятанным (без окна) и держать файл и порт.
        // Такие копии закрываем; если старая копия видна на экране, просто показываем её.
        private static void CloseOldCopies()
        {
            using var me = Process.GetCurrentProcess();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    if (p.Id == me.Id || !p.ProcessName.StartsWith("TLDTrainer", StringComparison.OrdinalIgnoreCase)) continue;
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
