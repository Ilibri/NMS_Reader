using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Threading;

namespace NMS_Reader
{
    public partial class App : Application
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetProcessDpiAwarenessContext(IntPtr dpiContext);

        private static readonly IntPtr DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2 = (IntPtr)(-4);

        private static void Log(string msg)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app_startup.log"),
                    $"[{DateTime.Now:HH:mm:ss.fff}] {msg}\n");
            }
            catch { }
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            Log("App OnStartup enter");
            try
            {
                SetProcessDpiAwarenessContext(DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2);
            }
            catch { }

            DispatcherUnhandledException += App_DispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            base.OnStartup(e);
            Log("App OnStartup completed");
        }

        private void App_DispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            Log($"DispatcherUnhandledException: {e.Exception.Message}\n{e.Exception.StackTrace}");
            MessageBox.Show($"Ошибка приложения:\n{e.Exception.Message}\n\n{e.Exception.StackTrace}",
                            "NMS_Reader - Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            e.Handled = true;
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            if (e.ExceptionObject is Exception ex)
            {
                Log($"CurrentDomain_UnhandledException: {ex.Message}\n{ex.StackTrace}");
                MessageBox.Show($"Критическая ошибка:\n{ex.Message}\n\n{ex.StackTrace}",
                                "NMS_Reader - Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
