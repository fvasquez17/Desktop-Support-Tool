using System.Net.NetworkInformation;
using System.Net.Sockets;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;

namespace DesktopSupportTool.Services;

/// <summary>
/// Network diagnostics and information gathering via .NET networking APIs and system commands.
/// </summary>
public static class NetworkService
{
    private static readonly LoggingService _log = LoggingService.Instance;

    /// <summary>
    /// Gathers comprehensive network information.
    /// </summary>
    public static async Task<NetworkInfo> GetNetworkInfoAsync()
    {
        return await Task.Run(() =>
        {
            var info = new NetworkInfo();

            try
            {
                // Find the best active network adapter (prefer Ethernet > Wi-Fi > others)
                var adapter = GetPrimaryAdapter();
                if (adapter == null)
                {
                    info.AdapterName = "No active network adapter";
                    return info;
                }

                info.AdapterName = adapter.Name;
                info.AdapterType = adapter.NetworkInterfaceType.ToString();
                info.ConnectionSpeed = FormatSpeed(adapter.Speed);
                info.MacAddress = FormatMac(adapter.GetPhysicalAddress().ToString());

                var ipProps = adapter.GetIPProperties();

                // IPv4 address & subnet
                foreach (var addr in ipProps.UnicastAddresses)
                {
                    if (addr.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        info.IPAddress = addr.Address.ToString();
                        info.SubnetMask = addr.IPv4Mask?.ToString() ?? "";
                        break;
                    }
                }

                // Default gateway
                foreach (var gw in ipProps.GatewayAddresses)
                {
                    if (gw.Address.AddressFamily == AddressFamily.InterNetwork)
                    {
                        info.DefaultGateway = gw.Address.ToString();
                        break;
                    }
                }

                // DNS servers
                var dnsServers = ipProps.DnsAddresses
                    .Where(a => a.AddressFamily == AddressFamily.InterNetwork)
                    .Select(a => a.ToString());
                info.DnsServers = string.Join(", ", dnsServers);

                // Domain
                info.DomainName = System.Net.NetworkInformation.IPGlobalProperties.GetIPGlobalProperties().DomainName;
                info.IsDomainJoined = !string.IsNullOrEmpty(info.DomainName);
                if (!info.IsDomainJoined)
                {
                    info.DomainName = Environment.UserDomainName;
                    info.IsDomainJoined = !info.DomainName.Equals(Environment.MachineName, StringComparison.OrdinalIgnoreCase);
                }

                // VPN detection
                DetectVpn(info);

                // Wi-Fi info
                if (adapter.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
                {
                    GetWifiInfo(info);
                }
            }
            catch (Exception ex)
            {
                _log.Error("Network", "Error gathering network information", ex.Message);
            }

            return info;
        });
    }

    /// <summary>
    /// Pings a host and returns the result.
    /// </summary>
    public static async Task<PingResult> PingHostAsync(string host, int timeoutMs = 3000)
    {
        var result = new PingResult { Host = host };
        try
        {
            using var ping = new Ping();
            var reply = await ping.SendPingAsync(host, timeoutMs);
            result.Success = reply.Status == IPStatus.Success;
            result.RoundtripMs = reply.RoundtripTime;
            result.Status = reply.Status.ToString();
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Status = ex.InnerException?.Message ?? ex.Message;
        }
        return result;
    }

    /// <summary>
    /// Flushes the DNS resolver cache (requires elevation).
    /// </summary>
    public static async Task<ActionResult> FlushDnsAsync()
    {
        _log.Info("Network", "Flushing DNS cache...");
        var result = await PowerShellRunner.RunCmdAsync("ipconfig /flushdns");
        _log.LogAction("Network", "Flush DNS", result);
        return result;
    }

    /// <summary>
    /// Renews the IP address via DHCP (requires elevation).
    /// </summary>
    public static async Task<ActionResult> RenewIpAsync()
    {
        _log.Info("Network", "Renewing IP address...");
        // Release then renew
        await PowerShellRunner.RunCmdAsync("ipconfig /release");
        var result = await PowerShellRunner.RunCmdAsync("ipconfig /renew", timeoutSeconds: 30);
        _log.LogAction("Network", "Renew IP", result);
        return result;
    }

    /// <summary>
    /// Resets the Winsock catalog and TCP/IP stack (requires elevation + reboot).
    /// </summary>
    public static async Task<ActionResult> ResetNetworkStackAsync()
    {
        _log.Info("Network", "Resetting network stack...");
        var r1 = await PowerShellRunner.RunCmdAsync("netsh winsock reset", elevated: true);
        var r2 = await PowerShellRunner.RunCmdAsync("netsh int ip reset", elevated: true);

        if (r1.Success && r2.Success)
        {
            var result = ActionResult.Ok("Network stack reset successfully. A reboot is required.");
            _log.LogAction("Network", "Reset Network Stack", result);
            return result;
        }

        var failResult = ActionResult.Fail(
            "Network stack reset may have partially failed.",
            $"Winsock: {r1.Message}\nTCP/IP: {r2.Message}");
        _log.LogAction("Network", "Reset Network Stack", failResult);
        return failResult;
    }

    /// <summary>
    /// Opens Windows Network & Internet settings.
    /// </summary>
    public static void OpenNetworkSettings()
    {
        ProcessHelper.OpenSettings("ms-settings:network");
    }

    // ─── Private Helpers ─────────────────────────────────────────

    private static NetworkInterface? GetPrimaryAdapter()
    {
        var adapters = NetworkInterface.GetAllNetworkInterfaces()
            .Where(a => a.OperationalStatus == OperationalStatus.Up
                     && a.NetworkInterfaceType != NetworkInterfaceType.Loopback
                     && a.NetworkInterfaceType != NetworkInterfaceType.Tunnel
                     // Exclude known virtual adapters
                     && !a.Description.Contains("Hyper-V", StringComparison.OrdinalIgnoreCase)
                     && !a.Description.Contains("Virtual", StringComparison.OrdinalIgnoreCase)
                     && !a.Description.Contains("VMware", StringComparison.OrdinalIgnoreCase)
                     && !a.Description.Contains("VirtualBox", StringComparison.OrdinalIgnoreCase)
                     && !a.Description.Contains("Docker", StringComparison.OrdinalIgnoreCase)
                     && !a.Description.Contains("vEthernet", StringComparison.OrdinalIgnoreCase)
                     && !a.Name.Contains("vEthernet", StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Prefer adapters that have a default gateway (meaning they route internet traffic)
        var withGateway = adapters
            .Where(a =>
            {
                var gateways = a.GetIPProperties().GatewayAddresses;
                return gateways.Any(g => g.Address.AddressFamily == AddressFamily.InterNetwork
                    && g.Address.ToString() != "0.0.0.0");
            })
            .OrderByDescending(a => a.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .ThenByDescending(a => a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .ThenByDescending(a => a.Speed)
            .ToList();

        if (withGateway.Count > 0)
            return withGateway.First();

        // Fallback: any active adapter sorted by preference
        return adapters
            .OrderByDescending(a => a.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
            .ThenByDescending(a => a.NetworkInterfaceType == NetworkInterfaceType.Wireless80211)
            .ThenByDescending(a => a.Speed)
            .FirstOrDefault();
    }

    private static void DetectVpn(NetworkInfo info)
    {
        try
        {
            // Check for VPN adapters (PPP, Tunnel, or adapters with VPN-like names)
            var vpnAdapters = NetworkInterface.GetAllNetworkInterfaces()
                .Where(a => a.OperationalStatus == OperationalStatus.Up
                         && (a.NetworkInterfaceType == NetworkInterfaceType.Ppp
                          || a.NetworkInterfaceType == NetworkInterfaceType.Tunnel
                          || a.Description.Contains("VPN", StringComparison.OrdinalIgnoreCase)
                          || a.Description.Contains("Cisco", StringComparison.OrdinalIgnoreCase)
                          || a.Description.Contains("Fortinet", StringComparison.OrdinalIgnoreCase)
                          || a.Description.Contains("GlobalProtect", StringComparison.OrdinalIgnoreCase)
                          || a.Description.Contains("Pulse", StringComparison.OrdinalIgnoreCase)
                          || a.Description.Contains("WireGuard", StringComparison.OrdinalIgnoreCase)
                          || a.Description.Contains("OpenVPN", StringComparison.OrdinalIgnoreCase)
                          || a.Description.Contains("Zscaler", StringComparison.OrdinalIgnoreCase)))
                .ToList();

            info.IsVpnConnected = vpnAdapters.Count > 0;
            info.VpnName = vpnAdapters.FirstOrDefault()?.Description ?? "";
        }
        catch
        {
            info.IsVpnConnected = false;
        }
    }

    private static void GetWifiInfo(NetworkInfo info)
    {
        try
        {
            // Use netsh to get Wi-Fi SSID and signal strength
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "netsh",
                Arguments = "wlan show interfaces",
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = System.Diagnostics.Process.Start(psi);
            if (proc == null) return;

            var output = proc.StandardOutput.ReadToEnd();
            proc.WaitForExit();

            foreach (var line in output.Split('\n'))
            {
                var trimmed = line.Trim();
                if (trimmed.StartsWith("SSID", StringComparison.OrdinalIgnoreCase)
                    && !trimmed.StartsWith("BSSID", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length == 2)
                        info.WifiSSID = parts[1].Trim();
                }
                else if (trimmed.StartsWith("Signal", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = trimmed.Split(':', 2);
                    if (parts.Length == 2)
                    {
                        var valStr = parts[1].Trim().Replace("%", "");
                        if (int.TryParse(valStr, out int signal))
                            info.WifiSignalPercent = signal;
                    }
                }
            }
        }
        catch { }
    }

    private static string FormatMac(string raw)
    {
        if (string.IsNullOrEmpty(raw) || raw.Length < 12) return raw;
        return string.Join(":", Enumerable.Range(0, 6).Select(i => raw.Substring(i * 2, 2)));
    }

    private static string FormatSpeed(long speedBps)
    {
        if (speedBps >= 1_000_000_000) return $"{speedBps / 1_000_000_000.0:F1} Gbps";
        if (speedBps >= 1_000_000) return $"{speedBps / 1_000_000.0:F0} Mbps";
        if (speedBps >= 1_000) return $"{speedBps / 1_000.0:F0} Kbps";
        return $"{speedBps} bps";
    }
}
