namespace DiskCleaner.Core.IO;

/// <param name="IsLink">True only for name-surrogate reparse points (symlinks, junctions, mount points), which are
/// never followed. Other reparse points (e.g. OneDrive cloud files) are scanned normally.</param>
public sealed record FsEntry(string Name, string FullPath, bool IsDirectory, long Length, DateTime LastWriteUtc, FileAttributes Attributes, bool IsLink = false)
{
    public bool IsReparsePoint => (Attributes & FileAttributes.ReparsePoint) != 0;
}
