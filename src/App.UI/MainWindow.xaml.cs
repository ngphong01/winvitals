using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using App.Core;
using App.Storage;
using App.Scanner;
using App.Cleaner;
using App.Performance;
using AppUI.ViewModels;
using Microsoft.Extensions.DependencyInjection;

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
    public bool IsScanning => _isScanning;
    private readonly List<(string Path, long SizeBytes, bool WasDirectory)> _lastCleanSnapshot = [];
    private Dictionary<string, long>? _diskBeforeSnapshot;

    // MVVM ViewModels
    private DashboardViewModel? _dashboardVM;
    private PerformanceViewModel? _perfVM;
    private ScannerViewModel? _scanVM;
    private SettingsViewModel? _settingsVM;
    private QuarantineViewModel? _quarantineVM;
    private DispatcherTimer? _perfTimer;

    public MainWindow()
    {
        InitializeComponent();

        _navButtons = [BtnDashboard, BtnDisk, BtnCleaner, BtnFileTools, BtnDevTools,
                       BtnPerf, BtnQuarantine, BtnAutoClean, BtnSettings];

        _baseDir = AppDomain.CurrentDomain.BaseDirectory;
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dataDir = Path.Combine(localAppData, "WindowsHealthManager");
        var rulesDir = Path.Combine(_baseDir, "rules");
        _quarantineDir = DatabaseProvider.GetQuarantineDirectory();

        Directory.CreateDirectory(dataDir);
        Directory.CreateDirectory(_quarantineDir);
        Directory.CreateDirectory(rulesDir);

        _storage = App.Services.GetRequiredService<IStorageProvider>();
        _ruleEngine = new RuleEngine(rulesDir);
        _riskEngine = new RiskEngine(rulesDir);
        _perfAnalyzer = new PerformanceAnalyzer();

        // Resolve ViewModels from DI
        _dashboardVM = App.Services.GetRequiredService<DashboardViewModel>();
        _perfVM = App.Services.GetRequiredService<PerformanceViewModel>();
        _scanVM = App.Services.GetRequiredService<ScannerViewModel>();
        _settingsVM = App.Services.GetRequiredService<SettingsViewModel>();
        _quarantineVM = App.Services.GetRequiredService<QuarantineViewModel>();

        Loaded += async (_, _) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _ruleEngine.LoadRulesAsync();
        await _storage.MigrateLegacyQuarantineDirectoryAsync();
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

        // Drives — visual progress cards
        var drives = System.IO.DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
            .ToList();

        DriveCards.Items.Clear();
        foreach (var d in drives)
        {
            var pct = Math.Round((1 - d.AvailableFreeSpace / (double)d.TotalSize) * 100, 1);
            var used = d.TotalSize - d.AvailableFreeSpace;
            var color = pct > 90 ? "Danger" : pct > 70 ? "Warning" : "Success";
            var colorBrush = pct > 90
                ? new SolidColorBrush(Color.FromRgb(0xE7, 0x6F, 0x80))
                : pct > 70
                    ? new SolidColorBrush(Color.FromRgb(0xDD, 0xAF, 0x68))
                    : new SolidColorBrush(Color.FromRgb(0x7E, 0xCF, 0x6A));

            var card = new Border
            {
                Style = FindResource("CardMetric") as Style,
                Width = 220, Margin = new Thickness(0, 0, 10, 10),
                Padding = new Thickness(14),
                Child = new StackPanel
                {
                    Children =
                    {
                        new StackPanel
                        {
                            Orientation = Orientation.Horizontal,
                            Children =
                            {
                                new TextBlock { Text = $"{d.Name} ", FontSize = 13, FontWeight = FontWeights.Bold,
                                    Foreground = Primary },
                                new TextBlock { Text = d.VolumeLabel, FontSize = 11,
                                    Foreground = Secondary,
                                    VerticalAlignment = VerticalAlignment.Center }
                            }
                        },
                        new TextBlock { Text = $"{ScanItem.FormatSize(used)} / {ScanItem.FormatSize(d.TotalSize)}",
                            FontSize = 12, Foreground = Secondary,
                            Margin = new Thickness(0, 4, 0, 8) },
                        // Progress bar
                        new Border
                        {
                            Height = 6, CornerRadius = new CornerRadius(3),
                            Background = new SolidColorBrush(Color.FromRgb(0x25, 0x25, 0x4A)),
                            Child = new Border
                            {
                                Width = 180 * (pct / 100), HorizontalAlignment = HorizontalAlignment.Left,
                                CornerRadius = new CornerRadius(3), Background = colorBrush,
                                SnapsToDevicePixels = true
                            }
                        },
                        new TextBlock { Text = $"{pct}% đã dùng", FontSize = 11, FontWeight = FontWeights.SemiBold,
                            Foreground = colorBrush, Margin = new Thickness(0, 6, 0, 0) }
                    }
                }
            };
            DriveCards.Items.Add(card);
        }

        // Health score
        TxtScore.Text = $"{snap.HealthScore:F0}";
        var scoreColor = snap.HealthScore >= 80
            ? Color.FromRgb(0x7E, 0xCF, 0x6A)
            : snap.HealthScore >= 60
                ? Color.FromRgb(0xDD, 0xAF, 0x68)
                : Color.FromRgb(0xE7, 0x6F, 0x80);
        TxtScore.Foreground = new SolidColorBrush(scoreColor);
        TxtHealthLabel.Text = snap.HealthScore >= 80 ? "Tốt" : snap.HealthScore >= 60 ? "Trung bình" : "Kém";
        TxtHealthLabel.Foreground = TxtScore.Foreground;

        // Ring donut — show disk % or health
        var ringPct = (int)snap.DiskPercent;
        TxtRingValue.Text = $"{ringPct}";
        HealthRingEllipse.Stroke = ringPct >= 90
            ? new SolidColorBrush(Color.FromRgb(0xE7, 0x6F, 0x80))
            : ringPct >= 70
                ? new SolidColorBrush(Color.FromRgb(0xE0, 0x80, 0x50))
                : new SolidColorBrush(Color.FromRgb(0x7E, 0xCF, 0x6A));
        // Circumference = π * d ≈ 220; dash = (pct/100) * 220
        var dashLen = ringPct / 100.0 * 220.0;
        HealthRingEllipse.StrokeDashArray = [dashLen, 220 - dashLen];

        TxtTotalFreed.Text = stats.TotalSpaceFreedFormatted;
        TxtQuarantinedDash.Text = "0 items"; // will be updated below

        // Cleanup Potential banner — gradient background + content synced with health score
        try
        {
            var estimate = await Task.Run(EstimateCleanableSpace);

            if (snap.HealthScore < 60)
            {
                // Kém: banner đỏ
                BannerGrad0.Color = Color.FromRgb(0x2A, 0x08, 0x08);
                BannerGrad1.Color = Color.FromRgb(0x40, 0x10, 0x10);
                BannerBorderColor.Color = Color.FromRgb(0xE7, 0x6F, 0x80);
                CleanupPotentialBannerIcon.Background = new SolidColorBrush(Color.FromRgb(0x40, 0x10, 0x18));
                CleanupPotentialBannerIconText.Text = "⚠️";
                TxtCleanableEstimate.Foreground = Danger;
                TxtCleanableEstimate.Text = estimate > 50_000_000
                    ? $"⚠️ Cần tối ưu hệ thống — có thể giải phóng ~{ScanItem.FormatSize(estimate)}"
                    : "⚠️ Điểm sức khỏe thấp — kiểm tra cảnh báo bên dưới";
                TxtCleanableDetail.Text = "Ổ đĩa hệ thống sắp đầy và hiệu năng RAM đang ở mức cao.";
            }
            else if (snap.HealthScore < 80 || estimate > 50_000_000)
            {
                // Trung bình: banner vàng cam (giống ảnh)
                BannerGrad0.Color = Color.FromRgb(0x2A, 0x1E, 0x08);
                BannerGrad1.Color = Color.FromRgb(0x3A, 0x28, 0x06);
                BannerBorderColor.Color = Color.FromRgb(0xDD, 0xAF, 0x68);
                CleanupPotentialBannerIcon.Background = new SolidColorBrush(Color.FromRgb(0x3A, 0x2A, 0x06));
                CleanupPotentialBannerIconText.Text = "⚡";
                TxtCleanableEstimate.Foreground = Warning;
                TxtCleanableEstimate.Text = estimate > 50_000_000
                    ? $"⚡ Cần tối ưu hệ thống — có thể giải phóng ~{ScanItem.FormatSize(estimate)}"
                    : "⚡ Hệ thống hoạt động ổn — còn có thể tối ưu thêm";
                TxtCleanableDetail.Text = "Ổ đĩa hệ thống sắp đầy và hiệu năng RAM đang ở mức cao.";
            }
            else
            {
                // Tốt: banner xanh
                BannerGrad0.Color = Color.FromRgb(0x08, 0x1E, 0x12);
                BannerGrad1.Color = Color.FromRgb(0x0A, 0x28, 0x18);
                BannerBorderColor.Color = Color.FromRgb(0x7E, 0xCF, 0x6A);
                CleanupPotentialBannerIcon.Background = new SolidColorBrush(Color.FromRgb(0x18, 0x34, 0x28));
                CleanupPotentialBannerIconText.Text = "✅";
                TxtCleanableEstimate.Foreground = Success;
                TxtCleanableEstimate.Text = "✅ Hệ thống đang rất sạch sẽ!";
                TxtCleanableDetail.Text = "Không tìm thấy nhiều rác. Tiếp tục duy trì tốt.";
            }
        }
        catch { TxtCleanableEstimate.Text = "Đang ước tính..."; }

        var qItems = await _storage.GetQuarantineItemsAsync();
        var activeQ = qItems.Where(q => q.Status == QuarantineStatus.Active).ToList();
        TxtQuarantinedDash.Text = $"{activeQ.Count} items";

        // Issues with severity badges
        IssuesList.Items.Clear();
        foreach (var issue in snap.Recommendations)
            IssuesList.Items.Add(new TextBlock
            {
                Text = $"  {issue}",
                FontSize = 12,
                Foreground = new SolidColorBrush(Color.FromRgb(0xC8, 0xCC, 0xE8)),
                Margin = new Thickness(0, 3, 0, 3),
                TextWrapping = TextWrapping.Wrap
            });

        // Recent activity with timestamps
        RecentList.Items.Clear();
        var cleans = await _storage.GetCleanHistoryAsync(7);
        foreach (var c in cleans.Take(8))
            RecentList.Items.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(0x16, 0x16, 0x40)),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 0, 4),
                Child = new StackPanel
                {
                    Children =
                    {
                        new TextBlock
                        {
                            Text = $"{c.CleanLevel} — {c.ItemsCleaned} mục",
                            FontSize = 12, FontWeight = FontWeights.SemiBold,
                            Foreground = Primary
                        },
                        new TextBlock
                        {
                            Text = $"{c.CleanDate:dd/MM/yyyy HH:mm} · Giải phóng {c.SpaceFreedFormatted}",
                            FontSize = 10.5,
                            Foreground = Secondary,
                            Margin = new Thickness(0, 2, 0, 0)
                        }
                    }
                }
            });

        if (RecentList.Items.Count == 0)
            RecentList.Items.Add(new TextBlock
            {
                Text = "Chưa có hoạt động nào. Hãy chạy quét đầu tiên!",
                FontSize = 12, Foreground = Muted,
                FontStyle = FontStyles.Italic, Margin = new Thickness(0, 4, 0, 0)
            });

        // SMART disk health (run in background thread to prevent blocking UI)
        try
        {
            var smartResults = await Task.Run(SmartDiskChecker.CheckAllDrives);
            if (smartResults.Count > 0)
            {
                RecentList.Items.Add(new TextBlock
                {
                    Text = " SSD/HDD Health (SMART)",
                    FontSize = 12, FontWeight = FontWeights.Bold,
                    Foreground = Accent,
                    Margin = new Thickness(0, 8, 0, 2)
                });
                foreach (var s in smartResults)
                    RecentList.Items.Add(new TextBlock
                    {
                        Text = $"  {s.Status} {s.DriveModel}: {s.HealthSummary}",
                        FontSize = 11.5,
                        Foreground = s.PredictFailure
                            ? new SolidColorBrush(Color.FromRgb(0xE7, 0x6F, 0x80))
                            : new SolidColorBrush(Color.FromRgb(0x7E, 0xCF, 0x6A)),
                        Margin = new Thickness(0, 2, 0, 2)
                    });
            }
        }
        catch { /* SMART not available */ }

        // Status bar
        try
        {
            StatusCpu.Text = $"CPU {snap.CpuPercent:F0}%";
            StatusRam.Text = $"RAM {snap.MemoryPercent:F0}%";
            StatusDisk.Text = $"Disk {snap.DiskPercent:F0}%";
        }
        catch { }

        // Refresh ViewModel in background
        _ = _dashboardVM?.RefreshAsync();

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

        // Hủy tác vụ scan đang chạy khi chuyển trang
        if (_cts != null && !_cts.IsCancellationRequested)
        {
            try { _cts.Cancel(); } catch { }
            _cts.Dispose();
            _cts = null;
        }
        HideLoading();

        _currentPage = tag;

        // Reset all nav button highlights
        foreach (var b in _navButtons)
            b.ClearValue(BorderBrushProperty);
        btn.SetValue(BorderBrushProperty, new SolidColorBrush(Color.FromRgb(0x6C, 0x8C, 0xF0)));

        bool showDash = tag == "Dashboard";
        DashboardView.Visibility = showDash ? Visibility.Visible : Visibility.Collapsed;
        PageView.Visibility = showDash ? Visibility.Collapsed : Visibility.Visible;

        var navFade = new System.Windows.Media.Animation.DoubleAnimation
        {
            From = 0.5,
            To = 1.0,
            Duration = TimeSpan.FromMilliseconds(200),
            EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut }
        };

        if (showDash)
        {
            DashboardView.BeginAnimation(UIElement.OpacityProperty, navFade);
            _ = RefreshDashboardAsync().ContinueWith(t =>
            {
                if (t.IsFaulted && t.Exception != null)
                    App.Log.Error(t.Exception, "Dashboard refresh failed from Nav_Click");
            }, TaskContinuationOptions.OnlyOnFaulted);
            return;
        }

        PageView.BeginAnimation(UIElement.OpacityProperty, navFade);

        // Cleanup resources from previous page
        if (PagePanel.Tag is System.Windows.Threading.DispatcherTimer oldTimer)
        {
            oldTimer.Stop();
        }
        else if (PagePanel.Tag is Action cleanupAction)
        {
            try { cleanupAction(); } catch { }
        }
        PagePanel.Tag = null;

        PagePanel.Children.Clear();

        PagePanel.Children.Add(new TextBlock
        {
            Text = GetPageTitle(tag),
            FontSize = 22, FontWeight = FontWeights.Bold,
            Foreground = Primary, Margin = new Thickness(0, 0, 0, 4)
        });

        switch (tag)
        {
            case "Disk": StopPerfTimer(); BuildDiskPage(); break;
            case "Cleaner": StopPerfTimer(); BuildCleanerPage(); break;
            case "FileTools": StopPerfTimer(); BuildFileToolsPage(); break;
            case "DevTools": StopPerfTimer(); BuildDevToolsPage(); break;
            case "Performance": BuildPerformancePage(); StartPerfTimer(); break;
            case "Quarantine": StopPerfTimer(); BuildQuarantinePage(); break;
            case "AutoClean": StopPerfTimer(); BuildAutoCleanPage(); break;
            case "Rules": case "Community": case "About": case "Settings":
                StopPerfTimer(); BuildSettingsPage(); break;
        }

        // Fade-in animation for new page content
        PagePanel.Opacity = 0;
        var fadeIn = new System.Windows.Media.Animation.DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        { EasingFunction = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut } };
        PagePanel.BeginAnimation(UIElement.OpacityProperty, fadeIn);
    }

    private static string GetPageTitle(string tag) => tag switch
    {
        "Disk"        => "📊  Phân Tích Ổ Đĩa",
        "Cleaner"     => "🧹  Dọn Dẹp",
        "FileTools"   => "📁  Công Cụ File",
        "DevTools"    => "⚙️  Dev Tools",
        "Performance" => "⚡  Hiệu Năng Hệ Thống",
        "Quarantine"  => "🛡  Cách Ly & Khôi Phục",
        "AutoClean"   => "🕐  Tự Động & Lịch",
        "Rules"       => "📋  Bộ Quy Tắc",
        "Community"   => "📦  Rule Packs Cộng Đồng",
        "About"       => "ℹ️  Giới Thiệu",
        _             => tag
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

    /// <summary>Guarantee loading overlay hides — call in finally blocks.</summary>
    private void SafeHideLoading()
    {
        if (LoadingOverlay.Visibility == Visibility.Visible)
        {
            _isScanning = false;
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    /// <summary>
    /// Execute a scan operation with guaranteed loading overlay cleanup.
    /// Prevents stuck "spinning" state when errors occur.
    /// </summary>
    private async Task SafeScanAsync(Func<Task> scanAction)
    {
        try
        {
            await scanAction();
        }
        catch (OperationCanceledException)
        {
            SafeHideLoading();
        }
        catch (Exception)
        {
            SafeHideLoading();
            throw;
        }
        finally
        {
            SafeHideLoading();
        }
    }

    // ========== UI Helpers ==========

    private static SolidColorBrush C(byte r, byte g, byte b) => new(Color.FromRgb(r, g, b));
    private SolidColorBrush Primary => C(0xDD, 0xE0, 0xF5);
    private SolidColorBrush Secondary => C(0x98, 0x9A, 0xB8);
    private SolidColorBrush Muted => C(0x5A, 0x5C, 0x80);
    private SolidColorBrush Accent => C(0x6C, 0x8C, 0xF0);
    private SolidColorBrush Success => C(0x7E, 0xCF, 0x6A);
    private SolidColorBrush Warning => C(0xDD, 0xAF, 0x68);
    private SolidColorBrush Danger => C(0xE7, 0x6F, 0x80);
    private SolidColorBrush CardBg => C(0x12, 0x12, 0x2A);
    private SolidColorBrush CardBorder => C(0x25, 0x25, 0x4A);
    private SolidColorBrush SurfaceAlt => C(0x19, 0x19, 0x3A);

    private Style? PrimaryBtn => FindResource("PrimaryBtn") as Style;
    private Style? SecondaryBtn => FindResource("SecondaryBtn") as Style;
    private Style? SuccessBtn => FindResource("SuccessBtn") as Style;
    private Style? DangerBtn => FindResource("DangerBtn") as Style;
    private Style? PageSubtitle => FindResource("PageSubtitle") as Style;
    private Style? StatusText => FindResource("StatusText") as Style;

    private Border StyledCard(object? child = null, Thickness? margin = null, Thickness? padding = null)
    {
        var card = new Border
        {
            Style = FindResource("GlassCard") as Style,
            Padding = padding ?? new(14),
            Margin = margin ?? new(0),
            SnapsToDevicePixels = true
        };
        if (child != null) card.Child = child as UIElement ?? new TextBlock { Text = child.ToString() };
        return card;
    }

    private Button MakeBtn(string text, Style? style = null, double? width = null, Thickness? margin = null)
    {
        var btn = new Button { Content = text, Style = style ?? FindResource("BtnPrimary") as Style ?? PrimaryBtn };
        if (width.HasValue) btn.Width = width.Value;
        if (margin != null) btn.Margin = margin.Value;
        return btn;
    }

    /// <summary>Card with hover animation — subtle scale/brightness on hover.</summary>
    private Border HoverCard(Thickness? padding = null, Thickness? margin = null)
    {
        var card = new Border
        {
            Style = FindResource("GlassCardHover") as Style,
            Padding = padding ?? new(14),
            Margin = margin ?? new(0),
            SnapsToDevicePixels = true
        };
        return card;
    }

    private TextBlock Label(string text, SolidColorBrush? color = null, double size = 12, FontWeight? weight = null)
        => new() { Text = text, FontSize = size, Foreground = color ?? Secondary, FontWeight = weight ?? FontWeights.Normal };

    private TextBlock SectionTitle(string text) => new()
    { Text = text, FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Primary, Margin = new(0, 0, 0, 6) };

    // ========== Page Builders ==========

    private void BuildDiskPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Phân tích dung lượng ổ đĩa — hiển thị trực quan dạng treemap, hỗ trợ cách ly & xóa.",
            Foreground = Secondary, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 18)
        });

        // Control bar
        var cardHeader = StyledCard(padding: new Thickness(16, 12, 16, 12), margin: new Thickness(0, 0, 0, 16));
        var drivesPanel = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };
        var driveCombo = new ComboBox { Width = 100, Margin = new Thickness(0, 0, 12, 0), Style = FindResource("ModernComboBox") as Style };
        foreach (var d in System.IO.DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed))
            driveCombo.Items.Add(d.Name.TrimEnd('\\'));
        driveCombo.SelectedIndex = 0;
        drivesPanel.Children.Add(driveCombo);

        var scanBtn = new Button { Content = "⚡ Quét Ổ Đĩa", Width = 120, Margin = new Thickness(0, 0, 10, 0), Style = FindResource("BtnPrimary") as Style ?? PrimaryBtn };
        var cancelBtn = new Button { Content = "⏹ Hủy Quét", Width = 100, IsEnabled = false, Style = FindResource("BtnSecondary") as Style ?? SecondaryBtn };
        drivesPanel.Children.Add(scanBtn);
        drivesPanel.Children.Add(cancelBtn);
        cardHeader.Child = drivesPanel;
        panel.Children.Add(cardHeader);

        var statusLabel = new TextBlock
        {
            Text = "Sẵn sàng quét đĩa.",
            Style = StatusText
        };
        panel.Children.Add(statusLabel);

        var resultsList = new ListBox
        {
            MaxHeight = 220,
            Style = FindResource("ModernListBox") as Style,
            Background = CardBg,
            Foreground = Primary
        };
        panel.Children.Add(resultsList);

        // Treemap Visualizer
        var treemap = new TreemapControl { Height = 290, Margin = new Thickness(0, 14, 0, 0) };
        panel.Children.Add(treemap);

        // Store scan results for actions
        List<ScanItem>? _scanResults = null;

        // Action buttons dock bar (hidden until scan completes)
        var actionCard = new Border
        {
            Style = FindResource("GlassCard") as Style,
            Padding = new Thickness(16, 10, 16, 10),
            Margin = new Thickness(0, 14, 0, 0),
            Visibility = Visibility.Collapsed
        };
        var actionPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };
        var quarantineBtn = new Button { Content = "📦 Cách Ly Mục Chọn", Width = 170, Margin = new Thickness(0, 0, 10, 0), Style = FindResource("BtnPrimary") as Style ?? PrimaryBtn };
        var deleteBtn = new Button { Content = "🗑️ Xóa Mục Chọn", Width = 150, Margin = new Thickness(0, 0, 10, 0), Style = FindResource("BtnDanger") as Style ?? DangerBtn };
        var openBtn = new Button { Content = "📁 Mở Thư Mục", Width = 130, Style = FindResource("BtnSecondary") as Style ?? SecondaryBtn };
        actionPanel.Children.Add(quarantineBtn);
        actionPanel.Children.Add(deleteBtn);
        actionPanel.Children.Add(openBtn);
        actionCard.Child = actionPanel;
        panel.Children.Add(actionCard);

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
                statusLabel.Text = $"Tìm thấy {items.Count} mục. Tổng: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))}";
                treemap.Items = items.Where(i => i.SizeBytes > 0).OrderByDescending(i => i.SizeBytes).Take(100).ToList();
                _scanResults = items;
                actionCard.Visibility = Visibility.Visible;
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
            if (!Directory.Exists(item.Path) && !File.Exists(item.Path)) return;

            var (action, risk, _) = _ruleEngine.Evaluate(item.Path, item.SizeBytes);
            if (action == ItemAction.Block)
            {
                MessageBox.Show($"Bị chặn: Quy tắc bảo vệ hệ thống không cho phép cách ly mục này.", "Đã Bị Chặn", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var qDir = Path.Combine(_quarantineDir, $"{Guid.NewGuid():N}_{item.Name}");
            try
            {
                if (Directory.Exists(item.Path)) Directory.Move(item.Path, qDir);
                else if (File.Exists(item.Path)) File.Move(item.Path, qDir);

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
                statusLabel.Text = $"📦 Đã cách ly: {item.Name} ({item.SizeFormatted}) — Có thể khôi phục tại trang Cách Ly trong 14 ngày";
                await RefreshDashboardAsync();
            }
            catch (Exception ex) { statusLabel.Text = $"Lỗi cách ly: {ex.Message}"; }
        };

        // Delete selected folder (permanently)
        deleteBtn.Click += async (_, _) =>
        {
            if (resultsList.SelectedIndex < 0 || _scanResults == null ||
                resultsList.SelectedIndex >= Math.Min(_scanResults.Count, 30)) return;
            var item = _scanResults[resultsList.SelectedIndex];
            if (!Directory.Exists(item.Path) && !File.Exists(item.Path)) return;

            var (action, risk, _) = _ruleEngine.Evaluate(item.Path, item.SizeBytes);
            if (action == ItemAction.Block)
            {
                MessageBox.Show($"Bị chặn: Quy tắc bảo vệ không cho phép xóa mục này.", "Đã Bị Chặn", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"⚠️ Xác nhận xóa vĩnh viễn:\n\n{item.Name}\nDung lượng: {item.SizeFormatted}\n\nĐường dẫn: {item.Path}\n\nHành động này không thể hoàn tác!",
                "Xác Nhận Xóa Vĩnh Viễn", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                if (Directory.Exists(item.Path)) Directory.Delete(item.Path, true);
                else if (File.Exists(item.Path)) File.Delete(item.Path);
                statusLabel.Text = $"🗑️ Đã xóa: {item.Name} ({item.SizeFormatted})";
                await _storage.SaveCleanHistoryAsync(new CleanHistory
                {
                    CleanDate = DateTime.Now,
                    CleanLevel = CleanLevel.Deep,
                    ItemsCleaned = 1,
                    SpaceFreedBytes = item.SizeBytes
                });
                await RefreshDashboardAsync();
            }
            catch (Exception ex) { statusLabel.Text = $"Lỗi xóa: {ex.Message}"; }
        };

        openBtn.Click += (_, _) =>
        {
            if (resultsList.SelectedIndex < 0 || _scanResults == null ||
                resultsList.SelectedIndex >= Math.Min(_scanResults.Count, 30)) return;
            var item = _scanResults[resultsList.SelectedIndex];
            try
            {
                if (Directory.Exists(item.Path))
                    System.Diagnostics.Process.Start("explorer.exe", item.Path);
                else if (File.Exists(item.Path))
                    System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{item.Path}\"");
            }
            catch (Exception ex) { statusLabel.Text = $"Lỗi mở thư mục: {ex.Message}"; }
        };

        cancelBtn.Click += (_, _) =>
        {
            _cts?.Cancel();
            statusLabel.Text = "Đang hủy...";
        };
    }

    private void BuildCleanerPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Dọn file rác an toàn nhiều lớp bảo vệ. Dọn Nhanh: temp/logs/thùng rác. Dọn Sâu: file sót, file cũ lớn.",
            Foreground = Secondary, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 18)
        });

        // Mode Card Bar
        var modeCard = StyledCard(padding: new Thickness(16, 12, 16, 12), margin: new Thickness(0, 0, 0, 16));
        var btns = new StackPanel { Orientation = Orientation.Horizontal };
        var quickBtn = new Button { Content = "⚡ Dọn Nhanh", Width = 130, Margin = new Thickness(0, 0, 10, 0), Style = FindResource("BtnSuccess") as Style ?? SuccessBtn };
        var deepBtn  = new Button { Content = "🧹 Dọn Sâu",  Width = 130, Margin = new Thickness(0, 0, 10, 0), Style = FindResource("BtnPrimary") as Style ?? PrimaryBtn };
        var previewBtn = new Button { Content = "👁 Xem Trước", Width = 120, Margin = new Thickness(0, 0, 10, 0), Style = FindResource("BtnSecondary") as Style ?? SecondaryBtn };
        btns.Children.Add(quickBtn); btns.Children.Add(deepBtn); btns.Children.Add(previewBtn);
        modeCard.Child = btns;
        panel.Children.Add(modeCard);

        var resultLabel = new TextBlock
        {
            Text = "Chọn chế độ dọn để bắt đầu.",
            Foreground = Secondary,
            Margin = new(0, 0, 0, 10)
        };
        panel.Children.Add(resultLabel);

        var resultsList = new ListBox
        {
            MaxHeight = 360,
            Style = FindResource("ModernListBox") as Style,
            Background = CardBg,
            Foreground = Primary
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

            resultLabel.Text = $"✅ Hoàn tất! Giải phóng {ScanItem.FormatSize(freed)}. Đã dọn {processed} mục. Lỗi: {errors.Count}";
            resultsList.Items.Add($"Giải phóng: {ScanItem.FormatSize(freed)}");
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
            var orphanScanner = new OrphanDetector();
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

            resultLabel.Text = $"Tìm thấy {deepItems.Count} mục. Hãy xem xét kỹ trước khi dọn.";
            foreach (var item in deepItems)
                resultsList.Items.Add(
                    $"  [{item.Risk}] {item.Category}: {item.Name} ({item.SizeFormatted}) - {item.Suggestion}");

            // Add a confirm clean button
            var confirmBtn = new Button
            {
                Content = $"⚠️ Dọn {deepItems.Count} Mục Đã Chọn",
                Width = 180,
                Margin = new Thickness(0, 12, 0, 0)
            };
            confirmBtn.Style = DangerBtn;
            panel.Children.Add(confirmBtn);

            confirmBtn.Click += async (_, _2) =>
            {
                confirmBtn.IsEnabled = false;
                var cleaner = new DeepCleaner(_ruleEngine, _riskEngine, _storage);
                var (freed, processed, errors) = await cleaner.CleanAsync(deepItems,
                    new Progress<string>(s => resultLabel.Text = s), _cts!.Token);
                resultLabel.Text = $"✅ Dọn sâu hoàn tất! Giải phóng {ScanItem.FormatSize(freed)}. {processed} mục. Đã cách ly các mục rủi ro cao.";
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
            resultLabel.Text = " 👁 XEM TRƯỚC — Đang quét...";
            ShowLoading("Đang xem trước (không có file nào bị xóa)...");

            _cts = new CancellationTokenSource();
            var scanner = new DiskScanner(_ruleEngine, _riskEngine);
            var drives = System.IO.DriveInfo.GetDrives()
                .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
                .Select(d => d.Name).ToList();

            var progress = new Progress<(string Status, int Progress)>(p =>
            {
                Dispatcher.Invoke(() => { resultLabel.Text = " 👁 XEM TRƯỚC — " + p.Status; ScanProgress.Value = p.Progress; });
            });
            var items = await scanner.ScanAsync(drives, progress, _cts.Token);
            HideLoading();

            // Categorize for preview
            var safeItems = items.Where(i => i.Risk <= RiskLevel.Low).ToList();
            var warnItems = items.Where(i => i.Risk == RiskLevel.Medium).ToList();
            var blockedItems = items.Where(i => i.Risk >= RiskLevel.High || i.RecommendedAction == ItemAction.Block).ToList();

            resultsList.Items.Add($"━━━ 👁 XEM TRƯỚC (DRY RUN) — Không có file nào bị xóa ━━━");
            resultsList.Items.Add($"");
            resultsList.Items.Add($"✅ An toàn: {safeItems.Count} mục ({ScanItem.FormatSize(safeItems.Sum(i => i.SizeBytes))})");
            resultsList.Items.Add($"⚠️ Cần xem lại: {warnItems.Count} mục ({ScanItem.FormatSize(warnItems.Sum(i => i.SizeBytes))})");
            resultsList.Items.Add($"🔒 Bị chặn bởi quy tắc: {blockedItems.Count} mục ({ScanItem.FormatSize(blockedItems.Sum(i => i.SizeBytes))})");
            resultsList.Items.Add($"");
            resultsList.Items.Add($" Tổng khả năng dọn: {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))} trên {items.Count} mục");
            resultsList.Items.Add($"");
            resultsList.Items.Add($" Chạy Dọn Nhanh hoặc Dọn Sâu để thực hiện xóa thực sự.");

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
            Text = "Tìm file lớn (>100 MB) đang chiếm nhiều dung lượng ổ đĩa.",
            Foreground = Secondary, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 18)
        });
        var scanBtn = new Button { Content = "🔍 Tìm File Lớn", Width = 150, Margin = new(0, 0, 0, 12) };
        scanBtn.Style = PrimaryBtn;
        panel.Children.Add(scanBtn);

        var statusLabel = new TextBlock { Text = "Sẵn sàng.", Foreground = Secondary, Margin = new(0, 0, 0, 8) };
        panel.Children.Add(statusLabel);

        var results = new ListBox { MaxHeight = 400, Style = FindResource("ModernListBox") as Style, Background = CardBg, Foreground = Primary };
        panel.Children.Add(results);

        scanBtn.Click += async (_, _) =>
        {
            results.Items.Clear();
            scanBtn.IsEnabled = false;
            statusLabel.Text = "Searching for files > 100MB...";
            ScanProgress.IsIndeterminate = false;
            ShowLoading("Đang tìm file lớn...");

            _cts = new CancellationTokenSource();
            var finder = new LargeFileFinder();
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
            Style = PageSubtitle
        });
        var scanBtn = new Button { Content = " Scan for Orphans", Width = 150, Margin = new(0, 0, 0, 10) };
        scanBtn.Style = PrimaryBtn;
        panel.Children.Add(scanBtn);

        var statusLabel = new TextBlock
        {
            Text = "Sẵn sàng.",
            Foreground = Secondary
        };
        panel.Children.Add(statusLabel);

        var results = new ListBox
        {
            MaxHeight = 400,
            Background = CardBg,
            Foreground = Primary
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
            var detector = new OrphanDetector();
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
            Style = PageSubtitle
        });
        var scanBtn = new Button { Content = " Find Duplicates", Width = 140, Margin = new(0, 0, 0, 10) };
        scanBtn.Style = PrimaryBtn;
        panel.Children.Add(scanBtn);

        var statusLabel = new TextBlock
        {
            Text = "Sẵn sàng.",
            Foreground = Secondary
        };
        panel.Children.Add(statusLabel);

        var results = new ListBox
        {
            MaxHeight = 400,
            Background = CardBg,
            Foreground = Primary
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
            var finder = new DuplicateFinder();
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
        panel.Children.Add(new TextBlock { Text = "Dọn cache lập trình: node_modules, build, .next, __pycache__, target, gradle, ...", Style = PageSubtitle });

        var pathPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 12) };
        var pathBox = new TextBox { Text = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), Width = 340, Margin = new Thickness(0, 0, 6, 0), Style = FindResource("ModernTextBox") as Style };
        pathPanel.Children.Add(pathBox);
        var browseBtn = new Button { Content = "📂", Width = 34, Height = 34, Margin = new Thickness(0, 0, 6, 0), Style = SecondaryBtn };
        browseBtn.Click += (_, _) =>
        {
            var dlg = new Microsoft.Win32.OpenFolderDialog();
            dlg.DefaultDirectory = pathBox.Text;
            if (dlg.ShowDialog() == true)
                pathBox.Text = dlg.FolderName;
        };
        pathPanel.Children.Add(browseBtn);
        var scanBtn = new Button { Content = "  Quét", Width = 80, Style = PrimaryBtn };
        pathPanel.Children.Add(scanBtn);
        panel.Children.Add(pathPanel);

        var statusLabel = new TextBlock { Style = StatusText };
        var cardPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 12), Visibility = Visibility.Collapsed };
        panel.Children.Add(cardPanel);

        var filterPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10), Visibility = Visibility.Collapsed };
        var ageFilter = new CheckBox { Content = "Cache cũ > 30 ngày", Foreground = Secondary, IsChecked = true, Margin = new(0, 0, 16, 0) };
        var sizeFilter = new CheckBox { Content = "Chỉ > 50 MB", Foreground = Secondary, Margin = new(0, 0, 16, 0) };
        filterPanel.Children.Add(ageFilter);
        filterPanel.Children.Add(sizeFilter);
        panel.Children.Add(filterPanel);
        panel.Children.Add(statusLabel);

        var results = new ListBox { MaxHeight = 340, Style = FindResource("ModernListBox") as Style };
        panel.Children.Add(results);

        var actionPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed };
        var toggleBtn = new Button { Content = "Chọn tất cả", Width = 110, Margin = new Thickness(0, 0, 10, 0), Style = SecondaryBtn };
        var totalText = new TextBlock { VerticalAlignment = VerticalAlignment.Center, FontSize = 12, Foreground = FindResource("TextSecondary") as Brush ?? Brushes.Gray };
        var cleanBtn = new Button { Content = " Dọn đã chọn", Width = 140, Margin = new Thickness(8, 0, 0, 0), IsEnabled = false, Style = SuccessBtn };
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
                var card = new Border
                {
                    Background = CardBg,
                    BorderBrush = CardBorder,
                    BorderThickness = new(1),
                    CornerRadius = new(8),
                    Padding = new(10, 8, 10, 8),
                    Margin = new(0, 0, 8, 8),
                    Width = 145
                };
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
                    var cb = new CheckBox { IsChecked = true, VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(0, 0, 10, 0) };
                    cb.Checked += (_, _) => { selectedPaths.Add(p); UpdateDevSummary(currentFiltered, totalText, cleanBtn, selectedPaths); };
                    cb.Unchecked += (_, _) => { selectedPaths.Remove(p); UpdateDevSummary(currentFiltered, totalText, cleanBtn, selectedPaths); };
                    Grid.SetColumn(cb, 0); g2.Children.Add(cb);
                    var info = new StackPanel();
                    info.Children.Add(new TextBlock { Text = $"{item.Name}  ·  {item.SizeFormatted}", FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Primary });
                    info.Children.Add(new TextBlock { Text = $"{item.Type}  ·  {item.AgeText}  ·  {item.Path}", FontSize = 11, Foreground = Secondary, Margin = new(0, 1, 0, 0) });
                    Grid.SetColumn(info, 1); g2.Children.Add(info);
                    var badge = new Border { CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 2, 6, 2), VerticalAlignment = VerticalAlignment.Center, Background = item.IsSafe ? new SolidColorBrush(Color.FromRgb(0x1A, 0x34, 0x28)) : new SolidColorBrush(Color.FromRgb(0x34, 0x28, 0x20)) };
                    badge.Child = new TextBlock { Text = item.IsSafe ? "An toàn" : "Thận trọng", FontSize = 10.5, FontWeight = FontWeights.SemiBold, Foreground = item.IsSafe ? new SolidColorBrush(Color.FromRgb(0x7E, 0xCF, 0x6A)) : new SolidColorBrush(Color.FromRgb(0xDD, 0xAF, 0x68)) };
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


    // ========== Package Cache Analyzer Page ==========

    private void BuildPackageCachePage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Phân tích cache của các Trình quản lý gói — npm, pip, NuGet, Go, Docker, Gradle...",
            FontSize = 13, Foreground = Secondary, Margin = new(0, 0, 0, 12)
        });

        var scanBtn = MakeBtn("⚡ Quét Package Caches", PrimaryBtn, 200, new(0, 0, 10, 0));
        panel.Children.Add(scanBtn);

        var statusLabel = Label("Sẵn sàng. Nhấn Quét để phân tích.", Secondary);
        panel.Children.Add(statusLabel);

        var resultsCard = StyledCard(padding: new(12), margin: new(0, 12, 0, 0));
        var resultsStack = new StackPanel();
        resultsCard.Child = resultsStack;
        panel.Children.Add(resultsCard);

        var summaryLabel = Label("", Muted, 11);
        panel.Children.Add(summaryLabel);

        List<ScanItem>? _cacheResults = null;

        scanBtn.Click += async (_, _) =>
        {
            scanBtn.IsEnabled = false;
            resultsStack.Children.Clear();
            ShowLoading("Đang phân tích package caches...");

            try
            {
                var scanner = new DevCacheScanner();
                var items = await scanner.ScanAsync(["C:"], new Progress<(string, int)>(p =>
                {
                    Dispatcher.Invoke(() => { statusLabel.Text = p.Item1; ScanProgress.Value = p.Item2; });
                }));

                HideLoading();
                _cacheResults = items;

                if (items.Count == 0)
                {
                    statusLabel.Text = "Không tìm thấy package cache nào đáng kể.";
                    scanBtn.IsEnabled = true;
                    return;
                }

                long total = items.Sum(i => i.SizeBytes);
                statusLabel.Text = $"Tìm thấy {items.Count} package caches ({ScanItem.FormatSize(total)})";
                summaryLabel.Text = $"Tổng có thể giải phóng: {ScanItem.FormatSize(total)} từ {items.Count} bộ nhớ tạm";

                foreach (var item in items)
                {
                    var card = StyledCard(padding: new(12, 8, 12, 8), margin: new(0, 0, 0, 4));
                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock
                    {
                        Text = $" {item.Name,-25}  {item.SizeFormatted,12}",
                        FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Success,
                        FontFamily = new System.Windows.Media.FontFamily("Consolas")
                    });
                    stack.Children.Add(new TextBlock
                    {
                        Text = $"    {item.Suggestion}",
                        FontSize = 11, Foreground = Secondary, Margin = new(0, 3, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    });
                    card.Child = stack;
                    resultsStack.Children.Add(card);
                }
            }
            catch (Exception ex) { HideLoading(); statusLabel.Text = $"Lỗi: {ex.Message}"; }
            finally { scanBtn.IsEnabled = true; }
        };
    }

    // ========== Stale Project Detector Page ==========

    private void BuildStaleProjectsPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Phát hiện project cũ — quét lịch sử git và tìm bộ nhớ tạm build (bin/obj, node_modules) có thể dọn.",
            FontSize = 13, Foreground = Secondary, Margin = new(0, 0, 0, 12)
        });

        var scanBtn = MakeBtn("⚡ Quét Projects", PrimaryBtn, 180, new(0, 0, 10, 0));
        panel.Children.Add(scanBtn);

        var statusLabel = Label("Sẵn sàng. Quét ~/source, ~/projects, ~/dev, GitHub...", Secondary);
        panel.Children.Add(statusLabel);

        var resultsCard = StyledCard(padding: new(12), margin: new(0, 12, 0, 0));
        var resultsStack = new StackPanel();
        resultsCard.Child = resultsStack;
        panel.Children.Add(resultsCard);

        var summaryLabel = Label("", Muted, 11, FontWeights.SemiBold);
        panel.Children.Add(summaryLabel);

        List<ScanItem>? _projectResults = null;

        scanBtn.Click += async (_, _) =>
        {
            scanBtn.IsEnabled = false;
            resultsStack.Children.Clear();
            ShowLoading("Đang quét thư mục project...");

            try
            {
                var scanner = new StaleProjectDetector();
                var items = await scanner.ScanAsync(["C:"], new Progress<(string, int)>(p =>
                {
                    Dispatcher.Invoke(() => { statusLabel.Text = p.Item1; ScanProgress.Value = p.Item2; });
                }));

                HideLoading();
                _projectResults = items;

                if (items.Count == 0)
                {
                    statusLabel.Text = "Không tìm thấy project nào để dọn.";
                    scanBtn.IsEnabled = true;
                    return;
                }

                int staleCount = items.Count(i => i.Risk <= RiskLevel.Low);
                long total = items.Sum(i => i.SizeBytes);
                statusLabel.Text = $"Tìm thấy {items.Count} projects ({ScanItem.FormatSize(total)} có thể thu hồi)";
                summaryLabel.Text = $"{staleCount} projects không dùng (>60 ngày) - {ScanItem.FormatSize(total)} có thể dọn";

                foreach (var item in items)
                {
                    var isStale = item.Risk <= RiskLevel.Low;
                    var badge = isStale ? "CŨ (>60d)" : "HOẠT ĐỘNG";
                    var badgeColor = isStale ? Danger : Success;

                    var card = StyledCard(padding: new(12, 8, 12, 8), margin: new(0, 0, 0, 4));
                    var stack = new StackPanel();
                    var headerRow = new StackPanel { Orientation = Orientation.Horizontal };
                    headerRow.Children.Add(new TextBlock
                    {
                        Text = $" {badge}",
                        FontSize = 9, FontWeight = FontWeights.Bold, Foreground = badgeColor,
                        Margin = new(0, 0, 8, 0), VerticalAlignment = VerticalAlignment.Center
                    });
                    headerRow.Children.Add(new TextBlock
                    {
                        Text = $"{item.Name}",
                        FontSize = 13, FontWeight = FontWeights.SemiBold, Foreground = Primary
                    });
                    stack.Children.Add(headerRow);

                    if (!string.IsNullOrEmpty(item.AppOrigin))
                        stack.Children.Add(new TextBlock
                        {
                            Text = $"    {item.AppOrigin}  .  {item.SizeFormatted} có thể dọn",
                            FontSize = 11, Foreground = Secondary, Margin = new(0, 3, 0, 0)
                        });

                    stack.Children.Add(new TextBlock
                    {
                        Text = $"    {item.Suggestion}",
                        FontSize = 10.5, Foreground = Muted, Margin = new(0, 3, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    });
                    card.Child = stack;
                    resultsStack.Children.Add(card);
                }
            }
            catch (Exception ex) { HideLoading(); statusLabel.Text = $"Lỗi: {ex.Message}"; }
            finally { scanBtn.IsEnabled = true; }
        };
    }

