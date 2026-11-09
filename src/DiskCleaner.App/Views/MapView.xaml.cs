using System.Windows.Controls;
using DiskCleaner.App.ViewModels;
using DiskCleaner.Core.Model;

namespace DiskCleaner.App.Views;

public partial class MapView : UserControl
{
    public MapView() => InitializeComponent();

    private MapViewModel? Vm => DataContext as MapViewModel;

    private void OnTreemapNodeClicked(object? sender, SpaceNode node) => Vm?.Activate(node);
}
