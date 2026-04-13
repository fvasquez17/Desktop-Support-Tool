# Desktop Support Tool — Developer & Modification Guide

> How to extend, customize, and maintain the Desktop Support Tool.

---

## Architecture Overview

```
DesktopSupportTool/
│
├── App.xaml / App.xaml.cs              # App lifecycle, theme, tray startup
├── DesktopSupportTool.csproj           # Project config (.NET 9, WPF + WinForms)
├── GlobalUsings.cs                     # Shared using statements
│
├── Models/                             # Data transfer objects (records / POCOs)
│   ├── ActionResult.cs                 #   Success/failure result for fixes
│   ├── DriverInfo.cs                   #   Driver metadata
│   ├── LogEntry.cs                     #   Activity log entry
│   ├── NetworkInfo.cs                  #   Network adapter data
│   ├── PeripheralInfo.cs               #   Printer/audio/display info
│   ├── ServiceInfo.cs                  #   Windows service metadata
│   └── SystemInfo.cs                   #   Hardware/OS data
│
├── Helpers/                            # Low-level utilities
│   ├── AudioHelper.cs                  #   COM interop for audio devices
│   ├── DisplayHelper.cs                #   Win32 P/Invoke for display settings
│   ├── ElevationHelper.cs              #   UAC elevation detection
│   ├── IconGenerator.cs                #   Programmatic icon creation (GDI+)
│   ├── PowerShellRunner.cs             #   PowerShell / CMD execution engine
│   ├── ProcessHelper.cs                #   Process management utilities
│   └── RegistryHelper.cs               #   Registry read/write helpers
│
├── Services/                           # Business logic layer
│   ├── DriverService.cs                #   WMI driver enumeration
│   ├── HealthCheckService.cs           #   Background system health monitor
│   ├── LoggingService.cs               #   Activity logging with severity
│   ├── NetworkService.cs               #   Adapter detection, DNS flush, IP renew
│   ├── PeripheralService.cs            #   Printer/audio/display management
│   ├── SystemInfoService.cs            #   WMI hardware/OS queries
│   ├── TroubleshootBot.cs              #   AI Support Agent engine (14 rules)
│   ├── TroubleshootService.cs          #   All fix implementations (16+ fixes)
│   └── WindowsServiceManager.cs        #   Windows service start/stop/restart
│
├── Tray/
│   └── TrayIconManager.cs              #   System tray icon + right-click menu
│
└── UI/
    ├── MainWindow.xaml / .cs           #   Shell: sidebar nav + content area
    └── Views/                          #   Each tab is a UserControl
        ├── AgentBotView.xaml / .cs     #     AI chat interface
        ├── DashboardView.xaml / .cs    #     Home dashboard
        ├── DriversView.xaml / .cs      #     Driver management
        ├── LogViewerView.xaml / .cs    #     Activity log viewer
        ├── NetworkView.xaml / .cs      #     Network diagnostics
        ├── PeripheralsView.xaml / .cs  #     Printers/audio/display
        ├── ServicesView.xaml / .cs     #     Windows services manager
        ├── SystemInfoView.xaml / .cs   #     System info display
        └── TroubleshootView.xaml / .cs #     One-click fix cards
```

### Design Principles

- **Modular views** — Each tab is an independent `UserControl` with its own `.xaml` and `.cs`
- **Service layer** — All logic lives in `Services/` — views are thin wrappers
- **Static services** — Services are static classes (no DI container needed)
- **Async everywhere** — All long-running operations use `async/await`
- **Per-action elevation** — The app runs as standard user; admin ops use `elevated: true`

---

## How to: Add a New One-Click Fix

### Step 1 — Add the Fix Method

Open `Services/TroubleshootService.cs` and add a new static async method:

