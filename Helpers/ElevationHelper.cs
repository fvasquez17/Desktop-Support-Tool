using System.Security.Principal;

namespace DesktopSupportTool.Helpers;

/// <summary>
/// Utility for checking and managing privilege elevation (UAC).
/// </summary>
public static class ElevationHelper
{
    /// <summary>
    /// Returns true if the current process is running with administrative privileges.
    /// </summary>
    public static bool IsRunningAsAdmin()
    {
        try
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Re-launches the current application with elevated (admin) privileges.
    /// The current instance should exit after calling this.
    /// </summary>
    public static bool RelaunchAsAdmin()
    {
        try
        {
            var exePath = Environment.ProcessPath
                ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;

            if (string.IsNullOrEmpty(exePath)) return false;

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = exePath,
                UseShellExecute = true,
                Verb = "runas"
            };

            System.Diagnostics.Process.Start(psi);
            return true;
        }
        catch (System.ComponentModel.Win32Exception ex) when (ex.NativeErrorCode == 1223)
        {
            // User declined UAC
            return false;
        }
        catch
        {
            return false;
        }
    }
}
