using DiskCleaner.Core.IO;
using DiskCleaner.Core.Rules;

namespace DiskCleaner.Core.Cleaning;

/// <summary>For items skipped as "in use": finds the programs holding them, asks the user, closes those programs
/// (gracefully first, then forcibly) and retries. System processes and this app are never closed.</summary>
public sealed class LockedItemsHandler
{
    public static readonly TimeSpan GracePeriod = TimeSpan.FromSeconds(5);
    private const int MaxFilesPerFolder = 2000;
    private const string CannotCloseSuffix = " (não pode ser fechado)";

    private readonly ILockingProcessFinder _finder;
    private readonly IProcessCloser _closer;
    private readonly IFileSystem _fs;
    private readonly int _ownProcessId;

    public LockedItemsHandler(ILockingProcessFinder finder, IProcessCloser closer, IFileSystem fs, int ownProcessId)
    {
        _finder = finder;
        _closer = closer;
        _fs = fs;
        _ownProcessId = ownProcessId;
    }

    public bool CanClose(LockingProcess p) => !p.IsSystem && p.Id != _ownProcessId && !ProtectedProcesses.IsProtectedName(p.Name);

    public CleanupResult CloseAndRetry(
        CleanupResult result,
        Cleaner cleaner,
        Func<IReadOnlyList<LockingProcess>, bool> confirmClose,
        Func<IReadOnlyList<CleanupItem>, bool> confirmPermanentDelete,
        CancellationToken ct)
    {
        var inUse = result.Skipped.Where(s => s.Reason == Cleaner.ReasonInUse).Select(s => s.Item).ToList();
        if (inUse.Count == 0) return result;

        var lockers = new Dictionary<CleanupItem, IReadOnlyList<LockingProcess>>(ReferenceEqualityComparer.Instance);
        foreach (var item in inUse)
        {
            if (ct.IsCancellationRequested) break;
            lockers[item] = SafeFind(item);
        }

        var closable = lockers.Values.SelectMany(l => l).Where(CanClose).DistinctBy(p => p.Id).ToList();
        var closedIds = new HashSet<int>();
        CleanupResult? retry = null;
        var retried = new HashSet<CleanupItem>(ReferenceEqualityComparer.Instance);

        if (closable.Count > 0 && !ct.IsCancellationRequested && confirmClose(closable))
        {
            var failed = _closer.CloseAll(closable, GracePeriod).Select(p => p.Id).ToHashSet();
            closedIds.UnionWith(closable.Select(p => p.Id).Where(id => !failed.Contains(id)));
            foreach (var (item, procs) in lockers)
                if (procs.Count > 0 && procs.All(p => closedIds.Contains(p.Id))) retried.Add(item);
            if (retried.Count > 0) retry = cleaner.Clean(retried.ToList(), confirmPermanentDelete, ct);
        }

        var removed = result.Removed.Concat(retry?.Removed ?? Array.Empty<CleanupItem>()).ToList();
        var skipped = result.Skipped.Where(s => !retried.Contains(s.Item))
            .Concat(retry?.Skipped ?? Array.Empty<SkippedItem>())
            .Select(s => s.Reason == Cleaner.ReasonInUse && lockers.TryGetValue(s.Item, out var procs) && procs.Count > 0
                ? s with { Reason = DescribeLockers(procs) }
                : s)
            .ToList();
        return new CleanupResult(removed, skipped, removed.Sum(i => i.Size));
    }

    private string DescribeLockers(IReadOnlyList<LockingProcess> procs)
    {
        var names = string.Join(", ", procs.Select(p => p.DisplayName).Distinct(StringComparer.OrdinalIgnoreCase));
        return $"{Cleaner.ReasonInUse} por {names}" + (procs.All(p => !CanClose(p)) ? CannotCloseSuffix : "");
    }

    private IReadOnlyList<LockingProcess> SafeFind(CleanupItem item)
    {
        try
        {
            var files = item.IsDirectory ? ListFiles(item.Path) : new List<string> { item.Path };
            return files.Count == 0 ? Array.Empty<LockingProcess>() : _finder.FindLockers(files);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or InvalidOperationException
                                      or System.ComponentModel.Win32Exception)
        {
            return Array.Empty<LockingProcess>();
        }
    }

    private List<string> ListFiles(string directory)
    {
        var files = new List<string>();
        var stack = new Stack<string>();
        stack.Push(directory);
        while (stack.Count > 0 && files.Count < MaxFilesPerFolder)
        {
            IEnumerable<FsEntry> entries;
            try { entries = _fs.Enumerate(stack.Pop()); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { continue; }
            foreach (var e in entries)
            {
                if (e.IsLink) continue;
                if (e.IsDirectory) stack.Push(e.FullPath);
                else if (files.Count < MaxFilesPerFolder) files.Add(e.FullPath);
            }
        }
        return files;
    }
}
