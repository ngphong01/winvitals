using System.IO;
using System.Reflection;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.App.Services;

namespace WinVitals.App.ViewModels;

public sealed partial class AboutViewModel : ObservableObject
{
    private readonly IUpdateService _updates;
    private readonly IAppNotifier _notify;
    private readonly ILocalizationService _loc;

    [ObservableProperty] private bool isCheckingUpdate;
    [ObservableProperty] private bool isDownloadingUpdate;
    [ObservableProperty] private int downloadProgress;
    [ObservableProperty] private string updateStatusText = "";
    [ObservableProperty] private string? latestVersion;
    [ObservableProperty] private string? releaseNotes;
    [ObservableProperty] private bool hasUpdateReady;

    public string Version { get; }
    public string BuildDate { get; }
    public string DotNetVersion => Environment.Version.ToString();
    public string OSVersion => Environment.OSVersion.VersionString;

    public string Author { get; } = "Đào Văn Phong";
    public string AuthorUrl { get; } = "https://github.com/daovanphong";

    public string LicenseText { get; } = @"MIT License

Copyright (c) 2026 Đào Văn Phong

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the ""Software""), to deal
in the Software without restriction. THE SOFTWARE IS PROVIDED ""AS IS"".";

    public string Changelog { get; } = @"v1.0.0 — Production release: onboarding, i18n, auto-update, telemetry, crash reporter
v0.9.0 — Auto-update Velopack, telemetry opt-in, crash reporter
v0.8.0 — i18n (VN/EN), Command Palette (Ctrl+K), shortcuts
v0.7.0 — Statistics page with charts and insights
v0.6.0 — Rules editor with test runner, import/export
v0.5.0 — Settings, exclusions, scheduling
v0.4.0 — Process manager and disk health
v0.3.0 — Clean + Quarantine engine
v0.2.0 — Smart rule engine
v0.1.0 — Initial skeleton";

    public string[] OpenSource { get; } = new[]
    {
        "CommunityToolkit.Mvvm (MIT)",
        "LiveChartsCore (MIT)",
        "LiteDB (MIT)",
        "Serilog (Apache-2.0)",
        "xUnit (Apache-2.0)"
    };

    public AboutViewModel(IUpdateService updates, IAppNotifier notify, ILocalizationService loc)
    {
        _updates = updates;
        _notify = notify;
        _loc = loc;
        var asm = Assembly.GetExecutingAssembly();
        Version = asm.GetName().Version?.ToString(3) ?? "1.0.0";
        try { BuildDate = new FileInfo(asm.Location).LastWriteTime.ToString("yyyy-MM-dd HH:mm"); }
        catch { BuildDate = "unknown"; }
    }

    [RelayCommand]
    private void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
        catch { }
    }

    [RelayCommand]
    private async Task CheckUpdateAsync()
    {
        IsCheckingUpdate = true;
        UpdateStatusText = "";
        try
        {
            var status = await _updates.CheckAsync();
            if (status.HasUpdate)
            {
                LatestVersion = status.LatestVersion;
                ReleaseNotes = status.ReleaseNotes;
                UpdateStatusText = string.Format(_loc["Update_AvailableFmt"], status.LatestVersion);
            }
            else UpdateStatusText = _loc["Update_UpToDate"];
        }
        catch { UpdateStatusText = _loc["Update_CheckFailed"]; }
        finally { IsCheckingUpdate = false; }
    }

    [RelayCommand]
    private async Task DownloadUpdateAsync()
    {
        IsDownloadingUpdate = true;
        DownloadProgress = 0;
        var progress = new Progress<int>(p => DownloadProgress = p);
        try
        {
            var ok = await _updates.DownloadAsync(progress);
            HasUpdateReady = ok;
            if (ok) UpdateStatusText = _loc["Update_ReadyToRestart"];
        }
        finally { IsDownloadingUpdate = false; }
    }

    [RelayCommand]
    private void RestartNow() => _updates.ApplyAndRestart();
}
