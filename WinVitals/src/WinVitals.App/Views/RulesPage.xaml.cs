using System.Windows.Controls;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class RulesPage : Page
{
    public RulesPage(RulesViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
    }
}
