using System.Diagnostics;

namespace DesktopSupportTool.Helpers;

/// <summary>
/// Utility for managing external processes: checking, killing, and launching.
/// </summary>
public static class ProcessHelper
{
    /// <summary>
    /// Checks if a process with the given name is currently running.
    /// </summary>
    public static bool IsRunning(string processName)
    {
        try
        {
            return Process.GetProcessesByName(processName).Length > 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Kills all instances of a process by name. Waits for exit.
    /// </summary>
    public static async Task KillAllAsync(string processName, int waitMs = 5000)
    {
        var processes = Process.GetProcessesByName(processName);
        foreach (var proc in processes)
        {
            try
            {
                proc.Kill(entireProcessTree: true);
                using var cts = new CancellationTokenSource(waitMs);
                await proc.WaitForExitAsync(cts.Token);
            }
            catch
            {
                // Process may have already exited
            }
            finally
            {
                proc.Dispose();
            }
        }
    }

    /// <summary>
    /// Launches an application or URI.
    /// </summary>
    /// <param name="path">Path to the executable or URI (e.g., ms-settings:sound).</param>
    /// <param name="args">Optional command-line arguments.</param>
    /// <param name="elevated">If true, launches with RunAs verb.</param>
    public static void Launch(string path, string args = "", bool elevated = false)
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = path,
                Arguments = args,
                UseShellExecute = true,
            };

            if (elevated)
                psi.Verb = "runas";

            Process.Start(psi);
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User cancelled UAC — silently ignore
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to launch {path}: {ex.Message}");
        }
    }

    /// <summary>
    /// Opens a Windows Settings page via ms-settings URI.
    /// </summary>
    public static void OpenSettings(string settingsUri)
    {
        Launch(settingsUri);
    }

    /// <summary>
    /// Opens a file or folder in Explorer.
    /// </summary>
    public static void OpenInExplorer(string path)
    {
        Launch("explorer.exe", $"\"{path}\"");
    }
}
