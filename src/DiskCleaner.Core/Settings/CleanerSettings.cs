using DiskCleaner.Core.Rules;

namespace DiskCleaner.Core.Settings;

public sealed record CleanerSettings
{
    public long LargeFileThresholdBytes { get; init; } = 524_288_000;
    public int OldDownloadDays { get; init; } = 90;
    public long DuplicateMinBytes { get; init; } = 1_048_576;
    public TimeSpan TempMinAge { get; init; } = TimeSpan.FromHours(24);
    public HashSet<CleanupCategory> DisabledCategories { get; init; } = new();
}
