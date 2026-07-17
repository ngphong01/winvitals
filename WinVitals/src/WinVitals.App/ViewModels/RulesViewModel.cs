using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using WinVitals.App.Services;
using WinVitals.Core;
using WinVitals.Core.Entities;
using WinVitals.Core.Rules;

namespace WinVitals.App.ViewModels;

public sealed partial class RulesViewModel : ObservableObject
{
    private readonly RuleRepository _repo;
    private readonly IAppNotifier _notify;

    public ObservableCollection<RuleRow> Rules { get; } = new();
    public ICollectionView RulesView { get; }

    [ObservableProperty] private string filter = "";
    [ObservableProperty] private bool showBuiltIn = true;
    [ObservableProperty] private RuleRow? selectedRule;
    [ObservableProperty] private RuleEditorViewModel? editor;

    [ObservableProperty] private string testPath = @"C:\Users\Anh\AppData\Local\Temp\example.log";
    [ObservableProperty] private string testSizeText = "2048";
    [ObservableProperty] private string testAgeText = "45";
    [ObservableProperty] private string testResult = "";

    public RulesViewModel(RuleRepository repo, IAppNotifier notify)
    {
        _repo = repo;
        _notify = notify;
        BindingOperations.EnableCollectionSynchronization(Rules, new object());
        RulesView = CollectionViewSource.GetDefaultView(Rules);
        RulesView.Filter = FilterRow;
        RulesView.SortDescriptions.Add(new SortDescription(nameof(RuleRow.Priority), ListSortDirection.Descending));
        Refresh();
    }

    partial void OnFilterChanged(string v) => RulesView.Refresh();
    partial void OnShowBuiltInChanged(bool v) => RulesView.Refresh();

    private bool FilterRow(object o)
    {
        if (o is not RuleRow r) return false;
        if (!ShowBuiltIn && r.IsBuiltIn) return false;
        if (string.IsNullOrWhiteSpace(Filter)) return true;
        return r.Id.Contains(Filter, StringComparison.OrdinalIgnoreCase)
            || r.Name.Contains(Filter, StringComparison.OrdinalIgnoreCase);
    }

    [RelayCommand]
    private void Refresh()
    {
        Rules.Clear();
        foreach (var r in _repo.LoadAll().OrderByDescending(x => x.Priority))
            Rules.Add(new RuleRow(r, ToggleEnabled));
    }

    private void ToggleEnabled(RuleRow row, bool newValue)
    {
        if (row.IsBuiltIn)
        {
            _notify.Warning("Built-in rule", "Cannot disable a system rule.");
            row.SetEnabledSilently(!newValue);
            return;
        }
        row.Source.Enabled = newValue;
        _repo.UpsertCustom(row.Source);
        _notify.Info(newValue ? "Enabled" : "Disabled", row.Name);
    }

