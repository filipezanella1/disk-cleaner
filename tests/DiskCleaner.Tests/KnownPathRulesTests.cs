using DiskCleaner.Core.Rules;
using DiskCleaner.Tests.Helpers;
using static DiskCleaner.Tests.Helpers.TestTrees;

namespace DiskCleaner.Tests;

public class KnownPathRulesTests : RuleTestBase
{
    private static readonly DateTime Old = Now.AddDays(-2);
    private static readonly DateTime Recent = Now.AddHours(-1);

    private static List<CleanupItem> Run(ICleanupRule rule, params (string rel, long size, DateTime lw)[] files) =>
        rule.Analyze(new[] { Build(Root, files) }, Ctx()).ToList();

    [Fact]
    public void Temp_SuggestsOldChildren_NotRecentOnes_NotTheFolder()
    {
        var t = Rel(F.UserTemp);
        var items = Run(KnownPathRules.Temp, ($"{t}/old.tmp", 10, Old), ($"{t}/new.tmp", 10, Recent), ($"{t}/olddir/x", 5, Old));
        Assert.Equal(new[] { J(F.UserTemp, "old.tmp"), J(F.UserTemp, "olddir") }.OrderBy(x => x), items.Select(i => i.Path).OrderBy(x => x));
        Assert.All(items, i => { Assert.Equal(CleanupCategory.Temp, i.Category); Assert.Equal(Risk.Safe, i.Risk); });
        Assert.Equal("Arquivo temporário com mais de 24h", items[0].Reason);
    }

    [Fact]
    public void Temp_OldFolderWithRecentFileInside_NotSuggested()
    {
        var t = Rel(F.UserTemp);
        Assert.Empty(Run(KnownPathRules.Temp, ($"{t}/d/old", 1, Old), ($"{t}/d/sub/new", 1, Recent)));
    }

    [Fact]
    public void Temp_WindowsTempChildrenSuggested()
    {
        var t = Rel(F.WindowsTemp);
        Assert.Equal(J(F.WindowsTemp, "a.log"), Assert.Single(Run(KnownPathRules.Temp, ($"{t}/a.log", 1, Old))).Path);
    }

    [Fact]
    public void BrowserCache_MatchesAnyProfile_NeverLoginDataOrCookies()
    {
        var ud = Rel(J(F.LocalAppData, "Google", "Chrome", "User Data"));
        var items = Run(KnownPathRules.BrowserCache,
            ($"{ud}/Default/Cache/data_1", 100, Old), ($"{ud}/Profile 1/Cache/data_1", 50, Old),
            ($"{ud}/Default/Code Cache/js/a", 5, Old),
            ($"{ud}/Default/Login Data", 1, Old), ($"{ud}/Default/Cookies", 1, Old), ($"{ud}/Default/Bookmarks", 1, Old));
        Assert.Equal(3, items.Count);
        Assert.DoesNotContain(items, i => i.Path.EndsWith("Login Data") || i.Path.EndsWith("Cookies") || i.Path.EndsWith("Bookmarks"));
        Assert.Contains(items, i => i.Path == J(F.LocalAppData, "Google", "Chrome", "User Data", "Profile 1", "Cache") && i.Size == 50 && i.IsDirectory);
    }

    [Fact]
    public void BrowserCache_EdgeBraveFirefox()
    {
        var items = Run(KnownPathRules.BrowserCache,
            (Rel(J(F.LocalAppData, "Microsoft", "Edge", "User Data", "Default", "GPUCache", "x")), 1, Old),
            (Rel(J(F.LocalAppData, "BraveSoftware", "Brave-Browser", "User Data", "Default", "Cache", "x")), 1, Old),
            (Rel(J(F.LocalAppData, "Mozilla", "Firefox", "Profiles", "abc.default", "cache2", "x")), 1, Old));
        Assert.Equal(3, items.Count);
        Assert.All(items, i => Assert.Equal("Cache do navegador (recriado automaticamente)", i.Reason));
    }

    [Fact]
    public void AppCache_FindsDiscordAndVsCodeAndSpotify()
    {
        var items = Run(KnownPathRules.AppCache,
            (Rel(J(F.RoamingAppData, "discord", "Cache", "x")), 1, Old),
            (Rel(J(F.RoamingAppData, "Code", "CachedData", "x")), 1, Old),
            (Rel(J(F.RoamingAppData, "Code", "User", "settings.json")), 1, Old),
            (Rel(J(F.LocalAppData, "Spotify", "Data", "x")), 1, Old),
            (Rel(J(F.LocalAppData, "Packages", "MSTeams_8wekyb3d8bbwe", "LocalCache", "x")), 1, Old));
        Assert.Equal(4, items.Count);
        Assert.All(items, i => Assert.Equal(CleanupCategory.AppCache, i.Category));
    }

    [Fact]
    public void LogsDumpsFolders_Found()
    {
        var items = Run(KnownPathRules.LogsDumpsFolders,
            (Rel(J(F.LocalAppData, "CrashDumps", "a.dmp")), 1, Old),
            (Rel(J(F.LocalAppData, "Microsoft", "Windows", "WER", "ReportArchive", "x")), 1, Old));
        Assert.Equal(2, items.Count);
        Assert.All(items, i => Assert.Equal("Log ou despejo de erro", i.Reason));
    }

    [Fact]
    public void KnownPath_NotInScannedTrees_ReturnsNothing()
    {
        var other = Build(Path.Combine(Path.GetTempPath(), "dc-other"), ("Temp/x", 1, Old));
        Assert.Empty(KnownPathRules.Temp.Analyze(new[] { other }, Ctx()));
    }

    [Fact]
    public void DumpFiles_FindsDmpAnywhere()
    {
        var items = new DumpFilesRule().Analyze(new[] { Build(Root, ("Games/x/crash.DMP", 7, Old), ("Games/x/save.dat", 7, Old)) }, Ctx()).ToList();
        var i = Assert.Single(items);
        Assert.Equal(CleanupCategory.LogsDumps, i.Category);
        Assert.Equal(7, i.Size);
    }
}
