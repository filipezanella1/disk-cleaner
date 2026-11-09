using System.Windows;
using DiskCleaner.App.ViewModels;

namespace DiskCleaner.App.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel(App.Services);
        DataContext = _vm;
    }

    private void OnSettingsClick(object sender, RoutedEventArgs e)
    {
        var w = new SettingsWindow(App.Services) { Owner = this };
        if (w.ShowDialog() == true)
            _vm.StatusText = "Configurações salvas. Rode a análise novamente para aplicá-las.";
    }
}
