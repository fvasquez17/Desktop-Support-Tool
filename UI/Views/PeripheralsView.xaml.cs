using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Enhanced peripherals view — inline display resolution control, audio device management
/// with volume sliders, and printer add/remove/set-default.
/// </summary>
public partial class PeripheralsView : UserControl
{
    private List<AudioDeviceInfo> _playbackDevices = new();
    private List<AudioDeviceInfo> _recordingDevices = new();
    private bool _suppressVolumeEvents;

    public PeripheralsView()
    {
        InitializeComponent();
    }

    public async Task RefreshAsync()
    {
        try
        {
            var info = await PeripheralService.GetPeripheralInfoAsync();

            // ─── Monitors with resolution controls ───
            await RenderMonitors(info.Monitors);

            // ─── Audio devices ───
            await Task.Run(() =>
            {
                _playbackDevices = AudioHelper.GetPlaybackDevices();
                _recordingDevices = AudioHelper.GetRecordingDevices();
            });

            Dispatcher.Invoke(() =>
            {
                LoadPlaybackDevices();
                LoadRecordingDevices();
                LoadVolumes();
            });

            // ─── Printers ───
            RenderPrinters(info.Printers);
        }
        catch (Exception ex)
        {
            LoggingService.Instance.Error("PeripheralsView", "Error refreshing peripheral info", ex.Message);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  DISPLAY MANAGEMENT
    // ═══════════════════════════════════════════════════════════

    private async Task RenderMonitors(List<MonitorInfo> monitors)
    {
        MonitorList.Children.Clear();

        foreach (var monitor in monitors)
        {
            var card = new Border
            {
                Background = (Brush)FindResource("SurfaceBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(20),
                BorderBrush = monitor.IsPrimary
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(monitor.IsPrimary ? 2 : 1),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var mainGrid = new Grid();
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            mainGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Left side: monitor info
            var infoStack = new StackPanel();

            // Header row
            var header = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 8) };
            header.Children.Add(new TextBlock
            {
                Text = "\uE7F4",
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 20,
                Foreground = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            header.Children.Add(new TextBlock
            {
                Text = monitor.Name,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            });

            if (monitor.IsPrimary)
            {
                var badge = new Border
                {
                    Background = (Brush)FindResource("AccentDimBrush"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(8, 3, 8, 3),
                    Margin = new Thickness(10, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = "Primary",
                    FontSize = 10,
                    Foreground = (Brush)FindResource("AccentHighBrush"),
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = (FontFamily)FindResource("MainFont")
                };
                header.Children.Add(badge);
            }
            infoStack.Children.Add(header);

            // Current resolution
            infoStack.Children.Add(new TextBlock
            {
                Text = $"Current: {monitor.Resolution}",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont"),
                Margin = new Thickness(0, 0, 0, 8)
            });

            Grid.SetColumn(infoStack, 0);
            mainGrid.Children.Add(infoStack);

            // Right side: resolution selector + apply
            var controlStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };

            var resLabel = new TextBlock
            {
                Text = "Resolution:",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 0, 0, 4),
                FontFamily = (FontFamily)FindResource("MainFont")
            };
            controlStack.Children.Add(resLabel);

            // Resolution combo + apply button
            var resRow = new StackPanel { Orientation = Orientation.Horizontal };

            var resCombo = new ComboBox { Width = 200, Margin = new Thickness(0, 0, 8, 0) };

            // Populate with supported modes
            var deviceName = monitor.DeviceId;
            var modes = await Task.Run(() => DisplayHelper.GetSupportedModes(deviceName));
            var currentMode = await Task.Run(() => DisplayHelper.GetCurrentMode(deviceName));

            int selectedIndex = -1;
            for (int i = 0; i < modes.Count; i++)
            {
                resCombo.Items.Add(modes[i].Label);
                if (currentMode != null && modes[i].Equals(currentMode))
                    selectedIndex = i;
            }
            if (selectedIndex >= 0) resCombo.SelectedIndex = selectedIndex;
            resCombo.Tag = new { DeviceName = deviceName, Modes = modes };

            resRow.Children.Add(resCombo);

            var applyBtn = new Button
            {
                Style = (Style)FindResource("PrimaryButton"),
                Padding = new Thickness(12, 6, 12, 6),
                Content = new TextBlock { Text = "Apply", FontSize = 12 }
            };
            applyBtn.Tag = resCombo;
            applyBtn.Click += ApplyResolution_Click;
            resRow.Children.Add(applyBtn);

            controlStack.Children.Add(resRow);
            Grid.SetColumn(controlStack, 1);
            mainGrid.Children.Add(controlStack);

            card.Child = mainGrid;
            MonitorList.Children.Add(card);
        }

        if (monitors.Count == 0)
        {
            MonitorList.Children.Add(new TextBlock
            {
                Text = "No monitors detected",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
        }
    }

    private void ApplyResolution_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not ComboBox combo) return;

        dynamic tag = combo.Tag;
        string deviceName = tag.DeviceName;
        List<DisplayModeInfo> modes = tag.Modes;

        int selectedIdx = combo.SelectedIndex;
        if (selectedIdx < 0 || selectedIdx >= modes.Count) return;

        var mode = modes[selectedIdx];

        var answer = MessageBox.Show(
            $"Change resolution to {mode.Label}?\n\nIf the display goes black, it will revert after 15 seconds.",
            "Change Resolution",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            bool success = DisplayHelper.ChangeResolution(deviceName, mode.Width, mode.Height, mode.RefreshRate);
            ShowStatus(success
                ? $"Resolution changed to {mode.Label}"
                : "Failed to change resolution", success);
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  AUDIO MANAGEMENT
    // ═══════════════════════════════════════════════════════════

    private void LoadPlaybackDevices()
    {
        CmbAudioOutput.Items.Clear();
        int defaultIndex = -1;

        for (int i = 0; i < _playbackDevices.Count; i++)
        {
            var device = _playbackDevices[i];
            CmbAudioOutput.Items.Add(device.IsDefault ? $"✓ {device.Name}" : device.Name);
            if (device.IsDefault) defaultIndex = i;
        }

        if (defaultIndex >= 0) CmbAudioOutput.SelectedIndex = defaultIndex;
        else if (_playbackDevices.Count > 0) CmbAudioOutput.SelectedIndex = 0;
    }

    private void LoadRecordingDevices()
    {
        CmbAudioInput.Items.Clear();
        int defaultIndex = -1;

        for (int i = 0; i < _recordingDevices.Count; i++)
        {
            var device = _recordingDevices[i];
            CmbAudioInput.Items.Add(device.IsDefault ? $"✓ {device.Name}" : device.Name);
            if (device.IsDefault) defaultIndex = i;
        }

        if (defaultIndex >= 0) CmbAudioInput.SelectedIndex = defaultIndex;
        else if (_recordingDevices.Count > 0) CmbAudioInput.SelectedIndex = 0;
    }

    private void LoadVolumes()
    {
        _suppressVolumeEvents = true;

        // Output volume
        float outputVol = AudioHelper.GetMasterVolume();
        SliderOutputVolume.Value = outputVol * 100;
        TxtOutputVolume.Text = $"{(int)(outputVol * 100)}%";

        bool outputMuted = AudioHelper.GetMasterMute();
        TxtOutputMuteIcon.Text = outputMuted ? "\uE74F" : "\uE767";

        // Input volume
        float inputVol = AudioHelper.GetMicrophoneVolume();
        SliderInputVolume.Value = inputVol * 100;
        TxtInputVolume.Text = $"{(int)(inputVol * 100)}%";

        bool inputMuted = AudioHelper.GetMicrophoneMute();
        TxtInputMuteIcon.Text = inputMuted ? "\uE74F" : "\uE720";

        _suppressVolumeEvents = false;
    }

    // ─── Audio Event Handlers ────────────────────────────────────

    private void AudioOutput_Changed(object sender, SelectionChangedEventArgs e) { }
    private void AudioInput_Changed(object sender, SelectionChangedEventArgs e) { }

    private void SetDefaultPlayback_Click(object sender, RoutedEventArgs e)
    {
        int idx = CmbAudioOutput.SelectedIndex;
        if (idx < 0 || idx >= _playbackDevices.Count) return;

        var device = _playbackDevices[idx];
        bool success = AudioHelper.SetDefaultDevice(device.Id);

        if (success)
        {
            ShowStatus($"Default playback device set to: {device.Name}", true);
            LoggingService.Instance.Info("Audio", $"Default playback changed to: {device.Name}");
            // Refresh to show updated checkmark
            _playbackDevices = AudioHelper.GetPlaybackDevices();
            LoadPlaybackDevices();
        }
        else
        {
            ShowStatus("Failed to set default device. Try Windows Sound Settings.", false);
        }
    }

    private void SetDefaultRecording_Click(object sender, RoutedEventArgs e)
    {
        int idx = CmbAudioInput.SelectedIndex;
        if (idx < 0 || idx >= _recordingDevices.Count) return;

        var device = _recordingDevices[idx];
        bool success = AudioHelper.SetDefaultDevice(device.Id);

        if (success)
        {
            ShowStatus($"Default recording device set to: {device.Name}", true);
            LoggingService.Instance.Info("Audio", $"Default recording changed to: {device.Name}");
            _recordingDevices = AudioHelper.GetRecordingDevices();
            LoadRecordingDevices();
        }
        else
        {
            ShowStatus("Failed to set default device. Try Windows Sound Settings.", false);
        }
    }

    private void OutputVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressVolumeEvents) return;
        float level = (float)(SliderOutputVolume.Value / 100.0);
        AudioHelper.SetMasterVolume(level);
        TxtOutputVolume.Text = $"{(int)(level * 100)}%";
    }

    private void InputVolume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressVolumeEvents) return;
        float level = (float)(SliderInputVolume.Value / 100.0);
        AudioHelper.SetMicrophoneVolume(level);
        TxtInputVolume.Text = $"{(int)(level * 100)}%";
    }

    private void ToggleOutputMute_Click(object sender, RoutedEventArgs e)
    {
        bool currentMute = AudioHelper.GetMasterMute();
        AudioHelper.SetMasterMute(!currentMute);
        TxtOutputMuteIcon.Text = !currentMute ? "\uE74F" : "\uE767";
    }

    private void ToggleInputMute_Click(object sender, RoutedEventArgs e)
    {
        bool currentMute = AudioHelper.GetMicrophoneMute();
        AudioHelper.SetMicrophoneMute(!currentMute);
        TxtInputMuteIcon.Text = !currentMute ? "\uE74F" : "\uE720";
    }

    // ═══════════════════════════════════════════════════════════
    //  PRINTER MANAGEMENT
    // ═══════════════════════════════════════════════════════════

    private void RenderPrinters(List<PrinterInfo> printers)
    {
        PrinterList.Children.Clear();

        foreach (var printer in printers)
        {
            var card = new Border
            {
                Background = (Brush)FindResource("SurfaceBrush"),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16),
                BorderBrush = printer.IsDefault
                    ? (Brush)FindResource("AccentBrush")
                    : (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(1),
                Margin = new Thickness(0, 0, 0, 8)
            };

            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            // Printer info
            var infoStack = new StackPanel();

            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(new TextBlock
            {
                Text = "\uE7F6",
                FontFamily = (FontFamily)FindResource("IconFont"),
                FontSize = 14,
                Foreground = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            nameRow.Children.Add(new TextBlock
            {
                Text = printer.Name,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
            if (printer.IsDefault)
            {
                var badge = new Border
                {
                    Background = (Brush)FindResource("AccentDimBrush"),
                    CornerRadius = new CornerRadius(4),
                    Padding = new Thickness(6, 2, 6, 2),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center
                };
                badge.Child = new TextBlock
                {
                    Text = "Default",
                    FontSize = 9,
                    Foreground = (Brush)FindResource("AccentHighBrush"),
                    FontWeight = FontWeights.SemiBold,
                    FontFamily = (FontFamily)FindResource("MainFont")
                };
                nameRow.Children.Add(badge);
            }
            infoStack.Children.Add(nameRow);

            var detailParts = new List<string> { $"Status: {printer.Status}" };
            if (!string.IsNullOrEmpty(printer.PortName)) detailParts.Add($"Port: {printer.PortName}");
            if (printer.IsNetwork) detailParts.Add("Network");

            infoStack.Children.Add(new TextBlock
            {
                Text = string.Join("  •  ", detailParts),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(22, 2, 0, 0),
                FontFamily = (FontFamily)FindResource("MainFont")
            });

            Grid.SetColumn(infoStack, 0);
            grid.Children.Add(infoStack);

            // Action buttons
            var btnStack = new StackPanel { Orientation = Orientation.Horizontal, VerticalAlignment = VerticalAlignment.Center };

            if (!printer.IsDefault)
            {
                var setDefaultBtn = new Button
                {
                    Style = (Style)FindResource("SecondaryButton"),
                    Padding = new Thickness(10, 6, 10, 6),
                    Margin = new Thickness(0, 0, 6, 0),
                    Content = new TextBlock { Text = "Set Default", FontSize = 11 },
                    Tag = printer.Name
                };
                setDefaultBtn.Click += SetDefaultPrinter_Click;
                btnStack.Children.Add(setDefaultBtn);
            }

            var removeBtn = new Button
            {
                Style = (Style)FindResource("DangerButton"),
                Padding = new Thickness(10, 6, 10, 6),
                Content = new TextBlock { Text = "Remove", FontSize = 11, Foreground = Brushes.White },
                Tag = printer.Name
            };
            removeBtn.Click += RemovePrinter_Click;
            btnStack.Children.Add(removeBtn);

            Grid.SetColumn(btnStack, 1);
            grid.Children.Add(btnStack);

            card.Child = grid;
            PrinterList.Children.Add(card);
        }

        if (printers.Count == 0)
        {
            PrinterList.Children.Add(new TextBlock
            {
                Text = "No printers detected",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
        }
    }

    // ─── Printer Event Handlers ──────────────────────────────────

    private void AddPrinter_Click(object sender, RoutedEventArgs e)
    {
        PeripheralService.OpenAddPrinterWizard();
        ShowStatus("Add Printer wizard opened. Click Refresh after adding.", true);
    }

    private async void RemovePrinter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string printerName) return;

        var answer = MessageBox.Show(
            $"Remove printer \"{printerName}\"?",
            "Remove Printer",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (answer == MessageBoxResult.Yes)
        {
            ShowStatus($"Removing printer: {printerName}...");
            var result = await PeripheralService.RemovePrinterAsync(printerName);
            ShowStatus(result.Message, result.Success);
            if (result.Success) await RefreshAsync();
        }
    }

    private async void SetDefaultPrinter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string printerName) return;

        ShowStatus($"Setting default: {printerName}...");
        var result = await PeripheralService.SetDefaultPrinterAsync(printerName);
        ShowStatus(result.Message, result.Success);
        if (result.Success) await RefreshAsync();
    }

    // ═══════════════════════════════════════════════════════════
    //  SETTINGS LAUNCHERS
    // ═══════════════════════════════════════════════════════════

    private void OpenDisplaySettings_Click(object sender, RoutedEventArgs e)
        => PeripheralService.OpenDisplaySettings();

    private void OpenSoundSettings_Click(object sender, RoutedEventArgs e)
        => PeripheralService.OpenSoundSettings();

    private async void RestartAudio_Click(object sender, RoutedEventArgs e)
    {
        BtnRestartAudio.IsEnabled = false;
        ShowStatus("Restarting audio service...");
        var result = await PeripheralService.RestartAudioServiceAsync();
        ShowStatus(result.Message, result.Success);
        BtnRestartAudio.IsEnabled = true;
        await RefreshAsync();
    }

    private void OpenPrinterSettings_Click(object sender, RoutedEventArgs e)
        => PeripheralService.OpenPrinterSettings();

    private async void Refresh_Click(object sender, RoutedEventArgs e)
        => await RefreshAsync();

    // ═══════════════════════════════════════════════════════════
    //  HELPERS
    // ═══════════════════════════════════════════════════════════

    private void ShowStatus(string message, bool? success = null)
    {
        StatusBanner.Visibility = Visibility.Visible;
        StatusText.Text = message;

        if (success.HasValue)
        {
            StatusText.Foreground = success.Value
                ? (Brush)FindResource("SuccessBrush")
                : (Brush)FindResource("ErrorBrush");
        }
        else
        {
            StatusText.Foreground = (Brush)FindResource("TextPrimaryBrush");
        }
    }
}
