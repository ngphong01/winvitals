using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views.Onboarding;

public partial class OnboardingWindow : Window
{
    private readonly Action<Action?> _onRunScan;
    private readonly OnboardingViewModel _vm;

    public OnboardingWindow(OnboardingViewModel vm, Action<Action?> onRunScan)
    {
        InitializeComponent();
        _vm = vm;
        _onRunScan = onRunScan;
        DataContext = vm;
        vm.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(vm.CurrentStep)) UpdateStep(); };
        UpdateStep();
    }

    private void UpdateStep()
    {
        Step0.Visibility = _vm.CurrentStep == 0 ? Visibility.Visible : Visibility.Collapsed;
        Step1.Visibility = _vm.CurrentStep == 1 ? Visibility.Visible : Visibility.Collapsed;
        Step2.Visibility = _vm.CurrentStep == 2 ? Visibility.Visible : Visibility.Collapsed;
        Step3.Visibility = _vm.CurrentStep == 3 ? Visibility.Visible : Visibility.Collapsed;
        Step4.Visibility = _vm.CurrentStep == 4 ? Visibility.Visible : Visibility.Collapsed;
        Dot0.Fill = _vm.CurrentStep >= 0 ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)) : new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
        Dot1.Fill = _vm.CurrentStep >= 1 ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)) : new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
        Dot2.Fill = _vm.CurrentStep >= 2 ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)) : new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
        Dot3.Fill = _vm.CurrentStep >= 3 ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)) : new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
        Dot4.Fill = _vm.CurrentStep >= 4 ? new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)) : new SolidColorBrush(Color.FromRgb(0x45, 0x47, 0x5A));
    }

    private void OpenPrivacyPolicy_Click(object sender, RoutedEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo("https://winvitals.example.com/privacy") { UseShellExecute = true }); }
        catch { }
    }

    private void RunScan_Click(object sender, RoutedEventArgs e) => _onRunScan(() => Close());

    private void SkipScan_Click(object sender, RoutedEventArgs e) => _vm.DoSkipScanCommand.Execute(null);
}
