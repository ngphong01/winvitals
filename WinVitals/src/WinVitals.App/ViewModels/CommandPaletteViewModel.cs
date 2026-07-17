using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace WinVitals.App.ViewModels;

public sealed partial class CommandPaletteViewModel : ObservableObject
{
    private readonly Services.IAppCommands _commands;
    private readonly Action _closeDialog;

    public ObservableCollection<PaletteItem> Items { get; } = new();
    public ICollectionView ItemsView { get; }

    [ObservableProperty] private string query = "";
    [ObservableProperty] private PaletteItem? selectedItem;

    public CommandPaletteViewModel(Services.IAppCommands commands, Action closeDialog)
    {
        _commands = commands;
        _closeDialog = closeDialog;
        foreach (var c in commands.All)
            Items.Add(new PaletteItem(c));
        ItemsView = CollectionViewSource.GetDefaultView(Items);
        ItemsView.Filter = FilterItem;
        ItemsView.SortDescriptions.Add(new SortDescription(nameof(PaletteItem.Category), ListSortDirection.Ascending));
        ItemsView.GroupDescriptions.Add(new PropertyGroupDescription(nameof(PaletteItem.Category)));
        SelectedItem = Items.FirstOrDefault();
    }

    partial void OnQueryChanged(string value)
    {
        ItemsView.Refresh();
        SelectedItem = Items.FirstOrDefault(i => ItemsView.CanFilter && FilterItem(i));
    }

    private bool FilterItem(object o)
    {
        if (o is not PaletteItem p) return false;
        if (string.IsNullOrWhiteSpace(Query)) return true;
        return p.Title.Contains(Query, StringComparison.OrdinalIgnoreCase)
            || p.Category.Contains(Query, StringComparison.OrdinalIgnoreCase)
            || (p.Shortcut?.Contains(Query, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    [RelayCommand]
    private void Execute(PaletteItem? item)
    {
        var target = item ?? SelectedItem;
        if (target is null) return;
        _closeDialog();
        _commands.Execute(target.Id);
    }

    [RelayCommand]
    private void MoveNext()
    {
        var visible = ItemsView.Cast<PaletteItem>().ToList();
        if (visible.Count == 0) return;
        var idx = SelectedItem is null ? -1 : visible.IndexOf(SelectedItem);
        SelectedItem = visible[Math.Min(idx + 1, visible.Count - 1)];
    }

    [RelayCommand]
    private void MovePrevious()
    {
        var visible = ItemsView.Cast<PaletteItem>().ToList();
        if (visible.Count == 0) return;
        var idx = SelectedItem is null ? 0 : visible.IndexOf(SelectedItem);
        SelectedItem = visible[Math.Max(idx - 1, 0)];
    }
}

public sealed class PaletteItem
{
    public string Id { get; }
    public string Title { get; }
    public string Category { get; }
    public string? Shortcut { get; }
    public string IconGlyph { get; }

    public PaletteItem(Services.AppCommand c)
    {
        Id = c.Id; Title = c.TitleFactory(); Category = c.Category;
        Shortcut = c.Shortcut; IconGlyph = c.IconGlyph;
    }
}
