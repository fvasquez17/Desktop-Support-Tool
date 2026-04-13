using Microsoft.Win32;

namespace DesktopSupportTool.Helpers;

/// <summary>
/// Registry read/write operations for auto-start registration and system queries.
/// </summary>
public static class RegistryHelper
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string AppName = "DesktopSupportTool";

    /// <summary>
    /// Registers the application to start automatically with Windows (HKCU — no admin required).
    /// </summary>
    public static void RegisterAutoStart(string? exePath = null)
    {
        try
        {
            exePath ??= Environment.ProcessPath ?? System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exePath)) return;

            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.SetValue(AppName, $"\"{exePath}\"");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to register auto-start: {ex.Message}");
        }
    }

    /// <summary>
    /// Removes the application from Windows auto-start.
    /// </summary>
    public static void UnregisterAutoStart()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
            key?.DeleteValue(AppName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to unregister auto-start: {ex.Message}");
        }
    }

    /// <summary>
    /// Checks if the application is currently registered for auto-start.
    /// </summary>
    public static bool IsAutoStartEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(AppName) != null;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads a registry value from HKLM (falling back to HKCU).
    /// </summary>
    public static object? ReadValue(string keyPath, string valueName, RegistryHive hive = RegistryHive.LocalMachine)
    {
        try
        {
            using var baseKey = RegistryKey.OpenBaseKey(hive, RegistryView.Registry64);
            using var key = baseKey.OpenSubKey(keyPath);
            return key?.GetValue(valueName);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Reads a string value from the registry.
    /// </summary>
    public static string ReadString(string keyPath, string valueName, RegistryHive hive = RegistryHive.LocalMachine)
    {
        return ReadValue(keyPath, valueName, hive)?.ToString() ?? string.Empty;
    }
}
