using System.Diagnostics;
using System.Windows;

namespace WinVitals.App.Views;

public partial class CrashReportWindow : Window
{
    private readonly Exception _ex;
    private readonly string _reportPath;

    public CrashReportWindow(Exception ex, string reportPath)
    {
        InitializeComponent();
        _ex = ex;
        _reportPath = reportPath;
        DetailsText.Text = $"{ex.GetType().Name}: {ex.Message}\n\n{ex.StackTrace}";
    }

    private void OpenFolder_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{_reportPath}\"") { UseShellExecute = true }); }
        catch { }
    }

    private void Ignore_Click(object sender, RoutedEventArgs e) => Close();

    private void Send_Click(object sender, RoutedEventArgs e) => Close();
}
