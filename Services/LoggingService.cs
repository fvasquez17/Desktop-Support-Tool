using System.Diagnostics;
using System.IO;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Thread-safe singleton logging service with security hardening:
///   - NIST AU-2:  Logs all security-relevant events
///   - NIST AU-3:  Captures who, what, when, where, outcome
///   - NIST AU-9:  Writes to Windows Event Log (tamper-resistant)
///   - NIST AU-11: Rolling retention with configurable max age
///   - CIS 8.5:    Windows Event Log supports central SIEM forwarding
///   - HIPAA §164.312(b): Tamper-resistant audit controls
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
    /// Maximum number of days to retain log files. Set to 0 to disable rotation.
    /// Default: 90 days (NIST AU-11 recommended minimum).
    /// </summary>
    public int RetentionDays { get; set; } = 90;

    /// <summary>
    /// Enable/disable writing to Windows Event Log.
    /// Default: true (recommended for HIPAA/CIS compliance).
    /// </summary>
    public bool WriteToEventLog { get; set; } = true;

    private const string EventLogSource = "DesktopSupportTool";
    private const string EventLogName = "Application";

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

        // Register Event Log source (safe to call multiple times)
        RegisterEventLogSource();

        // Enforce log retention on startup
        EnforceRetention();
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
    /// Logs security-relevant events (elevation, admin actions, UAC prompts).
    /// NIST AU-2 / HIPAA §164.312(b)
    /// </summary>
    public void Security(string category, string message, string details = "")
    {
        var entry = new LogEntry
        {
            Timestamp = DateTime.Now,
            Level = LogLevel.Warning,
            Category = $"SECURITY:{category}",
            Message = message,
            Details = details
        };

        AddEntry(entry);
        WriteToFile(entry);
        WriteToWindowsEventLog(entry, EventLogEntryType.Warning);
        LogAdded?.Invoke(entry);
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
            $"  User:      {Environment.UserDomainName}\\{Environment.UserName}",
            $"  Elevated:  {Helpers.ElevationHelper.IsRunningAsAdmin()}",
            "═══════════════════════════════════════════════════════",
            ""
        };

        foreach (var entry in snapshot)
        {
            lines.Add(entry.ToFileString());
        }

        File.WriteAllLines(filePath, lines);

        Security("Export", $"Diagnostic log exported to {filePath}");
        return filePath;
    }

    /// <summary>
    /// Returns the log directory path.
    /// </summary>
    public string GetLogDirectory() => _logDirectory;

    // ═══════════════════════════════════════════════════════════
    //  INTERNAL LOGGING
    // ═══════════════════════════════════════════════════════════

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

        AddEntry(entry);
        WriteToFile(entry);

        // Write errors and security events to Windows Event Log
        if (level == LogLevel.Error || category.StartsWith("SECURITY"))
        {
            var eventType = level == LogLevel.Error
                ? EventLogEntryType.Error
                : EventLogEntryType.Warning;
            WriteToWindowsEventLog(entry, eventType);
        }

        LogAdded?.Invoke(entry);
    }

    private void AddEntry(LogEntry entry)
    {
        lock (_entriesLock)
        {
            _entries.Add(entry);
            if (_entries.Count > MaxInMemoryEntries)
                _entries.RemoveRange(0, _entries.Count - MaxInMemoryEntries);
        }
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

    // ═══════════════════════════════════════════════════════════
    //  WINDOWS EVENT LOG (NIST AU-9 / CIS 8.5 / HIPAA)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Registers the application as a Windows Event Log source.
    /// This allows events to appear in Event Viewer → Application log,
    /// and be forwarded by SIEM tools (Splunk, Sentinel, etc.).
    /// </summary>
    private void RegisterEventLogSource()
    {
        try
        {
            if (!EventLog.SourceExists(EventLogSource))
            {
                EventLog.CreateEventSource(EventLogSource, EventLogName);
            }
        }
        catch
        {
            // Source creation requires admin on first run — silently skip
            // Events will still work if source was pre-registered by SCCM/GPO
        }
    }

    /// <summary>
    /// Writes an entry to the Windows Event Log for tamper-resistant auditing.
    /// Windows Event Log is protected by SYSTEM-level ACLs and can be
    /// forwarded to a central SIEM via Windows Event Forwarding (WEF).
    /// </summary>
    private void WriteToWindowsEventLog(LogEntry entry, EventLogEntryType eventType)
    {
        if (!WriteToEventLog) return;

        try
        {
            var message = $"[{entry.Category}] {entry.Message}\n" +
                          $"User: {entry.User}\n" +
                          $"Machine: {entry.MachineName}\n" +
                          $"Time: {entry.Timestamp:yyyy-MM-dd HH:mm:ss}\n" +
                          (string.IsNullOrEmpty(entry.Details) ? "" : $"Details: {entry.Details}");

            EventLog.WriteEntry(EventLogSource, message, eventType);
        }
        catch
        {
            // If Event Log source isn't registered, skip silently
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  LOG RETENTION (NIST AU-11)
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Deletes log files older than RetentionDays.
    /// Called on startup and can be called manually.
    /// NIST AU-11: Audit Record Retention (90-day default).
    /// </summary>
    public void EnforceRetention()
    {
        if (RetentionDays <= 0) return;

        try
        {
            var cutoff = DateTime.Now.AddDays(-RetentionDays);
            var logFiles = Directory.GetFiles(_logDirectory, "log_*.txt");

            foreach (var file in logFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.LastWriteTime < cutoff)
                    {
                        fileInfo.Delete();
                    }
                }
                catch { }
            }
        }
        catch { }
    }
}
