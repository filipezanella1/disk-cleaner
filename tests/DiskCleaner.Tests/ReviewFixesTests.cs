using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.IO;
using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Safety;
using DiskCleaner.Core.Scanning;
using DiskCleaner.Tests.Helpers;
using static DiskCleaner.Tests.Helpers.TestTrees;

namespace DiskCleaner.Tests;

/// <summary>Regression tests for the final-review findings.</summary>
public class ReviewFixesTests : RuleTestBase
{
    private static readonly DateTime W = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class NullLog : ICleanupLog { public void Write(CleanupResult r) { } }

    // #1 Emptying the bin must happen before recycling, so this batch's files survive in the bin.
    [Fact]
    public void Clean_RecycleBinAndFiles_EmptiesBinBeforeRecycling()
    {
        var fs = new FakeFileSystem();
        var order = new List<string>();
        var bin = new FakeRecycleBin { Size = 5 };
        bin.OnRecycled = p => order.Add("recycle");
        var path = J(F.Downloads, "big.iso");
        fs.File(path, 10, W);
        var items = new[]
        {
            new CleanupItem(path, false, 10, 10, W, CleanupCategory.LargeFiles, Risk.Review, "x"),
            new CleanupItem(CleanupItem.RecycleBinPath, false, 5, 5, DateTime.MinValue, CleanupCategory.RecycleBin, Risk.Safe, "x"),
        };
        var cleaner = new Cleaner(fs, new OrderedBin(bin, order), new ProtectedPaths(F), new NullLog());
        cleaner.Clean(items, _ => false, default);
        Assert.Equal(new[] { "empty", "recycle" }, order);
    }

    private sealed class OrderedBin(FakeRecycleBin inner, List<string> order) : IRecycleBin
    {
        public long QuerySize() => inner.QuerySize();
        public bool CanRecycle(string path) => inner.CanRecycle(path);
        public RecycleOutcome Recycle(string path) => inner.Recycle(path);
        public bool Empty() { order.Add("empty"); return inner.Empty(); }
    }

    // #2 A duplicate whose kept copy is itself being removed must not be suggested.
    [Fact]
    public void Run_DuplicateWhoseKeptCopyIsAlsoSuggested_Dropped()
    {
        var kept = J(F.Downloads, "setup.zip");
        var copy = J(F.UserProfile, "Documents", "setup.zip");
        var rules = new ICleanupRule[]
        {
            new Stub(CleanupCategory.Duplicates, new CleanupItem(copy, false, 10, 10, W, CleanupCategory.Duplicates, Risk.Review, "c") { KeepPath = kept }),
            new Stub(CleanupCategory.OldDownloads, new CleanupItem(kept, false, 10, 10, W, CleanupCategory.OldDownloads, Risk.Review, "o")),
        };
        var groups = new RuleEngine(rules, new ProtectedPaths(F)).Run(new[] { Build(Root, ("f", 1, T0)) }, Ctx());
        var only = Assert.Single(Assert.Single(groups).Items);
        Assert.Equal(kept, only.Path);
    }

    [Fact]
    public void Clean_DuplicateWhoseKeptCopyIsGone_Skipped()
    {
        var fs = new FakeFileSystem();
        var copy = J(F.Downloads, "b.bin");
        fs.File(copy, 10, W);
        var item = new CleanupItem(copy, false, 10, 10, W, CleanupCategory.Duplicates, Risk.Review, "c") { KeepPath = J(F.Downloads, "a.bin") };
        var r = new Cleaner(fs, new FakeRecycleBin(), new ProtectedPaths(F), new NullLog()).Clean(new[] { item }, _ => false, default);
        Assert.Equal("Cópia mantida não encontrada", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public async Task DuplicatesRule_SetsKeepPath()
    {
        using var d = new TempDir();
        var c = new byte[1_048_576]; new Random(9).NextBytes(c);
        var oldest = d.File("a.bin", c, Now.AddDays(-9));
        d.File("b.bin", c, Now.AddDays(-1));
        var t = new SpaceTree(d.Path);
        await new Scanner(new PhysicalFileSystem()).ScanAsync(t, null, default);
        var item = Assert.Single(new DuplicatesRule().Analyze(new[] { t }, Ctx() with { FileSystem = new PhysicalFileSystem() }));
        Assert.Equal(oldest, item.KeepPath);
    }

    // #3 A folder modified since the scan is skipped (except constantly-rewritten caches).
    [Fact]
    public void Clean_TempFolderModifiedSinceScan_Skipped()
    {
        var fs = new FakeFileSystem();
        var dir = J(F.UserTemp, "setup-123");
        fs.Dir(dir, W.AddHours(1));
        var item = new CleanupItem(dir, true, 10, 10, W, CleanupCategory.Temp, Risk.Safe, "t");
        var r = new Cleaner(fs, new FakeRecycleBin(), new ProtectedPaths(F), new NullLog()).Clean(new[] { item }, _ => false, default);
        Assert.Equal("Alterado desde a análise", Assert.Single(r.Skipped).Reason);
    }

    // #4 Non-link reparse points (OneDrive cloud files/folders) are scanned, not skipped.
    [Fact]
    public async Task Scan_CloudReparseFolder_IsScanned()
    {
        var r = P("od");
        var fs = new FakeFileSystem().Dir(r);
        fs.Dir(Path.Combine(r, "OneDrive"), attrs: FileAttributes.Directory | FileAttributes.ReparsePoint);
        fs.File(Path.Combine(r, "OneDrive", "doc.docx"), 100, attrs: FileAttributes.ReparsePoint);
        var tree = new SpaceTree(r);
        await new Scanner(fs).ScanAsync(tree, null, default);
        Assert.Equal(100, tree.Root.AllocatedSize);
        Assert.False(tree.Find(Path.Combine(r, "OneDrive"))!.Flags.HasFlag(NodeFlags.ReparsePoint));
    }

    // #5 Removable/network drives have no Recycle Bin → app's own permanent-delete confirmation is used.
    [Theory]
    [InlineData(DriveType.Fixed, true)]
    [InlineData(DriveType.Removable, false)]
    [InlineData(DriveType.Network, false)]
    public void RecyclePolicy_OnlyFixedDrives(DriveType type, bool expected) =>
        Assert.Equal(expected, RecyclePolicy.DriveHasRecycleBin(type));

    // #7 A removed node no longer propagates sizes to its former ancestors.
    [Fact]
    public void RemoveChild_DetachesParent_LaterAddsDoNotLeak()
    {
        var tree = Build(Root, ("a/x", 10, T0));
        var a = tree.Find(P("a"))!;
        tree.Root.RemoveChild(a);
        a.AddChild(SpaceNode.CreateFile(P("a", "late"), 50, 50, T0));
        Assert.Null(a.Parent);
        Assert.Equal(0, tree.Root.AllocatedSize);
    }

    private sealed class Stub(CleanupCategory c, params CleanupItem[] items) : ICleanupRule
    {
        public CleanupCategory Category => c;
        public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx) => items;
    }
}
