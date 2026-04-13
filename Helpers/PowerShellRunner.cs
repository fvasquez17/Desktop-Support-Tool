using System.Diagnostics;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Helpers;

/// <summary>
/// Executes PowerShell and CMD commands silently, capturing output.
/// Supports both standard and elevated (UAC) execution.
/// </summary>
public static class PowerShellRunner
{
    /// <summary>
    /// Runs a PowerShell command asynchronously.
    /// </summary>
    /// <param name="command">The PowerShell command string.</param>
    /// <param name="elevated">If true, launches with RunAs verb (triggers UAC).</param>
    /// <param name="timeoutSeconds">Maximum execution time before killing the process.</param>
    public static async Task<ActionResult> RunAsync(string command, bool elevated = false, int timeoutSeconds = 120)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-NoProfile -NonInteractive -ExecutionPolicy Bypass -Command \"{EscapeCommand(command)}\"",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (elevated)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
            }
            else
            {
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
            }

            using var process = new Process { StartInfo = psi };
            process.Start();

            string output = string.Empty;
            string error = string.Empty;

            if (!elevated)
            {
                // Read streams asynchronously to avoid deadlocks
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return ActionResult.Fail("Command timed out");
                }

                output = await outputTask;
                error = await errorTask;
            }
            else
            {
                // For elevated processes, we can't redirect output
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return ActionResult.Fail("Elevated command timed out");
                }
            }

            if (process.ExitCode == 0)
            {
                return ActionResult.Ok(
                    "Command completed successfully",
                    string.IsNullOrWhiteSpace(output) ? "(no output)" : output.Trim());
            }
            else
            {
                var msg = !string.IsNullOrWhiteSpace(error) ? error.Trim() : $"Exit code: {process.ExitCode}";
                return ActionResult.Fail(msg, output.Trim());
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // ERROR_CANCELLED – user declined the UAC prompt
            return ActionResult.Fail("Operation cancelled — UAC prompt was declined.");
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Error executing command: {ex.Message}");
        }
    }

    /// <summary>
    /// Runs a CMD command (e.g., ipconfig, gpupdate) asynchronously.
    /// </summary>
    public static async Task<ActionResult> RunCmdAsync(string command, bool elevated = false, int timeoutSeconds = 120)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                CreateNoWindow = true,
                WindowStyle = ProcessWindowStyle.Hidden,
            };

            if (elevated)
            {
                psi.UseShellExecute = true;
                psi.Verb = "runas";
            }
            else
            {
                psi.UseShellExecute = false;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
            }

            using var process = new Process { StartInfo = psi };
            process.Start();

            string output = string.Empty;
            string error = string.Empty;

            if (!elevated)
            {
                var outputTask = process.StandardOutput.ReadToEndAsync();
                var errorTask = process.StandardError.ReadToEndAsync();

                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    try { process.Kill(true); } catch { }
                    return ActionResult.Fail("Command timed out");
                }

                output = await outputTask;
                error = await errorTask;
            }
            else
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
                try
                {
                    await process.WaitForExitAsync(cts.Token);
                }
                catch (OperationCanceledException)
                {
                    return ActionResult.Fail("Elevated command timed out");
                }
            }

            if (process.ExitCode == 0)
            {
                return ActionResult.Ok("Command completed successfully",
                    string.IsNullOrWhiteSpace(output) ? "(no output)" : output.Trim());
            }
            else
            {
                var msg = !string.IsNullOrWhiteSpace(error) ? error.Trim() : $"Exit code: {process.ExitCode}";
                return ActionResult.Fail(msg, output.Trim());
            }
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            return ActionResult.Fail("Operation cancelled — UAC prompt was declined.");
        }
        catch (Exception ex)
        {
            return ActionResult.Fail($"Error executing command: {ex.Message}");
        }
    }

    /// <summary>
    /// Escapes double quotes in PowerShell command strings.
    /// </summary>
    private static string EscapeCommand(string command)
    {
        // Replace double quotes with escaped double quotes for the outer cmd wrapping
        return command.Replace("\"", "\\\"");
    }
}
