using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace WinVitals.App.Services;

public interface ILocalizationService : INotifyPropertyChanged
{
    void SetLanguage(string cultureCode);
    string CurrentCulture { get; }
    string this[string key] { get; }
    string T(string key);
}

public sealed class LocalizationService : ILocalizationService
{
    private static readonly ResourceManager Rm =
        new("WinVitals.App.Resources.Strings", typeof(LocalizationService).Assembly);

    public event PropertyChangedEventHandler? PropertyChanged;
    public string CurrentCulture { get; private set; } = "en";

    public string this[string key] =>
        Rm.GetString(key, CultureInfo.CurrentUICulture) ?? $"[{key}]";

    public string T(string key)
    {
        // Convert "Nav.Dashboard" -> "Nav_Dashboard"
        var resKey = key.Replace('.', '_');
        return this[resKey];
    }

    public void SetLanguage(string cultureCode)
    {
        try
        {
            var ci = new CultureInfo(cultureCode);
            CultureInfo.CurrentUICulture = ci;
            CultureInfo.DefaultThreadCurrentUICulture = ci;
            CurrentCulture = cultureCode;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
        catch { }
    }
}
