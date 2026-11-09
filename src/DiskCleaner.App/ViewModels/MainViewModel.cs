using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskCleaner.App.Services;
using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.Formatting;
using DiskCleaner.Core.Model;
using DiskCleaner.Core.Scanning;

namespace DiskCleaner.App.ViewModels;

public sealed class MainViewModel : ObservableObject
{
    private readonly AppServices _services;
    private readonly List<SpaceTree> _trees = new();
    private CancellationTokenSource? _cts;
    private bool _isScanning;
    private string _progressText = "";
    private string _statusText = "Escolha os discos e clique em Analisar.";
    private int _selectedTab;

    public MainViewModel(AppServices services)
    {
        _services = services;
        foreach (var d in DriveViewModel.Discover()) Drives.Add(d);
        Map = new MapViewModel(services);
        Suggestions = new SuggestionsViewModel(services);
        Suggestions.Cleaned += OnCleaned;
        Map.Cleaned += OnCleaned;
        AnalyzeCommand = new AsyncRelayCommand(AnalyzeAsync, () => !IsScanning);
        CancelCommand = new RelayCommand(() => _cts?.Cancel(), () => IsScanning);
    }

    public ObservableCollection<DriveViewModel> Drives { get; } = new();
    public MapViewModel Map { get; }
    public SuggestionsViewModel Suggestions { get; }
    public IAsyncRelayCommand AnalyzeCommand { get; }
    public IRelayCommand CancelCommand { get; }
    public IReadOnlyList<SpaceTree> Trees => _trees;

    public bool IsScanning
    {
        get => _isScanning;
        private set
        {
            if (!SetProperty(ref _isScanning, value)) return;
            AnalyzeCommand.NotifyCanExecuteChanged();
            CancelCommand.NotifyCanExecuteChanged();
        }
    }

    public string ProgressText { get => _progressText; private set => SetProperty(ref _progressText, value); }
    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public int SelectedTab { get => _selectedTab; set => SetProperty(ref _selectedTab, value); }

    private async Task AnalyzeAsync()
    {
        var selected = Drives.Where(d => d.IsSelected).ToList();
        if (selected.Count == 0) { StatusText = "Selecione ao menos um disco."; return; }

        _cts = new CancellationTokenSource();
        var ct = _cts.Token;
        IsScanning = true;
        _trees.Clear();
        Map.Clear();
        Suggestions.Clear();
        SelectedTab = 0;
        StatusText = "Analisando…";

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) => Map.RefreshSizes();
        var progress = new Progress<ScanProgress>(p =>
            ProgressText = $"{Text.Number(p.Files)} arquivos · {SizeFormatter.Format(p.Bytes)} · {p.CurrentPath}");
        try
        {
            timer.Start();
            var scanner = _services.CreateScanner();
            foreach (var drive in selected)
            {
                if (ct.IsCancellationRequested) break;
                var tree = new SpaceTree(drive.RootPath);
                _trees.Add(tree);
                Map.AddRoot(tree);
                await Task.Run(() => scanner.ScanAsync(tree, progress, ct));
            }
            timer.Stop();
            Map.RefreshSizes();

            if (ct.IsCancellationRequested || _trees.Any(t => !t.IsComplete))
            {
                StatusText = "Análise cancelada — sugestões não geradas. O mapa parcial continua disponível.";
                return;
            }

            ProgressText = "Gerando sugestões…";
            var groups = await Task.Run(() => _services.CreateEngine().Run(_trees, _services.CreateContext(ct)));
            Suggestions.Load(groups);
            var total = groups.Sum(g => g.TotalSize);
            StatusText = $"Análise concluída: {SizeFormatter.Format(_trees.Sum(t => t.Root.AllocatedSize))} analisados, " +
                         $"{SizeFormatter.Format(total)} podem ser liberados.";
            ProgressText = "";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Análise cancelada — sugestões não geradas. O mapa parcial continua disponível.";
        }
        finally
        {
            timer.Stop();
            IsScanning = false;
            if (ProgressText.StartsWith("Gerando", StringComparison.Ordinal)) ProgressText = "";
        }
    }

    private void OnCleaned(CleanupResult result)
    {
        Map.RemovePaths(result.Removed.Where(i => !i.IsRecycleBin).Select(i => i.Path));
        Suggestions.RemoveItems(result.Removed);
        StatusText = $"Limpeza concluída: {SizeFormatter.Format(result.BytesFreed)} liberados.";
    }
}
