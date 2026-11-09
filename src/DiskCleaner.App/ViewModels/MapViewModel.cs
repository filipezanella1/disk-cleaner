using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskCleaner.App.Services;
using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.Formatting;
using DiskCleaner.Core.Model;
using DiskCleaner.Core.Rules;

namespace DiskCleaner.App.ViewModels;

public sealed class MapViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly List<SpaceTree> _trees = new();
    private NodeViewModel? _selected;
    private SpaceNode? _treemapNode;
    private int _version;

    public MapViewModel(AppServices services)
    {
        _services = services;
        GoUpCommand = new RelayCommand(GoUp, () => TreemapNode?.Parent is not null);
    }

    public event Action<CleanupResult>? Cleaned;

    public ObservableCollection<NodeViewModel> Roots { get; } = new();
    public IRelayCommand GoUpCommand { get; }

    public NodeViewModel? Selected { get => _selected; private set => SetProperty(ref _selected, value); }

    /// <summary>Directory whose children the treemap shows.</summary>
    public SpaceNode? TreemapNode
    {
        get => _treemapNode;
        set
        {
            if (!SetProperty(ref _treemapNode, value)) return;
            OnPropertyChanged(nameof(CurrentPathText));
            GoUpCommand.NotifyCanExecuteChanged();
        }
    }

    public string CurrentPathText => TreemapNode is null ? "" : $"{TreemapNode.FullPath}  —  {SizeFormatter.Format(TreemapNode.AllocatedSize)}";

    /// <summary>Incremented whenever sizes change, so the treemap redraws.</summary>
    public int Version { get => _version; private set => SetProperty(ref _version, value); }

    public void Clear()
    {
        _trees.Clear();
        Roots.Clear();
        Selected = null;
        TreemapNode = null;
    }

    public void AddRoot(SpaceTree tree)
    {
        _trees.Add(tree);
        var vm = new NodeViewModel(tree.Root, null, this);
        Roots.Add(vm);
        TreemapNode ??= tree.Root;
        vm.IsExpanded = true;
    }

    public void RefreshSizes()
    {
        foreach (var r in Roots) r.Refresh();
        OnPropertyChanged(nameof(CurrentPathText));
        Version++;
    }

    internal void OnSelected(NodeViewModel vm)
    {
        Selected = vm;
        TreemapNode = vm.Node.IsDirectory ? vm.Node : vm.Node.Parent ?? vm.Node;
    }

    /// <summary>Treemap click: enter a folder or select a file.</summary>
    public void Activate(SpaceNode node)
    {
        if (node.IsDirectory && !node.Flags.HasFlag(NodeFlags.ReparsePoint)) TreemapNode = node;
        SelectNode(node);
    }

    public void SelectNode(SpaceNode node)
    {
        var chain = new Stack<SpaceNode>();
        for (var n = node; n is not null; n = n.Parent) chain.Push(n);
        var root = Roots.FirstOrDefault(r => ReferenceEquals(r.Node, chain.Peek()));
        if (root is null) return;
        chain.Pop();
        var current = root;
        while (chain.Count > 0)
        {
            current.IsExpanded = true;
            var next = current.FindChild(chain.Pop());
            if (next is null) break;
            current = next;
        }
        var keepTreemap = TreemapNode;
        current.IsSelected = true;
        if (node.IsDirectory) TreemapNode = node;
        else TreemapNode = keepTreemap;
    }

    private void GoUp()
    {
        if (TreemapNode?.Parent is { } p) { TreemapNode = p; SelectNode(p); }
    }

    public void OpenInExplorer(NodeViewModel vm)
    {
        try
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{vm.Node.FullPath}\"") { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception e)
        {
            _services.Dialogs.Info("DiskCleaner", $"Não foi possível abrir o Explorer: {e.Message}");
        }
    }

    public bool CanRecycle(NodeViewModel vm) => vm.Parent is not null && !_services.Protected.IsProtected(vm.Node.FullPath);

    public async Task RecycleAsync(NodeViewModel vm)
    {
        if (!CanRecycle(vm))
        {
            _services.Dialogs.Info("Caminho protegido", "Este caminho é protegido e não pode ser removido pelo DiskCleaner.");
            return;
        }
        if (!_services.Dialogs.Confirm("Mandar para a Lixeira",
                $"Mandar \"{vm.Node.Name}\" ({SizeFormatter.Format(vm.Node.AllocatedSize)}) para a Lixeira?\n\n{vm.Node.FullPath}"))
            return;

        var item = CleanupItem.FromNode(vm.Node, CleanupCategory.LargeFiles, Risk.Review, "Removido manualmente");
        var cleaner = _services.CreateCleaner();
        var result = await Task.Run(() => cleaner.Clean(new[] { item }, items => ConfirmPermanent(_services.Dialogs, items), CancellationToken.None));
        if (result.Skipped.Count > 0)
            _services.Dialogs.Info("Não removido", $"{vm.Node.Name}: {result.Skipped[0].Reason}");
        Cleaned?.Invoke(result);
    }

    internal static bool ConfirmPermanent(IDialogs dialogs, IReadOnlyList<CleanupItem> items) =>
        dialogs.Confirm("Exclusão permanente",
            $"Estes {items.Count} itens ({SizeFormatter.Format(items.Sum(i => i.Size))}) não cabem na Lixeira e serão excluídos permanentemente. Continuar?\n\n" +
            string.Join("\n", items.Take(10).Select(i => i.Path)) + (items.Count > 10 ? "\n…" : ""));

    /// <summary>Detaches cleaned paths from the trees and refreshes the view.</summary>
    public void RemovePaths(IEnumerable<string> paths)
    {
        var any = false;
        foreach (var path in paths)
        foreach (var tree in _trees)
        {
            var node = tree.Find(path);
            if (node?.Parent is null) continue;
            if (ReferenceEquals(TreemapNode, node) || IsAncestor(node, TreemapNode)) TreemapNode = node.Parent;
            node.Parent.RemoveChild(node);
            any = true;
            break;
        }
        if (any) RefreshSizes();
    }

    private static bool IsAncestor(SpaceNode ancestor, SpaceNode? node)
    {
        for (var n = node; n is not null; n = n.Parent) if (ReferenceEquals(n, ancestor)) return true;
        return false;
    }
}
