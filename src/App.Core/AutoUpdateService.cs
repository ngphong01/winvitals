using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace App.Core;

/// <summary>
/// Auto-update service — checks GitHub releases and downloads updates.
/// Requirements: v3.2 Auto-Update, 17.4, 17.6, 17.8
/// </summary>
public class AutoUpdateService
{
    private const string Owner = "ngphong01";
    private const string Repo = "winvitals";
    private const string ApiUrl = $"https://api.github.com/repos/{Owner}/{Repo}/releases/latest";
    private static readonly Version CurrentVersion = typeof(AutoUpdateService).Assembly.GetName().Version ?? new Version(2, 0, 0);

    /// <summary>
    /// Check GitHub for a newer release.
    /// </summary>
    public async Task<UpdateCheckResult> CheckAsync()
    {
        try
        {
            using var http = new HttpClient();
            http.DefaultRequestHeaders.UserAgent.ParseAdd($"WHM/{CurrentVersion}");
            http.Timeout = TimeSpan.FromSeconds(10);

            var json = await http.GetStringAsync(ApiUrl);
            var release = JsonSerializer.Deserialize<GitHubRelease>(json);

            if (release?.TagName == null)
                return new UpdateCheckResult { HasUpdate = false, Error = "Cannot parse release info" };

            var remoteVer = ParseVersion(release.TagName);
            if (remoteVer == null)
                return new UpdateCheckResult { HasUpdate = false, Error = "Cannot parse version" };

            var hasUpdate = remoteVer > CurrentVersion;

            return new UpdateCheckResult
            {
                HasUpdate = hasUpdate,
                CurrentVersion = CurrentVersion.ToString(),
                RemoteVersion = remoteVer.ToString(),
                ReleaseUrl = release.HtmlUrl,
                ReleaseNotes = release.Body,
                PublishedAt = release.PublishedAt,
                DownloadUrl = release.Assets?.FirstOrDefault(a => a.Name?.EndsWith(".exe") == true)?.BrowserDownloadUrl
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult { HasUpdate = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// Download the latest release to a temp path.
    /// </summary>
    public async Task<string?> DownloadAsync(string downloadUrl)
    {
        try
        {
            using var http = new HttpClient();
            http.Timeout = TimeSpan.FromMinutes(5);

            var destPath = Path.Combine(Path.GetTempPath(), $"WHM_Update_{Guid.NewGuid():N}.exe");
            var bytes = await http.GetByteArrayAsync(downloadUrl);
            await File.WriteAllBytesAsync(destPath, bytes);

            return destPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Launch the downloaded installer and exit the current app.
    /// </summary>
    public static void ApplyUpdate(string installerPath)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = installerPath,
                UseShellExecute = true,
                Arguments = "/SILENT /SUPPRESSMSGBOXES /CLOSEAPPLICATIONS"
            };
            Process.Start(psi);
            Environment.Exit(0);
        }
        catch { }
    }

    private static Version? ParseVersion(string tag)
    {
        var v = tag.TrimStart('v', 'V');
        // Handle "v2.0.0" or "2.0.0" formats
        var parts = v.Split('.');
        if (parts.Length >= 3 &&
            int.TryParse(parts[0], out var major) &&
            int.TryParse(parts[1], out var minor) &&
            int.TryParse(parts[2], out var patch))
        {
            return new Version(major, minor, patch);
        }
        return null;
    }
}

public class UpdateCheckResult
{
    public bool HasUpdate { get; set; }
    public string? CurrentVersion { get; set; }
    public string? RemoteVersion { get; set; }
    public string? ReleaseUrl { get; set; }
    public string? ReleaseNotes { get; set; }
    public string? PublishedAt { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Error { get; set; }
}

internal class GitHubRelease
{
    [JsonPropertyName("tag_name")] public string? TagName { get; set; }
    [JsonPropertyName("html_url")] public string? HtmlUrl { get; set; }
    [JsonPropertyName("body")] public string? Body { get; set; }
    [JsonPropertyName("published_at")] public string? PublishedAt { get; set; }
    [JsonPropertyName("assets")] public List<GitHubAsset>? Assets { get; set; }
}

internal class GitHubAsset
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("browser_download_url")] public string? BrowserDownloadUrl { get; set; }
    [JsonPropertyName("size")] public long Size { get; set; }
}
