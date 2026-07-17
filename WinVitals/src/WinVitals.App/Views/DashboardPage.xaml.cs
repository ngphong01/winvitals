using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class DashboardPage : Page
{
    public DashboardPage(DashboardViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
