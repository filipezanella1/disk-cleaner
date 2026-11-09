using System.IO;
using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.IO;
using DiskCleaner.Core.Platform.Windows;
using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Safety;
using DiskCleaner.Core.Scanning;
using DiskCleaner.Core.Settings;

namespace DiskCleaner.App.Services;

/// <summary>Single composition root for the Core services.</summary>
public sealed class AppServices
{
    public AppServices()
    {
        WindowsPlatform.Initialize();
        DataDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DiskCleaner");
        LogDirectory = Path.Combine(DataDirectory, "logs");
        ErrorLogPath = Path.Combine(LogDirectory, "error.log");
        Folders = KnownFolders.FromEnvironment();
        Protected = new ProtectedPaths(Folders);
        FileSystem = new WindowsFileSystem();
        RecycleBin = new ShellRecycleBin();
        SettingsStore = new SettingsStore(Path.Combine(DataDirectory, "settings.json"));
        Settings = SettingsStore.Load();
        Dialogs = new WpfDialogs();
    }

    public string DataDirectory { get; }
    public string LogDirectory { get; }
    public string ErrorLogPath { get; }
    public KnownFolders Folders { get; }
    public ProtectedPaths Protected { get; }
    public IFileSystem FileSystem { get; }
    public IRecycleBin RecycleBin { get; }
    public SettingsStore SettingsStore { get; }
    public CleanerSettings Settings { get; set; }
    public IDialogs Dialogs { get; }

    /// <summary>Rules in CleanupCategory order.</summary>
    public static IReadOnlyList<ICleanupRule> CreateRules() => new ICleanupRule[]
    {
        KnownPathRules.Temp,
        KnownPathRules.BrowserCache,
        KnownPathRules.AppCache,
        KnownPathRules.LogsDumpsFolders,
        new DumpFilesRule(),
        new RecycleBinRule(),
        new LargeFilesRule(),
        new DuplicatesRule(),
        new OldDownloadsRule(),
        new DevFoldersRule(),
    };

    public Scanner CreateScanner() => new(FileSystem);
    public RuleEngine CreateEngine() => new(CreateRules(), Protected);
    public Cleaner CreateCleaner() => new(FileSystem, RecycleBin, Protected, new FileCleanupLog(LogDirectory, () => DateTime.Now));
    public RuleContext CreateContext(CancellationToken ct) => new(Folders, Settings, FileSystem, RecycleBin, DateTime.UtcNow, ct);
}
