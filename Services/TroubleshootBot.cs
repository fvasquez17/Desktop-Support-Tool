using System.Diagnostics;
using System.IO;
using System.Net.NetworkInformation;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Advanced offline IT Support Agent — combines keyword analysis with LIVE system
/// diagnostics, context-aware responses, follow-up questions, and auto-scan.
/// No external APIs required; works entirely offline.
/// </summary>
public static class TroubleshootBot
{
    // ─── Public Types ────────────────────────────────────────────

    public record Diagnosis(
        string Category,
        string Title,
        string Explanation,
        List<SuggestedAction> Actions);

    public record SuggestedAction(
        string Name,
        string Description,
        Func<Task<ActionResult>> Execute,
        bool RequiresConfirmation = false);

    /// <summary>Result of a full bot analysis with context.</summary>
    public record BotResponse(
        string Message,
        List<Diagnosis> Diagnoses,
        List<string> QuickReplies,
        SystemSnapshot? Snapshot = null);

    /// <summary>Live system metrics captured at analysis time.</summary>
    public record SystemSnapshot(
        double CpuPercent,
        double RamPercent,
        double DiskPercent,
        long DiskFreeGB,
        bool InternetConnected,
        bool DnsWorking,
        List<string> StoppedCriticalServices,
        int UptimeHours);

    // ─── Session State ───────────────────────────────────────────

    private static readonly List<string> _triedFixes = new();
    private static SystemSnapshot? _lastSnapshot;

    public static void ClearSession()
    {
        _triedFixes.Clear();
        _lastSnapshot = null;
    }

    public static void RecordFix(string fixName) => _triedFixes.Add(fixName);
    public static bool HasTried(string fixName) => _triedFixes.Contains(fixName);

    // ─── Knowledge Base ──────────────────────────────────────────

