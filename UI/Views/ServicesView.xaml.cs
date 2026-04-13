using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Services view — lists all Windows services, provides start/stop/restart controls,
/// startup type changes, and quick access to common helpdesk services.
/// </summary>
public partial class ServicesView : UserControl
{
    private List<ServiceInfo> _allServices = new();

    public ServicesView()
    {
        InitializeComponent();
    }

    /// <summary>
    /// Loads all services asynchronously.
    /// </summary>
    public async Task RefreshAsync()
    {
        LoadingOverlay.Visibility = Visibility.Visible;

        try
        {
            _allServices = await WindowsServiceManager.GetAllServicesAsync();

            RenderCommonServices();
            ApplyFilters();

            TxtServiceCount.Text = $"{_allServices.Count} services";
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("ServicesView", "Error loading services", ex.Message);
        }
        finally
        {
            LoadingOverlay.Visibility = Visibility.Collapsed;
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  COMMON SERVICES QUICK PANEL
    // ═══════════════════════════════════════════════════════════

    private void RenderCommonServices()
    {
        CommonServicePanel.Children.Clear();

        foreach (var (name, display, category) in WindowsServiceManager.CommonServices)
        {
            var svc = _allServices.FirstOrDefault(s =>
                s.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            if (svc == null) continue;

            var chip = new Border
            {
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(10, 6, 10, 6),
                Margin = new Thickness(0, 0, 6, 6),
                Background = svc.IsRunning
                    ? (Brush)FindResource("AccentDimBrush")
                    : (Brush)FindResource("SurfaceHighBrush"),
                BorderBrush = svc.IsRunning
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                Tag = svc
            };

            var content = new StackPanel { Orientation = Orientation.Horizontal };

            // Status dot
            content.Children.Add(new Ellipse
            {
                Width = 6,
                Height = 6,
                Fill = svc.IsRunning
                    ? (Brush)FindResource("SuccessBrush")
                    : (Brush)FindResource("ErrorBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 6, 0)
            });

            content.Children.Add(new TextBlock
            {
                Text = display,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            });

            chip.Child = content;
            chip.MouseLeftButtonUp += CommonService_Click;
            CommonServicePanel.Children.Add(chip);
        }
    }

    private async void CommonService_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is not Border border || border.Tag is not ServiceInfo svc) return;

        // Quick toggle: if running -> restart, if stopped -> start
        if (svc.IsRunning)
        {
            var answer = MessageBox.Show(
                $"Restart \"{svc.DisplayName}\"?",
                "Restart Service",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (answer == MessageBoxResult.Yes)
            {
                var result = await WindowsServiceManager.RestartServiceAsync(svc.Name);
                ShowResult(result);
                await RefreshAsync();
            }
        }
        else
        {
            var result = await WindowsServiceManager.StartServiceAsync(svc.Name);
            ShowResult(result);
            await RefreshAsync();
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  FULL SERVICE LIST
    // ═══════════════════════════════════════════════════════════

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (_allServices.Count > 0) ApplyFilters();
    }

    private void StatusFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (_allServices.Count > 0) ApplyFilters();
    }

    private void ApplyFilters()
    {
        if (ServiceList == null || SearchBox == null || StatusFilter == null) return;
        var search = SearchBox?.Text?.Trim() ?? "";
        var statusSelection = (StatusFilter?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "All";

        IEnumerable<ServiceInfo> filtered = _allServices;

        // Status filter
        if (statusSelection == "Running")
            filtered = filtered.Where(s => s.Status == "Running");
        else if (statusSelection == "Stopped")
            filtered = filtered.Where(s => s.Status == "Stopped");

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(s =>
                s.DisplayName.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                s.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                (s.Description?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
        }

        RenderServiceList(filtered.ToList());
    }

    private void RenderServiceList(List<ServiceInfo> services)
    {
        ServiceList.Children.Clear();

        if (services.Count == 0)
        {
            ServiceList.Children.Add(new TextBlock
            {
                Text = "No services match the current filter",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(16, 16, 0, 0),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
            return;
        }

        foreach (var svc in services)
        {
            var rowBorder = new Border
            {
                Padding = new Thickness(16, 10, 16, 10),
                BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(0, 0, 0, 0.5)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(24) });   // status
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // name
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(90) });   // status text
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) });  // startup
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });      // actions

            // Status dot
            var dot = new Ellipse
            {
                Width = 8,
                Height = 8,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = svc.Status switch
                {
                    "Running" => (Brush)FindResource("SuccessBrush"),
                    "Stopped" => (Brush)FindResource("ErrorBrush"),
                    "Paused" => (Brush)FindResource("WarningBrush"),
                    _ => (Brush)FindResource("TextTertiaryBrush")
                }
            };
            Grid.SetColumn(dot, 0);
            grid.Children.Add(dot);

            // Display name + service name
            var nameStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            nameStack.Children.Add(new TextBlock
            {
                Text = svc.DisplayName,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                ToolTip = svc.Description ?? svc.DisplayName
            });
            nameStack.Children.Add(new TextBlock
            {
                Text = svc.Name,
                FontSize = 10,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                FontFamily = new FontFamily("Consolas"),
                TextTrimming = TextTrimming.CharacterEllipsis
            });
            Grid.SetColumn(nameStack, 1);
            grid.Children.Add(nameStack);

            // Status
            var statusText = new TextBlock
            {
                Text = svc.Status,
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = svc.Status switch
                {
                    "Running" => (Brush)FindResource("SuccessBrush"),
                    "Stopped" => (Brush)FindResource("ErrorBrush"),
                    "Paused" => (Brush)FindResource("WarningBrush"),
                    _ => (Brush)FindResource("TextSecondaryBrush")
                },
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            };
            Grid.SetColumn(statusText, 2);
            grid.Children.Add(statusText);

            // Startup type
            var startupText = new TextBlock
            {
                Text = svc.StartupType,
                FontSize = 11,
                Foreground = svc.StartupType == "Disabled"
                    ? (Brush)FindResource("WarningBrush")
                    : (Brush)FindResource("TextTertiaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            };
            Grid.SetColumn(startupText, 3);
            grid.Children.Add(startupText);

            // Action buttons
            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (svc.IsRunning)
            {
                // Restart
                var restartBtn = CreateActionButton("\uE72C", "Restart", svc.Name);
                restartBtn.Click += RestartService_Click;
                btnStack.Children.Add(restartBtn);

                // Stop
                var stopBtn = CreateActionButton("\uE71A", "Stop", svc.Name);
                stopBtn.Click += StopService_Click;
                btnStack.Children.Add(stopBtn);
            }
            else
            {
                // Start
                var startBtn = CreateActionButton("\uE768", "Start", svc.Name);
                startBtn.Click += StartService_Click;
                btnStack.Children.Add(startBtn);
            }

            // More options (startup type change)
            var moreBtn = new Button
            {
                Style = (Style)FindResource("SecondaryButton"),
                Padding = new Thickness(6, 4, 6, 4),
                Margin = new Thickness(2, 0, 0, 0),
                Content = new TextBlock { Text = "•••", FontSize = 10 },
                Tag = svc,
                ContextMenu = BuildServiceContextMenu(svc)
            };
            moreBtn.Click += (s, _) => { if (s is Button b) b.ContextMenu.IsOpen = true; };
            btnStack.Children.Add(moreBtn);

            Grid.SetColumn(btnStack, 4);
            grid.Children.Add(btnStack);

            rowBorder.Child = grid;
            ServiceList.Children.Add(rowBorder);
        }
    }

    private Button CreateActionButton(string icon, string tooltip, string serviceName)
    {
        var btn = new Button
        {
            Style = (Style)FindResource("SecondaryButton"),
            Padding = new Thickness(6, 4, 6, 4),
            Margin = new Thickness(2, 0, 0, 0),
            ToolTip = tooltip,
            Tag = serviceName,
            Content = new TextBlock
            {
                Text = icon,
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 12
            }
        };
        return btn;
    }

    private ContextMenu BuildServiceContextMenu(ServiceInfo svc)
    {
        var menu = new ContextMenu
        {
            Background = (Brush)FindResource("SurfaceHighBrush"),
            Foreground = (Brush)FindResource("TextPrimaryBrush"),
            BorderBrush = (Brush)FindResource("BorderSubtleBrush")
        };

        // Startup type options
        var autoItem = new MenuItem { Header = "Set Startup: Automatic", Tag = new { svc.Name, Type = "Automatic" } };
        autoItem.Click += SetStartupType_Click;
        menu.Items.Add(autoItem);

        var manualItem = new MenuItem { Header = "Set Startup: Manual", Tag = new { svc.Name, Type = "Manual" } };
        manualItem.Click += SetStartupType_Click;
        menu.Items.Add(manualItem);

        var disableItem = new MenuItem { Header = "Set Startup: Disabled", Tag = new { svc.Name, Type = "Disabled" } };
        disableItem.Click += SetStartupType_Click;
        menu.Items.Add(disableItem);

        menu.Items.Add(new Separator());

        // Details
        var detailItem = new MenuItem { Header = "View Details", Tag = svc };
        detailItem.Click += ViewDetails_Click;
        menu.Items.Add(detailItem);

        return menu;
    }

    // ═══════════════════════════════════════════════════════════
    //  EVENT HANDLERS
    // ═══════════════════════════════════════════════════════════

    private async void StartService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string name) return;
        btn.IsEnabled = false;
        var result = await WindowsServiceManager.StartServiceAsync(name);
        ShowResult(result);
        btn.IsEnabled = true;
        await RefreshAsync();
    }

    private async void StopService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string name) return;

        var svc = _allServices.FirstOrDefault(s => s.Name == name);
        var displayName = svc?.DisplayName ?? name;

        var answer = MessageBox.Show(
            $"Stop \"{displayName}\"?\n\nStopping critical services may affect system stability.",
            "Stop Service",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (answer == MessageBoxResult.Yes)
        {
            btn.IsEnabled = false;
            var result = await WindowsServiceManager.StopServiceAsync(name);
            ShowResult(result);
            btn.IsEnabled = true;
            await RefreshAsync();
        }
    }

    private async void RestartService_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string name) return;
        btn.IsEnabled = false;
        var result = await WindowsServiceManager.RestartServiceAsync(name);
        ShowResult(result);
        btn.IsEnabled = true;
        await RefreshAsync();
    }

    private async void SetStartupType_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item) return;

        dynamic tag = item.Tag;
        string name = tag.Name;
        string type = tag.Type;

        var result = await WindowsServiceManager.SetStartupTypeAsync(name, type);
        ShowResult(result);
        await RefreshAsync();
    }

    private void ViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not MenuItem item || item.Tag is not ServiceInfo svc) return;

        var info = $"Service: {svc.DisplayName}\n" +
                   $"Name: {svc.Name}\n" +
                   $"Status: {svc.Status}\n" +
                   $"Startup Type: {svc.StartupType}\n" +
                   $"Log On As: {svc.Account}\n" +
                   $"PID: {svc.ProcessId}\n" +
                   $"Path: {svc.PathName}\n\n" +
                   $"Description:\n{svc.Description}";

        MessageBox.Show(info, $"Service Details — {svc.DisplayName}",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private async void Refresh_Click(object sender, RoutedEventArgs e) => await RefreshAsync();

    private void OpenServicesConsole_Click(object sender, RoutedEventArgs e)
        => WindowsServiceManager.OpenServicesConsole();

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