private async void BuildPerformancePage()
    {
        var panel = PagePanel;
        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 12) };
        var tabPerf = MakeBtn("  Hiệu Năng", PrimaryBtn, 120, new(0, 0, 6, 0));
        var tabDiag = MakeBtn("  Chẩn Đoán", SecondaryBtn, 120, new(0, 0, 6, 0));
        var tabStartup = MakeBtn("  Khởi Động", SecondaryBtn, 120, new(0, 0, 0, 0));
        tabRow.Children.Add(tabPerf); tabRow.Children.Add(tabDiag); tabRow.Children.Add(tabStartup);
        panel.Children.Add(tabRow);
        var perfContent = new StackPanel();
        var diagContent = new StackPanel { Visibility = Visibility.Collapsed };
        var startupContent = new StackPanel { Visibility = Visibility.Collapsed };
        panel.Children.Add(perfContent); panel.Children.Add(diagContent); panel.Children.Add(startupContent);
        tabPerf.Click += (_, _) => { tabPerf.Style = PrimaryBtn; tabDiag.Style = SecondaryBtn; tabStartup.Style = SecondaryBtn; perfContent.Visibility = Visibility.Visible; diagContent.Visibility = Visibility.Collapsed; startupContent.Visibility = Visibility.Collapsed; };
        tabDiag.Click += async (_, _) => { tabPerf.Style = SecondaryBtn; tabDiag.Style = PrimaryBtn; tabStartup.Style = SecondaryBtn;
            perfContent.Visibility = Visibility.Collapsed; diagContent.Visibility = Visibility.Visible; startupContent.Visibility = Visibility.Collapsed;
            if (diagContent.Children.Count == 0) await RunDiagnosticAsync(diagContent); };
        tabStartup.Click += async (_, _) => { tabPerf.Style = SecondaryBtn; tabDiag.Style = SecondaryBtn; tabStartup.Style = PrimaryBtn;
            perfContent.Visibility = Visibility.Collapsed; diagContent.Visibility = Visibility.Collapsed; startupContent.Visibility = Visibility.Visible;
            if (startupContent.Children.Count == 0) await BuildStartupTab(startupContent); };
        await LoadPerfTab(perfContent);
    }
    private void RenderPerformancePage(StackPanel panel, PerformanceSnapshot snap)
    {
        panel.Children.Clear();
        panel.Children.Add(new TextBlock
        {
            Text = "Hiệu Năng Hệ Thống",
            FontSize = 22, FontWeight = FontWeights.Bold,
            Foreground = Primary, Margin = new(0, 0, 0, 4)
        });

        // System overview card
        var overviewCard = StyledCard(padding: new(18));
        var overviewStack = new StackPanel();
        overviewStack.Children.Add(new TextBlock
        {
            Text = $"CPU     {snap.CpuPercent,6:F1}%   |   {snap.CpuCoreCount} nhân",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Accent, Margin = new(0, 0, 0, 6)
        });
        overviewStack.Children.Add(new TextBlock
        {
            Text = $"RAM     {snap.MemoryPercent,6:F0}%   |   {snap.MemoryUsedGB:F1} / {snap.MemoryTotalGB:F1} GB",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Warning, Margin = new(0, 0, 0, 6)
        });
        overviewStack.Children.Add(new TextBlock
        {
            Text = $"Disk     {snap.DiskPercent,6:F0}%   |   {snap.DriveLetter}:  {snap.DiskFreeGB:F1} GB free",
            FontSize = 14, FontWeight = FontWeights.Bold, Foreground = Success, Margin = new(0, 0, 0, 0)
        });
        overviewCard.Child = overviewStack;
        panel.Children.Add(overviewCard);

        // Chart
        var chartCard = StyledCard(padding: new(12), margin: new(0, 12, 0, 0));
        var chart = new PerfChart { Height = 200 };
        chart.AddDataPoint(snap.CpuPercent, snap.MemoryPercent, snap.DiskPercent);
        chartCard.Child = chart;
        panel.Children.Add(chartCard);

        // Timer
        var statsLabel = new TextBlock
        {
            Text = "Live — cập nhật mỗi 2s",
            FontSize = 10.5, Foreground = Muted, Margin = new(0, 6, 0, 12)
        };
        panel.Children.Add(statsLabel);
        var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        timer.Tick += async (_, _) =>
        {
            try
            {
                var s = await _perfAnalyzer.GetSnapshotAsync();
                chart.AddDataPoint(s.CpuPercent, s.MemoryPercent, s.DiskPercent);
                statsLabel.Text = $"Live — CPU {s.CpuPercent:F0}% | RAM {s.MemoryPercent:F0}% | Disk {s.DiskPercent:F0}%";
            }
            catch { }
        };
        timer.Start();
        panel.Tag = timer;

        // Top processes
        panel.Children.Add(SectionTitle("Tiến Trình Ngốn Nhiều RAM Nhất"));
        var procCard = StyledCard(padding: new(10));
        var procStack = new StackPanel();
        foreach (var p in snap.TopProcesses.Take(15))
        {
            var color = p.MemoryMB > 1000 ? Danger : p.MemoryMB > 300 ? Warning : Secondary;
            procStack.Children.Add(new TextBlock
            {
                Text = $"PID {p.Pid,-6}  {p.MemoryMB,7:F0} MB  {p.Name}",
                FontSize = 12, Foreground = color, Margin = new(0, 2, 0, 2),
                FontFamily = new System.Windows.Media.FontFamily("Consolas")
            });
        }
        procCard.Child = procStack;
        panel.Children.Add(procCard);
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
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Primary, Margin = new(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"Tìm thấy {entries.Count} mục khởi động. Tắt bớt để tăng tốc khởi động máy.",
            Foreground = Secondary, FontSize = 12.5, Margin = new(0, 0, 0, 14)
        });

        var entryList = new ListBox
        {
            MaxHeight = 400,
            Style = FindResource("ModernListBox") as Style,
            Background = CardBg,
            Foreground = Primary
        };
        foreach (var e in entries)
        {
            var impact = e.Impact switch { "High" => "🔴", "Medium" => "🟡", _ => "🟢" };
            entryList.Items.Add($"{impact}  [{e.Impact,-6}]  {e.Name}  —  {e.Publisher}  ({e.Location})");
        }
        panel.Children.Add(entryList);

        var disableBtn = new Button { Content = "⏸  Tắt Mục Chọn", Width = 150, Margin = new Thickness(0, 14, 0, 0) };
        disableBtn.Style = DangerBtn;
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
                Foreground = Danger,
                Margin = new Thickness(0, 12, 0, 0)
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
            FontSize = 22,
            FontWeight = FontWeights.Bold,
            Foreground = Primary, Margin = new(0, 0, 0, 4)
        });

        panel.Children.Add(new TextBlock
        {
            Text = $"{active.Count} file đang được cách ly — {ScanItem.FormatSize(active.Sum(i => i.SizeBytes))} tổng. " +
                   "Tự động xóa sau 14 ngày. Bạn có thể khôi phục bất cứ lúc nào.",
            Foreground = Secondary, FontSize = 12.5, Margin = new(0, 0, 0, 18)
        });

        // Summary stats bar
        var statsCard = StyledCard(padding: new Thickness(16, 12, 16, 12), margin: new Thickness(0, 0, 0, 14));
        var statsRow = new StackPanel { Orientation = Orientation.Horizontal };
        void AddStat(string label, string val, SolidColorBrush color)
        {
            var sp = new StackPanel { Margin = new Thickness(0, 0, 32, 0) };
            sp.Children.Add(new TextBlock { Text = val, FontSize = 22, FontWeight = FontWeights.Bold, Foreground = color });
            sp.Children.Add(new TextBlock { Text = label, FontSize = 11, Foreground = Secondary });
            statsRow.Children.Add(sp);
        }
        AddStat("Đang cách ly", $"{active.Count} mục", Warning);
        AddStat("Tổng dung lượng", ScanItem.FormatSize(active.Sum(i => i.SizeBytes)), Danger);
        AddStat("Sẽ xóa sau", "14 ngày", Muted);
        statsCard.Child = statsRow;
        panel.Children.Add(statsCard);

        var qList = new ListBox
        {
            MaxHeight = 350,
            Style = FindResource("ModernListBox") as Style,
            Background = CardBg, Foreground = Primary
        };
        foreach (var q in active)
            qList.Items.Add(
                $"🗂  [{q.DaysRemaining} ngày còn lại]  {q.SizeFormatted,-10}  {q.FileName}  —  {q.Reason}  ({q.Risk})");
        panel.Children.Add(qList);

        var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 14, 0, 0) };
        var restoreBtn = new Button { Content = "↩  Khôi Phục Mục Chọn", Width = 170, Margin = new Thickness(0, 0, 10, 0) };
        restoreBtn.Style = SuccessBtn;
        var deleteBtn = new Button { Content = "🗑  Xóa Vĩnh Viễn", Width = 150 };
        deleteBtn.Style = DangerBtn;
        btnPanel.Children.Add(restoreBtn); btnPanel.Children.Add(deleteBtn);
        panel.Children.Add(btnPanel);

        restoreBtn.Click += async (_, _) =>
        {
            if (qList.SelectedIndex < 0 || qList.SelectedIndex >= active.Count) return;
            var q = active[qList.SelectedIndex];
            try
            {
                if (File.Exists(q.QuarantinePath) || Directory.Exists(q.QuarantinePath))
                {
                    var destDir = Path.GetDirectoryName(q.OriginalPath);
                    if (destDir != null) Directory.CreateDirectory(destDir);
                    if (File.Exists(q.QuarantinePath))
                        File.Move(q.QuarantinePath, q.OriginalPath, true);
                    else
                        Directory.Move(q.QuarantinePath, q.OriginalPath);
                }
                await _storage.RemoveQuarantineItemAsync(q.Id);
                MessageBox.Show("Khôi phục file thành công!", "Đã Khôi Phục", MessageBoxButton.OK, MessageBoxImage.Information);
                BuildQuarantinePage();
                await RefreshDashboardAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Không thể khôi phục file: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        };

        deleteBtn.Click += async (_, _) =>
        {
            if (qList.SelectedIndex < 0 || qList.SelectedIndex >= active.Count) return;
            var q = active[qList.SelectedIndex];
            if (MessageBox.Show($"Xóa vĩnh viễn {q.FileName}?", "Xác Nhận Xóa",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
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
                Foreground = Danger,
                Margin = new Thickness(0, 12, 0, 0)
            });
        }
    }

    // ========== Cài Đặt & Quy Tắc (gộp 5 tab) ==========

    private void BuildSettingsPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Quản lý quy tắc, rule packs cộng đồng, nhật ký hoạt động và thông tin ứng dụng.",
            Foreground = Secondary, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 18)
        });

        var tabRow = new WrapPanel { Margin = new(0, 0, 0, 12) };
        var tabs = new[] { "📋 Quy Tắc", "📦 Packs", "⬇ VS Code", "📄 Hoạt Động", "ℹ️ Giới Thiệu" };
        var tabBtns = new List<Button>();
        var contentPanel = new StackPanel();
        panel.Children.Add(tabRow);
        panel.Children.Add(contentPanel);

        Action<Button, int> activate = (btn, i) =>
        {
            foreach (var b in tabBtns) b.Style = SecondaryBtn;
            btn.Style = PrimaryBtn;
            contentPanel.Children.Clear();
            if (contentPanel.Tag is Action u) { u(); contentPanel.Tag = null; }
            switch (i)
            {
                case 0: BuildRulesTab(contentPanel); break;
                case 1: BuildCommunityTab(contentPanel); break;
                case 2: BuildVscodeTab(contentPanel); break;
                case 3: BuildActivityTab(contentPanel); break;
                case 4: BuildAboutTab(contentPanel); break;
            }
        };

        for (int i = 0; i < tabs.Length; i++)
        {
            var btn = MakeBtn($"  {tabs[i]}", i == 0 ? PrimaryBtn : SecondaryBtn, 150, new(0, 0, 6, 4));
            var idx = i; btn.Click += (_, _) => activate(btn, idx);
            tabBtns.Add(btn); tabRow.Children.Add(btn);
        }

        BuildRulesTab(contentPanel);
    }

    // ---- Tab builders (adapted from old page builders) ----

    private void BuildRulesTab(StackPanel p)
    {
        p.Children.Add(new TextBlock { Text = "Bộ Quy Tắc — đánh giá file theo rủi ro và hành động.", FontSize = 12.5, Foreground = Secondary, Margin = new(0, 0, 0, 10) });
        var rules = _ruleEngine.GetRules();
        if (rules.Count == 0) { p.Children.Add(new TextBlock { Text = "Chưa có rule nào.", FontStyle = FontStyles.Italic, Foreground = Muted }); return; }
        foreach (var r in rules.OrderByDescending(r => r.Priority))
        {
            var icon = r.Action switch { ItemAction.Block => "🔒", ItemAction.Quarantine => "📦", ItemAction.WarnDelete => "⚠️", _ => "✅" };
            var card = StyledCard(padding: new(10, 6, 10, 6), margin: new(0, 0, 0, 4));
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            row.Children.Add(new TextBlock { Text = $"{icon} ", FontSize = 12, VerticalAlignment = VerticalAlignment.Center });
            var txt = new TextBlock { Text = $"[P{r.Priority}] {r.Name} — {r.Action} ({r.Risk})", FontSize = 11.5, Foreground = Primary, VerticalAlignment = VerticalAlignment.Center };
            row.Children.Add(txt);
            card.Child = row; p.Children.Add(card);
        }
    }

    private async void BuildCommunityTab(StackPanel p)
    {
        p.Children.Add(new TextBlock { Text = "Tải và áp dụng bộ quy tắc từ cộng đồng.", FontSize = 12.5, Foreground = Secondary, Margin = new(0, 0, 0, 10) });
        var manager = App.Services.GetRequiredService<RulePackManager>();
        List<RulePack> packs;
        try { packs = await manager.FetchCommunityPacksAsync(); } catch { packs = BuiltInPacks.All; }
        p.Children.Add(new TextBlock { Text = $"Tìm thấy {packs.Count} rule packs.", FontSize = 11, Foreground = Muted });
        var wrap = new WrapPanel();
        foreach (var pack in packs)
        {
            var card = StyledCard(padding: new(14), margin: new(0, 0, 10, 10)); card.Width = 260;
            var s = new StackPanel();
            s.Children.Add(new TextBlock { Text = $"{pack.Name}  v{pack.Version}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Primary });
            s.Children.Add(new TextBlock { Text = pack.Description, FontSize = 11, Foreground = Secondary, Margin = new(0, 4, 0, 6) });
            var btn = MakeBtn("  Cài", PrimaryBtn, 70);
            btn.Click += (_, _) => { manager.InstallPack(pack); ShowToast("Đã cài!", pack.Name, false); };
            s.Children.Add(btn);
            card.Child = s; wrap.Children.Add(card);
        }
        p.Children.Add(wrap);
    }

    private void BuildVscodeTab(StackPanel p)
    {
        p.Children.Add(new TextBlock { Text = "Tích chọn extension → Cài Đã Chọn. Cài thẳng vào VS Code, không cần terminal.", FontSize = 12.5, Foreground = Secondary, Margin = new(0, 0, 0, 10) });

        if (!VscodeHelper.IsCodeAvailable())
        {
            p.Children.Add(new TextBlock { Text = "⚠️ Chưa có VS Code CLI. Mở VS Code → Ctrl+Shift+P → 'Shell Command: Install code command in PATH'.", FontSize = 11.5, Foreground = Warning });
            return;
        }

        var installedSet = VscodeHelper.GetInstalledExtensions();
        var checkedSet = new HashSet<string>();

        var statusLbl = new TextBlock { Text = $"Đã cài: {VscodeHelper.Recommended.Count(e => installedSet.Contains(e.Id))}/{VscodeHelper.Recommended.Length}", FontSize = 11, Foreground = Success, Margin = new(0, 0, 0, 8) };
        p.Children.Add(statusLbl);

        var installBtn = MakeBtn("⚡  Cài Đã Chọn (0)", PrimaryBtn, 160, new(0, 0, 0, 8));
        installBtn.IsEnabled = false;
        p.Children.Add(installBtn);

        void UpdateBtn() { var c = checkedSet.Count(id => !installedSet.Contains(id)); installBtn.Content = c == 0 ? "⚡  Cài Đã Chọn" : $"⚡  Cài Đã Chọn ({c})"; installBtn.IsEnabled = c > 0; }

        var scroll = new ScrollViewer { MaxHeight = 340, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var listP = new StackPanel(); scroll.Content = listP; p.Children.Add(scroll);

        foreach (var (id, name, desc, cat) in VscodeHelper.Recommended.Take(30))
        {
            var isOk = installedSet.Contains(id); var chk = checkedSet.Contains(id);
            var card = StyledCard(padding: new(8, 6, 8, 6), margin: new(0, 0, 0, 2));
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var cb = new CheckBox { VerticalAlignment = VerticalAlignment.Center, Margin = new(4, 0, 8, 0), IsChecked = isOk || chk, IsEnabled = !isOk };
            cb.Checked += (_, _) => { if (!isOk) { checkedSet.Add(id); UpdateBtn(); } };
            cb.Unchecked += (_, _) => { checkedSet.Remove(id); UpdateBtn(); };
            Grid.SetColumn(cb, 0); row.Children.Add(cb);

            var info = new StackPanel();
            info.Children.Add(new TextBlock { Text = isOk ? $"{name} ✅" : name, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = isOk ? Success : Primary });
            info.Children.Add(new TextBlock { Text = $"{id} · {cat}", FontSize = 9.5, Foreground = Muted });
            Grid.SetColumn(info, 1); row.Children.Add(info);

            card.Child = row; listP.Children.Add(card);
        }

        installBtn.Click += async (_, _) =>
        {
            var todo = checkedSet.Where(id => !installedSet.Contains(id)).ToList();
            if (todo.Count == 0) return;
            installBtn.IsEnabled = false; installBtn.Content = "⏳  Đang cài...";

            // ── Modern progress card (Steam-style) ──
            var progCard = StyledCard(padding: new(16), margin: new(0, 0, 0, 8));
            var progStack = new StackPanel();
            var progIcon = new TextBlock { Text = "📦", FontSize = 18, Margin = new(0, 0, 0, 6) };
            var progTitle = new TextBlock { Text = "Đang cài extensions...", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Accent };
            var progBar = new ProgressBar { Height = 6, Maximum = 100, Value = 0, Margin = new(0, 8, 0, 4) };
            var progDetail = new TextBlock { Text = "0 / 0", FontSize = 11, Foreground = Secondary };
            var progSpeed = new TextBlock { Text = "⚡ Đang chuẩn bị...", FontSize = 10.5, Foreground = Muted, Margin = new(0, 2, 0, 0) };
            progStack.Children.Add(progIcon); progStack.Children.Add(progTitle);
            progStack.Children.Add(progBar); progStack.Children.Add(progDetail); progStack.Children.Add(progSpeed);
            progCard.Child = progStack;
            p.Children.Insert(2, progCard); // Insert after statusLbl

            var startTime = DateTime.Now;
            int ok = 0;

            for (int i = 0; i < todo.Count; i++)
            {
                var id = todo[i];
                var name = VscodeHelper.Recommended.First(e => e.Id == id).Name;
                var pct = (double)(i + 1) / todo.Count * 100;
                var elapsed = (DateTime.Now - startTime).TotalSeconds;
                var eta = elapsed > 0 ? (elapsed / (i + 1)) * (todo.Count - i - 1) : 0;

                progTitle.Text = $"⬇  {name}";
                progBar.Value = pct;
                progDetail.Text = $"{pct:F0}%  ·  {i + 1} / {todo.Count}";
                progSpeed.Text = $"⏱  {(eta < 60 ? $"{eta:F0}s còn lại" : $"{eta / 60:F0}m còn lại")}";
                statusLbl.Text = $"⏳ {i + 1}/{todo.Count}...";

                if (await VscodeHelper.InstallAsync(id))
                { ok++; installedSet.Add(id); checkedSet.Remove(id); ActivityLogger.Success("VS Code", name); }
                else { ActivityLogger.Fail("VS Code", name); await Task.Delay(500); }
            }

            // ── Done ──
            var totalTime = (DateTime.Now - startTime).TotalSeconds;
            progIcon.Text = "✅";
            progTitle.Text = "Hoàn tất!";
            progTitle.Foreground = Success;
            progBar.Value = 100;
            progDetail.Text = $"⏱  {totalTime:F1}s  ·  {ok}/{todo.Count} thành công";
            progSpeed.Text = "";

            statusLbl.Text = $"✅ Đã cài {ok}/{todo.Count}. Mở VS Code để dùng.";
            installBtn.Content = "⚡  Cài Đã Chọn"; UpdateBtn();

            // Auto-remove progress after 4 seconds
            await Task.Delay(4000);
            p.Children.Remove(progCard);
        };
    }

    private void BuildActivityTab(StackPanel p)
    {
        p.Children.Add(new TextBlock { Text = "Nhật ký hoạt động — tự động cập nhật khi dùng app.", FontSize = 12.5, Foreground = Secondary, Margin = new(0, 0, 0, 10) });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 8) };
        var openBtn = MakeBtn("📄  Mở Log Notepad", SecondaryBtn, 150, new(0, 0, 8, 0));
        openBtn.Click += (_, _) => ActivityLogger.OpenInNotepad();
        btnRow.Children.Add(openBtn);
        p.Children.Add(btnRow);

        var scroll = new ScrollViewer { MaxHeight = 420, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var logPanel = new StackPanel(); scroll.Content = logPanel; p.Children.Add(scroll);

        void RefreshLog()
        {
            logPanel.Children.Clear();
            var entries = ActivityLogger.RecentEntries;
            if (entries.Count == 0) { logPanel.Children.Add(new TextBlock { Text = "Chưa có hoạt động nào.", FontStyle = FontStyles.Italic, Foreground = Muted }); return; }
            for (int i = entries.Count - 1; i >= 0; i--)
            {
                var e = entries[i];
                var color = e.Status == "FAIL" ? Danger : e.Status == "OK" ? Success : Secondary;
                var card = StyledCard(padding: new(8, 5, 8, 5), margin: new(0, 0, 0, 2));
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = e.Icon, FontSize = 12, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 6, 0) });
                row.Children.Add(new TextBlock { Text = e.TimeStr, FontSize = 10, Foreground = Muted, Width = 55, VerticalAlignment = VerticalAlignment.Center, Margin = new(0, 0, 8, 0) });
                row.Children.Add(new TextBlock { Text = e.Action, FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = color, VerticalAlignment = VerticalAlignment.Center });
                if (!string.IsNullOrEmpty(e.Detail)) row.Children.Add(new TextBlock { Text = $" — {e.Detail}", FontSize = 10.5, Foreground = Secondary, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                card.Child = row; logPanel.Children.Add(card);
            }
        }

        RefreshLog();

        // Subscribe to real-time updates
        Action<LogEntry> handler = _ => Dispatcher.Invoke(RefreshLog);
        ActivityLogger.OnEntryAdded += handler;
        p.Tag = new Action(() => ActivityLogger.OnEntryAdded -= handler); // unsubscribe on tab switch
    }

    private void BuildAboutTab(StackPanel p)
    {
        var headerCard = StyledCard(padding: new Thickness(20), margin: new Thickness(0, 8, 0, 14));
        var headerStack = new StackPanel();
        headerStack.Children.Add(new TextBlock { Text = "Windows Health Manager", FontSize = 20, FontWeight = FontWeights.Bold, Foreground = Primary, Margin = new(0, 0, 0, 4) });
        headerStack.Children.Add(new TextBlock { Text = "v2.0.0  ·  MIT License  ·  Đào Văn Phong", FontSize = 13, Foreground = Accent, Margin = new(0, 0, 0, 6) });
        headerStack.Children.Add(new TextBlock { Text = "Developer Disk Manager cho Windows. Không telemetry, không quảng cáo, không internet.", FontSize = 11.5, Foreground = Secondary, Margin = new(0, 0, 0, 16) });

        var btnRow = new StackPanel { Orientation = Orientation.Horizontal };
        var updateBtn = MakeBtn("🔄 Kiểm Tra Cập Nhật", PrimaryBtn, 180, new(0, 0, 8, 0));
        updateBtn.Click += CheckForUpdates_Click;
        var reportBtn = MakeBtn("📊 Báo Cáo Tuần", SecondaryBtn, 160);
        reportBtn.Click += (_, _) => { try { App.Services.GetRequiredService<ReportGenerator>().ShowHtmlReportAsync(); } catch { } };
        btnRow.Children.Add(updateBtn); btnRow.Children.Add(reportBtn);
        headerStack.Children.Add(btnRow);
        headerCard.Child = headerStack;
        p.Children.Add(headerCard);

        var techCard = StyledCard(padding: new Thickness(16), margin: new Thickness(0, 0, 0, 0));
        var techStack = new StackPanel();
        techStack.Children.Add(new TextBlock { Text = "🛠  TECH STACK", FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Muted, Margin = new(0, 0, 0, 8) });
        foreach (var (icon, tech) in new[] {
            ("⚡", ".NET 9.0 WPF — Windows Presentation Foundation"),
            ("💾", "LiteDB — Embedded NoSQL database, zero-config"),
            ("📝", "Serilog — Structured logging với daily rolling"),
            ("🏗", "Clean Architecture + MVVM pattern"),
            ("🔗", "github.com/ngphong01/winvitals")
        })
        {
            techStack.Children.Add(new TextBlock
            {
                Text = $"{icon}  {tech}",
                FontSize = 11.5, Foreground = Secondary, Margin = new(0, 0, 0, 5)
            });
        }
        techCard.Child = techStack;
        p.Children.Add(techCard);
    }
    private void BuildAutoCleanPage()
    {
        var panel = PagePanel;
        panel.Children.Add(new TextBlock
        {
            Text = "Cài đặt lịch dọn dẹp tự động — chọn preset, đặt lịch rồi để hệ thống tự chạy.",
            Foreground = Secondary, FontSize = 12.5, Margin = new Thickness(0, 0, 0, 18)
        });

        // ── Presets ──
        panel.Children.Add(new TextBlock { Text = "⚡  CLEAN PRESETS", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Accent, Margin = new(0, 0, 0, 10) });

        var presetsWrap = new WrapPanel();
        foreach (var preset in CleanPresets.All)
        {
            var card = StyledCard(padding: new(16), margin: new(0, 0, 8, 8));
            card.Width = 210;
            var s = new StackPanel();
            s.Children.Add(new TextBlock { Text = $"{preset.Icon}  {preset.Name}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = Primary });
            s.Children.Add(new TextBlock { Text = preset.Description, FontSize = 10.5, Foreground = Secondary, Margin = new(0, 4, 0, 6), TextWrapping = TextWrapping.Wrap, Height = 42 });
            s.Children.Add(new TextBlock { Text = $"⏱ {preset.Duration}  ·  📦 {preset.EstimatedCleanable}", FontSize = 10, Foreground = Muted });

            var btn = MakeBtn(preset.SafeToAuto ? "  Chạy Ngay" : "  Preview", preset.SafeToAuto ? SuccessBtn : SecondaryBtn, 110, new(0, 8, 0, 0));
            var capturedPreset = preset;
            btn.Click += async (_, _) =>
            {
                btn.IsEnabled = false; btn.Content = "⏳  Đang chạy...";
                ShowLoading($"{capturedPreset.Name}...");
                try
                {
                    CaptureDiskSnapshot(); _lastCleanSnapshot.Clear();
                    if (!capturedPreset.SafeToAuto)
                    {
                        var scanner = new DiskScanner(_ruleEngine, _riskEngine);
                        var drives = System.IO.DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed).Select(d => d.Name).ToList();
                        var items = await scanner.ScanAsync(drives, new Progress<(string, int)>(p => Dispatcher.Invoke(() => TxtLoading.Text = p.Item1)), CancellationToken.None);
                        SafeHideLoading();
                        ShowToast($"{capturedPreset.Name}: Preview", $"Có thể dọn {ScanItem.FormatSize(items.Sum(i => i.SizeBytes))} từ {items.Count} mục.", false);
                    }
                    else
                    {
                        // Fast direct clean
                        long freed = 0; int processed = 0;
                        await Task.Run(() =>
                        {
                            var dirs = new[] { Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"), Path.GetTempPath(), @"C:\Windows\Temp", @"C:\Windows\Prefetch" };
                            foreach (var d in dirs)
                            {
                                try { if (!Directory.Exists(d)) continue; foreach (var f in Directory.EnumerateFiles(d, "*", SearchOption.AllDirectories).Take(500)) { try { var s = new FileInfo(f).Length; File.Delete(f); freed += s; processed++; _lastCleanSnapshot.Add((f, s, false)); } catch { } } } catch { }
                            }
                        });
                        SafeHideLoading();
                        if (processed == 0) { ShowToast("Không có gì để dọn", "Hệ thống đã sạch.", false); }
                        else
                        {
                            var diff = FormatBeforeAfter();
                            ShowToast($"{capturedPreset.Name}: Đã dọn {ScanItem.FormatSize(freed)}", string.IsNullOrEmpty(diff) ? $"{processed} mục. Ctrl+Z để hoàn tác." : $"{diff}\n{processed} mục. Ctrl+Z để hoàn tác.", true);
                            ActivityLogger.Success($"Preset: {capturedPreset.Name}", $"Giải phóng {ScanItem.FormatSize(freed)}, {processed} mục");
                            await RefreshDashboardAsync();
                        }
                    }
                }
                catch (Exception ex) { SafeHideLoading(); ShowToast("Lỗi", ex.Message, false); ActivityLogger.Fail($"Preset: {capturedPreset.Name}", ex.Message); }
                finally { btn.IsEnabled = true; btn.Content = capturedPreset.SafeToAuto ? "  Chạy Ngay" : "  Preview"; }
            };
            s.Children.Add(btn);
            card.Child = s;
            presetsWrap.Children.Add(card);
        }
        panel.Children.Add(presetsWrap);

        // ── Scheduler ──
        panel.Children.Add(new TextBlock { Text = "⏰  LỊCH TỰ ĐỘNG", FontSize = 12, FontWeight = FontWeights.Bold, Foreground = Primary, Margin = new(0, 16, 0, 10) });

        try
        {
            var scheduler = App.Services.GetRequiredService<SchedulerService>();
            var tasks = scheduler.ListTasks();

            // Task list
            if (tasks.Count > 0)
            {
                foreach (var t in tasks)
                {
                    var taskCard = StyledCard(padding: new(12, 10, 12, 10), margin: new(0, 0, 0, 4));
                    var row = new Grid();
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var info = new StackPanel();
                    info.Children.Add(new TextBlock { Text = $"📅  {t.Name}", FontSize = 12.5, FontWeight = FontWeights.SemiBold, Foreground = Secondary });
                    info.Children.Add(new TextBlock { Text = $"{t.NextRun}  ·  {t.Status}", FontSize = 11, Foreground = Secondary, Margin = new(0, 2, 0, 0) });
                    Grid.SetColumn(info, 0); row.Children.Add(info);

                    var delBtn = new Button { Content = "✕", Width = 26, Height = 26, Style = DangerBtn, FontSize = 11, Padding = new(0), Margin = new(8, 0, 0, 0) };
                    var captured = t;
                    delBtn.Click += (_, _) =>
                    {
                        try { scheduler.DeleteTask(captured.Name); ShowToast("Đã xóa lịch", captured.Name, false); ActivityLogger.Success("Xóa lịch đặt", captured.Name); BuildAutoCleanPage(); } catch { }
                    };
                    Grid.SetColumn(delBtn, 1); row.Children.Add(delBtn);
                    taskCard.Child = row;
                    panel.Children.Add(taskCard);
                }
            }
            else
            {
                panel.Children.Add(new TextBlock { Text = "Chưa có lịch nào. Tạo lịch bên dưới.", FontSize = 11.5, Foreground = Muted, FontStyle = FontStyles.Italic, Margin = new(0, 4, 0, 8) });
            }

            // Create form
            var formCard = StyledCard(padding: new(16), margin: new(0, 8, 0, 0));
            var formGrid = new Grid();
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            formGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var presets = CleanPresets.All.ToList();
            var presetCombo = new ComboBox { Width = 180, Style = FindResource("ModernComboBox") as Style, Margin = new(0, 0, 8, 0) };
            foreach (var p in presets) presetCombo.Items.Add($"{p.Icon} {p.Name}");
            presetCombo.SelectedIndex = 0;
            Grid.SetColumn(presetCombo, 0); formGrid.Children.Add(presetCombo);

            var recurCombo = new ComboBox { Width = 120, Style = FindResource("ModernComboBox") as Style, Margin = new(0, 0, 8, 0) };
            recurCombo.Items.Add("Hàng Ngày"); recurCombo.Items.Add("Hàng Tuần"); recurCombo.Items.Add("Hàng Tháng"); recurCombo.Items.Add("Một Lần");
            recurCombo.SelectedIndex = 0;
            Grid.SetColumn(recurCombo, 1); formGrid.Children.Add(recurCombo);

            var timeCombo = new ComboBox { Width = 80, Style = FindResource("ModernComboBox") as Style, Margin = new(0, 0, 8, 0) };
            for (int h = 0; h < 24; h++) timeCombo.Items.Add($"{h:D2}:00");
            timeCombo.SelectedIndex = 9;
            Grid.SetColumn(timeCombo, 2); formGrid.Children.Add(timeCombo);

            var createBtn = MakeBtn("  Tạo Lịch", PrimaryBtn, 110);
            Grid.SetColumn(createBtn, 3); formGrid.Children.Add(createBtn);

            var statusLbl = new TextBlock { Text = "", FontSize = 11, Foreground = Muted, Margin = new(8, 0, 0, 0) };
            Grid.SetColumn(statusLbl, 4); formGrid.Children.Add(statusLbl);

            formCard.Child = formGrid;
            panel.Children.Add(formCard);

            createBtn.Click += (_, _) =>
            {
                var p = presets[presetCombo.SelectedIndex];
                var rec = recurCombo.SelectedIndex switch { 3 => "ONCE", 2 => "MONTHLY", 0 => "DAILY", _ => "WEEKLY" };
                var hour = timeCombo.SelectedIndex;
                var name = $"{p.Name} định kỳ";
                var ok2 = scheduler.CreateCleanTask(name, p.CleanLevel.ToString().ToLower(), rec, false, DateTime.Today.AddHours(hour));
                if (ok2) { statusLbl.Text = "✅ Đã tạo!"; statusLbl.Foreground = Success; ShowToast("Đã tạo lịch!", $"{name} · {rec} · {hour:D2}:00", false); ActivityLogger.Success("Tạo lịch đặt", $"{name} · {rec}"); BuildAutoCleanPage(); }
                else { statusLbl.Text = "❌ Lỗi tạo lịch"; statusLbl.Foreground = Danger; }
            };
        }
        catch (Exception ex) { panel.Children.Add(new TextBlock { Text = $"Lỗi: {ex.Message}", Foreground = Danger }); }
    }


    // ========== Community Rule Packs Page ==========

    private async void CheckForUpdates_Click(object? sender, RoutedEventArgs? e)
    {
        try
        {
            var updateService = App.Services.GetRequiredService<AutoUpdateService>();
            var result = await updateService.CheckAsync();

            if (result.HasUpdate && result.ReleaseUrl != null)
            {
                var msg = $"Phien ban moi: {result.RemoteVersion}\n" +
                          $"Ban dang dung: {result.CurrentVersion}\n\n" +
                          $"{result.ReleaseNotes?.Substring(0, Math.Min(500, result.ReleaseNotes.Length))}\n\n" +
                          $"Tai xuong: {result.DownloadUrl}";

                var mbResult = MessageBox.Show(msg, "Cap nhat moi!", MessageBoxButton.YesNo, MessageBoxImage.Information);
                if (mbResult == MessageBoxResult.Yes && result.DownloadUrl != null)
                {
                    ShowLoading("Đang tải phien ban moi...");
                    var installerPath = await updateService.DownloadAsync(result.DownloadUrl);
                    HideLoading();
                    if (installerPath != null)
                    {
                        AutoUpdateService.ApplyUpdate(installerPath);
                    }
                    else
                    {
                        MessageBox.Show("Tai xuong that bai. Vui long thu lai sau.", "Loi", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            else
            {
                var msg = result.Error != null
                    ? $"Không thể kiem tra: {result.Error}"
                    : "Ban dang dung phien ban moi nhat!";
                MessageBox.Show(msg, "Cap nhat", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}", "Cap nhat", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private async void OneClickFix_Click(object sender, RoutedEventArgs e)
    {
        var btn = (Button)sender;
        btn.IsEnabled = false;
        btn.Content = "⏳  Đang sửa...";
        ShowLoading("Đang dọn nhanh...");
        try
        {
            CaptureDiskSnapshot();
            _lastCleanSnapshot.Clear();
            var safeDirs = new (string Path, int MaxFiles)[] {
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"), 500),
                (Path.GetTempPath(), 500), (@"C:\Windows\Temp", 300), (@"C:\Windows\Prefetch", 200),
                (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Windows\\WER"), 100),
            };
            long freed = 0; int processed = 0;
            await Task.Run(() =>
            {
                foreach (var (dir, max) in safeDirs)
                {
                    try {
                        if (!Directory.Exists(dir)) continue;
                        foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories).Take(max))
                        {
                            try { var s = new FileInfo(file).Length; File.Delete(file); freed += s; processed++; _lastCleanSnapshot.Add((file, s, false)); } catch { }
                        }
                    } catch { }
                }
            });
            SafeHideLoading();
            if (processed == 0) { ShowToast("Không có gì để dọn", "Hệ thống đã sạch.", false); return; }
            var diff = FormatBeforeAfter();
            var body = string.IsNullOrEmpty(diff) ? $"Đã dọn {ScanItem.FormatSize(freed)} — {processed} mục. Ctrl+Z để hoàn tác." : $"{diff}\nĐã dọn {processed} mục. Ctrl+Z để hoàn tác.";
            ShowToast("🛠 Đã sửa xong!", body, true);
            ActivityLogger.Success("Sửa Tất Cả (One Click Fix)", $"Giải phóng {ScanItem.FormatSize(freed)}, {processed} mục");
            await RefreshDashboardAsync();
        }
        catch (Exception ex) { SafeHideLoading(); ShowToast("Lỗi", ex.Message, false); ActivityLogger.Fail("Sửa Tất Cả", ex.Message); }
        finally { btn.IsEnabled = true; btn.Content = "🛠  Sửa Tất Cả"; }
    }

    private void CleanupPotential_Click(object sender, System.Windows.Input.MouseButtonEventArgs e) { OneClickFix_Click(BtnSuaNgay, null!); }

    // ========== Toast + Before/After + Estimate ==========

    private CancellationTokenSource? _toastCts;
    private async void ShowToast(string title, string body, bool showUndo = false)
    {
        try
        {
            // Simple status-bar toast: update status text briefly
            StatusCpu.Text = $"{title}: {body}";
            _toastCts?.Cancel();
            _toastCts = new CancellationTokenSource();
            var token = _toastCts.Token;
            await Task.Delay(5000, token);
            if (!token.IsCancellationRequested)
                StatusCpu.Text = "";
        }
        catch { }
    }

    private void CaptureDiskSnapshot()
    {
        _diskBeforeSnapshot = System.IO.DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
            .ToDictionary(d => d.Name.TrimEnd('\\'), d => d.AvailableFreeSpace);
    }

    private string FormatBeforeAfter()
    {
        if (_diskBeforeSnapshot == null) return "";
        var after = System.IO.DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == System.IO.DriveType.Fixed)
            .ToDictionary(d => d.Name.TrimEnd('\\'), d => d.AvailableFreeSpace);
        var parts = new List<string>();
        foreach (var (drive, beforeFree) in _diskBeforeSnapshot)
        {
            if (after.TryGetValue(drive, out var afterFree))
            {
                var delta = afterFree - beforeFree;
                if (delta > 10_000_000)
                    parts.Add($"{drive}: {ScanItem.FormatSize(beforeFree)} → {ScanItem.FormatSize(afterFree)} (+{ScanItem.FormatSize(delta)})");
            }
        }
        _diskBeforeSnapshot = null;
        return string.Join("\n", parts);
    }

    private static long EstimateCleanableSpace()
    {
        long total = 0;
        var maxPerDir = 200_000_000L;
        var dirs = new (string Path, int Max)[]
        {
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"), 500),
            (Path.GetTempPath(), 500), (@"C:\Windows\Temp", 300), (@"C:\Windows\Prefetch", 200),
            (Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\Windows\\WER"), 100),
        };
        foreach (var (path, max) in dirs)
        {
            try
            {
                if (!Directory.Exists(path)) continue;
                long dirTotal = 0;
                foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
                {
                    try { dirTotal += new FileInfo(file).Length; } catch { }
                    if (dirTotal > maxPerDir) break;
                }
                total += dirTotal;
            }
            catch { }
        }
        try
        {
            var recycleDir = @"C:\$Recycle.Bin";
            if (Directory.Exists(recycleDir))
            {
                long rs = 0;
                foreach (var file in Directory.EnumerateFiles(recycleDir, "*", SearchOption.AllDirectories))
                {
                    try { rs += new FileInfo(file).Length; } catch { }
                    if (rs > 500_000_000) break;
                }
                total += rs;
            }
        }
        catch { }
        return total;
    }

    private async void PerfCheck_Click(object sender, RoutedEventArgs e)
    {
        Nav_Click(BtnPerf, null!);
        await RefreshDashboardAsync();
    }


    // ========== File Tools Page (composite) ==========

    private void BuildFileToolsPage()
    {
        var panel = PagePanel;
        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 10) };
        var tabLarge = MakeBtn("  File Lớn", PrimaryBtn, 110, new(0, 0, 6, 0));
        var tabOrphan = MakeBtn("  File Mồ Côi", SecondaryBtn, 110, new(0, 0, 6, 0));
        var tabDup = MakeBtn("  File Trùng", SecondaryBtn, 110, new(0, 0, 0, 0));
        tabRow.Children.Add(tabLarge); tabRow.Children.Add(tabOrphan); tabRow.Children.Add(tabDup);
        panel.Children.Add(tabRow);

        var contentPanel = new StackPanel();
        panel.Children.Add(contentPanel);

        var saved = panel;
        tabLarge.Click += (_, _) => { contentPanel.Children.Clear(); RunFileScan(contentPanel, "Large File", new LargeFileFinder()); };
        tabOrphan.Click += (_, _) => { contentPanel.Children.Clear(); RunFileScan(contentPanel, "Orphan", new OrphanDetector()); };
        tabDup.Click += (_, _) => { contentPanel.Children.Clear(); RunFileScan(contentPanel, "Duplicate", new DuplicateFinder()); };

        RunFileScan(contentPanel, "Large File", new LargeFileFinder());
    }

    private void RunFileScan(StackPanel panel, string label, IScanner scanner)
    {
        panel.Children.Add(Label("Nhấn Quét de tim file co the don.", Secondary));
        var statusLabel = Label("Sẵn sàng.", Secondary);
        panel.Children.Add(statusLabel);

        var scanBtn = MakeBtn("  Quét", PrimaryBtn, 100, new(0, 0, 0, 10));
        panel.Children.Add(scanBtn);

        var resultsBox = new ListBox { MaxHeight = 350, Background = CardBg, Foreground = Primary };
        panel.Children.Add(resultsBox);

        scanBtn.Click += async (_, _) =>
        {
            scanBtn.IsEnabled = false;
            resultsBox.Items.Clear();
            ShowLoading("Đang quét...");

            try
            {
                var items = await scanner.ScanAsync(["C:"], new Progress<(string, int)>(p =>
                    Dispatcher.Invoke(() => statusLabel.Text = p.Item1)));

                SafeHideLoading();
                if (items.Count == 0) { statusLabel.Text = "Không tìm thấy."; scanBtn.IsEnabled = true; return; }

                statusLabel.Text = $"Tìm thấy {items.Count} items ({ScanItem.FormatSize(items.Sum(i => i.SizeBytes))})";
                foreach (var item in items.Take(30))
                    resultsBox.Items.Add($"{item.SizeFormatted,10}  {item.Name}  {item.Suggestion}");
            }
            catch (Exception ex) { SafeHideLoading(); statusLabel.Text = $"Lỗi: {ex.Message}"; }
            finally { scanBtn.IsEnabled = true; SafeHideLoading(); }
        };
    }

    // ========== Dev Tools Page (composite) ==========

    private void BuildDevToolsPage()
    {
        var panel = PagePanel;
        var tabRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new(0, 0, 0, 10) };
        var tabDev = MakeBtn("  Cache Dev", PrimaryBtn, 110, new(0, 0, 6, 0));
        var tabPkg = MakeBtn("  Package Cache", SecondaryBtn, 130, new(0, 0, 6, 0));
        var tabStale = MakeBtn("  Stale Projects", SecondaryBtn, 130, new(0, 0, 0, 0));
        tabRow.Children.Add(tabDev); tabRow.Children.Add(tabPkg); tabRow.Children.Add(tabStale);
        panel.Children.Add(tabRow);

        var contentPanel = new StackPanel();
        panel.Children.Add(contentPanel);

        tabDev.Click += (_, _) => { contentPanel.Children.Clear(); BuildDevTab(contentPanel); };
        tabPkg.Click += (_, _) => { contentPanel.Children.Clear(); BuildPackageTab(contentPanel); };
        tabStale.Click += (_, _) => { contentPanel.Children.Clear(); BuildStaleTab(contentPanel); };

        BuildDevTab(contentPanel);
    }

    private void BuildDevTab(StackPanel panel)
    {
        panel.Children.Add(Label("Don cache lap trinh: node_modules, build, dist, .next...", Secondary));
        panel.Children.Add(MakeBtn("  Quét Cache Dev", PrimaryBtn, 160));
    }

    private void BuildPackageTab(StackPanel panel)
    {
        panel.Children.Add(Label("Phan tich npm, pip, NuGet, Go, Docker, Gradle caches.", Secondary));
        var statusLabel = Label("Sẵn sàng.", Secondary);
        panel.Children.Add(statusLabel);
        var scanBtn = MakeBtn("  Quét Package Caches", PrimaryBtn, 180, new(0, 0, 0, 10));
        panel.Children.Add(scanBtn);
        var resultsStack = new StackPanel();
        panel.Children.Add(resultsStack);

        scanBtn.Click += async (_, _) =>
        {
            scanBtn.IsEnabled = false; resultsStack.Children.Clear();
            ShowLoading("Đang phân tích...");
            try
            {
                var scanner = new DevCacheScanner();
                var items = await scanner.ScanAsync(["C:"], new Progress<(string, int)>(p =>
                    Dispatcher.Invoke(() => statusLabel.Text = p.Item1)));
                SafeHideLoading();
                foreach (var item in items.Take(30))
                {
                    var card = StyledCard(padding: new(10, 6, 10, 6), margin: new(0, 0, 0, 4));
                    card.Child = new TextBlock { Text = $"{item.Name,-25}  {item.SizeFormatted,10}", FontSize = 12, Foreground = Success, FontFamily = new System.Windows.Media.FontFamily("Consolas") };
                    resultsStack.Children.Add(card);
                }
                statusLabel.Text = $"Tìm thấy {items.Count} package caches ({ScanItem.FormatSize(items.Sum(i => i.SizeBytes))})";
            }
            catch (Exception ex) { SafeHideLoading(); statusLabel.Text = $"Lỗi: {ex.Message}"; }
            finally { scanBtn.IsEnabled = true; SafeHideLoading(); }
        };
    }

    private void BuildStaleTab(StackPanel panel)
    {
        panel.Children.Add(Label("Phát hiện project cu >60 ngay khong commit.", Secondary));
        var statusLabel = Label("Sẵn sàng.", Secondary);
        panel.Children.Add(statusLabel);
        var scanBtn = MakeBtn("  Quét Stale Projects", PrimaryBtn, 180, new(0, 0, 0, 10));
        panel.Children.Add(scanBtn);
        var resultsStack = new StackPanel();
        panel.Children.Add(resultsStack);

        scanBtn.Click += async (_, _) =>
        {
            scanBtn.IsEnabled = false; resultsStack.Children.Clear();
            ShowLoading("Đang quét...");
            try
            {
                var scanner = new StaleProjectDetector();
                var items = await scanner.ScanAsync(["C:"], new Progress<(string, int)>(p =>
                    Dispatcher.Invoke(() => statusLabel.Text = p.Item1)));
                SafeHideLoading();
                foreach (var item in items.Take(30))
                {
                    var card = StyledCard(padding: new(10, 6, 10, 6), margin: new(0, 0, 0, 4));
                    var isStale = item.Risk <= RiskLevel.Low;
                    var stack = new StackPanel();
                    stack.Children.Add(new TextBlock { Text = $"{(isStale ? "STALE" : "ACTIVE")}  {item.Name}", FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = isStale ? Warning : Success });
                    stack.Children.Add(new TextBlock { Text = $"   {item.Suggestion}", FontSize = 10.5, Foreground = Muted });
                    card.Child = stack;
                    resultsStack.Children.Add(card);
                }
                statusLabel.Text = $"Tìm thấy {items.Count} projects ({ScanItem.FormatSize(items.Sum(i => i.SizeBytes))})";
            }
            catch (Exception ex) { SafeHideLoading(); statusLabel.Text = $"Lỗi: {ex.Message}"; }
            finally { scanBtn.IsEnabled = true; SafeHideLoading(); }
        };
    }

    // ========== Performance Timer ==========

    private void StartPerfTimer()
    {
        StopPerfTimer();
        _perfTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
        _perfTimer.Tick += async (_, _) =>
        {
            if (_perfVM == null) return;
            await _perfVM.RefreshAsync();
            UpdatePerfLabels();
        };
        _perfTimer.Start();
    }

    private void StopPerfTimer()
    {
        _perfTimer?.Stop();
        _perfTimer = null;
    }

    

    private async Task LoadPerfTab(StackPanel panel)
    {
        try
        {
            var snap = await _perfAnalyzer.GetSnapshotAsync();
            RenderPerformancePage(panel, snap);
        }
        catch (Exception ex)
        {
            panel.Children.Add(new TextBlock { Text = $"Lỗi: {ex.Message}", Foreground = Danger, Margin = new(0, 12, 0, 0) });
        }
    }

    

    private Task RunBoostTab(StackPanel panel)
    {
        if (panel.Children.Count > 0) return Task.CompletedTask;
        var booster = App.Services.GetRequiredService<SystemBooster>();
        var progress = new Progress<string>(s => App.Log.Information("Boost: {Msg}", s));

        var btn = MakeBtn("  Tăng Tốc He Thong", PrimaryBtn, 180, new(0, 0, 0, 12));
        panel.Children.Add(btn);

        var resultCard = StyledCard(padding: new(16), margin: new(0, 0, 0, 12));
        resultCard.Visibility = Visibility.Collapsed;
        panel.Children.Add(resultCard);

        btn.Click += async (_, _) =>
        {
            btn.IsEnabled = false;
            btn.Content = "  Đang tăng tốc...";
            resultCard.Visibility = Visibility.Collapsed;

            try
            {
                var result = await booster.QuickBoostAsync(progress);
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = "Tăng Tốc Thanh Cong!", FontSize = 18, FontWeight = FontWeights.Bold, Foreground = Success });
                stack.Children.Add(new TextBlock { Text = $"Giải phóng RAM: {result.MemoryFreedMB:F0} MB", FontSize = 13, Foreground = Secondary, Margin = new(0, 6, 0, 0) });
                stack.Children.Add(new TextBlock { Text = $"Đã dọn temp: {result.TempFreedMB:F1} MB", FontSize = 13, Foreground = Secondary });
                stack.Children.Add(new TextBlock { Text = $"DNS cache: {(result.DnsCacheCleared ? "Đã xóa" : "Không doi")}", FontSize = 13, Foreground = Secondary });
                    stack.Children.Add(new TextBlock { Text = $"Hieu ung hinh anh: {(result.VisualEffectsDisabled ? "Đã tat" : "Không doi")}", FontSize = 13, Foreground = Secondary });
                    stack.Children.Add(new TextBlock { Text = $"Power Plan: {(result.PowerPlanSet ? "Hiệu năng cao" : "Không doi")}", FontSize = 13, Foreground = Secondary });
                stack.Children.Add(new TextBlock { Text = $"Thoi gian: {result.DurationMs}ms", FontSize = 11, Foreground = Muted, Margin = new(0, 6, 0, 0) });

                resultCard.Child = stack;
                resultCard.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                resultCard.Child = new TextBlock { Text = $"Lỗi: {ex.Message}", Foreground = Danger };
                resultCard.Visibility = Visibility.Visible;
            }
            finally { btn.IsEnabled = true; btn.Content = "  Tăng Tốc He Thong"; }
        };
        return Task.CompletedTask;
    }

private async Task RunDiagnosticAsync(StackPanel panel)
    {
        ShowLoading("Đang chẩn đoán hệ thống...");
        try
        {
            var diag = App.Services.GetRequiredService<SystemDiagnostic>();
            var findings = await diag.DiagnoseAsync();
            var score = diag.GetHealthScore(findings);
            SafeHideLoading();

            var scoreCard = StyledCard(padding: new(16), margin: new(0, 0, 0, 12));
            var scoreColor = score >= 80 ? Success : score >= 50 ? Warning : Danger;
            scoreCard.Child = new TextBlock { Text = $"Điểm sức khỏe: {score}/100", FontSize = 22, FontWeight = FontWeights.Bold, Foreground = scoreColor };
            panel.Children.Add(scoreCard);

            if (findings.Count == 0)
            {
                panel.Children.Add(Label("Máy đang hoạt động tốt! Không phát hiện vấn đề.", Success, 13));
                return;
            }

            foreach (var f in findings)
            {
                var color = f.Severity == FindingSeverity.Critical ? Danger : f.Severity == FindingSeverity.Warning ? Warning : Success;
                var card = StyledCard(padding: new(14), margin: new(0, 0, 0, 8));
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock { Text = $"[{f.Category}] {f.Title}", FontSize = 13, FontWeight = FontWeights.Bold, Foreground = color });
                stack.Children.Add(new TextBlock { Text = f.Detail, FontSize = 11, Foreground = Secondary, Margin = new(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
                stack.Children.Add(new TextBlock { Text = $"  {f.Action}", FontSize = 11, Foreground = Accent, Margin = new(0, 4, 0, 0), TextWrapping = TextWrapping.Wrap });
                card.Child = stack;
                panel.Children.Add(card);
            }

            // Add "Dọn Ổ C:" button if disk issue was found
            if (findings.Any(f => f.Category == "Disk"))
            {
                var diskBtn = MakeBtn("  Dọn Ổ C:", DangerBtn, 160, new(0, 12, 0, 0));
                diskBtn.Click += async (_, _) =>
                {
                    diskBtn.IsEnabled = false;
                    ShowLoading("Đang dọn ổ C:...");
                    try
                    {
                        _cts = new CancellationTokenSource();
                        long totalFreed = 0; int totalItems = 0;

                        // 1. Temp files
                        var tempPath = Path.GetTempPath();
                        if (Directory.Exists(tempPath))
                        {
                            TxtLoading.Text = "Đang dọn Temp files...";
                            var tempFiles = Directory.GetFiles(tempPath, "*", SearchOption.TopDirectoryOnly);
                            foreach (var f in tempFiles.Take(200))
                            {
                                try
                                {
                                    var info = new FileInfo(f);
                                    if (info.LastWriteTime < DateTime.Now.AddHours(-24))
                                    { totalFreed += info.Length; File.Delete(f); totalItems++; }
                                }
                                catch { }
                            }
                        }

                        // 2. Recycle Bin — use cmd
                        TxtLoading.Text = "Đang dọn Recycle Bin...";
                        try
                        {
                            var psi = new ProcessStartInfo("cmd.exe", "/c rd /s /q C:\\$Recycle.bin 2>nul")
                            { CreateNoWindow = true, UseShellExecute = false };
                            Process.Start(psi)?.WaitForExit(3000);
                        }
                        catch { }

                        // 3. Windows Temp
                        var winTemp = @"C:\Windows\Temp";
                        if (Directory.Exists(winTemp))
                        {
                            TxtLoading.Text = "Đang dọn Windows Temp...";
                            foreach (var f in Directory.GetFiles(winTemp, "*", SearchOption.TopDirectoryOnly).Take(100))
                            {
                                try { var info = new FileInfo(f); totalFreed += info.Length; File.Delete(f); totalItems++; }
                                catch { }
                            }
                        }

                        // 4. Prefetch
                        var prefetch = @"C:\Windows\Prefetch";
                        if (Directory.Exists(prefetch))
                        {
                            TxtLoading.Text = "Đang dọn Prefetch...";
                            foreach (var f in Directory.GetFiles(prefetch, "*", SearchOption.TopDirectoryOnly).Take(100))
                            {
                                try { var info = new FileInfo(f); totalFreed += info.Length; File.Delete(f); totalItems++; }
                                catch { }
                            }
                        }

                        SafeHideLoading();

                        if (totalFreed > 0)
                            MessageBox.Show($"Đã dọn ổ C: thành công!\nGiải phóng: {ScanItem.FormatSize(totalFreed)}\n{totalItems} items đã xóa.",
                                "Dọn Ổ C:", MessageBoxButton.OK, MessageBoxImage.Information);
                        else
                            MessageBox.Show("Không tìm thấy file nào để dọn.\nThử dùng Disk Analyzer để tìm file lớn.",
                                "Dọn Ổ C:", MessageBoxButton.OK, MessageBoxImage.Information);

                        await RefreshDashboardAsync();
                    }
                    catch (Exception ex) { SafeHideLoading(); MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButton.OK, MessageBoxImage.Error); }
                    finally { diskBtn.IsEnabled = true; }
                };
                panel.Children.Add(diskBtn);
            }
        }
        catch (Exception ex) { SafeHideLoading(); panel.Children.Add(new TextBlock { Text = $"Lỗi: {ex.Message}", Foreground = Danger }); }
    }

    private async Task BuildStartupTab(StackPanel panel)
    {
        ShowLoading("Đang tải danh sach khoi dong...");
        try
        {
            var entries = await _perfAnalyzer.GetStartupEntriesAsync();
            SafeHideLoading();
            panel.Children.Add(Label($"{entries.Count} ung dung khoi dong cung Windows.", Secondary));
            foreach (var entry in entries)
            {
                var color = entry.Impact switch { "High" => Danger, "Medium" => Warning, _ => Muted };
                var card = StyledCard(padding: new(10, 6, 10, 6), margin: new(0, 0, 0, 4));
                var row = new StackPanel { Orientation = Orientation.Horizontal };
                row.Children.Add(new TextBlock { Text = $"  ", FontSize = 12, Foreground = color, VerticalAlignment = VerticalAlignment.Center });
                row.Children.Add(new StackPanel
                {
                    Children =
                    {
                        new TextBlock { Text = entry.Name, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = Primary },
                        new TextBlock { Text = $"Tac dong: {entry.Impact}  |  {entry.Publisher}", FontSize = 10.5, Foreground = Secondary }
                    }
                });
                card.Child = row;
                panel.Children.Add(card);
            }
        }
        catch (Exception ex) { SafeHideLoading(); panel.Children.Add(new TextBlock { Text = $"Lỗi: {ex.Message}", Foreground = Danger }); }
    }

private void UpdatePerfLabels()
    {
        if (_perfVM == null) return;
        // Perf labels updated via Dispatcher — UI controls read _perfVM properties
    }
}
