using DiskCleaner.Tests.Helpers;
using static DiskCleaner.Tests.Helpers.TestTrees;

namespace DiskCleaner.Tests;

public class SpaceTreeTests
{
    [Fact]
    public void AddChild_PropagatesAllocatedSizeToAncestors()
    {
        var tree = Build(Root, ("a/b/x.bin", 100, T0), ("a/y.bin", 50, T0), ("z.bin", 7, T0));
        Assert.Equal(157, tree.Root.AllocatedSize);
        Assert.Equal(157, tree.Root.LogicalSize);
        Assert.Equal(150, tree.Find(P("a"))!.AllocatedSize);
        Assert.Equal(100, tree.Find(P("a", "b"))!.AllocatedSize);
    }

    [Fact]
    public void Find_IsCaseInsensitive_AndNullWhenMissing()
    {
        var tree = Build(Root, ("Dir/File.txt", 1, T0));
        Assert.NotNull(tree.Find(P("dir", "FILE.TXT")));
        Assert.Same(tree.Root, tree.Find(Root));
        Assert.Null(tree.Find(P("nope")));
        Assert.Null(tree.Find(Path.Combine(Path.GetTempPath(), "other")));
    }

    [Fact]
    public void EnumerateFiles_ReturnsOnlyFiles()
    {
        var tree = Build(Root, ("a/x", 1, T0), ("a/b/y", 1, T0), ("z", 1, T0));
        var files = tree.EnumerateFiles().ToList();
        Assert.Equal(3, files.Count);
        Assert.All(files, f => Assert.Equal(NodeKind.File, f.Kind));
        Assert.Equal(6, tree.EnumerateAll().Count()); // root, a, b + 3 files
    }

    [Fact]
    public void Parent_IsSet()
    {
        var tree = Build(Root, ("a/x", 1, T0));
        Assert.Same(tree.Root, tree.Find(P("a"))!.Parent);
    }
}
