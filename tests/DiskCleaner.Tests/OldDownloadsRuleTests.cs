using DiskCleaner.Core.Rules;
using DiskCleaner.Tests.Helpers;
using static DiskCleaner.Tests.Helpers.TestTrees;

namespace DiskCleaner.Tests;

public class OldDownloadsRuleTests : RuleTestBase
{
    [Fact]
    public void MatchesExtensionsCaseInsensitive_OlderThan90Days()
    {
        var d = Rel(F.Downloads);
        var items = new OldDownloadsRule().Analyze(new[] { Build(Root,
            ($"{d}/Setup.EXE", 1, Now.AddDays(-100)),
            ($"{d}/new.exe", 1, Now.AddDays(-10)),
            ($"{d}/doc.pdf", 1, Now.AddDays(-100)),
            ($"{d}/sub/disk.iso", 1, Now.AddDays(-91)),
            ($"{d}/a.msi", 1, Now.AddDays(-200)), ($"{d}/b.zip", 1, Now.AddDays(-200)),
            ($"{d}/c.rar", 1, Now.AddDays(-200)), ($"{d}/e.7z", 1, Now.AddDays(-200))) }, Ctx()).ToList();
        Assert.Equal(6, items.Count);
        Assert.DoesNotContain(items, i => i.Path.EndsWith("new.exe") || i.Path.EndsWith("doc.pdf"));
        Assert.All(items, i => { Assert.Equal(Risk.Review, i.Risk); Assert.Equal(CleanupCategory.OldDownloads, i.Category); });
        Assert.Equal("Instalador/arquivo compactado em Downloads há mais de 90 dias", items[0].Reason);
    }

    [Fact]
    public void OutsideDownloads_Ignored() =>
        Assert.Empty(new OldDownloadsRule().Analyze(new[] { Build(Root, ("Users/u/Desktop/x.exe", 1, Now.AddDays(-300))) }, Ctx()));
}
