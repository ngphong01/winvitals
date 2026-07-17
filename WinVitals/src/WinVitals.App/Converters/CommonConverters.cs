using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace WinVitals.App.Converters;

public sealed class EqIntConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c)
    {
        if (value is not int v) return Visibility.Collapsed;
        if (!int.TryParse(parameter?.ToString(), out var p)) return Visibility.Collapsed;
        return v == p ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class GreaterThanConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c)
    {
        if (value is not int v) return false;
        if (!int.TryParse(parameter?.ToString(), out var p)) return false;
        return v > p;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class LessThanConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c)
    {
        if (value is not int v) return Visibility.Collapsed;
        if (!int.TryParse(parameter?.ToString(), out var p)) return Visibility.Collapsed;
        return v < p ? Visibility.Visible : Visibility.Collapsed;
    }
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class InvertBoolConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is bool b ? !b : true;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) =>
        v is bool b ? !b : false;
}

public sealed class InvertVisConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        value is bool b && b ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class EnumEqConverter : IValueConverter
{
    public object Convert(object value, Type t, object parameter, CultureInfo c)
    {
        if (value is null || parameter is null) return false;
        return string.Equals(value.ToString(), parameter.ToString(), StringComparison.OrdinalIgnoreCase);
    }
    public object ConvertBack(object value, Type t, object parameter, CultureInfo c)
    {
        if (value is bool b && b && parameter is not null)
            return Enum.Parse(t, parameter.ToString()!);
        return Binding.DoNothing;
    }
}

public sealed class NotNullConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) => value is not null;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class NonEmptyVisConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}

public sealed class BoolToVisConverter : IValueConverter
{
    public object Convert(object value, Type t, object p, CultureInfo c)
        => value is bool b && b ? Visibility.Visible : Visibility.Collapsed;
    public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
}
