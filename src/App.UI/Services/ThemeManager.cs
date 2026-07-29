using System.Windows;
using System.Windows.Media;

namespace AppUI.Services;

/// <summary>
/// Manages application themes — Light, Dark, High Contrast.
/// Switches ResourceDictionary at runtime for instant theme changes.
/// Requirements: 4.1, 4.2, 4.3, 4.4
/// </summary>
public enum AppTheme { Dark, Light, HighContrast }

public class ThemeManager
{
    private AppTheme _currentTheme = AppTheme.Dark;

    public AppTheme CurrentTheme => _currentTheme;

    public void ApplyTheme(AppTheme theme)
    {
        _currentTheme = theme;

        var app = Application.Current;
        var resources = app.Resources;

        // Clear existing theme brushes
        resources.Remove("MainBg");
        resources.Remove("SidebarBg");
        resources.Remove("CardBg");
        resources.Remove("CardBorder");
        resources.Remove("TextPrimary");
        resources.Remove("TextSecondary");
        resources.Remove("TextMuted");
        resources.Remove("Accent");
        resources.Remove("Success");
        resources.Remove("Warning");
        resources.Remove("Danger");
        resources.Remove("SurfaceAlt");
        resources.Remove("InputBg");
        resources.Remove("Overlay");

        switch (theme)
        {
            case AppTheme.Dark:
                ApplyDarkTheme(resources);
                break;
            case AppTheme.Light:
                ApplyLightTheme(resources);
                break;
            case AppTheme.HighContrast:
                ApplyHighContrastTheme(resources);
                break;
        }
    }

    private static void ApplyDarkTheme(ResourceDictionary r)
    {
        r["MainBg"] = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x1A));
        r["SidebarBg"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x16));
        r["CardBg"] = new SolidColorBrush(Color.FromRgb(0x14, 0x14, 0x2E));
        r["CardBorder"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2B, 0x42));
        r["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0xE6, 0xE6, 0xFA));
        r["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0xA0, 0xA0, 0xC0));
        r["TextMuted"] = new SolidColorBrush(Color.FromRgb(0x6E, 0x6E, 0x8A));
        r["Accent"] = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA));
        r["Success"] = new SolidColorBrush(Color.FromRgb(0x9E, 0xCE, 0x6A));
        r["Warning"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xAF, 0x68));
        r["Danger"] = new SolidColorBrush(Color.FromRgb(0xF7, 0x76, 0x8E));
        r["SurfaceAlt"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x30));
        r["InputBg"] = new SolidColorBrush(Color.FromRgb(0x0C, 0x0C, 0x1A));
        r["Overlay"] = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00));
    }

    private static void ApplyLightTheme(ResourceDictionary r)
    {
        r["MainBg"] = new SolidColorBrush(Color.FromRgb(0xF5, 0xF5, 0xFA));
        r["SidebarBg"] = new SolidColorBrush(Color.FromRgb(0xEA, 0xEA, 0xF0));
        r["CardBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        r["CardBorder"] = new SolidColorBrush(Color.FromRgb(0xDC, 0xDC, 0xE4));
        r["TextPrimary"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x2E));
        r["TextSecondary"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x70));
        r["TextMuted"] = new SolidColorBrush(Color.FromRgb(0x90, 0x90, 0xA0));
        r["Accent"] = new SolidColorBrush(Color.FromRgb(0x2E, 0x6A, 0xD8));
        r["Success"] = new SolidColorBrush(Color.FromRgb(0x2E, 0x8B, 0x57));
        r["Warning"] = new SolidColorBrush(Color.FromRgb(0xC4, 0x7A, 0x20));
        r["Danger"] = new SolidColorBrush(Color.FromRgb(0xD1, 0x3B, 0x3B));
        r["SurfaceAlt"] = new SolidColorBrush(Color.FromRgb(0xEE, 0xEE, 0xF4));
        r["InputBg"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
        r["Overlay"] = new SolidColorBrush(Color.FromArgb(0x40, 0x00, 0x00, 0x00));
    }

    private static void ApplyHighContrastTheme(ResourceDictionary r)
    {
        r["MainBg"] = new SolidColorBrush(Colors.Black);
        r["SidebarBg"] = new SolidColorBrush(Colors.Black);
        r["CardBg"] = new SolidColorBrush(Colors.Black);
        r["CardBorder"] = new SolidColorBrush(Colors.White);
        r["TextPrimary"] = new SolidColorBrush(Colors.White);
        r["TextSecondary"] = new SolidColorBrush(Colors.White);
        r["TextMuted"] = new SolidColorBrush(Colors.Silver);
        r["Accent"] = new SolidColorBrush(Colors.Cyan);
        r["Success"] = new SolidColorBrush(Colors.Lime);
        r["Warning"] = new SolidColorBrush(Colors.Yellow);
        r["Danger"] = new SolidColorBrush(Colors.Red);
        r["SurfaceAlt"] = new SolidColorBrush(Colors.Black);
        r["InputBg"] = new SolidColorBrush(Colors.Black);
        r["Overlay"] = new SolidColorBrush(Color.FromArgb(0x80, 0x00, 0x00, 0x00));
    }
}
