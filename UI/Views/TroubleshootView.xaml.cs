using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Troubleshoot view — large clickable action cards for one-click fixes.
/// Each action runs async, shows progress, and reports success/failure.
/// Admin cards are visually distinguished for non-elevated users.
/// </summary>
public partial class TroubleshootView : UserControl
{
    private static readonly LoggingService _log = LoggingService.Instance;
    private readonly bool _isAdmin;

    public TroubleshootView()
    {
        InitializeComponent();
        _isAdmin = ElevationHelper.IsRunningAsAdmin();
        Loaded += (s, e) => ApplyAdminVisibility();
    }

    /// <summary>
    /// Dims admin-only cards when the app is not running elevated.
    /// Cards remain clickable (UAC will prompt), but the visual
    /// distinction makes it clear which actions require elevation.
    /// </summary>
    private void ApplyAdminVisibility()
    {
        var adminCards = new[]
        {
            CardStartMenu, CardPrint, CardQueue, CardNetReset,
            CardWinUpdate, CardSfc, CardDism, CardIntune,
            CardSccm, CardFileAssoc, CardTempProfile, CardProfileCache
        };

        if (!_isAdmin)
        {
            foreach (var card in adminCards)
            {
                if (card != null)
                    card.Opacity = 0.7;
            }
        }
    }

    // ─── Application Resets ──────────────────────────────────────

