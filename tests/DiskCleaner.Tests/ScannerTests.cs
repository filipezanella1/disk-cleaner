using DiskCleaner.Core.IO;
using DiskCleaner.Core.Scanning;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class ScannerTests
{
    private static readonly string R = Path.Combine(TestTrees.Root, "r");

    private static async Task<SpaceTree> Scan(IFileSystem fs, string root, CancellationToken ct = default, IProgress<ScanProgress>? p = null)
    {
        var tree = new SpaceTree(root);
        await new Scanner(fs).ScanAsync(tree, p, ct);
        return tree;
    }

    [Fact]
    public async Task Scan_RealDirectory_SumsSizes()
    {
        using var d = new TempDir();
        d.File("a/x.bin", 1000); d.File("a/b/y.bin", 24); d.File("z.bin", 1);
        var tree = await Scan(new PhysicalFileSystem(), d.Path);
        Assert.True(tree.IsComplete);
        Assert.Equal(1025, tree.Root.LogicalSize);
        Assert.Equal(1025, tree.Root.AllocatedSize);
        Assert.Equal(3, tree.EnumerateFiles().Count());
    }

    [Fact]
    public async Task Scan_Symlink_NotFollowed_MarkedReparse()
    {
        using var d = new TempDir();
        d.File("real/big.bin", 5000);
        Directory.CreateSymbolicLink(Path.Combine(d.Path, "link"), Path.Combine(d.Path, "real"));
        var tree = await Scan(new PhysicalFileSystem(), d.Path);
        Assert.Equal(5000, tree.Root.AllocatedSize);
        var link = tree.Find(Path.Combine(d.Path, "link"))!;
        Assert.True(link.Flags.HasFlag(NodeFlags.ReparsePoint));
        Assert.Equal(0, link.AllocatedSize);
        Assert.Empty(link.GetChildrenSnapshot());
    }

    [Fact]
    public async Task Scan_AccessDenied_MarksNodeAndContinues()
    {
        var fs = new FakeFileSystem().Dir(R).Dir(Path.Combine(R, "locked")).File(Path.Combine(R, "ok", "f"), 10);
        fs.EnumerateThrows[Path.Combine(R, "locked")] = new UnauthorizedAccessException();
        var tree = await Scan(fs, R);
        Assert.True(tree.IsComplete);
        Assert.True(tree.Find(Path.Combine(R, "locked"))!.Flags.HasFlag(NodeFlags.AccessDenied));
        Assert.Equal(10, tree.Root.AllocatedSize);
    }

    [Fact]
    public async Task Scan_RootAccessDenied_MarksRoot()
    {
        var fs = new FakeFileSystem().Dir(R);
        fs.EnumerateThrows[R] = new UnauthorizedAccessException();
        var tree = await Scan(fs, R);
        Assert.True(tree.Root.Flags.HasFlag(NodeFlags.AccessDenied));
    }

    [Fact]
    public async Task Scan_EntryVanishes_ContinuesScan()
    {
        var fs = new FakeFileSystem().Dir(R).Dir(Path.Combine(R, "gone")).File(Path.Combine(R, "ok", "f"), 10);
        fs.EnumerateThrows[Path.Combine(R, "gone")] = new DirectoryNotFoundException();
        var tree = await Scan(fs, R);
        Assert.True(tree.IsComplete);
        Assert.Equal(10, tree.Root.AllocatedSize);
    }

    [Fact]
    public async Task Scan_OfflineFile_CountsZeroAllocated()
    {
        var fs = new FakeFileSystem().Dir(R).File(Path.Combine(R, "cloud.bin"), 1000, attrs: FileAttributes.Offline);
        var tree = await Scan(fs, R);
        var n = tree.Find(Path.Combine(R, "cloud.bin"))!;
        Assert.Equal(1000, n.LogicalSize);
        Assert.Equal(0, n.AllocatedSize);
    }

    [Fact]
    public async Task Scan_Cancelled_ReturnsPartialIncompleteTree()
    {
        var fs = new FakeFileSystem().Dir(R).File(Path.Combine(R, "a", "f"), 10);
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var tree = await Scan(fs, R, cts.Token);
        Assert.False(tree.IsComplete);
    }

    [Fact]
    public async Task Scan_ManyDirectories_AllCounted()
    {
        var fs = new FakeFileSystem().Dir(R);
        for (int i = 0; i < 200; i++) fs.File(Path.Combine(R, "d" + (i % 20), "s" + (i % 7), "f" + i), 3);
        var tree = await Scan(fs, R);
        Assert.Equal(600, tree.Root.AllocatedSize);
        Assert.Equal(200, tree.EnumerateFiles().Count());
    }

    [Fact]
    public async Task Scan_ReportsProgress()
    {
        var fs = new FakeFileSystem().Dir(R).File(Path.Combine(R, "a", "f"), 10);
        var reports = new List<ScanProgress>();
        var p = new SyncProgress(reports.Add);
        await Scan(fs, R, default, p);
        Assert.NotEmpty(reports);
        Assert.Equal(1, reports[^1].Files);
        Assert.Equal(10, reports[^1].Bytes);
    }

    private sealed class SyncProgress(Action<ScanProgress> a) : IProgress<ScanProgress>
    {
        public void Report(ScanProgress value) { lock (this) a(value); }
    }
}
