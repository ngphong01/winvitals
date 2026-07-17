using System.IO;
using System.Text.Json;
using System.Windows;
using WinVitals.Core.Telemetry;

namespace WinVitals.App.Services;

public interface ICrashReporter
{
    void Install();
    Task<string> SaveCrashDumpAsync(Exception ex);
}

public sealed class CrashReporter : ICrashReporter
{
    private readonly ITelemetry _telemetry;
    private readonly string _crashDir;
    private readonly ILocalizationService _loc;

    public CrashReporter(ITelemetry telemetry, ILocalizationService loc, string dataDir)
    {
        _telemetry = telemetry;
        _loc = loc;
        _crashDir = Path.Combine(dataDir, "crashes");
        Directory.CreateDirectory(_crashDir);
    }

    public void Install()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            HandleCrash(e.ExceptionObject as Exception, terminating: e.IsTerminating);

        Application.Current.DispatcherUnhandledException += (_, e) =>
        {
            HandleCrash(e.Exception, terminating: false);
            e.Handled = true;
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            HandleCrash(e.Exception, terminating: false);
            e.SetObserved();
        };
    }

    private async void HandleCrash(Exception? ex, bool terminating)
    {
        if (ex is null) return;
        try
        {
            var path = await SaveCrashDumpAsync(ex);
            _telemetry.TrackException(ex, new Dictionary<string, object>
            {
                ["terminating"] = terminating,
                ["reportSaved"] = path
            });
            if (!terminating) ShowCrashDialog(ex, path);
        }
        catch { /* never throw in crash handler */ }
    }

    public async Task<string> SaveCrashDumpAsync(Exception ex)
    {
        var name = $"crash-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}.json";
        var path = Path.Combine(_crashDir, name);

        var report = new
        {
            timestamp = DateTime.UtcNow,
            appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(),
            osVersion = Environment.OSVersion.VersionString,
            dotnetVersion = Environment.Version.ToString(),
            culture = System.Globalization.CultureInfo.CurrentUICulture.Name,
            exception = new
            {
                type = ex.GetType().FullName,
                message = Redact(ex.Message),
                stackTrace = Redact(ex.StackTrace ?? ""),
                inner = ex.InnerException is null ? null : new
                {
                    type = ex.InnerException.GetType().FullName,
                    message = Redact(ex.InnerException.Message)
                }
            }
        };

        var json = JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json);
        return path;
    }

    private void ShowCrashDialog(Exception ex, string reportPath)
    {
        Application.Current.Dispatcher.Invoke(() =>
        {
            var win = new Views.CrashReportWindow(ex, reportPath)
            {
                Owner = Application.Current.MainWindow
            };
            win.ShowDialog();
        });
    }

    public static string Redact(string s)
    {
        // User home paths (more specific first)
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"[A-Za-z]:\\Users\\[^\\\s""']+(\\[^\\\s""']+)*",
            @"C:\Users\<user>\...");
        // Generic paths (exclude already-redacted <user> and <path> markers)
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"[A-Za-z]:\\(?!(Users\\<user>))[^\s""']+",
            "<path>");
        s = System.Text.RegularExpressions.Regex.Replace(s,
            @"[\w\.-]+@[\w\.-]+\.\w+", "<email>");
        return s;
    }
}
