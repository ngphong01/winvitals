using System.IO;
using System.Threading;
using System.Windows;
using App.Cleaner;
using App.Core;
using App.Performance;
using App.Scanner;
using App.Storage;
using App.Storage.Repositories;
using App.Storage.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using ILogger = Serilog.ILogger;

namespace AppUI;

public partial class App : Application
{
    public static ILogger Log { get; private set; } = null!;
    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        // Single-instance check
        var mutex = new Mutex(true, "WindowsHealthManager_Mutex", out bool isNew);
        if (!isNew)
        {
            base.OnStartup(e);
            MessageBox.Show("Windows Health Manager đang chạy rồi!", "Đã mở",
                MessageBoxButton.OK, MessageBoxImage.Information);
            Shutdown();
            return;
        }

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

        Log.Information("=== Windows Health Manager v2 started ===");
        Log.Information("OS: {OS}, .NET: {NET}", Environment.OSVersion, Environment.Version);

        // Extract embedded rules to disk (for single-file publish)
        ExtractEmbeddedRules();

        var provider = ConfigureServices();
        Services = provider;

        // ---- CLI Mode ----
        if (e.Args.Length > 0)
        {
            var runner = provider.GetRequiredService<CliRunner>();
            Environment.ExitCode = await runner.RunAsync(e.Args);
            Shutdown();
            return;
        }

        Log.Information("DI container initialized — GUI mode");

        // Auto-install: if running from Downloads/Desktop, offer to install
        if (TryAutoInstall()) { Shutdown(); return; }

