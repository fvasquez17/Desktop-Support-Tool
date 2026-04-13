using System.IO;
using System.Net.NetworkInformation;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Background health check service. Periodically monitors system health
/// and raises events when issues are detected.
/// </summary>
public sealed class HealthCheckService : IDisposable
{
    private static readonly Lazy<HealthCheckService> _instance = new(() => new HealthCheckService());
    public static HealthCheckService Instance => _instance.Value;

    private readonly LoggingService _log = LoggingService.Instance;
    private Timer? _timer;
    private bool _disposed;

    /// <summary>Current health status snapshot.</summary>
    public HealthStatus CurrentStatus { get; private set; } = new();

    /// <summary>Fires when health status changes.</summary>
    public event Action<HealthStatus>? StatusChanged;

    private HealthCheckService() { }

    /// <summary>
    /// Starts background health monitoring (default: every 60 seconds).
    /// </summary>
    public void Start(int intervalSeconds = 60)
    {
        _timer?.Dispose();
        _timer = new Timer(async _ => await RunChecksAsync(),
            null, TimeSpan.Zero, TimeSpan.FromSeconds(intervalSeconds));

        _log.Info("HealthCheck", "Background health monitoring started");
    }

    /// <summary>
    /// Stops background monitoring.
    /// </summary>
    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
        _log.Info("HealthCheck", "Background health monitoring stopped");
    }

    /// <summary>
    /// Runs all health checks on demand.
    /// </summary>
    public async Task<HealthStatus> RunChecksAsync()
    {
        var status = new HealthStatus();

        try
        {
            // CPU check
            status.CpuUsage = SystemInfoService.GetCpuUsage();
            status.CpuHealthy = status.CpuUsage < 90;

            // RAM check
            var (used, total, pct) = SystemInfoService.GetMemoryUsage();
            status.RamUsagePercent = pct;
            status.RamHealthy = pct < 90;

            // Disk check (lowest free space on any fixed drive)
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    double freeGB = drive.TotalFreeSpace / (1024.0 * 1024 * 1024);
                    if (freeGB < status.LowestDiskFreeGB || status.LowestDiskFreeGB == 0)
                    {
                        status.LowestDiskFreeGB = freeGB;
                        status.LowestDiskDrive = drive.Name;
                    }
                }
            }
            status.DiskHealthy = status.LowestDiskFreeGB > 5; // Warning below 5 GB

            // Network check
            status.NetworkConnected = await CheckNetworkAsync();

            // Pending reboot check
            status.PendingReboot = CheckPendingReboot();

            // Overall
            status.OverallHealthy = status.CpuHealthy && status.RamHealthy
                && status.DiskHealthy && status.NetworkConnected && !status.PendingReboot;

            status.LastChecked = DateTime.Now;

            // Build issue list
            status.Issues.Clear();
            if (!status.CpuHealthy)
                status.Issues.Add($"High CPU usage: {status.CpuUsage:F0}%");
            if (!status.RamHealthy)
                status.Issues.Add($"High RAM usage: {status.RamUsagePercent:F0}%");
            if (!status.DiskHealthy)
                status.Issues.Add($"Low disk space on {status.LowestDiskDrive}: {status.LowestDiskFreeGB:F1} GB free");
            if (!status.NetworkConnected)
                status.Issues.Add("No network connectivity");
            if (status.PendingReboot)
                status.Issues.Add("Pending system reboot");
        }
        catch (Exception ex)
        {
            _log.Error("HealthCheck", "Error during health check", ex.Message);
        }

        CurrentStatus = status;
        StatusChanged?.Invoke(status);
        return status;
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private static async Task<bool> CheckNetworkAsync()
    {
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync("8.8.8.8", 3000);
            return reply.Status == IPStatus.Success;
        }
        catch
        {
            return false;
        }
    }

    private static bool CheckPendingReboot()
    {
        try
        {
            // Check common registry keys that indicate a pending reboot
            var paths = new[]
            {
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending", Microsoft.Win32.RegistryHive.LocalMachine),
                (@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired", Microsoft.Win32.RegistryHive.LocalMachine),
            };

            foreach (var (path, hive) in paths)
            {
                using var baseKey = Microsoft.Win32.RegistryKey.OpenBaseKey(hive, Microsoft.Win32.RegistryView.Registry64);
                using var key = baseKey.OpenSubKey(path);
                if (key != null) return true;
            }

            // Also check PendingFileRenameOperations
            using var sessionKey = Microsoft.Win32.RegistryKey.OpenBaseKey(
                Microsoft.Win32.RegistryHive.LocalMachine, Microsoft.Win32.RegistryView.Registry64);
            using var smKey = sessionKey.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            var pendingOps = smKey?.GetValue("PendingFileRenameOperations");
            if (pendingOps is string[] ops && ops.Length > 0) return true;
        }
        catch { }

        return false;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _timer?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Snapshot of system health at a point in time.
/// </summary>
public class HealthStatus
{
    public bool OverallHealthy { get; set; } = true;
    public DateTime LastChecked { get; set; } = DateTime.Now;

    public double CpuUsage { get; set; }
    public bool CpuHealthy { get; set; } = true;

    public double RamUsagePercent { get; set; }
    public bool RamHealthy { get; set; } = true;

    public double LowestDiskFreeGB { get; set; }
    public string LowestDiskDrive { get; set; } = "";
    public bool DiskHealthy { get; set; } = true;

    public bool NetworkConnected { get; set; } = true;
    public bool PendingReboot { get; set; }

    public List<string> Issues { get; set; } = new();
}
