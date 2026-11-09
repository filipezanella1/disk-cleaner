namespace DiskCleaner.Core.Cleaning;

public interface ICleanupLog
{
    void Write(CleanupResult result);
}
