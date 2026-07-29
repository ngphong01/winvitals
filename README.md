<!-- markdownlint-disable MD033 MD041 -->
<div align="center">
  <img src="src/App.UI/Resources/app.png" alt="WinHealth" width="80"/>

  # Windows Health Manager

  **Developer Disk Manager — không phải CCleaner clone.**

  [![.NET](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet)]()
  [![Windows](https://img.shields.io/badge/Windows-10%2F11%20x64-0078D4?logo=windows)]()
  [![License](https://img.shields.io/badge/license-MIT-green)]()
  [![Tests](https://img.shields.io/badge/tests-113%20passed-brightgreen)]()
  [![Release](https://img.shields.io/badge/release-v2.0.0-blue)]()

  <br/>

  > **Win 10/11 x64 · Portable 129 MB · Không cần cài .NET · 1 file duy nhất · MIT**

  **[Tải về](#tải-về) · [Tính Năng](#tính-năng-cốt-lõi) · [Cài Đặt](#cài-đặt-dev) · [Kiến Trúc](#kiến-trúc) · [An Toàn](#an-toàn)**

</div>

---

## Tải Về

| Cách | Link | Yêu cầu |
|---|---|---|
| **Portable .exe** | [WinHealth.exe](../../releases/latest) | Windows 10/11 x64, không cần .NET |
| **Source** | `git clone https://github.com/ngphong01/winvitals.git` | .NET 9 SDK |

```text
Tải WinHealth.exe (129 MB) → Double-click → Lần đầu hỏi "Cài vào máy?"
→ Yes: tự cài vào Program Files + tạo shortcut Desktop
→ No:  chạy portable trực tiếp, không để lại gì trên máy
```

---

---

## Tại Sao WinHealth?

CCleaner, BleachBit, Wise Care là cleaner cho người dùng phổ thông. Họ không hiểu `node_modules`, Docker cache hay NuGet packages là gì.

| | CCleaner | WinHealth |
|---|---|---|
| Quét `node_modules`, `.next`, `.gradle`... | ❌ | ✅ |
| 27 loại package manager cache | ❌ | ✅ |
| Stale project detection | ❌ | ✅ |
| Rule Engine mở rộng bằng JSON | ❌ | ✅ |
| Quarantine 14 ngày + Undo (Ctrl+Z) | ❌ | ✅ |
| Rule Packs cộng đồng | ❌ | ✅ |
| Open source, không telemetry | ❌ | ✅ MIT |

---

## Tính Năng Cốt Lõi

### Dashboard — Mở App Là Biết Làm Gì

`Ctrl+1`

- **Health Score 0-100** — donut chart, đánh giá sức khỏe hệ thống
- **Cleanup Potential** — ước tính "⚡ Có thể giải phóng ~X GB", nút **Sửa Ngay** 1-click
- **Drive Cards** — usage bar xanh/vàng/đỏ, click để phân tích chi tiết
- **PerfChart** — CPU/RAM/Disk real-time 60 giây
- **Issues** — vấn đề cần chú ý từ PerformanceAnalyzer

### Cleaner — Dọn Nhanh, An Toàn, Có Undo

`Ctrl+3`

| Chế độ | Thời gian | An toàn |
|---|---|---|
| **Quick Clean** — Temp, Logs, Recycle Bin, Crash Dumps | ~30s | ✅ |
| **Deep Clean** — Windows Update, app leftovers, file mồ côi | 2-5 phút | ⚠️ Review |
| **Preview** — Dry-run, xem trước không xóa | ~30s | ✅ |

- **Before/After**: `C: 50.2 GB → 53.1 GB (+2.9 GB)` hiển thị sau mỗi lần dọn
- **Undo (Ctrl+Z)** — hoàn tác trong 10 giây
- **7 Presets**: Quick System, Developer, Deep Monthly, Drive C:, Privacy, Gamer, Designer

### Deep Scan — 6 Công Cụ Cho Developer

`Ctrl+4`

| Tab | Tìm gì |
|---|---|
| **File Lớn** | >100MB, gợi ý xóa theo loại |
| **File Trùng** | SHA256 hash, giữ 1 bản |
| **File Mồ Côi** | Leftovers sau gỡ cài đặt |
| **Cache Dev** | `node_modules`, `.next`, `__pycache__`, `.gradle`... |
| **Package Cache** | 27 loại: npm, pip, NuGet, Cargo, Go, Gradle, Docker... |
| **Stale Projects** | Project >60 ngày không commit, detect 17 framework |

### Quarantine — An Toàn Trên Hết

`Ctrl+6`

```
Safe → Low → Medium → High → Critical → Unknown
  ↓                          ↓
Xóa luôn              Cách ly 14 ngày
                     → Restore hoặc Xóa vĩnh viễn
```

### AutoClean — Đặt Lịch Rồi Quên

`Ctrl+7`

- Chọn preset → chọn recurrence → chọn giờ → **Tạo Lịch**
- Tích hợp Windows Task Scheduler (`schtasks.exe`)
- Danh sách lịch đã đặt + nút xóa

### Settings — Cấu Hình & Công Cụ

`Ctrl+8`

| Tab | Dành cho |
|---|---|
| 📋 **Quy Tắc** | Xem & quản lý rule engine |
| 📦 **Packs** | Tải community rule packs 1-click |
| ⬇ **VS Code** | Cài 100+ extensions — checkbox → 1 nút → cài thẳng |
| 📄 **Hoạt Động** | Real-time activity log — biết app vừa làm gì |
| ℹ️ **About** | Version, update check, báo cáo tuần |

### VS Code Extensions — Cài Hàng Loạt 1 Nút

- **100+ extensions** phân loại: .NET, Git, AI, Web, Python, Rust, Go, JVM, DevOps, Database...
- Checkbox chọn → **Cài Đã Chọn** → `code --install-extension` tự động
- Progress bar Steam-style: tên + % + ETA
- Tự detect extension đã cài — hiển thị ✅
- Không cần mở terminal, không cần copy-paste

---

## Cài Đặt (Dev)

```bash
git clone https://github.com/ngphong01/winvitals.git
cd winvitals

dotnet build WindowsHealthManager.sln
dotnet test WindowsHealthManager.sln    # 113 tests
dotnet run --project src/App.UI

# Publish single-file (129 MB, self-contained)
dotnet publish src/App.UI -c Release -o ./publish
```

---

## CLI

```bash
WinHealth.exe --cli <command> [--json]
```

| Lệnh | Mô tả |
|---|---|
| `scan` | Quét ổ đĩa (`-d`, `-t`, `--git`, `--empty`) |
| `clean` | Dọn dẹp (`-l`, `-P`, `--preview`, `-y`) |
| `report` | Báo cáo hệ thống (`-w` weekly) |
| `suggest` | Gợi ý dọn dẹp thông minh |
| `status` | Trạng thái hệ thống nhanh |
| `rules` | Danh sách rules đã load |
| `schedule` | Quản lý SmartSchedule |
| `version` | Phiên bản |

---

## Kiến Trúc

```
Scanner → RuleEngine → RiskEngine → Cleaner/Preview → Quarantine → Before/After
```

```
src/
├── App.Core/         # Interfaces, Models, Enums, ActivityLogger, VscodeHelper
├── App.Cleaner/       # RuleEngine, RiskEngine, Cleaners, Scheduler, Quarantine
├── App.Scanner/       # 12 Scanners + ScannerService
├── App.Performance/   # PerformanceAnalyzer, SystemDiagnostic, SystemBooster
├── App.Storage/       # LiteDB + 5 Repositories + UnitOfWork
├── App.UI/            # WPF (MainWindow, HealthRing, PerfChart, TreemapControl)
└── App.Tests/         # xUnit + FluentAssertions (113 tests)
```

| Layer | Tech |
|---|---|
| Runtime | .NET 9.0 WPF |
| Database | LiteDB (embedded NoSQL, single-file) |
| Logging | Serilog (daily rolling, 30-day) |
| System | PerformanceCounter, WMI, P/Invoke |
| Testing | xUnit + FluentAssertions |
| Theme | Tokyo Night (`#0D0D1A` bg, `#7aa2f7` accent) |

---

## An Toàn

| Lớp | Cơ chế |
|---|---|
| **UAC** | `requireAdministrator` — tự động prompt |
| **Protected Paths** | System32, Program Files, .git, .env — không bao giờ bị đụng |
| **Risk Engine** | 6 mức — đánh giá từng file trước khi xóa |
| **Preview** | Dry-run trước khi xóa thật |
| **Quarantine** | File High → cách ly 14 ngày thay vì xóa thẳng |
| **Undo** | Ctrl+Z — snapshot trước khi dọn |
| **Tick-to-Confirm** | Xóa vĩnh viễn cần checkbox xác nhận |

---

## License

MIT — tự do sử dụng, sửa đổi, phân phối.

<div align="center">
  <sub>🇻🇳 Made in Vietnam — Đào Văn Phong · <a href="https://github.com/ngphong01/winvitals">github.com/ngphong01/winvitals</a></sub>
</div>
