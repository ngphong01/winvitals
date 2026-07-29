<!-- markdownlint-disable MD033 MD041 -->
<div align="center">
  <img src="src/App.UI/Resources/app.png" alt="WinHealth" width="80"/>

  # Windows Health Manager

  **Disk cleaner built for developers.**

  [![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)]()
  [![Windows](https://img.shields.io/badge/Windows-10%2F11-0078D4?logo=windows)]()
  [![License](https://img.shields.io/badge/license-MIT-green)]()
  [![Tests](https://img.shields.io/badge/tests-113%20passed-brightgreen)]()
  [![Release](https://img.shields.io/badge/release-v2.0.1-blue)]()

</div>

---

## Download

| Platform | File | Size |
|---|---|---|
| Windows 10/11 x64 | [WinHealth.exe](../../releases/latest/download/WinHealth.exe) | 129 MB |
| Windows 10 x86 | [WinHealth-x86.exe](../../releases/latest/download/WinHealth-x86.exe) | 120 MB |

Portable — no install, no .NET runtime required. Double-click to run.

---

## Features

| Category | Details |
|---|---|
| **Dashboard** | Health score, drive usage, real-time CPU/RAM/Disk chart, one-click fix suggestions |
| **Quick Clean** | Temp files, logs, recycle bin, browser cache, crash dumps — ~30s |
| **Deep Scan** | Large files (>100 MB), duplicate files (SHA256), orphaned files, dev caches (`node_modules`, `.next`, `__pycache__`, `.gradle`...), 27 package manager caches, stale projects |
| **Quarantine** | Risk-based: safe files deleted immediately, high-risk files isolated 14 days before permanent deletion |
| **Undo** | `Ctrl+Z` — restore within 10 seconds after cleaning |
| **Scheduler** | Windows Task Scheduler integration — set recurring cleanups by preset + time |
| **Rule Engine** | JSON-based rules, extensible via community rule packs |
| **VS Code Extensions** | Batch-install 100+ extensions by category with progress tracking |
| **Activity Log** | Real-time log of all app actions |

---

## Dev Setup

```bash
git clone https://github.com/ngphong01/winvitals.git
cd winvitals

dotnet build WindowsHealthManager.sln
dotnet test WindowsHealthManager.sln      # 113 tests
dotnet run --project src/App.UI

# Publish
dotnet publish src/App.UI -c Release -o ./publish          # x64
dotnet publish src/App.UI -c Release -r win-x86 -o ./publish-x86  # x86
```

---

## Architecture

```
Scanner → RuleEngine → RiskEngine → Cleaner → Quarantine
```

```
src/
├── App.Core/         # Interfaces, models, enums
├── App.Cleaner/      # Rule engine, risk engine, cleaners, scheduler
├── App.Scanner/      # 12 scanners + scanner service
├── App.Performance/  # Performance analyzer, system diagnostic, booster
├── App.Storage/      # LiteDB + repositories + unit of work
├── App.UI/           # WPF UI (Dashboard, charts, controls)
└── App.Tests/        # xUnit + FluentAssertions (113 tests)
```

| Layer | Tech |
|---|---|
| Runtime | .NET 9.0 WPF |
| Database | LiteDB (embedded NoSQL) |
| Logging | Serilog |
| System | PerformanceCounter, WMI, P/Invoke |
| Testing | xUnit + FluentAssertions |

---

## Safety

- **UAC** — administrator prompt required before any system change
- **Protected paths** — System32, Program Files, `.git`, `.env` are never touched
- **Preview mode** — dry-run before actual deletion
- **Risk levels** — each file scored before deletion; high-risk files quarantined
- **Tick-to-confirm** — permanent deletion requires checkbox confirmation

---

## License

MIT

<div align="center">
  <sub><a href="https://github.com/ngphong01/winvitals">github.com/ngphong01/winvitals</a></sub>
</div>
