using DiskCleaner.Core.Cleaning;
using DiskCleaner.Core.IO;
using DiskCleaner.Core.Safety;
using DiskCleaner.Core.Settings;

namespace DiskCleaner.Core.Rules;

public sealed record RuleContext(
    KnownFolders Folders,
    CleanerSettings Settings,
    IFileSystem FileSystem,
    IRecycleBin RecycleBin,
    DateTime NowUtc,
    CancellationToken CancellationToken);
