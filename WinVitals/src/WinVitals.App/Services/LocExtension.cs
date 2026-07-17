using System.Windows;
using System.Windows.Data;
using System.Windows.Markup;

namespace WinVitals.App.Services;

public sealed class LocExtension : MarkupExtension
{
    public string Key { get; set; } = "";

    public LocExtension() { }
    public LocExtension(string key) => Key = key;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var loc = (Application.Current as App)?.Services
            .GetService(typeof(ILocalizationService)) as ILocalizationService;
        if (loc is null) return $"[{Key}]";

        // Convert dots to underscores to match resx keys
        var resKey = Key.Replace('.', '_');
        var binding = new Binding($"[{resKey}]")
        {
            Source = loc,
            Mode = BindingMode.OneWay
        };
        return binding.ProvideValue(serviceProvider);
    }
}
