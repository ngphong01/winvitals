using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace WinVitals.App.Controls;

public partial class SkeletonBox : UserControl
{
    public SkeletonBox()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (Resources["ShimmerAnim"] is Storyboard sb)
            {
                var clone = sb.Clone();
                Storyboard.SetTarget(clone, ShimmerBar);
                clone.Begin();
            }
        };
    }

    public static readonly DependencyProperty SkeletonTypeProperty =
        DependencyProperty.Register(nameof(SkeletonType), typeof(string), typeof(SkeletonBox),
            new PropertyMetadata("Line", OnTypeChanged));

    public string SkeletonType
    {
        get => (string)GetValue(SkeletonTypeProperty);
        set => SetValue(SkeletonTypeProperty, value);
    }

    private static void OnTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not SkeletonBox box) return;
        var type = e.NewValue as string ?? "Line";
        box.Height = type switch
        {
            "Circle" => 40,
            "Card" => 120,
            _ => 16
        };
        box.Width = type == "Circle" ? 40 : double.NaN;
    }
}
