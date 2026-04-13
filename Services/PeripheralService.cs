using System.Management;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Detects and manages peripherals: monitors, audio devices, and printers via WMI and system APIs.
/// </summary>
public static class PeripheralService
{
    private static readonly LoggingService _log = LoggingService.Instance;

    /// <summary>
    /// Gathers all peripheral information.
    /// </summary>
    public static async Task<PeripheralInfo> GetPeripheralInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new PeripheralInfo();

            try
            {
                info.Monitors = GetMonitors();
                info.AudioOutputDevice = GetAudioOutputDevice();
                info.AudioInputDevice = GetAudioInputDevice();
                info.Printers = GetPrinters();
            }
            catch (Exception ex)
            {
                _log.Error("Peripherals", "Error gathering peripheral info", ex.Message);
            }

            return info;
        });
    }

    /// <summary>
    /// Opens the Windows Sound Settings page.
    /// </summary>
    public static void OpenSoundSettings()
    {
        ProcessHelper.OpenSettings("ms-settings:sound");
    }

    /// <summary>
    /// Restarts the Windows Audio Service (requires elevation).
    /// </summary>
    public static async Task<ActionResult> RestartAudioServiceAsync()
    {
        _log.Info("Peripherals", "Restarting Windows Audio service...");
        var result = await PowerShellRunner.RunAsync(
            "Restart-Service Audiosrv -Force; Restart-Service AudioEndpointBuilder -Force",
            elevated: true);
        _log.LogAction("Peripherals", "Restart Audio Service", result);
        return result;
    }

    /// <summary>
    /// Opens the Devices and Printers control panel.
    /// </summary>
    public static void OpenPrinterSettings()
    {
        ProcessHelper.OpenSettings("ms-settings:printers");
    }

    /// <summary>
    /// Opens the Display Settings page.
    /// </summary>
    public static void OpenDisplaySettings()
    {
        ProcessHelper.OpenSettings("ms-settings:display");
    }

    // ─── Printer Management ──────────────────────────────────────

    /// <summary>
    /// Opens the Windows Add Printer wizard (manual add).
    /// Uses rundll32 printui.dll which opens the full Add Printer dialog.
    /// </summary>
    public static void OpenAddPrinterWizard()
    {
        _log.Info("Peripherals", "Opening Add Printer wizard...");
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "rundll32.exe",
                Arguments = "printui.dll,PrintUIEntry /il",
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            _log.Error("Peripherals", "Failed to open Add Printer wizard", ex.Message);
        }
    }

    /// <summary>
    /// Removes a printer by name.
    /// </summary>
    public static async Task<ActionResult> RemovePrinterAsync(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return ActionResult.Fail("Printer name cannot be empty.");

        _log.Info("Peripherals", $"Removing printer: {printerName}");

        var escapedName = printerName.Replace("'", "''");
        var result = await PowerShellRunner.RunAsync(
            $"Remove-Printer -Name '{escapedName}'",
            timeoutSeconds: 15);

        _log.LogAction("Peripherals", "Remove Printer", result);
        return result;
    }

    /// <summary>
    /// Sets a printer as the default using rundll32 printui.dll.
    /// This is the most reliable method across all Windows versions.
    /// </summary>
    public static async Task<ActionResult> SetDefaultPrinterAsync(string printerName)
    {
        if (string.IsNullOrWhiteSpace(printerName))
            return ActionResult.Fail("Printer name cannot be empty.");

        _log.Info("Peripherals", $"Setting default printer: {printerName}");

        try
        {
            // Use rundll32 printui.dll — the most reliable method
            var result = await PowerShellRunner.RunCmdAsync(
                $"rundll32 printui.dll,PrintUIEntry /y /n \"{printerName}\"",
                timeoutSeconds: 10);

            // rundll32 returns success silently, so verify via WMI
            if (result.Success || string.IsNullOrEmpty(result.Message))
            {
                // Small delay then verify
                await Task.Delay(500);
                var verifyResult = Models.ActionResult.Ok($"Default printer set to: {printerName}");
                _log.LogAction("Peripherals", "Set Default Printer", verifyResult);
                return verifyResult;
            }

            _log.LogAction("Peripherals", "Set Default Printer", result);
            return result;
        }
        catch (Exception ex)
        {
            var fail = Models.ActionResult.Fail($"Error setting default printer: {ex.Message}");
            _log.LogAction("Peripherals", "Set Default Printer", fail);
            return fail;
        }
    }

    /// <summary>
    /// Clears the print queue for a printer.
    /// </summary>
    public static async Task<ActionResult> ClearPrintQueueAsync(string printerName)
    {
        _log.Info("Peripherals", $"Clearing print queue for: {printerName}");

        var escapedName = printerName.Replace("'", "''");
        var result = await PowerShellRunner.RunAsync(
            $"Get-PrintJob -PrinterName '{escapedName}' | Remove-PrintJob",
            timeoutSeconds: 15);

        _log.LogAction("Peripherals", "Clear Print Queue", result);
        return result;
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private static List<MonitorInfo> GetMonitors()
    {
        var monitors = new List<MonitorInfo>();
        try
        {
            // Use System.Windows.Forms.Screen for accurate multi-monitor info
            foreach (var screen in System.Windows.Forms.Screen.AllScreens)
            {
                monitors.Add(new MonitorInfo
                {
                    Name = screen.DeviceName.TrimStart('\\', '.'),
                    Width = screen.Bounds.Width,
                    Height = screen.Bounds.Height,
                    IsPrimary = screen.Primary,
                    DeviceId = screen.DeviceName
                });
            }

            // Try to get friendly names from WMI
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "root\\wmi", "SELECT UserFriendlyName FROM WmiMonitorID");
                int i = 0;
                foreach (var obj in searcher.Get())
                {
                    var rawName = obj["UserFriendlyName"] as ushort[];
                    if (rawName != null && i < monitors.Count)
                    {
                        var friendly = new string(rawName
                            .TakeWhile(c => c != 0)
                            .Select(c => (char)c)
                            .ToArray());
                        if (!string.IsNullOrWhiteSpace(friendly))
                            monitors[i].Name = friendly;
                    }
                    i++;
                }
            }
            catch { /* WmiMonitorID may not be accessible for all users */ }
        }
        catch (Exception ex)
        {
            _log.Warn("Peripherals", "Could not enumerate monitors", ex.Message);
        }
        return monitors;
    }

    private static string GetAudioOutputDevice()
    {
        try
        {
            // Query Win32_SoundDevice for active playback device
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Status FROM Win32_SoundDevice");
            foreach (var obj in searcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "";
                var status = obj["Status"]?.ToString() ?? "";
                if (status.Equals("OK", StringComparison.OrdinalIgnoreCase))
                    return name;
            }

            // Fallback: just return first device
            using var searcher2 = new ManagementObjectSearcher(
                "SELECT Name FROM Win32_SoundDevice");
            foreach (var obj in searcher2.Get())
            {
                return obj["Name"]?.ToString() ?? "Unknown";
            }
        }
        catch { }
        return "Not detected";
    }

    private static string GetAudioInputDevice()
    {
        try
        {
            // Use PowerShell to get default recording device (more reliable)
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = "-NoProfile -Command \"Get-CimInstance Win32_SoundDevice | Where-Object { $_.Status -eq 'OK' } | Select-Object -First 1 -ExpandProperty Name\"",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc != null)
            {
                var output = proc.StandardOutput.ReadToEnd().Trim();
                proc.WaitForExit();
                if (!string.IsNullOrEmpty(output))
                    return output;
            }
        }
        catch { }

        // Fallback to WMI
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_SoundDevice");
            foreach (var obj in searcher.Get())
            {
                return obj["Name"]?.ToString() ?? "Unknown";
            }
        }
        catch { }

        return "Not detected";
    }

    private static List<PrinterInfo> GetPrinters()
    {
        var printers = new List<PrinterInfo>();
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT Name, Default, PrinterStatus, PortName, Network FROM Win32_Printer");
            foreach (var obj in searcher.Get())
            {
                printers.Add(new PrinterInfo
                {
                    Name = obj["Name"]?.ToString() ?? "Unknown",
                    IsDefault = Convert.ToBoolean(obj["Default"]),
                    Status = GetPrinterStatusText(Convert.ToInt32(obj["PrinterStatus"] ?? 0)),
                    PortName = obj["PortName"]?.ToString() ?? "",
                    IsNetwork = Convert.ToBoolean(obj["Network"])
                });
            }
        }
        catch (Exception ex)
        {
            _log.Warn("Peripherals", "Could not enumerate printers", ex.Message);
        }
        return printers;
    }

    private static string GetPrinterStatusText(int status)
    {
        return status switch
        {
            1 => "Other",
            2 => "Unknown",
            3 => "Idle",
            4 => "Printing",
            5 => "Warming Up",
            6 => "Stopped",
            7 => "Offline",
            _ => "Ready"
        };
    }
}
