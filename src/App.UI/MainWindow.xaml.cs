using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using App.Core;
using App.Storage;
using App.Scanner;
using App.Cleaner;
using App.Performance;

namespace AppUI;

public partial class MainWindow : Window
{
    private readonly IStorageProvider _storage;
    private readonly IRuleEngine _ruleEngine;
    private readonly IRiskEngine _riskEngine;
    private readonly IPerformanceAnalyzer _perfAnalyzer;
    private readonly string _baseDir;
    private readonly string _quarantineDir;
    private CancellationTokenSource? _cts;
    private bool _isScanning;

    public MainWindow()
    {
        InitializeComponent();

        _navButtons = [BtnDashboard, BtnDisk, BtnCleaner, BtnLargeFiles, BtnOrphan,
                       BtnDuplicates, BtnDevClean, BtnPerf, BtnStartup, BtnQuarantine, BtnRules];

        _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var dataDir = Path.Combine(_baseDir, "data");
        var rulesDir = Path.Combine(_baseDir, "rules");
        _quarantineDir = Path.Combine(_baseDir, "quarantine");

        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(_quarantineDir);
        Directory.CreateDirectory(rulesDir);

        _storage = new DatabaseProvider(Path.Combine(dataDir, "whm.db"));
        _ruleEngine = new RuleEngine(rulesDir);
        _riskEngine = new RiskEngine(rulesDir);
        _perfAnalyzer = new PerformanceAnalyzer();

        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _ruleEngine.LoadRulesAsync();
        await RefreshDashboardAsync();
        Nav_Click(BtnDashboard, null!);
    }

    private async Task RefreshDashboardAsync()
    {
        try
        {
            var stats = await _storage.GetStatisticsAsync();

        // Get snapshot ONCE
        var snap = await _perfAnalyzer.GetSnapshotAsync();

        // Drives
        var drives = System.IO.DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
            .Select(d =>
            {
                var pct = Math.Round((1 - d.AvailableFreeSpace / (double)d.TotalSize) * 100, 1);
                var bar = new string('█', (int)(pct / 5)) + new string('░', 20 - (int)(pct / 5));
                var color = pct > 90 ? "" : pct > 70 ? "" : "";
                return $"{color} {d.Name} {d.VolumeLabel}: {ScanItem.FormatSize(d.TotalSize - d.AvailableFreeSpace)} / {ScanItem.FormatSize(d.TotalSize)} [{bar}] {pct}%";
            }).ToList();

        TxtDrives.Text = string.Join("\n", drives);

        // Health score (using snap from above)
        TxtScore.Text = $"{snap.HealthScore:F0}";
        TxtScore.Foreground = snap.HealthScore >= 80
            ? new SolidColorBrush(Color.FromRgb(0x9E, 0xCE, 0x6A))
            : snap.HealthScore >= 60
                ? new SolidColorBrush(Color.FromRgb(0xE0, 0xAF, 0x68))
                : new SolidColorBrush(Color.FromRgb(0xF7, 0x76, 0x8E));
        TxtHealthLabel.Text = snap.HealthScore >= 80 ? "Tốt" : snap.HealthScore >= 60 ? "Trung bình" : "Kém";
        TxtHealthLabel.Foreground = TxtScore.Foreground;

        TxtTotalFreed.Text = stats.TotalSpaceFreedFormatted;

        var qItems = await _storage.GetQuarantineItemsAsync();
        var activeQ = qItems.Where(q => q.Status == QuarantineStatus.Active).ToList();
        TxtQuarantinedDash.Text = $"{activeQ.Count} items";

        // Issues
        IssuesList.Items.Clear();
        foreach (var issue in snap.Recommendations)
            IssuesList.Items.Add(new TextBlock
            {
                Text = issue,
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
                Margin = new Thickness(0, 2, 0, 2)
            });

        // Recent activity
        RecentList.Items.Clear();
        var cleans = await _storage.GetCleanHistoryAsync(7);
        foreach (var c in cleans.Take(5))
            RecentList.Items.Add(new TextBlock
            {
                Text = $"[{c.CleanDate:yyyy-MM-dd HH:mm}] {c.CleanLevel}: {c.ItemsCleaned} mục, đã giải phóng {c.SpaceFreedFormatted}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
                Margin = new Thickness(0, 2, 0, 2)
            });

        if (RecentList.Items.Count == 0)
            RecentList.Items.Add(new TextBlock
            {
                Text = "Chưa có hoạt động nào. Hãy chạy quét đầu tiên!",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86))
            });

