using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Safety;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class LockedItemsHandlerTests : RuleTestBase
{
    private static readonly DateTime W = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private sealed class NullLog : ICleanupLog { public void Write(CleanupResult r) { } }

    private sealed class FakeFinder : ILockingProcessFinder
    {
        public Dictionary<string, List<LockingProcess>> Locks { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<IReadOnlyList<string>> Calls { get; } = new();
        public IReadOnlyList<LockingProcess> FindLockers(IReadOnlyList<string> files)
        {
            Calls.Add(files);
            return files.SelectMany(f => Locks.TryGetValue(f, out var l) ? l : new List<LockingProcess>())
                .DistinctBy(p => p.Id).ToList();
        }
    }

    private sealed class FakeCloser : IProcessCloser
    {
        public List<LockingProcess> Closed { get; } = new();
        public HashSet<int> Refuse { get; } = new();
        public Action<LockingProcess>? OnClosed { get; set; }
        public IReadOnlyList<LockingProcess> CloseAll(IReadOnlyList<LockingProcess> processes, TimeSpan grace)
        {
            var failed = new List<LockingProcess>();
            foreach (var p in processes)
            {
                if (Refuse.Contains(p.Id)) { failed.Add(p); continue; }
                Closed.Add(p);
                OnClosed?.Invoke(p);
            }
            return failed;
        }
    }

    private readonly FakeFileSystem _fs = new();
    private readonly FakeRecycleBin _bin = new();
    private readonly FakeFinder _finder = new();
    private readonly FakeCloser _closer = new();
    private readonly HashSet<string> _locked = new(StringComparer.OrdinalIgnoreCase);

    public LockedItemsHandlerTests()
    {
        // A locked path reports InUse until the process holding it is closed.
        _bin.OnRecycled = p => _fs.Remove(p);
        _closer.OnClosed = p => { foreach (var path in p.Paths) _locked.Remove(path); };
    }

    private Cleaner NewCleaner() => new(_fs, new LockAwareBin(_bin, _locked), new ProtectedPaths(F), new NullLog());
    private LockedItemsHandler Handler(int ownPid = -1) => new(_finder, _closer, _fs, ownPid);

    private sealed class LockAwareBin(FakeRecycleBin inner, HashSet<string> locked) : IRecycleBin
    {
        public long QuerySize() => inner.QuerySize();
        public bool CanRecycle(string path) => true;
        public RecycleOutcome Recycle(string path) => locked.Contains(path) ? RecycleOutcome.InUse : inner.Recycle(path);
        public bool Empty() => inner.Empty();
    }

    private CleanupItem FileItem(string name, long size = 10)
    {
        var path = J(F.Downloads, name);
        _fs.File(path, size, W);
        return new CleanupItem(path, false, size, size, W, CleanupCategory.LargeFiles, Risk.Review, "x");
    }

    private LockingProcess Proc(int id, string name, params string[] paths) =>
        new(id, name, name, null, IsSystem: false, Paths: paths);

    private void Lock(LockingProcess p)
    {
        foreach (var path in p.Paths) { _locked.Add(path); (_finder.Locks.TryGetValue(path, out var l) ? l : _finder.Locks[path] = new()).Add(p); }
    }

    private CleanupResult Run(CleanupItem[] items, Func<IReadOnlyList<LockingProcess>, bool> confirm, int ownPid = -1)
    {
        var cleaner = NewCleaner();
        var first = cleaner.Clean(items, _ => false, default);
        return Handler(ownPid).CloseAndRetry(first, cleaner, confirm, _ => false, default);
    }

    [Fact]
    public void NoInUse_ReturnsSameResult_DoesNotAsk()
    {
        var asked = false;
        var r = Run(new[] { FileItem("a") }, _ => asked = true);
        Assert.False(asked);
        Assert.Empty(_finder.Calls);
        Assert.Single(r.Removed);
    }

    [Fact]
    public void InUse_AsksWithLockingApps_ClosesThemAndRetries()
    {
        var a = FileItem("a", 10); var b = FileItem("b", 5);
        var chrome = Proc(100, "Chrome", a.Path);
        Lock(chrome);
        IReadOnlyList<LockingProcess>? offered = null;
        var r = Run(new[] { a, b }, procs => { offered = procs; return true; });
        Assert.Equal(new[] { 100 }, offered!.Select(p => p.Id));
        Assert.Equal(new[] { 100 }, _closer.Closed.Select(p => p.Id));
        Assert.Equal(2, r.Removed.Count);
        Assert.Empty(r.Skipped);
        Assert.Equal(15, r.BytesFreed);
    }

    [Fact]
    public void Declined_DoesNotClose_ResultUnchanged()
    {
        var a = FileItem("a");
        Lock(Proc(100, "Chrome", a.Path));
        var r = Run(new[] { a }, _ => false);
        Assert.Empty(_closer.Closed);
        Assert.Equal("Em uso por Chrome", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public void ProtectedProcesses_NeverOffered_NorClosed()
    {
        var a = FileItem("a"); var b = FileItem("b"); var c = FileItem("c");
        Lock(Proc(1, "explorer", a.Path));
        Lock(new LockingProcess(2, "SomeService", "Some Service", null, IsSystem: true, Paths: new[] { b.Path }));
        Lock(Proc(3, "DiskCleaner", c.Path));
        var asked = false;
        var r = Run(new[] { a, b, c }, _ => asked = true, ownPid: 3);
        Assert.False(asked);
        Assert.Empty(_closer.Closed);
        Assert.Equal(3, r.Skipped.Count);
        Assert.All(r.Skipped, s => Assert.EndsWith("(não pode ser fechado)", s.Reason));
    }

    [Fact]
    public void ProcessFailsToClose_ItemStaysInUse()
    {
        var a = FileItem("a");
        Lock(Proc(100, "Teimoso", a.Path));
        _closer.Refuse.Add(100);
        var r = Run(new[] { a }, _ => true);
        Assert.Empty(r.Removed);
        Assert.Equal("Em uso por Teimoso", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public void InUseWithNoKnownLocker_KeepsPlainReason_DoesNotAsk()
    {
        var a = FileItem("a");
        _locked.Add(a.Path); // locked, but the finder cannot tell by whom
        var asked = false;
        var r = Run(new[] { a }, _ => asked = true);
        Assert.False(asked);
        Assert.Equal("Em uso", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public void DirectoryItem_FinderReceivesFilesInside()
    {
        var dir = J(F.UserTemp, "d");
        _fs.Dir(dir, W).File(J(dir, "x.tmp"), 1, W).File(J(dir, "sub", "y.tmp"), 1, W);
        _fs.Dir(dir, W); // keep the folder timestamp as scanned
        _locked.Add(dir);
        var item = new CleanupItem(dir, true, 2, 2, W, CleanupCategory.Temp, Risk.Safe, "t");
        Run(new[] { item }, _ => false);
        var files = Assert.Single(_finder.Calls);
        Assert.Contains(J(dir, "x.tmp"), files);
        Assert.Contains(J(dir, "sub", "y.tmp"), files);
    }

    [Theory]
    [InlineData("explorer", true)]
    [InlineData("svchost", true)]
    [InlineData("CSRSS", true)]
    [InlineData("chrome", false)]
    [InlineData("Discord", false)]
    public void ProtectedProcessNames(string name, bool expected) =>
        Assert.Equal(expected, ProtectedProcesses.IsProtectedName(name));
}
