using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class AboutPage : Page
{
    public AboutPage(AboutViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
