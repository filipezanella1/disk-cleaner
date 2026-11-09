using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public sealed class RecycleBinRule : ICleanupRule
{
    public CleanupCategory Category => CleanupCategory.RecycleBin;

    public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx)
    {
        var size = ctx.RecycleBin.QuerySize();
        if (size > 0)
            yield return new CleanupItem(CleanupItem.RecycleBinPath, false, size, size, DateTime.MinValue, Category, Risk.Safe, "Itens na Lixeira");
    }
}
