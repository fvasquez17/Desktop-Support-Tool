namespace DesktopSupportTool.Models;

/// <summary>
/// A single log entry for the application's diagnostic log.
/// NIST AU-3 compliant: captures who, what, when, where, and outcome.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// The Windows identity (DOMAIN\User) that initiated this action.
    /// Captured automatically at creation time.
    /// </summary>
    public string User { get; set; } = Environment.UserDomainName + "\\" + Environment.UserName;

    /// <summary>
    /// The machine name where the action was performed.
    /// </summary>
    public string MachineName { get; set; } = Environment.MachineName;

    /// <summary>
    /// Formats the entry for file output (NIST AU-3 compliant).
    /// </summary>
    public string ToFileString() =>
        $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{User}@{MachineName}] [{Category}] {Message}" +
        (string.IsNullOrEmpty(Details) ? "" : $"\n    Details: {Details}");

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss}] {Message}";
}

/// <summary>
/// Log severity levels.
/// </summary>
public enum LogLevel
{
    Info,
    Warning,
    Error,
    Success
}
