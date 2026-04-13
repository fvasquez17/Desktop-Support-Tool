using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Services;
using DesktopSupportTool.UI.Views;

namespace DesktopSupportTool.UI;

/// <summary>
/// Main application window. Hosts sidebar navigation and content area.
/// Minimizes to system tray on close (does not exit the app).
/// </summary>
public partial class MainWindow : Window
{
    // Pre-create all views for instant switching
    private readonly DashboardView _dashboardView = new();
    private readonly SystemInfoView _systemInfoView = new();
    private readonly NetworkView _networkView = new();
    private readonly PeripheralsView _peripheralsView = new();
    private readonly DriversView _driversView = new();
    private readonly ServicesView _servicesView = new();
    private readonly TroubleshootView _troubleshootView = new();
    private readonly LogViewerView _logViewerView = new();
    private readonly AgentBotView _agentBotView = new();

    public MainWindow()
    {
        InitializeComponent();

        // Fix maximize: stay within work area, don't overlap taskbar
        SourceInitialized += (s, e) =>
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            var source = System.Windows.Interop.HwndSource.FromHwnd(handle);
            source?.AddHook(WndProc);
        };

        // Set window icon
        try
        {
            var icon = IconGenerator.CreateWindowIcon();
            Icon = Imaging.CreateBitmapSourceFromHIcon(
                icon.Handle, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        }
        catch { }

        // Set hostname in title bar
        HostnameLabel.Text = $"— {Environment.MachineName}";

        // Load default view
        ContentArea.Content = _dashboardView;

        // Subscribe to health status updates
        HealthCheckService.Instance.StatusChanged += OnHealthStatusChanged;

        // Load initial status
        UpdateHealthIndicator(HealthCheckService.Instance.CurrentStatus);
    }

    /// <summary>
    /// Programmatic navigation (called from TrayIconManager context menu).
    /// </summary>
    public void NavigateTo(string viewName)
    {
        switch (viewName.ToLowerInvariant())
        {
            case "agent":
                NavAgent.IsChecked = true;
                break;
            case "dashboard":
                NavDashboard.IsChecked = true;
                break;
            case "system":
                NavSystem.IsChecked = true;
                break;
            case "network":
                NavNetwork.IsChecked = true;
                break;
            case "peripherals":
                NavPeripherals.IsChecked = true;
                break;
            case "drivers":
                NavDrivers.IsChecked = true;
                break;
            case "services":
                NavServices.IsChecked = true;
                break;
            case "troubleshoot":
                NavTroubleshoot.IsChecked = true;
                break;
            case "log":
                NavLog.IsChecked = true;
                break;
        }
    }

    // ─── Navigation ──────────────────────────────────────────────

    private void Nav_Checked(object sender, RoutedEventArgs e)
    {
        if (ContentArea == null) return;

        if (sender is RadioButton rb)
        {
            ContentArea.Content = rb.Name switch
            {
                "NavAgent" => _agentBotView,
                "NavDashboard" => _dashboardView,
                "NavSystem" => _systemInfoView,
                "NavNetwork" => _networkView,
                "NavPeripherals" => _peripheralsView,
                "NavDrivers" => _driversView,
                "NavServices" => _servicesView,
                "NavTroubleshoot" => _troubleshootView,
                "NavLog" => _logViewerView,
                _ => _dashboardView
            };

            // Trigger data refresh on view activation
            if (ContentArea.Content is AgentBotView ab) ab.Initialize();
            else if (ContentArea.Content is DashboardView dv) _ = dv.RefreshAsync();
            else if (ContentArea.Content is SystemInfoView sv) _ = sv.RefreshAsync();
            else if (ContentArea.Content is NetworkView nv) _ = nv.RefreshAsync();
            else if (ContentArea.Content is PeripheralsView pv) _ = pv.RefreshAsync();
            else if (ContentArea.Content is DriversView drv) _ = drv.RefreshAsync();
            else if (ContentArea.Content is ServicesView srv) _ = srv.RefreshAsync();
            else if (ContentArea.Content is LogViewerView lv) lv.RefreshLog();
        }
    }

    // ─── Health Status ───────────────────────────────────────────

    private void OnHealthStatusChanged(HealthStatus status)
    {
        Dispatcher.BeginInvoke(() => UpdateHealthIndicator(status));
    }

    private void UpdateHealthIndicator(HealthStatus status)
    {
        if (status.OverallHealthy)
        {
            StatusDot.Fill = (Brush)FindResource("SuccessBrush");
            StatusLabel.Text = "System Healthy";
        }
        else if (status.Issues.Count > 0)
        {
            StatusDot.Fill = (Brush)FindResource("WarningBrush");
            StatusLabel.Text = status.Issues.First();
        }
        else
        {
            StatusDot.Fill = (Brush)FindResource("ErrorBrush");
            StatusLabel.Text = "Issues Detected";
        }
    }

    // ─── Window Chrome Buttons ───────────────────────────────────

    private void MinimizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState.Minimized;
    }

    private void MaximizeClick(object sender, RoutedEventArgs e)
    {
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }

    /// <summary>
    /// Close button minimizes to tray instead of exiting.
    /// </summary>
    private void CloseClick(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        // Update maximize button icon
        if (MaxBtn != null)
        {
            MaxBtn.Content = WindowState == WindowState.Maximized ? "\uE923" : "\uE922";
            MaxBtn.ToolTip = WindowState == WindowState.Maximized ? "Restore" : "Maximize";
        }

        // Fix padding when maximized to prevent content from going behind screen edges
        if (RootGrid != null)
        {
            RootGrid.Margin = WindowState == WindowState.Maximized
                ? new Thickness(7)
                : new Thickness(0);
        }
    }

    /// <summary>
    /// WndProc hook — handles WM_GETMINMAXINFO to constrain maximized window
    /// to the current monitor's work area (respects taskbar).
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (msg == WM_GETMINMAXINFO)
        {
            var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<MINMAXINFO>(lParam);
            var monitor = MonitorFromWindow(hwnd, 2 /* MONITOR_DEFAULTTONEAREST */);
            if (monitor != IntPtr.Zero)
            {
                var monitorInfo = new MONITORINFO { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<MONITORINFO>() };
                if (GetMonitorInfo(monitor, ref monitorInfo))
                {
                    var work = monitorInfo.rcWork;
                    var monArea = monitorInfo.rcMonitor;
                    mmi.ptMaxPosition.X = work.Left - monArea.Left;
                    mmi.ptMaxPosition.Y = work.Top - monArea.Top;
                    mmi.ptMaxSize.X = work.Right - work.Left;
                    mmi.ptMaxSize.Y = work.Bottom - work.Top;
                }
            }
            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, lParam, true);
        }
        return IntPtr.Zero;
    }

    // ─── Win32 Interop for monitor-aware maximize ────────────────

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, uint dwFlags);

    [System.Runtime.InteropServices.DllImport("user32.dll")]
    private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct POINT { public int X, Y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct RECT { public int Left, Top, Right, Bottom; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    private struct MONITORINFO
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public int dwFlags;
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        // Prevent actual close — just hide to tray
        e.Cancel = true;
        Hide();
    }
}
