namespace DiskCleaner.Core.IO;

public sealed record FsEntry(string Name, string FullPath, bool IsDirectory, long Length, DateTime LastWriteUtc, FileAttributes Attributes)
{
    public bool IsReparsePoint => (Attributes & FileAttributes.ReparsePoint) != 0;
}