        base.OnStartup(e);
    }

    private static IServiceProvider ConfigureServices()
    {
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(b => b.AddSerilog(Log, dispose: true));

        // Infrastructure — Database
        // 1 file duy nhất: %LocalAppData%\WindowsHealthManager\whm.db
        // Cùng instance chia sẻ cho repository layer lẫn IStorageProvider
        var dbPath = DatabaseProvider.GetDefaultDatabasePath();
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);
        services.AddSingleton(new LiteDatabaseProvider(dbPath));
        services.AddSingleton<IStorageProvider>(
            sp => new DatabaseProvider(sp.GetRequiredService<LiteDatabaseProvider>()));
        services.AddSingleton<MigrationService>();

        // Infrastructure — File System
        services.AddSingleton<IFileSystemService, FileSystemService>();

        // Repositories
        services.AddSingleton<IScanRepository, ScanRepository>();
        services.AddSingleton<ICleanRepository, CleanRepository>();
        services.AddSingleton<IRuleRepository, RuleRepository>();
        services.AddSingleton<IQuarantineRepository, QuarantineRepository>();
        services.AddSingleton<IPerformanceRepository, PerformanceRepository>();
        services.AddSingleton<IUnitOfWork, UnitOfWork>();

        // Domain — Engines
        var rulesDir = Path.Combine(AppContext.BaseDirectory, "rules");
        services.AddSingleton<IRuleEngine>(_ => new RuleEngine(rulesDir));
        services.AddSingleton<IRiskEngine>(_ => new RiskEngine(rulesDir));
        services.AddSingleton<IPerformanceAnalyzer, PerformanceAnalyzer>();

        // Application Services
        services.AddSingleton<IScannerService, ScannerService>();
        services.AddSingleton<ICleanerService, CleanerService>();
        services.AddSingleton<IPerformanceService, PerformanceService>();
        services.AddSingleton<IQuarantineService, QuarantineService>();

        // App Services
        services.AddSingleton<Services.ThemeManager>();
        services.AddSingleton<CliRunner>();
        services.AddSingleton<SchedulerService>();
        services.AddSingleton<RulePackManager>();
        services.AddSingleton<AutoUpdateService>();
        services.AddSingleton<ReportGenerator>();
        services.AddSingleton<SystemDiagnostic>();
        services.AddSingleton<SystemBooster>();

        // ViewModels
        services.AddTransient<ViewModels.DashboardViewModel>();
        services.AddTransient<ViewModels.PerformanceViewModel>();
        services.AddTransient<ViewModels.SettingsViewModel>();
        services.AddTransient<ViewModels.ScannerViewModel>();
        services.AddTransient<ViewModels.QuarantineViewModel>();

        // Scanners
        services.AddSingleton<IScanner, DiskScanner>();
        services.AddSingleton<IScanner, LargeFileFinder>();
        services.AddSingleton<IScanner, OrphanDetector>();
        services.AddSingleton<IScanner, DuplicateFinder>();
        services.AddSingleton<IScanner, DevCacheScanner>();
        services.AddSingleton<IScanner, StaleProjectDetector>();
        services.AddSingleton<IScanner, CloudCacheScanner>();
        services.AddSingleton<IScanner, BrowserCacheScanner>();
        services.AddSingleton<IScanner, WindowsStoreScanner>();

        return services.BuildServiceProvider();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        Log.Information("=== Windows Health Manager shutting down ===");
        global::Serilog.Log.CloseAndFlush();
        base.OnExit(e);
    }

    private static void ExtractEmbeddedRules()
    {
        try
        {
            var rulesDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "rules");
            Directory.CreateDirectory(rulesDir);
            var assembly = typeof(App).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.Contains(".rules.")) continue;
                var parts = name.Split('.');
                var fileName = parts[^2] + "." + parts[^1];
                var target = Path.Combine(rulesDir, fileName);
                if (File.Exists(target)) continue;
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream == null) continue;
                using var fs = new FileStream(target, FileMode.Create, FileAccess.Write);
                stream.CopyTo(fs);
            }
        }
        catch { }
    }

    private static bool TryAutoInstall()
    {
#if DEBUG
        return false;
#else
        var exePath = Environment.ProcessPath;
        if (string.IsNullOrEmpty(exePath)) return false;
        var installDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "WinHealth");
        if (exePath.StartsWith(installDir, StringComparison.OrdinalIgnoreCase)) return false;

        // Check if user previously declined or explicitly set portable mode
        var flagPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WHM", "portable.flag");
        if (File.Exists(flagPath) || File.Exists(Path.Combine(AppContext.BaseDirectory, ".portable"))) return false;

        var result = MessageBox.Show(
            $"Bạn muốn cài đặt Windows Health Manager vào máy tính?\n\nThư mục: {installDir}\nSẽ tạo shortcut Desktop & Start Menu.\n\nChọn Yes để cài, No để chạy portable.",
            "Cài Đặt Windows Health Manager", MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(flagPath)!);
                File.WriteAllText(flagPath, "portable");
            }
            catch { }
            return false;
        }

        try
        {
            foreach (var p in System.Diagnostics.Process.GetProcessesByName("WinHealth"))
            { try { if (p.Id != Environment.ProcessId) p.Kill(); } catch { } }

            var sourceDir = Path.GetDirectoryName(exePath)!;
            if (Directory.Exists(installDir)) Directory.Delete(installDir, true);
            Directory.CreateDirectory(installDir);
            File.Copy(exePath, Path.Combine(installDir, "WinHealth.exe"), true);
            var rulesDir = Path.Combine(sourceDir, "rules");
            if (Directory.Exists(rulesDir))
            {
                var destRules = Path.Combine(installDir, "rules");
                Directory.CreateDirectory(destRules);
                foreach (var f in Directory.GetFiles(rulesDir)) File.Copy(f, Path.Combine(destRules, Path.GetFileName(f)), true);
            }

            using var ps = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell",
                $"-NoProfile -Command \"$ws=New-Object -ComObject WScript.Shell;$s=$ws.CreateShortcut('{Environment.GetFolderPath(Environment.SpecialFolder.Desktop)}\\Windows Health Manager.lnk');$s.TargetPath='{Path.Combine(installDir, "WinHealth.exe")}';$s.WorkingDirectory='{installDir}';$s.Save()\"")
            { CreateNoWindow = true, UseShellExecute = false });
            ps?.WaitForExit(3000);

            var startMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "WinHealth");
            Directory.CreateDirectory(startMenu);
            using var ps2 = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("powershell",
                $"-NoProfile -Command \"$ws=New-Object -ComObject WScript.Shell;$s=$ws.CreateShortcut('{startMenu}\\Windows Health Manager.lnk');$s.TargetPath='{Path.Combine(installDir, "WinHealth.exe")}';$s.WorkingDirectory='{installDir}';$s.Save()\"")
            { CreateNoWindow = true, UseShellExecute = false });
            ps2?.WaitForExit(3000);

            try
            {
                var rk = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\WinHealth");
                if (rk != null) { rk.SetValue("DisplayName", "Windows Health Manager"); rk.SetValue("DisplayVersion", "2.0.0"); rk.SetValue("Publisher", "Dao Van Phong"); rk.SetValue("UninstallString", $"cmd /c rmdir /s /q \"{installDir}\""); rk.SetValue("InstallLocation", installDir); rk.SetValue("NoModify", 1); rk.SetValue("NoRepair", 1); rk.Close(); }
            }
            catch { }

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            { FileName = Path.Combine(installDir, "WinHealth.exe"), WorkingDirectory = installDir, UseShellExecute = true });

            MessageBox.Show($"Đã cài đặt thành công!\n\nDesktop: Windows Health Manager\nStart Menu: WinHealth → Windows Health Manager", "Cài Đặt Hoàn Tất", MessageBoxButton.OK, MessageBoxImage.Information);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Cài đặt thất bại: {ex.Message}\n\nApp sẽ chạy ở chế độ portable.", "Lỗi Cài Đặt", MessageBoxButton.OK, MessageBoxImage.Warning);
            return false;
        }
#endif
    }
}
