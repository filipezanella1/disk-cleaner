using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Safety;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class CleanerTests : RuleTestBase
{
    private sealed class MemLog : ICleanupLog
    {
        public List<CleanupResult> Results { get; } = new();
        public void Write(CleanupResult result) => Results.Add(result);
    }

    private readonly FakeFileSystem _fs = new();
    private readonly FakeRecycleBin _bin = new();
    private readonly MemLog _log = new();
    private Cleaner Cleaner() => new(_fs, _bin, new ProtectedPaths(F), _log);
    private static readonly DateTime W = new(2026, 5, 1, 0, 0, 0, DateTimeKind.Utc);

    private CleanupItem FileItem(string path, long size = 10)
    {
        _fs.File(path, size, W);
        return new CleanupItem(path, false, size, size, W, CleanupCategory.LargeFiles, Risk.Review, "x");
    }

    private CleanupResult Clean(params CleanupItem[] items) => Cleaner().Clean(items, _ => false, default);

    public CleanerTests() => _bin.OnRecycled = p => _fs.Remove(p);

    [Fact]
    public void Clean_RecyclesValidItems_SumsBytesFreed()
    {
        var r = Clean(FileItem(J(F.Downloads, "a"), 10), FileItem(J(F.Downloads, "b"), 5));
        Assert.Equal(2, r.Removed.Count);
        Assert.Equal(15, r.BytesFreed);
        Assert.Empty(r.Skipped);
        Assert.Equal(2, _bin.Recycled.Count);
    }

    [Fact]
    public void Clean_ProtectedPath_RefusedEvenIfPassedIn()
    {
        var r = Clean(FileItem(J(F.ProgramFiles, "app.exe")));
        Assert.Empty(_bin.Recycled);
        Assert.Equal("Caminho protegido", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public void Clean_FileChangedSinceScan_Skipped()
    {
        var item = FileItem(J(F.Downloads, "a"));
        _fs.File(item.Path, 999, W); // grew
        var item2 = FileItem(J(F.Downloads, "b"));
        _fs.File(item2.Path, item2.LogicalSizeAtScan, W.AddMinutes(1)); // touched
        var r = Clean(item, item2);
        Assert.All(r.Skipped, s => Assert.Equal("Alterado desde a análise", s.Reason));
        Assert.Equal(2, r.Skipped.Count);
        Assert.Empty(_bin.Recycled);
    }

    [Fact]
    public void Clean_Missing_Skipped()
    {
        var item = new CleanupItem(J(F.Downloads, "gone"), false, 1, 1, W, CleanupCategory.LargeFiles, Risk.Review, "x");
        Assert.Equal("Não encontrado", Assert.Single(Clean(item).Skipped).Reason);
    }

    [Fact]
    public void Clean_Directory_OnlyChecksExistence()
    {
        var dir = J(F.UserTemp, "d");
        _fs.Dir(dir, W.AddDays(5)); // directory timestamp changed — still cleaned
        var item = new CleanupItem(dir, true, 100, 100, W, CleanupCategory.Temp, Risk.Safe, "x");
        Assert.Single(Clean(item).Removed);
    }

    [Fact]
    public void Clean_RecycleInUse_SkippedAndContinues()
    {
        _bin.Outcomes.Enqueue(RecycleOutcome.InUse);
        _bin.Outcomes.Enqueue(RecycleOutcome.Recycled);
        var r = Clean(FileItem(J(F.Downloads, "a")), FileItem(J(F.Downloads, "b")));
        Assert.Single(r.Removed);
        Assert.Equal("Em uso", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public void Clean_RecycleFailed_Skipped()
    {
        _bin.Outcomes.Enqueue(RecycleOutcome.Failed);
        Assert.Equal("Falha ao mover para a Lixeira", Assert.Single(Clean(FileItem(J(F.Downloads, "a"))).Skipped).Reason);
    }

    [Fact]
    public void Clean_Unrecyclable_AsksOnce_DeletesPermanentlyIfConfirmed()
    {
        _bin.CanRecycleFunc = p => !p.EndsWith("net");
        var calls = 0;
        IReadOnlyList<CleanupItem>? asked = null;
        var r = Cleaner().Clean(new[] { FileItem(J(F.Downloads, "a-net")), FileItem(J(F.Downloads, "b-net")), FileItem(J(F.Downloads, "c")) },
            items => { calls++; asked = items; return true; }, default);
        Assert.Equal(1, calls);
        Assert.Equal(2, asked!.Count);
        Assert.Equal(3, r.Removed.Count);
        Assert.Equal(2, _fs.Deleted.Count);
    }

    [Fact]
    public void Clean_Unrecyclable_SkipsIfDeclined()
    {
        _bin.CanRecycleFunc = _ => false;
        var r = Clean(FileItem(J(F.Downloads, "a")));
        Assert.Equal("Não cabe na Lixeira", Assert.Single(r.Skipped).Reason);
        Assert.Empty(_fs.Deleted);
    }

    [Fact]
    public void Clean_NothingUnrecyclable_DoesNotAsk()
    {
        var asked = false;
        Cleaner().Clean(new[] { FileItem(J(F.Downloads, "a")) }, _ => asked = true, default);
        Assert.False(asked);
    }

    [Fact]
    public void Clean_RecycleBinItem_EmptiesBin()
    {
        _bin.Size = 50;
        var rb = new CleanupItem(CleanupItem.RecycleBinPath, false, 50, 50, DateTime.MinValue, CleanupCategory.RecycleBin, Risk.Safe, "x");
        var r = Clean(rb);
        Assert.Equal(1, _bin.EmptyCalls);
        Assert.Equal(50, r.BytesFreed);
    }

    [Fact]
    public void Clean_Cancelled_RemainingSkipped()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var r = Cleaner().Clean(new[] { FileItem(J(F.Downloads, "a")) }, _ => false, cts.Token);
        Assert.Equal("Cancelado", Assert.Single(r.Skipped).Reason);
    }

    [Fact]
    public void Clean_WritesLog()
    {
        Clean(FileItem(J(F.Downloads, "a")));
        Assert.Single(_log.Results);
    }
}
