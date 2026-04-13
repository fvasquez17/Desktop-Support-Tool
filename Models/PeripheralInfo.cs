namespace DesktopSupportTool.Models;

/// <summary>
/// Information about connected peripherals: monitors, audio devices, printers.
/// </summary>
public class PeripheralInfo
{
    public List<MonitorInfo> Monitors { get; set; } = new();
    public string AudioOutputDevice { get; set; } = string.Empty;
    public string AudioInputDevice { get; set; } = string.Empty;
    public List<PrinterInfo> Printers { get; set; } = new();
}

/// <summary>
/// Information about a connected display.
/// </summary>
public class MonitorInfo
{
    public string Name { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Resolution => $"{Width} x {Height}";
    public bool IsPrimary { get; set; }
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>
/// Information about an installed printer.
/// </summary>
public class PrinterInfo
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public string Status { get; set; } = string.Empty;
    public string PortName { get; set; } = string.Empty;
    public bool IsNetwork { get; set; }
}
