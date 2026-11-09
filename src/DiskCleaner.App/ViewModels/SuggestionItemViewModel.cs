using CommunityToolkit.Mvvm.ComponentModel;
using DiskCleaner.Core.Formatting;
using DiskCleaner.Core.Rules;

namespace DiskCleaner.App.ViewModels;

public sealed class SuggestionItemViewModel : ObservableObject
{
    private bool _isChecked;

    public SuggestionItemViewModel(CleanupItem item, DateTime nowUtc)
    {
        Item = item;
        _isChecked = item.Risk == Risk.Safe;
        AgeText = Text.Age(item.LastWriteUtcAtScan, nowUtc);
    }

    public CleanupItem Item { get; }
    public string PathText => Item.IsRecycleBin ? "Lixeira do Windows" : Item.Path;
    public string SizeText => SizeFormatter.Format(Item.Size);
    public string Reason => Item.Reason;
    public string AgeText { get; }
    public bool IsChecked { get => _isChecked; set => SetProperty(ref _isChecked, value); }
}
