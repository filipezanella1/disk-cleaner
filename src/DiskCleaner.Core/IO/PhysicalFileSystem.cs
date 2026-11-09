namespace DiskCleaner.Core.IO;

public class PhysicalFileSystem : IFileSystem
{
    /// <summary>FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS (cloud "online-only" files).</summary>
    public const FileAttributes RecallOnDataAccess = (FileAttributes)0x00400000;

    private static readonly EnumerationOptions Options = new()
    {
        IgnoreInaccessible = false,
        AttributesToSkip = 0,
        RecurseSubdirectories = false,
        ReturnSpecialDirectories = false,
    };

    public virtual IEnumerable<FsEntry> Enumerate(string directory)
    {
        var list = new List<FsEntry>();
        foreach (var info in new DirectoryInfo(directory).EnumerateFileSystemInfos("*", Options))
            list.Add(ToEntry(info));
        return list;
    }

    public virtual long GetAllocatedSize(FsEntry file) =>
        IsCloudPlaceholder(file.Attributes) ? 0 : file.Length;

    public static bool IsCloudPlaceholder(FileAttributes a) =>
        (a & (FileAttributes.Offline | RecallOnDataAccess)) != 0;

    public virtual FsEntry? GetEntry(string path)
    {
        var fi = new FileInfo(path);
        if (fi.Exists) return ToEntry(fi);
        var di = new DirectoryInfo(path);
        return di.Exists ? ToEntry(di) : null;
    }

    public virtual Stream OpenRead(string path) =>
        new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete, 81920, FileOptions.SequentialScan);

    public virtual void DeletePermanently(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
        else File.Delete(path);
    }

    protected static FsEntry ToEntry(FileSystemInfo info) => info switch
    {
        FileInfo f => new FsEntry(f.Name, f.FullName, false, SafeLength(f), f.LastWriteTimeUtc, f.Attributes, IsLink(f)),
        _ => new FsEntry(info.Name, info.FullName, true, 0, info.LastWriteTimeUtc, info.Attributes, IsLink(info)),
    };

    private static bool IsLink(FileSystemInfo info)
    {
        if ((info.Attributes & FileAttributes.ReparsePoint) == 0) return false;
        try { return info.LinkTarget is not null; }
        catch (IOException) { return true; } // unreadable reparse data: be safe, don't follow
        catch (UnauthorizedAccessException) { return true; }
    }

    private static long SafeLength(FileInfo f)
    {
        try { return f.Length; } catch (IOException) { return 0; }
    }
}