```csharp
/// <summary>
/// Description of what this fix does.
/// </summary>
public static async Task<ActionResult> YourNewFixAsync()
{
    _log.Info("Troubleshoot", "Running your new fix...");

    // OPTION A: PowerShell command (standard user)
    var result = await PowerShellRunner.RunAsync(
        "Get-Service Spooler | Restart-Service -Force",
        elevated: false,
        timeoutSeconds: 30);

    // OPTION B: CMD command (admin elevation — triggers UAC)
    var result = await PowerShellRunner.RunCmdAsync(
        "sfc /scannow",
        elevated: true,
        timeoutSeconds: 600);

    // OPTION C: Multi-line PowerShell script
    var script = @"
        Stop-Process -Name 'outlook' -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
        Remove-Item '$env:LOCALAPPDATA\Microsoft\Outlook\*.ost' -Force
    ";
    var result = await PowerShellRunner.RunAsync(script, elevated: false);

    return result.Success
        ? ActionResult.Ok("Fix applied successfully.")
        : ActionResult.Fail($"Fix failed: {result.Output}");
}
```

### Step 2 — Add the UI Card

Open `UI/Views/TroubleshootView.xaml` and add a card in the appropriate `<WrapPanel>`:

```xml
<!-- Your New Fix -->
<Border Style="{StaticResource Card}" Margin="0,0,10,10" Width="200" Cursor="Hand"
        x:Name="CardYourFix" MouseLeftButtonUp="YourFix_Click">
    <StackPanel>
        <!-- Icon (find codes at https://learn.microsoft.com/en-us/windows/apps/design/style/segoe-ui-symbol-font) -->
        <TextBlock FontFamily="{StaticResource IconFont}" Text="&#xE895;"
                   FontSize="28" Foreground="{StaticResource AccentBrush}"
                   Margin="0,0,0,10"/>

        <!-- Title + optional Admin badge -->
        <StackPanel Orientation="Horizontal">
            <TextBlock Text="Your Fix Name" FontSize="15" FontWeight="SemiBold"
                       Foreground="{StaticResource TextPrimaryBrush}"
                       FontFamily="{StaticResource MainFont}"/>
            <!-- ONLY include this Border if the fix uses elevated: true -->
            <Border Background="{StaticResource WarningBrush}" CornerRadius="4"
                    Padding="5,1,5,2" Margin="6,0,0,0" VerticalAlignment="Center">
                <TextBlock Text="🛡️ Admin" FontSize="9" FontWeight="Bold"
                           Foreground="White" FontFamily="{StaticResource MainFont}"/>
            </Border>
        </StackPanel>

        <!-- Description -->
        <TextBlock Text="Short description of what this fix does"
                   FontSize="11" Foreground="{StaticResource TextTertiaryBrush}"
                   TextWrapping="Wrap" FontFamily="{StaticResource MainFont}"/>

        <!-- Status (updated at runtime) -->
        <TextBlock x:Name="StatusYourFix" Text="" FontSize="11" Margin="0,6,0,0"
                   TextWrapping="Wrap" FontFamily="{StaticResource MainFont}"/>
    </StackPanel>
</Border>
```

### Step 3 — Add the Click Handler

Open `UI/Views/TroubleshootView.xaml.cs`:

```csharp
private async void YourFix_Click(object sender, MouseButtonEventArgs e)
{
    // For destructive or admin actions, show a confirmation:
    var answer = MessageBox.Show(
        "This will do X and Y.\nA reboot may be needed.\n\nContinue?",
        "Your Fix Name",
        MessageBoxButton.YesNo,
        MessageBoxImage.Question);

    if (answer == MessageBoxResult.Yes)
    {
        await RunAction(CardYourFix, StatusYourFix, "Running fix...",
            () => TroubleshootService.YourNewFixAsync());
    }

    // For safe, non-destructive actions, skip the dialog:
    // await RunAction(CardYourFix, StatusYourFix, "Running fix...",
    //     () => TroubleshootService.YourNewFixAsync());
}
```

> The `RunAction()` helper handles: showing "running…" status, disabling the card, calling the fix, showing ✅/❌ result, and logging.

### Step 4 — (Optional) Teach the AI Bot

Open `Services/TroubleshootBot.cs` and add a diagnostic rule to the `_rules` list:

```csharp
// ── Your Category ──
(new[] { "keyword1", "keyword2", "keyword3" },                    // Keywords (1pt each)
 new[] { "exact phrase match 1", "exact phrase match 2" },        // Phrases (3pts each)
 "CategoryName",                                                  // Category label
 (snap) => new Diagnosis("Category", "Human-Readable Title",
     "Explanation of the problem. " +
     (snap?.DiskPercent > 90
         ? "⚠️ Your disk is nearly full — this may be related."   // Context-aware!
         : ""),
     new List<SuggestedAction>
     {
         new("Fix Name", "What this fix does",
             TroubleshootService.YourNewFixAsync,
             RequiresConfirmation: true),  // true = shows dialog before running
     })),
```

