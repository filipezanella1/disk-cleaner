using DiskCleaner.Core.IO;

namespace DiskCleaner.Tests.Helpers;

/// <summary>In-memory file system. Paths use the host separator.</summary>
public sealed class FakeFileSystem : IFileSystem
{
    private readonly Dictionary<string, FsEntry> _entries = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, byte[]> _content = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Exception> EnumerateThrows { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, Exception> OpenReadThrows { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<string> Deleted { get; } = new();

    public FakeFileSystem Dir(string path, DateTime? lastWriteUtc = null)
    {
        _entries[path] = new FsEntry(Path.GetFileName(path), path, true, 0, lastWriteUtc ?? TestTrees.T0, FileAttributes.Directory);
        var parent = Path.GetDirectoryName(path);
        if (parent is not null && parent != path && !_entries.ContainsKey(parent) && Path.GetPathRoot(path) != path) Dir(parent);
        return this;
    }

    public FakeFileSystem File(string path, long size, DateTime? lastWriteUtc = null, FileAttributes attrs = FileAttributes.Normal)
    {
        var parent = Path.GetDirectoryName(path)!;
        if (!_entries.ContainsKey(parent)) Dir(parent);
        _entries[path] = new FsEntry(Path.GetFileName(path), path, false, size, lastWriteUtc ?? TestTrees.T0, attrs);
        return this;
    }

    public FakeFileSystem File(string path, byte[] content, DateTime? lastWriteUtc = null)
    {
        File(path, content.LongLength, lastWriteUtc);
        _content[path] = content;
        return this;
    }

    public void Remove(string path) => _entries.Remove(path);

    public IEnumerable<FsEntry> Enumerate(string directory)
    {
        if (EnumerateThrows.TryGetValue(directory, out var ex)) throw ex;
        if (!_entries.TryGetValue(directory, out var d) || !d.IsDirectory) throw new DirectoryNotFoundException(directory);
        return _entries.Values.Where(e => string.Equals(Path.GetDirectoryName(e.FullPath), directory, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public long GetAllocatedSize(FsEntry file) =>
        (file.Attributes & (FileAttributes.Offline | (FileAttributes)0x00400000)) != 0 ? 0 : file.Length;

    public FsEntry? GetEntry(string path) => _entries.TryGetValue(path, out var e) ? e : null;

    public Stream OpenRead(string path)
    {
        if (OpenReadThrows.TryGetValue(path, out var ex)) throw ex;
        if (_content.TryGetValue(path, out var c)) return new MemoryStream(c, false);
        if (_entries.TryGetValue(path, out var e) && !e.IsDirectory) return new MemoryStream(new byte[e.Length], false);
        throw new FileNotFoundException(path);
    }

    public void DeletePermanently(string path)
    {
        Deleted.Add(path);
        foreach (var k in _entries.Keys.Where(k => k.Equals(path, StringComparison.OrdinalIgnoreCase) ||
                     k.StartsWith(path + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)).ToList())
            _entries.Remove(k);
    }
}
