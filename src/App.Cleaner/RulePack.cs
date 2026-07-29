using System.Text.Json;
using System.Text.Json.Serialization;
using App.Core;

namespace App.Cleaner;

/// <summary>
/// Community Rule Pack — a shareable collection of cleaning rules.
/// Packs can be imported/exported as JSON and shared via GitHub.
/// Requirements: v3.0 Community Rule Packs
/// </summary>
public class RulePack
{
    [JsonPropertyName("id")] public string Id { get; set; } = Guid.NewGuid().ToString("N")[..12];
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("version")] public string Version { get; set; } = "1.0.0";
    [JsonPropertyName("author")] public string Author { get; set; } = "";
    [JsonPropertyName("description")] public string Description { get; set; } = "";
    [JsonPropertyName("app")] public string TargetApp { get; set; } = "";
    [JsonPropertyName("estimated")] public string EstimatedCleanable { get; set; } = "";
    [JsonPropertyName("tags")] public List<string> Tags { get; set; } = [];
    [JsonPropertyName("downloads")] public int Downloads { get; set; }
    [JsonPropertyName("source")] public string? SourceUrl { get; set; }
    [JsonPropertyName("rules")] public List<RulePackRule> Rules { get; set; } = [];
}

/// <summary>
/// A single rule within a RulePack.
/// </summary>
public class RulePackRule
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("patterns")] public List<string> PathPatterns { get; set; } = [];
    [JsonPropertyName("extensions")] public List<string> Extensions { get; set; } = [];
    [JsonPropertyName("action")] public string Action { get; set; } = "WarnDelete";
    [JsonPropertyName("risk")] public string Risk { get; set; } = "Low";
    [JsonPropertyName("note")] public string Note { get; set; } = "";

    public ItemAction ParseAction() => Action.ToLowerInvariant() switch
    {
        "safedelete" or "safe" => ItemAction.SafeDelete,
        "block" => ItemAction.Block,
        "quarantine" => ItemAction.Quarantine,
        "skip" => ItemAction.Skip,
        _ => ItemAction.WarnDelete
    };
}

/// <summary>
/// Built-in community rule packs — these are embedded defaults.
/// In production, these would be fetched from a GitHub repo.
/// </summary>
public static class BuiltInPacks
{
    public static List<RulePack> All =>
    [
        AdobePack,
        NvidiaPack,
        SteamPack,
        VsCodePack,
        BrowserCachePack,
        DockerPack
    ];

    public static RulePack AdobePack => new()
    {
        Name = "Adobe Creative Cloud Cleaner",
        Version = "1.0.0", Author = "Đào Văn Phong",
        Description = "Dọn cache Adobe: Media Cache, After Effects temp, Illustrator scratch, Bridge cache.",
        TargetApp = "Adobe Creative Cloud",
        EstimatedCleanable = "15-50 GB",
        Tags = ["design", "adobe", "creative"],
        SourceUrl = "https://github.com/ngphong01/whm-community-packs",
        Rules =
        [
            new() {
                Name = "Adobe Media Cache", PathPatterns = ["**\\Adobe\\Media Cache\\**"],
                Action = "WarnDelete", Risk = "Low",
                Note = "Preview sẽ chậm hơn lần đầu sau khi xóa."
            },
            new() {
                Name = "After Effects Temp", PathPatterns = ["**\\Adobe\\After Effects\\*\\Temp\\**"],
                Action = "WarnDelete", Risk = "Low"
            },
            new() {
                Name = "Illustrator Scratch", PathPatterns = ["**\\Adobe\\Illustrator\\*\\Scratch\\**"],
                Action = "SafeDelete", Risk = "Safe"
            },
            new() {
                Name = "Bridge Cache", PathPatterns = ["**\\Adobe\\Bridge\\*\\Cache\\**"],
                Action = "SafeDelete", Risk = "Safe"
            }
        ]
    };

    public static RulePack NvidiaPack => new()
    {
        Name = "NVIDIA & GPU Cleaner",
        Version = "1.0.0", Author = "Đào Văn Phong",
        Description = "Shader cache NVIDIA, DirectX Shader Cache, GPU driver installer cũ.",
        TargetApp = "NVIDIA GPU",
        EstimatedCleanable = "2-15 GB",
        Tags = ["gaming", "nvidia", "gpu"],
        Rules =
        [
            new() {
                Name = "NVIDIA Shader Cache",
                PathPatterns = ["**\\NVIDIA Corporation\\*\\ShaderCache\\**"],
                Action = "SafeDelete", Risk = "Safe",
                Note = "Shader cache tự tạo lại khi chạy game."
            },
            new() {
                Name = "DirectX Shader Cache",
                PathPatterns = ["**\\DirectX\\ShaderCache\\**", "**\\D3DSCache\\**"],
                Action = "SafeDelete", Risk = "Safe"
            },
            new() {
                Name = "NVIDIA Installer Logs",
                PathPatterns = ["**\\NVIDIA Corporation\\*\\Installer*\\**"],
                Action = "WarnDelete", Risk = "Low",
                Note = "Installer cũ, an toàn nếu không rollback driver."
            }
        ]
    };