        // SMART disk health
        try
        {
            var smartResults = SmartDiskChecker.CheckAllDrives();
            if (smartResults.Count > 0)
            {
                RecentList.Items.Add(new TextBlock
                {
                    Text = " Disk Health (SMART)",
                    FontSize = 13,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)),
                    Margin = new Thickness(0, 8, 0, 2)
                });
                foreach (var s in smartResults)
                    RecentList.Items.Add(new TextBlock
                    {
                        Text = $"  {s.Status} {s.DriveModel}: {s.HealthSummary}",
                        FontSize = 12,
                        Foreground = s.PredictFailure
                            ? new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8))
                            : new SolidColorBrush(Color.FromRgb(0xA6, 0xE3, 0xA1)),
                        Margin = new Thickness(0, 2, 0, 2)
                    });
            }
        }
        catch { /* SMART not available on this system */ }

        // Sidebar stats
        TxtSidebarStats.Text = $"Đã dọn: {stats.TotalSpaceFreedFormatted}\nCách ly: {activeQ.Count} mục";
        }
        catch (Exception ex)
        {
            App.Log.Error(ex, "Dashboard refresh failed");
            TxtSidebarStats.Text = "Không thể tải dashboard";
        }
    }

    private readonly List<Button> _navButtons = [];
    private string _currentPage = "";

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        var tag = btn.Tag?.ToString() ?? "Dashboard";

        // Skip if already on this page (debounce)
        if (_currentPage == tag && tag != "Dashboard") return;

        // Chặn chuyển trang nếu đang scan
        if (_isScanning)
        {
            MessageBox.Show("Đang quét — vui lòng đợi hoặc hủy tác vụ hiện tại trước khi chuyển trang.", "Đang xử lý",
                MessageBoxButton.OK, MessageBoxImage.Information);
            // keep current page
            return;
        }

        _currentPage = tag;

        // Hủy mọi tác vụ scan đang chạy trước khi chuyển trang
        try { _cts?.Cancel(); } catch { /* token may not be cancellable */ }
        _cts?.Dispose();
        _cts = null;

        // Reset all nav button highlights
        foreach (var b in _navButtons)
            b.ClearValue(BorderBrushProperty);
        btn.SetValue(BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)));

        bool showDash = tag == "Dashboard";
        DashboardView.Visibility = showDash ? Visibility.Visible : Visibility.Collapsed;
        PageView.Visibility = showDash ? Visibility.Collapsed : Visibility.Visible;

        if (showDash)
        {
            _ = RefreshDashboardAsync().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    App.Log.Error(t.Exception, "Dashboard refresh failed from Nav_Click");
            }, TaskContinuationOptions.OnlyOnFaulted);
            return;
        }

        PagePanel.Children.Clear();

        // Stop any running timers from previous page
        if (PagePanel.Tag is System.Windows.Threading.DispatcherTimer oldTimer)
            oldTimer.Stop();

        PagePanel.Children.Add(new TextBlock
        {
            Text = GetPageTitle(tag),
            Style = FindResource("PageTitle") as Style
        });

        switch (tag)
        {
            case "Disk": BuildDiskPage(); break;
            case "Cleaner": BuildCleanerPage(); break;
            case "LargeFiles": BuildLargeFilesPage(); break;
            case "Orphan": BuildOrphanPage(); break;
            case "Duplicate": BuildDuplicatePage(); break;
            case "DevClean": BuildDevCleanPage(); break;
            case "Performance": BuildPerformancePage(); break;
            case "Startup": BuildStartupPage(); break;
            case "Quarantine": BuildQuarantinePage(); break;
            case "Rules": BuildRulesPage(); break;
        }
    }

    private static string GetPageTitle(string tag) => tag switch
    {
        "Disk" => " Disk Analyzer",
        "Cleaner" => " Cleaner",
        "LargeFiles" => " Large File Finder",
        "Orphan" => " Orphan File Detector",
        "Duplicate" => " Duplicate Finder",
        "DevClean" => " Developer Cleaner",
        "Performance" => "⚡ Performance Analyzer",
        "Startup" => " Startup Manager",
        "Quarantine" => " Quarantine & Restore",
        "Rules" => "⚙️ Rule Engine",
        _ => tag
    };

    private void ShowLoading(string msg)
    {
        _isScanning = true;
        LoadingOverlay.Visibility = Visibility.Visible;
        TxtLoading.Text = msg;
        ScanProgress.IsIndeterminate = true;
    }

    private void HideLoading()
    {
        _isScanning = false;
        LoadingOverlay.Visibility = Visibility.Collapsed;
    }

    // ========== Page Builders ==========

    private void BuildDiskPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Phân tích dung lượng ổ đĩa — hiển thị trực quan dạng treemap.",
            Style = FindResource("PageDesc") as Style
        });

        var drivesPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var driveCombo = new ComboBox { Width = 80, Margin = new Thickness(0, 0, 8, 0) };
        foreach (var d in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed))
            driveCombo.Items.Add(d.Name.TrimEnd('\\'));
        driveCombo.SelectedIndex = 0;
        drivesPanel.Children.Add(driveCombo);

        var scanBtn = new Button { Content = " Scan", Width = 100, Margin = new Thickness(0, 0, 8, 0) };
        scanBtn.Style = FindResource("PrimaryBtn") as Style;
        var cancelBtn = new Button { Content = "⏹ Cancel", Width = 80, IsEnabled = false };
        drivesPanel.Children.Add(scanBtn);
        drivesPanel.Children.Add(cancelBtn);
        panel.Children.Add(drivesPanel);

        var statusLabel = new TextBlock
        {
            Text = "Sẵn sàng.",
            Style = FindResource("StatusText") as Style
        };
        panel.Children.Add(statusLabel);

        var resultsList = new ListBox
        {
            MaxHeight = 200,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        panel.Children.Add(resultsList);

        // Treemap
        var treemap = new TreemapControl { Height = 280, Margin = new Thickness(0, 8, 0, 0) };
        panel.Children.Add(treemap);

        // Store scan results for actions
        List<ScanItem>? _scanResults = null;

        // Action buttons (hidden until scan completes)
        var actionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 8, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var quarantineBtn = new Button { Content = " Quarantine Selected", Width = 160, Margin = new Thickness(0, 0, 8, 0) };
        quarantineBtn.Style = FindResource("PrimaryBtn") as Style;
        var deleteBtn = new Button { Content = "️ Delete Selected", Width = 140, Margin = new Thickness(0, 0, 8, 0) };
        deleteBtn.ApplyTemplate();
        try { deleteBtn.Style = FindResource("DangerBtn") as Style; } catch { }
        var openBtn = new Button { Content = " Open Folder", Width = 120 };
        openBtn.Style = FindResource("PrimaryBtn") as Style;
        actionPanel.Children.Add(quarantineBtn);
        actionPanel.Children.Add(deleteBtn);
        actionPanel.Children.Add(openBtn);
        panel.Children.Add(actionPanel);

        scanBtn.Click += async (_, _) =>
        {
            var drive = driveCombo.SelectedItem?.ToString() + "\\";
            if (string.IsNullOrEmpty(drive) || drive == "\\") return;
            resultsList.Items.Clear();
            treemap.Items = null;
            scanBtn.IsEnabled = false;
            cancelBtn.IsEnabled = true;
            statusLabel.Text = "Đang quét...";

            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var scanner = new DiskScanner(_ruleEngine, _riskEngine);
            var progress = new Progress<(string, int)>(p => { statusLabel.Text = p.Item1; });

            try
            {
                var items = await scanner.ScanAsync([drive], progress, _cts.Token);
                foreach (var item in items.Take(30))
                {
                    var isBlocked = item.RecommendedAction == ItemAction.Block;
                    var icon = isBlocked ? "🔒" : item.Risk <= RiskLevel.Low ? "✅" : item.Risk == RiskLevel.Medium ? "⚠️" : "🔴";
                    resultsList.Items.Add(
                        $"{icon} {item.SizeFormatted,10}  {item.Name}  {item.Suggestion}");
                }
                statusLabel.Text = $"Found {items.Count} items. Total: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))}";
                treemap.Items = items.Where(i => i.SizeBytes > 0).OrderByDescending(i => i.SizeBytes).Take(100).ToList();
                _scanResults = items;
                actionPanel.Visibility = Visibility.Visible;
            }
            catch (OperationCanceledException) { statusLabel.Text = "Đã hủy quét."; }
            catch (Exception ex) { statusLabel.Text = $"Lỗi: {ex.Message}"; }
            finally { scanBtn.IsEnabled = true; cancelBtn.IsEnabled = false; }
        };

        // Quarantine selected folder
        quarantineBtn.Click += async (_, _) =>
        {
            if (resultsList.SelectedIndex < 0 || _scanResults == null ||
                resultsList.SelectedIndex >= Math.Min(_scanResults.Count, 30)) return;
            var item = _scanResults[resultsList.SelectedIndex];
            if (!Directory.Exists(item.Path)) return;

            var (action, risk, _) = _ruleEngine.Evaluate(item.Path, item.SizeBytes);
            if (action == ItemAction.Block)
            {
                MessageBox.Show($" Cannot quarantine: blocked by rule.\n{_ruleEngine.Evaluate(item.Path, item.SizeBytes)}", "Blocked");
                return;
            }

            var qDir = Path.Combine(_quarantineDir, $"{Guid.NewGuid():N}_{item.Name}");
            try
            {
                Directory.Move(item.Path, qDir);
                await _storage.SaveQuarantineItemAsync(new QuarantineItem
                {
                    OriginalPath = item.Path,
                    QuarantinePath = qDir,
                    FileName = item.Name,
                    SizeBytes = item.SizeBytes,
                    QuarantineDate = DateTime.Now,
                    ExpiryDate = DateTime.Now.AddDays(14),
                    Status = QuarantineStatus.Active,
                    Reason = "Disk Analyzer",
                    SourceModule = "DiskAnalyzer",
                    Risk = risk
                });
                statusLabel.Text = $" Quarantined: {item.Name} ({item.SizeFormatted}) — restore in Quarantine tab within 14 days";
                await RefreshDashboardAsync();
            }
            catch (Exception ex) { statusLabel.Text = $"Lỗi: {ex.Message}"; }
        };

        // Delete selected folder (permanently)
        deleteBtn.Click += async (_, _) =>
        {
            if (resultsList.SelectedIndex < 0 || _scanResults == null ||
                resultsList.SelectedIndex >= Math.Min(_scanResults.Count, 30)) return;
            var item = _scanResults[resultsList.SelectedIndex];
            if (!Directory.Exists(item.Path)) return;

            var (action, risk, _) = _ruleEngine.Evaluate(item.Path, item.SizeBytes);
            if (action == ItemAction.Block)
            {
                MessageBox.Show($" Cannot delete: blocked by rule.", "Blocked");
                return;
            }

            var result = MessageBox.Show(
                $"⚠️ Permanently delete:\n\n{item.Name}\n{item.SizeFormatted}\n\n{item.Path}\n\nThis CANNOT be undone!",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                Directory.Delete(item.Path, true);
                statusLabel.Text = $"️ Deleted: {item.Name} ({item.SizeFormatted})";
                await _storage.SaveCleanHistoryAsync(new CleanHistory
                {
                    CleanDate = DateTime.Now,
                    CleanLevel = CleanLevel.Deep,
                    ItemsCleaned = 1,
                    SpaceFreedBytes = item.SizeBytes
                });
                await RefreshDashboardAsync();
            }
            catch (Exception ex) { statusLabel.Text = $"Lỗi: {ex.Message}"; }
        };

        // Open folder in Explorer
        openBtn.Click += (_, _) =>
        {
            if (resultsList.SelectedIndex < 0 || _scanResults == null ||
                resultsList.SelectedIndex >= Math.Min(_scanResults.Count, 30)) return;
            var item = _scanResults[resultsList.SelectedIndex];
            if (Directory.Exists(item.Path))
                System.Diagnostics.Process.Start("explorer.exe", $"\"{item.Path}\"");
        };
    }

    private void BuildCleanerPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Dọn rác an toàn. Dọn Nhanh: temp/logs/thùng rác. Dọn Sâu: file sót, file cũ.",
            Style = FindResource("PageDesc") as Style
        });

        var btns = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
        var quickBtn = new Button { Content = " Quick Clean", Width = 120, Margin = new Thickness(0, 0, 8, 0) };
        quickBtn.Style = FindResource("PrimaryBtn") as Style;
        var deepBtn = new Button { Content = " Deep Clean", Width = 120, Margin = new Thickness(0, 0, 8, 0) };
        deepBtn.Style = FindResource("PrimaryBtn") as Style;
        var previewBtn = new Button { Content = " Preview", Width = 100, Margin = new Thickness(0, 0, 8, 0) };
        previewBtn.Style = FindResource("PrimaryBtn") as Style;
        btns.Children.Add(quickBtn);
        btns.Children.Add(deepBtn);
        btns.Children.Add(previewBtn);
        panel.Children.Add(btns);

        var resultLabel = new TextBlock
        {
            Text = "Chọn chế độ dọn để bắt đầu.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
            Margin = new Thickness(0, 0, 0, 8)
        };
        panel.Children.Add(resultLabel);

        var resultsList = new ListBox
        {
            MaxHeight = 350,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        panel.Children.Add(resultsList);

        quickBtn.Click += async (_, _) =>
        {
            resultsList.Items.Clear();
            quickBtn.IsEnabled = false;
            resultLabel.Text = "Đang quét nhanh...";
            ShowLoading("Đang quét nhanh tất cả ổ đĩa...");

            _cts = new CancellationTokenSource();
            var scanner = new DiskScanner(_ruleEngine, _riskEngine);
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                .Select(d => d.Name).ToList();

            var progress = new Progress<(string Status, int Progress)>(p =>
            {
                Dispatcher.Invoke(() => { resultLabel.Text = p.Status; ScanProgress.Value = p.Progress; });
            });
            var items = await scanner.ScanAsync(drives, progress, _cts.Token);
            var quickItems = items.Where(i => i.Risk <= RiskLevel.Low &&
                i.Category is ItemCategory.TempFile or ItemCategory.LogFile
                    or ItemCategory.CrashDump or ItemCategory.RecycleBin
                    or ItemCategory.Prefetch or ItemCategory.ThumbnailCache).Take(200).ToList();

            HideLoading();

            if (quickItems.Count == 0)
            {
                resultLabel.Text = "Không tìm thấy mục nào để dọn nhanh.";
                quickBtn.IsEnabled = true;
                return;
            }

            var cleaner = new QuickCleaner(_ruleEngine, _riskEngine, _storage);
            var (freed, processed, errors) = await cleaner.CleanAsync(quickItems,
                new Progress<string>(s => resultLabel.Text = s), _cts.Token);

            resultLabel.Text = $"✅ Done! Freed {ScanItem.FormatSize(freed)}. {processed} items cleaned. Errors: {errors.Count}";
            resultsList.Items.Add($"Freed: {ScanItem.FormatSize(freed)}");
            foreach (var item in quickItems.Take(50))
                resultsList.Items.Add($"  ✓ {item.Category}: {item.Name} ({item.SizeFormatted})");
            quickBtn.IsEnabled = true;

            await RefreshDashboardAsync();
        };

        deepBtn.Click += async (_, _) =>
        {
            resultsList.Items.Clear();
            deepBtn.IsEnabled = false;
            resultLabel.Text = "Đang quét sâu...";
            ShowLoading("Đang quét sâu tìm file còn sót và file cũ...");

            _cts = new CancellationTokenSource();
            var scanner = new DiskScanner(_ruleEngine, _riskEngine);
            var orphanScanner = new OrphanDetector(_ruleEngine, _riskEngine);
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                .Select(d => d.Name).ToList();

            var deepProgress = new Progress<(string Status, int Progress)>(p =>
            {
                Dispatcher.Invoke(() => { resultLabel.Text = p.Status; ScanProgress.Value = p.Progress; });
            });
            var items = await scanner.ScanAsync(drives, deepProgress, _cts.Token);
            var orphans = await orphanScanner.ScanAsync(drives, deepProgress, _cts.Token);

            var deepItems = items.Where(i =>
                i.Category is ItemCategory.WindowsUpdateCache or ItemCategory.OldInstaller
                    or ItemCategory.Unknown && i.SizeBytes > 50_000_000)
                .Concat(orphans).DistinctBy(i => i.Path).Take(100).ToList();

            HideLoading();

            if (deepItems.Count == 0)
            {
                resultLabel.Text = "Không tìm thấy mục nào để dọn sâu.";
                deepBtn.IsEnabled = true;
                return;
            }

            resultLabel.Text = $"Found {deepItems.Count} items. Review carefully before cleaning.";
            foreach (var item in deepItems)
                resultsList.Items.Add(
                    $"  [{item.Risk}] {item.Category}: {item.Name} ({item.SizeFormatted}) - {item.Suggestion}");

            // Add a confirm clean button
            var confirmBtn = new Button
            {
                Content = $"⚠️ Clean {deepItems.Count} Items",
                Width = 180,
                Margin = new Thickness(0, 8, 0, 0)
            };
            confirmBtn.Style = FindResource("DangerBtn") as Style;
            panel.Children.Add(confirmBtn);

            confirmBtn.Click += async (_, _2) =>
            {
                confirmBtn.IsEnabled = false;
                var cleaner = new DeepCleaner(_ruleEngine, _riskEngine, _storage);
                var (freed, processed, errors) = await cleaner.CleanAsync(deepItems,
                    new Progress<string>(s => resultLabel.Text = s), _cts!.Token);
                resultLabel.Text = $"✅ Deep clean done! Freed {ScanItem.FormatSize(freed)}. {processed} items. Quarantined some high-risk items.";
                panel.Children.Remove(confirmBtn);
                deepBtn.IsEnabled = true;
                await RefreshDashboardAsync();
            };
            deepBtn.IsEnabled = true;
        };

        // Preview mode: scan & show results WITHOUT deleting
        previewBtn.Click += async (_, _) =>
        {
            resultsList.Items.Clear();
            previewBtn.IsEnabled = false;
            resultLabel.Text = " DRY RUN — Scanning...";
            ShowLoading("Preview scanning (no files will be deleted)...");

            _cts = new CancellationTokenSource();
            var scanner = new DiskScanner(_ruleEngine, _riskEngine);
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                .Select(d => d.Name).ToList();

            var progress = new Progress<(string Status, int Progress)>(p =>
            {
                Dispatcher.Invoke(() => { resultLabel.Text = " DRY RUN — " + p.Status; ScanProgress.Value = p.Progress; });
            });
            var items = await scanner.ScanAsync(drives, progress, _cts.Token);
            HideLoading();

            // Categorize for preview
            var safeItems = items.Where(i => i.Risk <= RiskLevel.Low).ToList();
            var warnItems = items.Where(i => i.Risk == RiskLevel.Medium).ToList();
            var blockedItems = items.Where(i => i.Risk >= RiskLevel.High || i.RecommendedAction == ItemAction.Block).ToList();

            resultsList.Items.Add($"━━━ DRY RUN PREVIEW — No files were deleted ━━━");
            resultsList.Items.Add($"");
            resultsList.Items.Add($"✅ An toàn: {safeItems.Count} items ({ScanItem.FormatSize(safeItems.Sum(i => i.SizeBytes))})");
            resultsList.Items.Add($"⚠️  Cần xem lại: {warnItems.Count} items ({ScanItem.FormatSize(warnItems.Sum(i => i.SizeBytes))})");
            resultsList.Items.Add($" Blocked by rules: {blockedItems.Count} items ({ScanItem.FormatSize(blockedItems.Sum(i => i.SizeBytes))})");
            resultsList.Items.Add($"");
            resultsList.Items.Add($" Total scannable: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))} across {items.Count} items");
            resultsList.Items.Add($"");
            resultsList.Items.Add($" Run Quick Clean or Deep Clean to actually remove these files.");

            foreach (var item in safeItems.Take(20))
                resultsList.Items.Add($"  ✅ [{item.Category}] {item.Name} ({item.SizeFormatted})");
            foreach (var item in warnItems.Take(10))
                resultsList.Items.Add($"  ⚠️ [{item.Category}] {item.Name} ({item.SizeFormatted}) — {item.Suggestion}");
            foreach (var item in blockedItems.Take(5))
                resultsList.Items.Add($"   [{item.Category}] {item.Name} ({item.SizeFormatted}) — BLOCKED: {item.MatchedRule}");

            resultLabel.Text = $" DRY RUN COMPLETE — {safeItems.Count} safe, {warnItems.Count} need review, {blockedItems.Count} blocked";
            previewBtn.IsEnabled = true;
        };
    }

    private void BuildLargeFilesPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Tìm file lớn (>100MB) đang chiếm dung lượng.",
            Style = FindResource("PageDesc") as Style
        });
        var scanBtn = new Button { Content = " Find Large Files", Width = 140, Margin = new Thickness(0, 0, 0, 8) };
        scanBtn.Style = FindResource("PrimaryBtn") as Style;
        panel.Children.Add(scanBtn);

        var statusLabel = new TextBlock
        {
            Text = "Sẵn sàng.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8))
        };
        panel.Children.Add(statusLabel);

        var results = new ListBox
        {
            MaxHeight = 400,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        panel.Children.Add(results);

        scanBtn.Click += async (_, _) =>
        {
            results.Items.Clear();
            scanBtn.IsEnabled = false;
            statusLabel.Text = "Searching for files > 100MB...";
            ScanProgress.IsIndeterminate = false;
            ShowLoading("Đang tìm file lớn...");

            _cts = new CancellationTokenSource();
            var finder = new LargeFileFinder(_ruleEngine, _riskEngine);
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                .Select(d => d.Name).ToList();

            var progress = new Progress<(string Status, int Progress)>(p =>
            {
                Dispatcher.Invoke(() =>
                {
                    statusLabel.Text = p.Status;
                    ScanProgress.Value = p.Progress;
                });
            });

            var items = await finder.ScanAsync(drives, progress, _cts.Token);
            HideLoading();

            foreach (var item in items.Take(80))
                results.Items.Add(
                    $"{item.SizeFormatted,10} | {item.Extension,-6} | {item.Name} - {item.Suggestion}");
            statusLabel.Text = $"Tìm thấy {items.Count} file lớn. Tổng: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))}";
            scanBtn.IsEnabled = true;
        };
    }

    private void BuildOrphanPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Phát hiện file còn sót sau khi gỡ phần mềm.",
            Style = FindResource("PageDesc") as Style
        });
        var scanBtn = new Button { Content = " Scan for Orphans", Width = 150, Margin = new Thickness(0, 0, 0, 8) };
        scanBtn.Style = FindResource("PrimaryBtn") as Style;
        panel.Children.Add(scanBtn);

        var statusLabel = new TextBlock
        {
            Text = "Sẵn sàng.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8))
        };
        panel.Children.Add(statusLabel);

        var results = new ListBox
        {
            MaxHeight = 400,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        panel.Children.Add(results);

        scanBtn.Click += async (_, _) =>
        {
            results.Items.Clear();
            scanBtn.IsEnabled = false;
            statusLabel.Text = "Đang kiểm tra ứng dụng đã cài và tìm file sót...";
            ScanProgress.IsIndeterminate = false;
            ShowLoading("Đang phát hiện file mồ côi...");

            _cts = new CancellationTokenSource();
            var detector = new OrphanDetector(_ruleEngine, _riskEngine);
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                .Select(d => d.Name).ToList();

            var progress = new Progress<(string Status, int Progress)>(p =>
            {
                Dispatcher.Invoke(() => { statusLabel.Text = p.Status; ScanProgress.Value = p.Progress; });
            });
            var items = await detector.ScanAsync(drives, progress, _cts.Token);
            HideLoading();

            foreach (var item in items)
                results.Items.Add(
                    $"⚠️ {item.SizeFormatted,10} | {item.Name} - Likely from: {item.AppOrigin}");
            statusLabel.Text = $"Tìm thấy {items.Count} mục mồ côi. Tổng: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))}";
            scanBtn.IsEnabled = true;
        };
    }

    private void BuildDuplicatePage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Tìm file trùng lặp bằng cách so sánh hash.",
            Style = FindResource("PageDesc") as Style
        });
        var scanBtn = new Button { Content = " Find Duplicates", Width = 140, Margin = new Thickness(0, 0, 0, 8) };
        scanBtn.Style = FindResource("PrimaryBtn") as Style;
        panel.Children.Add(scanBtn);

        var statusLabel = new TextBlock
        {
            Text = "Sẵn sàng.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8))
        };
        panel.Children.Add(statusLabel);

        var results = new ListBox
        {
            MaxHeight = 400,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        panel.Children.Add(results);

        scanBtn.Click += async (_, _) =>
        {
            results.Items.Clear();
            scanBtn.IsEnabled = false;
            statusLabel.Text = "Phase 1: Scanning files...";
            ScanProgress.IsIndeterminate = false;
            ShowLoading("Đang tìm file trùng lặp...");

            _cts = new CancellationTokenSource();
            var finder = new DuplicateFinder(_ruleEngine, _riskEngine);
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                .Select(d => d.Name).ToList();

            var progress = new Progress<(string Status, int Progress)>(p =>
            {
                Dispatcher.Invoke(() => { statusLabel.Text = p.Status; ScanProgress.Value = p.Progress; });
            });
            var items = await finder.ScanAsync(drives, progress, _cts.Token);
            HideLoading();

            foreach (var item in items.Take(60))
                results.Items.Add(
                    $"  {item.SizeFormatted,10} | {item.Name} - {item.Suggestion}");
            statusLabel.Text = $"Tìm thấy {items.Count} file trùng. Tổng: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))}";
            scanBtn.IsEnabled = true;
        };
    }

    private static readonly (string Type, string[] Dirs, string Icon)[] DevCacheTypes = [
        ("Node.js",     ["node_modules"],                  ""),
        ("Next.js",     [".next", ".nuxt", ".output"],     ""),
        ("Build",       ["build", "dist", "out"],          ""),
        ("Python",      ["__pycache__", ".pytest_cache", ".mypy_cache", ".ruff_cache"], ""),
        (".NET",        ["obj", "bin"],                    ""),
        ("Gradle",      [".gradle"],                       ""),
        ("Rust/Java",   ["target"],                        ""),
        ("Flutter",     [".dart_tool", ".flutter-plugins"], ""),
        ("PHP/Go",      ["vendor"],                        ""),
        ("Terraform",   [".terraform"],                    ""),
        ("iOS/Cocoa",   ["Pods"],                          ""),
        ("Coverage",    ["coverage", ".nyc_output"],       ""),
    ];

    private record DevCacheItem(
        string Type, string Path, string Name, long SizeBytes,
        DateTime LastModified, bool IsSafe)
    {
        public string SizeFormatted => ScanItem.FormatSize(SizeBytes);
        public double AgeDays => (DateTime.Now - LastModified).TotalDays;
        public string AgeText => AgeDays < 1 ? "Hôm nay" :
            AgeDays < 30 ? $"{(int)AgeDays} ngày" :
            $"{(int)(AgeDays / 30)} tháng";
    }

        private void BuildDevCleanPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock { Text = "Dọn cache lập trình: node_modules, build, .next, __pycache__, target, gradle, ...", Style = FindResource("PageDesc") as Style });

        var pathPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12) };
        var pathBox = new TextBox { Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Width = 340, Margin = new Thickness(0, 0, 6, 0), Style = FindResource("ModernTextBox") as Style };
        pathPanel.Children.Add(pathBox);
        var browseBtn = new Button { Content = "📂", Width = 34, Height = 34, Margin = new Thickness(0, 0, 6, 0), Style = FindResource("SecondaryBtn") as Style };
        browseBtn.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog();
            dlg.DefaultDirectory = pathBox.Text;
            if (dlg.ShowDialog() == true)
                pathBox.Text = dlg.FolderName;
        };
        pathPanel.Children.Add(browseBtn);
        var scanBtn = new Button { Content = "  Quét", Width = 80, Style = FindResource("PrimaryBtn") as Style };
        pathPanel.Children.Add(scanBtn);
        panel.Children.Add(pathPanel);

        var statusLabel = new TextBlock { Style = FindResource("StatusText") as Style };
        var cardPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 12), Visibility = Visibility.Collapsed };
        panel.Children.Add(cardPanel);

        var filterPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
        var ageFilter = new CheckBox { Content = "Cache cũ > 30 ngày", Style = FindResource("FilterCheckBox") as Style, IsChecked = true };
        var sizeFilter = new CheckBox { Content = "Chỉ > 50 MB", Style = FindResource("FilterCheckBox") as Style };
        filterPanel.Children.Add(ageFilter);
        filterPanel.Children.Add(sizeFilter);
        panel.Children.Add(filterPanel);
        panel.Children.Add(statusLabel);

        var results = new ListBox { MaxHeight = 340, Style = FindResource("ModernListBox") as Style };
        panel.Children.Add(results);

        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
        var toggleBtn = new Button { Content = "Chọn tất cả", Width = 110, Margin = new Thickness(0, 0, 8, 0), Style = FindResource("SecondaryBtn") as Style };
        var totalText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12.5, Foreground = FindResource("TextSecondary") as Brush ?? Brushes.Gray };
        var cleanBtn = new Button { Content = " Dọn đã chọn", Width = 140, Margin = new Thickness(8, 0, 0, 0), IsEnabled = false, Style = FindResource("SuccessBtn") as Style };
        actionPanel.Children.Add(toggleBtn);
        actionPanel.Children.Add(totalText);
        actionPanel.Children.Add(cleanBtn);
        panel.Children.Add(actionPanel);

        List<DevCacheItem>? allItems = null;
        var selectedPaths = new HashSet<string>();
        bool allToggled = true;
        IEnumerable<DevCacheItem> currentFiltered = [];

        scanBtn.Click += async (_, _) =>
        {
            results.Items.Clear();
            cardPanel.Visibility = filterPanel.Visibility = actionPanel.Visibility = Visibility.Collapsed;
            selectedPaths.Clear();
            scanBtn.IsEnabled = false;
            statusLabel.Text = "Đang tìm cache lập trình...";
            ShowLoading("Đang quét cache dev...");
            _cts = new CancellationTokenSource();
            var items = await Task.Run(() => ScanDevCaches(pathBox.Text, _cts.Token), _cts.Token);
            HideLoading();
            allItems = items;
            statusLabel.Text = $"Tìm thấy {items.Count} cache dev. Tổng: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))}";
            scanBtn.IsEnabled = true;
            if (items.Count == 0) return;

            cardPanel.Children.Clear();
            var groups = items.GroupBy(i => i.Type).OrderByDescending(g => g.Sum(i => i.SizeBytes)).Take(6);
            foreach (var g in groups)
            {
                var card = new Border { Style = FindResource("CategoryCard") as Style };
                var st = new StackPanel();
                st.Children.Add(new TextBlock { Text = g.Key, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = FindResource("TextPrimary") as Brush });
                st.Children.Add(new TextBlock { Text = ScanItem.FormatSize(g.Sum(i => i.SizeBytes)), FontSize = 16, FontWeight = FontWeights.Bold, Foreground = FindResource("Accent") as Brush, Margin = new Thickness(0, 2, 0, 0) });
                st.Children.Add(new TextBlock { Text = $"{g.Count()} items", FontSize = 10.5, Foreground = FindResource("TextSecondary") as Brush });
                card.Child = st;
                cardPanel.Children.Add(card);
            }
            cardPanel.Visibility = filterPanel.Visibility = Visibility.Visible;

            void RenderList()
            {
                results.Items.Clear();
                selectedPaths.Clear();
                allToggled = true;
                currentFiltered = items.AsEnumerable();
                if (ageFilter.IsChecked == true) currentFiltered = currentFiltered.Where(i => i.AgeDays > 30);
                if (sizeFilter.IsChecked == true) currentFiltered = currentFiltered.Where(i => i.SizeBytes > 50_000_000);
                currentFiltered = currentFiltered.OrderByDescending(i => i.SizeBytes).ToList();
                foreach (var item in currentFiltered)
                {
                    var p = item.Path;
                    selectedPaths.Add(p);
                    var row = new Border { Padding = new Thickness(10, 7, 10, 7), BorderBrush = FindResource("CardBorder") as Brush, BorderThickness = new Thickness(0, 0, 0, 1) };
                    var g2 = new Grid();
                    g2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    g2.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8, GridUnitType.Star) });
                    g2.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                    var cb = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 8, 0) };
                    cb.Checked += (_, _) => { selectedPaths.Add(p); UpdateDevSummary(currentFiltered, totalText, cleanBtn, selectedPaths); };
                    cb.Unchecked += (_, _) => { selectedPaths.Remove(p); UpdateDevSummary(currentFiltered, totalText, cleanBtn, selectedPaths); };
                    Grid.SetColumn(cb, 0); g2.Children.Add(cb);
                    var info = new StackPanel();
                    info.Children.Add(new TextBlock { Text = $"{item.Name}  ·  {item.SizeFormatted}", Style = FindResource("DevItemName") as Style });
                    info.Children.Add(new TextBlock { Text = $"{item.Type}  ·  {item.AgeText}  ·  {item.Path}", Style = FindResource("DevItemSub") as Style, Margin = new Thickness(0, 1, 0, 0) });
                    Grid.SetColumn(info, 1); g2.Children.Add(info);
                    var badge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, Background = item.IsSafe ? new SolidColorBrush(Color.FromRgb(0x1A, 0x34, 0x28)) : new SolidColorBrush(Color.FromRgb(0x34, 0x28, 0x20)) };
                    badge.Child = new TextBlock { Text = item.IsSafe ? "An toàn" : "Thận trọng", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = item.IsSafe ? new SolidColorBrush(Color.FromRgb(0x9E, 0xCE, 0x6A)) : new SolidColorBrush(Color.FromRgb(0xE0, 0xAF, 0x68)) };
                    Grid.SetColumn(badge, 2); g2.Children.Add(badge);
                    row.Child = g2; results.Items.Add(row);
                }
                UpdateDevSummary(currentFiltered, totalText, cleanBtn, selectedPaths);
            }
            RenderList();
            ageFilter.Checked += (_, _) => RenderList();
            ageFilter.Unchecked += (_, _) => RenderList();
            sizeFilter.Checked += (_, _) => RenderList();
            sizeFilter.Unchecked += (_, _) => RenderList();
            toggleBtn.Click += (_, _) => { allToggled = !allToggled; toggleBtn.Content = allToggled ? "Chọn tất cả" : "Bỏ chọn"; if (allToggled) { selectedPaths.Clear(); foreach (var i in currentFiltered) selectedPaths.Add(i.Path); } else { selectedPaths.Clear(); } UpdateDevSummary(currentFiltered, totalText, cleanBtn, selectedPaths); };
            actionPanel.Visibility = Visibility.Visible;
            UpdateDevSummary(currentFiltered, totalText, cleanBtn, selectedPaths);
        };
        cleanBtn.Click += async (_, _) =>
        {
            var toClean = allItems?.Where(i => selectedPaths.Contains(i.Path)).Select(i => new ScanItem { Path = i.Path, Name = i.Name, SizeBytes = i.SizeBytes, IsDirectory = true, Category = ItemCategory.DevCache, Risk = i.IsSafe ? RiskLevel.Low : RiskLevel.Medium, RecommendedAction = ItemAction.SafeDelete }).ToList();
            if ((toClean?.Count ?? 0) == 0) return;
            cleanBtn.IsEnabled = false;
            var cleaner = new DeveloperCleaner(_ruleEngine, _riskEngine, _storage);
            var (freed, processed, errors) = await cleaner.CleanAsync(toClean!, new Progress<string>(s => statusLabel.Text = s), _cts?.Token ?? CancellationToken.None);
            statusLabel.Text = $"✅ Xong! Giải phóng {ScanItem.FormatSize(freed)}. {processed} cache đã xóa.";
            results.Items.Clear();
            cleanBtn.IsEnabled = true;
            actionPanel.Visibility = cardPanel.Visibility = filterPanel.Visibility = Visibility.Collapsed;
            await RefreshDashboardAsync();
        };
    }

    private List<DevCacheItem> ScanDevCaches(string rootPath, CancellationToken ct)
    {
        var results = new List<DevCacheItem>();
        var typeLookup = DevCacheTypes.SelectMany(t => t.Dirs.Select(d => (t.Type, d))).ToDictionary(x => x.d, x => x.Type, StringComparer.OrdinalIgnoreCase);
        try
        {
            foreach (var dir in Directory.GetDirectories(rootPath, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true, MaxRecursionDepth = 6 }))
            {
                if (ct.IsCancellationRequested) break;
                var dirName = Path.GetFileName(dir);
                if (!typeLookup.TryGetValue(dirName, out var type)) continue;
                try { var dInfo = new DirectoryInfo(dir); var size = GetDirSize(dir); if (size >= 1024 * 1024) results.Add(new DevCacheItem(type, dir, dirName, size, dInfo.LastWriteTime, dirName is "node_modules" or "vendor" ? false : true)); } catch { }
            }
        }
        catch { }
        return results.OrderByDescending(i => i.SizeBytes).ToList();
    }

    private static long GetDirSize(string path)
    {
        long size = 0;
        try { foreach (var f in Directory.GetFiles(path, "*.*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true })) try { size += new FileInfo(f).Length; } catch { } } catch { }
        return size;
    }

    private static void UpdateDevSummary(IEnumerable<DevCacheItem> filtered, TextBlock totalText, Button cleanBtn, HashSet<string> selected)
    {
        var total = filtered.Where(i => selected.Contains(i.Path)).Sum(i => i.SizeBytes);
        var count = filtered.Count(i => selected.Contains(i.Path));
        totalText.Text = $"Đã chọn: {count} items ({ScanItem.FormatSize(total)})";
        cleanBtn.IsEnabled = count > 0;
        cleanBtn.Content = count > 0 ? $" Don {count} muc" : " Dọn đã chọn";
    }

private async void BuildPerformancePage()
    {
        var panel = PagePanel;
        ShowLoading("Đang phân tích hiệu năng hệ thống...");

        try
        {
            var snap = await _perfAnalyzer.GetSnapshotAsync();
            HideLoading();
            RenderPerformancePage(panel, snap);
        }
        catch (Exception ex)
        {
            HideLoading();
            panel.Children.Clear();
            panel.Children.Add(new TextBlock
            {
                Text = $"⚠️ Failed to analyze performance: {ex.Message}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
    }

    private void RenderPerformancePage(StackPanel panel, PerformanceSnapshot snap)
    {
        panel.Children.Clear();
        panel.Children.Add(new TextBlock
        {
            Text = "⚡ Performance Analyzer",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"CPU: {snap.CpuPercent:F1}% ({snap.CpuCoreCount} nhân) | " +
                   $"RAM: {snap.MemoryUsedGB:F1}/{snap.MemoryTotalGB:F1} GB ({snap.MemoryPercent:F0}%) | " +
                   $"Ổ {snap.DriveLetter}: {snap.DiskFreeGB:F1} GB trống ({100 - snap.DiskPercent:F0}%)",
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        // Real-time chart
        var chart = new PerfChart { Height = 180, Margin = new Thickness(0, 0, 0, 8) };
        chart.AddDataPoint(snap.CpuPercent, snap.MemoryPercent, snap.DiskPercent);
        panel.Children.Add(chart);

        // Refresh timer — updates chart + stats every 2 seconds
        var statsLabel = (TextBlock)panel.Children[^2]; // second-to-last element
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) =>
        {
            try
            {
                var s = await _perfAnalyzer.GetSnapshotAsync();
                chart.AddDataPoint(s.CpuPercent, s.MemoryPercent, s.DiskPercent);
                statsLabel.Text = $"CPU: {s.CpuPercent:F1}% ({s.CpuCoreCount} cores) | " +
                    $"RAM: {s.MemoryUsedGB:F1}/{s.MemoryTotalGB:F1} GB ({s.MemoryPercent:F0}%) | " +
                    $"Disk {s.DriveLetter}: {s.DiskFreeGB:F1} GB free ({100 - s.DiskPercent:F0}%)";
            }
            catch { /* skip if perf counter fails during refresh */ }
        };
        timer.Start();

        // Cleanup timer when navigating away
        panel.Tag = timer;

        panel.Children.Add(new TextBlock
        {
            Text = "Tiến Trình Ngốn Nhiều RAM Nhất",
            FontSize = 14,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0x89, 0xB4, 0xFA)),
            Margin = new Thickness(0, 8, 0, 4)
        });

        var procList = new ListBox
        {
            MaxHeight = 300,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        foreach (var p in snap.TopProcesses.Take(20))
        {
            var color = p.MemoryMB > 1000 ? "" : p.MemoryMB > 200 ? "" : "";
            procList.Items.Add($"{color} PID:{p.Pid,-6} {p.MemoryMB,8:F0} MB | {p.Name} ({p.Status})");
        }
        panel.Children.Add(procList);

        var refreshBtn = new Button
        {
            Content = " Refresh",
            Width = 100,
            Margin = new Thickness(0, 8, 0, 0)
        };
        refreshBtn.Style = FindResource("PrimaryBtn") as Style;
        refreshBtn.Click += (_, _) => BuildPerformancePage();
        panel.Children.Add(refreshBtn);
    }

    private async void BuildStartupPage()
    {
        var panel = PagePanel;
        ShowLoading("Đang tải danh sách khởi động...");

        try
        {
            var entries = await _perfAnalyzer.GetStartupEntriesAsync();
            HideLoading();

        panel.Children.Clear();
        panel.Children.Add(new TextBlock
        {
            Text = " Startup Manager",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"Tìm thấy {entries.Count} mục khởi động. Tắt bớt để tăng tốc khởi động máy.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var entryList = new ListBox
        {
            MaxHeight = 400,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        foreach (var e in entries)
        {
            var impact = e.Impact switch { "High" => "", "Medium" => "", _ => "" };
            entryList.Items.Add($"{impact} [{e.Impact,-6}] {e.Name} - {e.Publisher} ({e.Location})");
        }
        panel.Children.Add(entryList);

        var disableBtn = new Button
        {
            Content = "⏸ Disable Selected",
            Width = 140,
            Margin = new Thickness(0, 8, 0, 0)
        };
        disableBtn.Style = FindResource("PrimaryBtn") as Style;
        disableBtn.Click += async (_, _) =>
        {
            if (entryList.SelectedIndex < 0 || entryList.SelectedIndex >= entries.Count) return;
            var entry = entries[entryList.SelectedIndex];
            if (await _perfAnalyzer.DisableStartupEntryAsync(entry))
            {
                var idx = entryList.SelectedIndex;
                entryList.Items[idx] = " " + entryList.Items[idx]!.ToString() + " (Disabled)";
            }
        };
        panel.Children.Add(disableBtn);
        }
        catch (Exception ex)
        {
            HideLoading();
            App.Log.Error(ex, "Startup page failed to load");
            panel.Children.Add(new TextBlock
            {
                Text = $"⚠️ Không thể tải danh sách khởi động: {ex.Message}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
    }

    private async void BuildQuarantinePage()
    {
        var panel = PagePanel;
        ShowLoading("Đang tải khu cách ly...");

        try
        {
            var items = await _storage.GetQuarantineItemsAsync();
        var active = items.Where(q => q.Status == QuarantineStatus.Active).ToList();
        HideLoading();

        panel.Children.Clear();
        panel.Children.Add(new TextBlock
        {
            Text = " Quarantine & Restore",
            FontSize = 20,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4)),
            Margin = new Thickness(0, 0, 0, 12)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"{active.Count} items in quarantine ({ScanItem.FormatSize(active.Sum(i => i.SizeBytes))}). " +
                   "File được giữ 14 ngày trước khi xóa vĩnh viễn.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var qList = new ListBox
        {
            MaxHeight = 350,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };
        foreach (var q in active)
            qList.Items.Add(
                $"[{q.DaysRemaining}d left] {q.SizeFormatted,10} | {q.FileName} - {q.Reason} ({q.Risk})");
        panel.Children.Add(qList);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 8, 0, 0) };
        var restoreBtn = new Button { Content = "↩️ Restore Selected", Width = 140, Margin = new Thickness(0, 0, 8, 0) };
        restoreBtn.Style = FindResource("PrimaryBtn") as Style;
        var deleteBtn = new Button { Content = "️ Delete Permanently", Width = 160 };
        deleteBtn.Style = FindResource("DangerBtn") as Style;

        btnPanel.Children.Add(restoreBtn);
        btnPanel.Children.Add(deleteBtn);
        panel.Children.Add(btnPanel);

        restoreBtn.Click += async (_, _) =>
        {
            if (qList.SelectedIndex < 0 || qList.SelectedIndex >= active.Count) return;
            var q = active[qList.SelectedIndex];
            if (File.Exists(q.QuarantinePath) || Directory.Exists(q.QuarantinePath))
            {
                var destDir = Path.GetDirectoryName(q.OriginalPath);
                if (destDir != null) Directory.CreateDirectory(destDir);
                if (File.Exists(q.QuarantinePath))
                    File.Move(q.QuarantinePath, q.OriginalPath);
                else
                    Directory.Move(q.QuarantinePath, q.OriginalPath);
            }
            await _storage.RemoveQuarantineItemAsync(q.Id);
            MessageBox.Show("File restored successfully.", "Restored");
            BuildQuarantinePage();
            await RefreshDashboardAsync();
        };

        deleteBtn.Click += async (_, _) =>
        {
            if (qList.SelectedIndex < 0 || qList.SelectedIndex >= active.Count) return;
            var q = active[qList.SelectedIndex];
            if (MessageBox.Show($"Permanently delete {q.FileName}?", "Confirm",
                MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            {
                try
                {
                    if (File.Exists(q.QuarantinePath)) File.Delete(q.QuarantinePath);
                    else if (Directory.Exists(q.QuarantinePath)) Directory.Delete(q.QuarantinePath, true);
                }
                catch { /* ignore */ }
                await _storage.RemoveQuarantineItemAsync(q.Id);
                BuildQuarantinePage();
                await RefreshDashboardAsync();
            }
        };
        }
        catch (Exception ex)
        {
            HideLoading();
            App.Log.Error(ex, "Quarantine page failed to load");
            panel.Children.Add(new TextBlock
            {
                Text = $"⚠️ Không thể tải khu cách ly: {ex.Message}",
                Foreground = new SolidColorBrush(Color.FromRgb(0xF3, 0x8B, 0xA8)),
                Margin = new Thickness(0, 8, 0, 0)
            });
        }
    }

    private void BuildRulesPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Bộ Quy Tắc — Cấu hình cách đánh giá file theo mức độ rủi ro và hành động.",
            Foreground = new SolidColorBrush(Color.FromRgb(0xA6, 0xAD, 0xC8)),
            Margin = new Thickness(0, 0, 0, 8)
        });

        var rules = _ruleEngine.GetRules();
        var rulesList = new ListBox
        {
            MaxHeight = 450,
            Background = new SolidColorBrush(Color.FromRgb(0x31, 0x32, 0x44)),
            Foreground = new SolidColorBrush(Color.FromRgb(0xCD, 0xD6, 0xF4))
        };

        foreach (var r in rules.OrderByDescending(r => r.Priority))
        {
            var actionIcon = r.Action switch
            {
                ItemAction.SafeDelete => "✅",
                ItemAction.WarnDelete => "⚠️",
                ItemAction.Block => "",
                ItemAction.Quarantine => "",
                _ => "❓"
            };
            rulesList.Items.Add(
                $"{actionIcon} P{r.Priority:D3} [{r.Risk,-8}] {r.Name}: {r.Action} " +
                $"- Exts:[{string.Join(",", r.Extensions.Take(3))}] Paths:[{string.Join(",", r.PathPatterns.Take(2))}]");
        }
        panel.Children.Add(rulesList);

        panel.Children.Add(new TextBlock
        {
            Text = $"Total: {rules.Count} rules loaded.",
            Foreground = new SolidColorBrush(Color.FromRgb(0x6C, 0x70, 0x86)),
            Margin = new Thickness(0, 8, 0, 0)
        });
    }

    // ========== Dashboard Actions ==========

    private async void FullScan_Click(object sender, RoutedEventArgs e)
    {
        Nav_Click(BtnDisk, null!);
        await RefreshDashboardAsync();
    }

    private void QuickClean_Click(object sender, RoutedEventArgs e)
    {
        Nav_Click(BtnCleaner, null!);
    }

    private async void PerfCheck_Click(object sender, RoutedEventArgs e)
    {
        Nav_Click(BtnPerf, null!);
        await RefreshDashboardAsync();
    }
}
