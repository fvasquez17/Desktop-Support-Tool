using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Dashboard view code-behind. Shows health summary, alerts, and quick-launch tools.
/// </summary>
public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        WelcomeText.Text = $"Welcome, {Environment.UserName} — {DateTime.Now:dddd, MMMM d, yyyy}";
    }

    /// <summary>
    /// Refreshes all dashboard data.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            // Fetch health status
            var health = await HealthCheckService.Instance.RunChecksAsync();

            // ─── CPU ───
            CpuValue.Text = $"{health.CpuUsage:F0}%";
            CpuBar.Width = Math.Max(0, CpuBar.Parent is FrameworkElement p1
                ? p1.ActualWidth * health.CpuUsage / 100 : 0);
            CpuBar.Background = GetStatusBrush(health.CpuUsage);

            // ─── RAM ───
            RamValue.Text = $"{health.RamUsagePercent:F0}%";
            RamBar.Width = Math.Max(0, RamBar.Parent is FrameworkElement p2
                ? p2.ActualWidth * health.RamUsagePercent / 100 : 0);
            RamBar.Background = GetStatusBrush(health.RamUsagePercent);

            // ─── Disk ───
            DiskValue.Text = $"{health.LowestDiskFreeGB:F0} GB";
            DiskDetail.Text = $"free on {health.LowestDiskDrive}";

            // ─── Network ───
            NetStatusDot.Fill = health.NetworkConnected
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("ErrorBrush");
            NetStatusText.Text = health.NetworkConnected ? "Connected" : "Offline";

            try
            {
                var netInfo = await NetworkService.GetNetworkInfoAsync();
                NetIpText.Text = netInfo.IPAddress;
            }
            catch { }

            // ─── Uptime ───
            var uptime = TimeSpan.FromMilliseconds(Environment.TickCount64);
            if (uptime.TotalDays >= 1)
                UptimeValue.Text = $"{(int)uptime.TotalDays}d {uptime.Hours}h";
            else
                UptimeValue.Text = $"{(int)uptime.TotalHours}h {uptime.Minutes}m";
            UptimeDetail.Text = $"since {DateTime.Now - uptime:MMM d, h:mm tt}";

            // ─── Alerts ───
            if (health.Issues.Count > 0)
            {
                AlertBanner.Visibility = Visibility.Visible;
                AlertText.Text = string.Join("\n", health.Issues);
            }
            else
            {
                AlertBanner.Visibility = Visibility.Collapsed;
            }

            // ─── Recent Activity ───
            LoadRecentActivity();
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("Dashboard", "Error refreshing dashboard", ex.Message);
        }
    }

    private void LoadRecentActivity()
    {
        var entries = LoggingService.Instance.GetEntries()
            .OrderByDescending(e => e.Timestamp)
            .Take(8)
            .ToList();

        RecentActivityList.Children.Clear();

        if (entries.Count == 0)
        {
            RecentActivityList.Children.Add(new TextBlock
            {
                Text = "No recent activity",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextTertiaryBrush")
            });
            return;
        }

        foreach (var entry in entries)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 3, 0, 3) };

            // Status dot
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0),
                Fill = entry.Level switch
                {
                    Models.LogLevel.Success => (Brush)FindResource("SuccessBrush"),
                    Models.LogLevel.Error => (Brush)FindResource("ErrorBrush"),
                    Models.LogLevel.Warning => (Brush)FindResource("WarningBrush"),
                    _ => (Brush)FindResource("TextTertiaryBrush"),
                }
            };
            row.Children.Add(dot);

            // Timestamp
            row.Children.Add(new TextBlock
            {
                Text = entry.Timestamp.ToString("HH:mm:ss"),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 0, 12, 0),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            });

            // Message
            row.Children.Add(new TextBlock
            {
                Text = entry.Message,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                MaxWidth = 500,
                FontFamily = (FontFamily)FindResource("MainFont")
            });

            RecentActivityList.Children.Add(row);
        }
    }

    // ─── Button Handlers ─────────────────────────────────────────

    private void LaunchRdp_Click(object sender, RoutedEventArgs e)
        => TroubleshootService.LaunchRdp();

    // ─── Helpers ─────────────────────────────────────────────────

    private Brush GetStatusBrush(double percent)
    {
        if (percent >= 85) return (Brush)FindResource("ErrorBrush");
        if (percent >= 60) return (Brush)FindResource("WarningBrush");
        return (Brush)FindResource("SuccessBrush");
    }
}
