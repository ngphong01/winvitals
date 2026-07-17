using System.IO;
using System.Net.Http;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Velopack;
using WinVitals.App.ViewModels;
using WinVitals.App.Views;
using WinVitals.Core.Telemetry;
using WinVitals.Services;

namespace WinVitals.App;

public partial class App : Application
{
    public static IHost Host { get; private set; } = null!;
    public IServiceProvider Services => Host.Services;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Velopack MUST run first
        VelopackApp.Build().Run();

        var logDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WinVitals", "logs");
        Directory.CreateDirectory(logDir);

        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console()
            .WriteTo.File(Path.Combine(logDir, "winvitals-.log"),
                rollingInterval: RollingInterval.Day, retainedFileCountLimit: 7)
            .CreateLogger();

        Host = Microsoft.Extensions.Hosting.Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                var dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "WinVitals");
                services.AddWinVitalsServices(dataDir);

                // Telemetry
                services.AddSingleton<ITelemetry>(sp =>
                {
                    var settings = sp.GetRequiredService<WinVitals.Core.Storage.SettingsStore>().Get();
                    if (!settings.AnonymousTelemetry) return new NullTelemetry();
                    var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var endp = new Uri("https://telemetry.winvitals.example.com/events");
                    var id = AnonymousIdGenerator.GetOrCreate(dataDir);
                    var log = sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<HttpTelemetry>>();
                    return new HttpTelemetry(http, endp, id, log) { IsEnabled = true };
                });

                services.AddSingleton<WinVitals.App.Services.TelegramNotifier>();
                services.AddSingleton<MainWindow>();
                services.AddSingleton<DashboardViewModel>();
                services.AddSingleton<PerformanceViewModel>();
                services.AddSingleton<ScanViewModel>();
                services.AddSingleton<CleanViewModel>();
                services.AddSingleton<QuarantineViewModel>();
                services.AddSingleton<ProcessesViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddSingleton<RulesViewModel>();
                services.AddSingleton<StatisticsViewModel>();
                services.AddSingleton<WinVitals.App.Services.IAppNotifier, WinVitals.App.Services.AppNotifier>();
                services.AddSingleton<WinVitals.App.Services.IThemeManager, WinVitals.App.Services.ThemeManager>();
                services.AddSingleton<WinVitals.App.Services.IStartupRegistrar, WinVitals.App.Services.StartupRegistrar>();
                services.AddSingleton<WinVitals.App.Services.IAppCommands, WinVitals.App.Services.AppCommands>();
                services.AddSingleton<WinVitals.App.Services.ILocalizationService, WinVitals.App.Services.LocalizationService>();
                services.AddSingleton<WinVitals.App.Services.IUpdateService, WinVitals.App.Services.UpdateService>();
                services.AddSingleton<WinVitals.App.Services.UpdateHostedService>();
                services.AddHostedService(sp => sp.GetRequiredService<WinVitals.App.Services.UpdateHostedService>());
                services.AddSingleton<WinVitals.App.Services.ICrashReporter>(sp =>
                    new WinVitals.App.Services.CrashReporter(
                        sp.GetRequiredService<ITelemetry>(),
                        sp.GetRequiredService<WinVitals.App.Services.ILocalizationService>(),
                        dataDir));

                // Help
                services.AddTransient<HelpViewModel>();
                services.AddTransient<HelpPage>();

                services.AddTransient<RuleEditorViewModel>();
                services.AddTransient<AboutViewModel>();

                services.AddSingleton<DashboardPage>();
                services.AddSingleton<PerformancePage>();
                services.AddSingleton<ScanPage>();
                services.AddSingleton<CleanPage>();
                services.AddSingleton<QuarantinePage>();
                services.AddSingleton<SettingsPage>();
                services.AddSingleton<RulesPage>();
                services.AddSingleton<StatisticsPage>();
                services.AddTransient<AboutPage>();
            })
            .Build();

        await Host.StartAsync();

        // Onboarding for first run
        var settingsStore = Host.Services.GetRequiredService<WinVitals.Core.Storage.SettingsStore>();
        var settings = settingsStore.Get();
        if (!settingsStore.IsOnboardingCompleted())
        {
            var loc = Host.Services.GetRequiredService<WinVitals.App.Services.ILocalizationService>();
            var theme = Host.Services.GetRequiredService<WinVitals.App.Services.IThemeManager>();
            var scanVm = Host.Services.GetRequiredService<ScanViewModel>();
            bool done = false;
            var onboarding = new Views.Onboarding.OnboardingWindow(
                new OnboardingViewModel(settingsStore, loc, theme, () => { done = true; }),
                onComplete =>
                {
                    done = true;
                    onComplete?.Invoke();
                });
            onboarding.Closed += (_, _) =>
            {
                if (!done) return; // user closed window without finishing
                // Already saved by OnboardingViewModel.Finish
            };
            onboarding.ShowDialog();
        }

        var main = Host.Services.GetRequiredService<MainWindow>();
        main.Show();

        // Telegram notification
        _ = Task.Run(async () =>
        {
            try
            {
                var sysInfo = Host.Services.GetRequiredService<DashboardViewModel>().SysInfo;
                if (sysInfo is not null)
                {
                    var msg = $"<b>Machine:</b> {sysInfo.MachineName}\n"
                            + $"<b>User:</b> {sysInfo.UserName}\n"
                            + $"<b>OS:</b> {sysInfo.OsVersion} ({sysInfo.OsArchitecture})\n"
                            + $"<b>CPU:</b> {sysInfo.CpuName}\n"
                            + $"<b>Cores:</b> {sysInfo.CpuCores}c/{sysInfo.CpuLogicalProcessors}t\n"
                            + $"<b>RAM:</b> {sysInfo.RamTotalGb:F1} GB\n"
                            + $"<b>GPU:</b> {sysInfo.GpuName}\n"
                            + $"<b>.NET:</b> {sysInfo.DotNetVersion}";
                    await Host.Services.GetRequiredService<WinVitals.App.Services.TelegramNotifier>()
                        .SendSystemInfoAsync(msg);
                }
            }
            catch { /* Don't block startup */ }
        });

        // Crash reporter
        var crash = Host.Services.GetRequiredService<WinVitals.App.Services.ICrashReporter>();
        crash.Install();

        // Apply saved theme
        var themeMgr = Host.Services.GetRequiredService<WinVitals.App.Services.IThemeManager>();
        var savedSettings = Host.Services.GetRequiredService<WinVitals.Core.Storage.SettingsStore>().Get();
        themeMgr.Apply(savedSettings.Theme);

        // Scheduler notification hook
        var scheduler = Host.Services.GetRequiredService<WinVitals.Services.Scheduling.ISchedulerService>();
        var notifier = Host.Services.GetRequiredService<WinVitals.App.Services.IAppNotifier>();
        scheduler.RunCompleted += (_, args) =>
        {
            var mb = args.BytesFreed / 1024.0 / 1024.0;
            notifier.Success("Scheduled cleanup done",
                $"Cleaned {args.ItemsCleaned} items, freed {mb:0.0} MB in {args.Elapsed.TotalSeconds:0}s");
        };

        base.OnStartup(e);
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        await Host.StopAsync();
        Host.Dispose();
        Log.CloseAndFlush();
        base.OnExit(e);
    }
}
