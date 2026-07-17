using System.IO;
using System.Windows;
using Serilog;
using Serilog.Events;

namespace AppUI;

public partial class App : Application
{
    public static ILogger Log { get; private set; } = null!;

    protected override void OnStartup(StartupEventArgs e)
    {
        var logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Directory.CreateDirectory(logDir);

        Log = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .WriteTo.Console(restrictedToMinimumLevel: LogEventLevel.Information)
            .WriteTo.File(
                Path.Combine(logDir, "whm-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 30,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("=== Windows Health Manager started ===");
        Log.Information("OS: {OS}, .NET: {NET}", Environment.OSVersion, Environment.Version);
        Log.Information("Log directory: {Dir}", logDir);

        base.OnStartup(e);
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== Windows Health Manager shutting down ===");
        global::Serilog.Log.CloseAndFlush();
        base.OnExit(e);
    }
}

