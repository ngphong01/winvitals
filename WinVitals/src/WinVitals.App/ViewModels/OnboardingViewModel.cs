using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.App.Services;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Storage;

namespace WinVitals.App.ViewModels;

public sealed partial class OnboardingViewModel : ObservableObject
{
    private readonly SettingsStore _settings;
    private readonly ILocalizationService _loc;
    private readonly IThemeManager _theme;
    private readonly Action _closeAction;

    [ObservableProperty] private int currentStep;
    [ObservableProperty] private string selectedLanguage = "en";
    [ObservableProperty] private AppTheme selectedTheme = AppTheme.System;
    [ObservableProperty] private bool enableTelemetry;

    [RelayCommand] private void DoSkipScan() { CurrentStep++; }

    public bool CanGoBack => CurrentStep > 0;
    public bool CanGoNext => CurrentStep < 4;
    public bool IsLastStep => CurrentStep == 4;

    public string[] Languages { get; } = { "en", "vi" };
    public AppTheme[] Themes { get; } = Enum.GetValues<AppTheme>();

    public OnboardingViewModel(SettingsStore settings, ILocalizationService loc, IThemeManager theme, Action closeAction)
    {
        _settings = settings; _loc = loc; _theme = theme; _closeAction = closeAction;
    }

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoNext));
        OnPropertyChanged(nameof(IsLastStep));
    }

    partial void OnSelectedThemeChanged(AppTheme value) => _theme.Apply(value);
    partial void OnSelectedLanguageChanged(string value) => _loc.SetLanguage(value);

    [RelayCommand] private void SelectTheme(AppTheme t) => SelectedTheme = t;
    [RelayCommand] private void Next() { if (CanGoNext) CurrentStep++; }
    [RelayCommand] private void Back() { if (CanGoBack) CurrentStep--; }

    [RelayCommand]
    private void Finish()
    {
        var s = _settings.Get();
        s.Theme = SelectedTheme;
        s.AnonymousTelemetry = EnableTelemetry;
        _settings.Save(s);
        // Use method-based access to avoid WPF temp compilation issue
        _settings.SetOnboardingCompleted();
        _closeAction();
    }
}
