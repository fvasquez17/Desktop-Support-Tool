namespace DesktopSupportTool.Models;

/// <summary>
/// Standardized result type for all troubleshooting actions and command executions.
/// </summary>
public class ActionResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Output { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; } = DateTime.Now;

    /// <summary>Create a success result.</summary>
    public static ActionResult Ok(string message = "Completed successfully", string output = "")
        => new() { Success = true, Message = message, Output = output };

    /// <summary>Create a failure result.</summary>
    public static ActionResult Fail(string message, string output = "")
        => new() { Success = false, Message = message, Output = output };

    public override string ToString() =>
        $"[{(Success ? "OK" : "FAIL")}] {Message}";
}
