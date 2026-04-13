using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Troubleshoot view — large clickable action cards for one-click fixes.
/// Each action runs async, shows progress, and reports success/failure.
/// </summary>
public partial class TroubleshootView : UserControl
{
    public TroubleshootView()
    {
        InitializeComponent();
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
            "This will close Outlook and delete its local cache (OST).\nYour mailbox will re-download from Exchange on next launch.\n\nContinue?",
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

    // ─── Shared Action Runner ────────────────────────────────────

    /// <summary>
    /// Runs an async action with visual feedback on the card.
    /// Shows spinning state, then success/failure result.
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
        }
        finally
        {
            card.IsEnabled = true;
            card.Opacity = 1.0;
        }
    }
}
