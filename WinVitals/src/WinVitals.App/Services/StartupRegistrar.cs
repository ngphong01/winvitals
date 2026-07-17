using Microsoft.Win32;

namespace WinVitals.App.Services;

public interface IStartupRegistrar
{
    bool IsEnabled();
    void Enable(bool startMinimized);
    void Disable();
}

public sealed class StartupRegistrar : IStartupRegistrar
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "WinVitals";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is not null;
    }

    public void Enable(bool startMinimized)
    {
        var exe = Environment.ProcessPath ?? "";
        if (string.IsNullOrEmpty(exe)) return;
        var value = startMinimized ? $"\"{exe}\" --minimized" : $"\"{exe}\"";
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        key.SetValue(ValueName, value);
    }

    public void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
