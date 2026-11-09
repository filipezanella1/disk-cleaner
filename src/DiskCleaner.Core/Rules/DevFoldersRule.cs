using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public sealed class DevFoldersRule : ICleanupRule
{
    private const string NodeReason = "Dependências do Node (reinstaláveis com npm install)";
    private const string DotnetReason = "Saída de compilação .NET";
    private const string VenvReason = "Ambiente virtual Python";
    private const string CacheReason = "Cache de pacotes";

    public CleanupCategory Category => CleanupCategory.DevFolders;

    public IEnumerable<CleanupItem> Analyze(IReadOnlyList<SpaceTree> trees, RuleContext ctx)
    {
        var f = ctx.Folders;
        var caches = new[]
        {
            Path.Combine(f.LocalAppData, "npm-cache"),
            Path.Combine(f.LocalAppData, "pip", "Cache"),
            Path.Combine(f.UserProfile, ".nuget", "packages"),
            Path.Combine(f.UserProfile, ".gradle", "caches"),
        };
        foreach (var tree in trees)
        {
            foreach (var c in caches)
                if (tree.Find(c) is { IsDirectory: true, AllocatedSize: > 0 } n)
                    yield return Item(n, CacheReason);

            var stack = new Stack<SpaceNode>();
            stack.Push(tree.Root);
            while (stack.Count > 0)
            {
                ctx.CancellationToken.ThrowIfCancellationRequested();
                var dir = stack.Pop();
                var children = dir.GetChildrenSnapshot();
                bool hasProject = children.Any(c => !c.IsDirectory &&
                    (c.Name.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) || c.Name.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase)));
                foreach (var c in children)
                {
                    if (!c.IsDirectory || c.Flags.HasFlag(NodeFlags.ReparsePoint)) continue;
                    string? reason = c.Name.ToLowerInvariant() switch
                    {
                        "node_modules" => NodeReason,
                        "bin" or "obj" when hasProject => DotnetReason,
                        ".venv" or "venv" when c.GetChild("pyvenv.cfg") is { IsDirectory: false } => VenvReason,
                        _ => null,
                    };
                    if (reason is not null)
                    {
                        if (c.AllocatedSize > 0) yield return Item(c, reason);
                    }
                    else stack.Push(c);
                }
            }
        }
    }

    private CleanupItem Item(SpaceNode n, string reason) => CleanupItem.FromNode(n, Category, Risk.Review, reason);
}
