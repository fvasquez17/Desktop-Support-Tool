# 🖥️ Desktop Support Tool

**Enterprise IT Diagnostics & Remediation Platform for Windows 11**

A full-featured, offline-first desktop support utility built for enterprise IT teams. Provides real-time system health monitoring, an AI-powered troubleshooting agent, and 24+ one-click automated fixes — all running locally with zero external dependencies.

![.NET 9.0](https://img.shields.io/badge/.NET-9.0-512BD4?logo=dotnet&logoColor=white)
![WPF](https://img.shields.io/badge/WPF-Desktop-0078D4?logo=windows&logoColor=white)
![License](https://img.shields.io/badge/License-MIT-green)
![Platform](https://img.shields.io/badge/Platform-Windows%2011-0078D4?logo=windows11&logoColor=white)

---

## ✨ Features

### 🔧 One-Click Troubleshooting (24+ Fixes)

| Category | Fixes |
|---|---|
| **Application Resets** | Teams, Office, Citrix, Browser Caches |
| **Email & Sync** | Outlook (new profile creation), OneDrive sync repair |
| **Credentials** | Clear Credential Manager (Office 365/SSO) |
| **Network & Printing** | Full network stack reset, Print Spooler restart, Clear print queue |
| **System Actions** | GPUpdate, Restart Explorer, Clear Temp Files, Rebuild Icon Cache, Fix Start Menu |
| **Device Management** | Intune MDM sync, SCCM client repair (CCMExec + policy triggers) |
| **File Associations** | Reset default apps, Repair broken .pdf/.docx/.xlsx associations |
| **User Profile** | Fix Temporary Profile (.bak registry repair), Scan profile cache |
| **Advanced Diagnostics** | SFC Scan, DISM RestoreHealth |

### 🤖 AI Support Agent

An offline, context-aware troubleshooting assistant that:

- Understands **natural language** — type *"Outlook keeps crashing"* or *"my files won't open"*
- Runs **live system health scans** (CPU, RAM, disk, network, DNS, services, uptime)
- Matches issues across **17 diagnostic rule categories** with contextual explanations
- Offers **one-click fix buttons** directly in the chat interface
- Maintains **session memory** for follow-up questions

### 📊 System Dashboard

Real-time monitoring panels for:

- **System Info** — OS, hostname, domain, uptime
- **Hardware** — CPU/RAM/Disk usage with live gauges
- **Network** — Adapter status, IP configuration, connectivity check
- **Drivers** — Full inventory with problem device detection
- **Printers** — Add, remove, set default, manage queue
- **Services** — Start, stop, restart any Windows service
- **Activity Log** — Full audit trail with export to file

### 🔒 Enterprise Security & Compliance

- **Zero external data transmission** — fully offline, no telemetry, no API calls
- **Least-privilege model** — runs as standard user; admin actions use per-action UAC
- **Tamper-resistant audit logging** — dual-write to local files + Windows Event Log
- **Identity tracking** — every action logs `DOMAIN\Username`, machine name, timestamp, and outcome
- **90-day log retention** — automated cleanup with configurable retention period
- **SIEM-ready** — Windows Event Log entries forwardable via WEF/Splunk/Sentinel

Compliance mappings documented for:
- **NIST SP 800-53 Rev. 5** — AC-3, AC-6, AU-2/3/4/5/9/11/12, CM-7, IA-2, SC-7, SI-4
- **CIS Controls v8** — 4.1, 5.4, 6.1, 8.2/8.5/8.9/8.11, 16.1
- **HIPAA § 164.312** — Access Control, Unique User ID, Audit Controls, Integrity, Authentication

---

## 📸 Screenshots

> *Coming soon — run the app to see the full UI*

---

## 🚀 Getting Started

### Prerequisites

- **Windows 10/11** (64-bit)
- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later

### Build & Run

```bash
# Clone the repository
git clone https://github.com/yourusername/Desktop-Support-Tool.git
cd Desktop-Support-Tool

# Build
dotnet build

# Run
dotnet run
```

### Publish as Single-File EXE

```bash
# Self-contained (no .NET install required on target machines)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true -o ./publish

# Framework-dependent (smaller size, requires .NET 9.0 runtime)
dotnet publish -c Release -r win-x64 --self-contained false /p:PublishSingleFile=true -o ./publish
```

The output EXE will be in the `./publish` folder.

---

## 🏗️ Architecture

```
Desktop Support Tool/
├── Helpers/
│   ├── ElevationHelper.cs        # UAC / admin detection
│   ├── PowerShellRunner.cs       # PowerShell execution engine
│   └── ProcessHelper.cs          # Process management utilities
├── Models/
│   ├── ActionResult.cs           # Success/failure result wrapper
│   └── LogEntry.cs               # Audit log entry (NIST AU-3 compliant)
├── Services/
│   ├── LoggingService.cs         # Dual-write logging (file + Event Log)
│   ├── TroubleshootBot.cs        # AI diagnostic engine (17 rule categories)
│   └── TroubleshootService.cs    # 24+ remediation scripts
├── UI/
│   ├── MainWindow.xaml/.cs       # Shell, navigation, WndProc hooks
│   ├── Styles/
│   │   └── Theme.xaml            # Dark mode design system
│   └── Views/
│       ├── AgentBotView.xaml/.cs  # AI chat interface
│       ├── DashboardView.xaml/.cs # System info & health
│       ├── DriversView.xaml/.cs   # Driver inventory
│       ├── LogView.xaml/.cs       # Activity log viewer
│       ├── NetworkView.xaml/.cs   # Network diagnostics
│       ├── PrinterView.xaml/.cs   # Printer management
│       ├── ServicesView.xaml/.cs  # Windows services
│       └── TroubleshootView.xaml/.cs # One-click fix cards
├── Docs/
│   ├── USER_GUIDE.md             # End-user documentation
│   ├── DEVELOPER_GUIDE.md        # Developer & modification guide
│   ├── SecurityCompliance.html   # NIST/CIS/HIPAA compliance mapping
│   ├── UserGuide.html            # Print-ready user guide
│   └── DeveloperGuide.html       # Print-ready developer guide
└── DesktopSupportTool.csproj     # .NET 9.0 project file
```

### Key Design Decisions

| Decision | Rationale |
|---|---|
| **WPF over WinUI 3** | Broader Windows 10/11 compatibility, no MSIX packaging requirement |
| **Static services** | Stateless remediation methods — easier to test and reference from bot rules |
| **PowerShell scripts** | Leverage the full Windows management surface without P/Invoke complexity |
| **Per-action UAC** | Follows NIST AC-6 least privilege — app never runs with persistent admin |
| **Dual-write logging** | Local files for the UI + Event Log for tamper-resistance and SIEM forwarding |
| **Offline AI** | Rule-based diagnostics with system context — no cloud dependency, no data exfiltration risk |

---

## 🏢 Enterprise Deployment

The tool supports three deployment methods for large-scale rollout:

### SCCM / MECM
```powershell
# Detection method (registry-based)
dotnet publish -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true -o \\server\share\DST

# Install command
xcopy "\\server\share\DST" "C:\Program Files\DesktopSupportTool\" /E /I /Y

# Detection: File exists
# C:\Program Files\DesktopSupportTool\DesktopSupportTool.exe
```

### Microsoft Intune (Win32 App)
```powershell
# 1. Package with IntuneWinAppUtil
IntuneWinAppUtil.exe -c .\publish -s DesktopSupportTool.exe -o .\intune

# 2. Upload the .intunewin file to Intune portal
# 3. Install command: xcopy "%~dp0*" "C:\Program Files\DesktopSupportTool\" /E /I /Y
# 4. Detection: File exists at C:\Program Files\DesktopSupportTool\DesktopSupportTool.exe
```

### Group Policy (Startup Script)
```powershell
# deploy.ps1 — place on network share, assign via GPO
$source = "\\fileserver\Software\DesktopSupportTool"
$dest = "C:\Program Files\DesktopSupportTool"

if (-not (Test-Path "$dest\DesktopSupportTool.exe")) {
    Copy-Item -Path "$source\*" -Destination $dest -Recurse -Force
}
```

> See [Developer Guide](Docs/DEVELOPER_GUIDE.md) for full deployment instructions including auto-start, code signing, and AppLocker/WDAC allowlisting.

---

## 📖 Documentation

| Document | Description |
|---|---|
| [User Guide](Docs/USER_GUIDE.md) | How to use every feature of the tool |
| [Developer Guide](Docs/DEVELOPER_GUIDE.md) | Architecture, adding new fixes, theming, build commands |
| [Security & Compliance](Docs/SecurityCompliance.html) | NIST/CIS/HIPAA mapping with implementation details |

---

## 🛠️ Tech Stack

| Component | Technology |
|---|---|
| **Language** | C# 13 |
| **Runtime** | .NET 9.0 |
| **UI Framework** | WPF (Windows Presentation Foundation) |
| **System APIs** | WMI, Win32 P/Invoke, COM Interop |
| **Automation** | PowerShell 5.1 / 7+ |
| **Logging** | Custom dual-write (file + Windows Event Log) |

---

## 🔐 Security

This tool is designed for deployment in regulated environments (healthcare, finance, government):

- **No network calls** — only ICMP ping and DNS resolve for health checks
- **No data collection** — zero telemetry, zero analytics, zero cloud
- **No credential storage** — credential actions only *clear* cached tokens
- **No PHI/PII processing** — system utility only, does not access clinical or patient data
- **Tamper-resistant logs** — Windows Event Log is SYSTEM-protected, standard users cannot modify
- **Code signing ready** — see [Security Compliance doc](Docs/SecurityCompliance.html) for signing instructions

---

## 🤝 Contributing

1. Fork the repository
2. Create a feature branch (`git checkout -b feature/new-fix`)
3. Add your fix to `TroubleshootService.cs`
4. Add a UI card to `TroubleshootView.xaml`
5. Add a click handler to `TroubleshootView.xaml.cs`
6. (Optional) Add a bot rule to `TroubleshootBot.cs`
7. Test with `dotnet run`
8. Submit a pull request

See [Developer Guide](Docs/DEVELOPER_GUIDE.md) for detailed instructions on adding new fixes.

---

## 📄 License

This project is licensed under the MIT License — see the [LICENSE](LICENSE) file for details.

---
