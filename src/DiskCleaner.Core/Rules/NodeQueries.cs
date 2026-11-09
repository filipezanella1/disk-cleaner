using DiskCleaner.Core.Model;

namespace DiskCleaner.Core.Rules;

public static class NodeQueries
{
    /// <summary>Most recent write time among the node and all its descendants.</summary>
    public static DateTime NewestWriteUtc(SpaceNode node)
    {
        var newest = node.LastWriteUtc;
        var stack = new Stack<SpaceNode>();
        stack.Push(node);
        while (stack.Count > 0)
        {
            var n = stack.Pop();
            if (n.LastWriteUtc > newest) newest = n.LastWriteUtc;
            foreach (var c in n.GetChildrenSnapshot()) stack.Push(c);
        }
        return newest;
    }

    /// <summary>Finds nodes matching an absolute path pattern where a segment may be "*" (any child)
    /// and "{a,b}" alternatives are allowed anywhere.</summary>
    public static IEnumerable<SpaceNode> Match(SpaceTree tree, string pattern)
    {
        foreach (var expanded in ExpandAlternatives(pattern))
        {
            var root = Path.GetPathRoot(expanded) ?? "";
            var segments = expanded[root.Length..].Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar },
                StringSplitOptions.RemoveEmptyEntries);
            int firstWild = Array.FindIndex(segments, s => s == "*");
            if (firstWild < 0)
            {
                var n = tree.Find(expanded);
                if (n is not null) yield return n;
                continue;
            }
            var prefix = Path.Combine(root, Path.Combine(segments[..firstWild]));
            var start = tree.Find(prefix);
            if (start is null) continue;
            foreach (var m in Walk(start, segments, firstWild)) yield return m;
        }
    }

    private static IEnumerable<SpaceNode> Walk(SpaceNode node, string[] segments, int index)
    {
        if (index == segments.Length) { yield return node; yield break; }
        var seg = segments[index];
        if (seg == "*")
        {
            foreach (var c in node.GetChildrenSnapshot())
                if (c.IsDirectory && !c.Flags.HasFlag(NodeFlags.ReparsePoint))
                    foreach (var m in Walk(c, segments, index + 1)) yield return m;
        }
        else
        {
            var c = node.GetChild(seg);
            if (c is not null)
                foreach (var m in Walk(c, segments, index + 1)) yield return m;
        }
    }

    private static IEnumerable<string> ExpandAlternatives(string pattern)
    {
        int open = pattern.IndexOf('{');
        if (open < 0) { yield return pattern; yield break; }
        int close = pattern.IndexOf('}', open);
        var head = pattern[..open];
        var tail = pattern[(close + 1)..];
        foreach (var alt in pattern[(open + 1)..close].Split(','))
            foreach (var rest in ExpandAlternatives(tail))
                yield return head + alt + rest;
    }
}
