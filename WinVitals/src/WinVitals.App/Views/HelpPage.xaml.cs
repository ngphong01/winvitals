using System.Windows.Controls;
using System.Windows.Input;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class HelpPage : Page
{
    private readonly HelpViewModel _vm;

    public HelpPage(HelpViewModel vm)
    {
        InitializeComponent();
        _vm = vm;
        DataContext = vm;
        vm.SelectedItem = vm.TableOfContents.FirstOrDefault();
    }

    private void SearchBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
            _vm.FilterTocCommand.Execute(_vm.SearchQuery);
    }
}
