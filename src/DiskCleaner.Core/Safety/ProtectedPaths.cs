using DiskCleaner.Core.IO;

namespace DiskCleaner.Core.Safety;

/// <summary>Hard blocklist: nothing under these paths is ever suggested or removed.</summary>
public sealed class ProtectedPaths
{
    private static readonly string[] RootSystemFiles = { "pagefile.sys", "swapfile.sys", "hiberfil.sys" };
    private static readonly string[] ProtectedSegments = { "System Volume Information", "$Recycle.Bin" };

    private readonly string[] _trees;
    private readonly string _windowsDir;
    private readonly string _windowsTemp;

    public ProtectedPaths(KnownFolders folders)
    {
        _windowsDir = Norm(folders.WindowsDir);
        _windowsTemp = Norm(folders.WindowsTemp);
        _trees = new[] { folders.ProgramFiles, folders.ProgramFilesX86, folders.ProgramData, folders.AppDirectory }
            .Where(p => !string.IsNullOrWhiteSpace(p)).Select(Norm).ToArray();
    }

    private static string Norm(string p) => string.IsNullOrWhiteSpace(p) ? "" : PathUtil.Normalize(p);

    public bool IsProtected(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return true;
        string p;
        try { p = PathUtil.Normalize(path); }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException) { return true; }

        var root = Path.GetPathRoot(p);
        if (root is null || string.Equals(p, root, PathUtil.Comparison)) return true;

        var parent = Path.GetDirectoryName(p);
        if (parent is not null && string.Equals(PathUtil.Normalize(parent), root, PathUtil.Comparison) &&
            RootSystemFiles.Contains(Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
            return true;

        var segments = p.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => ProtectedSegments.Contains(s, StringComparer.OrdinalIgnoreCase))) return true;

        if (_windowsDir.Length > 0 && PathUtil.IsUnderOrEqual(p, _windowsDir))
            return !(_windowsTemp.Length > 0 && PathUtil.IsUnder(p, _windowsTemp));

        foreach (var t in _trees)
            if (PathUtil.IsUnderOrEqual(p, t)) return true;

        return false;
    }
}
