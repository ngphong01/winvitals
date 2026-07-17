using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Documents;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.App.Services;

namespace WinVitals.App.ViewModels;

public sealed partial class HelpViewModel : ObservableObject
{
    private readonly ILocalizationService _loc;

    public ObservableCollection<DocItem> TableOfContents { get; } = new();
    [ObservableProperty] private FlowDocument? currentDocument;
    [ObservableProperty] private DocItem? selectedItem;
    [ObservableProperty] private string searchQuery = "";

    public HelpViewModel(ILocalizationService loc)
    {
        _loc = loc;
        var lang = _loc.CurrentCulture.StartsWith("vi") ? "vi" : "en";
        TableOfContents.Add(new DocItem("getting-started", lang == "vi" ? "Bắt đầu" : "Getting Started", lang));
        TableOfContents.Add(new DocItem("scan-presets", lang == "vi" ? "Preset quét" : "Scan Presets", lang));
        TableOfContents.Add(new DocItem("quarantine", lang == "vi" ? "Khu cách ly" : "Quarantine", lang));
        TableOfContents.Add(new DocItem("rules-editor", lang == "vi" ? "Trình sửa luật" : "Rules Editor", lang));
        TableOfContents.Add(new DocItem("faq", lang == "vi" ? "Hỏi đáp" : "FAQ", lang));
    }

    partial void OnSelectedItemChanged(DocItem? value)
    {
        if (value is null) return;
        var md = LoadEmbeddedMarkdown(value.Key, value.Language);
        if (!string.IsNullOrEmpty(md))
        {
            var doc = new FlowDocument();
            var para = new System.Windows.Documents.Paragraph(new Run(md));
            doc.Blocks.Add(para);
            CurrentDocument = doc;
        }
    }

    [RelayCommand]
    private void FilterToc(string? query)
    {
        // Simple filter: just navigate to first match
        if (string.IsNullOrWhiteSpace(query)) return;
        var match = TableOfContents.FirstOrDefault(d =>
            d.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
        if (match is not null) SelectedItem = match;
    }

    private static string? LoadEmbeddedMarkdown(string key, string lang)
    {
        var asm = typeof(HelpViewModel).Assembly;
        var baseName = asm.GetName().Name ?? "WinVitals";
        // Try language-specific first, then fallback to English
        foreach (var culture in new[] { lang, "en" })
        {
            // Resource naming includes .App. prefix due to project structure
            var resourceName = $"{baseName}.App.Docs.{culture}.{key}.md";
            using var stream = asm.GetManifestResourceStream(resourceName);
            if (stream is not null)
            {
                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            }
            // Also try without .App prefix
            resourceName = $"{baseName}.Docs.{culture}.{key}.md";
            using var stream2 = asm.GetManifestResourceStream(resourceName);
            if (stream2 is not null)
            {
                using var reader = new StreamReader(stream2);
                return reader.ReadToEnd();
            }
        }
        return null;
    }
}

public sealed class DocItem
{
    public string Key { get; }
    public string Title { get; }
    public string Language { get; }
    public DocItem(string key, string title, string lang)
    { Key = key; Title = title; Language = lang; }
}
