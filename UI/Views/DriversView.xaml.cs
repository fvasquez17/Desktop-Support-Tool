using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Drivers view — lists all signed drivers, highlights problems,
/// and provides troubleshooting actions per device.
/// </summary>
public partial class DriversView : UserControl
{
    private List<DriverInfo> _allDrivers = new();
    private List<DriverInfo> _problemDevices = new();
    private readonly HashSet<string> _deviceClasses = new();

    public DriversView()
    {
        InitializeComponent();

        ClassFilter.Items.Add("All Classes");
        ClassFilter.SelectedIndex = 0;
    }

    /// <summary>
    /// Loads all driver data asynchronously.
    /// </summary>
    public async Task RefreshAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            // Fetch drivers and problems in parallel
            var driversTask = DriverService.GetAllDriversAsync();
            var problemsTask = DriverService.GetProblemDevicesAsync();

            await Task.WhenAll(driversTask, problemsTask);

            _allDrivers = await driversTask;
            _problemDevices = await problemsTask;

            // Populate class filter
            _deviceClasses.Clear();
            foreach (var d in _allDrivers)
            {
                if (!string.IsNullOrEmpty(d.DeviceClass))
                    _deviceClasses.Add(d.DeviceClass);
            }

            var currentFilter = ClassFilter.SelectedItem?.ToString();
            ClassFilter.Items.Clear();
            ClassFilter.Items.Add("All Classes");
            foreach (var cls in _deviceClasses.OrderBy(c => c))
                ClassFilter.Items.Add(cls);
            ClassFilter.SelectedIndex = 0;

            // Render
            RenderProblemDevices();
            ApplyFilters();

            TxtDriverCount.Text = $"{_allDrivers.Count} drivers";
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("DriversView", "Error loading drivers", ex.Message);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    // ─── Problem Devices ─────────────────────────────────────────

