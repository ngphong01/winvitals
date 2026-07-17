<!-- markdownlint-disable MD033 MD041 -->
<div align="center">
  <img src="docs/images/logo.png" alt="Windows Health Manager" width="80"/>

  # Windows Health Manager

  **Open-source disk cleaner & system optimizer for Windows developers.**

  [![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)]()
  [![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows)]()
  [![License](https://img.shields.io/badge/license-MIT-green)]()
  [![Tests](https://img.shields.io/badge/tests-38%20passing-brightgreen)]()
  [![PRs Welcome](https://img.shields.io/badge/PRs-welcome-ff69b4)]()

  <br/>

  **[Features](#features) • [Quick Start](#quick-start) • [Documentation](#documentation) • [Contributing](#contributing)**

  <br/>
</div>

---

## Overview

Windows Health Manager helps developers and power users reclaim disk space, monitor system performance, and keep their Windows machines clean — without ads, upsells, or shady telemetry.

Unlike traditional cleaners (CCleaner, Wise Care), this tool is **developer-aware**: it understands `node_modules`, `.gradle`, Docker build cache, and other dev-specific clutter that generic cleaners miss.

### Why another cleaner?

| Problem | Solution |
|---|---|
| CCleaner has ads + malware history (2017) | **100% open source, MIT license** |
| Most cleaners ignore dev caches | **Developer-specific scan** (npm, pip, NuGet, Docker, Gradle) |
| Opaque deletion rules | **Preview before delete** + explain why each file can be removed |
| Telemetry / phone-home | **No telemetry, no ads, no "Pro" upsell** |

## Features

### 🧹 Smart Cleaner

| Mode | What it cleans | Safety |
|---|---|---|
| **Quick Clean** | Temp files, logs, recycle bin, crash dumps, prefetch, thumbnails | ✅ Fully safe |
| **Deep Clean** | Windows Update cache, old installers, app leftovers | ⚠️ Preview recommended |
| **Dev Cache Cleaner** | `node_modules`, `build/`, `.next`, `__pycache__`, `target/`, `.gradle`, `vendor/` | ✅ Configurable |

### 📊 Disk Analyzer

- Scan folder sizes with interactive treemap visualization
- Identify what's taking space at a glance
- Built-in risk assessment: files are classified as Safe / Low / Medium / High / Critical

### ⚡ Performance Monitor

- Real-time CPU, RAM, and disk usage chart
- Top processes by memory consumption
- Startup manager — view and disable startup entries
- SMART disk health check (WMI)

### 🔬 Specialized Tools

- **Duplicate Finder** — compare files by SHA256 hash, skip files under 1MB
- **Large File Finder** — find files > 100MB across your drives
- **Orphan Detector** — find leftovers from uninstalled applications
- **Quarantine** — safe move + restore with 14-day auto-expiry
- **Rules Engine** — customizable JSON rules for file classification

## Tech Stack

| Layer | Technology | Purpose |
|---|---|---|
| **Runtime** | .NET 9.0 WPF | Desktop UI |
| **Database** | Microsoft.Data.Sqlite | Embedded storage |
| **Logging** | Serilog | File + console logging |
| **System** | PerformanceCounter, WMI, P/Invoke | CPU, RAM, disk, SMART monitoring |
| **Testing** | xUnit + FluentAssertions | Unit tests (38 passing) |

### Project Structure

```
src/
├── App.Core/          # Enums, models, interfaces
├── App.Cleaner/       # Rule engine, risk engine, cleaners
├── App.Scanner/       # Disk, large file, orphan, duplicate scanners
├── App.Storage/       # SQLite database provider
├── App.Performance/   # Performance analyzer, SMART disk checker
├── App.UI/            # WPF frontend
└── App.Tests/         # 38 unit tests
```

## Quick Start

### Prerequisites

- Windows 10 (1809+) or Windows 11
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- 200 MB free disk space

### Build & Run

```bash
# Restore, build, and run
dotnet restore
dotnet build
dotnet run --project src/App.UI/App.UI.csproj

# Run tests
dotnet test src/App.Tests/App.Tests.csproj
```

### Publish (single-file)

```bash
dotnet publish src/App.UI -c Release -r win-x64 --self-contained -o ./publish
```

## Rule System

Files are evaluated against a priority-based rule engine:

| Priority | Source | Example |
|---|---|---|
| 100 | Built-in (hard-coded) | System32 → **Block**, <br/>`.db`/`.env` → **Block** |
| 80–99 | JSON rules (`rules/*.json`) | Temp files → **SafeDelete**, <br/>Old installers → **WarnDelete** |
| 0 | Default fallback | Unknown → **WarnDelete** |

Custom rules are JSON files placed in the `rules/` directory:

```json
[
  {
    "id": "my_app_cache",
    "name": "MyApp Cache",
    "pathPatterns": ["**\\MyApp\\Cache\\**"],
    "extensions": [".tmp", ".cache"],
    "action": "SafeDelete",
    "risk": "Low",
    "priority": 80,
    "enabled": true
  }
]
```

## Screenshots

> Screenshots coming soon — the project is under active development.

```
Dashboard    → Health score, drive usage, recent activity
Disk Analyzer → Folder size treemap, risk-colored items
Cleaner      → Quick/Deep/Preview modes
Performance  → Real-time CPU/RAM/Disk chart
```

## Roadmap

- [x] Prototype (code-behind, SQLite)
- [x] Disk scanner + treemap visualization
- [x] Rule engine with priority matching
- [x] Developer cache cleaner (12 cache types)
- [x] Real-time performance monitoring
- [ ] WinVitals v2 — MVVM, LiteDB, LiveCharts
- [ ] Portable single-file publish
- [ ] CLI mode for automation
- [ ] MSIX installer

## Contributing

Contributions are welcome! Here's how:

1. Fork the repo
2. Create a branch: `feat/your-feature` or `fix/your-fix`
3. Write tests for new code
4. Run `dotnet test` before submitting
5. Open a PR with a clear description

Please follow [Conventional Commits](https://www.conventionalcommits.org/) for commit messages.

## License

**MIT** — free for personal and commercial use.

---

<div align="center">
  <sub>Built with .NET 9 + WPF + SQLite</sub>
  <br/>
  <sub>🇻🇳 Made in Vietnam</sub>
</div>
