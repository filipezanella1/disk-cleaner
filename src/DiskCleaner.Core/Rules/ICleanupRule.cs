using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public interface ICleanupRule
{
    CleanupCategory Category { get; }

    /// <summary>Suggests items found in the given (complete) trees.</summary>
    IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx);
}
