using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace WinVitals.App.Services;

public interface IAppNotifier
{
    void SetHost(Panel panel);
    void Info(string title, string message);
    void Success(string title, string message);
    void Warning(string title, string message);
    void Error(string title, string message);
}

public sealed class AppNotifier : IAppNotifier
{
    private Panel? _host;

    public void SetHost(Panel panel) => _host = panel;

    public void Info(string title, string message) => Show(title, message, "#60A5FA");
    public void Success(string title, string message) => Show(title, message, "#A6E3A1");
    public void Warning(string title, string message) => Show(title, message, "#FAB387");
    public void Error(string title, string message) => Show(title, message, "#F38BA8");

    private void Show(string title, string message, string color)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            if (_host is null) return;

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
                BorderBrush = (Brush)new BrushConverter().ConvertFromString(color)!,
                BorderThickness = new Thickness(0, 0, 0, 3),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(14, 10, 14, 10),
                Margin = new Thickness(0, 0, 0, 6),
                MinWidth = 240, MaxWidth = 360,
                Opacity = 0
            };

            var sp = new StackPanel();
            sp.Children.Add(new TextBlock
            {
                Text = title, FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)), FontSize = 13
            });
            sp.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)), FontSize = 12,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 2, 0, 0)
            });
            border.Child = sp;

            _host.Children.Add(border);

            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200));
            border.BeginAnimation(UIElement.OpacityProperty, fadeIn);

            // Auto-remove sau 4 giây
            var timer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(300));
                fadeOut.Completed += (_, _) => _host.Children.Remove(border);
                border.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            timer.Start();
        });
    }
}
