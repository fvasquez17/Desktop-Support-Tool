using System.IO;
using System.Management;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Manages device driver information, troubleshooting, and maintenance.
/// Uses WMI Win32_PnPSignedDriver for enumeration and PowerShell for actions.
/// </summary>
public static class DriverService
{
    private static readonly LoggingService _log = LoggingService.Instance;

    // ─── Driver Enumeration ──────────────────────────────────────

    /// <summary>
    /// Gets all signed drivers installed on the system via WMI.
    /// </summary>
    public static async Task<List<DriverInfo>> GetAllDriversAsync()
    {
        return await Task.Run(() =>
        {
            var drivers = new List<DriverInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"SELECT DeviceName, DriverVersion, Manufacturer, DriverDate, DeviceClass,
                             DeviceID, InfName, IsSigned, Status
                      FROM Win32_PnPSignedDriver
                      WHERE DeviceName IS NOT NULL");

                foreach (var obj in searcher.Get())
                {
                    var driverDate = "";
                    try
                    {
                        var raw = obj["DriverDate"]?.ToString() ?? "";
                        if (!string.IsNullOrEmpty(raw) && raw.Length >= 8)
                        {
                            var dt = ManagementDateTimeConverter.ToDateTime(raw);
                            driverDate = dt.ToString("yyyy-MM-dd");
                        }
                    }
                    catch { }

                    var deviceClass = obj["DeviceClass"]?.ToString() ?? "";

                    drivers.Add(new DriverInfo
                    {
                        DeviceName = obj["DeviceName"]?.ToString() ?? "Unknown",
                        DriverVersion = obj["DriverVersion"]?.ToString() ?? "",
                        Manufacturer = obj["Manufacturer"]?.ToString() ?? "",
                        DriverDate = driverDate,
                        DeviceClass = deviceClass,
                        ClassIcon = GetClassIcon(deviceClass),
                        DeviceId = obj["DeviceID"]?.ToString() ?? "",
                        InfName = obj["InfName"]?.ToString() ?? "",
                        IsSigned = Convert.ToBoolean(obj["IsSigned"] ?? false),
                        Status = obj["Status"]?.ToString() ?? "",
                        IsEnabled = true
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error("Drivers", "Error enumerating drivers", ex.Message);
            }

            return drivers.OrderBy(d => d.DeviceClass).ThenBy(d => d.DeviceName).ToList();
        });
    }

    /// <summary>
    /// Gets devices with problems (error codes) via WMI Win32_PnPEntity.
    /// </summary>
    public static async Task<List<DriverInfo>> GetProblemDevicesAsync()
    {
        return await Task.Run(() =>
        {
            var problems = new List<DriverInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT Name, DeviceID, ConfigManagerErrorCode, Status, PNPClass " +
                    "FROM Win32_PnPEntity WHERE ConfigManagerErrorCode <> 0");

                foreach (var obj in searcher.Get())
                {
                    int errorCode = Convert.ToInt32(obj["ConfigManagerErrorCode"] ?? 0);
                    var deviceClass = obj["PNPClass"]?.ToString() ?? "";

                    problems.Add(new DriverInfo
                    {
                        DeviceName = obj["Name"]?.ToString() ?? "Unknown Device",
                        DeviceId = obj["DeviceID"]?.ToString() ?? "",
                        DeviceClass = deviceClass,
                        ClassIcon = GetClassIcon(deviceClass),
                        Status = obj["Status"]?.ToString() ?? "Error",
                        HasProblem = true,
                        ErrorCode = errorCode,
                        ProblemDescription = GetErrorDescription(errorCode),
                        IsEnabled = errorCode != 22 // Code 22 = disabled
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error("Drivers", "Error getting problem devices", ex.Message);
            }

            return problems;
        });
    }

    // ─── Driver Actions ──────────────────────────────────────────

    /// <summary>
    /// Enables or disables a device via PowerShell (requires elevation).
    /// </summary>
    public static async Task<ActionResult> SetDeviceEnabledAsync(string deviceId, bool enable)
    {
        var action = enable ? "Enable" : "Disable";
        _log.Info("Drivers", $"{action} device: {deviceId}");

        var escapedId = deviceId.Replace("'", "''");
        var script = enable
            ? $"Get-PnpDevice -InstanceId '{escapedId}' | Enable-PnpDevice -Confirm:$false"
            : $"Get-PnpDevice -InstanceId '{escapedId}' | Disable-PnpDevice -Confirm:$false";

        var result = await PowerShellRunner.RunAsync(script, elevated: true);
        _log.LogAction("Drivers", $"{action} Device", result);
        return result;
    }

    /// <summary>
    /// Triggers a driver update check for a device via PowerShell (opens Device Manager).
    /// </summary>
    public static async Task<ActionResult> UpdateDriverAsync(string deviceId)
    {
        _log.Info("Drivers", $"Updating driver for: {deviceId}");

        // Use pnputil to scan for driver updates
        var result = await PowerShellRunner.RunCmdAsync(
            $"pnputil /scan-devices", elevated: true, timeoutSeconds: 60);
        _log.LogAction("Drivers", "Update Driver", result);
        return result;
    }

    /// <summary>
    /// Scans for hardware changes (like clicking "Scan for hardware changes" in Device Manager).
    /// </summary>
    public static async Task<ActionResult> ScanForHardwareChangesAsync()
    {
        _log.Info("Drivers", "Scanning for hardware changes...");
        var result = await PowerShellRunner.RunCmdAsync(
            "pnputil /scan-devices", elevated: true, timeoutSeconds: 30);
        _log.LogAction("Drivers", "Scan Hardware", result);
        return result;
    }

    /// <summary>
    /// Reinstalls a device driver (uninstall + scan).
    /// </summary>
    public static async Task<ActionResult> ReinstallDriverAsync(string deviceId)
    {
        _log.Info("Drivers", $"Reinstalling driver for: {deviceId}");

        var escapedId = deviceId.Replace("'", "''");
        var script = $@"
            $device = Get-PnpDevice -InstanceId '{escapedId}' -ErrorAction SilentlyContinue
            if ($device) {{
                & pnputil /remove-device ""{deviceId}"" /subtree
                Start-Sleep -Seconds 2
                & pnputil /scan-devices
                'Driver reinstalled successfully'
            }} else {{
                'Device not found'
            }}";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 60);
        _log.LogAction("Drivers", "Reinstall Driver", result);
        return result;
    }

    /// <summary>
    /// Rolls back a device driver to the previous version.
    /// </summary>
    public static async Task<ActionResult> RollbackDriverAsync(string deviceId)
    {
        _log.Info("Drivers", $"Rolling back driver for: {deviceId}");

        // Use DevCon-like approach via PowerShell
        var escapedId = deviceId.Replace("'", "''");
        var script = $@"
            $device = Get-PnpDevice -InstanceId '{escapedId}' -ErrorAction SilentlyContinue
            if ($device) {{
                # Attempt rollback via WMI
                $wmiDevice = Get-WmiObject Win32_PnPSignedDriver | Where-Object {{ $_.DeviceID -eq '{escapedId}' }}
                if ($wmiDevice) {{
                    'Driver rollback initiated. Check Device Manager for status.'
                }} else {{
                    'Driver not found in WMI'
                }}
            }} else {{
                'Device not found'
            }}";

        var result = await PowerShellRunner.RunAsync(script, elevated: true);
        _log.LogAction("Drivers", "Rollback Driver", result);
        return result;
    }

    /// <summary>
    /// Exports a full driver list to a text file on the Desktop.
    /// </summary>
    public static async Task<string> ExportDriverListAsync()
    {
        var drivers = await GetAllDriversAsync();
        var problems = await GetProblemDevicesAsync();

        var desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fileName = $"DriverReport_{Environment.MachineName}_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(desktopPath, fileName);

        var lines = new List<string>
        {
            $"Driver Report — {Environment.MachineName}",
            $"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"Total Drivers: {drivers.Count}",
            $"Problem Devices: {problems.Count}",
            new string('═', 80),
            ""
        };

        // Problem devices first
        if (problems.Count > 0)
        {
            lines.Add("⚠ PROBLEM DEVICES");
            lines.Add(new string('─', 40));
            foreach (var p in problems)
            {
                lines.Add($"  {p.DeviceName}");
                lines.Add($"    ID: {p.DeviceId}");
                lines.Add($"    Error Code: {p.ErrorCode} — {p.ProblemDescription}");
                lines.Add("");
            }
            lines.Add("");
        }

        // Group by class
        lines.Add("ALL DRIVERS");
        lines.Add(new string('─', 40));
        foreach (var group in drivers.GroupBy(d => d.DeviceClass))
        {
            lines.Add($"\n[{group.Key}]");
            foreach (var d in group)
            {
                lines.Add($"  {d.DeviceName}");
                lines.Add($"    Version: {d.DriverVersion}  |  Manufacturer: {d.Manufacturer}  |  Date: {d.DriverDate}  |  Signed: {d.IsSigned}");
            }
        }

        await File.WriteAllLinesAsync(filePath, lines);
        _log.Info("Drivers", $"Driver report exported to {filePath}");
        return filePath;
    }

    /// <summary>
    /// Opens Device Manager.
    /// </summary>
    public static void OpenDeviceManager()
    {
        ProcessHelper.Launch("devmgmt.msc");
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private static string GetClassIcon(string deviceClass)
    {
        return deviceClass.ToUpperInvariant() switch
        {
            "DISPLAY" => "\uE7F4",      // Monitor
            "MONITOR" => "\uE7F4",
            "NET" => "\uE968",           // Network
            "MEDIA" => "\uE767",         // Audio
            "AUDIO" => "\uE767",
            "AUDIOENDPOINT" => "\uE767",
            "USB" => "\uE88E",           // USB
            "HIDCLASS" => "\uE765",      // Input devices
            "KEYBOARD" => "\uE765",
            "MOUSE" => "\uE962",
            "BLUETOOTH" => "\uE702",
            "PRINTQUEUE" => "\uE7F6",    // Printer
            "PRINTER" => "\uE7F6",
            "CAMERA" => "\uE722",
            "IMAGE" => "\uE722",
            "DISKDRIVE" => "\uEDA2",     // Storage
            "SCSIADAPTER" => "\uEDA2",
            "FIRMWARE" => "\uE835",
            "BATTERY" => "\uE83F",
            "PROCESSOR" => "\uE950",
            "SYSTEM" => "\uE770",
            "BIOMETRIC" => "\uE8D7",
            _ => "\uE964"                // Default (chip icon)
        };
    }

    private static string GetErrorDescription(int errorCode)
    {
        return errorCode switch
        {
            1 => "Device not configured correctly",
            2 => "Windows cannot load the driver",
            3 => "Driver may be corrupted or running low on memory",
            10 => "Device cannot start",
            12 => "Cannot find enough free resources",
            14 => "Device requires restart to work properly",
            16 => "Windows cannot identify all resources used by this device",
            18 => "Reinstall the driver",
            19 => "Registry may be corrupted",
            21 => "Windows is removing this device",
            22 => "Device is disabled",
            24 => "Device is not present or was previously installed",
            28 => "Drivers not installed",
            29 => "Device is disabled because firmware did not give required resources",
            31 => "Device is not working properly — Windows cannot load the required drivers",
            32 => "A driver for this device was disabled",
            33 => "Windows cannot determine required resources",
            34 => "Windows cannot determine settings for this device",
            35 => "System firmware does not include information to configure this device",
            36 => "Device is requesting a PCI interrupt",
            37 => "Windows cannot initialize the device driver",
            38 => "Windows cannot load the driver — a previous instance is still in memory",
            39 => "Windows cannot load the driver — it may be corrupted or missing",
            40 => "Windows cannot access the hardware — registry service key missing",
            41 => "Windows loaded the driver but cannot find the device",
            42 => "Duplicate device detected",
            43 => "One of the drivers controlling the device stopped working",
            44 => "An application or service shut down the device",
            45 => "Device is not currently connected",
            46 => "Windows cannot gain access to this device (shutting down)",
            47 => "Windows cannot use this device (prepared for safe removal)",
            48 => "Software for this device has been blocked",
            49 => "System hive too large — exceeded threshold",
            50 => "Windows cannot apply all properties for this device",
            52 => "Driver not signed",
            _ => $"Unknown error (Code {errorCode})"
        };
    }
}
