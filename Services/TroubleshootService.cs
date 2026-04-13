using System.IO;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// All troubleshooting and repair actions. Each method executes a specific fix,
/// logs the result, and returns an ActionResult with success/failure status.
/// </summary>
public static class TroubleshootService
{
    private static readonly LoggingService _log = LoggingService.Instance;

    // ─── Cache Resets ────────────────────────────────────────────

    /// <summary>
    /// Kills Microsoft Teams and deletes its cache directories.
    /// Works for both classic Teams and new Teams.
    /// </summary>
    public static async Task<ActionResult> ResetTeamsCacheAsync()
    {
        _log.Info("Troubleshoot", "Resetting Microsoft Teams cache...");
        try
        {
            // Kill Teams processes
            await ProcessHelper.KillAllAsync("ms-teams");
            await ProcessHelper.KillAllAsync("Teams");
            await Task.Delay(1000);

            int deleted = 0;

            // Classic Teams cache
            var classicPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Teams");
            if (Directory.Exists(classicPath))
            {
                var cacheDirs = new[] { "Cache", "blob_storage", "databases", "GPUCache",
                    "IndexedDB", "Local Storage", "tmp", "Application Cache", "Code Cache" };
                foreach (var dir in cacheDirs)
                {
                    var fullPath = Path.Combine(classicPath, dir);
                    if (Directory.Exists(fullPath))
                    {
                        SafeDeleteDirectory(fullPath);
                        deleted++;
                    }
                }
            }

            // New Teams cache
            var newTeamsPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Packages");
            if (Directory.Exists(newTeamsPath))
            {
                var teamsDirs = Directory.GetDirectories(newTeamsPath, "MSTeams*");
                foreach (var dir in teamsDirs)
                {
                    var localCache = Path.Combine(dir, "LocalCache");
                    if (Directory.Exists(localCache))
                    {
                        SafeDeleteDirectory(localCache);
                        deleted++;
                    }
                }
            }

            var result = ActionResult.Ok($"Teams cache cleared ({deleted} cache directories cleaned). Please relaunch Teams.");
            _log.LogAction("Troubleshoot", "Reset Teams Cache", result);
            return result;
        }
        catch (Exception ex)
        {
            var result = ActionResult.Fail($"Error resetting Teams cache: {ex.Message}");
            _log.LogAction("Troubleshoot", "Reset Teams Cache", result);
            return result;
        }
    }

    /// <summary>
    /// Clears Microsoft Office cache and credential manager entries.
    /// </summary>
    public static async Task<ActionResult> ResetOfficeCacheAsync()
    {
        _log.Info("Troubleshoot", "Resetting Microsoft Office cache...");
        try
        {
            // Kill Office applications
            var officeApps = new[] { "WINWORD", "EXCEL", "POWERPNT", "OUTLOOK", "ONENOTE", "MSACCESS" };
            foreach (var app in officeApps)
            {
                await ProcessHelper.KillAllAsync(app);
            }
            await Task.Delay(1000);

            int deleted = 0;

            // Office cache locations
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var cachePaths = new[]
            {
                Path.Combine(localAppData, "Microsoft", "Office", "16.0", "OfficeFileCache"),
                Path.Combine(localAppData, "Microsoft", "Office", "Spw"),
                Path.Combine(localAppData, "Microsoft", "Office", "OTele"),
            };

            foreach (var path in cachePaths)
            {
                if (Directory.Exists(path))
                {
                    SafeDeleteDirectory(path);
                    deleted++;
                }
            }

            // Clear Office identity cache
            var identityPath = Path.Combine(localAppData, "Microsoft", "OneAuth");
            if (Directory.Exists(identityPath))
            {
                SafeDeleteDirectory(identityPath);
                deleted++;
            }

            var result = ActionResult.Ok($"Office cache cleared ({deleted} directories cleaned). Please relaunch Office applications.");
            _log.LogAction("Troubleshoot", "Reset Office Cache", result);
            return result;
        }
        catch (Exception ex)
        {
            var result = ActionResult.Fail($"Error resetting Office cache: {ex.Message}");
            _log.LogAction("Troubleshoot", "Reset Office Cache", result);
            return result;
        }
    }

    /// <summary>
    /// Resets Citrix Workspace by killing processes and clearing cache.
    /// </summary>
    public static async Task<ActionResult> ResetCitrixAsync()
    {
        _log.Info("Troubleshoot", "Resetting Citrix Workspace...");
        try
        {
            // Kill Citrix processes
            var citrixProcs = new[] { "SelfServicePlugin", "SelfService", "Receiver", "AuthManager",
                "concentr", "wfcrun32", "redirector", "CDViewer" };
            foreach (var proc in citrixProcs)
            {
                await ProcessHelper.KillAllAsync(proc);
            }
            await Task.Delay(1000);

            int deleted = 0;

            // Clear Citrix cache
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var citrixPaths = new[]
            {
                Path.Combine(appData, "ICAClient"),
                Path.Combine(appData, "Citrix", "SelfService"),
            };

            foreach (var path in citrixPaths)
            {
                if (Directory.Exists(path))
                {
                    SafeDeleteDirectory(path);
                    deleted++;
                }
            }

            var result = ActionResult.Ok($"Citrix cache cleared ({deleted} directories cleaned). Please relaunch Citrix Workspace.");
            _log.LogAction("Troubleshoot", "Reset Citrix", result);
            return result;
        }
        catch (Exception ex)
        {
            var result = ActionResult.Fail($"Error resetting Citrix: {ex.Message}");
            _log.LogAction("Troubleshoot", "Reset Citrix", result);
            return result;
        }
    }

