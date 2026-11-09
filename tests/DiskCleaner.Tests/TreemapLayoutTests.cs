using DiskCleaner.Core.Layout;

namespace DiskCleaner.Tests;

public class TreemapLayoutTests
{
    private static readonly double[] Weights = { 6, 6, 4, 3, 2, 2, 1 };
    private static readonly LayoutRect Bounds = new(0, 0, 600, 400);
    private static IReadOnlyList<TreemapCell<double>> Layout(double[] w, LayoutRect? b = null) =>
        TreemapLayout.Squarify(w, x => x, b ?? Bounds);

    [Fact]
    public void AreasProportionalToWeights()
    {
        var cells = Layout(Weights);
        Assert.Equal(Weights.Length, cells.Count);
        var total = 600.0 * 400.0;
        foreach (var c in cells)
            Assert.Equal(c.Item / Weights.Sum(), c.Bounds.Width * c.Bounds.Height / total, 6);
    }

    [Fact]
    public void CellsInsideBounds_NoOverlap()
    {
        var b = new LayoutRect(10, 20, 300, 200);
        var cells = Layout(Weights, b);
        foreach (var c in cells)
        {
            Assert.True(c.Bounds.X >= b.X - 1e-9 && c.Bounds.Y >= b.Y - 1e-9);
            Assert.True(c.Bounds.X + c.Bounds.Width <= b.X + b.Width + 1e-9);
            Assert.True(c.Bounds.Y + c.Bounds.Height <= b.Y + b.Height + 1e-9);
        }
        for (int i = 0; i < cells.Count; i++)
        for (int j = i + 1; j < cells.Count; j++)
        {
            var (a, o) = (cells[i].Bounds, cells[j].Bounds);
            var ix = Math.Min(a.X + a.Width, o.X + o.Width) - Math.Max(a.X, o.X);
            var iy = Math.Min(a.Y + a.Height, o.Y + o.Height) - Math.Max(a.Y, o.Y);
            Assert.False(ix > 1e-6 && iy > 1e-6, $"cells {i} and {j} overlap");
        }
    }

    [Fact] public void ZeroWeights_Omitted() => Assert.Equal(2, Layout(new double[] { 3, 0, -1, 2 }).Count);
    [Fact] public void Empty_ReturnsEmpty() => Assert.Empty(Layout(Array.Empty<double>()));
    [Fact] public void EmptyBounds_ReturnsEmpty() => Assert.Empty(Layout(Weights, new LayoutRect(0, 0, 0, 100)));

    [Fact]
    public void SingleItem_FillsBounds() => Assert.Equal(Bounds, Assert.Single(Layout(new double[] { 5 })).Bounds);

    [Fact]
    public void AspectRatiosReasonable()
    {
        foreach (var c in Layout(Weights))
            Assert.True(Math.Max(c.Bounds.Width / c.Bounds.Height, c.Bounds.Height / c.Bounds.Width) <= 3.0);
    }

    [Fact]
    public void LargestFirst()
    {
        var cells = Layout(new double[] { 1, 9, 3 });
        Assert.Equal(new double[] { 9, 3, 1 }, cells.Select(c => c.Item));
    }
}
