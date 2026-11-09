using System.Windows;
using DiskCleaner.App.Services;
using DiskCleaner.App.ViewModels;
using DiskCleaner.Core.Rules;

namespace DiskCleaner.App.Views;

public partial class SettingsWindow : Window
{
    private const long MB = 1024 * 1024;
    private readonly AppServices _services;
    private readonly List<RuleToggle> _rules;

    public sealed class RuleToggle
    {
        public required CleanupCategory Category { get; init; }
        public required string Name { get; init; }
        public bool Enabled { get; set; }
    }

    public SettingsWindow(AppServices services)
    {
        InitializeComponent();
        _services = services;
        var s = services.Settings;
        LargeFileMb.Text = (s.LargeFileThresholdBytes / MB).ToString(Text.PtBr);
        OldDownloadDays.Text = s.OldDownloadDays.ToString(Text.PtBr);
        _rules = Enum.GetValues<CleanupCategory>()
            .Select(c => new RuleToggle { Category = c, Name = Text.Category(c), Enabled = !s.DisabledCategories.Contains(c) })
            .ToList();
        Rules.ItemsSource = _rules;
    }

    private void OnSave(object sender, RoutedEventArgs e)
    {
        if (!long.TryParse(LargeFileMb.Text, out var mb) || mb < 1)
        {
            Error.Text = "Informe um tamanho em MB maior que zero.";
            return;
        }
        if (!int.TryParse(OldDownloadDays.Text, out var days) || days < 1)
        {
            Error.Text = "Informe um número de dias maior que zero.";
            return;
        }
        var settings = _services.Settings with
        {
            LargeFileThresholdBytes = mb * MB,
            OldDownloadDays = days,
            DisabledCategories = _rules.Where(r => !r.Enabled).Select(r => r.Category).ToHashSet(),
        };
        try
        {
            _services.SettingsStore.Save(settings);
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException)
        {
            Error.Text = $"Não foi possível salvar: {ex.Message}";
            return;
        }
        _services.Settings = settings;
        DialogResult = true;
    }
}
