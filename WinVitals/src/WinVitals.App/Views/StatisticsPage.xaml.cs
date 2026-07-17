using System.Windows;
using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class StatisticsPage : Page
{
    private readonly StatisticsViewModel _vm;

    public StatisticsPage(StatisticsViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(vm.TotalSessionsEver))
                UpdateEmptyState();
        };
        Loaded += (_, _) => UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var empty = _vm.TotalSessionsEver == 0;
        StatsEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
    }
}
