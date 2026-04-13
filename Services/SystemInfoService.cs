using System.IO;
using System.Management;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Gathers comprehensive system information via WMI and .NET APIs.
/// Includes machine identity, OS details, hardware specs, performance metrics, battery, and disk info.
/// </summary>
public static class SystemInfoService
{
    private static readonly LoggingService _log = LoggingService.Instance;

    /// <summary>
    /// Collects all system information in one call.
    /// </summary>
    public static async Task<SystemInfo> GetSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new SystemInfo();

            try
            {
                // ─── Machine Identity ───────────────────────────────
                info.Hostname = Environment.MachineName;
                info.Username = $"{Environment.UserDomainName}\\{Environment.UserName}";
                info.Domain = Environment.UserDomainName;

                // ─── OS Information ─────────────────────────────────
                info.OSVersion = GetWmiValue("Win32_OperatingSystem", "Caption");
                info.OSBuild = GetWmiValue("Win32_OperatingSystem", "BuildNumber");

                // ─── Uptime ─────────────────────────────────────────
                info.Uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);

                // ─── CPU ────────────────────────────────────────────
                info.CpuName = GetWmiValue("Win32_Processor", "Name");
                info.CpuUsagePercent = GetCpuUsage();

                // ─── RAM ────────────────────────────────────────────
                GetMemoryInfo(info);

                // ─── Disks ──────────────────────────────────────────
                info.Disks = GetDiskInfo();

                // ─── BIOS & Hardware ────────────────────────────────
                info.BiosVersion = GetWmiValue("Win32_BIOS", "SMBIOSBIOSVersion");
                info.SerialNumber = GetWmiValue("Win32_BIOS", "SerialNumber");
                info.Manufacturer = GetWmiValue("Win32_ComputerSystem", "Manufacturer");
                info.Model = GetWmiValue("Win32_ComputerSystem", "Model");

                // ─── Windows Activation ─────────────────────────────
                info.WindowsActivation = GetWindowsActivationStatus();

                // ─── Battery ────────────────────────────────────────
                GetBatteryInfo(info);
            }
            catch (Exception ex)
            {
                _log.Error("SystemInfo", "Error gathering system information", ex.Message);
            }

            return info;
        });
    }

    /// <summary>
    /// Gets current CPU usage percentage via WMI.
    /// </summary>
    public static double GetCpuUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT LoadPercentage FROM Win32_Processor");
            foreach (var obj in searcher.Get())
            {
                return Convert.ToDouble(obj["LoadPercentage"]);
            }
        }
        catch { }
        return 0;
    }

    /// <summary>
    /// Gets current RAM usage.
    /// </summary>
    public static (long usedMB, long totalMB, double percent) GetMemoryUsage()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in searcher.Get())
            {
                long totalKB = Convert.ToInt64(obj["TotalVisibleMemorySize"]);
                long freeKB = Convert.ToInt64(obj["FreePhysicalMemory"]);
                long totalMB = totalKB / 1024;
                long usedMB = (totalKB - freeKB) / 1024;
                double percent = totalKB > 0 ? ((double)(totalKB - freeKB) / totalKB) * 100 : 0;
                return (usedMB, totalMB, percent);
            }
        }
        catch { }
        return (0, 0, 0);
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private static void GetMemoryInfo(SystemInfo info)
    {
        var (used, total, pct) = GetMemoryUsage();
        info.UsedRamMB = used;
        info.TotalRamMB = total;
        info.FreeRamMB = total - used;
        info.RamUsagePercent = pct;
    }

    private static List<DiskInfo> GetDiskInfo()
    {
        var disks = new List<DiskInfo>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType == DriveType.Fixed)
                {
                    double totalGB = drive.TotalSize / (1024.0 * 1024 * 1024);
                    double freeGB = drive.TotalFreeSpace / (1024.0 * 1024 * 1024);
                    disks.Add(new DiskInfo
                    {
                        Drive = drive.Name.TrimEnd('\\'),
                        Label = drive.VolumeLabel,
                        TotalGB = totalGB,
                        FreeGB = freeGB,
                        UsagePercent = totalGB > 0 ? ((totalGB - freeGB) / totalGB) * 100 : 0
                    });
                }
            }
        }
        catch { }
        return disks;
    }

    private static void GetBatteryInfo(SystemInfo info)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_Battery");
            var collection = searcher.Get();
            info.HasBattery = collection.Count > 0;

            foreach (var obj in collection)
            {
                info.BatteryPercent = Convert.ToInt32(obj["EstimatedChargeRemaining"] ?? 0);
                var statusCode = Convert.ToInt32(obj["BatteryStatus"] ?? 0);
                info.BatteryStatus = statusCode switch
                {
                    1 => "Discharging",
                    2 => "AC Power",
                    3 => "Fully Charged",
                    4 => "Low",
                    5 => "Critical",
                    6 => "Charging",
                    7 => "Charging (High)",
                    8 => "Charging (Low)",
                    9 => "Charging (Critical)",
                    10 => "Undefined",
                    11 => "Partially Charged",
                    _ => "Unknown"
                };
                break; // Only take the first battery
            }
        }
        catch
        {
            info.HasBattery = false;
        }
    }

    private static string GetWindowsActivationStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(
                "SELECT LicenseStatus FROM SoftwareLicensingProduct WHERE PartialProductKey IS NOT NULL AND LicenseFamily IS NOT NULL");
            foreach (var obj in searcher.Get())
            {
                var status = Convert.ToInt32(obj["LicenseStatus"]);
                return status switch
                {
                    0 => "Unlicensed",
                    1 => "Licensed (Activated)",
                    2 => "Out-of-Box Grace",
                    3 => "Out-of-Tolerance Grace",
                    4 => "Non-Genuine Grace",
                    5 => "Notification",
                    6 => "Extended Grace",
                    _ => "Unknown"
                };
            }
        }
        catch { }
        return "Unknown";
    }

    /// <summary>
    /// Reads a single WMI property value.
    /// </summary>
    private static string GetWmiValue(string wmiClass, string propertyName)
    {
        try
        {
            using var searcher = new ManagementObjectSearcher($"SELECT {propertyName} FROM {wmiClass}");
            foreach (var obj in searcher.Get())
            {
                return obj[propertyName]?.ToString()?.Trim() ?? string.Empty;
            }
        }
        catch { }
        return string.Empty;
    }
}
