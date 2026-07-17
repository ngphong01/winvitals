using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace AppUI;

/// <summary>
/// Real-time line chart for CPU/RAM/Disk monitoring.
/// Shows last 60 seconds of data, auto-scrolling.
/// </summary>
public class PerfChart : Canvas
{
    private readonly List<double> _cpuHistory = [];
    private readonly List<double> _ramHistory = [];
    private readonly List<double> _diskHistory = [];
    private const int MaxPoints = 60;

    private readonly Brush _cpuBrush = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
    private readonly Brush _ramBrush = new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1));
    private readonly Brush _diskBrush = new SolidColorBrush(Color.FromRgb(0xFA, 0xB3, 0x87));
    private readonly Brush _gridBrush = new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));

    public PerfChart()
    {
        Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44));
        MinHeight = 200;
        SizeChanged += (_, _) => Draw();
    }

    public void AddDataPoint(double cpu, double ram, double disk)
    {
        _cpuHistory.Add(Math.Clamp(cpu, 0, 100));
        _ramHistory.Add(Math.Clamp(ram, 0, 100));
        _diskHistory.Add(Math.Clamp(disk, 0, 100));

        while (_cpuHistory.Count > MaxPoints) _cpuHistory.RemoveAt(0);
        while (_ramHistory.Count > MaxPoints) _ramHistory.RemoveAt(0);
        while (_diskHistory.Count > MaxPoints) _diskHistory.RemoveAt(0);

        Draw();
    }

    private void Draw()
    {
        Children.Clear();
        var w = ActualWidth > 0 ? ActualWidth : 600;
        var h = ActualHeight > 0 ? ActualHeight : 200;
        var margin = 30;

        // Background grid
        for (int i = 0; i <= 4; i++)
        {
            double y = margin + (h - margin - 10) * i / 4;
            Children.Add(new Line
            {
                X1 = margin,
                Y1 = y,
                X2 = w - 10,
                Y2 = y,
                Stroke = _gridBrush,
                StrokeThickness = 0.5
            });
            var label = new TextBlock
            {
                Text = $"{100 - i * 25}%",
                FontSize = 9,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)),
                IsHitTestVisible = false
            };
            SetLeft(label, 2); SetTop(label, y - 7);
            Children.Add(label);
        }

        if (_cpuHistory.Count < 2) return;

        // Draw lines
        DrawLine(_cpuHistory, _cpuBrush, w, h, margin);
        DrawLine(_ramHistory, _ramBrush, w, h, margin);
        DrawLine(_diskHistory, _diskBrush, w, h, margin);

        // Legend
        var legendY = h - 16;
        AddLegend(10, legendY, "CPU", _cpuBrush);
        AddLegend(70, legendY, "RAM", _ramBrush);
        AddLegend(130, legendY, "Disk", _diskBrush);
    }

    private void DrawLine(List<double> data, Brush brush, double w, double h, double margin)
    {
        if (data.Count < 2) return;
        var path = new System.Windows.Media.StreamGeometry();
        using var ctx = path.Open();
        double stepX = (w - margin - 10) / Math.Max(1, data.Count - 1);

        ctx.BeginFigure(new Point(margin, margin + (h - margin - 10) * (1 - data[0] / 100)), false, false);
        for (int i = 1; i < data.Count; i++)
        {
            double x = margin + i * stepX;
            double y = margin + (h - margin - 10) * (1 - data[i] / 100);
            ctx.LineTo(new Point(x, y), true, true);
        }

        Children.Add(new System.Windows.Shapes.Path
        {
            Data = path,
            Stroke = brush,
            StrokeThickness = 2,
            Fill = null
        });
    }

    private void AddLegend(double x, double y, string label, Brush brush)
    {
        Children.Add(new Rectangle { Width = 12, Height = 3, Fill = brush });
        var r = Children[^1];
        SetLeft(r, x); SetTop(r, y + 5);

        var txt = new TextBlock
        {
            Text = label,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
            IsHitTestVisible = false
        };
        SetLeft(txt, x + 15); SetTop(txt, y);
        Children.Add(txt);
    }
}
