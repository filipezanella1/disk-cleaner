namespace DiskCleaner.Core.Cleaning;

public enum RecycleOutcome { Recycled, InUse, NotFound, Failed }

public interface IRecycleBin
{
    /// <summary>Total bytes currently in the Recycle Bin (all drives).</summary>
    long QuerySize();

    /// <summary>False when the path's drive has no Recycle Bin (network, etc.).</summary>
    bool CanRecycle(string path);

    RecycleOutcome Recycle(string path);

    bool Empty();
}
