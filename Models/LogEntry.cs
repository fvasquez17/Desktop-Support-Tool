namespace DesktopSupportTool.Models;

/// <summary>
/// A single log entry for the application's diagnostic log.
/// </summary>
public class LogEntry
{
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public LogLevel Level { get; set; } = LogLevel.Info;
    public string Category { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;

    /// <summary>
    /// Formats the entry for file output.
    /// </summary>
    public string ToFileString() =>
        $"[{Timestamp:yyyy-MM-dd HH:mm:ss}] [{Level}] [{Category}] {Message}" +
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