    public static RulePack SteamPack => new()
    {
        Name = "Steam & Game Cache",
        Version = "1.0.0", Author = "Đào Văn Phong",
        Description = "Steam download cache, workshop temp, depot cache.",
        TargetApp = "Steam",
        EstimatedCleanable = "5-30 GB",
        Tags = ["gaming", "steam", "valve"],
        Rules =
        [
            new() {
                Name = "Steam Download Cache",
                PathPatterns = ["**\\Steam\\depotcache\\**", "**\\Steam\\appcache\\**"],
                Action = "WarnDelete", Risk = "Low",
                Note = "Sau khi xóa, Steam sẽ tải lại cache khi update game."
            },
            new() {
                Name = "Steam Workshop Temp",
                PathPatterns = ["**\\Steam\\steamapps\\workshop\\temp\\**"],
                Action = "SafeDelete", Risk = "Safe"
            }
        ]
    };

    public static RulePack VsCodePack => new()
    {
        Name = "VS Code Cleaner",
        Version = "1.0.0", Author = "Đào Văn Phong",
        Description = "VS Code cache, extension caches, CachedData, crash reports.",
        TargetApp = "Visual Studio Code",
        EstimatedCleanable = "1-5 GB",
        Tags = ["dev", "vscode", "editor"],
        Rules =
        [
            new() {
                Name = "VS Code CachedData",
                PathPatterns = ["**\\Code\\CachedData\\**"],
                Action = "SafeDelete", Risk = "Safe",
                Note = "Sẽ tạo lại khi mở VS Code."
            },
            new() {
                Name = "VS Code Crash Reports",
                PathPatterns = ["**\\Code\\Crashpad\\**"],
                Action = "SafeDelete", Risk = "Safe"
            },
            new() {
                Name = "VS Code Extension Cache",
                PathPatterns = ["**\\Code\\User\\workspaceStorage\\**"],
                Action = "WarnDelete", Risk = "Low"
            }
        ]
    };

    public static RulePack BrowserCachePack => new()
    {
        Name = "Browser Cache Cleaner",
        Version = "1.0.0", Author = "Đào Văn Phong",
        Description = "Chrome, Firefox, Edge caches — Code Cache, GPU Cache, Service Worker.",
        TargetApp = "Browser",
        EstimatedCleanable = "2-10 GB",
        Tags = ["browser", "cache", "chrome", "firefox"],
        Rules =
        [
            new() {
                Name = "Chrome Code Cache",
                PathPatterns = ["**\\Google\\Chrome\\*\\Code Cache\\**"],
                Action = "SafeDelete", Risk = "Safe",
                Note = "Cache JS/WASM, sẽ tạo lại."
            },
            new() {
                Name = "Chrome Service Worker",
                PathPatterns = ["**\\Google\\Chrome\\*\\Service Worker\\**"],
                Action = "SafeDelete", Risk = "Safe"
            },
            new() {
                Name = "Firefox Cache",
                PathPatterns = ["**\\Mozilla\\Firefox\\*\\cache2\\**"],
                Action = "SafeDelete", Risk = "Safe"
            }
        ]
    };

    public static RulePack DockerPack => new()
    {
        Name = "Docker Deep Clean",
        Version = "1.0.0", Author = "Đào Văn Phong",
        Description = "Docker build cache, dangling images, unused volumes, container logs.",
        TargetApp = "Docker Desktop",
        EstimatedCleanable = "5-50 GB",
        Tags = ["docker", "devops", "container"],
        Rules =
        [
            new() {
                Name = "Docker Build Cache",
                PathPatterns = ["**\\Docker\\*\\builder\\**"],
                Action = "WarnDelete", Risk = "Low",
                Note = "Sau khi xóa, lần build sau sẽ chậm hơn. Dùng docker builder prune."
            },
            new() {
                Name = "Docker Container Logs",
                PathPatterns = ["**\\Docker\\*\\containers\\**\\*.log"],
                Extensions = [".log"], Action = "SafeDelete", Risk = "Safe"
            },
            new() {
                Name = "Docker Desktop VM Disk",
                PathPatterns = ["**\\Docker\\Docker Desktop\\Docker.raw"],
                Action = "Block", Risk = "Critical",
                Note = "Disk ảnh Docker — KHÔNG xóa. Dùng compact trong Troubleshoot."
            }
        ]
    };
}

