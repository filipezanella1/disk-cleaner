using System.Security.Cryptography;
using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

/// <summary>Finds identical files: size → partial hash (first + last 64 KB) → full SHA-256.
/// Keeps the oldest copy of each group (tie: shortest path, then ordinal) and suggests the rest.</summary>
public sealed class DuplicatesRule : ICleanupRule
{
    private const int PartialChunk = 64 * 1024;

    public CleanupCategory Category => CleanupCategory.Duplicates;

    public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx)
    {
        var min = ctx.Settings.DuplicateMinBytes;
        var bySize = trees.SelectMany(t => t.EnumerateFiles())
            .Where(f => f.LogicalSize >= min && f.AllocatedSize > 0 && !f.Flags.HasFlag(NodeFlags.ReparsePoint))
            .GroupBy(f => f.LogicalSize)
            .Where(g => g.Count() > 1);

        var results = new List<CleanupItem>();
        foreach (var sizeGroup in bySize)
        foreach (var partialGroup in GroupByHash(sizeGroup, f => PartialHash(ctx, f), ctx))
        foreach (var fullGroup in GroupByHash(partialGroup, f => FullHash(ctx, f), ctx))
        {
            var ordered = fullGroup
                .OrderBy(f => f.LastWriteUtc)
                .ThenBy(f => f.FullPath.Length)
                .ThenBy(f => f.FullPath, StringComparer.Ordinal)
                .ToList();
            var keep = ordered[0];
            foreach (var dup in ordered.Skip(1))
                results.Add(CleanupItem.FromNode(dup, Category, Risk.Review, $"Cópia de {keep.FullPath}") with { KeepPath = keep.FullPath });
        }
        return results;
    }

    private static IEnumerable<List<SpaceNode>> GroupByHash(IEnumerable<SpaceNode> files, Func<SpaceNode, string?> hash, RuleContext ctx)
    {
        var groups = new Dictionary<string, List<SpaceNode>>(StringComparer.Ordinal);
        foreach (var f in files)
        {
            ctx.CancellationToken.ThrowIfCancellationRequested();
            var h = hash(f);
            if (h is null) continue;
            if (!groups.TryGetValue(h, out var list)) groups[h] = list = new List<SpaceNode>();
            list.Add(f);
        }
        return groups.Values.Where(g => g.Count > 1);
    }

    private static string? PartialHash(RuleContext ctx, SpaceNode f)
    {
        try
        {
            using var s = ctx.FileSystem.OpenRead(f.FullPath);
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buf = new byte[PartialChunk];
            sha.AppendData(buf, 0, ReadFully(s, buf));
            if (f.LogicalSize > PartialChunk && s.CanSeek)
            {
                s.Seek(Math.Max(PartialChunk, f.LogicalSize - PartialChunk), SeekOrigin.Begin);
                sha.AppendData(buf, 0, ReadFully(s, buf));
            }
            return Convert.ToHexString(sha.GetHashAndReset());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string? FullHash(RuleContext ctx, SpaceNode f)
    {
        try
        {
            using var s = ctx.FileSystem.OpenRead(f.FullPath);
            return Convert.ToHexString(SHA256.HashData(s));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static int ReadFully(Stream s, byte[] buf)
    {
        int total = 0, n;
        while (total < buf.Length && (n = s.Read(buf, total, buf.Length - total)) > 0) total += n;
        return total;
    }
}
