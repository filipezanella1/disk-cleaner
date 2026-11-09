namespace DiskCleaner.Core.Scanning;

public sealed record ScanProgress(long Files, long Bytes, string CurrentPath);
