namespace DesktopSupportTool.Models;

/// <summary>
/// Represents an installed device driver.
/// </summary>
public class DriverInfo
{
    public string DeviceName { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string Manufacturer { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public string DeviceClass { get; set; } = string.Empty;
    public string ClassIcon { get; set; } = "\uE964"; // Default icon
    public string Status { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string InfName { get; set; } = string.Empty;
    public bool IsSigned { get; set; }
    public bool HasProblem { get; set; }
    public int ErrorCode { get; set; }
    public string ProblemDescription { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}

/// <summary>
/// Represents an audio endpoint device (speaker, headphones, microphone, etc.)
/// </summary>
public class AudioDeviceInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DeviceType { get; set; } = string.Empty; // "Playback" or "Recording"
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Represents a display resolution mode.
/// </summary>
public class DisplayModeInfo
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int RefreshRate { get; set; }
    public int BitsPerPixel { get; set; }
    public string Label => $"{Width} x {Height} @ {RefreshRate}Hz";

    public override string ToString() => Label;

    public override bool Equals(object? obj) =>
        obj is DisplayModeInfo other &&
        Width == other.Width && Height == other.Height && RefreshRate == other.RefreshRate;

    public override int GetHashCode() => HashCode.Combine(Width, Height, RefreshRate);
}
