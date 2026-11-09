namespace DiskCleaner.Core.Rules;

public static class KnownPathRules
{
    private static string J(params string[] p) => Path.Combine(p);
    private const string ChromiumCaches = "{Cache,Code Cache,GPUCache}";

    public static ICleanupRule Temp { get; } = new KnownPathRule(
        CleanupCategory.Temp, Risk.Safe, "Arquivo temporário com mais de 24h",
        f => new[] { f.UserTemp, f.WindowsTemp },
        childrenOnly: true, minAge: ctx => ctx.Settings.TempMinAge);

    public static ICleanupRule BrowserCache { get; } = new KnownPathRule(
        CleanupCategory.BrowserCache, Risk.Safe, "Cache do navegador (recriado automaticamente)",
        f => new[]
        {
            J(f.LocalAppData, "Google", "Chrome", "User Data", "*", ChromiumCaches),
            J(f.LocalAppData, "Microsoft", "Edge", "User Data", "*", ChromiumCaches),
            J(f.LocalAppData, "BraveSoftware", "Brave-Browser", "User Data", "*", ChromiumCaches),
            J(f.LocalAppData, "Mozilla", "Firefox", "Profiles", "*", "cache2"),
        });

    public static ICleanupRule AppCache { get; } = new KnownPathRule(
        CleanupCategory.AppCache, Risk.Safe, "Cache de aplicativo (recriado automaticamente)",
        f => new[]
        {
            J(f.RoamingAppData, "discord", ChromiumCaches),
            J(f.LocalAppData, "Spotify", "Data"),
            J(f.RoamingAppData, "Microsoft", "Teams", ChromiumCaches),
            J(f.LocalAppData, "Packages", "MSTeams_8wekyb3d8bbwe", "LocalCache"),
            J(f.RoamingAppData, "Code", "{Cache,CachedData,Code Cache,GPUCache}"),
        });

    public static ICleanupRule LogsDumpsFolders { get; } = new KnownPathRule(
        CleanupCategory.LogsDumps, Risk.Safe, "Log ou despejo de erro",
        f => new[]
        {
            J(f.LocalAppData, "CrashDumps"),
            J(f.LocalAppData, "Microsoft", "Windows", "WER"),
        });
}
