using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using DiskCleaner.Core.Cleaning;

namespace DiskCleaner.Core.Platform.Windows;

[SupportedOSPlatform("windows")]
public sealed class ShellRecycleBin : IRecycleBin
{
    private const int MaxShellPath = 259;
    private const int E_UNEXPECTED = unchecked((int)0x8000FFFF);

    public long QuerySize()
    {
        var info = new NativeMethods.SHQUERYRBINFO { cbSize = (uint)Marshal.SizeOf<NativeMethods.SHQUERYRBINFO>() };
        return NativeMethods.SHQueryRecycleBin(null, ref info) == 0 ? info.i64Size : 0;
    }

    public bool CanRecycle(string path)
    {
        if (path.Length > MaxShellPath) return false; // SHFileOperation cannot handle long paths
        try
        {
            var root = Path.GetPathRoot(path);
            if (string.IsNullOrEmpty(root) || root.StartsWith(@"\\", StringComparison.Ordinal)) return false;
            var type = new DriveInfo(root).DriveType;
            return RecyclePolicy.DriveHasRecycleBin(type);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public RecycleOutcome Recycle(string path)
    {
        var op = new NativeMethods.SHFILEOPSTRUCT
        {
            wFunc = NativeMethods.FO_DELETE,
            pFrom = path + "\0\0",
            fFlags = (ushort)(NativeMethods.FOF_ALLOWUNDO | NativeMethods.FOF_NOCONFIRMATION | NativeMethods.FOF_NOERRORUI
                              | NativeMethods.FOF_SILENT | NativeMethods.FOF_WANTNUKEWARNING),
        };
        var rc = NativeMethods.SHFileOperation(ref op);
        if (rc == 0 && !op.fAnyOperationsAborted)
            return File.Exists(path) || Directory.Exists(path) ? RecycleOutcome.Failed : RecycleOutcome.Recycled;
        return rc switch
        {
            0x20 or 0x05 or 0x78 => RecycleOutcome.InUse,
            0x02 or 0x03 or 0x7C => RecycleOutcome.NotFound,
            _ => RecycleOutcome.Failed,
        };
    }

    public bool Empty()
    {
        var hr = NativeMethods.SHEmptyRecycleBin(IntPtr.Zero, null,
            NativeMethods.SHERB_NOCONFIRMATION | NativeMethods.SHERB_NOPROGRESSUI | NativeMethods.SHERB_NOSOUND);
        return hr == 0 || hr == E_UNEXPECTED; // E_UNEXPECTED = already empty
    }
}
