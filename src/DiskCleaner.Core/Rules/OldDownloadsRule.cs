using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public sealed class OldDownloadsRule : ICleanupRule
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase) { ".exe", ".msi", ".iso", ".zip", ".rar", ".7z" };

    public CleanupCategory Category => CleanupCategory.OldDownloads;

    public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx)
    {
        var days = ctx.Settings.OldDownloadDays;
        var cutoff = ctx.NowUtc.AddDays(-days);
        var reason = $"Instalador/arquivo compactado em Downloads há mais de {days} dias";
        foreach (var tree in trees)
        {
            var downloads = tree.Find(ctx.Folders.Downloads);
            if (downloads is null) continue;
            var stack = new Stack<SpaceNode>();
            stack.Push(downloads);
            while (stack.Count > 0)
            {
                var n = stack.Pop();
                foreach (var c in n.GetChildrenSnapshot())
                {
                    if (c.Flags.HasFlag(NodeFlags.ReparsePoint)) continue;
                    if (c.IsDirectory) stack.Push(c);
                    else if (Extensions.Contains(Path.GetExtension(c.Name)) && c.LastWriteUtc < cutoff)
                        yield return CleanupItem.FromNode(c, Category, Risk.Review, reason);
                }
            }
        }
    }
}
