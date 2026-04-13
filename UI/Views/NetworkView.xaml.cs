using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Network diagnostics view with connectivity info, ping tests, and network tools.
/// </summary>
public partial class NetworkView : UserControl
{
    private NetworkInfo? _currentInfo;

    public NetworkView()
    {
        InitializeComponent();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var info = await NetworkService.GetNetworkInfoAsync();
            _currentInfo = info;

            // Connection
            TxtIp.Text = info.IPAddress;
            TxtSubnet.Text = info.SubnetMask;
            TxtGateway.Text = info.DefaultGateway;
            TxtMac.Text = info.MacAddress;

            // Domain / DNS
            TxtDomain.Text = info.IsDomainJoined ? info.DomainName : "Not domain-joined";
            DomainDot.Fill = info.IsDomainJoined
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("TextTertiaryBrush");
            TxtDns.Text = info.DnsServers;
            TxtAdapter.Text = $"{info.AdapterName} ({info.AdapterType})";

            // VPN
            TxtVpn.Text = info.IsVpnConnected ? info.VpnName : "Not connected";
            VpnDot.Fill = info.IsVpnConnected
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("TextTertiaryBrush");

            // Wi-Fi
            if (!string.IsNullOrEmpty(info.WifiSSID))
            {
                TxtWifiSsid.Text = info.WifiSSID;
                TxtWifiSignal.Text = $"{info.WifiSignalPercent}%";
                TxtWifiSignal.Foreground = info.WifiSignalPercent > 60
                    ? (Brush)FindResource("SuccessBrush")
                    : info.WifiSignalPercent > 30
                        ? (Brush)FindResource("WarningBrush")
                        : (Brush)FindResource("ErrorBrush");
            }
            else
            {
                TxtWifiSsid.Text = "N/A (Wired)";
                TxtWifiSignal.Text = "N/A";
            }

            TxtSpeed.Text = info.ConnectionSpeed;
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("NetworkView", "Error refreshing network info", ex.Message);
        }
    }

    // ─── Ping Tests ──────────────────────────────────────────────

    private async void PingGoogle_Click(object sender, RoutedEventArgs e)
    {
        BtnPingGoogle.IsEnabled = false;
        await RunPingTest("8.8.8.8");
        BtnPingGoogle.IsEnabled = true;
    }

    private async void PingGateway_Click(object sender, RoutedEventArgs e)
    {
        var gateway = _currentInfo?.DefaultGateway;
        if (string.IsNullOrEmpty(gateway))
        {
            ShowPingResult("No gateway detected", false, 0);
            return;
        }
        BtnPingGateway.IsEnabled = false;
        await RunPingTest(gateway);
        BtnPingGateway.IsEnabled = true;
    }

    private async void PingCustom_Click(object sender, RoutedEventArgs e)
    {
        var host = TxtCustomHost.Text.Trim();
        if (string.IsNullOrEmpty(host)) return;
        BtnPingCustom.IsEnabled = false;
        await RunPingTest(host);
        BtnPingCustom.IsEnabled = true;
    }

    private async Task RunPingTest(string host)
    {
        var result = await NetworkService.PingHostAsync(host);
        ShowPingResult(host, result.Success, result.RoundtripMs, result.Status);
    }

    private void ShowPingResult(string host, bool success, long msRoundtrip, string status = "")
    {
        var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 4, 0, 4) };

        // Status dot
        row.Children.Add(new System.Windows.Shapes.Ellipse
        {
            Width = 8, Height = 8,
            Fill = success ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        });

        // Timestamp
        row.Children.Add(new TextBlock
        {
            Text = DateTime.Now.ToString("HH:mm:ss"),
            FontSize = 11,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 10, 0),
            FontFamily = (FontFamily)FindResource("MainFont")
        });

        // Host + result
        var resultText = success
            ? $"Ping {host} — {msRoundtrip}ms"
            : $"Ping {host} — FAILED ({status})";

        row.Children.Add(new TextBlock
        {
            Text = resultText,
            FontSize = 12,
            Foreground = success ? (Brush)FindResource("TextPrimaryBrush") : (Brush)FindResource("ErrorBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            FontFamily = (FontFamily)FindResource("MainFont")
        });

        // Insert at top
        PingResults.Children.Insert(0, row);

        // Limit to 10 results
        while (PingResults.Children.Count > 10)
            PingResults.Children.RemoveAt(PingResults.Children.Count - 1);
    }

    // ─── Network Tools ───────────────────────────────────────────

    private async void FlushDns_Click(object sender, RoutedEventArgs e)
    {
        BtnFlushDns.IsEnabled = false;
        ShowStatus("Flushing DNS cache...");
        var result = await NetworkService.FlushDnsAsync();
        ShowStatus(result.Message, result.Success);
        BtnFlushDns.IsEnabled = true;
    }

    private async void RenewIp_Click(object sender, RoutedEventArgs e)
    {
        BtnRenewIp.IsEnabled = false;
        ShowStatus("Renewing IP address...");
        var result = await NetworkService.RenewIpAsync();
        ShowStatus(result.Message, result.Success);
        BtnRenewIp.IsEnabled = true;
        await RefreshAsync(); // Refresh to show new IP
    }

    private async void ResetStack_Click(object sender, RoutedEventArgs e)
    {
        var mbResult = MessageBox.Show(
            "This will reset your network stack (Winsock + TCP/IP).\nA reboot will be required.\n\nContinue?",
            "Reset Network Stack",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (mbResult == MessageBoxResult.Yes)
        {
            BtnResetStack.IsEnabled = false;
            ShowStatus("Resetting network stack...");
            var result = await NetworkService.ResetNetworkStackAsync();
            ShowStatus(result.Message, result.Success);
            BtnResetStack.IsEnabled = true;
        }
    }

    private void OpenSettings_Click(object sender, RoutedEventArgs e)
    {
        NetworkService.OpenNetworkSettings();
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private void ShowStatus(string message, bool? success = null)
    {
        StatusBanner.Visibility = Visibility.Visible;
        StatusText.Text = message;

        if (success.HasValue)
        {
            StatusBanner.BorderBrush = success.Value
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("ErrorBrush");
            StatusText.Foreground = success.Value
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("ErrorBrush");
        }
        else
        {
            StatusBanner.BorderBrush = (Brush)FindResource("AccentBrush");
            StatusText.Foreground = (Brush)FindResource("TextPrimaryBrush");
        }
    }
}
