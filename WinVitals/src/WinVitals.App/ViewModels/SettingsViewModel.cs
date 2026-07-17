using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinVitals.App.Services;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Storage;
using WinVitals.Services.Scheduling;

namespace WinVitals.App.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private readonly IThemeManager _theme;
    private readonly IStartupRegistrar _startup;
    private readonly ISchedulerService _scheduler;
    private readonly IAppNotifier _notify;
    private bool _loading;

    public ObservableCollection<string> Exclusions { get; } = new();

    [ObservableProperty] private AppTheme selectedTheme;
    [ObservableProperty] private bool useMica;
    [ObservableProperty] private bool reduceMotion;
    [ObservableProperty] private int retentionDays;
    [ObservableProperty] private double maxQuarantineGb;
    [ObservableProperty] private ScheduleFrequency scheduleFrequency;
    private readonly ILocalizationService _loc;
    public LanguageChoice[] AllLanguages { get; } = new[] { new LanguageChoice("en", "English"), new LanguageChoice("vi", "Tiếng Việt") };
    [ObservableProperty] private LanguageChoice selectedLanguage;
    [ObservableProperty] private ScanPreset schedulePreset;
    [ObservableProperty] private int scheduleHour;
    [ObservableProperty] private int scheduleMinute;
    [ObservableProperty] private DayOfWeek scheduleDayOfWeek;
    [ObservableProperty] private int scheduleDayOfMonth;
    [ObservableProperty] private bool scheduleAutoConfirm;
    [ObservableProperty] private string nextRunText = "";
    [ObservableProperty] private bool launchAtStartup;
    [ObservableProperty] private bool startMinimized;
    [ObservableProperty] private bool minimizeToTray;
    [ObservableProperty] private bool enableTelemetry;
    [ObservableProperty] private string newExclusion = "";

    public AppTheme[] AllThemes { get; } = Enum.GetValues<AppTheme>();
    public ScheduleFrequency[] AllFrequencies { get; } = Enum.GetValues<ScheduleFrequency>();
    public ScanPreset[] AllPresets { get; } = Enum.GetValues<ScanPreset>();
    public DayOfWeek[] AllDays { get; } = Enum.GetValues<DayOfWeek>();

    public static int[] Hours => Enumerable.Range(0, 24).ToArray();
    public static int[] Minutes => new[] { 0, 5, 10, 15, 20, 25, 30, 35, 40, 45, 50, 55 };

    public SettingsViewModel(
        SettingsStore store, IThemeManager theme, IStartupRegistrar startup,
        ISchedulerService scheduler, IAppNotifier notify, ILocalizationService loc)
    {
        _store = store;
        _theme = theme;
        _startup = startup;
        _scheduler = scheduler;
        _notify = notify;
        _loc = loc;
        BindingOperations.EnableCollectionSynchronization(Exclusions, new object());
        Load();
    }

    private void Load()
    {
        _loading = true;
        var s = _store.Get();
        SelectedTheme = s.Theme;
        UseMica = s.UseMicaBackdrop;
        ReduceMotion = s.ReduceMotion;
        RetentionDays = s.QuarantineRetentionDays;
        MaxQuarantineGb = s.MaxQuarantineSizeBytes / 1024.0 / 1024.0 / 1024.0;
        ScheduleFrequency = s.ScheduleFrequency;
        SchedulePreset = s.SchedulePreset;
        ScheduleHour = s.ScheduleTime.Hour;
        ScheduleMinute = s.ScheduleTime.Minute;
        ScheduleDayOfWeek = s.ScheduleDayOfWeek;
        ScheduleDayOfMonth = s.ScheduleDayOfMonth;
        ScheduleAutoConfirm = s.ScheduleAutoConfirm;
        LaunchAtStartup = _startup.IsEnabled();
        StartMinimized = s.StartMinimized;
        MinimizeToTray = s.MinimizeToTray;
        EnableTelemetry = s.AnonymousTelemetry;
        // TODO: Load language from s.Language once WPF temp compilation issue is resolved
        SelectedLanguage = AllLanguages.First();

        Exclusions.Clear();
        foreach (var e in s.ExcludedPatterns) Exclusions.Add(e);
        UpdateNextRunText();
        _loading = false;
    }

    private void SaveAll()
    {
        if (_loading) return;
        var s = _store.Get();
        s.Theme = SelectedTheme;
        // s.Language = SelectedLanguage.Code; // WPF temp compilation workaround
        s.UseMicaBackdrop = UseMica;
        s.ReduceMotion = ReduceMotion;
        s.QuarantineRetentionDays = Math.Clamp(RetentionDays, 3, 90);
        s.MaxQuarantineSizeBytes = (long)(MaxQuarantineGb * 1024 * 1024 * 1024);
        s.ScheduleFrequency = ScheduleFrequency;
        s.SchedulePreset = SchedulePreset;
        s.ScheduleTime = new TimeOnly(ScheduleHour, ScheduleMinute);
        s.ScheduleDayOfWeek = ScheduleDayOfWeek;
        s.ScheduleDayOfMonth = Math.Clamp(ScheduleDayOfMonth, 1, 28);
        s.ScheduleAutoConfirm = ScheduleAutoConfirm;
        s.StartMinimized = StartMinimized;
        s.AnonymousTelemetry = EnableTelemetry;
        s.MinimizeToTray = MinimizeToTray;
        s.ExcludedPatterns = Exclusions.ToList();
        _store.Save(s);
        UpdateNextRunText();
    }

    partial void OnSelectedThemeChanged(AppTheme v) { _theme.Apply(v); SaveAll(); }
    partial void OnSelectedLanguageChanged(LanguageChoice v) { if (_loading) return; _loc.SetLanguage(v.Code); SaveAll(); }
    partial void OnLaunchAtStartupChanged(bool v) { if (_loading) return; if (v) _startup.Enable(StartMinimized); else _startup.Disable(); }

    partial void OnUseMicaChanged(bool v) => SaveAll();
    partial void OnReduceMotionChanged(bool v) => SaveAll();
    partial void OnRetentionDaysChanged(int v) => SaveAll();
    partial void OnMaxQuarantineGbChanged(double v) => SaveAll();
    partial void OnScheduleFrequencyChanged(ScheduleFrequency v) => SaveAll();
    partial void OnSchedulePresetChanged(ScanPreset v) => SaveAll();
    partial void OnScheduleHourChanged(int v) => SaveAll();
    partial void OnScheduleMinuteChanged(int v) => SaveAll();
    partial void OnScheduleDayOfWeekChanged(DayOfWeek v) => SaveAll();
    partial void OnScheduleDayOfMonthChanged(int v) => SaveAll();
    partial void OnScheduleAutoConfirmChanged(bool v) => SaveAll();
    partial void OnStartMinimizedChanged(bool v) => SaveAll();
    partial void OnMinimizeToTrayChanged(bool v) => SaveAll();
    partial void OnEnableTelemetryChanged(bool v) => SaveAll();

    [RelayCommand]
    private void OpenPrivacyPolicy() =>
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(
            "https://winvitals.example.com/privacy")
        { UseShellExecute = true });

    [RelayCommand]
    private void AddExclusion()
    {
        var v = NewExclusion?.Trim();
        if (string.IsNullOrEmpty(v)) return;
        if (Exclusions.Contains(v, StringComparer.OrdinalIgnoreCase)) return;
        Exclusions.Add(v);
        NewExclusion = "";
        SaveAll();
    }

    [RelayCommand]
    private void RemoveExclusion(string? pattern)
    {
        if (pattern is null) return;
        Exclusions.Remove(pattern);
        SaveAll();
    }

    [RelayCommand]
    private void BrowseFolderExclusion()
    {
        var dlg = new OpenFolderDialog { Title = "Select folder to exclude" };
        if (dlg.ShowDialog() == true)
        {
            var path = dlg.FolderName.TrimEnd('\\') + @"\**";
            NewExclusion = path;
        }
    }

    [RelayCommand]
    private async Task RunScheduledNowAsync()
    {
        _notify.Info("Scheduled run", "Starting scheduled cleanup...");
        await _scheduler.TriggerNowAsync();
        _notify.Success("Scheduled run", "Completed");
        UpdateNextRunText();
    }

    private void UpdateNextRunText()
    {
        var next = _scheduler.NextRunUtc;
        NextRunText = next is null
            ? "Not scheduled"
            : $"Next run: {next.Value.ToLocalTime():yyyy-MM-dd HH:mm}";
    }
}
