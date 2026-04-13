using System.Windows;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Services;
using WinForms = System.Windows.Forms;

namespace DesktopSupportTool.Tray;

/// <summary>
/// Manages the system tray (notification area) icon, context menu, and tooltip.
/// Left-click toggles the main window; right-click shows quick actions.
/// </summary>
public sealed class TrayIconManager : IDisposable
{
    private readonly WinForms.NotifyIcon _notifyIcon;
    private readonly Window _mainWindow;
    private bool _disposed;

    public TrayIconManager(Window mainWindow)
    {
        _mainWindow = mainWindow;

        // Create the tray icon
        _notifyIcon = new WinForms.NotifyIcon
        {
            Icon = IconGenerator.CreateTrayIcon(),
            Text = $"Desktop Support Tool — {Environment.MachineName}",
            Visible = true,
        };

        // Wire events
        _notifyIcon.MouseClick += OnTrayMouseClick;

        // Build context menu
        _notifyIcon.ContextMenuStrip = BuildContextMenu();

        LoggingService.Instance.Info("Tray", "System tray icon initialized");
    }

    /// <summary>
    /// Shows a balloon notification from the tray icon.
    /// </summary>
    public void ShowNotification(string title, string message, WinForms.ToolTipIcon icon = WinForms.ToolTipIcon.Info)
    {
        _notifyIcon.ShowBalloonTip(3000, title, message, icon);
    }

    // ─── Event Handlers ─────────────────────────────────────────

    private void OnTrayMouseClick(object? sender, WinForms.MouseEventArgs e)
    {
        if (e.Button == WinForms.MouseButtons.Left)
        {
            ToggleMainWindow();
        }
    }

    private void ToggleMainWindow()
    {
        if (_mainWindow.IsVisible)
        {
            _mainWindow.Hide();
        }
        else
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        }
    }

    // ─── Context Menu ────────────────────────────────────────────

    private WinForms.ContextMenuStrip BuildContextMenu()
    {
        var menu = new WinForms.ContextMenuStrip();
        menu.BackColor = System.Drawing.Color.FromArgb(26, 35, 50);
        menu.ForeColor = System.Drawing.Color.FromArgb(241, 245, 249);

        // Header
        var header = new WinForms.ToolStripLabel("Desktop Support Tool")
        {
            ForeColor = System.Drawing.Color.FromArgb(59, 130, 246),
            Font = new System.Drawing.Font("Segoe UI", 9, System.Drawing.FontStyle.Bold)
        };
        menu.Items.Add(header);
        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Open Dashboard
        AddMenuItem(menu, "📊  Open Dashboard", (s, e) => ToggleMainWindow());
        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Quick Actions
        AddMenuItem(menu, "🔄  Run GPUpdate", async (s, e) =>
        {
            ShowNotification("GPUpdate", "Running gpupdate /force...");
            var result = await TroubleshootService.RunGpUpdateAsync();
            ShowNotification("GPUpdate", result.Message,
                result.Success ? WinForms.ToolTipIcon.Info : WinForms.ToolTipIcon.Error);
        });

        AddMenuItem(menu, "🔁  Restart Explorer", async (s, e) =>
        {
            var result = await TroubleshootService.RestartExplorerAsync();
            ShowNotification("Explorer", result.Message,
                result.Success ? WinForms.ToolTipIcon.Info : WinForms.ToolTipIcon.Error);
        });

        AddMenuItem(menu, "🌐  Flush DNS", async (s, e) =>
        {
            var result = await NetworkService.FlushDnsAsync();
            ShowNotification("DNS", result.Message,
                result.Success ? WinForms.ToolTipIcon.Info : WinForms.ToolTipIcon.Error);
        });

        AddMenuItem(menu, "🌐  Network Info", (s, e) =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            // Navigate to network view
            if (_mainWindow is UI.MainWindow mw)
                mw.NavigateTo("network");
        });

        menu.Items.Add(new WinForms.ToolStripSeparator());

        // Exit
        var exitItem = AddMenuItem(menu, "❌  Exit", (s, e) =>
        {
            _mainWindow.Close();
            Application.Current.Shutdown();
        });
        exitItem.ForeColor = System.Drawing.Color.FromArgb(239, 68, 68);

        return menu;
    }

    private static WinForms.ToolStripMenuItem AddMenuItem(WinForms.ContextMenuStrip menu, string text, EventHandler handler)
    {
        var item = new WinForms.ToolStripMenuItem(text);
        item.Click += handler;
        item.Font = new System.Drawing.Font("Segoe UI", 9);
        menu.Items.Add(item);
        return item;
    }

    // ─── Cleanup ─────────────────────────────────────────────────

    public void Dispose()
    {
        if (!_disposed)
        {
            _notifyIcon.Visible = false;
            _notifyIcon.Dispose();
            _disposed = true;
        }
    }
}
