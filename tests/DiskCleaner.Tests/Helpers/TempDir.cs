namespace DiskCleaner.Tests.Helpers;

public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dc-test-" + Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(Path);

    public string File(string rel, long size, DateTime? lastWriteUtc = null) =>
        File(rel, new byte[size], lastWriteUtc);

    public string File(string rel, byte[] content, DateTime? lastWriteUtc = null)
    {
        var full = System.IO.Path.Combine(Path, rel.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        System.IO.File.WriteAllBytes(full, content);
        if (lastWriteUtc is { } lw) System.IO.File.SetLastWriteTimeUtc(full, lw);
        return full;
    }

    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { /* best effort */ }
    }
}
