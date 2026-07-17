using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using App.Core;

namespace AppUI;

/// <summary>
/// Treemap control — WinDirStat-style folder size visualization.
/// Draws nested colored rectangles proportional to size.
/// </summary>
public class TreemapControl : Canvas
{
    public static readonly DependencyProperty ItemsProperty =
        DependencyProperty.Register(nameof(Items), typeof(List<ScanItem>), typeof(TreemapControl),
            new PropertyMetadata(null, (d, _) => ((TreemapControl)d).Draw()));

    public List<ScanItem>? Items
    {
        get => (List<ScanItem>?)GetValue(ItemsProperty);
        set => SetValue(ItemsProperty, value);
    }

    private readonly Dictionary<string, Brush> _colorMap = new()
    {
        // Richer, more differentiated palette
        ["Critical"] = new SolidColorBrush(Color.FromRgb(0xF7, 0x76, 0x8E)),
        ["High"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xAF, 0x68)),
        ["Medium"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xC6, 0x8F)),
        ["Low"] = new SolidColorBrush(Color.FromRgb(0x9E, 0xCE, 0x6A)),
        ["Safe"] = new SolidColorBrush(Color.FromRgb(0x7C, 0x96, 0xF0)),
        ["DevCache"] = new SolidColorBrush(Color.FromRgb(0xBB, 0x9A, 0xF7)),
        ["System"] = new SolidColorBrush(Color.FromRgb(0x3B, 0x3D, 0x58)),
    };

    public TreemapControl()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x1A, 0x1B, 0x2E));
        MinHeight = 300;
        IsVisibleChanged += (s, e) => { if ((bool)e.NewValue) Draw(); };
        Loaded += (_, _) => Draw();
    }

    private void Draw()
    {
        Children.Clear();
        if (Items == null || Items.Count == 0) return;

        var w = ActualWidth > 0 ? ActualWidth : 600;
        var h = ActualHeight > 0 ? ActualHeight : 400;

        // Take top items, sort by size descending
        var items = Items.Where(i => i.SizeBytes > 0).OrderByDescending(i => i.SizeBytes).Take(120).ToList();
        if (items.Count == 0) return;

        long totalSize = items.Sum(i => i.SizeBytes);
        if (totalSize == 0) return;

        // Slice-and-dice treemap layout
        double x = 0, y = 0;
        int rowStart = 0;

        while (rowStart < items.Count)
        {
            // Decide row: take items until row is "square enough"
            long rowSize = 0;
            int rowEnd = rowStart;
            double rowWidth = w - x;

            for (int i = rowStart; i < items.Count; i++)
            {
                double testW = rowWidth * (rowSize + items[i].SizeBytes) / (double)totalSize;
                double testH = (h - y) * items[i].SizeBytes / (double)(rowSize + items[i].SizeBytes);
                if (i > rowStart && testH < testW / 3) break; // Row too short
                rowSize += items[i].SizeBytes;
                rowEnd = i;
            }
            if (rowEnd < rowStart) rowEnd = rowStart;

            double rowH = (h - y) * rowSize / (double)totalSize;
            double cx = x;

            for (int i = rowStart; i <= rowEnd; i++)
            {
                double cw = rowWidth * items[i].SizeBytes / (double)rowSize;
                if (cw < 20 || rowH < 16) continue; // Too small to render

                var brush = GetBrush(items[i]);
                var rect = new Rectangle
                {
                    Width = Math.Max(0, cw - 2),
                    Height = Math.Max(0, rowH - 2),
                    Fill = brush,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x2E)),
                    StrokeThickness = 1,
                    RadiusX = 3,
                    RadiusY = 3,
                    ToolTip = $"{items[i].Name}\n{items[i].SizeFormatted}\n{items[i].Category} | {items[i].Risk}",
                    Cursor = System.Windows.Input.Cursors.Hand
                };
                SetLeft(rect, cx + 1);
                SetTop(rect, y + 1);
                Children.Add(rect);

                // Label (only if big enough)
                if (cw > 50 && rowH > 20)
                {
                    var label = new TextBlock
                    {
                        Text = Truncate(items[i].Name, (int)(cw / 7)),
                        FontSize = Math.Min(11, rowH / 2.5),
                        Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
                        Margin = new Thickness(4, 1, 0, 0),
                        IsHitTestVisible = false
                    };
                    SetLeft(label, cx + 3);
                    SetTop(label, y + 2);
                    Children.Add(label);
                }

                cx += cw;
            }

            y += rowH;
            x = 0;
            rowStart = rowEnd + 1;
        }

        // Legend
        double ly = h - 22;
        var legendItems = new[] { ("Safe", GetColor("Safe")), ("Low", GetColor("Low")),
            ("Medium", GetColor("Medium")), ("High", GetColor("High")), ("Dev", GetColor("DevCache")) };
        double lx = 8;
        foreach (var (name, color) in legendItems)
        {
            var dot = new Rectangle { Width = 10, Height = 10, Fill = new SolidColorBrush(color), RadiusX = 2, RadiusY = 2 };
            SetLeft(dot, lx); SetTop(dot, ly + 2);
            Children.Add(dot);
            var txt = new TextBlock { Text = name, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)), IsHitTestVisible = false };
            SetLeft(txt, lx + 13); SetTop(txt, ly);
            Children.Add(txt);
            lx += 60;
        }
    }

    private Brush GetBrush(ScanItem item) => item.Risk switch
    {
        RiskLevel.Critical => _colorMap["Critical"],
        RiskLevel.High => _colorMap["High"],
        RiskLevel.Medium => _colorMap["Medium"],
        RiskLevel.Low => _colorMap["Low"],
        RiskLevel.Safe => _colorMap["Safe"],
        _ => item.Category == ItemCategory.DevCache ? _colorMap["DevCache"] : _colorMap["System"]
    };

    private Color GetColor(string key) => (_colorMap.GetValueOrDefault(key) as SolidColorBrush)?.Color ?? Colors.Gray;

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..(max - 1)] + "…";
}