    private void RenderProblemDevices()
    {
        ProblemDeviceList.Children.Clear();

        if (_problemDevices.Count == 0)
        {
            ProblemBanner.Visibility = Visibility.Collapsed;
            return;
        }

        ProblemBanner.Visibility = Visibility.Visible;
        TxtProblemCount.Text = $"{_problemDevices.Count} Problem Device{(_problemDevices.Count > 1 ? "s" : "")}";

        foreach (var problem in _problemDevices)
        {
            var row = new Grid { Margin = new Thickness(0, 4, 0, 4) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Problem info
            var infoStack = new StackPanel();
            infoStack.Children.Add(new TextBlock
            {
                Text = problem.DeviceName,
                FontSize = 13,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
            infoStack.Children.Add(new TextBlock
            {
                Text = $"Code {problem.ErrorCode}: {problem.ProblemDescription}",
                FontSize = 11,
                Foreground = (Brush)FindResource("ErrorBrush"),
                FontFamily = (FontFamily)FindResource("MainFont"),
                TextWrapping = TextWrapping.Wrap
            });
            Grid.SetColumn(infoStack, 0);
            row.Children.Add(infoStack);

            // Action buttons
            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            // Troubleshoot: try reinstall for most errors
            var fixBtn = new Button
            {
                Style = (Style)FindResource("PrimaryButton"),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 0),
                Tag = problem.DeviceId
            };
            fixBtn.Content = new TextBlock { Text = "Troubleshoot", FontSize = 11 };
            fixBtn.Click += TroubleshootDevice_Click;
            btnStack.Children.Add(fixBtn);

            // Enable (if disabled)
            if (problem.ErrorCode == 22)
            {
                var enableBtn = new Button
                {
                    Style = (Style)FindResource("SuccessButton"),
                    Padding = new Thickness(10, 6, 10, 6),
                    Tag = problem.DeviceId
                };
                enableBtn.Content = new TextBlock { Text = "Enable", FontSize = 11, Foreground = Brushes.White };
                enableBtn.Click += EnableDevice_Click;
                btnStack.Children.Add(enableBtn);
            }

            Grid.SetColumn(btnStack, 1);
            row.Children.Add(btnStack);

            ProblemDeviceList.Children.Add(row);
        }
    }

    // ─── Driver List ─────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ClassFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var search = SearchBox?.Text?.Trim() ?? "";
        var selectedClass = ClassFilter?.SelectedItem?.ToString() ?? "All Classes";

        IEnumerable<DriverInfo> filtered = _allDrivers;

        // Class filter
        if (selectedClass != "All Classes")
        {
            filtered = filtered.Where(d =>
                d.DeviceClass.Equals(selectedClass, StringComparison.OrdinalIgnoreCase));
        }

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(d =>
                d.DeviceName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.Manufacturer.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.DeviceClass.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                d.DriverVersion.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        RenderDriverList(filtered.ToList());
    }

    private void RenderDriverList(List<DriverInfo> drivers)
    {
        DriverList.Children.Clear();

        if (drivers.Count == 0)
        {
            DriverList.Children.Add(new TextBlock
            {
                Text = "No drivers match the current filter",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(16, 16, 0, 0),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
            return;
        }

        // Group by device class
        foreach (var group in drivers.GroupBy(d => d.DeviceClass))
        {
            // Class header
            var headerBorder = new Border
            {
                Background = (Brush)FindResource("SurfaceHighBrush"),
                Padding = new Thickness(16, 8, 16, 8),
            };
            headerBorder.Child = new TextBlock
            {
                Text = group.Key.ToUpperInvariant(),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            };
            DriverList.Children.Add(headerBorder);

            foreach (var driver in group)
            {
                var rowBorder = new Border
                {
                    Padding = new Thickness(16, 10, 16, 10),
                    BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
                    BorderThickness = new Thickness(0, 0, 0, 0.5)
                };

                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(28) });   // icon
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });  // version
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });  // manufacturer
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });   // date
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });   // signed
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });       // actions

                // Icon
                var icon = new TextBlock
                {
                    Text = driver.ClassIcon,
                    FontFamily = (FontFamily)FindResource("IconFont"),
                    FontSize = 14,
                    Foreground = (Brush)FindResource("AccentBrush"),
                    VerticalAlignment = VerticalAlignment.Center
                };
                Grid.SetColumn(icon, 0);
                grid.Children.Add(icon);

                // Device name
                var name = new TextBlock
                {
                    Text = driver.DeviceName,
                    FontSize = 12,
                    Foreground = (Brush)FindResource("TextPrimaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontFamily = (FontFamily)FindResource("MainFont"),
                    ToolTip = driver.DeviceName
                };
                Grid.SetColumn(name, 1);
                grid.Children.Add(name);

                // Version
                var version = new TextBlock
                {
                    Text = driver.DriverVersion,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = new FontFamily("Consolas"),
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    ToolTip = driver.DriverVersion
                };
                Grid.SetColumn(version, 2);
                grid.Children.Add(version);

                // Manufacturer
                var mfr = new TextBlock
                {
                    Text = driver.Manufacturer,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextTertiaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                    FontFamily = (FontFamily)FindResource("MainFont")
                };
                Grid.SetColumn(mfr, 3);
                grid.Children.Add(mfr);

                // Date
                var date = new TextBlock
                {
                    Text = driver.DriverDate,
                    FontSize = 11,
                    Foreground = (Brush)FindResource("TextTertiaryBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = (FontFamily)FindResource("MainFont")
                };
                Grid.SetColumn(date, 4);
                grid.Children.Add(date);

                // Signed
                var signed = new TextBlock
                {
                    Text = driver.IsSigned ? "✓ Signed" : "✗ Unsigned",
                    FontSize = 10,
                    Foreground = driver.IsSigned
                        ? (Brush)FindResource("SuccessBrush")
                        : (Brush)FindResource("WarningBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = (FontFamily)FindResource("MainFont")
                };
                Grid.SetColumn(signed, 5);
                grid.Children.Add(signed);

                // Actions dropdown
                var actionsBtn = new Button
                {
                    Style = (Style)FindResource("SecondaryButton"),
                    Padding = new Thickness(8, 4, 8, 4),
                    Content = new TextBlock { Text = "•••", FontSize = 11 },
                    Tag = driver,
                    ContextMenu = BuildDriverContextMenu(driver)
                };
                actionsBtn.Click += (s, _) => { if (s is Button b) b.ContextMenu.IsOpen = true; };
                Grid.SetColumn(actionsBtn, 6);
                grid.Children.Add(actionsBtn);

                rowBorder.Child = grid;
                DriverList.Children.Add(rowBorder);
            }
        }
    }

    private ContextMenu BuildDriverContextMenu(DriverInfo driver)
    {
        var menu = new ContextMenu
        {
            Background = (Brush)FindResource("SurfaceHighBrush"),
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            BorderBrush = (Brush)FindResource("BorderSubtleBrush")
        };

        var updateItem = new MenuItem { Header = "Update Driver", Tag = driver.DeviceId };
        updateItem.Click += UpdateDriver_Click;
        menu.Items.Add(updateItem);

        var reinstallItem = new MenuItem { Header = "Reinstall Driver", Tag = driver.DeviceId };
        reinstallItem.Click += ReinstallDriver_Click;
        menu.Items.Add(reinstallItem);

        var rollbackItem = new MenuItem { Header = "Rollback Driver", Tag = driver.DeviceId };
        rollbackItem.Click += RollbackDriver_Click;
        menu.Items.Add(rollbackItem);

        menu.Items.Add(new Separator());

        var disableItem = new MenuItem
        {
            Header = driver.IsEnabled ? "Disable Device" : "Enable Device",
            Tag = new { driver.DeviceId, driver.IsEnabled }
        };
        disableItem.Click += ToggleDevice_Click;
        menu.Items.Add(disableItem);

        return menu;
    }

    // ─── Device Actions ──────────────────────────────────────────

    private async void TroubleshootDevice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string deviceId) return;

        btn.IsEnabled = false;
        var result = await DriverService.ReinstallDriverAsync(deviceId);
        ShowResult(result);
        btn.IsEnabled = true;
        await RefreshAsync();
    }

    private async void EnableDevice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string deviceId) return;

        btn.IsEnabled = false;
        var result = await DriverService.SetDeviceEnabledAsync(deviceId, true);
        ShowResult(result);
        btn.IsEnabled = true;
        await RefreshAsync();
    }

    private async void UpdateDriver_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string deviceId) return;

        var result = await DriverService.UpdateDriverAsync(deviceId);
        ShowResult(result);
    }

    private async void ReinstallDriver_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string deviceId) return;

        var answer = MessageBox.Show(
            "Reinstall this driver? The device will be temporarily unavailable.",
            "Reinstall Driver",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            var result = await DriverService.ReinstallDriverAsync(deviceId);
            ShowResult(result);
            await RefreshAsync();
        }
    }

    private async void RollbackDriver_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not string deviceId) return;

        var result = await DriverService.RollbackDriverAsync(deviceId);
        ShowResult(result);
    }

    private async void ToggleDevice_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;

        dynamic tag = item.Tag;
        string deviceId = tag.DeviceId;
        bool isEnabled = tag.IsEnabled;

        var action = isEnabled ? "Disable" : "Enable";
        var answer = MessageBox.Show(
            $"{action} this device?",
            $"{action} Device",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            var result = await DriverService.SetDeviceEnabledAsync(deviceId, !isEnabled);
            ShowResult(result);
            await RefreshAsync();
        }
    }

    // ─── Toolbar Actions ─────────────────────────────────────────

    private async void ExportDriverList_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = await DriverService.ExportDriverListAsync();
            MessageBox.Show(
                $"Driver report exported to:\n{path}",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ProcessHelper.Launch(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error exporting: {ex.Message}", "Error",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void ScanHardware_Click(object sender, RoutedEventArgs e)
    {
        BtnScanHw.IsEnabled = false;
        var result = await DriverService.ScanForHardwareChangesAsync();
        ShowResult(result);
        BtnScanHw.IsEnabled = true;
        await RefreshAsync();
    }

    private void OpenDeviceManager_Click(object sender, RoutedEventArgs e)
    {
        DriverService.OpenDeviceManager();
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private void ShowResult(ActionResult result)
    {
        var icon = result.Success ? "✓" : "✗";
        MessageBox.Show(
            $"{icon} {result.Message}",
            result.Success ? "Success" : "Error",
            MessageBoxButton.OK,
            result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
    }
}
