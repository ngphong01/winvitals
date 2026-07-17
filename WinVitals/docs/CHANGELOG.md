# WinVitals Changelog

## v1.0.0 (2026-07-10) — Production Release

### 🎉 New

- **Onboarding wizard** — 5-step first-run experience (language, theme, privacy, first scan, tips)
- **Empty state illustrations** — Friendly placeholders when lists are empty
- **Loading skeletons** — Shimmer animations while data loads
- **Offline help docs** — Embedded Markdown docs viewer (EN/VI), F1 shortcut
- **Auto-update** — Velopack integration with in-app update UI
- **Telemetry opt-in** — Anonymous usage stats, privacy-first design
- **Crash reporter** — Unhandled exception handler with redaction and user consent

### ✨ Features (from all iterations)

- Multi-preset scanning (Quick, Deep, Safe, Developer)
- Smart rule engine with glob patterns, built-in + custom rules
- Quarantine with restore, auto-janitor, retention policies
- Real-time performance monitoring (CPU, RAM, Disk, SMART)
- Process manager with safe termination
- Statistics dashboard with trends and insights
- i18n (English / Tiếng Việt)
- Command palette (Ctrl+K) with keyboard navigation
- Settings with scheduling, exclusions, privacy controls
- Clean architecture: Core → Services → App, DI, async streaming

### 🔧 Technical

- .NET 8.0 WPF, Catppuccin Mocha theme
- LiteDB embedded database, Serilog logging
- CommunityToolkit.Mvvm, LiveCharts
- xUnit + FluentAssertions (96 tests)
- BenchmarkDotNet performance suite
- Velopack auto-update delta patches

---

## v0.9.0 — Auto-update & Telemetry

- Velopack auto-update with GitHub Releases source
- UpdateHostedService: periodic check every 6 hours
- Telemetry infrastructure (ITelemetry, NullTelemetry, HttpTelemetry)
- Crash reporter with redaction and user consent dialog
- Privacy settings with telemetry opt-in toggle

## v0.8.0 — i18n & Command Palette

- English/Vietnamese localization via resx
- LocalizationService with T() method
- LocExtension for XAML markup
- Command palette (Ctrl+K) with search, keyboard nav
- About page with version, changelog, licenses
- Keyboard shortcuts Ctrl+1-8 for navigation

## v0.7.0 — Statistics Dashboard

- Statistics page with KPI cards and charts
- Clean session history and trends
- Streak calculator for consecutive clean streaks

## v0.6.0 — Rules Editor

- Rules management UI with CRUD operations
- Rule test runner for previewing matches
- Import/export rules functionality
- Built-in vs custom rule distinction

## v0.5.0 — Settings & Scheduling

- Settings page with appearance, quarantine, scheduling
- Exclusion patterns management
- Scheduled automatic cleanup with configurable frequency
- System tray, minimize to tray, start with Windows

## v0.4.0 — Process Manager & Disk Health

- Process list with CPU, RAM, disk usage per process
- Safe process termination
- SMART disk health monitoring
- Drive usage visualization

## v0.3.0 — Clean & Quarantine Engine

- CleanService with preview and execute modes
- QuarantineService with atomic file moves
- Quarantine page with restore/purge
- Quarantine janitor for automatic cleanup

## v0.2.0 — Smart Rule Engine

- RuleEvaluator with OR logic
- GlobMatcher with cached regex compilation
- Built-in rules (7 rules)
- Risk Engine scoring

## v0.1.0 — Initial Skeleton

- Project structure: Core, Services, App, Tests
- WPF shell with sidebar navigation
- Dashboard with placeholder widgets
- LiteDB integration, DI setup
