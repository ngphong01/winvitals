using System.Text;

namespace App.Core;

public static class ActivityLogger
{
    private static readonly object _lock = new();
    private static readonly string LogPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "WindowsHealthManager", "activity.log");

    private const int MaxEntries = 120;
    private static readonly List<LogEntry> _entries = new(MaxEntries + 10);

    public static event Action<LogEntry>? OnEntryAdded;

    static ActivityLogger()
    {
        var dir = Path.GetDirectoryName(LogPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
    }

    public static void Success(string action, string detail = "") => Write("OK", action, detail);
    public static void Fail(string action, string error) => Write("FAIL", action, error);
    public static void Info(string action, string detail = "") => Write("INFO", action, detail);

    public static IReadOnlyList<LogEntry> RecentEntries { get { lock (_lock) return _entries.ToList(); } }

    private static void Write(string status, string action, string detail)
    {
        var entry = new LogEntry { Time = DateTime.Now, Status = status, Action = action, Detail = detail };
        var line = $"{entry.Time:yyyy-MM-dd HH:mm:ss} [{entry.Status}] {entry.Action}";
        if (!string.IsNullOrWhiteSpace(detail)) line += $" — {detail}";

        lock (_lock)
        {
            _entries.Add(entry);
            while (_entries.Count > MaxEntries) _entries.RemoveAt(0);
            try { File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8); } catch { }
        }
        OnEntryAdded?.Invoke(entry);
    }

    public static void OpenInNotepad()
    {
        try
        {
            var dir = Path.GetDirectoryName(LogPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            if (!File.Exists(LogPath))
                File.WriteAllText(LogPath, $"# WinHealth Activity Log — {DateTime.Now:yyyy-MM-dd HH:mm:ss}" + Environment.NewLine, Encoding.UTF8);
            System.Diagnostics.Process.Start("notepad.exe", $"\"{LogPath}\"");
        }
        catch { }
    }

    public static string GetLogPath() => LogPath;

    public static string ReadTail(int lines = 200)
    {
        try
        {
            if (!File.Exists(LogPath)) return "(Chưa có hoạt động nào.)";
            var all = File.ReadAllLines(LogPath, Encoding.UTF8);
            return string.Join(Environment.NewLine, all.Skip(Math.Max(0, all.Length - lines)));
        }
        catch { return "(Không đọc được log.)"; }
    }
}

public class LogEntry
{
    public DateTime Time { get; set; }
    public string Status { get; set; } = "";
    public string Action { get; set; } = "";
    public string Detail { get; set; } = "";
    public string Icon => Status switch { "OK" => "✅", "FAIL" => "❌", _ => "ℹ️" };
    public string TimeStr => Time.ToString("HH:mm:ss");
}
