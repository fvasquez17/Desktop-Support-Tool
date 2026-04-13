# Desktop Support Tool — User Guide

> Enterprise-grade Windows desktop diagnostics, troubleshooting, and AI support agent.  
> **Version**: 1.0.0 · **Framework**: .NET 9.0 · **Platform**: Windows 10/11

---

## Getting Started

### Launching the App

The tool starts **minimized to the system tray** (notification area near the clock).

- **Single-click** the tray icon → Opens the main window
- **Right-click** the tray icon → Context menu with quick navigation
- **Close (✕)** the window → Minimizes back to tray (does NOT exit the app)
- To fully exit → Right-click tray icon → **Exit**

### Window Controls

| Button | Action |
|--------|--------|
| `—` (Minimize) | Minimize to taskbar |
| `□` (Maximize) | Toggle fullscreen / windowed (respects taskbar) |
| `✕` (Close) | Hide to system tray (app keeps running) |

---

## IT Support Agent 🤖

The built-in AI Support Agent is your first stop for troubleshooting. It diagnoses issues from plain English descriptions, runs **live system scans**, and can fix problems directly from the chat.

### How to Use

1. Click **"IT Support Agent"** in the sidebar (top item under SUPPORT)
2. **Type your problem** in the text box at the bottom:
   - *"Outlook keeps crashing"*
   - *"I can't print anything"*
   - *"My computer is really slow"*
   - *"No internet connection"*
   - *"Teams won't load"*
3. The agent will:
   - Scan your system in real-time (CPU, RAM, disk, internet, DNS, services)
   - Show a **health bar** with live metrics
   - Diagnose your issue with a confidence rating
   - Suggest **fixes** you can run with one click
4. Click any **▶ fix button** to execute it
5. Use **quick reply chips** (blue pill buttons) for fast follow-ups

### Scan System Button

Click the **"Scan System"** button in the header (or type *"scan my system"*) to run a full health check without describing a problem. The agent proactively finds issues.

### What Gets Scanned

| Metric | What It Checks | Alert Levels |
|--------|---------------|--------------|
| **CPU** | Current processor usage | ⚠️ >70% · 🔴 >90% |
| **RAM** | Memory usage percentage | ⚠️ >70% · 🔴 >85% |
| **Disk** | System drive free space | ⚠️ >75% full · 🔴 >90% full |
| **Internet** | Ping to 8.8.8.8 | 🔴 if unreachable |
| **DNS** | Resolves microsoft.com | 🔴 if fails |
| **Services** | 8 critical Windows services | ⚠️ if any stopped |
| **Uptime** | Time since last restart | ⚠️ if >7 days |

### Issues the Agent Can Diagnose

| Category | Example Phrases |
|----------|----------------|
| **Outlook / Email** | "Outlook won't open", "email not loading", "mailbox not syncing" |
| **Microsoft Teams** | "Teams crash", "can't join meeting", "Teams black screen" |
| **Printing** | "Can't print", "print job stuck", "printer offline" |
| **Network** | "No internet", "WiFi not working", "DNS error" |
| **Performance** | "Computer is slow", "freezing", "high CPU" |
| **Windows Update** | "Update failed", "update stuck", "can't update" |
| **OneDrive** | "Files not syncing", "OneDrive error", "sync stuck" |
| **Sign-in / SSO** | "Keeps asking for password", "MFA not working", "can't sign in" |
| **Start Menu** | "Start menu not opening", "search not working" |
| **Icons** | "Blank icons", "wrong icons", "thumbnails broken" |
| **Citrix / VDI** | "Citrix not working", "can't launch app" |
| **Browser** | "Chrome not working", "page not loading" |
| **Group Policy** | "Policy not applying", "GPO not working" |
| **System Corruption** | "Blue screen", "random crashes", "corrupted files" |

### Session Memory

The agent remembers which fixes you've already run during the current session and won't suggest them again. Click **"Clear"** to reset the session.

---

## Dashboard 📊

The home page showing a quick overview:

- **System Summary** — Computer name, OS, CPU, RAM, disk usage
- **Network** — Active adapter name, IP address, gateway, DNS
- **Health Indicator** — Green/amber/red dot in the sidebar footer
- **Remote Desktop** — Quick launch button to open RDP

---

## System Info 💻

Detailed hardware and software inventory:

