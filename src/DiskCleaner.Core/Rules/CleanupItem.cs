using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public enum Risk { Safe, Review }

/// <summary>Order defines display order and priority when two rules suggest the same path.</summary>
public enum CleanupCategory { Temp, BrowserCache, AppCache, LogsDumps, RecycleBin, LargeFiles, Duplicates, OldDownloads, DevFolders }

/// <param name="Size">Allocated bytes (space freed).</param>
public sealed record CleanupItem(
    string Path,
    bool IsDirectory,
    long Size,
    long LogicalSizeAtScan,
    DateTime LastWriteUtcAtScan,
    CleanupCategory Category,
    Risk Risk,
    string Reason)
{
    public const string RecycleBinPath = "::RecycleBin";

    /// <summary>For duplicates: the copy that is kept. Cleaning is refused if it no longer exists.</summary>
    public string? KeepPath { get; init; }

    public bool IsRecycleBin => Path == RecycleBinPath;

    public static CleanupItem FromNode(SpaceNode n, CleanupCategory c, Risk r, string reason) =>
        new(n.FullPath, n.IsDirectory, n.AllocatedSize, n.LogicalSize, n.LastWriteUtc, c, r, reason);
}
