using DiskCleaner.Core.IO;
using DiskCleaner.Tests.Helpers;

namespace DiskCleaner.Tests;

public class PathUtilTests
{
    private static readonly char S = Path.DirectorySeparatorChar;

    [Fact]
    public void Normalize_StripsLongPrefixAndTrailingSeparator()
    {
        var p = TestTrees.P("a", "b");
        Assert.Equal(p, PathUtil.Normalize(@"\\?\" + p + S));
        Assert.Equal(p, PathUtil.Normalize(p + S + "c" + S + ".."));
        var root = Path.GetPathRoot(p)!;
        Assert.Equal(root, PathUtil.Normalize(root));
    }

    [Fact]
    public void IsUnderOrEqual_CaseInsensitive_SegmentAware()
    {
        var a = TestTrees.P("a", "b");
        Assert.True(PathUtil.IsUnderOrEqual(TestTrees.P("A", "B", "c"), a));
        Assert.True(PathUtil.IsUnderOrEqual(a, a));
        Assert.False(PathUtil.IsUnder(a, a));
        Assert.False(PathUtil.IsUnderOrEqual(TestTrees.P("a", "bc"), a));
        Assert.True(PathUtil.IsUnder(TestTrees.P("a", "b", "c"), a));
        var root = Path.GetPathRoot(a)!;
        Assert.True(PathUtil.IsUnder(a, root));
    }
}
