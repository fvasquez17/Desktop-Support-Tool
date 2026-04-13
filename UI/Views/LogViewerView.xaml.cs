using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DesktopSupportTool.Helpers;
using DesktopSupportTool.Models;
using DesktopSupportTool.Services;

namespace DesktopSupportTool.UI.Views;

/// <summary>
/// Log viewer — displays all application log entries with search and filter.
/// </summary>
public partial class LogViewerView : UserControl
{
    private List<LogEntry> _allEntries = new();

    public LogViewerView()
    {
        InitializeComponent();

        // Populate level filter dropdown
        LevelFilter.Items.Add("All");
        LevelFilter.Items.Add("Info");
        LevelFilter.Items.Add("Success");
        LevelFilter.Items.Add("Warning");
        LevelFilter.Items.Add("Error");
        LevelFilter.SelectedIndex = 0;

        // Subscribe to live log updates
        LoggingService.Instance.LogAdded += OnLogAdded;
    }

    /// <summary>
    /// Refreshes the log from the service.
    /// </summary>
    public void RefreshLog()
    {
        _allEntries = LoggingService.Instance.GetEntries();
        ApplyFilters();
    }

    private void OnLogAdded(LogEntry entry)
    {
        Dispatcher.BeginInvoke(() =>
        {
            _allEntries.Add(entry);
            ApplyFilters();
        });
    }

    // ─── Filtering ───────────────────────────────────────────────

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void LevelFilter_Changed(object sender, SelectionChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void ApplyFilters()
    {
        var search = SearchBox?.Text?.Trim() ?? "";
        var levelText = LevelFilter?.SelectedItem?.ToString() ?? "All";

        IEnumerable<LogEntry> filtered = _allEntries;

        // Level filter
        if (levelText != "All" && Enum.TryParse<LogLevel>(levelText, out var level))
        {
            filtered = filtered.Where(e => e.Level == level);
        }

        // Search filter
        if (!string.IsNullOrEmpty(search))
        {
            filtered = filtered.Where(e =>
                e.Message.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                e.Details.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        RenderLogEntries(filtered.OrderByDescending(e => e.Timestamp).ToList());
    }

    private void RenderLogEntries(List<LogEntry> entries)
    {
        LogList.Children.Clear();

        if (entries.Count == 0)
        {
            LogList.Children.Add(new TextBlock
            {
                Text = "No log entries match the current filter",
                FontSize = 13,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 8, 0, 0),
                FontFamily = (FontFamily)FindResource("MainFont")
            });
            return;
        }

        // Limit display to most recent 500 for performance
        var displayed = entries.Take(500).ToList();

        foreach (var entry in displayed)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 1) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });  // dot
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });  // time
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(60) });  // level
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(100) }); // category
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }); // message

            // Bottom border for row separation
            var border = new Border
            {
                BorderBrush = (Brush)FindResource("BorderSubtleBrush"),
                BorderThickness = new Thickness(0, 0, 0, 0.5),
                Padding = new Thickness(4, 6, 4, 6)
            };

            var innerGrid = new Grid();
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(14) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(70) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(110) });
            innerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Status dot
            var dot = new System.Windows.Shapes.Ellipse
            {
                Width = 6, Height = 6,
                VerticalAlignment = VerticalAlignment.Center,
                Fill = GetLevelBrush(entry.Level)
            };
            Grid.SetColumn(dot, 0);
            innerGrid.Children.Add(dot);

            // Timestamp
            var time = new TextBlock
            {
                Text = entry.Timestamp.ToString("HH:mm:ss"),
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = new FontFamily("Consolas, Courier New")
            };
            Grid.SetColumn(time, 1);
            innerGrid.Children.Add(time);

            // Level
            var levelLabel = new TextBlock
            {
                Text = entry.Level.ToString().ToUpper(),
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = GetLevelBrush(entry.Level),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            };
            Grid.SetColumn(levelLabel, 2);
            innerGrid.Children.Add(levelLabel);

            // Category
            var category = new TextBlock
            {
                Text = entry.Category,
                FontSize = 11,
                Foreground = (Brush)FindResource("AccentBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            };
            Grid.SetColumn(category, 3);
            innerGrid.Children.Add(category);

            // Message
            var message = new TextBlock
            {
                Text = entry.Message,
                FontSize = 12,
                Foreground = (Brush)FindResource("TextPrimaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                FontFamily = (FontFamily)FindResource("MainFont"),
                ToolTip = string.IsNullOrEmpty(entry.Details)
                    ? entry.Message
                    : $"{entry.Message}\n\n{entry.Details}"
            };
            Grid.SetColumn(message, 4);
            innerGrid.Children.Add(message);

            border.Child = innerGrid;
            LogList.Children.Add(border);
        }

        // Show count
        if (entries.Count > 500)
        {
            LogList.Children.Add(new TextBlock
            {
                Text = $"Showing 500 of {entries.Count} entries",
                FontSize = 11,
                Foreground = (Brush)FindResource("TextTertiaryBrush"),
                Margin = new Thickness(0, 8, 0, 0),
                HorizontalAlignment = HorizontalAlignment.Center,
                FontFamily = (FontFamily)FindResource("MainFont")
            });
        }
    }

    // ─── Button Handlers ─────────────────────────────────────────

    private void ExportDiagnostics_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var path = LoggingService.Instance.ExportDiagnostics();
            MessageBox.Show(
                $"Diagnostics exported to:\n{path}",
                "Export Complete",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            // Open the file
            ProcessHelper.Launch(path);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Error exporting diagnostics:\n{ex.Message}",
                "Export Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void OpenLogFolder_Click(object sender, RoutedEventArgs e)
    {
        var logDir = LoggingService.Instance.GetLogDirectory();
        ProcessHelper.OpenInExplorer(logDir);
    }

    private void Refresh_Click(object sender, RoutedEventArgs e)
    {
        RefreshLog();
    }

    // ─── Helpers ─────────────────────────────────────────────────

    private Brush GetLevelBrush(LogLevel level)
    {
        return level switch
        {
            LogLevel.Error => (Brush)FindResource("ErrorBrush"),
            LogLevel.Warning => (Brush)FindResource("WarningBrush"),
            LogLevel.Success => (Brush)FindResource("SuccessBrush"),
            _ => (Brush)FindResource("TextTertiaryBrush"),
        };
    }
}
