namespace DiskCleaner.Core.IO;

public interface IFileSystem
{
    /// <summary>Direct children of a directory. May throw UnauthorizedAccessException / IOException.</summary>
    IEnumerable<FsEntry> Enumerate(string directory);

    /// <summary>Bytes the file actually occupies on disk.</summary>
    long GetAllocatedSize(FsEntry file);

    /// <summary>Current state of a path, or null if it does not exist.</summary>
    FsEntry? GetEntry(string path);

    Stream OpenRead(string path);

    void DeletePermanently(string path);
}
