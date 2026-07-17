using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WinVitals.Core;
using WinVitals.Core.Entities;

namespace WinVitals.App.ViewModels;

public sealed partial class RuleEditorViewModel : ObservableObject
{
    private readonly Action<CleanRule> _onSave;
    private readonly Action _onCancel;

    public CleanRule Rule { get; }

    [ObservableProperty] private string id;
    [ObservableProperty] private string name;
    [ObservableProperty] private string description;
    [ObservableProperty] private int priority;
    [ObservableProperty] private ItemAction action;
    [ObservableProperty] private RiskLevel risk;
    [ObservableProperty] private ScanPreset preset;
    [ObservableProperty] private long minSizeBytes;
    [ObservableProperty] private int maxAgeDays;
    [ObservableProperty] private bool enabled;
    [ObservableProperty] private string pathPatternsText;
    [ObservableProperty] private string extensionsText;
    [ObservableProperty] private string errors = "";
    [ObservableProperty] private bool isNew;

    public string Title => IsNew ? "New rule" : $"Edit rule: {Rule.Id}";

    public ItemAction[] AllActions { get; } = Enum.GetValues<ItemAction>();
    public RiskLevel[] AllRisks { get; } = Enum.GetValues<RiskLevel>();
    public ScanPreset[] AllPresets { get; } = Enum.GetValues<ScanPreset>();

    public RuleEditorViewModel(CleanRule seed, bool isNew,
        Action<CleanRule> onSave, Action onCancel)
    {
        Rule = seed;
        IsNew = isNew;
        _onSave = onSave;
        _onCancel = onCancel;

        id = seed.Id;
        name = seed.Name;
        description = seed.Description;
        priority = seed.Priority;
        action = seed.Action;
        risk = seed.Risk;
        preset = seed.Preset;
        minSizeBytes = seed.MinSizeBytes;
        maxAgeDays = seed.MaxAgeDays;
        enabled = seed.Enabled;
        pathPatternsText = string.Join(Environment.NewLine, seed.PathPatterns);
        extensionsText = string.Join(", ", seed.Extensions);
    }

    [RelayCommand]
    private void Save()
    {
        Rule.Id = (Id ?? "").Trim().ToLowerInvariant();
        Rule.Name = (Name ?? "").Trim();
        Rule.Description = Description ?? "";
        Rule.Priority = Priority;
        Rule.Action = Action;
        Rule.Risk = Risk;
        Rule.Preset = Preset;
        Rule.MinSizeBytes = Math.Max(0, MinSizeBytes);
        Rule.MaxAgeDays = Math.Max(0, MaxAgeDays);
        Rule.Enabled = Enabled;
        Rule.PathPatterns = (PathPatternsText ?? "")
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        Rule.Extensions = (ExtensionsText ?? "")
            .Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(e => e.StartsWith('.') ? e : "." + e)
            .Select(e => e.ToLowerInvariant())
            .Distinct()
            .ToList();
        Rule.IsBuiltIn = false;

        var errs = WinVitals.Core.Rules.RuleValidator.Validate(Rule);
        if (errs.Count > 0)
        {
            Errors = string.Join("\n• ", errs.Prepend(""));
            return;
        }
        Errors = "";
        _onSave(Rule);
    }

    [RelayCommand]
    private void Cancel() => _onCancel();

    public double MinSizeMB
    {
        get => MinSizeBytes / 1024.0 / 1024.0;
        set { MinSizeBytes = (long)(value * 1024 * 1024); OnPropertyChanged(); }
    }
}
