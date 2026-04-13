using System.IO;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Thread-safe singleton logging service. Writes to %LOCALAPPDATA%\DesktopSupportTool\logs\.
/// Rolling daily log files with in-memory cache for the UI log viewer.
/// </summary>
public sealed class LoggingService
{
    private static readonly Lazy<LoggingService> _instance = new(() => new LoggingService());
    public static LoggingService Instance => _instance.Value;

    private readonly string _logDirectory;
    private readonly object _fileLock = new();
    private readonly List<LogEntry> _entries = new();
    private readonly object _entriesLock = new();
    private const int MaxInMemoryEntries = 5000;

    /// <summary>
    /// Fires whenever a new log entry is added (for UI binding).
    /// </summary>
    public event Action<LogEntry>? LogAdded;

    private LoggingService()
    {
        _logDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "DesktopSupportTool", "logs");

        Directory.CreateDirectory(_logDirectory);
    }

    /// <summary>
    /// Logs an informational message.
    /// </summary>
    public void Info(string category, string message, string details = "")
        => Log(LogLevel.Info, category, message, details);

    /// <summary>
    /// Logs a warning message.
    /// </summary>
    public void Warn(string category, string message, string details = "")
        => Log(LogLevel.Warning, category, message, details);

    /// <summary>
    /// Logs an error message.
    /// </summary>
    public void Error(string category, string message, string details = "")
        => Log(LogLevel.Error, category, message, details);

    /// <summary>
    /// Logs a success message.
    /// </summary>
    public void Success(string category, string message, string details = "")
        => Log(LogLevel.Success, category, message, details);

    /// <summary>
    /// Logs the result of an action.
    /// </summary>
    public void LogAction(string category, string actionName, ActionResult result)
    {
        if (result.Success)
            Success(category, $"{actionName}: {result.Message}", result.Output);
        else
            Error(category, $"{actionName}: {result.Message}", result.Output);
    }

    /// <summary>
    /// Returns a snapshot of all in-memory log entries.
    /// </summary>
    public List<LogEntry> GetEntries()
    {
        lock (_entriesLock)
        {
            return new List<LogEntry>(_entries);
        }
    }

    /// <summary>
    /// Returns entries filtered by category or level.
    /// </summary>
    public List<LogEntry> GetEntries(string? category = null, LogLevel? level = null)
    {
        lock (_entriesLock)
        {
            IEnumerable<LogEntry> query = _entries;
            if (!string.IsNullOrEmpty(category))
                query = query.Where(e => e.Category.Equals(category, StringComparison.OrdinalIgnoreCase));
            if (level.HasValue)
                query = query.Where(e => e.Level == level.Value);
            return query.ToList();
        }
    }

    /// <summary>
    /// Exports all log entries to a single text file on the desktop and returns the path.
    /// </summary>
    public string ExportDiagnostics()
    {
        var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        var fileName = $"DesktopSupportTool_Diagnostics_{DateTime.Now:yyyyMMdd_HHmmss}.txt";
        var filePath = Path.Combine(desktop, fileName);

        List<LogEntry> snapshot;
        lock (_entriesLock)
        {
            snapshot = new List<LogEntry>(_entries);
        }

        var lines = new List<string>
        {
            "═══════════════════════════════════════════════════════",
            "  Desktop Support Tool — Diagnostic Export",
            $"  Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
            $"  Machine:   {Environment.MachineName}",
            $"  User:      {Environment.UserName}",
            "═══════════════════════════════════════════════════════",
            ""
        };

        foreach (var entry in snapshot)
        {
            lines.Add(entry.ToFileString());
        }

        File.WriteAllLines(filePath, lines);

        Info("Logging", $"Diagnostics exported to {filePath}");
        return filePath;
    }

    /// <summary>
    /// Returns the log directory path.
    /// </summary>
    public string GetLogDirectory() => _logDirectory;

    // ─── Internal ───────────────────────────────────────────────

    private void Log(LogLevel level, string category, string message, string details)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = level,
            Category = category,
            Message = message,
            Details = details
        };

        // Add to in-memory cache
        lock (_entriesLock)
        {
            _entries.Add(entry);
            // Trim old entries to prevent memory growth
            if (_entries.Count > MaxInMemoryEntries)
                _entries.RemoveRange(0, _entries.Count - MaxInMemoryEntries);
        }

        // Write to daily log file
        WriteToFile(entry);

        // Notify UI
        LogAdded?.Invoke(entry);
    }

    private void WriteToFile(LogEntry entry)
    {
        try
        {
            var filePath = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
            lock (_fileLock)
            {
                File.AppendAllText(filePath, entry.ToFileString() + Environment.NewLine);
            }
        }
        catch
        {
            // Logging should never crash the application
        }
    }
}
