<div align="center">

# Klyr

**Windows PC optimizer — 40 optimizations across 5 modules + hardware monitoring + 5 advanced tools. EN + FR, 100% local, no telemetry.**

🌐 **English** · [Français](README.md)

![Klyr Dashboard](Assets/Branding/screenshot-dashboard.png)

[![Windows](https://img.shields.io/badge/Windows-10%20%7C%2011-0078D6?style=flat-square&logo=windows)](https://www.microsoft.com/windows)
[![.NET](https://img.shields.io/badge/.NET-10-512BD4?style=flat-square&logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/license-See%20LICENSE.txt-blue?style=flat-square)](LICENSE.txt)
[![Discord](https://img.shields.io/badge/Discord-Join-5865F2?style=flat-square&logo=discord&logoColor=white)](https://discord.com/invite/nPWU9cW3NG)
[![Published by](https://img.shields.io/badge/published_by-InfoZen_·_Yahya-4B8BF5?style=flat-square)](#)

</div>

---

## Why Klyr

Over the months, Windows accumulates heavy settings, useless services, telemetry, and optimizations disabled by default. Klyr bundles **40 validated tweaks** across 5 modules, without installing 50 separate utilities and without sending a single byte of data over the internet.

- ⚡ **Measurable**: +377 3DMark points measured after the Gaming module
- 🔒 **100% local**: no telemetry, no tracking, no account
- ↩️ **Reversible**: automatic restore point + targeted backups of system files
- 📖 **Open-source**: you can read every line before installing

---

## What's new in v2.3.0

```
                 KLYR  v2.2.0  ─────────────────►  v2.3.0
 ┌────────────────────────────┐      ┌────────────────────────────────────────┐
 │ DASHBOARD                   │      │ DASHBOARD                               │
 │  • CPU % / RAM % / Disk     │  ──► │  • Circular Performance Score gauge     │
 │                             │      │  • GPU card (name / usage / temperature)│
 │                             │      │  • Live disk speed                      │
 ├────────────────────────────┤      ├────────────────────────────────────────┤
 │ MODULES (5)                 │      │ MODULES (5)  — unchanged                │
 │  Gaming · Old PC ·          │  ══  │  Gaming · Old PC · Cleaning ·           │
 │  Cleaning · Network ·       │      │  Network · Streaming                    │
 │  Streaming                  │      │                                        │
 ├────────────────────────────┤      ├────────────────────────────────────────┤
 │ TOOLS                       │      │ TOOLS  (+5 integrated as tabs)          │
 │  Terminal                   │  ──► │  Terminal · Uninstaller · winget upd.   │
 │                             │      │  · Disk Analyzer · Startup ·            │
 │                             │      │  Browsers                               │
 ├────────────────────────────┤      ├────────────────────────────────────────┤
 │ SYSTEM                      │      │ SYSTEM                                  │
 │  • Resizable window         │  ──► │  • Sidebar: active state + scrollable   │
 │  • FR / EN                  │      │  • Scheduled scans (Task Scheduler)     │
 │                             │      │  • GitHub auto-updater (opt-out)        │
 │                             │      │  • Auto-restart on language change      │
 └────────────────────────────┘      └────────────────────────────────────────┘
        4 tool modules                      real-time hardware monitoring
                                            + 5 tools inspired by Kudu/VoltAir
```

**In short**: v2.2.0 was an optimizer (5 modules). v2.3.0 becomes a **full suite** — real-time hardware monitoring (temperatures, Performance Score), 5 new integrated tools (uninstaller, winget, disk analyzer, startup, browsers), scheduled scans, and automatic updates.

---

## Installation

1. Download the latest release from [Releases](../../releases) → **`Klyr_Setup_v2.3.0.exe`**
2. Run the installer
3. The app installs to `C:\Program Files\Klyr` and a Desktop shortcut is created

The installer bundles the **.NET 10** runtime — no prerequisite on the target machine.

### SmartScreen note

Klyr is not signed with an EV certificate. Windows SmartScreen may show:

> *"Windows protected your PC"*

This is normal for any unsigned app. To continue:

1. Click **"More info"**
2. Click **"Run anyway"**

If Defender blocks the download: add the download folder to the exclusions, or download again.

---

## Modules

| Module | Count | Highlights |
|---|---|---|
| **Gaming / FPS** | 9 | High-perf power plan, Game DVR off, FPS unlock, TCP latency, kill parasitic processes |
| **Old PC** | 8 | Disable heavy services, telemetry, animations, Win32 RAM cleanup, smart SSD/HDD defrag |
| **Cleaning** | 8 | Disk cleanup, debloat, SFC/DISM, built-in Defender scan + quarantine, cache wipe |
| **Network** | 9 | Winsock/TCP reset, DNS auto-bench, ping optimization, speed test, hosts telemetry block |
| **Streaming / Creation** | 6 | Streamer mode (perf + latency), auto encoder CPU priority, Game Mode OFF, HAGS OFF, parasite killer, OBS cache cleanup |

Optimizations marked **"Advanced"** (uncertain gain or regression risk) are hidden by default. Enable them in Settings if you want to push further.

**Per-optimization cancellation**: each optimization shows a "Cancel" button while running that kills the underlying process (useful for long SFC/DISM/antivirus passes).

**Language**: EN/FR — the app follows your system culture by default (Settings → Language).

---

## Hardware monitoring & Performance Score

The Dashboard shows in real time (refreshed every 2 s) via **LibreHardwareMonitor**:

- **Performance Score /100** — overall score (CPU load + RAM + free disk space), color-coded
- **Dedicated GPU card** (name + usage + temperature, via NvAPI — all vendors)
- **Live disk** read/write speed
- CPU / RAM / disk with responsive bars

> The **GPU** temperature is shown via NvAPI. The **CPU** temperature is not displayed: on AMD/Intel it relies on a kernel driver (WinRing0) that Windows 11 Memory Integrity (HVCI) often blocks, making it unreliable — we prefer showing nothing over a misleading "0".

---

## Advanced tools

Accessible from the **Tools** section of the sidebar:

| Tool | Description |
|---|---|
| **Uninstaller** | Lists installed programs, uninstalls, and **detects leftover files** (folders + app data) to remove |
| **Updates** | Detects and bulk-installs software updates via **winget** |
| **Disk Analyzer** | Visualizes folder usage (proportional bars + drill-down) |
| **Startup** | Enables/disables startup programs (reversible, no deletion) |
| **Browsers** | Clears cache / cookies / history for Chrome, Edge, Firefox, Brave |

**Scheduled scans** (Settings → Scheduled scans): automatic daily/weekly/monthly cleanup via the Windows Task Scheduler, in the background without opening the app.

---

## Privacy & Security

Klyr makes **no network calls** except:
- Internet speed test (ping `1.1.1.1`, on user request)
- DNS benchmark (resolving `github.com`, `microsoft.com`, `cloudflare.com`)
- **Update check** (v2.3.0): a simple `GET` to the GitHub Releases API at startup to compare versions. **No data sent** (read-only), can be disabled in *Settings → Check for updates*.
- **Software Updater** (v2.3.0): `winget` downloads updates for the software **you select** (explicit action).

**No personal data is ever sent anywhere.** No analytics, no tracking, no account.

### Automatic backups before critical operations

| Action | Backup created |
|---|---|
| `net_reset` (Winsock/TCP reset) | `Documents\Klyr\Backups\Network\` |
| `net_reset_firewall` | Firewall rules export via `netsh advfirewall export` |
| `net_telemetry_block` (hosts edit) | Dated `hosts` copy |
| `clean_event_logs` | `.evtx` export before clearing |

Plus an automatic **system restore point** option before each admin optimization (configurable in Settings).

---

## Found a bug?

Klyr embeds two diagnostic tools in the `About` window:

### Copy system info
Copies a markdown block to the clipboard, to paste into a [GitHub Issue](../../issues):
```
**Klyr v2.3.0**
- OS: Windows 11 25H2
- CPU: AMD Ryzen 5 9600X
- RAM: 7.4 / 31.1 GB (24%)
- Disk C: : 765 / 930 GB free
- Architecture: x64
- Admin: yes
```

### Export report
Generates a `.zip` in `Documents\Klyr\Reports\` containing:
- `system_info.txt` full diagnostic (OS, CPU, RAM, sensors, settings, .NET version)
- `settings.json` your settings
- `logs/` all persistent logs
- `terminal_session.txt` session terminal buffer

Attach this zip to your bug report — you only need to describe the problem, the diagnostic does the rest.

**Privacy**: no personal data in the zip. You can open it before sending to verify.

---

## Measured results

| Tested config | Before | After | Gain |
|---|---|---|---|
| Ryzen 5 9600X, RTX 5060 Ti 16GB (full Gaming optimization) | 3DMark 14681 | 3DMark 15058 | **+377 pts** |

> Gains vary by configuration. The less optimized the baseline, the higher the margin.
> Optimizations marked "Advanced" can degrade some setups — use them with discernment.

---

## Architecture

```
Klyr/
├── Klyr.sln                          Visual Studio solution
├── Klyr.csproj                       Targets .NET 10 / WPF / x64
├── App.xaml / App.xaml.cs            Global resources + ErrorHandler init + culture
├── app.manifest                      UAC asInvoker + DPI PerMonitorV2
│
├── README.md / README.en.md          This page (FR / EN)
├── CHANGELOG.md                      Version history
├── SECURITY.md                       Vulnerability disclosure policy
├── LICENSE.txt                       Terms of use
├── .gitignore  /  .gitattributes
│
├── .github/
│   ├── ISSUE_TEMPLATE/               bug_report + feature_request + config.yml
│   └── copilot-instructions.md       GitHub Copilot hints
│
├── Assets/
│   ├── Icons/icon.ico                Multi-resolution icon (16/24/32/48/64/128/256)
│   └── Branding/                     Mark + wordmark SVG + PNG export + screenshot
│
├── Commands/                         RelayCommand + AsyncRelayCommand
├── Models/                           OptimizationItem, SystemInfoModel (+ sensors/score),
│                                     InstalledProgram, UpgradablePackage, DiskEntry, StartupEntry
├── Services/
│   ├── SystemService                 Win32 P/Invoke, CMD/PowerShell runner (CancellationToken kill), restore points
│   ├── HardwareMonitorService        v2.3.0 — LibreHardwareMonitor sensors (CPU/GPU temp, disk speed)
│   ├── PerformanceScoreService       v2.3.0 — weighted /100 score
│   ├── UninstallerService            v2.3.0 — installed programs + leftover detection
│   ├── WingetService                 v2.3.0 — software updater (winget parsing)
│   ├── DiskAnalyzerService           v2.3.0 — folder sizing (parallel recursive)
│   ├── StartupManagerService         v2.3.0 — Windows startup (reversible toggle)
│   ├── BrowserCleanerService         v2.3.0 — browser cache/cookies/history
│   ├── ScheduledScanService          v2.3.0 — Task Scheduler (schtasks)
│   ├── SilentCleanupService          v2.3.0 — headless cleanup (--scheduled-clean)
│   ├── UpdateService                 v2.3.0 — GitHub Releases update check (read-only)
│   ├── LogService                    Circular buffer (2000 entries) + auto-save + i18n categories
│   ├── SettingsService               JSON persistence in %AppData%\Klyr (language + theme + scans + toggles)
│   ├── ThemeService                  Dynamic Dark/Light switch (45 brushes)
│   ├── DiagnosticService             Diagnostic generation + zip export
│   ├── ErrorHandler                  Global crash handler + persistent crash reports
│   ├── AdminChecker                  WindowsPrincipal — admin rights check
│   ├── ProgressHelper                Progress animation (propagates CancellationToken)
│   ├── OptimizationBenchmarkService  Before/after CPU/RAM/disk snapshots
│   ├── OptimizationProfileService    Optimization metadata
│   ├── GamingOptimizations           Module 1 (9)   ── OldPcOptimizations  Module 2 (8)
│   ├── CleaningOptimizations         Module 3 (8 + antivirus quarantine)
│   ├── NetworkOptimizations          Module 4 (9)   ── StreamingOptimizations Module 5 (6)
│
├── Resources/                        FR/EN localization (Strings.resx + .en.resx + .Designer.cs)
├── Styles/                           Icons.xaml (Segoe Fluent Icons) + Animations.xaml
├── ViewModels/                       MainViewModel (IDisposable, nav cache, run all, CTS, OpenTool)
├── Views/                            MainWindow, SplashScreen, Settings, About, Legal,
│                                     Uninstaller, Updater, DiskAnalyzer, StartupManager,
│                                     BrowserCleaner  (v2.3.0)
└── Installer/                        BUILD_INSTALLER.bat + Klyr_Setup.nsi script
```

---

## Links

- [CHANGELOG.md](CHANGELOG.md) — version history
- [SECURITY.md](SECURITY.md) — vulnerability disclosure policy
- [LICENSE.txt](LICENSE.txt) — terms of use

---

<div align="center">

**Klyr** — Published by **InfoZen · Yahya** — 2026

</div>
