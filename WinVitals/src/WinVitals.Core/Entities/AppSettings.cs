namespace WinVitals.Core.Entities;

public enum AppTheme { System, Light, Dark }
public enum ScheduleFrequency { Never, Daily, Weekly, Monthly }

public sealed class AppSettings
{
    public int Id { get; set; } = 1;
    public AppTheme Theme { get; set; } = AppTheme.System;
    public string Language { get; set; } = "en";
    public bool UseMicaBackdrop { get; set; } = true;
    public bool ReduceMotion { get; set; }
    public int QuarantineRetentionDays { get; set; } = 14;
    public long MaxQuarantineSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024;
    public bool AnonymousTelemetry { get; set; }
    public List<string> ExcludedPatterns { get; set; } = new();
    public ScheduleFrequency ScheduleFrequency { get; set; } = ScheduleFrequency.Never;
    public ScanPreset SchedulePreset { get; set; } = ScanPreset.Quick;
    public TimeOnly ScheduleTime { get; set; } = new(3, 0);
    public DayOfWeek ScheduleDayOfWeek { get; set; } = DayOfWeek.Sunday;
    public int ScheduleDayOfMonth { get; set; } = 1;
    public bool ScheduleAutoConfirm { get; set; }
    public DateTime? LastScheduledRunUtc { get; set; }
    public bool OnboardingCompleted { get; set; }
    public bool LaunchAtStartup { get; set; }
    public bool StartMinimized { get; set; }
    public bool MinimizeToTray { get; set; } = true;
}