---

## How to: Add a New View (Tab)

### Step 1 — Create the UserControl

Create `UI/Views/YourView.xaml`:

```xml
<UserControl x:Class="DesktopSupportTool.UI.Views.YourView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{StaticResource BgBrush}">

    <ScrollViewer VerticalScrollBarVisibility="Auto" Padding="28,24">
        <StackPanel>
            <!-- Page Header -->
            <TextBlock Text="Your Section" FontSize="26" FontWeight="Bold"
                       Foreground="{StaticResource TextPrimaryBrush}"
                       FontFamily="{StaticResource MainFont}"/>
            <TextBlock Text="Description of this section"
                       FontSize="13" Foreground="{StaticResource TextSecondaryBrush}"
                       FontFamily="{StaticResource MainFont}" Margin="0,4,0,24"/>

            <!-- Your content here -->
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

Create `UI/Views/YourView.xaml.cs`:

```csharp
using System.Windows.Controls;

namespace DesktopSupportTool.UI.Views;

public partial class YourView : UserControl
{
    public YourView()
    {
        InitializeComponent();
    }

    public async Task RefreshAsync()
    {
        // Called every time the user navigates to this tab
        // Load your data here
    }
}
```

### Step 2 — Register in MainWindow

**MainWindow.xaml** — Add a RadioButton in the sidebar:

```xml
<RadioButton Style="{StaticResource NavButton}" Content="Your Section"
             Tag="&#xE895;" x:Name="NavYourSection"
             Checked="Nav_Checked"/>
```

**MainWindow.xaml.cs** — 4 changes:

```csharp
// 1. Add field (line ~27)
private readonly YourView _yourView = new();

// 2. Add to NavigateTo() switch
case "yoursection":
    NavYourSection.IsChecked = true;
    break;

// 3. Add to Nav_Checked content switch
"NavYourSection" => _yourView,

// 4. Add refresh trigger
else if (ContentArea.Content is YourView yv) _ = yv.RefreshAsync();
```

---

## How to: Create a New Service

### Pattern

```csharp
// Services/YourService.cs
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Description of what this service does.
/// </summary>
public static class YourService
{
    private static readonly LoggingService _log = LoggingService.Instance;

    public static async Task<List<YourModel>> GetDataAsync()
    {
        _log.Info("YourService", "Loading data...");

        return await Task.Run(() =>
        {
            var results = new List<YourModel>();
            // Your logic here — WMI, PowerShell, registry, etc.
            return results;
        });
    }

    public static async Task<ActionResult> DoSomethingAsync()
    {
        _log.Info("YourService", "Performing action...");

        var result = await PowerShellRunner.RunAsync(
            "Your-Command", elevated: false);

        return result.Success
            ? ActionResult.Ok("Done.")
            : ActionResult.Fail($"Failed: {result.Output}");
    }
}
```

---

## How to: Customize the Theme

All colors, styles, and fonts are defined in `App.xaml`.

### Core Colors

```xml
<!-- App.xaml — find and edit these values -->
<Color x:Key="BgColor">#0F172A</Color>           <!-- Main background -->
<Color x:Key="SidebarColor">#0B1120</Color>       <!-- Sidebar -->
<Color x:Key="SurfaceColor">#1E293B</Color>       <!-- Card backgrounds -->
<Color x:Key="SurfaceHighColor">#334155</Color>   <!-- Hover / elevated surfaces -->
<Color x:Key="AccentColor">#3B82F6</Color>        <!-- Primary accent (blue) -->
<Color x:Key="SuccessColor">#22C55E</Color>       <!-- Green -->
<Color x:Key="WarningColor">#F59E0B</Color>       <!-- Amber -->
<Color x:Key="ErrorColor">#EF4444</Color>         <!-- Red -->
```

### Rebranding Examples

```xml
<!-- Microsoft Blue -->
<Color x:Key="AccentColor">#0078D4</Color>

<!-- Corporate Green -->
<Color x:Key="AccentColor">#00A651</Color>

