namespace DiskCleaner.Core.Model;

/// <summary>A file or directory in the space tree. Directory sizes are the sum of descendants,
/// maintained incrementally (thread-safe) as children are added.</summary>
public sealed class SpaceNode
{
    private readonly List<SpaceNode>? _children;
    private readonly object _lock = new();
    private long _logicalSize;
    private long _allocatedSize;
    private int _flags;

    private SpaceNode(string fullPath, NodeKind kind, long logical, long allocated, DateTime lastWriteUtc)
    {
        FullPath = fullPath;
        var name = Path.GetFileName(fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        Name = string.IsNullOrEmpty(name) ? fullPath : name;
        Kind = kind;
        _logicalSize = logical;
        _allocatedSize = allocated;
        LastWriteUtc = lastWriteUtc;
        if (kind == NodeKind.Directory) _children = new List<SpaceNode>();
    }

    public static SpaceNode CreateDirectory(string fullPath, DateTime lastWriteUtc) =>
        new(fullPath, NodeKind.Directory, 0, 0, lastWriteUtc);

    public static SpaceNode CreateFile(string fullPath, long logicalSize, long allocatedSize, DateTime lastWriteUtc) =>
        new(fullPath, NodeKind.File, logicalSize, allocatedSize, lastWriteUtc);

    public string Name { get; }
    public string FullPath { get; }
    public NodeKind Kind { get; }
    public DateTime LastWriteUtc { get; }
    public SpaceNode? Parent { get; private set; }
    public long LogicalSize => Interlocked.Read(ref _logicalSize);
    public long AllocatedSize => Interlocked.Read(ref _allocatedSize);

    public NodeFlags Flags
    {
        get => (NodeFlags)Volatile.Read(ref _flags);
        set => Volatile.Write(ref _flags, (int)value);
    }

    public bool IsDirectory => Kind == NodeKind.Directory;

    public IReadOnlyList<SpaceNode> GetChildrenSnapshot()
    {
        if (_children is null) return Array.Empty<SpaceNode>();
        lock (_lock) return _children.ToArray();
    }

    public SpaceNode? GetChild(string name)
    {
        if (_children is null) return null;
        lock (_lock)
            foreach (var c in _children)
                if (string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase)) return c;
        return null;
    }

    public void AddChild(SpaceNode child)
    {
        if (_children is null) throw new InvalidOperationException("Files cannot have children.");
        child.Parent = this;
        lock (_lock) _children.Add(child);
        AddToAncestors(this, child.LogicalSize, child.AllocatedSize);
    }

    /// <summary>Detaches a child (used after it is cleaned) and subtracts its size from ancestors.</summary>
    public bool RemoveChild(SpaceNode child)
    {
        if (_children is null) return false;
        bool removed;
        lock (_lock) removed = _children.Remove(child);
        if (removed)
        {
            AddToAncestors(this, -child.LogicalSize, -child.AllocatedSize);
            child.Parent = null;
        }
        return removed;
    }

    private static void AddToAncestors(SpaceNode? node, long logical, long allocated)
    {
        for (; node is not null; node = node.Parent)
        {
            Interlocked.Add(ref node._logicalSize, logical);
            Interlocked.Add(ref node._allocatedSize, allocated);
        }
    }

    public override string ToString() => FullPath;
}
