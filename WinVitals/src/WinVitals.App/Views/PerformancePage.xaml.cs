using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class PerformancePage : Page
{
    public PerformancePage(PerformanceViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
