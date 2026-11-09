using DiskCleaner.Core.IO;
using DiskCleaner.Core.Rules;
using DiskCleaner.Core.Safety;

namespace DiskCleaner.Core.Cleaning;

/// <summary>Sends confirmed items to the Recycle Bin after re-checking each one. Never throws for a single item.</summary>
public sealed class Cleaner
{
    public const string ReasonProtected = "Caminho protegido";
    public const string ReasonMissing = "Não encontrado";
    public const string ReasonChanged = "Alterado desde a análise";
    public const string ReasonInUse = "Em uso";
    public const string ReasonFailed = "Falha ao mover para a Lixeira";
    public const string ReasonNoRecycle = "Não cabe na Lixeira";
    public const string ReasonCancelled = "Cancelado";
    public const string ReasonDeleteFailed = "Falha ao excluir";
    public const string ReasonEmptyFailed = "Falha ao esvaziar a Lixeira";

    private readonly IFileSystem _fs;
    private readonly IRecycleBin _bin;
    private readonly ProtectedPaths _protected;
    private readonly ICleanupLog _log;

    public Cleaner(IFileSystem fs, IRecycleBin bin, ProtectedPaths protectedPaths, ICleanupLog log)
    {
        _fs = fs;
        _bin = bin;
        _protected = protectedPaths;
        _log = log;
    }

    public CleanupResult Clean(IReadOnlyList<CleanupItem> items, Func<IReadOnlyList<CleanupItem>, bool> confirmPermanentDelete, CancellationToken ct)
    {
        var removed = new List<CleanupItem>();
        var skipped = new List<SkippedItem>();
        var recyclable = new List<CleanupItem>();
        var unrecyclable = new List<CleanupItem>();
        CleanupItem? recycleBinItem = null;

        foreach (var item in items)
        {
            if (item.IsRecycleBin) { recycleBinItem = item; continue; }
            var problem = Validate(item);
            if (problem is not null) { skipped.Add(new SkippedItem(item, problem)); continue; }
            (_bin.CanRecycle(item.Path) ? recyclable : unrecyclable).Add(item);
        }

        bool permanentOk = unrecyclable.Count > 0 && !ct.IsCancellationRequested && confirmPermanentDelete(unrecyclable);

        foreach (var item in recyclable)
        {
            if (ct.IsCancellationRequested) { skipped.Add(new SkippedItem(item, ReasonCancelled)); continue; }
            var outcome = SafeRecycle(item.Path);
            switch (outcome)
            {
                case RecycleOutcome.Recycled: removed.Add(item); break;
                case RecycleOutcome.InUse: skipped.Add(new SkippedItem(item, ReasonInUse)); break;
                case RecycleOutcome.NotFound: skipped.Add(new SkippedItem(item, ReasonMissing)); break;
                default: skipped.Add(new SkippedItem(item, ReasonFailed)); break;
            }
        }

        foreach (var item in unrecyclable)
        {
            if (ct.IsCancellationRequested) { skipped.Add(new SkippedItem(item, ReasonCancelled)); continue; }
            if (!permanentOk) { skipped.Add(new SkippedItem(item, ReasonNoRecycle)); continue; }
            if (_protected.IsProtected(item.Path)) { skipped.Add(new SkippedItem(item, ReasonProtected)); continue; }
            try { _fs.DeletePermanently(item.Path); removed.Add(item); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { skipped.Add(new SkippedItem(item, ReasonDeleteFailed)); }
        }

        if (recycleBinItem is not null)
        {
            if (ct.IsCancellationRequested) skipped.Add(new SkippedItem(recycleBinItem, ReasonCancelled));
            else if (_bin.Empty()) removed.Add(recycleBinItem);
            else skipped.Add(new SkippedItem(recycleBinItem, ReasonEmptyFailed));
        }

        var result = new CleanupResult(removed, skipped, removed.Sum(i => i.Size));
        try { _log.Write(result); } catch (Exception e) when (e is IOException or UnauthorizedAccessException) { /* logging must not break cleanup */ }
        return result;
    }

    private string? Validate(CleanupItem item)
    {
        if (_protected.IsProtected(item.Path)) return ReasonProtected;
        FsEntry? now;
        try { now = _fs.GetEntry(item.Path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return ReasonMissing; }
        if (now is null || now.IsDirectory != item.IsDirectory) return ReasonMissing;
        if (!item.IsDirectory && (now.Length != item.LogicalSizeAtScan || now.LastWriteUtc != item.LastWriteUtcAtScan))
            return ReasonChanged;
        return null;
    }

    private RecycleOutcome SafeRecycle(string path)
    {
        if (_protected.IsProtected(path)) return RecycleOutcome.Failed; // second line of defence
        try { return _bin.Recycle(path); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { return RecycleOutcome.Failed; }
    }
}
