namespace DiskCleaner.Core.Cleaning;

public sealed class FileCleanupLog : ICleanupLog
{
    private readonly string _logDirectory;
    private readonly Func<DateTime> _nowLocal;

    public FileCleanupLog(string logDirectory, Func<DateTime> nowLocal)
    {
        _logDirectory = logDirectory;
        _nowLocal = nowLocal;
    }

    public void Write(CleanupResult result)
    {
        Directory.CreateDirectory(_logDirectory);
        var file = Path.Combine(_logDirectory, $"cleanup-{_nowLocal():yyyyMMdd-HHmmss}.log");
        var lines = result.Removed.Select(i => $"REMOVED\t{i.Size}\t{i.Path}")
            .Concat(result.Skipped.Select(s => $"SKIPPED\t{s.Reason}\t{s.Item.Path}"));
        File.AppendAllLines(file, lines);
    }
}
