using DiskCleaner.Core.Model;
using DiskCleaner.Core.Safety;

namespace DiskCleaner.Core.Rules;

/// <summary>Suggests nodes at well-known locations (patterns may use "*" segments and {a,b} alternatives).</summary>
public sealed class KnownPathRule : ICleanupRule
{
    private readonly Risk _risk;
    private readonly string _reason;
    private readonly Func<KnownFolders, IEnumerable<string>> _patterns;
    private readonly bool _childrenOnly;
    private readonly Func<RuleContext, TimeSpan>? _minAge;

    public KnownPathRule(CleanupCategory category, Risk risk, string reason, Func<KnownFolders, IEnumerable<string>> patterns,
        bool childrenOnly = false, Func<RuleContext, TimeSpan>? minAge = null)
    {
        Category = category;
        _risk = risk;
        _reason = reason;
        _patterns = patterns;
        _childrenOnly = childrenOnly;
        _minAge = minAge;
    }

    public CleanupCategory Category { get; }

    public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx)
    {
        var minAge = _minAge?.Invoke(ctx);
        var seen = new HashSet<SpaceNode>();
        foreach (var tree in trees)
        foreach (var pattern in _patterns(ctx.Folders))
        foreach (var match in NodeQueries.Match(tree, pattern))
        {
            var nodes = _childrenOnly ? match.GetChildrenSnapshot() : new[] { match };
            foreach (var n in nodes)
            {
                if (!seen.Add(n) || n.Flags.HasFlag(NodeFlags.ReparsePoint) || n.AllocatedSize == 0)
                    continue;
                if (minAge is { } age && ctx.NowUtc - NodeQueries.NewestWriteUtc(n) < age) continue;
                yield return CleanupItem.FromNode(n, Category, _risk, _reason);
            }
        }
    }
}
