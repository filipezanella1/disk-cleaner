using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.Rules;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class FileCleanupLogTests
{
    [Fact]
    public void WritesExpectedFormat()
    {
        using var d = new TempDir();
        var logDir = Path.Combine(d.Path, "logs");
        var a = new CleanupItem("/x/a", false, 10, 10, TestTrees.T0, CleanupCategory.Temp, Risk.Safe, "r");
        var b = a with { Path = "/x/b" };
        new FileCleanupLog(logDir, () => new DateTime(2026, 11, 9, 13, 5, 7)).Write(
            new CleanupResult(new[] { a }, new[] { new SkippedItem(b, "Em uso") }, 10));
        var file = Path.Combine(logDir, "cleanup-20261109-130507.log");
        Assert.True(File.Exists(file));
        Assert.Equal(new[] { "REMOVED\t10\t/x/a", "SKIPPED\tEm uso\t/x/b" }, File.ReadAllLines(file));
    }
}