    private async void ResetTeams_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardTeams, StatusTeams, "Resetting Teams...",
            () => TroubleshootService.ResetTeamsCacheAsync());
    }

    private async void ResetOffice_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardOffice, StatusOffice, "Resetting Office...",
            () => TroubleshootService.ResetOfficeCacheAsync());
    }

    private async void ResetCitrix_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardCitrix, StatusCitrix, "Resetting Citrix...",
            () => TroubleshootService.ResetCitrixAsync());
    }

    private async void ClearBrowserCaches_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardBrowser, StatusBrowser, "Clearing browser caches...",
            () => TroubleshootService.ClearBrowserCachesAsync());
    }

    // ─── System Actions ──────────────────────────────────────────

    private async void GpUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardGpu, StatusGpu, "Running GPUpdate...",
            () => TroubleshootService.RunGpUpdateAsync());
    }

    private async void RestartExplorer_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardExplorer, StatusExplorer, "Restarting Explorer...",
            () => TroubleshootService.RestartExplorerAsync());
    }

    private async void ClearTemp_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardTemp, StatusTemp, "Clearing temp files...",
            () => TroubleshootService.ClearTempFilesAsync());
    }

    private async void RestartSpooler_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardPrint, StatusPrint, "Restarting Print Spooler...",
            () => TroubleshootService.RestartPrintSpoolerAsync());
    }

    // ─── Advanced Diagnostics ────────────────────────────────────

    private async void SfcScan_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "System File Checker can take 10-30 minutes.\nThis requires administrator privileges.\n\nContinue?",
            "SFC Scan",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardSfc, StatusSfc, "Running SFC (this takes a while)...",
                () => TroubleshootService.RunSfcScanAsync());
        }
    }

    private async void DismHealth_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "DISM RestoreHealth can take 15-45 minutes.\nThis requires administrator privileges and internet access.\n\nContinue?",
            "DISM Health Check",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardDism, StatusDism, "Running DISM (this takes a while)...",
                () => TroubleshootService.RunDismHealthAsync());
        }
    }

    // ─── New One-Click Fixes ─────────────────────────────────────

    private async void RepairOutlook_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will close Outlook and create a new mail profile.\nYour existing OST data will NOT be deleted.\nOutlook will prompt you to set up your email on next launch.\n\nContinue?",
            "Repair Outlook",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardOutlook, StatusOutlook, "Repairing Outlook...",
                () => TroubleshootService.RepairOutlookAsync());
        }
    }

    private async void RepairOneDrive_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will reset OneDrive and re-sync all files.\nNo files will be deleted.\n\nContinue?",
            "Repair OneDrive",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardOneDrive, StatusOneDrive, "Repairing OneDrive...",
                () => TroubleshootService.RepairOneDriveSyncAsync());
        }
    }

    private async void ClearCredentials_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will clear saved Microsoft/Office passwords.\nYou will need to sign in again to Office, Teams, and OneDrive.\n\nContinue?",
            "Clear Credentials",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardCreds, StatusCreds, "Clearing credentials...",
                () => TroubleshootService.ClearCredentialManagerAsync());
        }
    }

    private async void RebuildIcons_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will temporarily kill Explorer to rebuild icon cache.\nYour taskbar will briefly disappear.\n\nContinue?",
            "Rebuild Icon Cache",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardIcons, StatusIcons, "Rebuilding icon cache...",
                () => TroubleshootService.RebuildIconCacheAsync());
        }
    }

    private async void FixStartMenu_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardStartMenu, StatusStartMenu, "Re-registering Start Menu...",
            () => TroubleshootService.ReRegisterStartMenuAsync());
    }

    private async void ClearPrintQueue_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardQueue, StatusQueue, "Clearing print queue...",
            () => TroubleshootService.ClearPrintQueueAsync());
    }

    private async void FullNetworkReset_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will reset Winsock, TCP/IP, and flush DNS.\nA reboot is recommended after.\n\nContinue?",
            "Full Network Reset",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardNetReset, StatusNetReset, "Resetting network stack...",
                () => TroubleshootService.FullNetworkResetAsync());
        }
    }

    private async void ResetWindowsUpdate_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will stop Windows Update services, clear the download cache,\nre-register DLLs, and restart services.\n\nA reboot is recommended after.\n\nContinue?",
            "Reset Windows Update",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardWinUpdate, StatusWinUpdate, "Resetting Windows Update...",
                () => TroubleshootService.ResetWindowsUpdateAsync());
        }
    }

    /// <summary>
    /// Runs an async action with visual feedback on the card.
    /// Shows spinning state, then success/failure result.
    /// Security: logs every action with user identity and outcome.
    /// </summary>
    private async Task RunAction(Border card, TextBlock statusLabel,
        string inProgressText, Func<Task<ActionResult>> action)
    {
        // Disable card during execution
        card.IsEnabled = false;
        card.Opacity = 0.7;

        // Show "in progress" text
        statusLabel.Text = inProgressText;
        statusLabel.Foreground = (Brush)FindResource("AccentBrush");

        // Security audit: log action initiation
        _log.Security("Action", $"User initiated: {inProgressText.TrimEnd('.')}");

        try
        {
            var result = await action();

            // Show result
            statusLabel.Text = result.Success
                ? $"✓ {result.Message}"
                : $"✗ {result.Message}";

            statusLabel.Foreground = result.Success
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("ErrorBrush");

            // Temporarily highlight the card border
            card.BorderBrush = result.Success
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("ErrorBrush");

            // Security audit: log action outcome
            if (result.Success)
                _log.Security("Action", $"Completed successfully: {inProgressText.TrimEnd('.')}");
            else
                _log.Security("Action", $"FAILED: {inProgressText.TrimEnd('.')}",
                    result.Message);

            // Reset border after delay
            _ = Task.Delay(3000).ContinueWith(_ =>
            {
                Dispatcher.BeginInvoke(() =>
                {
                    card.BorderBrush = (Brush)FindResource("BorderSubtleBrush");
                });
            });
        }
        catch (Exception ex)
        {
            statusLabel.Text = $"✗ Error: {ex.Message}";
            statusLabel.Foreground = (Brush)FindResource("ErrorBrush");
            _log.Security("Action", $"EXCEPTION: {inProgressText.TrimEnd('.')}", ex.Message);
        }
        finally
        {
            card.IsEnabled = true;
            card.Opacity = _isAdmin ? 1.0 : 0.7;
        }
    }
    private async void IntuneSync_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will force an Intune policy and app sync.\nThis triggers the MDM scheduled tasks and restarts the Intune Management Extension.\n\nRequires administrator privileges. Continue?",
            "Intune Sync",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardIntune, StatusIntune, "Syncing with Intune...",
                () => TroubleshootService.SyncIntuneAsync());
        }
    }

    // ─── Software Center / SCCM ─────────────────────────────────

    private async void SccmRepair_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will restart the SCCM client (CCMExec) and trigger:\n• Machine Policy retrieval\n• App Deployment evaluation\n• Software Update scan\n\nRequires administrator privileges. Continue?",
            "SCCM Client Repair",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardSccm, StatusSccm, "Repairing SCCM client...",
                () => TroubleshootService.RepairSccmClientAsync());
        }
    }

    // ─── File Association / Default Apps ─────────────────────────

    private async void ResetDefaultApps_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will reset all default app associations.\nWindows will prompt you to choose apps for common file types.\n\nExplorer will restart. Continue?",
            "Reset Default Apps",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardDefaultApps, StatusDefaultApps, "Resetting default apps...",
                () => TroubleshootService.ResetDefaultAppsAsync());
        }
    }

    private async void RepairFileAssoc_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will repair broken file associations for common types:\n.pdf, .docx, .xlsx, .pptx, .txt, .csv\n\nExplorer will restart. Continue?",
            "Fix File Associations",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardFileAssoc, StatusFileAssoc, "Repairing file associations...",
                () => TroubleshootService.RepairFileAssociationsAsync());
        }
    }

    // ─── User Profile & Login ───────────────────────────────────

    private async void FixTempProfile_Click(object sender, MouseButtonEventArgs e)
    {
        var answer = MessageBox.Show(
            "This will scan the registry for .bak profile entries\nand attempt to repair the Temporary Profile issue.\n\nA sign-out/sign-in will be required after repair.\n\nRequires administrator privileges. Continue?",
            "Fix Temporary Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            await RunAction(CardTempProfile, StatusTempProfile, "Scanning for temp profile issues...",
                () => TroubleshootService.FixTempProfileAsync());
        }
    }

    private async void ClearProfileCache_Click(object sender, MouseButtonEventArgs e)
    {
        await RunAction(CardProfileCache, StatusProfileCache, "Scanning profile cache...",
            () => TroubleshootService.ClearProfileCacheAsync());
    }
}
