using System.Management;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Manages Windows services — enumerate, start, stop, restart, change startup type.
/// Uses WMI for enumeration and PowerShell for administrative actions.
/// </summary>
public static class WindowsServiceManager
{
    private static readonly LoggingService _log = LoggingService.Instance;

    // ─── Enumeration ─────────────────────────────────────────────

    /// <summary>
    /// Gets all Windows services via WMI.
    /// </summary>
    public static async Task<List<ServiceInfo>> GetAllServicesAsync()
    {
        return await Task.Run(() =>
        {
            var services = new List<ServiceInfo>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    @"SELECT Name, DisplayName, State, StartMode, StartName, 
                             Description, ProcessId, PathName 
                      FROM Win32_Service");

                foreach (var obj in searcher.Get())
                {
                    services.Add(new ServiceInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "",
                        DisplayName = obj["DisplayName"]?.ToString() ?? "",
                        Status = obj["State"]?.ToString() ?? "Unknown",
                        StartupType = NormalizeStartMode(obj["StartMode"]?.ToString() ?? ""),
                        Account = obj["StartName"]?.ToString() ?? "",
                        Description = obj["Description"]?.ToString() ?? "",
                        ProcessId = Convert.ToInt32(obj["ProcessId"] ?? 0),
                        PathName = obj["PathName"]?.ToString() ?? ""
                    });
                }
            }
            catch (Exception ex)
            {
                _log.Error("Services", "Error enumerating services", ex.Message);
            }

            return services.OrderBy(s => s.DisplayName).ToList();
        });
    }

    // ─── Service Actions ─────────────────────────────────────────

    /// <summary>
    /// Starts a stopped service.
    /// </summary>
    public static async Task<ActionResult> StartServiceAsync(string serviceName)
    {
        _log.Info("Services", $"Starting service: {serviceName}");
        var escaped = serviceName.Replace("'", "''");
        var result = await PowerShellRunner.RunAsync(
            $"Start-Service -Name '{escaped}' -ErrorAction Stop; 'Service started successfully.'",
            elevated: true, timeoutSeconds: 30);
        _log.LogAction("Services", $"Start {serviceName}", result);
        return result;
    }

    /// <summary>
    /// Stops a running service.
    /// </summary>
    public static async Task<ActionResult> StopServiceAsync(string serviceName)
    {
        _log.Info("Services", $"Stopping service: {serviceName}");
        var escaped = serviceName.Replace("'", "''");
        var result = await PowerShellRunner.RunAsync(
            $"Stop-Service -Name '{escaped}' -Force -ErrorAction Stop; 'Service stopped successfully.'",
            elevated: true, timeoutSeconds: 30);
        _log.LogAction("Services", $"Stop {serviceName}", result);
        return result;
    }

    /// <summary>
    /// Restarts a service (stop then start).
    /// </summary>
    public static async Task<ActionResult> RestartServiceAsync(string serviceName)
    {
        _log.Info("Services", $"Restarting service: {serviceName}");
        var escaped = serviceName.Replace("'", "''");
        var result = await PowerShellRunner.RunAsync(
            $"Restart-Service -Name '{escaped}' -Force -ErrorAction Stop; 'Service restarted successfully.'",
            elevated: true, timeoutSeconds: 30);
        _log.LogAction("Services", $"Restart {serviceName}", result);
        return result;
    }

    /// <summary>
    /// Changes the startup type for a service.
    /// </summary>
    public static async Task<ActionResult> SetStartupTypeAsync(string serviceName, string startupType)
    {
        _log.Info("Services", $"Setting {serviceName} startup to: {startupType}");
        var escaped = serviceName.Replace("'", "''");
        var result = await PowerShellRunner.RunAsync(
            $"Set-Service -Name '{escaped}' -StartupType '{startupType}' -ErrorAction Stop; " +
            $"'Startup type changed to {startupType}.'",
            elevated: true, timeoutSeconds: 15);
        _log.LogAction("Services", $"Set Startup {serviceName}", result);
        return result;
    }

    /// <summary>
    /// Gets details for a single service.
    /// </summary>
    public static async Task<ServiceInfo?> GetServiceAsync(string serviceName)
    {
        return await Task.Run(() =>
        {
            try
            {
                var escaped = serviceName.Replace("'", "\\'");
                using var searcher = new ManagementObjectSearcher(
                    $"SELECT Name, DisplayName, State, StartMode, StartName, Description, ProcessId, PathName " +
                    $"FROM Win32_Service WHERE Name='{escaped}'");

                foreach (var obj in searcher.Get())
                {
                    return new ServiceInfo
                    {
                        Name = obj["Name"]?.ToString() ?? "",
                        DisplayName = obj["DisplayName"]?.ToString() ?? "",
                        Status = obj["State"]?.ToString() ?? "Unknown",
                        StartupType = NormalizeStartMode(obj["StartMode"]?.ToString() ?? ""),
                        Account = obj["StartName"]?.ToString() ?? "",
                        Description = obj["Description"]?.ToString() ?? "",
                        ProcessId = Convert.ToInt32(obj["ProcessId"] ?? 0),
                        PathName = obj["PathName"]?.ToString() ?? ""
                    };
                }
            }
            catch { }
            return null;
        });
    }

    /// <summary>
    /// Opens the Windows Services MMC snap-in.
    /// </summary>
    public static void OpenServicesConsole()
    {
        ProcessHelper.Launch("services.msc");
    }

    // ─── Well-Known Services ─────────────────────────────────────

    /// <summary>
    /// Common services that helpdesk frequently needs to manage.
    /// </summary>
    public static readonly (string Name, string Display, string Category)[] CommonServices = new[]
    {
        ("Spooler", "Print Spooler", "Printing"),
        ("wuauserv", "Windows Update", "System"),
        ("BITS", "Background Intelligent Transfer", "System"),
        ("Dnscache", "DNS Client", "Network"),
        ("LanmanWorkstation", "Workstation (SMB)", "Network"),
        ("LanmanServer", "Server (SMB)", "Network"),
        ("WinRM", "Windows Remote Management", "Remote"),
        ("TermService", "Remote Desktop Services", "Remote"),
        ("Audiosrv", "Windows Audio", "Multimedia"),
        ("AudioEndpointBuilder", "Audio Endpoint Builder", "Multimedia"),
        ("WSearch", "Windows Search", "System"),
        ("Themes", "Themes", "UI"),
        ("EventLog", "Windows Event Log", "System"),
        ("W32Time", "Windows Time", "System"),
        ("Netlogon", "Netlogon", "Authentication"),
        ("CryptSvc", "Cryptographic Services", "Security"),
        ("gpsvc", "Group Policy Client", "System"),
        ("Schedule", "Task Scheduler", "System"),
        ("SysMain", "SysMain (Superfetch)", "Performance"),
        ("DiagTrack", "Connected User Experiences", "Telemetry"),
    };

    // ─── Helpers ─────────────────────────────────────────────────

    private static string NormalizeStartMode(string startMode)
    {
        return startMode switch
        {
            "Auto" => "Automatic",
            "Manual" => "Manual",
            "Disabled" => "Disabled",
            "Boot" => "Boot",
            "System" => "System",
            _ => startMode
        };
    }
}
