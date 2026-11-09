using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using DiskCleaner.Core.Formatting;

namespace DiskCleaner.App.ViewModels;

public sealed class DriveViewModel : ObservableObject
{
    private bool _isSelected;

    public DriveViewModel(DriveInfo d)
    {
        RootPath = d.RootDirectory.FullName;
        Name = d.Name.TrimEnd('\\');
        Label = string.IsNullOrWhiteSpace(d.VolumeLabel) ? (d.DriveType == DriveType.Removable ? "Removível" : "Disco local") : d.VolumeLabel;
        Total = d.TotalSize;
        Free = d.AvailableFreeSpace;
        _isSelected = d.DriveType == DriveType.Fixed;
    }

    public string RootPath { get; }
    public string Name { get; }
    public string Label { get; }
    public long Total { get; }
    public long Free { get; }
    public string Title => $"{Label} ({Name})";
    public string SpaceText => $"{SizeFormatter.Format(Free)} livres de {SizeFormatter.Format(Total)}";
    public double UsedFraction => Total == 0 ? 0 : (Total - Free) / (double)Total;

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    public static IEnumerable<DriveViewModel> Discover() =>
        DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType is DriveType.Fixed or DriveType.Removable)
            .Select(d => new DriveViewModel(d));
}
