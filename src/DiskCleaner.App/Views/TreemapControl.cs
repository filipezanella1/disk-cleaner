using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DiskCleaner.Core.Formatting;
using DiskCleaner.Core.Layout;
using DiskCleaner.Core.Model;

namespace DiskCleaner.App.Views;

/// <summary>Draws the children of <see cref="Node"/> as a squarified treemap.</summary>
public sealed class TreemapControl : FrameworkElement
{
    public static readonly DependencyProperty NodeProperty = DependencyProperty.Register(
        nameof(Node), typeof(SpaceNode), typeof(TreemapControl),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VersionProperty = DependencyProperty.Register(
        nameof(Version), typeof(int), typeof(TreemapControl),
        new FrameworkPropertyMetadata(0, FrameworkPropertyMetadataOptions.AffectsRender));

    private static readonly Dictionary<string, Color> ExtensionColors = BuildExtensionColors();
    private static readonly Color DirectoryColor = Color.FromRgb(0x3B, 0x82, 0xF6);
    private static readonly Color OtherColor = Color.FromRgb(0x9C, 0xA3, 0xAF);
    private static readonly Typeface Face = new("Segoe UI");
    private IReadOnlyList<TreemapCell<SpaceNode>> _cells = Array.Empty<TreemapCell<SpaceNode>>();
    private SpaceNode? _hover;

    public TreemapControl()
    {
        ClipToBounds = true;
        ToolTipService.SetInitialShowDelay(this, 300);
    }

    public event EventHandler<SpaceNode>? NodeClicked;

    public SpaceNode? Node { get => (SpaceNode?)GetValue(NodeProperty); set => SetValue(NodeProperty, value); }
    public int Version { get => (int)GetValue(VersionProperty); set => SetValue(VersionProperty, value); }

    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawRectangle(Brushes.White, null, new Rect(RenderSize));
        var node = Node;
        if (node is null || ActualWidth < 4 || ActualHeight < 4) { _cells = Array.Empty<TreemapCell<SpaceNode>>(); return; }

        var children = node.GetChildrenSnapshot();
        _cells = TreemapLayout.Squarify(children, c => c.AllocatedSize, new LayoutRect(0, 0, ActualWidth, ActualHeight));
        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var border = new Pen(Brushes.White, 1);
        foreach (var cell in _cells)
        {
            var r = new Rect(cell.Bounds.X, cell.Bounds.Y, cell.Bounds.Width, cell.Bounds.Height);
            var color = ColorFor(cell.Item);
            if (ReferenceEquals(cell.Item, _hover)) color = Lighten(color, 0.25);
            dc.DrawRectangle(new SolidColorBrush(color), border, r);
            if (r.Width > 60 && r.Height > 18)
            {
                var text = new FormattedText($"{cell.Item.Name}\n{SizeFormatter.Format(cell.Item.AllocatedSize)}",
                    CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, Face, 11, Brushes.White, dpi)
                {
                    MaxTextWidth = Math.Max(1, r.Width - 8),
                    MaxTextHeight = Math.Max(1, r.Height - 4),
                    Trimming = TextTrimming.CharacterEllipsis,
                };
                dc.DrawText(text, new Point(r.X + 4, r.Y + 2));
            }
        }
        if (_cells.Count == 0)
        {
            var msg = new FormattedText(node.IsDirectory ? "Pasta vazia" : node.Name, CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight, Face, 13, Brushes.Gray, dpi);
            dc.DrawText(msg, new Point(12, 12));
        }
    }

    private SpaceNode? HitTest(Point p) =>
        _cells.FirstOrDefault(c => p.X >= c.Bounds.X && p.X < c.Bounds.X + c.Bounds.Width &&
                                   p.Y >= c.Bounds.Y && p.Y < c.Bounds.Y + c.Bounds.Height)?.Item;

    protected override void OnMouseMove(MouseEventArgs e)
    {
        var hit = HitTest(e.GetPosition(this));
        if (ReferenceEquals(hit, _hover)) return;
        _hover = hit;
        ToolTip = hit is null ? null : $"{hit.FullPath}\n{SizeFormatter.Format(hit.AllocatedSize)}";
        Cursor = hit?.IsDirectory == true ? Cursors.Hand : Cursors.Arrow;
        InvalidateVisual();
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        _hover = null;
        InvalidateVisual();
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        if (HitTest(e.GetPosition(this)) is { } hit) NodeClicked?.Invoke(this, hit);
    }

    private static Color ColorFor(SpaceNode n)
    {
        if (n.IsDirectory) return DirectoryColor;
        return ExtensionColors.TryGetValue(Path.GetExtension(n.Name), out var c) ? c : OtherColor;
    }

    private static Color Lighten(Color c, double f) => Color.FromRgb(
        (byte)(c.R + (255 - c.R) * f), (byte)(c.G + (255 - c.G) * f), (byte)(c.B + (255 - c.B) * f));

    private static Dictionary<string, Color> BuildExtensionColors()
    {
        var d = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase);
        void Add(Color c, params string[] exts) { foreach (var e in exts) d[e] = c; }
        Add(Color.FromRgb(0x8B, 0x5C, 0xF6), ".mp4", ".mkv", ".avi", ".mov", ".wmv", ".webm");
        Add(Color.FromRgb(0x10, 0xB9, 0x81), ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".heic", ".raw", ".webp");
        Add(Color.FromRgb(0xF5, 0x9E, 0x0B), ".mp3", ".wav", ".flac", ".aac", ".ogg", ".m4a");
        Add(Color.FromRgb(0xEF, 0x44, 0x44), ".zip", ".rar", ".7z", ".iso", ".exe", ".msi", ".cab");
        Add(Color.FromRgb(0xEA, 0xB3, 0x08), ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".txt");
        Add(Color.FromRgb(0x64, 0x74, 0x8B), ".dll", ".sys", ".bin", ".dat", ".vhdx", ".vmdk", ".pak");
        return d;
    }
}
