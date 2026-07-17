using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class QuarantinePage : Page
{
    public QuarantinePage(QuarantineViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        vm.Entries.CollectionChanged += (_, _) => UpdateEmptyState();
        Loaded += (_, _) => UpdateEmptyState();
    }

    private void UpdateEmptyState()
    {
        var empty = ((QuarantineViewModel)DataContext).Entries.Count == 0;
        QuarantineEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        QuarantineGrid.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }
}
