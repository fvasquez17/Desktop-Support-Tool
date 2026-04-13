using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// System Information view — displays machine details, OS info, hardware, performance, and disks.
/// </summary>
public partial class SystemInfoView : UserControl
{
    private SystemInfo? _currentInfo;

    public SystemInfoView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Fetches and displays all system information.
    /// </summary>
    public async Task RefreshAsync()
    {
        try
        {
            var info = await SystemInfoService.GetSystemInfoAsync();
            _currentInfo = info;

            // Machine Identity
            TxtHostname.Text = info.Hostname;
            TxtUsername.Text = info.Username;
            TxtDomain.Text = info.Domain;
            TxtSerial.Text = info.SerialNumber;
            TxtManufacturer.Text = info.Manufacturer;
            TxtModel.Text = info.Model;

            // OS
            TxtOsVersion.Text = info.OSVersion;
            TxtOsBuild.Text = info.OSBuild;
            TxtActivation.Text = info.WindowsActivation;
            TxtActivation.Foreground = info.WindowsActivation.Contains("Activated")
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("WarningBrush");

            var uptime = info.Uptime;
            TxtUptime.Text = uptime.TotalDays >= 1
                ? $"{(int)uptime.TotalDays}d {uptime.Hours}h {uptime.Minutes}m"
                : $"{(int)uptime.TotalHours}h {uptime.Minutes}m";

            // Hardware
            TxtCpu.Text = info.CpuName;
            TxtBios.Text = info.BiosVersion;
            TxtTotalRam.Text = $"{info.TotalRamMB:N0} MB ({info.TotalRamMB / 1024.0:F1} GB)";

            // Battery
            if (info.HasBattery)
            {
                BatteryPanel.Visibility = Visibility.Visible;
                TxtBattery.Text = $"{info.BatteryPercent}% ({info.BatteryStatus})";
            }
            else
            {
                BatteryPanel.Visibility = Visibility.Collapsed;
            }

            // Performance
            UpdatePerformanceMetrics(info);

            // Disks
            RenderDisks(info.Disks);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("SystemInfoView", "Error refreshing system info", ex.Message);
        }
    }

    private void UpdatePerformanceMetrics(SystemInfo info)
    {
        // CPU
        TxtCpuUsage.Text = $"{info.CpuUsagePercent:F0}%";
        if (CpuUsageBar.Parent is FrameworkElement parent1)
        {
            CpuUsageBar.Width = Math.Max(0, parent1.ActualWidth * info.CpuUsagePercent / 100);
        }
        CpuUsageBar.Background = GetStatusBrush(info.CpuUsagePercent);

        // RAM
        TxtRamUsage.Text = $"{info.RamUsagePercent:F0}%";
        TxtRamDetail.Text = $"{info.UsedRamMB:N0} / {info.TotalRamMB:N0} MB";
        if (RamUsageBar.Parent is FrameworkElement parent2)
        {
            RamUsageBar.Width = Math.Max(0, parent2.ActualWidth * info.RamUsagePercent / 100);
        }
        RamUsageBar.Background = GetStatusBrush(info.RamUsagePercent);
    }

    private void RenderDisks(List<DiskInfo> disks)
    {
        DiskList.Children.Clear();

        foreach (var disk in disks)
        {
            var card = new Border
            {
                Background = (Brush)FindResource("SurfaceBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var stack = new StackPanel();

            // Drive label
            var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 6) };
            headerRow.Children.Add(new TextBlock
            {
                Text = $"{disk.Drive}",
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
            if (!string.IsNullOrEmpty(disk.Label))
            {
                headerRow.Children.Add(new TextBlock
                {
                    Text = $"  ({disk.Label})",
                    FontSize = 13,
                    Foreground = (Brush)FindResource("TextTertiaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = (FontFamily)FindResource("MainFont")
                });
            }
            stack.Children.Add(headerRow);

            // Usage text
            stack.Children.Add(new TextBlock
            {
                Text = $"{disk.FreeGB:F1} GB free of {disk.TotalGB:F1} GB ({disk.UsagePercent:F0}% used)",
                FontSize = 12,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                Margin = new Thickness(0, 0, 0, 8),
                FontFamily = (FontFamily)FindResource("MainFont")
            });

            // Progress bar
            var barBg = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = (Brush)FindResource("SurfaceHighBrush")
            };
            var barFg = new Border
            {
                Height = 6,
                CornerRadius = new CornerRadius(3),
                Background = GetStatusBrush(disk.UsagePercent),
                HorizontalAlignment = HorizontalAlignment.Left,
                Width = 0
            };
            barBg.Child = barFg;
            stack.Children.Add(barBg);

            card.Child = stack;
            DiskList.Children.Add(card);

            // Set bar width after layout
            card.Loaded += (s, e) =>
            {
                barFg.Width = Math.Max(0, barBg.ActualWidth * disk.UsagePercent / 100);
            };
        }
    }

    // ─── Button Handlers ─────────────────────────────────────────

    private void CopyAll_Click(object sender, RoutedEventArgs e)
    {
        if (_currentInfo != null)
        {
            try
            {
                Clipboard.SetText(_currentInfo.ToClipboardText());
                LoggingService.Instance.Info("SystemInfo", "System info copied to clipboard");
                // Brief visual feedback
                if (sender is Button btn)
                {
                    var original = btn.Content;
                    btn.Content = new TextBlock
                    {
                        Text = "✓ Copied!",
                        Foreground = Brushes.White,
                        FontWeight = FontWeights.SemiBold
                    };
                    _ = Task.Delay(1500).ContinueWith(_ =>
                        Dispatcher.BeginInvoke(() => btn.Content = original));
                }
            }
            catch (Exception ex)
            {
                LoggingService.Instance.Error("SystemInfo", "Failed to copy to clipboard", ex.Message);
            }
        }
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshAsync();
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private Brush GetStatusBrush(double percent)
    {
        if (percent >= 85) return (Brush)FindResource("ErrorBrush");
        if (percent >= 60) return (Brush)FindResource("WarningBrush");
        return (Brush)FindResource("SuccessBrush");
    }
}
