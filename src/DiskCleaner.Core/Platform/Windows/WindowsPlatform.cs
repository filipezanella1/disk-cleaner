using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DiskCleaner.Core.Safety;

namespace DiskCleaner.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public static class WindowsPlatform
{
    /// <summary>Wires Windows-only lookups (e.g. the real Downloads folder) into the Core.</summary>
    public static void Initialize() => KnownFolders.DownloadsResolver = GetDownloadsFolder;

    public static string? GetDownloadsFolder()
    {
        if (NativeMethods.SHGetKnownFolderPath(NativeMethods.FOLDERID_Downloads, 0, IntPtr.Zero, out var ptr) != 0) return null;
        try { return Marshal.PtrToStringUni(ptr); }
        finally { Marshal.FreeCoTaskMem(ptr); }
    }
}
