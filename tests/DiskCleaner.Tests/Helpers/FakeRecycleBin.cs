using DiskCleaner.Core.Cleaning;

namespace DiskCleaner.Tests.Helpers;

public sealed class FakeRecycleBin : IRecycleBin
{
    public long Size { get; set; }
    public List<string> Recycled { get; } = new();
    public Queue<RecycleOutcome> Outcomes { get; } = new();
    public Func<string, bool> CanRecycleFunc { get; set; } = _ => true;
    public int EmptyCalls { get; private set; }
    public Action<string>? OnRecycled { get; set; }

    public long QuerySize() => Size;
    public bool CanRecycle(string path) => CanRecycleFunc(path);

    public RecycleOutcome Recycle(string path)
    {
        var outcome = Outcomes.Count > 0 ? Outcomes.Dequeue() : RecycleOutcome.Recycled;
        if (outcome == RecycleOutcome.Recycled) { Recycled.Add(path); OnRecycled?.Invoke(path); }
        return outcome;
    }

    public bool Empty() { EmptyCalls++; Size = 0; return true; }
}
