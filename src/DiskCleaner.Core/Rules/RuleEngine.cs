using DiskCleaner.Core.IO;
using DiskCleaner.Core.Model;
using DiskCleaner.Core.Safety;

namespace DiskCleaner.Core.Rules;

public sealed class RuleEngine
{
    private readonly IReadOnlyList<ICleanupRule> _rules;
    private readonly ProtectedPaths _protected;

    public RuleEngine(IEnumerable<ICleanupRule> rules, ProtectedPaths protectedPaths)
    {
        _rules = rules.ToList();
        _protected = protectedPaths;
    }

    public IReadOnlyList<CategoryGroup> Run(IReadOnlyList<SpaceTree> trees, RuleContext ctx)
    {
        var complete = trees.Where(t => t.IsComplete).ToList();
        var candidates = new List<CleanupItem>();
        foreach (var rule in _rules)
        {
            if (ctx.Settings.DisabledCategories.Contains(rule.Category)) continue;
            ctx.CancellationToken.ThrowIfCancellationRequested();
            candidates.AddRange(rule.Analyze(complete, ctx));
        }

        var accepted = new List<CleanupItem>();
        var acceptedPaths = new HashSet<string>(PathUtil.Comparer);
        // Shorter paths first so ancestors are accepted before their descendants; ties go to the higher-priority category.
        foreach (var item in candidates
                     .Where(i => i.IsRecycleBin || !_protected.IsProtected(i.Path))
                     .OrderBy(i => i.IsRecycleBin ? 0 : PathUtil.Normalize(i.Path).Length)
                     .ThenBy(i => i.Category))
        {
            if (item.IsRecycleBin)
            {
                if (accepted.Any(a => a.IsRecycleBin)) continue;
                accepted.Add(item);
                continue;
            }
            var path = PathUtil.Normalize(item.Path);
            if (HasAcceptedAncestorOrSelf(path, acceptedPaths)) continue;
            accepted.Add(item);
            acceptedPaths.Add(path);
        }

        // A duplicate is only safe to remove while its kept copy stays: drop it if the kept copy is itself suggested.
        accepted.RemoveAll(i => i.KeepPath is not null && HasAcceptedAncestorOrSelf(PathUtil.Normalize(i.KeepPath), acceptedPaths));

        return accepted
            .GroupBy(i => i.Category)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var items = g.OrderByDescending(i => i.Size).ToList();
                return new CategoryGroup(g.Key, items, items.Sum(i => i.Size));
            })
            .ToList();
    }

    private static bool HasAcceptedAncestorOrSelf(string path, HashSet<string> accepted)
    {
        for (string? p = path; !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
            if (accepted.Contains(p)) return true;
        return false;
    }
}
