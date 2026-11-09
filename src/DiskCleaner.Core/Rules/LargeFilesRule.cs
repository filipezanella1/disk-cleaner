using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public sealed class LargeFilesRule : ICleanupRule
{
    public CleanupCategory Category => CleanupCategory.LargeFiles;

    public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx) =>
        trees.SelectMany(t => t.EnumerateFiles())
            .Where(f => f.AllocatedSize >= ctx.Settings.LargeFileThresholdBytes && !f.Flags.HasFlag(NodeFlags.ReparsePoint))
            .Select(f => CleanupItem.FromNode(f, Category, Risk.Review, "Arquivo grande"));
}
