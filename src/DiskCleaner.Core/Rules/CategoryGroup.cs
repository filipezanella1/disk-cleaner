namespace DiskCleaner.Core.Rules;

public sealed record CategoryGroup(CleanupCategory Category, IReadOnlyList<CleanupItem> Items, long TotalSize);
