namespace DiskCleaner.Core.Layout;

public readonly record struct LayoutRect(double X, double Y, double Width, double Height);

public sealed record TreemapCell<T>(T Item, LayoutRect Bounds);

/// <summary>Squarified treemap (Bruls, Huizing, van Wijk 2000).</summary>
public static class TreemapLayout
{
    public static IReadOnlyList<TreemapCell<T>> Squarify<T>(IReadOnlyList<T> items, Func<T, double> weight, LayoutRect bounds)
    {
        var result = new List<TreemapCell<T>>();
        if (bounds.Width <= 0 || bounds.Height <= 0) return result;
        var sorted = items.Select(i => (Item: i, W: weight(i))).Where(x => x.W > 0).OrderByDescending(x => x.W).ToList();
        if (sorted.Count == 0) return result;

        var total = sorted.Sum(x => x.W);
        var scale = bounds.Width * bounds.Height / total;
        var areas = sorted.Select(x => x.W * scale).ToList();

        var rect = bounds;
        int start = 0;
        while (start < areas.Count)
        {
            var side = Math.Min(rect.Width, rect.Height);
            int end = start + 1;
            var worst = Worst(areas, start, end, side);
            while (end < areas.Count)
            {
                var next = Worst(areas, start, end + 1, side);
                if (next > worst) break;
                worst = next;
                end++;
            }
            rect = LayoutRow(sorted, areas, start, end, rect, result);
            start = end;
        }
        return result;
    }

    private static double Worst(List<double> areas, int start, int end, double side)
    {
        double sum = 0, max = double.MinValue, min = double.MaxValue;
        for (int i = start; i < end; i++) { sum += areas[i]; max = Math.Max(max, areas[i]); min = Math.Min(min, areas[i]); }
        var s2 = side * side;
        var sum2 = sum * sum;
        return Math.Max(s2 * max / sum2, sum2 / (s2 * min));
    }

    private static LayoutRect LayoutRow<T>(List<(T Item, double W)> items, List<double> areas, int start, int end, LayoutRect rect, List<TreemapCell<T>> result)
    {
        double sum = 0;
        for (int i = start; i < end; i++) sum += areas[i];
        bool horizontalStrip = rect.Width >= rect.Height; // strip along the shorter (left) side
        if (horizontalStrip)
        {
            var w = sum / rect.Height;
            double y = rect.Y;
            for (int i = start; i < end; i++)
            {
                var h = areas[i] / w;
                result.Add(new TreemapCell<T>(items[i].Item, new LayoutRect(rect.X, y, w, h)));
                y += h;
            }
            return new LayoutRect(rect.X + w, rect.Y, Math.Max(0, rect.Width - w), rect.Height);
        }
        else
        {
            var h = sum / rect.Width;
            double x = rect.X;
            for (int i = start; i < end; i++)
            {
                var w = areas[i] / h;
                result.Add(new TreemapCell<T>(items[i].Item, new LayoutRect(x, rect.Y, w, h)));
                x += w;
            }
            return new LayoutRect(rect.X, rect.Y + h, rect.Width, Math.Max(0, rect.Height - h));
        }
    }
}
