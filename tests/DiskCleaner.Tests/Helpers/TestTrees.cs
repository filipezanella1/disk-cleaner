namespace DiskCleaner.Tests.Helpers;

public static class TestTrees
{
    public static readonly DateTime T0 = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public static string Root { get; } = Path.Combine(Path.GetTempPath(), "dc-fake");

    public static SpaceTree Build(string root, params (string relPath, long size, DateTime lastWriteUtc)[] files)
    {
        var tree = new SpaceTree(root);
        foreach (var (rel, size, lw) in files) AddFile(tree, rel, size, lw);
        tree.IsComplete = true;
        return tree;
    }

    public static SpaceNode AddFile(SpaceTree tree, string relPath, long size, DateTime lastWriteUtc)
    {
        var parts = relPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var node = tree.Root;
        for (int i = 0; i < parts.Length - 1; i++)
        {
            var child = node.GetChild(parts[i]);
            if (child is null)
            {
                child = SpaceNode.CreateDirectory(Path.Combine(node.FullPath, parts[i]), lastWriteUtc);
                node.AddChild(child);
            }
            node = child;
        }
        var file = SpaceNode.CreateFile(Path.Combine(node.FullPath, parts[^1]), size, size, lastWriteUtc);
        node.AddChild(file);
        return file;
    }

    public static string P(params string[] rel) => Path.Combine([Root, .. rel]);
}