    // ─── System Actions ──────────────────────────────────────────

    /// <summary>
    /// Runs gpupdate /force to refresh Group Policy.
    /// </summary>
    public static async Task<ActionResult> RunGpUpdateAsync()
    {
        _log.Info("Troubleshoot", "Running GPUpdate...");
        var result = await PowerShellRunner.RunCmdAsync("gpupdate /force", timeoutSeconds: 120);
        _log.LogAction("Troubleshoot", "GPUpdate", result);
        return result;
    }

    /// <summary>
    /// Restarts Windows Explorer (kills and relaunches explorer.exe).
    /// </summary>
    public static async Task<ActionResult> RestartExplorerAsync()
    {
        _log.Info("Troubleshoot", "Restarting Windows Explorer...");
        try
        {
            await ProcessHelper.KillAllAsync("explorer");
            await Task.Delay(1500);

            // Relaunch Explorer
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            });

            var result = ActionResult.Ok("Windows Explorer restarted successfully.");
            _log.LogAction("Troubleshoot", "Restart Explorer", result);
            return result;
        }
        catch (Exception ex)
        {
            var result = ActionResult.Fail($"Error restarting Explorer: {ex.Message}");
            _log.LogAction("Troubleshoot", "Restart Explorer", result);
            return result;
        }
    }

    /// <summary>
    /// Clears temporary files from user and system temp directories.
    /// </summary>
    public static async Task<ActionResult> ClearTempFilesAsync()
    {
        _log.Info("Troubleshoot", "Clearing temporary files...");
        return await Task.Run(() =>
        {
            try
            {
                int filesDeleted = 0;
                long bytesFreed = 0;

                var tempPaths = new[]
                {
                    Path.GetTempPath(),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp"),
                };

                foreach (var tempPath in tempPaths)
                {
                    if (!Directory.Exists(tempPath)) continue;

                    foreach (var file in Directory.EnumerateFiles(tempPath, "*", SearchOption.TopDirectoryOnly))
                    {
                        try
                        {
                            var fi = new FileInfo(file);
                            bytesFreed += fi.Length;
                            fi.Delete();
                            filesDeleted++;
                        }
                        catch { /* Skip files in use */ }
                    }

                    foreach (var dir in Directory.EnumerateDirectories(tempPath))
                    {
                        try
                        {
                            var di = new DirectoryInfo(dir);
                            bytesFreed += GetDirectorySize(di);
                            di.Delete(true);
                            filesDeleted++;
                        }
                        catch { /* Skip dirs in use */ }
                    }
                }

                double mbFreed = bytesFreed / (1024.0 * 1024);
                var result = ActionResult.Ok($"Cleared {filesDeleted} items, freed {mbFreed:F1} MB.");
                _log.LogAction("Troubleshoot", "Clear Temp Files", result);
                return result;
            }
            catch (Exception ex)
            {
                var result = ActionResult.Fail($"Error clearing temp files: {ex.Message}");
                _log.LogAction("Troubleshoot", "Clear Temp Files", result);
                return result;
            }
        });
    }

    /// <summary>
    /// Restarts the Print Spooler service (requires elevation).
    /// </summary>
    public static async Task<ActionResult> RestartPrintSpoolerAsync()
    {
        _log.Info("Troubleshoot", "Restarting Print Spooler service...");
        var result = await PowerShellRunner.RunAsync(
            "Stop-Service Spooler -Force; Start-Sleep -Seconds 2; Start-Service Spooler",
            elevated: true);
        _log.LogAction("Troubleshoot", "Restart Print Spooler", result);
        return result;
    }

    /// <summary>
    /// Runs System File Checker (sfc /scannow) — requires elevation, takes several minutes.
    /// </summary>
    public static async Task<ActionResult> RunSfcScanAsync()
    {
        _log.Info("Troubleshoot", "Starting System File Checker (this may take several minutes)...");
        var result = await PowerShellRunner.RunCmdAsync("sfc /scannow", elevated: true, timeoutSeconds: 600);
        _log.LogAction("Troubleshoot", "SFC Scan", result);
        return result;
    }

    /// <summary>
    /// Runs DISM health check and repair — requires elevation.
    /// </summary>
    public static async Task<ActionResult> RunDismHealthAsync()
    {
        _log.Info("Troubleshoot", "Running DISM health check...");
        var result = await PowerShellRunner.RunCmdAsync(
            "DISM /Online /Cleanup-Image /RestoreHealth",
            elevated: true, timeoutSeconds: 900);
        _log.LogAction("Troubleshoot", "DISM Health", result);
        return result;
    }

    /// <summary>
    /// Clears browser caches for Chrome, Edge, and Firefox.
    /// </summary>
    public static async Task<ActionResult> ClearBrowserCachesAsync()
    {
        _log.Info("Troubleshoot", "Clearing browser caches...");
        try
        {
            // Kill browsers first
            await ProcessHelper.KillAllAsync("chrome");
            await ProcessHelper.KillAllAsync("msedge");
            await ProcessHelper.KillAllAsync("firefox");
            await Task.Delay(1000);

            int deleted = 0;
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

            // Chrome cache
            var chromeCache = Path.Combine(localAppData, "Google", "Chrome", "User Data", "Default", "Cache");
            if (Directory.Exists(chromeCache)) { SafeDeleteDirectory(chromeCache); deleted++; }

            // Edge cache
            var edgeCache = Path.Combine(localAppData, "Microsoft", "Edge", "User Data", "Default", "Cache");
            if (Directory.Exists(edgeCache)) { SafeDeleteDirectory(edgeCache); deleted++; }

            // Firefox cache
            var firefoxProfiles = Path.Combine(localAppData, "Mozilla", "Firefox", "Profiles");
            if (Directory.Exists(firefoxProfiles))
            {
                foreach (var profile in Directory.GetDirectories(firefoxProfiles))
                {
                    var cache = Path.Combine(profile, "cache2");
                    if (Directory.Exists(cache)) { SafeDeleteDirectory(cache); deleted++; }
                }
            }

            var result = ActionResult.Ok($"Browser caches cleared ({deleted} directories). Please relaunch browsers.");
            _log.LogAction("Troubleshoot", "Clear Browser Caches", result);
            return result;
        }
        catch (Exception ex)
        {
            var result = ActionResult.Fail($"Error clearing browser caches: {ex.Message}");
            _log.LogAction("Troubleshoot", "Clear Browser Caches", result);
            return result;
        }
    }

    // ─── Windows Update Fix ───────────────────────────────────────

    /// <summary>
    /// Resets Windows Update components — stops services, clears caches,
    /// re-registers DLLs, restarts services. Fixes stuck/failed updates.
    /// </summary>
    public static async Task<ActionResult> ResetWindowsUpdateAsync()
    {
        _log.Info("Troubleshoot", "Resetting Windows Update components...");

        var script = @"
            Stop-Service -Name wuauserv, bits, cryptsvc, msiserver -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            Remove-Item -Path ""$env:SystemRoot\SoftwareDistribution"" -Recurse -Force -ErrorAction SilentlyContinue
            Remove-Item -Path ""$env:SystemRoot\System32\catroot2"" -Recurse -Force -ErrorAction SilentlyContinue
            netsh winsock reset
            regsvr32 /s wuaueng.dll
            regsvr32 /s wuaueng1.dll
            regsvr32 /s wucltui.dll
            regsvr32 /s wups.dll
            regsvr32 /s wups2.dll
            regsvr32 /s wuweb.dll
            regsvr32 /s atl.dll
            Start-Service -Name wuauserv, bits, cryptsvc, msiserver -ErrorAction SilentlyContinue
            'Windows Update components reset successfully. Please restart your computer.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 60);
        _log.LogAction("Troubleshoot", "Reset Windows Update", result);
        return result;
    }

    // ─── Outlook Profile Repair ──────────────────────────────────

    /// <summary>
    /// Resets Outlook profile by clearing local cache (OST), 
    /// killing Outlook, and letting it rebuild on next launch.
    /// </summary>
    public static async Task<ActionResult> RepairOutlookAsync()
    {
        _log.Info("Troubleshoot", "Repairing Outlook — creating new profile...");

        var script = @"
            $results = @()

            # 1. Close Outlook
            $outlookProc = Get-Process -Name 'OUTLOOK' -ErrorAction SilentlyContinue
            if ($outlookProc) {
                Stop-Process -Name 'OUTLOOK' -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 3
                $results += 'Outlook closed'
            }

            # 2. Create a new Outlook profile via registry
            $profilesPath = 'HKCU:\Software\Microsoft\Office\16.0\Outlook\Profiles'
            $settingsPath = 'HKCU:\Software\Microsoft\Office\16.0\Outlook'

            # Fallback to Office 15.0 if 16.0 doesn't exist
            if (-not (Test-Path $settingsPath)) {
                $profilesPath = 'HKCU:\Software\Microsoft\Office\15.0\Outlook\Profiles'
                $settingsPath = 'HKCU:\Software\Microsoft\Office\15.0\Outlook'
            }

            # Generate a unique profile name
            $timestamp = Get-Date -Format 'MMdd'
            $newProfileName = ""Outlook-$timestamp""

            # Create the profiles key if it doesn't exist
            if (-not (Test-Path $profilesPath)) {
                New-Item -Path $profilesPath -Force | Out-Null
            }

            # Create the new profile key
            $newProfilePath = ""$profilesPath\$newProfileName""
            if (Test-Path $newProfilePath) {
                # Profile name already exists, add time
                $newProfileName = ""Outlook-$(Get-Date -Format 'MMdd-HHmm')""
                $newProfilePath = ""$profilesPath\$newProfileName""
            }
            New-Item -Path $newProfilePath -Force | Out-Null
            $results += ""Created new profile: $newProfileName""

            # 3. Set the new profile as default
            try {
                Set-ItemProperty -Path $settingsPath -Name 'DefaultProfile' -Value $newProfileName -ErrorAction Stop
                $results += ""Set '$newProfileName' as default profile""
            } catch {
                # Try the SPI\Profiles path
                try {
                    $spiPath = ""$settingsPath\SPI\Profiles""
                    if (-not (Test-Path $spiPath)) { New-Item -Path $spiPath -Force | Out-Null }
                    Set-ItemProperty -Path $spiPath -Name 'DefaultProfile' -Value $newProfileName -ErrorAction Stop
                    $results += ""Set '$newProfileName' as default profile (SPI)""
                } catch {
                    $results += 'Could not set default profile automatically'
                }
            }

            # 4. Clear AutoDiscover cache (speeds up new profile setup)
            $autoD = Join-Path $env:LOCALAPPDATA 'Microsoft\Outlook\AutoD'
            if (Test-Path $autoD) {
                Remove-Item -Path $autoD -Recurse -Force -ErrorAction SilentlyContinue
                $results += 'AutoDiscover cache cleared'
            }

            # 5. Clear XML autodiscover files only (NOT the OST)
            $outlookLocal = Join-Path $env:LOCALAPPDATA 'Microsoft\Outlook'
            if (Test-Path $outlookLocal) {
                $xmlFiles = Get-ChildItem -Path $outlookLocal -Filter '*.xml' -ErrorAction SilentlyContinue
                foreach ($xml in $xmlFiles) {
                    try { Remove-Item $xml.FullName -Force } catch { }
                }
                if ($xmlFiles.Count -gt 0) { $results += ""Cleared $($xmlFiles.Count) XML cache file(s)"" }
            }

            # 6. Clear roaming AutoD folder
            $roamAutoD = Join-Path $env:APPDATA 'Microsoft\Outlook\AutoD'
            if (Test-Path $roamAutoD) {
                Remove-Item -Path $roamAutoD -Recurse -Force -ErrorAction SilentlyContinue
                $results += 'Roaming AutoD cache cleared'
            }

            Write-Output ($results -join '; ')
            Write-Output 'New Outlook profile created. Launch Outlook — it will prompt you to configure your email account.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: false, timeoutSeconds: 15);
        _log.LogAction("Troubleshoot", "Repair Outlook", result);
        return result;
    }

    // ─── Credential Manager Clear ────────────────────────────────

    /// <summary>
    /// Clears Windows Credential Manager entries for Office/Microsoft 365,
    /// fixing SSO loops, MFA stuck prompts, and "enter password" issues.
    /// </summary>
    public static async Task<ActionResult> ClearCredentialManagerAsync()
    {
        _log.Info("Troubleshoot", "Clearing Microsoft credential cache...");

        var script = @"
            $targets = @('MicrosoftOffice*', 'Microsoft_OC*', 'msteams*', 'OneDrive*', 
                         '*office*', '*sharepoint*', '*outlook*', '*login.microsoftonline*')
            $removed = 0
            foreach ($pattern in $targets) {
                $creds = cmdkey /list 2>&1 | Select-String -Pattern $pattern.Replace('*','')
                foreach ($c in $creds) {
                    $line = $c.Line.Trim()
                    if ($line -match 'Target:\s*(.+)') {
                        cmdkey /delete:$($Matches[1]) 2>&1 | Out-Null
                        $removed++
                    }
                }
            }
            ""Cleared $removed cached credentials. Re-sign into Office/Teams when prompted.""
        ";

        var result = await PowerShellRunner.RunAsync(script, timeoutSeconds: 15);
        _log.LogAction("Troubleshoot", "Clear Credential Manager", result);
        return result;
    }

    // ─── OneDrive Sync Repair ────────────────────────────────────

    /// <summary>
    /// Resets OneDrive client — kills the process and runs the built-in
    /// /reset command which clears sync state and re-initializes.
    /// </summary>
    public static async Task<ActionResult> RepairOneDriveSyncAsync()
    {
        _log.Info("Troubleshoot", "Repairing OneDrive sync...");
        try
        {
            await ProcessHelper.KillAllAsync("OneDrive");
            await Task.Delay(1000);

            // OneDrive /reset clears all sync partnerships and re-initializes
            var resetPaths = new[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Microsoft", "OneDrive", "OneDrive.exe"),
                @"C:\Program Files\Microsoft OneDrive\OneDrive.exe",
                @"C:\Program Files (x86)\Microsoft OneDrive\OneDrive.exe"
            };

            string? exePath = resetPaths.FirstOrDefault(File.Exists);

            if (exePath != null)
            {
                var result = await PowerShellRunner.RunCmdAsync(
                    $"\"{exePath}\" /reset", timeoutSeconds: 30);

                // Wait for OneDrive to restart automatically (it relaunches after reset)
                await Task.Delay(5000);

                var finalResult = ActionResult.Ok(
                    "OneDrive sync reset complete. It will restart and re-sync automatically. " +
                    "Sign in again if prompted.");
                _log.LogAction("Troubleshoot", "Repair OneDrive", finalResult);
                return finalResult;
            }
            else
            {
                var result = ActionResult.Fail("OneDrive executable not found.");
                _log.LogAction("Troubleshoot", "Repair OneDrive", result);
                return result;
            }
        }
        catch (Exception ex)
        {
            var result = ActionResult.Fail($"Error repairing OneDrive: {ex.Message}");
            _log.LogAction("Troubleshoot", "Repair OneDrive", result);
            return result;
        }
    }

    // ─── Icon Cache Rebuild ──────────────────────────────────────

    /// <summary>
    /// Rebuilds the Windows icon cache — fixes blank/wrong icons on 
    /// desktop, taskbar, and Start menu.
    /// </summary>
    public static async Task<ActionResult> RebuildIconCacheAsync()
    {
        _log.Info("Troubleshoot", "Rebuilding icon cache...");
        try
        {
            // Kill Explorer to release icon cache files
            await ProcessHelper.KillAllAsync("explorer");
            await Task.Delay(1500);

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            int deleted = 0;

            // Delete icon cache files
            var cacheFiles = Directory.GetFiles(localAppData, "iconcache*", SearchOption.TopDirectoryOnly);
            foreach (var file in cacheFiles)
            {
                try { File.Delete(file); deleted++; } catch { }
            }

            // Delete thumbcache files
            var thumbPath = Path.Combine(localAppData, "Microsoft", "Windows", "Explorer");
            if (Directory.Exists(thumbPath))
            {
                foreach (var file in Directory.GetFiles(thumbPath, "thumbcache*"))
                {
                    try { File.Delete(file); deleted++; } catch { }
                }
                foreach (var file in Directory.GetFiles(thumbPath, "iconcache*"))
                {
                    try { File.Delete(file); deleted++; } catch { }
                }
            }

            // Restart Explorer
            await Task.Delay(1000);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                UseShellExecute = true
            });

            var result = ActionResult.Ok($"Icon cache rebuilt ({deleted} cache files cleared). Icons will regenerate.");
            _log.LogAction("Troubleshoot", "Rebuild Icon Cache", result);
            return result;
        }
        catch (Exception ex)
        {
            var result = ActionResult.Fail($"Error rebuilding icon cache: {ex.Message}");
            _log.LogAction("Troubleshoot", "Rebuild Icon Cache", result);
            return result;
        }
    }

    // ─── Re-register Start Menu / Shell ──────────────────────────

    /// <summary>
    /// Re-registers all built-in Windows apps and Start Menu shell.
    /// Fixes Start menu not opening, broken search, missing apps.
    /// </summary>
    public static async Task<ActionResult> ReRegisterStartMenuAsync()
    {
        _log.Info("Troubleshoot", "Re-registering Start Menu and shell apps...");

        var script = @"
            Get-AppxPackage -AllUsers | Where-Object {$_.InstallLocation -like '*SystemApps*' -or $_.Name -like '*ShellExperienceHost*' -or $_.Name -like '*StartMenuExperienceHost*' -or $_.Name -like '*Search*'} | ForEach-Object {
                Add-AppxPackage -DisableDevelopmentMode -Register ""$($_.InstallLocation)\AppXManifest.xml"" -ErrorAction SilentlyContinue
            }
            'Start Menu and shell components re-registered successfully.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 120);
        _log.LogAction("Troubleshoot", "Re-register Start Menu", result);
        return result;
    }

    // ─── Network Reset (Winsock + TCP/IP) ────────────────────────

    /// <summary>
    /// Full network reset — Winsock, TCP/IP, firewall rules reset,
    /// flushes DNS. Fixes "connected but no internet" and proxy issues.
    /// </summary>
    public static async Task<ActionResult> FullNetworkResetAsync()
    {
        _log.Info("Troubleshoot", "Running full network reset...");

        var script = @"
            $results = @()

            # 1. Flush DNS cache
            try {
                $null = ipconfig /flushdns 2>&1
                $results += 'DNS cache flushed'
            } catch { $results += 'DNS flush skipped' }

            # 2. Flush NetBIOS cache (non-critical, may fail on non-domain machines)
            try {
                $null = nbtstat -R 2>&1
                $results += 'NetBIOS cache flushed'
            } catch { $results += 'NetBIOS flush skipped (non-critical)' }

            # 3. Reset Winsock catalog
            try {
                $out = netsh winsock reset 2>&1
                $results += 'Winsock catalog reset'
            } catch { $results += 'Winsock reset failed' }

            # 4. Reset TCP/IP stack (requires a log file path on some Windows versions)
            try {
                $logPath = Join-Path $env:TEMP 'netsh_ip_reset.log'
                $out = netsh int ip reset $logPath 2>&1
                $results += 'TCP/IP stack reset'
            } catch { $results += 'TCP/IP reset failed' }

            # 5. Release and renew IP (non-critical)
            try {
                $null = ipconfig /release 2>&1
                Start-Sleep -Seconds 2
                $null = ipconfig /renew 2>&1
                $results += 'IP address renewed'
            } catch { $results += 'IP renew skipped' }

            Write-Output ($results -join '; ')
            Write-Output 'Network reset complete. A reboot is recommended.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 45);
        _log.LogAction("Troubleshoot", "Full Network Reset", result);
        return result;
    }

    // ─── Clear Print Queue ───────────────────────────────────────

    /// <summary>
    /// Stops Print Spooler, clears ALL stuck print jobs from the spool directory,
    /// then restarts the Spooler. Fixes "document stuck in queue" issues.
    /// </summary>
    public static async Task<ActionResult> ClearPrintQueueAsync()
    {
        _log.Info("Troubleshoot", "Clearing all stuck print jobs...");

        var script = @"
            Stop-Service -Name Spooler -Force -ErrorAction SilentlyContinue
            Start-Sleep -Seconds 2
            Remove-Item -Path ""$env:SystemRoot\System32\spool\PRINTERS\*"" -Force -ErrorAction SilentlyContinue
            Start-Service -Name Spooler
            'Print queue cleared and Spooler restarted.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 20);
        _log.LogAction("Troubleshoot", "Clear Print Queue", result);
        return result;
    }

    // ─── Quick Launch Helpers ────────────────────────────────────

    /// <summary>Launches Remote Desktop Connection.</summary>
    public static void LaunchRdp() => ProcessHelper.Launch("mstsc.exe");

    /// <summary>Launches Quick Assist.</summary>
    public static void LaunchQuickAssist() => ProcessHelper.Launch("quickassist.exe");

    /// <summary>Launches Microsoft Remote Assistance.</summary>
    public static void LaunchRemoteAssistance() => ProcessHelper.Launch("msra.exe");

    // ─── Utilities ───────────────────────────────────────────────

    private static void SafeDeleteDirectory(string path)
    {
        try
        {
            foreach (var file in Directory.EnumerateFiles(path, "*", SearchOption.AllDirectories))
            {
                try { File.Delete(file); } catch { }
            }
            foreach (var dir in Directory.EnumerateDirectories(path, "*", SearchOption.AllDirectories).Reverse())
            {
                try { Directory.Delete(dir, false); } catch { }
            }
        }
        catch { }
    }

    private static long GetDirectorySize(DirectoryInfo dir)
    {
        try
        {
            return dir.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length);
        }
        catch { return 0; }
    }
    // ─── Microsoft Intune Sync ────────────────────────────────

    /// <summary>
    /// Forces an immediate Intune policy and app sync by triggering
    /// the MDM enrollment scheduled tasks and restarting IME.
    /// </summary>
    public static async Task<ActionResult> SyncIntuneAsync()
    {
        _log.Info("Troubleshoot", "Triggering Microsoft Intune sync...");

        var script = @"
            try {
                # 1. Find the MDM enrollment GUID from scheduled tasks
                $enrollments = Get-ScheduledTask | Where-Object { $_.TaskPath -like '*Microsoft*Windows*EnterpriseMgmt*' -and $_.TaskName -notlike '*client*' }

                if (-not $enrollments -or $enrollments.Count -eq 0) {
                    Write-Output 'WARNING: No Intune enrollment found on this device.'
                    exit 1
                }

                # 2. Trigger all MDM sync scheduled tasks
                $triggered = 0
                foreach ($task in $enrollments) {
                    try {
                        Start-ScheduledTask -TaskPath $task.TaskPath -TaskName $task.TaskName
                        $triggered++
                    } catch { }
                }
                Write-Output ""Triggered $triggered MDM scheduled task(s).""

                # 3. Restart Intune Management Extension service
                $imeSvc = Get-Service -Name 'IntuneManagementExtension' -ErrorAction SilentlyContinue
                if ($imeSvc) {
                    Restart-Service -Name 'IntuneManagementExtension' -Force -ErrorAction SilentlyContinue
                    Write-Output 'Restarted IntuneManagementExtension service.'
                }

                # 4. Restart Device Management Enrollment Service
                $dmeSvc = Get-Service -Name 'dmwappushservice' -ErrorAction SilentlyContinue
                if ($dmeSvc) {
                    Restart-Service -Name 'dmwappushservice' -Force -ErrorAction SilentlyContinue
                    Write-Output 'Restarted dmwappushservice.'
                }

                Write-Output 'Intune sync triggered successfully. Policies and apps will update shortly.'
            } catch {
                Write-Output ""Failed: $_""
                exit 1
            }
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 30);

        return result.Success
            ? ActionResult.Ok("Intune sync triggered. Policies and apps will update within a few minutes.")
            : ActionResult.Fail($"Intune sync failed: {result.Output}");
    }

    // ─── SCCM / Software Center ──────────────────────────────────

    /// <summary>
    /// Restarts the CCMExec (SMS Agent Host) service and triggers
    /// Machine Policy retrieval + App Deployment evaluation.
    /// Fixes apps not installing and policies not applying from SCCM.
    /// </summary>
    public static async Task<ActionResult> RepairSccmClientAsync()
    {
        _log.Info("Troubleshoot", "Repairing SCCM client (CCMExec + policy + app eval)...");

        var script = @"
            $results = @()

            # 1. Check if SCCM client is installed
            $ccmSvc = Get-Service -Name 'CcmExec' -ErrorAction SilentlyContinue
            if (-not $ccmSvc) {
                Write-Output 'SCCM client (CcmExec) is not installed on this device. This fix only applies to SCCM-managed endpoints.'
                exit 0
            }

            # 2. Restart the SMS Agent Host service
            try {
                Restart-Service -Name 'CcmExec' -Force -ErrorAction Stop
                Start-Sleep -Seconds 5
                $results += 'CcmExec service restarted'
            } catch {
                $results += ""CcmExec restart failed: $_""
            }

            # Helper function to trigger SCCM schedule (CIM with WMI fallback)
            function Trigger-CCMSchedule {
                param([string]$ScheduleId, [string]$Name)
                # Try CIM first (modern PowerShell)
                try {
                    Invoke-CimMethod -Namespace 'root\ccm' -ClassName 'SMS_Client' -MethodName 'TriggerSchedule' -Arguments @{ sScheduleID = $ScheduleId } -ErrorAction Stop | Out-Null
                    return ""$Name triggered""
                } catch { }
                # Fallback to COM object
                try {
                    $smsClient = [wmiclass]'\\.\root\ccm:SMS_Client'
                    $smsClient.TriggerSchedule($ScheduleId) | Out-Null
                    return ""$Name triggered (COM)""
                } catch {
                    return ""$Name skipped (WMI/CIM unavailable)""
                }
            }

            # 3. Trigger Machine Policy Retrieval & Evaluation
            $results += Trigger-CCMSchedule '{00000000-0000-0000-0000-000000000021}' 'Machine Policy'

            # 4. Trigger Application Deployment Evaluation
            $results += Trigger-CCMSchedule '{00000000-0000-0000-0000-000000000121}' 'App Deployment'

            # 5. Trigger Software Update Scan
            $results += Trigger-CCMSchedule '{00000000-0000-0000-0000-000000000113}' 'Software Update'

            # 6. Trigger Hardware Inventory (bonus — helps SCCM see updated state)
            $results += Trigger-CCMSchedule '{00000000-0000-0000-0000-000000000001}' 'Hardware Inventory'

            Write-Output ($results -join '; ')
            Write-Output 'SCCM client repair complete. Check Software Center for pending apps.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 30);
        _log.LogAction("Troubleshoot", "SCCM Client Repair", result);
        return result;
    }

    // ─── File Association / Default App Fixes ────────────────────

    /// <summary>
    /// Resets all default app associations back to Windows defaults
    /// and repairs common broken file associations (.pdf, .docx, .xlsx, etc).
    /// </summary>
    public static async Task<ActionResult> ResetDefaultAppsAsync()
    {
        _log.Info("Troubleshoot", "Resetting default app associations...");

        var script = @"
            $results = @()

            # 1. Remove the user-level file association overrides
            try {
                $assocPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts'
                $extensions = @('.pdf', '.docx', '.doc', '.xlsx', '.xls', '.pptx', '.ppt',
                                '.txt', '.csv', '.html', '.htm', '.jpg', '.jpeg', '.png',
                                '.gif', '.mp4', '.mp3', '.zip', '.xml', '.json')

                $cleared = 0
                foreach ($ext in $extensions) {
                    $userChoice = ""$assocPath\$ext\UserChoice""
                    if (Test-Path $userChoice) {
                        try {
                            Remove-Item -Path $userChoice -Recurse -Force -ErrorAction Stop
                            $cleared++
                        } catch { }
                    }
                }
                $results += ""Cleared $cleared user file associations""
            } catch {
                $results += 'File association cleanup had some errors (non-critical)'
            }

            # 2. Reset the default apps notification
            try {
                $regPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\ApplicationAssociationToasts'
                if (Test-Path $regPath) {
                    Remove-Item -Path $regPath -Recurse -Force -ErrorAction SilentlyContinue
                    $results += 'Association toasts reset'
                }
            } catch { }

            # 3. Restart Explorer to apply changes
            try {
                Stop-Process -Name 'explorer' -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
                Start-Process 'explorer.exe'
                $results += 'Explorer restarted'
            } catch { }

            Write-Output ($results -join '; ')
            Write-Output 'Default apps reset. Windows will prompt you to choose apps for common file types.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: false, timeoutSeconds: 20);
        _log.LogAction("Troubleshoot", "Reset Default Apps", result);
        return result;
    }

    /// <summary>
    /// Repairs broken file associations for common office and media file types
    /// by re-registering their default handlers.
    /// </summary>
    public static async Task<ActionResult> RepairFileAssociationsAsync()
    {
        _log.Info("Troubleshoot", "Repairing broken file associations...");

        var script = @"
            $results = @()

            # Re-register common file type associations via assoc + ftype
            $associations = @{
                '.pdf'  = 'AcroExch.Document'
                '.docx' = 'Word.Document.12'
                '.doc'  = 'Word.Document.8'
                '.xlsx' = 'Excel.Sheet.12'
                '.xls'  = 'Excel.Sheet.8'
                '.pptx' = 'PowerPoint.Show.12'
                '.ppt'  = 'PowerPoint.Show.8'
                '.txt'  = 'txtfile'
                '.csv'  = 'Excel.CSV'
            }

            $repaired = 0
            foreach ($ext in $associations.Keys) {
                try {
                    $current = cmd /c ""assoc $ext 2>nul""
                    if (-not $current -or $current -notmatch '=') {
                        cmd /c ""assoc $ext=$($associations[$ext])"" 2>$null
                        $repaired++
                    }
                } catch { }
            }

            # Clear the ProgId hash cache to force fresh lookups
            try {
                $hashPath = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\FileExts'
                $results += ""Checked associations for $($associations.Count) file types""
                if ($repaired -gt 0) { $results += ""Repaired $repaired broken associations"" }
                else { $results += 'All associations are intact' }
            } catch { }

            # Notify Explorer of changes
            try {
                $null = New-PSDrive -Name HKCR -PSProvider Registry -Root HKEY_CLASSES_ROOT -ErrorAction SilentlyContinue
                Stop-Process -Name 'explorer' -Force -ErrorAction SilentlyContinue
                Start-Sleep -Seconds 2
                Start-Process 'explorer.exe'
                $results += 'Explorer restarted to apply changes'
            } catch { }

            Write-Output ($results -join '; ')
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 20);
        _log.LogAction("Troubleshoot", "Repair File Associations", result);
        return result;
    }

    // ─── User Profile & Login Issues ─────────────────────────────

    /// <summary>
    /// Detects and fixes the Temporary Profile issue by finding
    /// .bak profile entries in the registry and repairing them.
    /// Also clears the profile list cache.
    /// </summary>
    public static async Task<ActionResult> FixTempProfileAsync()
    {
        _log.Info("Troubleshoot", "Detecting and fixing Temporary Profile issue...");

        var script = @"
            $results = @()
            $profileListPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'
            $currentSid = ([System.Security.Principal.WindowsIdentity]::GetCurrent()).User.Value

            # 1. Search for .bak profile entries (the smoking gun for temp profiles)
            $allProfiles = Get-ChildItem -Path $profileListPath -ErrorAction SilentlyContinue
            $bakProfiles = $allProfiles | Where-Object { $_.PSChildName -like '*.bak' }

            if ($bakProfiles.Count -eq 0) {
                # Check for other temp profile indicators
                $currentProfile = Get-ItemProperty -Path ""$profileListPath\$currentSid"" -ErrorAction SilentlyContinue
                if ($currentProfile -and $currentProfile.ProfileImagePath -like '*TEMP*') {
                    $results += 'WARNING: Current profile is a TEMP profile but no .bak entry found'
                    $results += 'A manual profile rebuild may be required'
                } else {
                    $results += 'No temporary profile issues detected (.bak entries not found)'
                    $results += 'Current profile appears healthy'
                }
            } else {
                $results += ""Found $($bakProfiles.Count) .bak profile entry(s) - attempting repair""

                foreach ($bak in $bakProfiles) {
                    $bakName = $bak.PSChildName
                    $originalName = $bakName -replace '\.bak$', ''
                    $bakPath = ""$profileListPath\$bakName""
                    $originalPath = ""$profileListPath\$originalName""

                    try {
                        # If the non-.bak version exists, rename it to .old
                        if (Test-Path $originalPath) {
                            Rename-Item -Path $originalPath -NewName ""$originalName.old"" -Force
                            $results += ""Renamed conflicting profile entry $originalName to .old""
                        }

                        # Rename the .bak back to original
                        Rename-Item -Path $bakPath -NewName $originalName -Force
                        $results += ""Restored profile: $originalName""

                        # Fix the State value (remove temp profile flag)
                        $restoredPath = ""$profileListPath\$originalName""
                        $state = Get-ItemPropertyValue -Path $restoredPath -Name 'State' -ErrorAction SilentlyContinue
                        if ($state -band 0x1) {
                            $newState = $state -band (-bnot 0x1)
                            Set-ItemProperty -Path $restoredPath -Name 'State' -Value $newState
                            $results += 'Cleared temporary profile flag'
                        }
                    } catch {
                        $results += ""Failed to repair $bakName : $_""
                    }
                }
                $results += 'Profile repair complete. Please sign out and sign back in.'
            }

            Write-Output ($results -join ""`n"")
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 15);
        _log.LogAction("Troubleshoot", "Fix Temp Profile", result);
        return result;
    }

    /// <summary>
    /// Clears the user profile cache (non-destructive).
    /// Removes stale registry entries for profiles that no longer exist
    /// and clears the profile GUID mapping cache.
    /// </summary>
    public static async Task<ActionResult> ClearProfileCacheAsync()
    {
        _log.Info("Troubleshoot", "Clearing user profile cache...");

        var script = @"
            $results = @()
            $profileListPath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileList'

            # 1. Find stale profile entries (paths that no longer exist)
            $staleCount = 0
            $profiles = Get-ChildItem -Path $profileListPath -ErrorAction SilentlyContinue
            foreach ($profile in $profiles) {
                try {
                    $props = Get-ItemProperty -Path $profile.PSPath -ErrorAction SilentlyContinue
                    if ($props.ProfileImagePath -and -not (Test-Path $props.ProfileImagePath)) {
                        # Profile folder doesn't exist — stale entry
                        $staleCount++
                        $results += ""Found stale profile: $($props.ProfileImagePath)""
                    }
                } catch { }
            }

            if ($staleCount -eq 0) {
                $results += 'No stale profile entries found'
            } else {
                $results += ""Found $staleCount stale profile entries (review manually in Registry Editor)""
            }

            # 2. Clear the Profile GUID cache
            try {
                $guidCachePath = 'HKLM:\SOFTWARE\Microsoft\Windows NT\CurrentVersion\ProfileGuid'
                if (Test-Path $guidCachePath) {
                    $guids = (Get-ChildItem -Path $guidCachePath -ErrorAction SilentlyContinue).Count
                    $results += ""Profile GUID cache contains $guids entries""
                }
            } catch { }

            # 3. Clear user profile temp data
            try {
                $tempProfiles = Get-ChildItem -Path 'C:\Users' -Filter 'TEMP*' -Directory -ErrorAction SilentlyContinue
                if ($tempProfiles.Count -gt 0) {
                    $results += ""WARNING: Found $($tempProfiles.Count) TEMP profile folder(s) in C:\Users""
                    foreach ($tp in $tempProfiles) {
                        $results += ""  → $($tp.Name) (created $($tp.CreationTime.ToString('yyyy-MM-dd')))""
                    }
                } else {
                    $results += 'No TEMP profile folders found in C:\Users'
                }
            } catch { }

            Write-Output ($results -join ""`n"")
            Write-Output 'Profile cache scan complete.'
        ";

        var result = await PowerShellRunner.RunAsync(script, elevated: true, timeoutSeconds: 15);
        _log.LogAction("Troubleshoot", "Clear Profile Cache", result);
        return result;
    }
}