<!-- Light Theme (change ALL bg/surface colors) -->
<Color x:Key="BgColor">#F8FAFC</Color>
<Color x:Key="SidebarColor">#F1F5F9</Color>
<Color x:Key="SurfaceColor">#FFFFFF</Color>
<Color x:Key="TextPrimaryColor">#1E293B</Color>
<Color x:Key="TextSecondaryColor">#475569</Color>
```

### Change App Title

In `MainWindow.xaml`, update the `Title` attribute and the title bar TextBlock:

```xml
<Window ... Title="Acme Corp Support Tool" ...>
```

```xml
<TextBlock Text="Acme Corp Support Tool" FontSize="13" .../>
```

### Change Company Branding

In `DesktopSupportTool.csproj`:

```xml
<Company>Acme Corporation</Company>
<Product>Acme Support Tool</Product>
<Authors>Acme IT Department</Authors>
<Copyright>Copyright © 2025 Acme Corporation</Copyright>
```

---

## UAC Elevation Model

The tool uses a **per-action elevation** strategy:

```
┌──────────────────────────────────────────────┐
│  DesktopSupportTool.exe (Standard User)      │
│                                              │
│  ┌──── User clicks "SFC Scan" ────┐         │
│  │                                 │         │
│  │  PowerShellRunner.RunAsync(     │         │
│  │    script,                      │         │
│  │    elevated: true  ────────────────► UAC Prompt
│  │  )                              │         │    │
│  │                                 │         │    ▼
│  │  ◄── result ──────────────────────── Elevated Process
│  └─────────────────────────────────┘         │   (runs sfc /scannow)
│                                              │
└──────────────────────────────────────────────┘
```

**Key points:**
- The app itself **never** runs as admin
- Only the specific command/script runs elevated
- Each `elevated: true` call triggers a separate UAC prompt
- If the user cancels UAC, the action fails gracefully
- Non-admin fixes work without any prompts

### Checking Elevation Status

```csharp
using DesktopSupportTool.Helpers;

// Check if the current process is elevated
bool isAdmin = ElevationHelper.IsElevated();
```

---

## Key APIs & Patterns

### Running System Commands

```csharp
using DesktopSupportTool.Helpers;

// PowerShell (standard user)
var result = await PowerShellRunner.RunAsync(
    "Get-Process | Select-Object -First 5",
    elevated: false,
    timeoutSeconds: 30);

// PowerShell (elevated — triggers UAC)
var result = await PowerShellRunner.RunAsync(
    "Restart-Service Spooler -Force",
    elevated: true,
    timeoutSeconds: 30);

// CMD command
var result = await PowerShellRunner.RunCmdAsync(
    "ipconfig /flushdns",
    elevated: false,
    timeoutSeconds: 10);

// Check result
if (result.Success)
    Console.WriteLine(result.Output);
else
    Console.WriteLine($"Error: {result.Output}");
```

### Logging

```csharp
var log = LoggingService.Instance;

log.Info("Category", "Something happened");
log.Warn("Category", "Something might be wrong");
log.Error("Category", "Something failed", exception);
```

### WMI Queries

```csharp
using System.Management;

var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Processor");
foreach (var obj in searcher.Get())
{
    var cpuName = obj["Name"]?.ToString();
}
```

---

## Build Commands

```powershell
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run locally
dotnet run

# Publish self-contained EXE
dotnet publish -c Release --self-contained true -r win-x64 `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -o .\publish

# Publish framework-dependent (smaller, needs .NET 9 runtime)
dotnet publish -c Release --self-contained false `
    -p:PublishSingleFile=true `
    -o .\publish
```

---

## Testing Checklist

When modifying the tool, verify:

- [ ] Build succeeds with 0 errors, 0 warnings
- [ ] App starts minimized to tray
- [ ] All sidebar navigation works
- [ ] Dashboard loads system info correctly
- [ ] Network shows the correct active adapter
- [ ] Troubleshoot cards show proper status after running
- [ ] Admin badges appear on elevated-only cards
- [ ] AI Agent responds to at least 5 different issue types
- [ ] Maximize respects taskbar (no overflow)
- [ ] Close button hides to tray (not exit)
- [ ] Right-click tray menu works
