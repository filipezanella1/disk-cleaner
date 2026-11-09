using System.IO;
using System.Windows;
using System.Windows.Threading;
using DiskCleaner.App.Services;

namespace DiskCleaner.App;

public partial class App : Application
{
    public static AppServices Services { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        Services = new AppServices();
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        TaskScheduler.UnobservedTaskException += (_, args) => { LogError(args.Exception); args.SetObserved(); };
        base.OnStartup(e);
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        LogError(e.Exception);
        MessageBox.Show($"Ocorreu um erro inesperado:\n\n{e.Exception.Message}\n\nDetalhes em {Services.ErrorLogPath}",
            "DiskCleaner", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }

    public static void LogError(Exception ex)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Services.ErrorLogPath)!);
            File.AppendAllText(Services.ErrorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {ex}\n\n");
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
