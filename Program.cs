using MewNX.Infrastructure;
using MewNX.UI;

namespace MewNX;

internal static class Program
{
    private const string MutexName = "Global\\MewNX_0F6B0F9A";

    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(true, MutexName, out var firstInstance);
        if (!firstInstance)
        {
            MessageBox.Show("MewNX ya está abierto.\n\nSolo se permite una instancia para evitar dos procesos accediendo al mismo USB.", "MewNX", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        var config = ConfigLoader.Load(AppContext.BaseDirectory);
        var paths = AppPaths.Create(config);
        Directory.CreateDirectory(paths.DataDirectory);
        Directory.CreateDirectory(paths.CacheDirectory);
        var logger = new AppLogger(paths.LogFile);
        Application.ThreadException += (_, e) =>
        {
            logger.Error("UI unhandled exception", e.Exception);
            MessageBox.Show("MewNX encontró un error inesperado.\n\nEl error se ha guardado en el log.\n\n" + e.Exception.Message,
                "MewNX", MessageBoxButtons.OK, MessageBoxIcon.Error);
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.Error("Process unhandled exception", e.ExceptionObject as Exception);
        using var form = MainForm.CreateDefault(paths, logger, config);
        if (config.Ui.StartMaximized) form.WindowState = FormWindowState.Maximized;
        Application.Run(form);
    }
}
