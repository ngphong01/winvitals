using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using WinVitals.Core.Entities;

namespace WinVitals.App.Services;

public interface IThemeManager
{
    void Apply(AppTheme theme);
    void Toggle();
    AppTheme Current { get; }
}

public sealed class ThemeManager : IThemeManager
{
    public AppTheme Current { get; private set; } = AppTheme.System;

    public void Toggle()
    {
        Apply(Current switch
        {
            AppTheme.Light => AppTheme.Dark,
            AppTheme.Dark => AppTheme.System,
            AppTheme.System => AppTheme.Light,
            _ => AppTheme.System
        });
    }

    public void Apply(AppTheme theme)
    {
        Current = theme;

        bool useLight = theme switch
        {
            AppTheme.Light => true,
            AppTheme.Dark => false,
            _ => DetectSystemLightMode()
        };

        var bg = useLight ? Color.FromRgb(0xF5, 0xF5, 0xF5) : Color.FromRgb(0x1E, 0x1E, 0x2E);
        var sidebar = useLight ? Color.FromRgb(0xE8, 0xE8, 0xEC) : Color.FromRgb(0x18, 0x18, 0x25);
        var card = useLight ? Color.FromRgb(0xFF, 0xFF, 0xFF) : Color.FromRgb(0x31, 0x32, 0x44);
        var input = useLight ? Color.FromRgb(0xE8, 0xE8, 0xEC) : Color.FromRgb(0x3B, 0x3B, 0x5C);
        var text = useLight ? Color.FromRgb(0x1E, 0x1E, 0x2E) : Color.FromRgb(0xCD, 0xD6, 0xF4);
        var muted = useLight ? Color.FromRgb(0x66, 0x66, 0x66) : Color.FromRgb(0xA6, 0xAD, 0xC8);

        // Update app-level resources
        var app = Application.Current;
        app.Resources["AppBackground"] = new SolidColorBrush(bg);
        app.Resources["AppSidebar"] = new SolidColorBrush(sidebar);
        app.Resources["AppCard"] = new SolidColorBrush(card);
        app.Resources["AppInputBg"] = new SolidColorBrush(input);
        app.Resources["AppText"] = new SolidColorBrush(text);
        app.Resources["AppTextMuted"] = new SolidColorBrush(muted);

        // Replace Card style with updated background
        var cardBrush = new SolidColorBrush(card);
        var cardStyle = new Style(typeof(Border));
        cardStyle.Setters.Add(new Setter(Border.BackgroundProperty, cardBrush));
        cardStyle.Setters.Add(new Setter(Border.CornerRadiusProperty, new CornerRadius(8)));
        cardStyle.Setters.Add(new Setter(Border.PaddingProperty, new Thickness(16)));
        app.Resources["Card"] = cardStyle;
    }

    private static bool DetectSystemLightMode()
    {
        try
        {
            using var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
            var v = key?.GetValue("AppsUseLightTheme");
            if (v is int i) return i != 0;
        }
        catch { }
        return false;
    }
}

internal static class Extensions
{
    public static void Let<T>(this T value, Action<T> action) { if (value is not null) action(value); }
}
