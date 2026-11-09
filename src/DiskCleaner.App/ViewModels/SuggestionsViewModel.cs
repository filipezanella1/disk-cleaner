using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DiskCleaner.App.Services;
using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.Formatting;
using DiskCleaner.Core.Rules;

namespace DiskCleaner.App.ViewModels;

public sealed class SuggestionsViewModel : ObservableObject
{
    private readonly AppServices _services;
    private bool _isCleaning;
    private bool _isLocked;

    public SuggestionsViewModel(AppServices services)
    {
        _services = services;
        CleanCommand = new AsyncRelayCommand(CleanAsync, () => !IsCleaning && !IsLocked && Selected().Any());
    }

    public event Action<CleanupResult>? Cleaned;

    public ObservableCollection<CategoryViewModel> Categories { get; } = new();
    public IAsyncRelayCommand CleanCommand { get; }
    public bool IsEmpty => Categories.Count == 0;

    /// <summary>Set while a scan runs: cleaning must not mutate trees that are being built.</summary>
    public bool IsLocked
    {
        get => _isLocked;
        set { if (SetProperty(ref _isLocked, value)) CleanCommand.NotifyCanExecuteChanged(); }
    }

    public bool IsCleaning
    {
        get => _isCleaning;
        private set { if (SetProperty(ref _isCleaning, value)) CleanCommand.NotifyCanExecuteChanged(); }
    }

    public string SelectedTotalText
    {
        get
        {
            var items = Selected().ToList();
            return $"Selecionado: {SizeFormatter.Format(items.Sum(i => i.Size))} em {Text.Number(items.Count)} itens";
        }
    }

    public void Clear()
    {
        Categories.Clear();
        SelectionChanged();
    }

    public void Load(IReadOnlyList<CategoryGroup> groups)
    {
        Categories.Clear();
        var now = DateTime.UtcNow;
        foreach (var g in groups) Categories.Add(new CategoryViewModel(g, now, SelectionChanged));
        SelectionChanged();
    }

    /// <summary>Drops suggestions that were removed, or that lived inside a removed folder.</summary>
    public void RemoveItems(IEnumerable<CleanupItem> removed)
    {
        var list = removed.ToList();
        var paths = list.Where(i => !i.IsRecycleBin).Select(i => i.Path).ToList();
        var binEmptied = list.Any(i => i.IsRecycleBin);
        bool Gone(SuggestionItemViewModel vm) => vm.Item.IsRecycleBin
            ? binEmptied
            : paths.Any(p => Core.IO.PathUtil.IsUnderOrEqual(vm.Item.Path, p));
        foreach (var c in Categories.ToList())
        {
            c.RemoveWhere(Gone);
            if (c.Items.Count == 0) Categories.Remove(c);
        }
        SelectionChanged();
    }

    private IEnumerable<CleanupItem> Selected() =>
        Categories.SelectMany(c => c.Items).Where(i => i.IsChecked).Select(i => i.Item);

    private void SelectionChanged()
    {
        OnPropertyChanged(nameof(SelectedTotalText));
        OnPropertyChanged(nameof(IsEmpty));
        CleanCommand.NotifyCanExecuteChanged();
    }

    private async Task CleanAsync()
    {
        var items = Selected().ToList();
        if (items.Count == 0) return;
        var dialogs = _services.Dialogs;
        var files = items.Where(i => !i.IsRecycleBin).ToList();
        var message = $"Mover {Text.Number(files.Count)} itens ({SizeFormatter.Format(files.Sum(i => i.Size))}) para a Lixeira?";
        if (items.Any(i => i.IsRecycleBin))
            message += "\n\nA Lixeira atual também será esvaziada (antes de receber estes itens) — o que já está nela não poderá ser recuperado.";
        if (!dialogs.Confirm("Limpar selecionados", message))
            return;

        IsCleaning = true;
        try
        {
            var cleaner = _services.CreateCleaner();
            var result = await Task.Run(() => cleaner.Clean(items, list => MapViewModel.ConfirmPermanent(dialogs, list), CancellationToken.None));
            var summary = $"Liberado: {SizeFormatter.Format(result.BytesFreed)}.\nRemovidos: {Text.Number(result.Removed.Count)}. Pulados: {Text.Number(result.Skipped.Count)}.";
            if (result.Skipped.Count > 0)
                summary += "\n\n" + string.Join("\n", result.Skipped.Take(15).Select(s => $"• {s.Reason} — {s.Item.Path}"))
                           + (result.Skipped.Count > 15 ? "\n…" : "");
            summary += $"\n\nLog salvo em {_services.LogDirectory}";
            Cleaned?.Invoke(result);
            dialogs.Info("Limpeza concluída", summary);
        }
        finally
        {
            IsCleaning = false;
        }
    }
}
