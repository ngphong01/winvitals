using System.Collections.ObjectModel;
using System.Windows.Input;
using App.Core;
using AppUI.Services;
using AppUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

namespace AppUI.ViewModels;

/// <summary>
/// ViewModel for the Settings page.
/// Manages theme selection, scan paths, scheduler settings.
/// Requirements: 14.1-14.10
/// </summary>
public class SettingsViewModel : ViewModelBase
{
    public SettingsViewModel()
    {
        Themes = new ObservableCollection<ThemeOption>
        {
            new() { Name = "Tối (Dark)", Value = AppTheme.Dark },
            new() { Name = "Sáng (Light)", Value = AppTheme.Light },
            new() { Name = "Tương phản cao", Value = AppTheme.HighContrast }
        };

        _selectedTheme = Themes[0];
        SwitchThemeCommand = new RelayCommand<ThemeOption>(SwitchTheme);
        ExportSettingsCommand = new RelayCommand(ExportSettings);
        ImportSettingsCommand = new RelayCommand(ImportSettings);
    }

    private ObservableCollection<ThemeOption> _themes = [];
    public ObservableCollection<ThemeOption> Themes
    {
        get => _themes;
        set => SetProperty(ref _themes, value);
    }

    private ThemeOption _selectedTheme;
    public ThemeOption SelectedTheme
    {
        get => _selectedTheme;
        set
        {
            if (SetProperty(ref _selectedTheme, value) && value != null)
            {
                App.Services.GetRequiredService<Services.ThemeManager>().ApplyTheme(value.Value);
            }
        }
    }

    private bool _launchOnStartup;
    public bool LaunchOnStartup
    {
        get => _launchOnStartup;
        set => SetProperty(ref _launchOnStartup, value);
    }

    private bool _scheduleEnabled;
    public bool ScheduleEnabled
    {
        get => _scheduleEnabled;
        set => SetProperty(ref _scheduleEnabled, value);
    }

    private string _scheduleFrequency = "Hàng tuần";
    public string ScheduleFrequency
    {
        get => _scheduleFrequency;
        set => SetProperty(ref _scheduleFrequency, value);
    }

    private bool _notificationsEnabled = true;
    public bool NotificationsEnabled
    {
        get => _notificationsEnabled;
        set => SetProperty(ref _notificationsEnabled, value);
    }

    public ICommand SwitchThemeCommand { get; }
    public ICommand ExportSettingsCommand { get; }
    public ICommand ImportSettingsCommand { get; }

    private void SwitchTheme(ThemeOption? option)
    {
        if (option == null) return;
        App.Log.Information("Theme switched to {Theme}", option.Name);
    }

    private void ExportSettings() => App.Log.Information("Export settings requested");
    private void ImportSettings() => App.Log.Information("Import settings requested");
}

public class ThemeOption
{
    public string Name { get; set; } = string.Empty;
    public AppTheme Value { get; set; }
}