| Section | Details Shown |
|---------|---------------|
| **Operating System** | Edition, version, build number, install date |
| **Processor** | CPU model, core count, thread count |
| **Memory** | Total RAM, usage |
| **Graphics** | GPU name and driver version |
| **Storage** | All drives with capacity, free space, and usage bars |
| **Identity** | Domain name, computer name, logged-in user |

---

## Network 🌐

Active network connection diagnostics:

- **Adapter** — Name, type (Wi-Fi / Ethernet), MAC address, link speed
- **IPv4** — IP address, subnet mask, default gateway
- **IPv6** — IPv6 address(es)
- **DNS** — Primary and secondary DNS servers
- **Quick Actions** — Flush DNS, Release/Renew IP, Open Network Settings

> **Note:** The tool automatically detects your primary internet-connected adapter and filters out virtual interfaces (Hyper-V, Docker, VMware, VPN tunnels, etc.).

---

## Peripherals 🖨️

Manage printers, displays, and audio devices.

### Printers
- View all installed printers with online/offline status
- **Set Default** — Set any printer as the Windows default
- **Add Printer** — Opens the Windows manual add-printer wizard
- **Remove** — Uninstall a printer

### Display
- View current resolution and refresh rate
- Access Windows display settings

### Audio
- View all playback and recording devices
- Volume slider and mute toggle

---

## Drivers 🔍

Complete driver inventory:

- **Driver List** — Name, version, date, manufacturer, digital signer
- **Status** — Running / Stopped indicator per driver
- **Problem Flags** — Drivers with issues are highlighted
- **Actions** — Update driver, disable driver, open Device Manager

---

## Services ⚙️

Windows services management:

- **Service List** — Service name, display name, status, startup type
- **Actions** — Start, Stop, Restart individual services
- **Search** — Filter services by name
- **Troubleshoot** — Quickly identify stopped services that should be running

---

## Troubleshooting 🔧

One-click fixes organized into 4 categories. Cards marked with **🛡️ Admin** will prompt for administrator credentials (UAC).

### Application Resets
| Fix | What It Does |
|-----|--------------|
| Reset Teams | Kills Teams and clears all local cache |
| Reset Office | Clears Office cache files and tokens |
| Reset Citrix | Clears Citrix Workspace cache |
| Repair Outlook | Deletes OST file, forces Exchange re-sync |
| Repair OneDrive | Resets the OneDrive sync client |
| Clear Browsers | Clears Chrome, Edge, and Firefox caches |
| Clear Credentials | Removes saved Microsoft/Office passwords |

### System Actions
| Fix | Admin? | What It Does |
|-----|--------|--------------|
| GPUpdate | No | Forces Group Policy refresh |
| Restart Explorer | No | Kills and restarts explorer.exe |
| Clear Temp Files | No | Deletes temporary files and folders |
| Rebuild Icons | No | Fixes blank/wrong desktop icons |
| Fix Start Menu | 🛡️ Yes | Re-registers Start Menu and Search |

### Network & Printing
| Fix | Admin? | What It Does |
|-----|--------|--------------|
| Restart Spooler | 🛡️ Yes | Restarts the Print Spooler service |
| Clear Print Queue | 🛡️ Yes | Removes all stuck print jobs |
| Full Network Reset | 🛡️ Yes | Winsock + TCP/IP reset + DNS flush |
| Reset Win Update | 🛡️ Yes | Resets all Windows Update components |

### Advanced Diagnostics
| Fix | Admin? | What It Does |
|-----|--------|--------------|
| SFC Scan | 🛡️ Yes | System File Checker (10-30 minutes) |
| DISM Repair | 🛡️ Yes | DISM RestoreHealth (15-45 minutes) |

> **Important:** 🛡️ Admin actions trigger a Windows UAC prompt. Standard users may need an administrator to approve the elevation.

---

## Activity Log 📝

Audit trail of every action performed in the tool:

- **Timestamped entries** — Date, time, category, message
- **Severity levels** — Info (blue), Warning (amber), Error (red)
- **Search filter** — Type to filter log entries
- **Persistent** — Logs are kept for the current session

---

## Tips & Tricks

1. **Right-click the tray icon** for quick access to any section without opening the full window
2. **Use the Agent first** — describe your issue in plain English before manually browsing fix cards
3. **Run "Scan my system"** periodically to catch issues before they become problems
4. **Clear Temp Files + Clear Browsers** is a quick way to free disk space
5. **Restart your PC** if uptime is over 7 days — the agent will remind you
6. **Non-admin fixes** (no 🛡️ badge) can be run by any user without IT assistance
