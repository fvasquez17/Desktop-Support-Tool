namespace DesktopSupportTool.Models;

/// <summary>
/// Represents a Windows service with its current state and configuration.
/// </summary>
public class ServiceInfo
{
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StartupType { get; set; } = string.Empty;
    public string Account { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string PathName { get; set; } = string.Empty;

    /// <summary>Icon glyph based on status.</summary>
    public string StatusIcon => Status switch
    {
        "Running" => "\uE73E",   // Checkmark
        "Stopped" => "\uE711",   // Cancel
        "Paused" => "\uE769",    // Pause
        "StartPending" => "\uE72C",
        "StopPending" => "\uE72C",
        _ => "\uE9CE"
    };

    /// <summary>Whether the service is currently running.</summary>
    public bool IsRunning => Status == "Running";
}
