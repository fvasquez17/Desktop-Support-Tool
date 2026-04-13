namespace DesktopSupportTool.Models;

/// <summary>
/// Network adapter and connectivity information.
/// </summary>
public class NetworkInfo
{
    public string AdapterName { get; set; } = string.Empty;
    public string AdapterType { get; set; } = string.Empty;
    public string IPAddress { get; set; } = string.Empty;
    public string SubnetMask { get; set; } = string.Empty;
    public string DefaultGateway { get; set; } = string.Empty;
    public string MacAddress { get; set; } = string.Empty;
    public string DnsServers { get; set; } = string.Empty;

    // Domain
    public string DomainName { get; set; } = string.Empty;
    public bool IsDomainJoined { get; set; }

    // VPN
    public bool IsVpnConnected { get; set; }
    public string VpnName { get; set; } = string.Empty;

    // Wi-Fi
    public string WifiSSID { get; set; } = string.Empty;
    public int WifiSignalPercent { get; set; }

    // Speed
    public string ConnectionSpeed { get; set; } = string.Empty;

    /// <summary>
    /// Formats network info for clipboard / ticket pasting.
    /// </summary>
    public string ToClipboardText()
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("=== NETWORK INFORMATION ===");
        sb.AppendLine($"Adapter:        {AdapterName} ({AdapterType})");
        sb.AppendLine($"IP Address:     {IPAddress}");
        sb.AppendLine($"Subnet:         {SubnetMask}");
        sb.AppendLine($"Gateway:        {DefaultGateway}");
        sb.AppendLine($"MAC:            {MacAddress}");
        sb.AppendLine($"DNS:            {DnsServers}");
        sb.AppendLine($"Domain:         {DomainName} (Joined: {IsDomainJoined})");
        sb.AppendLine($"VPN:            {(IsVpnConnected ? VpnName : "Not connected")}");
        if (!string.IsNullOrEmpty(WifiSSID))
        {
            sb.AppendLine($"Wi-Fi SSID:     {WifiSSID}");
            sb.AppendLine($"Wi-Fi Signal:   {WifiSignalPercent}%");
        }
        sb.AppendLine($"Speed:          {ConnectionSpeed}");
        return sb.ToString();
    }
}

/// <summary>
/// Result of a ping test.
/// </summary>
public class PingResult
{
    public string Host { get; set; } = string.Empty;
    public bool Success { get; set; }
    public long RoundtripMs { get; set; }
    public string Status { get; set; } = string.Empty;
}
