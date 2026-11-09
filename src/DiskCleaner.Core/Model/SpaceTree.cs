namespace DiskCleaner.Core.Model;

public sealed class SpaceTree
{
    public SpaceTree(string rootPath)
    {
        Root = SpaceNode.CreateDirectory(rootPath, DateTime.MinValue);
    }

    public SpaceNode Root { get; }
    public bool IsComplete { get; set; }

    /// <summary>Finds a node by full path (case-insensitive). Null if outside this tree or missing.</summary>
    public SpaceNode? Find(string path)
    {
        var rel = Path.GetRelativePath(Root.FullPath, path);
        if (rel == ".") return Root;
        if (rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel)) return null;
        var node = Root;
        foreach (var part in rel.Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            node = node.GetChild(part);
            if (node is null) return null;
        }
        return node;
    }

    public IEnumerable<SpaceNode> EnumerateAll()
    {
        var stack = new Stack<SpaceNode>();
        stack.Push(Root);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            yield return n;
            foreach (var c in n.GetChildrenSnapshot()) stack.Push(c);
        }
    }

    public IEnumerable<SpaceNode> EnumerateFiles() => EnumerateAll().Where(n => n.Kind == NodeKind.File);
}
