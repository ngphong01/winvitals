namespace WinVitals.App.Services;

public sealed record AppCommand(
    string Id,
    Func<string> TitleFactory,
    string Category,
    string? Shortcut,
    Action Execute,
    string IconGlyph = "▶");

public interface IAppCommands
{
    IReadOnlyList<AppCommand> All { get; }
    void Register(AppCommand cmd);
    void Execute(string id);
}

public sealed class AppCommands : IAppCommands
{
    private readonly List<AppCommand> _list = new();
    public IReadOnlyList<AppCommand> All => _list;

    public void Register(AppCommand cmd)
    {
        var existing = _list.FindIndex(c => c.Id == cmd.Id);
        if (existing >= 0) _list[existing] = cmd; else _list.Add(cmd);
    }

    public void Execute(string id) =>
        _list.FirstOrDefault(c => c.Id == id)?.Execute();
}