    [RelayCommand]
    private void New()
    {
        var seed = new CleanRule
        {
            Id = $"custom_{DateTime.UtcNow:HHmmss}",
            Name = "New rule", Priority = 50,
            Action = ItemAction.WarnDelete, Risk = RiskLevel.Medium,
            Preset = ScanPreset.Quick, Enabled = true
        };
        Editor = new RuleEditorViewModel(seed, isNew: true, Save, Cancel);
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Edit()
    {
        if (SelectedRule is null) return;
        if (SelectedRule.IsBuiltIn)
        {
            _notify.Warning("Read-only", "Built-in rules cannot be edited. You can duplicate it instead.");
            return;
        }
        var copy = CloneRule(SelectedRule.Source);
        Editor = new RuleEditorViewModel(copy, isNew: false, Save, Cancel);
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Duplicate()
    {
        if (SelectedRule is null) return;
        var copy = CloneRule(SelectedRule.Source);
        copy.Id = $"{copy.Id}_copy";
        copy.Name = $"{copy.Name} (copy)";
        copy.IsBuiltIn = false;
        Editor = new RuleEditorViewModel(copy, isNew: true, Save, Cancel);
    }

    [RelayCommand(CanExecute = nameof(CanEdit))]
    private void Delete()
    {
        if (SelectedRule is null) return;
        if (SelectedRule.IsBuiltIn)
        {
            _notify.Warning("Read-only", "Cannot delete built-in rule.");
            return;
        }
        if (_repo.DeleteCustom(SelectedRule.Id))
        {
            _notify.Success("Deleted", SelectedRule.Name);
            Refresh();
        }
    }

    private bool CanEdit() => SelectedRule is not null;

    partial void OnSelectedRuleChanged(RuleRow? value)
    {
        EditCommand.NotifyCanExecuteChanged();
        DuplicateCommand.NotifyCanExecuteChanged();
        DeleteCommand.NotifyCanExecuteChanged();
    }

    private void Save(CleanRule rule)
    {
        var errors = RuleValidator.Validate(rule);
        if (errors.Count > 0)
        {
            _notify.Error("Cannot save rule", string.Join("\n", errors));
            return;
        }
        _repo.UpsertCustom(rule);
        _notify.Success("Rule saved", rule.Name);
        Editor = null;
        Refresh();
    }

    private void Cancel() => Editor = null;

    [RelayCommand]
    private void RunTest()
    {
        if (!long.TryParse(TestSizeText, out var size)) size = 0;
        if (!int.TryParse(TestAgeText, out var ageDays)) ageDays = 0;
        var evaluator = new RuleEvaluator(_repo.LoadAll());
        var lastMod = DateTime.UtcNow.AddDays(-Math.Max(ageDays, 0));
        var match = evaluator.Evaluate(TestPath, size, lastMod);

        TestResult = $"Matched: {match.RuleName} ({match.RuleId})\nAction:  {match.Action}\nRisk:    {match.Risk}";
    }

    [RelayCommand]
    private void Export()
    {
        var dlg = new SaveFileDialog { Filter = "JSON files (*.json)|*.json", FileName = $"winvitals-rules-{DateTime.Now:yyyyMMdd}.json" };
        if (dlg.ShowDialog() != true) return;
        try
        {
            File.WriteAllText(dlg.FileName, _repo.ExportAll());
            _notify.Success("Export complete", System.IO.Path.GetFileName(dlg.FileName));
        }
        catch (Exception ex) { _notify.Error("Export failed", ex.Message); }
    }

    [RelayCommand]
    private void Import()
    {
        var dlg = new OpenFileDialog { Filter = "JSON files (*.json)|*.json", CheckFileExists = true };
        if (dlg.ShowDialog() != true) return;
        try
        {
            var json = File.ReadAllText(dlg.FileName);
            var result = _repo.ImportFromJson(json, merge: true);
            if (result.Success)
            {
                _notify.Success("Import complete", $"Added {result.Added}, updated {result.Updated}");
                Refresh();
            }
            else { _notify.Error("Import failed", result.Error ?? "Unknown error"); }
        }
        catch (Exception ex) { _notify.Error("Import failed", ex.Message); }
    }

    private static CleanRule CloneRule(CleanRule s) => new()
    {
        Id = s.Id, Name = s.Name, Description = s.Description,
        PathPatterns = new List<string>(s.PathPatterns),
        Extensions = new List<string>(s.Extensions),
        MinSizeBytes = s.MinSizeBytes, MaxAgeDays = s.MaxAgeDays,
        Action = s.Action, Risk = s.Risk, Priority = s.Priority,
        Enabled = s.Enabled, Preset = s.Preset, IsBuiltIn = s.IsBuiltIn
    };
}

public sealed partial class RuleRow : ObservableObject
{
    private readonly Action<RuleRow, bool> _onToggle;
    private bool _silent;

    public CleanRule Source { get; }
    public string Id => Source.Id;
    public string Name => Source.Name;
    public int Priority => Source.Priority;
    public ItemAction Action => Source.Action;
    public RiskLevel Risk => Source.Risk;
    public ScanPreset Preset => Source.Preset;
    public bool IsBuiltIn => Source.IsBuiltIn;
    public int PathPatternCount => Source.PathPatterns.Count;
    public int ExtensionCount => Source.Extensions.Count;

    [ObservableProperty] private bool enabled;

    partial void OnEnabledChanged(bool value) { if (_silent) return; _onToggle(this, value); }
    public void SetEnabledSilently(bool v) { _silent = true; Enabled = v; _silent = false; }

    public RuleRow(CleanRule src, Action<RuleRow, bool> onToggle)
    {
        Source = src; _onToggle = onToggle; Enabled = src.Enabled;
    }
}
