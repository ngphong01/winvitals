using System.Collections.Specialized;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class ScanPage : Page
{
    private readonly ScanViewModel _vm;

    public ScanPage(ScanViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.Items.CollectionChanged += (_, _) =>
        {
            ScanEmpty.Visibility = _vm.Items.Count == 0 && !_vm.IsScanning
                ? Visibility.Visible : Visibility.Collapsed;
        };
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.IsScanning))
                ScanEmpty.Visibility = _vm.Items.Count == 0 && !_vm.IsScanning
                    ? Visibility.Visible : Visibility.Collapsed;
        };
        Debug.WriteLine($"[ScanPage] Constructor, VM={vm.GetHashCode()}");
    }

    private async void BtnStart_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn) btn.IsEnabled = false;
        Debug.WriteLine("[ScanPage] START CLICKED!");
        await _vm.StartScan();
        if (sender is Button btn2) btn2.IsEnabled = true;
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ScanPage] CANCEL CLICKED!");
        _vm.CancelScan();
    }

    private void BtnGoClean_Click(object sender, RoutedEventArgs e)
    {
        Debug.WriteLine("[ScanPage] GO CLEAN CLICKED!");
        var mainWindow = Application.Current.MainWindow as MainWindow;
        mainWindow?.NavigateToPage(typeof(CleanPage));
    }
}
