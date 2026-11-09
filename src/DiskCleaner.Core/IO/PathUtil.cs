namespace DiskCleaner.Core.IO;

public static class PathUtil
{
    public const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>Full path without the \\?\ prefix, ".." resolved and no trailing separator (except at a root).</summary>
    public static string Normalize(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) path = @"\\" + path[8..];
        else if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal)) path = path[4..];
        var full = Path.GetFullPath(path);
        var root = Path.GetPathRoot(full);
        var trimmed = full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (root is not null && trimmed.Length < root.Length) return root;
        if (root is not null && string.Equals(full, root, Comparison)) return root;
        return trimmed;
    }

    public static bool AreEqual(string a, string b) => string.Equals(Normalize(a), Normalize(b), Comparison);

    public static bool IsUnderOrEqual(string path, string root)
    {
        var p = Normalize(path);
        var r = Normalize(root);
        return string.Equals(p, r, Comparison) || IsStrictlyUnder(p, r);
    }

    public static bool IsUnder(string path, string root) => IsStrictlyUnder(Normalize(path), Normalize(root));

    private static bool IsStrictlyUnder(string p, string r)
    {
        if (p.Length <= r.Length || !p.StartsWith(r, Comparison)) return false;
        var endsWithSep = r[^1] == Path.DirectorySeparatorChar || r[^1] == Path.AltDirectorySeparatorChar;
        if (endsWithSep) return true;
        var c = p[r.Length];
        return c == Path.DirectorySeparatorChar || c == Path.AltDirectorySeparatorChar;
    }
}