    private static readonly List<(string[] Keywords, string[] Phrases, string Category, Func<SystemSnapshot?, Diagnosis> Build)> _rules = new()
    {
        // ── Outlook / Email ──
        (new[] { "outlook", "email", "mailbox", "ost", "exchange", "mail" },
         new[] { "outlook won't open", "email not loading", "outlook slow", "mailbox not syncing",
                 "outlook crash", "outlook keeps asking for password", "cached mode" },
         "Email",
         (snap) => new Diagnosis("Email", "Outlook / Email Issues",
             BuildContextualExplanation("Outlook problems are usually caused by a corrupt local cache (OST file), " +
             "stale credentials, or Office cache corruption.", snap,
             snap?.RamPercent > 85 ? "⚠️ Your RAM is at {0:F0}% — Outlook may be struggling for memory." : null,
             snap?.DiskPercent > 90 ? "⚠️ Disk is {1:F0}% full — Outlook needs space for its cache." : null),
             new List<SuggestedAction>
             {
                 new("Repair Outlook Cache", "Close Outlook and delete the OST file so it re-syncs from Exchange",
                     TroubleshootService.RepairOutlookAsync, RequiresConfirmation: true),
                 new("Clear Saved Credentials", "Remove cached Office passwords from Credential Manager",
                     TroubleshootService.ClearCredentialManagerAsync, RequiresConfirmation: true),
                 new("Reset Office Cache", "Clear all Office cache files and identity tokens",
                     TroubleshootService.ResetOfficeCacheAsync),
             })),

        // ── Teams ──
        (new[] { "teams", "meeting", "video call", "teams chat" },
         new[] { "teams not working", "teams won't load", "teams crash", "can't join meeting",
                 "teams black screen", "teams slow", "teams no audio" },
         "Communication",
         (snap) => new Diagnosis("Communication", "Microsoft Teams Issues",
             BuildContextualExplanation("Teams issues are typically resolved by clearing the local cache. " +
             "This won't delete your chats or files — they're stored in the cloud.", snap,
             snap?.InternetConnected == false ? "🔴 **No internet detected!** Teams requires internet connectivity." : null,
             snap?.RamPercent > 80 ? "⚠️ RAM is at {0:F0}% — Teams uses significant memory." : null),
             new List<SuggestedAction>
             {
                 new("Reset Teams Cache", "Kill Teams and clear all local cache data",
                     TroubleshootService.ResetTeamsCacheAsync),
                 new("Clear Saved Credentials", "Fix sign-in loops by clearing cached tokens",
                     TroubleshootService.ClearCredentialManagerAsync, RequiresConfirmation: true),
             })),

        // ── Printing ──
        (new[] { "printer", "print", "printing", "spooler", "print queue" },
         new[] { "can't print", "printer not working", "print job stuck", "printer offline",
                 "print queue won't clear", "printer not responding" },
         "Printing",
         (snap) =>
         {
             var spoolerDown = snap?.StoppedCriticalServices?.Contains("Spooler") == true;
             var extra = spoolerDown
                 ? "🔴 **Print Spooler service is STOPPED!** This is why printing isn't working. Click 'Restart Print Spooler' below."
                 : "The Spooler service is running — the issue is likely stuck jobs in the queue.";
             return new Diagnosis("Printing", "Printer / Print Issues", extra,
                 new List<SuggestedAction>
                 {
                     new("Clear Print Queue", "Stop Spooler, delete stuck jobs, restart Spooler",
                         TroubleshootService.ClearPrintQueueAsync),
                     new("Restart Print Spooler", "Force restart the Print Spooler service",
                         TroubleshootService.RestartPrintSpoolerAsync),
                 });
         }),

        // ── Network / Internet ──
        (new[] { "internet", "network", "wifi", "wi-fi", "ethernet", "vpn", "dns", "connectivity" },
         new[] { "no internet", "can't connect", "connected but no internet", "wifi not working",
                 "network slow", "dns error", "can't reach", "page not loading", "vpn won't connect" },
         "Network",
         (snap) =>
         {
             string msg;
             if (snap?.InternetConnected == false && snap?.DnsWorking == false)
                 msg = "🔴 **Confirmed: No internet connectivity and DNS is not resolving.** " +
                       "This is a connectivity issue. Let's try a full network reset.";
             else if (snap?.InternetConnected == true && snap?.DnsWorking == false)
                 msg = "⚠️ **Internet is reachable but DNS is failing.** " +
                       "You can connect to IPs but not domain names. A DNS flush should fix this.";
             else if (snap?.InternetConnected == true)
                 msg = "✅ **Internet connectivity looks OK from my scan.** " +
                       "The issue may be intermittent or site-specific. Let's try a DNS flush first.";
             else
                 msg = "Network issues can range from DNS problems to deeper TCP/IP corruption. " +
                       "I'll start with the safest fix and escalate if needed.";
             return new Diagnosis("Network", "Network / Internet Connectivity", msg,
                 new List<SuggestedAction>
                 {
                     new("Flush DNS Cache", "Clear the local DNS resolver cache",
                         NetworkService.FlushDnsAsync),
                     new("Renew IP Address", "Release and renew your IP via DHCP",
                         NetworkService.RenewIpAsync),
                     new("Full Network Reset", "Reset Winsock + TCP/IP + DNS (reboot recommended)",
                         TroubleshootService.FullNetworkResetAsync, RequiresConfirmation: true),
                 });
         }),

        // ── Performance / Slow ──
        (new[] { "slow", "lag", "performance", "freeze", "hanging", "unresponsive", "memory" },
         new[] { "computer is slow", "pc slow", "everything is slow", "takes forever", "freezing",
                 "not responding", "high cpu", "high memory", "out of memory", "disk full" },
         "Performance",
         (snap) =>
         {
             var issues = new List<string>();
             if (snap != null)
             {
                 if (snap.CpuPercent > 80) issues.Add($"🔴 **CPU is at {snap.CpuPercent:F0}%** — something is consuming heavy processing power");
                 if (snap.RamPercent > 85) issues.Add($"🔴 **RAM is at {snap.RamPercent:F0}%** — memory is nearly full");
                 if (snap.DiskPercent > 90) issues.Add($"🔴 **Disk is {snap.DiskPercent:F0}% full** (only {snap.DiskFreeGB} GB free) — low disk space causes major slowdowns");
                 if (snap.UptimeHours > 168) issues.Add($"⚠️ **Uptime: {snap.UptimeHours / 24} days** — a restart is recommended");
                 if (issues.Count == 0) issues.Add("✅ CPU, RAM, and disk look normal. The issue may be application-specific.");
             }

             var msg = "**Live Diagnostics:**\n" + string.Join("\n", issues.Select(i => $"• {i}")) +
                 "\n\nHere are the recommended fixes based on what I found:";
             return new Diagnosis("Performance", "Performance / Slow PC", msg,
                 new List<SuggestedAction>
                 {
                     new("Clear Temp Files", "Delete temporary files and free disk space",
                         TroubleshootService.ClearTempFilesAsync),
                     new("Clear Browser Caches", "Free space by clearing Chrome, Edge, Firefox caches",
                         TroubleshootService.ClearBrowserCachesAsync),
                     new("Restart Explorer", "Restart Windows Explorer to free stuck resources",
                         TroubleshootService.RestartExplorerAsync),
                 });
         }),

        // ── Windows Update ──
        (new[] { "update", "windows update", "patch", "cumulative" },
         new[] { "update failed", "update stuck", "windows update error", "can't update",
                 "update loop", "update won't install", "pending restart" },
         "System",
         (snap) =>
         {
             var wuStopped = snap?.StoppedCriticalServices?.Contains("wuauserv") == true;
             var msg = wuStopped
                 ? "🔴 **Windows Update service (wuauserv) is STOPPED!** This is likely why updates are failing."
                 : "Failed or stuck Windows Updates are usually caused by corrupt download cache or stopped services.";
             msg += " This fix resets all Update components.";
             return new Diagnosis("System", "Windows Update Issues", msg,
                 new List<SuggestedAction>
                 {
                     new("Reset Windows Update", "Stop services, clear caches, re-register DLLs, restart",
                         TroubleshootService.ResetWindowsUpdateAsync, RequiresConfirmation: true),
                 });
         }),

        // ── OneDrive / Sync ──
        (new[] { "onedrive", "sync", "sharepoint", "files" },
         new[] { "onedrive not syncing", "files not syncing", "sync stuck", "onedrive error",
                 "conflicted copy", "sync pending" },
         "Cloud",
         (snap) => new Diagnosis("Cloud Storage", "OneDrive Sync Issues",
             BuildContextualExplanation("OneDrive sync problems are best fixed by resetting the client. " +
             "Your files won't be deleted — they'll re-sync from the cloud.", snap,
             snap?.InternetConnected == false ? "🔴 **No internet detected!** OneDrive requires connectivity to sync." : null,
             snap?.DiskPercent > 90 ? "⚠️ Disk is {1:F0}% full — OneDrive needs free space to sync files." : null),
             new List<SuggestedAction>
             {
                 new("Repair OneDrive Sync", "Reset OneDrive client and re-initialize sync",
                     TroubleshootService.RepairOneDriveSyncAsync, RequiresConfirmation: true),
             })),

        // ── Sign-in / Authentication ──
        (new[] { "password", "sign in", "login", "mfa", "sso", "credential", "authentication" },
         new[] { "keeps asking for password", "can't sign in", "sign in loop", "mfa not working",
                 "credential prompt", "enter password", "authentication failed" },
         "Auth",
         (snap) => new Diagnosis("Authentication", "Sign-in / Credential Issues",
             BuildContextualExplanation("Persistent password prompts and SSO failures are caused by stale tokens " +
             "in Windows Credential Manager. Clearing them forces a fresh sign-in.", snap,
             snap?.InternetConnected == false ? "🔴 **No internet!** Authentication requires connectivity to your identity provider." : null),
             new List<SuggestedAction>
             {
                 new("Clear Saved Credentials", "Remove cached Microsoft/Office credentials",
                     TroubleshootService.ClearCredentialManagerAsync, RequiresConfirmation: true),
                 new("Reset Office Cache", "Clear Office identity and token cache",
                     TroubleshootService.ResetOfficeCacheAsync),
             })),

        // ── Start Menu / Search ──
        (new[] { "start menu", "start button", "search", "cortana", "taskbar" },
         new[] { "start menu not opening", "search not working", "start menu broken",
                 "can't search", "start button not responding", "taskbar frozen" },
         "Shell",
         (snap) => new Diagnosis("Shell", "Start Menu / Search Issues",
             "Start Menu and Search are UWP shell apps that can break after updates. Re-registering them usually fixes it.",
             new List<SuggestedAction>
             {
                 new("Fix Start Menu", "Re-register Start Menu and Search shell components",
                     TroubleshootService.ReRegisterStartMenuAsync),
                 new("Restart Explorer", "Restart explorer.exe to reset the shell",
                     TroubleshootService.RestartExplorerAsync),
             })),

        // ── Icons ──
        (new[] { "icon", "icons", "blank", "thumbnail" },
         new[] { "blank icons", "wrong icons", "icons missing", "icons not showing", "white icons" },
         "Display",
         (snap) => new Diagnosis("Display", "Icon / Thumbnail Issues",
             "Blank or wrong icons are caused by a corrupted icon cache. Rebuilding it will temporarily restart Explorer.",
             new List<SuggestedAction>
             {
                 new("Rebuild Icon Cache", "Delete icon cache files and restart Explorer",
                     TroubleshootService.RebuildIconCacheAsync, RequiresConfirmation: true),
             })),

        // ── Citrix ──
        (new[] { "citrix", "vdi", "virtual desktop", "receiver", "workspace app" },
         new[] { "citrix not working", "citrix error", "can't launch app", "citrix black screen" },
         "VDI",
         (snap) => new Diagnosis("VDI", "Citrix / VDI Issues",
             BuildContextualExplanation("Citrix issues are commonly resolved by clearing the local Workspace cache.", snap,
             snap?.InternetConnected == false ? "🔴 **No internet!** Citrix requires network connectivity." : null),
             new List<SuggestedAction>
             {
                 new("Reset Citrix", "Kill Citrix processes and clear Workspace cache",
                     TroubleshootService.ResetCitrixAsync),
             })),

        // ── Browser ──
        (new[] { "browser", "chrome", "edge", "firefox", "webpage" },
         new[] { "browser slow", "page not loading", "chrome not working", "edge crash",
                 "browser crash", "can't open website" },
         "Browser",
         (snap) => new Diagnosis("Browser", "Browser Issues",
             BuildContextualExplanation("Browser problems are usually caused by cache corruption or excessive data buildup.", snap,
             snap?.DnsWorking == false ? "🔴 **DNS is not resolving!** This is likely why pages won't load." : null,
             snap?.InternetConnected == false ? "🔴 **No internet detected!** Check your connection first." : null),
             new List<SuggestedAction>
             {
                 new("Clear Browser Caches", "Clear Chrome, Edge, and Firefox caches",
                     TroubleshootService.ClearBrowserCachesAsync),
                 new("Flush DNS Cache", "Clear DNS cache in case of name resolution issues",
                     NetworkService.FlushDnsAsync),
             })),

        // ── Group Policy ──
        (new[] { "group policy", "gpo", "gpupdate", "policy" },
         new[] { "policy not applying", "gpo not working", "need to update policy" },
         "System",
         (snap) => new Diagnosis("System", "Group Policy Issues",
             "Group Policy can be refreshed to pull the latest settings from your domain controller.",
             new List<SuggestedAction>
             {
                 new("Run GPUpdate", "Force refresh Group Policy with gpupdate /force",
                     TroubleshootService.RunGpUpdateAsync),
             })),

        // ── System File Corruption ──
        (new[] { "corrupt", "blue screen", "bsod", "crash", "system error" },
         new[] { "blue screen", "system crash", "windows error", "corrupted files", "random crashes" },
         "System",
         (snap) =>
         {
             var msg = "System crashes and corruption can be diagnosed and repaired using built-in Windows tools. " +
                 "⚠️ These scans take 10-45 minutes.";
             if (snap?.UptimeHours > 48)
                 msg += $"\n\n💡 Your system has been running for **{snap.UptimeHours / 24} days** without a restart. " +
                        "Consider rebooting first — many crashes resolve after a clean restart.";
             return new Diagnosis("System", "System File Corruption", msg,
                 new List<SuggestedAction>
                 {
                     new("SFC Scan", "Scan and repair Windows system files (10-30 min)",
                         TroubleshootService.RunSfcScanAsync, RequiresConfirmation: true),
                     new("DISM Repair", "Repair Windows component store (15-45 min)",
                         TroubleshootService.RunDismHealthAsync, RequiresConfirmation: true),
                 });
         }),
    };

