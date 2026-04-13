namespace DesktopSupportTool.Models;

/// <summary>
/// Holds comprehensive system information gathered from WMI and .NET APIs.
/// </summary>
public class SystemInfo
{
    // Machine identity
    public string Hostname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Domain { get; set; } = string.Empty;
    public string SerialNumber { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;

    // Operating system
    public string OSVersion { get; set; } = string.Empty;
    public string OSBuild { get; set; } = string.Empty;
    public string WindowsActivation { get; set; } = string.Empty;

    // Hardware
    public string CpuName { get; set; } = string.Empty;
    public string BiosVersion { get; set; } = string.Empty;
    public TimeSpan Uptime { get; set; }

    // Performance (live)
    public double CpuUsagePercent { get; set; }
    public double RamUsagePercent { get; set; }
    public long TotalRamMB { get; set; }
    public long UsedRamMB { get; set; }
    public long FreeRamMB { get; set; }

    // Disks
    public List<DiskInfo> Disks { get; set; } = new();

    // Battery (laptops)
    public bool HasBattery { get; set; }
    public int BatteryPercent { get; set; }
    public string BatteryStatus { get; set; } = string.Empty;

    /// <summary>
    /// Formats all system info into a single multi-line string for clipboard / ticket pasting.
    /// </summary>
    public string ToClipboardText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== SYSTEM INFORMATION ===");
        sb.AppendLine($"Hostname:       {Hostname}");
        sb.AppendLine($"Username:       {Username}");
        sb.AppendLine($"Domain:         {Domain}");
        sb.AppendLine($"Serial Number:  {SerialNumber}");
        sb.AppendLine($"Manufacturer:   {Manufacturer}");
        sb.AppendLine($"Model:          {Model}");
        sb.AppendLine();
        sb.AppendLine($"OS Version:     {OSVersion}");
        sb.AppendLine($"OS Build:       {OSBuild}");
        sb.AppendLine($"Activation:     {WindowsActivation}");
        sb.AppendLine();
        sb.AppendLine($"CPU:            {CpuName}");
        sb.AppendLine($"BIOS:           {BiosVersion}");
        sb.AppendLine($"Uptime:         {Uptime:d\\.hh\\:mm\\:ss}");
        sb.AppendLine($"CPU Usage:      {CpuUsagePercent:F1}%");
        sb.AppendLine($"RAM:            {UsedRamMB:N0} / {TotalRamMB:N0} MB ({RamUsagePercent:F1}%)");
        sb.AppendLine();
        foreach (var d in Disks)
        {
            sb.AppendLine($"Disk {d.Drive}:      {d.FreeGB:N1} GB free / {d.TotalGB:N1} GB ({d.UsagePercent:F1}% used)");
        }
        if (HasBattery)
        {
            sb.AppendLine();
            sb.AppendLine($"Battery:        {BatteryPercent}% ({BatteryStatus})");
        }
        return sb.ToString();
    }
}

/// <summary>
/// Information about a single logical disk.
/// </summary>
public class DiskInfo
{
    public string Drive { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public double TotalGB { get; set; }
    public double FreeGB { get; set; }
    public double UsagePercent { get; set; }
}
