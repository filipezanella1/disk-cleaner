using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Settings;
using DiskCleaner.Tests.Helpers;
using static DiskCleaner.Tests.Helpers.TestTrees;

namespace DiskCleaner.Tests;

public class LargeFilesRuleTests : RuleTestBase
{
    [Fact]
    public void AtThreshold_Included_BelowExcluded()
    {
        var items = new LargeFilesRule().Analyze(new[] { Build(Root, ("a.bin", 524_288_000, T0), ("b.bin", 524_287_999, T0)) }, Ctx()).ToList();
        var i = Assert.Single(items);
        Assert.Equal(P("a.bin"), i.Path);
        Assert.Equal(Risk.Review, i.Risk);
        Assert.Equal(CleanupCategory.LargeFiles, i.Category);
        Assert.Equal("Arquivo grande", i.Reason);
    }

    [Fact]
    public void RespectsCustomThreshold() =>
        Assert.Single(new LargeFilesRule().Analyze(new[] { Build(Root, ("a", 10, T0), ("b", 9, T0)) },
            Ctx(new CleanerSettings { LargeFileThresholdBytes = 10 })));
}
