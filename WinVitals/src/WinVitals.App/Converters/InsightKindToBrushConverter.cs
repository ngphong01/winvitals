using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Converters;

public sealed class InsightKindToBrushConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
    {
        if (value is not InsightKind k) return Brushes.Gray;
        return k switch
        {
            InsightKind.Success => new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)),
            InsightKind.Warning => new SolidColorBrush(Color.FromRgb(0xFA, 0xB3, 0x87)),
            InsightKind.Tip => new SolidColorBrush(Color.FromRgb(0xCB, 0xA6, 0xF7)),
            _ => new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA))
        };
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
