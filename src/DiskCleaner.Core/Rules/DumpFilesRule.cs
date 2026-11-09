using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public sealed class DumpFilesRule : ICleanupRule
{
    public CleanupCategory Category => CleanupCategory.LogsDumps;

    public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx) =>
        trees.SelectMany(t => t.EnumerateFiles())
            .Where(f => f.Name.EndsWith(".dmp", StringComparison.OrdinalIgnoreCase) && !f.Flags.HasFlag(NodeFlags.ReparsePoint))
            .Select(f => CleanupItem.FromNode(f, Category, Risk.Safe, "Log ou despejo de erro"));
}
