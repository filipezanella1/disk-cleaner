namespace DiskCleaner.Core.Cleaning;

public static class RecyclePolicy
{
    /// <summary>Only fixed drives reliably have a Recycle Bin; removable and network drives delete permanently.</summary>
    public static bool DriveHasRecycleBin(DriveType type) => type == DriveType.Fixed;
}
