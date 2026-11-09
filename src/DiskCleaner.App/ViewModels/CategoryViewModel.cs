using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskCleaner.Core.Formatting;
using DiskCleaner.Core.Rules;

namespace DiskCleaner.App.ViewModels;

public sealed class CategoryViewModel : ObservableObject
{
    private readonly Action _selectionChanged;
    private bool _suppress;

    public CategoryViewModel(CategoryGroup group, DateTime nowUtc, Action selectionChanged)
    {
        Category = group.Category;
        _selectionChanged = selectionChanged;
        Risk = group.Items.Count > 0 ? group.Items[0].Risk : Core.Rules.Risk.Review;
        foreach (var i in group.Items) Add(new SuggestionItemViewModel(i, nowUtc));
        IsExpanded = Risk == Core.Rules.Risk.Review;
    }

    public CleanupCategory Category { get; }
    public Risk Risk { get; }
    public bool IsSafe => Risk == Core.Rules.Risk.Safe;
    public string DisplayName => Text.Category(Category);
    public string RiskText => Text.Risk(Risk);
    public ObservableCollection<SuggestionItemViewModel> Items { get; } = new();
    public bool IsExpanded { get; set; }
    public string TotalText => $"{SizeFormatter.Format(Items.Sum(i => i.Item.Size))} · {Text.Number(Items.Count)} itens";

    public bool? IsChecked
    {
        get
        {
            var n = Items.Count(i => i.IsChecked);
            return n == 0 ? false : n == Items.Count ? true : null;
        }
        set
        {
            _suppress = true;
            foreach (var i in Items) i.IsChecked = value ?? false;
            _suppress = false;
            OnPropertyChanged();
            _selectionChanged();
        }
    }

    public void RemoveWhere(Func<SuggestionItemViewModel, bool> predicate)
    {
        foreach (var i in Items.Where(predicate).ToList())
        {
            i.PropertyChanged -= OnItemChanged;
            Items.Remove(i);
        }
        OnPropertyChanged(nameof(TotalText));
        OnPropertyChanged(nameof(IsChecked));
    }

    private void Add(SuggestionItemViewModel vm)
    {
        vm.PropertyChanged += OnItemChanged;
        Items.Add(vm);
    }

    private void OnItemChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_suppress || e.PropertyName != nameof(SuggestionItemViewModel.IsChecked)) return;
        OnPropertyChanged(nameof(IsChecked));
        _selectionChanged();
    }
}
