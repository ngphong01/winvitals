using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WinVitals.Core;

namespace WinVitals.App.Converters;

public sealed class RiskToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not RiskLevel r) return Brushes.Transparent;
        return r switch
        {
            RiskLevel.Safe => new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)),
            RiskLevel.Low => new SolidColorBrush(Color.FromRgb(0x94, 0xE2, 0xD5)),
            RiskLevel.Medium => new SolidColorBrush(Color.FromRgb(0xFA, 0xB3, 0x87)),
            RiskLevel.High => new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
            RiskLevel.Critical => new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
