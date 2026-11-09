namespace DiskCleaner.Core.Safety;

public sealed record KnownFolders(
    string UserTemp,
    string WindowsDir,
    string WindowsTemp,
    string ProgramFiles,
    string ProgramFilesX86,
    string ProgramData,
    string LocalAppData,
    string RoamingAppData,
    string UserProfile,
    string Downloads,
    string AppDirectory)
{
    /// <summary>Optional resolver for the real Downloads folder (set by the Windows platform layer).</summary>
    public static Func<string?>? DownloadsResolver { get; set; }

    public static KnownFolders FromEnvironment()
    {
        static string F(Environment.SpecialFolder f) => Environment.GetFolderPath(f);
        var windows = F(Environment.SpecialFolder.Windows);
        var profile = F(Environment.SpecialFolder.UserProfile);
        return new KnownFolders(
            UserTemp: Path.GetFullPath(Path.GetTempPath()),
            WindowsDir: windows,
            WindowsTemp: Path.Combine(windows, "Temp"),
            ProgramFiles: F(Environment.SpecialFolder.ProgramFiles),
            ProgramFilesX86: F(Environment.SpecialFolder.ProgramFilesX86),
            ProgramData: F(Environment.SpecialFolder.CommonApplicationData),
            LocalAppData: F(Environment.SpecialFolder.LocalApplicationData),
            RoamingAppData: F(Environment.SpecialFolder.ApplicationData),
            UserProfile: profile,
            Downloads: DownloadsResolver?.Invoke() ?? Path.Combine(profile, "Downloads"),
            AppDirectory: AppContext.BaseDirectory);
    }
}
