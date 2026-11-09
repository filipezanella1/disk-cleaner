using DiskCleaner.Core.Rules;

namespace DiskCleaner.Core.Cleaning;

public sealed record SkippedItem(CleanupItem Item, string Reason);

public sealed record CleanupResult(IReadOnlyList<CleanupItem> Removed, IReadOnlyList<SkippedItem> Skipped, long BytesFreed);
