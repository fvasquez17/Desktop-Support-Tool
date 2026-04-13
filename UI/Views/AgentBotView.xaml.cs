using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Advanced AI Support Agent — chat interface with live system diagnostics,
/// context-aware responses, quick reply chips, auto-scan, and session memory.
/// </summary>
public partial class AgentBotView : UserControl
{
    private bool _initialized;

    public AgentBotView()
    {
        InitializeComponent();
        InputBox.TextChanged += (s, e) =>
        {
            InputPlaceholder.Visibility = string.IsNullOrEmpty(InputBox.Text)
                ? Visibility.Visible
                : Visibility.Collapsed;
        };
    }

    public void Initialize()
    {
        if (!_initialized)
        {
            _initialized = true;
            AddBotMessage(TroubleshootBot.GetGreeting());
            ShowQuickReplies(new List<string>
            {
                "Scan my system",
                "My computer is slow",
                "I can't print",
                "Outlook not working",
                "No internet"
            });
        }
    }

    // ═══════════════════════════════════════════════════════════
    //  INPUT HANDLING
    // ═══════════════════════════════════════════════════════════

    private void InputBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
        {
            e.Handled = true;
            ProcessInput();
        }
    }

    private void Send_Click(object sender, RoutedEventArgs e) => ProcessInput();
    private void ScanSystem_Click(object sender, RoutedEventArgs e) => RunAutoScan();

    private async void ProcessInput()
    {
        var input = InputBox.Text.Trim();
        if (string.IsNullOrEmpty(input)) return;

        AddUserMessage(input);
        InputBox.Text = "";
        BtnSend.IsEnabled = false;
        ClearQuickReplies();

        // Check for scan command
        if (input.Contains("scan", StringComparison.OrdinalIgnoreCase) &&
            (input.Contains("system", StringComparison.OrdinalIgnoreCase) ||
             input.Contains("my", StringComparison.OrdinalIgnoreCase) ||
             input.Contains("pc", StringComparison.OrdinalIgnoreCase) ||
             input.Contains("computer", StringComparison.OrdinalIgnoreCase)))
        {
            await DoAutoScan();
        }
        else
        {
            await DoAnalysis(input);
        }

        BtnSend.IsEnabled = true;
        InputBox.Focus();
    }

    private async void RunAutoScan()
    {
        ClearQuickReplies();
        AddUserMessage("Scan my system");
        await DoAutoScan();
    }

    // ═══════════════════════════════════════════════════════════
    //  CORE LOGIC
    // ═══════════════════════════════════════════════════════════

    private async Task DoAnalysis(string input)
    {
        var typing = AddTypingIndicator("Analyzing your issue and scanning system...");
        await Task.Delay(400);

        var response = await TroubleshootBot.AnalyzeAsync(input);

        ChatMessages.Children.Remove(typing);

        // Show system context bar if we have a snapshot
        if (response.Snapshot != null)
            AddSystemBar(response.Snapshot);

        // Show main message
        AddBotMessage(response.Message);

        // Show action buttons for primary diagnosis
        if (response.Diagnoses.Count > 0)
        {
            AddActionButtons(response.Diagnoses[0]);
        }

        // Show quick replies
        if (response.QuickReplies.Count > 0)
            ShowQuickReplies(response.QuickReplies);
    }

    private async Task DoAutoScan()
    {
        var typing = AddTypingIndicator("Running full system health scan...");

        var response = await TroubleshootBot.AutoScanAsync();

        ChatMessages.Children.Remove(typing);

        if (response.Snapshot != null)
            AddSystemBar(response.Snapshot);

        AddBotMessage(response.Message);

        if (response.QuickReplies.Count > 0)
            ShowQuickReplies(response.QuickReplies);
    }

    // ═══════════════════════════════════════════════════════════
    //  SYSTEM HEALTH BAR
    // ═══════════════════════════════════════════════════════════

    private void AddSystemBar(TroubleshootBot.SystemSnapshot snap)
    {
        var container = new Border
        {
            Margin = new Thickness(36, 4, 40, 8),
            Padding = new Thickness(14, 10, 14, 10),
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)), // darker than surface
            BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
            BorderThickness = new Thickness(1),
        };

        var grid = new Grid();
        for (int i = 0; i < 5; i++)
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        // CPU Gauge
        AddGauge(grid, 0, "CPU", snap.CpuPercent, "%");
        // RAM Gauge
        AddGauge(grid, 1, "RAM", snap.RamPercent, "%");
        // Disk Gauge
        AddGauge(grid, 2, "Disk", snap.DiskPercent, "%");
        // Network
        AddStatusDot(grid, 3, "Internet", snap.InternetConnected);
        // DNS
        AddStatusDot(grid, 4, "DNS", snap.DnsWorking);

        container.Child = grid;
        ChatMessages.Children.Add(container);
        ScrollToBottom();
    }

    private void AddGauge(Grid grid, int col, string label, double value, string unit)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };

        var valueText = new TextBlock
        {
            Text = value >= 0 ? $"{value:F0}{unit}" : "N/A",
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            FontFamily = (FontFamily)FindResource("MainFont"),
            Foreground = value switch
            {
                >= 90 => (Brush)FindResource("ErrorBrush"),
                >= 70 => (Brush)FindResource("WarningBrush"),
                _ => (Brush)FindResource("SuccessBrush")
            }
        };

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 9,
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            FontFamily = (FontFamily)FindResource("MainFont")
        };

        // Mini progress bar
        var barBg = new Border
        {
            Height = 3,
            CornerRadius = new CornerRadius(2),
            Background = (Brush)FindResource("SurfaceHighBrush"),
            Margin = new Thickness(4, 3, 4, 2)
        };
        var barFill = new Border
        {
            Height = 3,
            CornerRadius = new CornerRadius(2),
            HorizontalAlignment = HorizontalAlignment.Left,
            Width = Math.Max(0, Math.Min(value, 100)) * 0.6, // max ~60px
            Background = value switch
            {
                >= 90 => (Brush)FindResource("ErrorBrush"),
                >= 70 => (Brush)FindResource("WarningBrush"),
                _ => (Brush)FindResource("SuccessBrush")
            }
        };
        barBg.Child = barFill;

        panel.Children.Add(valueText);
        panel.Children.Add(barBg);
        panel.Children.Add(labelText);

        Grid.SetColumn(panel, col);
        grid.Children.Add(panel);
    }

    private void AddStatusDot(Grid grid, int col, string label, bool ok)
    {
        var panel = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new Thickness(4, 0, 4, 0) };

        var dotRow = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        dotRow.Children.Add(new Ellipse
        {
            Width = 8, Height = 8,
            Fill = ok ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush"),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 5, 0)
        });
        dotRow.Children.Add(new TextBlock
        {
            Text = ok ? "OK" : "FAIL",
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = ok ? (Brush)FindResource("SuccessBrush") : (Brush)FindResource("ErrorBrush"),
            FontFamily = (FontFamily)FindResource("MainFont")
        });

        var labelText = new TextBlock
        {
            Text = label,
            FontSize = 9,
            Margin = new Thickness(0, 3, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            FontFamily = (FontFamily)FindResource("MainFont")
        };

        panel.Children.Add(dotRow);
        panel.Children.Add(labelText);

        Grid.SetColumn(panel, col);
        grid.Children.Add(panel);
    }

    // ═══════════════════════════════════════════════════════════
    //  QUICK REPLY CHIPS
    // ═══════════════════════════════════════════════════════════

    private void ShowQuickReplies(List<string> replies)
    {
        ClearQuickReplies();

        var container = new WrapPanel
        {
            Margin = new Thickness(36, 4, 40, 8),
            Tag = "QuickReplies"
        };

        foreach (var reply in replies)
        {
            var chip = new Border
            {
                CornerRadius = new CornerRadius(16),
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(0, 0, 6, 6),
                Cursor = System.Windows.Input.Cursors.Hand,
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)FindResource("AccentBrush"),
                Background = Brushes.Transparent,
            };

            var text = new TextBlock
            {
                Text = reply,
                FontSize = 12,
                Foreground = (Brush)FindResource("AccentBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            };

            chip.Child = text;
            chip.Tag = reply;
            chip.MouseLeftButtonUp += QuickReply_Click;

            // Hover effect
            chip.MouseEnter += (s, e) =>
            {
                chip.Background = (Brush)FindResource("AccentBrush");
                text.Foreground = Brushes.White;
            };
            chip.MouseLeave += (s, e) =>
            {
                chip.Background = Brushes.Transparent;
                text.Foreground = (Brush)FindResource("AccentBrush");
            };

            container.Children.Add(chip);
        }

        ChatMessages.Children.Add(container);
        ScrollToBottom();
    }

    private void QuickReply_Click(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is Border chip && chip.Tag is string reply)
        {
            InputBox.Text = reply;
            ProcessInput();
        }
    }

    private void ClearQuickReplies()
    {
        var toRemove = ChatMessages.Children.OfType<WrapPanel>()
            .Where(p => p.Tag as string == "QuickReplies").ToList();
        foreach (var panel in toRemove)
            ChatMessages.Children.Remove(panel);
    }

    // ═══════════════════════════════════════════════════════════
    //  CHAT BUBBLES
    // ═══════════════════════════════════════════════════════════

    private void AddUserMessage(string text)
    {
        var container = new StackPanel
        {
            Margin = new Thickness(100, 4, 0, 4),
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var bubble = new Border
        {
            CornerRadius = new CornerRadius(14, 14, 4, 14),
            Padding = new Thickness(14, 10, 14, 10),
            Background = (Brush)FindResource("AccentBrush"),
            MaxWidth = 500
        };

        bubble.Child = new TextBlock
        {
            Text = text,
            FontSize = 13,
            Foreground = Brushes.White,
            TextWrapping = TextWrapping.Wrap,
            FontFamily = (FontFamily)FindResource("MainFont")
        };

        container.Children.Add(bubble);
        ChatMessages.Children.Add(container);
        ScrollToBottom();
    }

    private void AddBotMessage(string text, bool isSecondary = false)
    {
        var container = new StackPanel
        {
            Margin = new Thickness(0, 4, 80, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };

        // Avatar
        var avatar = new Border
        {
            Width = 28, Height = 28,
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = isSecondary
                ? (Brush)FindResource("SurfaceHighBrush")
                : new LinearGradientBrush(
                    Color.FromRgb(59, 130, 246),
                    Color.FromRgb(139, 92, 246), 45)
        };
        avatar.Child = new TextBlock
        {
            FontFamily = (FontFamily)FindResource("IconFont"),
            Text = "\uE99A", FontSize = 13,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var bubble = new Border
        {
            CornerRadius = new CornerRadius(14, 14, 14, 4),
            Padding = new Thickness(14, 10, 14, 10),
            Background = (Brush)FindResource("SurfaceHighBrush"),
            BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
            BorderThickness = new Thickness(1),
            MaxWidth = 520
        };

        // Parse markdown bold
        var textPanel = new StackPanel();
        foreach (var line in text.Split('\n'))
        {
            var tb = new TextBlock
            {
                TextWrapping = TextWrapping.Wrap,
                FontSize = 13,
                FontFamily = (FontFamily)FindResource("MainFont"),
                Margin = new Thickness(0, 1, 0, 1),
                Foreground = (Brush)FindResource("TextPrimaryBrush")
            };

            var remaining = line;
            while (remaining.Contains("**"))
            {
                int start = remaining.IndexOf("**");
                if (start > 0)
                    tb.Inlines.Add(new System.Windows.Documents.Run(remaining[..start]));
                remaining = remaining[(start + 2)..];
                int end = remaining.IndexOf("**");
                if (end < 0) break;
                tb.Inlines.Add(new System.Windows.Documents.Run(remaining[..end])
                    { FontWeight = FontWeights.Bold });
                remaining = remaining[(end + 2)..];
            }
            if (remaining.Length > 0)
                tb.Inlines.Add(new System.Windows.Documents.Run(remaining));

            if (line.TrimStart().StartsWith("•") || line.TrimStart().StartsWith("-"))
            {
                tb.Margin = new Thickness(8, 1, 0, 1);
                tb.Foreground = (Brush)FindResource("TextSecondaryBrush");
            }

            textPanel.Children.Add(tb);
        }

        bubble.Child = textPanel;
        row.Children.Add(avatar);
        row.Children.Add(bubble);
        container.Children.Add(row);
        ChatMessages.Children.Add(container);
        ScrollToBottom();
    }

    private Border AddTypingIndicator(string statusText)
    {
        var container = new StackPanel
        {
            Margin = new Thickness(0, 4, 80, 4),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        var row = new StackPanel { Orientation = Orientation.Horizontal };

        var avatar = new Border
        {
            Width = 28, Height = 28,
            CornerRadius = new CornerRadius(14),
            Margin = new Thickness(0, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Top,
            Background = new LinearGradientBrush(
                Color.FromRgb(59, 130, 246),
                Color.FromRgb(139, 92, 246), 45)
        };
        avatar.Child = new TextBlock
        {
            FontFamily = (FontFamily)FindResource("IconFont"),
            Text = "\uE99A", FontSize = 13,
            Foreground = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        var bubble = new Border
        {
            CornerRadius = new CornerRadius(14, 14, 14, 4),
            Padding = new Thickness(14, 10, 14, 10),
            Background = (Brush)FindResource("SurfaceHighBrush"),
            BorderBrush = (Brush)FindResource("AccentBrush"),
            BorderThickness = new Thickness(1)
        };

        var content = new StackPanel();
        // Dots row
        var dotsPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        for (int i = 0; i < 3; i++)
        {
            dotsPanel.Children.Add(new Ellipse
            {
                Width = 7, Height = 7,
                Fill = (Brush)FindResource("AccentBrush"),
                Margin = new Thickness(2, 0, 2, 0),
                Opacity = 0.3 + (i * 0.25)
            });
        }
        content.Children.Add(dotsPanel);

        // Status text
        content.Children.Add(new TextBlock
        {
            Text = statusText,
            FontSize = 10,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            FontFamily = (FontFamily)FindResource("MainFont")
        });

        bubble.Child = content;
        row.Children.Add(avatar);
        row.Children.Add(bubble);
        container.Children.Add(row);

        var wrapper = new Border { Child = container };
        ChatMessages.Children.Add(wrapper);
        ScrollToBottom();
        return wrapper;
    }

    // ═══════════════════════════════════════════════════════════
    //  ACTION BUTTONS
    // ═══════════════════════════════════════════════════════════

    private void AddActionButtons(TroubleshootBot.Diagnosis diagnosis)
    {
        if (diagnosis.Actions.Count == 0)
        {
            AddBotMessage("All available fixes for this issue have already been tried this session. " +
                "If the problem persists, consider restarting your PC or contacting IT support.", true);
            return;
        }

        var container = new StackPanel
        {
            Margin = new Thickness(36, 2, 80, 8),
            HorizontalAlignment = HorizontalAlignment.Left
        };

        // "Recommended Fixes" label
        container.Children.Add(new TextBlock
        {
            Text = "RECOMMENDED FIXES",
            FontSize = 9,
            FontWeight = FontWeights.Bold,
            Foreground = (Brush)FindResource("TextTertiaryBrush"),
            FontFamily = (FontFamily)FindResource("MainFont"),
            Margin = new Thickness(0, 0, 0, 4)
        });

        foreach (var action in diagnosis.Actions)
        {
            var actionBorder = new Border
            {
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 3, 0, 3),
                Background = (Brush)FindResource("SurfaceBrush"),
                BorderBrush = (Brush)FindResource("AccentBrush"),
                BorderThickness = new Thickness(1),
                Cursor = System.Windows.Input.Cursors.Hand,
                MaxWidth = 480,
                HorizontalAlignment = HorizontalAlignment.Left
            };

            var btn = new Button
            {
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(14, 10, 14, 10),
                Cursor = System.Windows.Input.Cursors.Hand,
                HorizontalContentAlignment = HorizontalAlignment.Left,
                Tag = action
            };
            btn.Click += ActionButton_Click;

            var content = new StackPanel();

            var nameRow = new StackPanel { Orientation = Orientation.Horizontal };
            nameRow.Children.Add(new TextBlock
            {
                FontFamily = (FontFamily)FindResource("IconFont"),
                Text = "\uE768", FontSize = 12,
                Foreground = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(0, 0, 8, 0)
            });
            nameRow.Children.Add(new TextBlock
            {
                Text = action.Name,
                FontSize = 13, FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource("AccentBrush"),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
            if (action.RequiresConfirmation)
            {
                nameRow.Children.Add(new TextBlock
                {
                    Text = "  🛡️ Admin",
                    FontSize = 10,
                    Foreground = (Brush)FindResource("WarningBrush"),
                    VerticalAlignment = VerticalAlignment.Center,
                    FontFamily = (FontFamily)FindResource("MainFont")
                });
            }

            content.Children.Add(nameRow);
            content.Children.Add(new TextBlock
            {
                Text = action.Description,
                FontSize = 11,
                Foreground = (Brush)FindResource("TextSecondaryBrush"),
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(20, 2, 0, 0),
                FontFamily = (FontFamily)FindResource("MainFont")
            });

            btn.Content = content;
            actionBorder.Child = btn;
            container.Children.Add(actionBorder);
        }

        ChatMessages.Children.Add(container);
        ScrollToBottom();
    }

    private async void ActionButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not TroubleshootBot.SuggestedAction action)
            return;

        if (action.RequiresConfirmation)
        {
            var answer = MessageBox.Show(
                $"Run \"{action.Name}\"?\n\n{action.Description}",
                "Confirm Action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (answer != MessageBoxResult.Yes) return;
        }

        btn.IsEnabled = false;
        ClearQuickReplies();
        AddBotMessage($"⏳ Running **{action.Name}**...");

        try
        {
            var result = await action.Execute();
            TroubleshootBot.RecordFix(action.Name);

            var icon = result.Success ? "✅" : "❌";
            AddBotMessage($"{icon} **{action.Name}**: {result.Message}");

            if (result.Success)
            {
                ShowQuickReplies(new List<string>
                {
                    "That fixed it, thanks!",
                    "Still having issues",
                    "Scan my system",
                    "I have another issue"
                });
            }
            else
            {
                AddBotMessage("The fix didn't fully succeed. You can try the next option, " +
                    "or describe what happened for further assistance.", isSecondary: true);
                ShowQuickReplies(new List<string>
                {
                    "Try the next fix",
                    "Scan my system",
                    "Contact IT support"
                });
            }
        }
        catch (Exception ex)
        {
            AddBotMessage($"❌ Error running {action.Name}: {ex.Message}");
        }

        btn.IsEnabled = true;
    }

    // ═══════════════════════════════════════════════════════════
    //  CLEAR AND SCROLL
    // ═══════════════════════════════════════════════════════════

    private void ClearChat_Click(object sender, RoutedEventArgs e)
    {
        ChatMessages.Children.Clear();
        TroubleshootBot.ClearSession();
        _initialized = false;
        Initialize();
    }

    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.Loaded, () =>
        {
            ChatScrollViewer.ScrollToEnd();
        });
    }
}
