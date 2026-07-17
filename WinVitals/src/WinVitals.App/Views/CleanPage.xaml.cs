using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class CleanPage : Page
{
    public CleanPage(CleanViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