    // ═══════════════════════════════════════════════════════════
    //  LIVE SYSTEM DIAGNOSTICS
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Captures a live snapshot of system health — CPU, RAM, disk, 
    /// network connectivity, DNS, critical services, and uptime.
    /// </summary>
    public static async Task<SystemSnapshot> CaptureSnapshotAsync()
    {
        return await Task.Run(() =>
        {
            double cpu = 0, ram = 0, disk = 0;
            long diskFreeGB = 0;
            bool internet = false, dns = false;
            var stoppedServices = new List<string>();
            int uptimeHours = 0;

            try
            {
                // CPU (quick sample)
                using (var cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total"))
                {
                    cpuCounter.NextValue(); // First call always returns 0
                    Thread.Sleep(500);
                    cpu = cpuCounter.NextValue();
                }
            }
            catch { cpu = -1; }

            try
            {
                // RAM
                var gcInfo = GC.GetGCMemoryInfo();
                var totalMem = gcInfo.TotalAvailableMemoryBytes;
                var availMem = new PerformanceCounter("Memory", "Available Bytes").NextValue();
                ram = totalMem > 0 ? (1 - availMem / totalMem) * 100 : 0;
            }
            catch { ram = -1; }

            try
            {
                // Disk (system drive)
                var systemDrive = Path.GetPathRoot(Environment.SystemDirectory) ?? "C:\\";
                var driveInfo = new DriveInfo(systemDrive);
                if (driveInfo.IsReady)
                {
                    disk = (1.0 - (double)driveInfo.AvailableFreeSpace / driveInfo.TotalSize) * 100;
                    diskFreeGB = driveInfo.AvailableFreeSpace / (1024L * 1024 * 1024);
                }
            }
            catch { }

            try
            {
                // Internet connectivity (ping 8.8.8.8)
                using var ping = new Ping();
                var reply = ping.Send("8.8.8.8", 2000);
                internet = reply.Status == IPStatus.Success;
            }
            catch { internet = false; }

            try
            {
                // DNS resolution
                var host = System.Net.Dns.GetHostEntry("www.microsoft.com");
                dns = host.AddressList.Length > 0;
            }
            catch { dns = false; }

            try
            {
                // Critical services check via WMI
                var criticalServices = new[] { "Spooler", "wuauserv", "BITS", "Dnscache",
                    "LanmanWorkstation", "Winmgmt", "W32Time", "EventLog" };
                foreach (var svcName in criticalServices)
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = "sc",
                            Arguments = $"query {svcName}",
                            RedirectStandardOutput = true,
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using var proc = Process.Start(psi);
                        if (proc != null)
                        {
                            var output = proc.StandardOutput.ReadToEnd();
                            proc.WaitForExit();
                            if (!output.Contains("RUNNING", StringComparison.OrdinalIgnoreCase))
                                stoppedServices.Add(svcName);
                        }
                    }
                    catch { }
                }
            }
            catch { }

            try
            {
                // Uptime
                uptimeHours = (int)(TimeSpan.FromMilliseconds(Environment.TickCount64).TotalHours);
            }
            catch { }

            var snapshot = new SystemSnapshot(cpu, ram, disk, diskFreeGB, internet, dns, stoppedServices, uptimeHours);
            _lastSnapshot = snapshot;
            return snapshot;
        });
    }

    // ═══════════════════════════════════════════════════════════
    //  ANALYSIS ENGINE
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Advanced analysis: captures live system state, runs keyword matching 
    /// with context-aware responses and follow-up suggestions.
    /// </summary>
    public static async Task<BotResponse> AnalyzeAsync(string userInput)
    {
        if (string.IsNullOrWhiteSpace(userInput))
            return new BotResponse("Please describe your issue.", new(), new());

        // Capture live system snapshot for context
        var snapshot = await CaptureSnapshotAsync();

        var input = userInput.ToLowerInvariant();
        var results = new List<(Diagnosis diag, double score)>();

        foreach (var rule in _rules)
        {
            double score = 0;

            foreach (var phrase in rule.Phrases)
                if (input.Contains(phrase, StringComparison.OrdinalIgnoreCase))
                    score += 3.0;

            foreach (var keyword in rule.Keywords)
                if (input.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                    score += 1.0;

            if (score > 0)
            {
                var diagnosis = rule.Build(snapshot);
                // Filter out fixes already tried this session
                var filtered = new Diagnosis(diagnosis.Category, diagnosis.Title,
                    diagnosis.Explanation,
                    diagnosis.Actions.Where(a => !_triedFixes.Contains(a.Name)).ToList());
                results.Add((filtered, Math.Min(score / 5.0, 1.0)));
            }
        }

        results = results.OrderByDescending(r => r.score).Take(3).ToList();

        if (results.Count == 0)
            return new BotResponse(GetNoMatchResponse(), new(), GetDefaultQuickReplies(), snapshot);

        var diagnoses = results.Select(r => r.diag).ToList();
        var primaryDiag = diagnoses[0];
        var confidence = results[0].score;

        var confidenceLabel = confidence switch
        {
            >= 0.8 => "High confidence",
            >= 0.4 => "Moderate confidence",
            _ => "Possible match"
        };

        var message = $"🔍 **{primaryDiag.Title}** ({confidenceLabel})\n\n{primaryDiag.Explanation}";

        // Add secondary matches
        if (diagnoses.Count > 1)
        {
            message += "\n\n📋 I also detected possible issues with:\n" +
                string.Join("\n", diagnoses.Skip(1).Select(d =>
                    $"• **{d.Title}** — {d.Actions.Count} fix(es) available"));
        }

        // Build contextual quick replies
        var quickReplies = BuildQuickReplies(primaryDiag, snapshot);

        return new BotResponse(message, diagnoses, quickReplies, snapshot);
    }

    // ═══════════════════════════════════════════════════════════
    //  AUTO-SCAN
    // ═══════════════════════════════════════════════════════════

    /// <summary>
    /// Proactively scans the system and reports any issues found,
    /// without the user having to describe a problem.
    /// </summary>
    public static async Task<BotResponse> AutoScanAsync()
    {
        var snapshot = await CaptureSnapshotAsync();
        var issues = new List<string>();
        var diagnoses = new List<Diagnosis>();
        var quickReplies = new List<string>();

        // Analyze each metric
        if (snapshot.CpuPercent > 80)
            issues.Add($"🔴 **CPU** is at **{snapshot.CpuPercent:F0}%**");
        else if (snapshot.CpuPercent > 50)
            issues.Add($"⚠️ **CPU** is at **{snapshot.CpuPercent:F0}%** (moderate)");
        else if (snapshot.CpuPercent >= 0)
            issues.Add($"✅ **CPU** is at **{snapshot.CpuPercent:F0}%**");

        if (snapshot.RamPercent > 85)
            issues.Add($"🔴 **RAM** is at **{snapshot.RamPercent:F0}%** — memory pressure detected");
        else if (snapshot.RamPercent > 60)
            issues.Add($"⚠️ **RAM** is at **{snapshot.RamPercent:F0}%**");
        else if (snapshot.RamPercent >= 0)
            issues.Add($"✅ **RAM** is at **{snapshot.RamPercent:F0}%**");

        if (snapshot.DiskPercent > 90)
        {
            issues.Add($"🔴 **Disk** is **{snapshot.DiskPercent:F0}%** full (only **{snapshot.DiskFreeGB} GB** free!)");
            quickReplies.Add("My computer is slow");
        }
        else if (snapshot.DiskPercent > 75)
            issues.Add($"⚠️ **Disk** is **{snapshot.DiskPercent:F0}%** full ({snapshot.DiskFreeGB} GB free)");
        else
            issues.Add($"✅ **Disk** has **{snapshot.DiskFreeGB} GB** free ({snapshot.DiskPercent:F0}% used)");

        if (!snapshot.InternetConnected)
        {
            issues.Add("🔴 **Internet** — NOT CONNECTED");
            quickReplies.Add("Fix my internet");
        }
        else if (!snapshot.DnsWorking)
        {
            issues.Add("⚠️ **Internet** — Connected but **DNS failing**");
            quickReplies.Add("DNS is not working");
        }
        else
            issues.Add("✅ **Internet** — Connected, DNS working");

        if (snapshot.StoppedCriticalServices.Count > 0)
        {
            issues.Add($"⚠️ **Services** — {snapshot.StoppedCriticalServices.Count} critical service(s) stopped: " +
                string.Join(", ", snapshot.StoppedCriticalServices));
            if (snapshot.StoppedCriticalServices.Contains("Spooler"))
                quickReplies.Add("I can't print");
        }
        else
            issues.Add("✅ **Services** — All critical services running");

        if (snapshot.UptimeHours > 168)
            issues.Add($"⚠️ **Uptime** — System has been running for **{snapshot.UptimeHours / 24} days** without restart");
        else
            issues.Add($"✅ **Uptime** — {snapshot.UptimeHours} hours");

        var hasProblems = issues.Any(i => i.Contains("🔴") || i.Contains("⚠️"));

        var msg = "**🖥️ System Health Scan Complete**\n\n" +
            string.Join("\n", issues) +
            (hasProblems
                ? "\n\n💡 I found some issues. Click a quick reply below or describe your problem."
                : "\n\n✅ **Everything looks healthy!** If you're still having issues, describe your problem and I'll dig deeper.");

        quickReplies.AddRange(new[] { "My computer is slow", "I need help with Outlook", "Run a full fix" });
        quickReplies = quickReplies.Distinct().Take(5).ToList();

        return new BotResponse(msg, diagnoses, quickReplies, snapshot);
    }

    // ═══════════════════════════════════════════════════════════
    //  HELPER METHODS
    // ═══════════════════════════════════════════════════════════

    private static string BuildContextualExplanation(string baseMsg, SystemSnapshot? snap, params string?[] extras)
    {
        var parts = new List<string> { baseMsg };
        if (snap != null)
        {
            foreach (var extra in extras)
            {
                if (extra != null)
                    parts.Add(string.Format(extra, snap.RamPercent, snap.DiskPercent));
            }
        }
        return string.Join("\n\n", parts);
    }

    private static List<string> BuildQuickReplies(Diagnosis diagnosis, SystemSnapshot snapshot)
    {
        var replies = new List<string>();

        switch (diagnosis.Category)
        {
            case "Email":
                replies.Add("Outlook keeps asking for password");
                replies.Add("Email is slow");
                break;
            case "Communication":
                replies.Add("Teams audio not working");
                replies.Add("Can't share screen");
                break;
            case "Printing":
                replies.Add("Printer is offline");
                replies.Add("Print job stuck");
                break;
            case "Network":
                replies.Add("VPN won't connect");
                replies.Add("WiFi keeps disconnecting");
                break;
            case "Performance":
                replies.Add("Computer freezes randomly");
                replies.Add("Apps take forever to open");
                break;
        }

        // Always add scan option
        if (!replies.Contains("Scan my system"))
            replies.Add("Scan my system");

        return replies.Take(4).ToList();
    }

    private static List<string> GetDefaultQuickReplies() => new()
    {
        "My computer is slow",
        "I can't print",
        "Outlook not working",
        "No internet",
        "Scan my system"
    };

    // ═══════════════════════════════════════════════════════════
    //  GREETING & FALLBACK
    // ═══════════════════════════════════════════════════════════

    public static string GetGreeting()
    {
        var hour = DateTime.Now.Hour;
        var timeGreeting = hour switch
        {
            < 12 => "Good morning",
            < 17 => "Good afternoon",
            _ => "Good evening"
        };

        return $"{timeGreeting}, {Environment.UserName}! 👋\n\n" +
               "I'm your **IT Support Agent**. I can diagnose issues, run live system scans, " +
               "and fix problems automatically.\n\n" +
               "**How to use me:**\n" +
               "• Describe your issue in plain English\n" +
               "• Click **Scan System** to check your PC health\n" +
               "• Click any quick reply below to get started";
    }

    public static string GetNoMatchResponse()
    {
        return "I wasn't able to match your issue to a known fix. Here are some things you can try:\n\n" +
               "• **Scan my system** — let me run a live health check\n" +
               "• **Restart your PC** — fixes many transient issues\n" +
               "• Go to the **Troubleshooting** tab for all one-click fixes\n" +
               "• Contact your **IT Help Desk** for further assistance\n\n" +
               "Try describing your issue differently — for example:\n" +
               "\"Outlook keeps crashing\" or \"my printer won't print\"";
    }
}
