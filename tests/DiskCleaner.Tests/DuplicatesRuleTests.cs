using DiskCleaner.Core.IO;
using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Scanning;
using DiskCleaner.Core.Settings;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class DuplicatesRuleTests : RuleTestBase
{
    private const int MB = 1_048_576;

    private static byte[] Content(int size, byte seed)
    {
        var b = new byte[size];
        new Random(seed).NextBytes(b);
        return b;
    }

    private static async Task<List<CleanupItem>> Run(IFileSystem fs, params string[] roots)
    {
        var trees = new List<SpaceTree>();
        foreach (var r in roots) { var t = new SpaceTree(r); await new Scanner(fs).ScanAsync(t, null, default); trees.Add(t); }
        return new DuplicatesRule().Analyze(trees, Ctx(fs: null) with { FileSystem = fs }).ToList();
    }

    [Fact]
    public async Task IdenticalFiles_KeepsOldest_SuggestsOthers()
    {
        using var d = new TempDir();
        var c = Content(2 * MB, 1);
        var oldest = d.File("a/one.bin", c, Now.AddDays(-30));
        d.File("b/two.bin", c, Now.AddDays(-10));
        d.File("c/three.bin", c, Now.AddDays(-1));
        var items = await Run(new PhysicalFileSystem(), d.Path);
        Assert.Equal(2, items.Count);
        Assert.DoesNotContain(items, i => i.Path == oldest);
        Assert.All(items, i =>
        {
            Assert.Equal($"Cópia de {oldest}", i.Reason);
            Assert.Equal(Risk.Review, i.Risk);
            Assert.Equal(CleanupCategory.Duplicates, i.Category);
        });
    }

    [Fact]
    public async Task SameSizeDifferentContent_NotDuplicates()
    {
        using var d = new TempDir();
        d.File("a.bin", Content(2 * MB, 1)); d.File("b.bin", Content(2 * MB, 2));
        Assert.Empty(await Run(new PhysicalFileSystem(), d.Path));
    }

    [Fact]
    public async Task DifferOnlyInMiddle_NotDuplicates()
    {
        using var d = new TempDir();
        var c1 = Content(3 * MB, 1);
        var c2 = (byte[])c1.Clone();
        c2[3 * MB / 2] ^= 0xFF;
        d.File("a.bin", c1); d.File("b.bin", c2);
        Assert.Empty(await Run(new PhysicalFileSystem(), d.Path));
    }

    [Fact]
    public async Task BelowMinSize_Ignored()
    {
        using var d = new TempDir();
        var c = Content(MB - 1, 1);
        d.File("a.bin", c); d.File("b.bin", c);
        Assert.Empty(await Run(new PhysicalFileSystem(), d.Path));
    }

    [Fact]
    public async Task UnreadableFile_Excluded()
    {
        var r = TestTrees.P("dups");
        var c = Content(2 * MB, 3);
        var fs = new FakeFileSystem().Dir(r)
            .File(Path.Combine(r, "1"), c, Now.AddDays(-3))
            .File(Path.Combine(r, "2"), c, Now.AddDays(-2))
            .File(Path.Combine(r, "3"), c, Now.AddDays(-1));
        fs.OpenReadThrows[Path.Combine(r, "2")] = new IOException("locked");
        var i = Assert.Single(await Run(fs, r));
        Assert.Equal(Path.Combine(r, "3"), i.Path);
    }

    [Fact]
    public async Task AcrossTwoTrees_Detected()
    {
        using var d1 = new TempDir();
        using var d2 = new TempDir();
        var c = Content(MB, 4);
        d1.File("x.bin", c, Now.AddDays(-5));
        var copy = d2.File("y.bin", c, Now.AddDays(-1));
        Assert.Equal(copy, Assert.Single(await Run(new PhysicalFileSystem(), d1.Path, d2.Path)).Path);
    }

    [Fact]
    public async Task SameDate_KeepsShortestPath()
    {
        using var d = new TempDir();
        var c = Content(MB, 5);
        var t = Now.AddDays(-1);
        d.File("deep/er/a.bin", c, t);
        d.File("a.bin", c, t);
        Assert.EndsWith(Path.Combine("deep", "er", "a.bin"), Assert.Single(await Run(new PhysicalFileSystem(), d.Path)).Path);
    }
}
