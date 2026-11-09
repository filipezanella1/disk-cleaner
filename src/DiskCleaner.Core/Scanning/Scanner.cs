using System.Diagnostics;
using System.Threading.Channels;
using DiskCleaner.Core.IO;
using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Scanning;

/// <summary>Scans a directory tree in parallel into a <see cref="SpaceTree"/>. Never follows reparse points.
/// Cancellation does not throw: the tree is left partial with IsComplete = false.</summary>
public sealed class Scanner
{
    private static readonly TimeSpan ProgressInterval = TimeSpan.FromMilliseconds(100);
    private readonly IFileSystem _fs;

    public Scanner(IFileSystem fs) => _fs = fs;

    public async Task ScanAsync(SpaceTree tree, IProgress<ScanProgress>? progress, CancellationToken ct, int maxParallelism = 0)
    {
        tree.IsComplete = false;
        if (maxParallelism <= 0) maxParallelism = Environment.ProcessorCount;
        if (ct.IsCancellationRequested) return;

        var queue = Channel.CreateUnbounded<SpaceNode>();
        long pending = 1, files = 0, bytes = 0;
        long lastReportTicks = 0;
        var clock = Stopwatch.StartNew();
        queue.Writer.TryWrite(tree.Root);

        void Report(string current, bool force)
        {
            if (progress is null) return;
            var now = clock.Elapsed.Ticks;
            var last = Interlocked.Read(ref lastReportTicks);
            if (!force && now - last < ProgressInterval.Ticks) return;
            if (!force && Interlocked.CompareExchange(ref lastReportTicks, now, last) != last) return;
            progress.Report(new ScanProgress(Interlocked.Read(ref files), Interlocked.Read(ref bytes), current));
        }

        async Task Worker()
        {
            await foreach (var dir in queue.Reader.ReadAllAsync().ConfigureAwait(false))
            {
                try
                {
                    if (ct.IsCancellationRequested) { queue.Writer.TryComplete(); return; }
                    foreach (var sub in ScanDirectory(dir, ref files, ref bytes))
                    {
                        Interlocked.Increment(ref pending);
                        queue.Writer.TryWrite(sub);
                    }
                    Report(dir.FullPath, false);
                }
                finally
                {
                    if (Interlocked.Decrement(ref pending) == 0) queue.Writer.TryComplete();
                }
            }
        }

        var workers = Enumerable.Range(0, maxParallelism).Select(_ => Task.Run(Worker)).ToArray();
        await Task.WhenAll(workers).ConfigureAwait(false);

        Report(tree.Root.FullPath, true);
        tree.IsComplete = !ct.IsCancellationRequested;
    }

    private List<SpaceNode> ScanDirectory(SpaceNode dir, ref long files, ref long bytes)
    {
        var subdirs = new List<SpaceNode>();
        List<FsEntry> entries;
        try
        {
            entries = _fs.Enumerate(dir.FullPath).ToList();
        }
        catch (UnauthorizedAccessException)
        {
            dir.Flags |= NodeFlags.AccessDenied;
            return subdirs;
        }
        catch (Exception e) when (e is IOException or System.Security.SecurityException)
        {
            return subdirs; // vanished or unreadable: skip
        }

        foreach (var e in entries)
        {
            if (e.IsReparsePoint)
            {
                var link = e.IsDirectory
                    ? SpaceNode.CreateDirectory(e.FullPath, e.LastWriteUtc)
                    : SpaceNode.CreateFile(e.FullPath, 0, 0, e.LastWriteUtc);
                link.Flags = NodeFlags.ReparsePoint;
                dir.AddChild(link);
            }
            else if (e.IsDirectory)
            {
                var sub = SpaceNode.CreateDirectory(e.FullPath, e.LastWriteUtc);
                dir.AddChild(sub);
                subdirs.Add(sub);
            }
            else
            {
                long allocated;
                try { allocated = _fs.GetAllocatedSize(e); }
                catch (IOException) { allocated = e.Length; }
                dir.AddChild(SpaceNode.CreateFile(e.FullPath, e.Length, allocated, e.LastWriteUtc));
                Interlocked.Increment(ref files);
                Interlocked.Add(ref bytes, allocated);
            }
        }
        return subdirs;
    }
}
