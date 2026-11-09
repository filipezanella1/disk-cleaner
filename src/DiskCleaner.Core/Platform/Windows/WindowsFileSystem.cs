using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DiskCleaner.Core.IO;

namespace DiskCleaner.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class WindowsFileSystem : PhysicalFileSystem
{
    private const int LongPathThreshold = 240;

    internal static string ToLongPath(string path) =>
        path.Length <= LongPathThreshold || path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path
        : path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..]
        : @"\\?\" + path;

    private static string StripPrefix(string path) =>
        path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase) ? @"\\" + path[8..]
        : path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..]
        : path;

    public override IEnumerable<FsEntry> Enumerate(string directory)
    {
        var entries = base.Enumerate(ToLongPath(directory));
        return entries.Select(e => e with { FullPath = StripPrefix(e.FullPath) });
    }

    public override FsEntry? GetEntry(string path)
    {
        var e = base.GetEntry(ToLongPath(path));
        return e is null ? null : e with { FullPath = StripPrefix(e.FullPath) };
    }

    public override Stream OpenRead(string path) => base.OpenRead(ToLongPath(path));

    public override void DeletePermanently(string path) => base.DeletePermanently(ToLongPath(path));

    public override long GetAllocatedSize(FsEntry file)
    {
        if (IsCloudPlaceholder(file.Attributes)) return 0;
        var low = NativeMethods.GetCompressedFileSize(ToLongPath(file.FullPath), out var high);
        if (low == NativeMethods.INVALID_FILE_SIZE && Marshal.GetLastWin32Error() != 0) return file.Length;
        return ((long)high << 32) | low;
    }
}
