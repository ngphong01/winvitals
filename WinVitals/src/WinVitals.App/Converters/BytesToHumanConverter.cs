using System.Globalization;
using System.Windows.Data;

namespace WinVitals.App.Converters;

public sealed class BytesToHumanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        long b = value switch
        {
            long l => l,
            int i => i,
            double d => (long)d,
            _ => 0
        };

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        double size = b;
        int u = 0;
        while (size >= 1024 && u < units.Length - 1) { size /= 1024; u++; }
        return $"{size:0.##} {units[u]}";
    }

    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
