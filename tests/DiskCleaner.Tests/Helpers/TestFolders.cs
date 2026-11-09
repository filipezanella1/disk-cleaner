using DiskCleaner.Core.Safety;

namespace DiskCleaner.Tests.Helpers;

public static class TestFolders
{
    public static KnownFolders Under(string root)
    {
        string P(params string[] p) => Path.Combine([root, .. p]);
        var profile = P("Users", "u");
        var local = Path.Combine(profile, "AppData", "Local");
        return new KnownFolders(
            UserTemp: Path.Combine(local, "Temp"),
            WindowsDir: P("Windows"),
            WindowsTemp: P("Windows", "Temp"),
            ProgramFiles: P("Program Files"),
            ProgramFilesX86: P("Program Files (x86)"),
            ProgramData: P("ProgramData"),
            LocalAppData: local,
            RoamingAppData: Path.Combine(profile, "AppData", "Roaming"),
            UserProfile: profile,
            Downloads: Path.Combine(profile, "Downloads"),
            AppDirectory: P("App"));
    }
}