/// <summary>
/// Manages RulePack lifecycle: import, export, apply, install.
/// </summary>
public class RulePackManager
{
    private readonly IRuleEngine _ruleEngine;
    private static readonly string PacksDir = Path.Combine(AppContext.BaseDirectory, "rule-packs");
    private static readonly string CommunityIndexUrl = "https://raw.githubusercontent.com/ngphong01/whm-community-packs/main/index.json";

    public RulePackManager(IRuleEngine ruleEngine)
    {
        _ruleEngine = ruleEngine;
        Directory.CreateDirectory(PacksDir);
    }

    /// <summary>
    /// Import a rule pack from a JSON file.
    /// </summary>
    public bool ImportFromFile(string filePath)
    {
        try
        {
            var json = File.ReadAllText(filePath);
            var pack = JsonSerializer.Deserialize<RulePack>(json);
            if (pack == null || pack.Rules.Count == 0) return false;

            return ApplyPack(pack);
        }
        catch { return false; }
    }

    /// <summary>
    /// Import a rule pack from JSON string.
    /// </summary>
    public bool ImportFromJson(string json)
    {
        try
        {
            var pack = JsonSerializer.Deserialize<RulePack>(json);
            if (pack == null || pack.Rules.Count == 0) return false;
            return ApplyPack(pack);
        }
        catch { return false; }
    }

    /// <summary>
    /// Export all current rules as a pack.
    /// </summary>
    public string ExportToJson(string packName, string author)
    {
        var rules = _ruleEngine.GetRules();
        var pack = new RulePack
        {
            Name = packName,
            Author = author,
            Description = $"Auto-exported from WHM on {DateTime.Now:yyyy-MM-dd}",
            Rules = rules.Select(r => new RulePackRule
            {
                Name = r.Name,
                PathPatterns = r.PathPatterns,
                Extensions = r.Extensions,
                Action = r.Action.ToString(),
                Risk = r.Risk.ToString()
            }).ToList()
        };

        return JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
    }

    /// <summary>
    /// Fetch community rule pack index from GitHub.
    /// Returns built-in packs if GitHub is unavailable.
    /// </summary>
    public async Task<List<RulePack>> FetchCommunityPacksAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
            var json = await http.GetStringAsync(CommunityIndexUrl);
            var packs = JsonSerializer.Deserialize<List<RulePack>>(json);
            if (packs != null && packs.Count > 0) return packs;
        }
        catch { /* offline, fall back to built-in */ }

        return BuiltInPacks.All;
    }

    /// <summary>
    /// Install a community pack by its source URL.
    /// </summary>
    public bool InstallPack(RulePack pack)
    {
        var filePath = Path.Combine(PacksDir, $"{pack.Id}.json");
        var json = JsonSerializer.Serialize(pack, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json);
        return ApplyPack(pack);
    }

    /// <summary>
    /// List installed packs.
    /// </summary>
    public List<string> ListInstalledPacks()
    {
        try
        {
            return Directory.GetFiles(PacksDir, "*.json")
                .Select(Path.GetFileNameWithoutExtension)
                .Where(f => f != null)
                .Cast<string>()
                .ToList();
        }
        catch { return []; }
    }

    /// <summary>
    /// Uninstall a pack by name.
    /// </summary>
    public bool UninstallPack(string packName)
    {
        try
        {
            var file = Directory.GetFiles(PacksDir, $"{packName}.json").FirstOrDefault();
            if (file == null) return false;
            File.Delete(file);
            return true;
        }
        catch { return false; }
    }

    private bool ApplyPack(RulePack pack)
    {
        try
        {
            foreach (var pr in pack.Rules)
            {
                var rule = new Rule
                {
                    Id = $"pack_{pack.Id}_{Guid.NewGuid():N}",
                    Name = pr.Name,
                    PathPatterns = pr.PathPatterns,
                    Extensions = pr.Extensions,
                    Action = pr.ParseAction(),
                    Risk = Enum.TryParse<RiskLevel>(pr.Risk, true, out var risk) ? risk : RiskLevel.Low,
                    Priority = 75,
                    Enabled = true
                };
                _ruleEngine.AddRule(rule);
            }

            // Save installed pack info
            var metaPath = Path.Combine(PacksDir, $"{pack.Id}.json");
            var meta = JsonSerializer.Serialize(new
            {
                pack.Name, pack.Version, pack.Author, pack.Description,
                AppliedAt = DateTime.Now,
                RuleCount = pack.Rules.Count
            }, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(metaPath, meta);

            return true;
        }
        catch { return false; }
    }
}
