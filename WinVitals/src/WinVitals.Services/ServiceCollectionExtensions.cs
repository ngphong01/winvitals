using Microsoft.Extensions.DependencyInjection;
using WinVitals.Core.Rules;
using WinVitals.Core.Storage;
using WinVitals.Services.Cleaning;
using WinVitals.Services.Disks;
using WinVitals.Services.Metrics;
using WinVitals.Services.Processes;
using WinVitals.Services.Quarantine;
using WinVitals.Services.Scanning;

namespace WinVitals.Services;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddWinVitalsServices(
        this IServiceCollection services, string dataDir)
    {
        var rulesDir = Path.Combine(dataDir, "rules");
        var quarantineDir = Path.Combine(dataDir, "quarantine");
        var dbPath = Path.Combine(dataDir, "winvitals.db");

        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(quarantineDir);

        // Storage
        services.AddSingleton(_ => new LiteDbStore(dbPath));
        services.AddSingleton<CleanSessionStore>();
        services.AddSingleton<SettingsStore>();

        // Rules
        services.AddSingleton(_ =>
        {
            var repo = new RuleRepository(rulesDir);
            repo.SaveDefaults();
            return repo;
        });

        // Metrics
        services.AddSingleton<MetricsService>();
        services.AddSingleton<IMetricsService>(sp => sp.GetRequiredService<MetricsService>());
        services.AddHostedService(sp => sp.GetRequiredService<MetricsService>());

        // Scanning
        services.AddSingleton<IScanService, ScanService>();

        // Quarantine + Cleaning
        services.AddSingleton<IQuarantineService>(sp =>
            new QuarantineService(
                sp.GetRequiredService<LiteDbStore>(),
                sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<QuarantineService>>(),
                quarantineDir));

        services.AddSingleton<ICleanService, CleanService>();

        // Background janitor
        services.AddHostedService<QuarantineJanitor>();

        // Processes + Disks
        services.AddSingleton<Processes.IProcessService, Processes.ProcessService>();
        services.AddSingleton<Disks.IDiskService, Disks.DiskService>();

        // Scheduling
        services.AddSingleton<Scheduling.SchedulerService>();
        services.AddSingleton<Scheduling.ISchedulerService>(sp => sp.GetRequiredService<Scheduling.SchedulerService>());
        services.AddHostedService(sp => sp.GetRequiredService<Scheduling.SchedulerService>());

        return services;
    }
}
