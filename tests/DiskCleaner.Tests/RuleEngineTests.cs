using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Safety;
using DiskCleaner.Core.Settings;
using DiskCleaner.Tests.Helpers;
using static DiskCleaner.Tests.Helpers.TestTrees;

namespace DiskCleaner.Tests;

public class RuleEngineTests : RuleTestBase
{
    private sealed class StubRule(CleanupCategory category, params CleanupItem[] items) : ICleanupRule
    {
        public CleanupCategory Category => category;
        public List<IReadOnlyList<SpaceTree>> Calls { get; } = new();
        public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx) { Calls.Add(trees); return items; }
    }

    private static CleanupItem Item(string path, long size, CleanupCategory c, Risk r = Risk.Safe, bool dir = true) =>
        new(path, dir, size, size, T0, c, r, "x");

    private static RuleEngine Engine(params ICleanupRule[] rules) => new(rules, new ProtectedPaths(F));
    private static readonly SpaceTree[] Trees = { Build(Root, ("f", 1, T0)) };

    [Fact]
    public void Run_DropsProtectedItems()
    {
        var groups = Engine(new StubRule(CleanupCategory.LargeFiles,
            Item(J(F.ProgramFiles, "x"), 10, CleanupCategory.LargeFiles),
            Item(J(F.Downloads, "y"), 10, CleanupCategory.LargeFiles))).Run(Trees, Ctx());
        var item = Assert.Single(Assert.Single(groups).Items);
        Assert.Equal(J(F.Downloads, "y"), item.Path);
    }

    [Fact]
    public void Run_NestedAndDuplicatePaths_KeepsOuterOnce()
    {
        var a = J(F.UserTemp, "A");
        var groups = Engine(
            new StubRule(CleanupCategory.DevFolders, Item(J(a, "node_modules"), 60, CleanupCategory.DevFolders), Item(a, 100, CleanupCategory.DevFolders)),
            new StubRule(CleanupCategory.Temp, Item(a, 100, CleanupCategory.Temp))).Run(Trees, Ctx());
        var g = Assert.Single(groups);
        Assert.Equal(CleanupCategory.Temp, g.Category);
        Assert.Equal(100, g.TotalSize);
        Assert.Equal(a, Assert.Single(g.Items).Path);
    }

    [Fact]
    public void Run_SamePathDifferentCase_CountedOnce()
    {
        var a = J(F.Downloads, "big.iso");
        var groups = Engine(
            new StubRule(CleanupCategory.LargeFiles, Item(a, 5, CleanupCategory.LargeFiles, Risk.Review, false)),
            new StubRule(CleanupCategory.OldDownloads, Item(a.ToUpperInvariant(), 5, CleanupCategory.OldDownloads, Risk.Review, false))).Run(Trees, Ctx());
        Assert.Equal(CleanupCategory.LargeFiles, Assert.Single(groups).Category);
    }

    [Fact]
    public void Run_SkipsIncompleteTrees()
    {
        var incomplete = Build(Root, ("f", 1, T0)); incomplete.IsComplete = false;
        var rule = new StubRule(CleanupCategory.Temp);
        Engine(rule).Run(new[] { incomplete }, Ctx());
        Assert.Empty(Assert.Single(rule.Calls));
    }

    [Fact]
    public void Run_SkipsDisabledCategories()
    {
        var rule = new StubRule(CleanupCategory.Temp, Item(J(F.UserTemp, "a"), 1, CleanupCategory.Temp));
        var s = new CleanerSettings { DisabledCategories = new HashSet<CleanupCategory> { CleanupCategory.Temp } };
        Assert.Empty(Engine(rule).Run(Trees, Ctx(s)));
        Assert.Empty(rule.Calls);
    }

    [Fact]
    public void Run_GroupsByCategoryInEnumOrder_ItemsBySizeDesc()
    {
        var groups = Engine(
            new StubRule(CleanupCategory.LargeFiles, Item(J(F.Downloads, "s"), 1, CleanupCategory.LargeFiles), Item(J(F.Downloads, "b"), 9, CleanupCategory.LargeFiles)),
            new StubRule(CleanupCategory.Temp, Item(J(F.UserTemp, "t"), 3, CleanupCategory.Temp))).Run(Trees, Ctx());
        Assert.Equal(new[] { CleanupCategory.Temp, CleanupCategory.LargeFiles }, groups.Select(g => g.Category));
        Assert.Equal(new long[] { 9, 1 }, groups[1].Items.Select(i => i.Size));
        Assert.Equal(10, groups[1].TotalSize);
    }

    [Fact]
    public void Run_RecycleBinItem_NotTreatedAsProtectedOrOverlapping()
    {
        var rb = new CleanupItem(CleanupItem.RecycleBinPath, false, 50, 50, T0, CleanupCategory.RecycleBin, Risk.Safe, "x");
        var groups = Engine(new StubRule(CleanupCategory.RecycleBin, rb)).Run(Trees, Ctx());
        Assert.Equal(50, Assert.Single(groups).TotalSize);
    }
}
