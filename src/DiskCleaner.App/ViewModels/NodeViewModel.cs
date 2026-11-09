using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskCleaner.Core.Formatting;
using DiskCleaner.Core.Model;

namespace DiskCleaner.App.ViewModels;

/// <summary>Tree row for a <see cref="SpaceNode"/>. Children are created lazily on expand.</summary>
public sealed class NodeViewModel : ObservableObject
{
    private static readonly NodeViewModel Placeholder = new();
    private readonly MapViewModel? _owner;
    private bool _loaded;
    private bool _isExpanded;
    private bool _isSelected;

    private NodeViewModel() { Node = SpaceNode.CreateDirectory("…", DateTime.MinValue); }

    public NodeViewModel(SpaceNode node, NodeViewModel? parent, MapViewModel owner)
    {
        Node = node;
        Parent = parent;
        _owner = owner;
        if (HasChildNodes) Children.Add(Placeholder);
        OpenInExplorerCommand = new RelayCommand(() => owner.OpenInExplorer(this));
        RecycleCommand = new AsyncRelayCommand(() => owner.RecycleAsync(this), () => owner.CanRecycle(this));
    }

    public SpaceNode Node { get; }
    public NodeViewModel? Parent { get; }
    public ObservableCollection<NodeViewModel> Children { get; } = new();
    public IRelayCommand? OpenInExplorerCommand { get; }
    public IAsyncRelayCommand? RecycleCommand { get; }

    public string Name => Node.Name;
    public string Icon => Node.Flags.HasFlag(NodeFlags.ReparsePoint) ? "🔗" : Node.IsDirectory ? "📁" : "📄";
    public string SizeText => SizeFormatter.Format(Node.AllocatedSize);
    public bool IsAccessDenied => Node.Flags.HasFlag(NodeFlags.AccessDenied);
    public string Note => IsAccessDenied ? "(sem acesso)" : Node.Flags.HasFlag(NodeFlags.ReparsePoint) ? "(atalho de pasta — não seguido)" : "";

    public double PercentOfParent
    {
        get
        {
            var parentSize = Parent?.Node.AllocatedSize ?? 0;
            return Parent is null ? 1 : parentSize <= 0 ? 0 : Node.AllocatedSize / (double)parentSize;
        }
    }

    public string PercentText => Text.Percent(PercentOfParent);

    private bool HasChildNodes => Node.IsDirectory && Node.GetChildrenSnapshot().Count > 0;

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (!SetProperty(ref _isExpanded, value)) return;
            if (value) EnsureChildren();
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (SetProperty(ref _isSelected, value) && value) _owner?.OnSelected(this);
        }
    }

    public void EnsureChildren()
    {
        if (_loaded) return;
        _loaded = true;
        RebuildChildren();
    }

    /// <summary>Finds (loading if needed) the child row for a node.</summary>
    public NodeViewModel? FindChild(SpaceNode node)
    {
        EnsureChildren();
        return Children.FirstOrDefault(c => ReferenceEquals(c.Node, node));
    }

    public void RemoveChildRow(NodeViewModel child) => Children.Remove(child);

    /// <summary>Re-reads sizes (and new children for loaded rows) after the tree changed.</summary>
    public void Refresh()
    {
        OnPropertyChanged(nameof(SizeText));
        OnPropertyChanged(nameof(PercentOfParent));
        OnPropertyChanged(nameof(PercentText));
        OnPropertyChanged(nameof(Note));
        if (!_loaded)
        {
            if (HasChildNodes && Children.Count == 0) Children.Add(Placeholder);
            return;
        }
        RebuildChildren();
        foreach (var c in Children) c.Refresh();
    }

    private void RebuildChildren()
    {
        if (_owner is null) return;
        var existing = new Dictionary<SpaceNode, NodeViewModel>(ReferenceEqualityComparer.Instance);
        foreach (var c in Children)
            if (!ReferenceEquals(c, Placeholder)) existing[c.Node] = c;
        var ordered = Node.GetChildrenSnapshot().OrderByDescending(n => n.AllocatedSize).ToList();
        var rows = ordered.Select(n => existing.TryGetValue(n, out var vm) ? vm : new NodeViewModel(n, this, _owner)).ToList();
        if (rows.Count == Children.Count && rows.SequenceEqual(Children)) return;
        Children.Clear();
        foreach (var r in rows) Children.Add(r);
    }
}
