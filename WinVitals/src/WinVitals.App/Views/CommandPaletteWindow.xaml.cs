using System.Windows;
using System.Windows.Input;
using WinVitals.App.ViewModels;

namespace WinVitals.App.Views;

public partial class CommandPaletteWindow : Window
{
    public CommandPaletteWindow(CommandPaletteViewModel vm)
    {
        InitializeComponent();
        DataContext = vm;
        Loaded += (_, _) => QueryBox.Focus();
    }

    private void Window_KeyDown(object sender, KeyEventArgs e)
    {
        var vm = (CommandPaletteViewModel)DataContext;
        switch (e.Key)
        {
            case Key.Escape: Close(); e.Handled = true; break;
            case Key.Enter: vm.ExecuteCommand.Execute(vm.SelectedItem); e.Handled = true; break;
            case Key.Down: vm.MoveNextCommand.Execute(null); e.Handled = true; break;
            case Key.Up: vm.MovePreviousCommand.Execute(null); e.Handled = true; break;
        }
    }
}
