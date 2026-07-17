using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using WinVitals.App.Services;
using WinVitals.App.ViewModels;
using WinVitals.App.Views;

namespace WinVitals.App;

public partial class MainWindow : Window
{
    private readonly IServiceProvider _sp;
    private readonly IAppNotifier _notifier;

    public MainWindow(IServiceProvider sp, IAppNotifier notifier)
    {
        _sp = sp;
        _notifier = notifier;
        InitializeComponent();
        _notifier.SetHost(NotificationHost);
        Loaded += OnLoaded;
        KeyDown += OnWindowKeyDown;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        NavigateTo(typeof(DashboardPage));
        RegisterAppCommands();
    }

    private void OnWindowKeyDown(object sender, KeyEventArgs e)
    {
        if (e.KeyboardDevice.Modifiers != ModifierKeys.Control) return;

        switch (e.Key)
        {
            case Key.K:
                OpenCommandPalette();
                e.Handled = true;
                break;
            case Key.D1: NavigateTo(typeof(DashboardPage)); e.Handled = true; break;
            case Key.D2: NavigateTo(typeof(ScanPage)); e.Handled = true; break;
            case Key.D3: NavigateTo(typeof(CleanPage)); e.Handled = true; break;
            case Key.D4: NavigateTo(typeof(PerformancePage)); e.Handled = true; break;
            case Key.D5: NavigateTo(typeof(QuarantinePage)); e.Handled = true; break;
            case Key.D6: NavigateTo(typeof(RulesPage)); e.Handled = true; break;
            case Key.D7: NavigateTo(typeof(StatisticsPage)); e.Handled = true; break;
            case Key.D8: NavigateTo(typeof(SettingsPage)); e.Handled = true; break;
            case Key.OemComma or Key.D9: NavigateTo(typeof(AboutPage)); e.Handled = true; break;
        }
        if (e.Key == Key.F1) { NavigateTo(typeof(HelpPage)); e.Handled = true; }
    }

    private void OpenCommandPalette()
    {
        var cmds = _sp.GetRequiredService<IAppCommands>();
        CommandPaletteWindow? win = null;
        win = new CommandPaletteWindow(new CommandPaletteViewModel(cmds, () => win!.Close())) { Owner = this };
        win.ShowDialog();
    }

    private void RegisterAppCommands()
    {
        var cmds = _sp.GetRequiredService<IAppCommands>();
        var loc = _sp.GetRequiredService<ILocalizationService>();

        cmds.Register(new AppCommand("nav:dashboard", () => loc.T("Nav.Dashboard"), "Navigation", "Ctrl+1",
            () => NavigateTo(typeof(DashboardPage)), "📊"));
        cmds.Register(new AppCommand("nav:scan", () => loc.T("Nav.Scan"), "Navigation", "Ctrl+2",
            () => NavigateTo(typeof(ScanPage)), "🔍"));
        cmds.Register(new AppCommand("nav:clean", () => loc.T("Nav.Clean"), "Navigation", "Ctrl+3",
            () => NavigateTo(typeof(CleanPage)), "🧹"));
        cmds.Register(new AppCommand("nav:performance", () => loc.T("Nav.Performance"), "Navigation", "Ctrl+4",
            () => NavigateTo(typeof(PerformancePage)), "📈"));
        cmds.Register(new AppCommand("nav:quarantine", () => loc.T("Nav.Quarantine"), "Navigation", "Ctrl+5",
            () => NavigateTo(typeof(QuarantinePage)), "🔒"));
        cmds.Register(new AppCommand("nav:rules", () => loc.T("Nav.Rules"), "Navigation", "Ctrl+6",
            () => NavigateTo(typeof(RulesPage)), "📋"));
        cmds.Register(new AppCommand("nav:statistics", () => loc.T("Nav.Statistics"), "Navigation", "Ctrl+7",
            () => NavigateTo(typeof(StatisticsPage)), "📊"));
        cmds.Register(new AppCommand("nav:settings", () => loc.T("Nav.Settings"), "Navigation", "Ctrl+8",
            () => NavigateTo(typeof(SettingsPage)), "⚙"));
        cmds.Register(new AppCommand("nav:about", () => loc.T("Nav.About"), "Navigation", "Ctrl+,",
            () => NavigateTo(typeof(AboutPage)), "ℹ"));
        cmds.Register(new AppCommand("action:scan", () => loc.T("Cmd.QuickScan"), "Actions", null,
            () => { _ = _sp.GetRequiredService<ScanViewModel>().StartScan(); }, "🔍"));
        cmds.Register(new AppCommand("action:toggle-theme", () => loc.T("Cmd.ToggleTheme"), "Actions", null,
            () => _sp.GetRequiredService<IThemeManager>().Toggle(), "🌓"));
        cmds.Register(new AppCommand("app.checkUpdate", () => loc.T("About_CheckUpdate"), "Actions", null,
            () => NavigateTo(typeof(AboutPage)), "⬆"));
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string pageType)
        {
            var type = pageType switch
            {
                "Dashboard" => typeof(DashboardPage),
                "Scan" => typeof(ScanPage),
                "Clean" => typeof(CleanPage),
                "Performance" => typeof(PerformancePage),
                "Quarantine" => typeof(QuarantinePage),
                "Rules" => typeof(RulesPage),
                "Statistics" => typeof(StatisticsPage),
                "Settings" => typeof(SettingsPage),
                "About" => typeof(AboutPage),
                "Help" => typeof(HelpPage),
                _ => typeof(DashboardPage)
            };
            NavigateTo(type);
        }
    }

    public void NavigateToPage(Type pageType)
    {
        if (_sp.GetService(pageType) is Page page)
            ContentFrame.Content = page;
    }

    private void NavigateTo(Type pageType)
    {
        NavigateToPage(pageType);
    }
}
