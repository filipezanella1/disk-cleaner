using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Settings;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class SettingsStoreTests
{
    [Fact]
    public void Defaults_MatchSpec()
    {
        var s = new CleanerSettings();
        Assert.Equal(524_288_000, s.LargeFileThresholdBytes);
        Assert.Equal(90, s.OldDownloadDays);
        Assert.Equal(1_048_576, s.DuplicateMinBytes);
        Assert.Equal(TimeSpan.FromHours(24), s.TempMinAge);
        Assert.Empty(s.DisabledCategories);
    }

    [Fact]
    public void Load_MissingOrCorrupt_ReturnsDefaults()
    {
        using var d = new TempDir();
        var path = Path.Combine(d.Path, "sub", "settings.json");
        Assert.Equal(524_288_000, new SettingsStore(path).Load().LargeFileThresholdBytes);
        d.File("bad.json", System.Text.Encoding.UTF8.GetBytes("{not json"));
        Assert.Equal(90, new SettingsStore(Path.Combine(d.Path, "bad.json")).Load().OldDownloadDays);
    }

    [Fact]
    public void RoundTrip()
    {
        using var d = new TempDir();
        var store = new SettingsStore(Path.Combine(d.Path, "sub", "settings.json"));
        store.Save(new CleanerSettings { LargeFileThresholdBytes = 1, OldDownloadDays = 7, DisabledCategories = new HashSet<CleanupCategory> { CleanupCategory.Duplicates } });
        var s = store.Load();
        Assert.Equal(1, s.LargeFileThresholdBytes);
        Assert.Equal(7, s.OldDownloadDays);
        Assert.Contains(CleanupCategory.Duplicates, s.DisabledCategories);
    }
}
