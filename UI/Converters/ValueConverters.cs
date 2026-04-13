using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace DesktopSupportTool.UI.Converters;

/// <summary>
/// Converts a boolean to a Visibility value. True = Visible, False = Collapsed.
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        // If parameter is "Invert", reverse the logic
        if (parameter?.ToString() == "Invert")
            boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => value is Visibility v && v == Visibility.Visible;
}

/// <summary>
/// Converts a percentage (0-100) to a width relative to a given max width.
/// Used for custom progress bars within cards.
/// </summary>
public class PercentToWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length >= 2
            && values[0] is double percent
            && values[1] is double maxWidth)
        {
            return Math.Max(0, Math.Min(maxWidth, maxWidth * percent / 100.0));
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a percentage value to a status color brush.
/// Green (0-60), Amber (60-85), Red (85-100).
/// </summary>
public class PercentToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        double pct = value is double d ? d : 0;

        // For "invert" param (e.g., disk free space where low = bad)
        if (parameter?.ToString() == "Invert")
            pct = 100 - pct;

        if (pct >= 85) return Application.Current.FindResource("ErrorBrush");
        if (pct >= 60) return Application.Current.FindResource("WarningBrush");
        return Application.Current.FindResource("SuccessBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a boolean (healthy/unhealthy) to a status color brush.
/// </summary>
public class BoolToStatusColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool healthy = value is bool b && b;
        return healthy
            ? Application.Current.FindResource("SuccessBrush")
            : Application.Current.FindResource("ErrorBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a LogLevel enum to a colored brush.
/// </summary>
public class LogLevelToBrushConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is Models.LogLevel level)
        {
            return level switch
            {
                Models.LogLevel.Error => Application.Current.FindResource("ErrorBrush"),
                Models.LogLevel.Warning => Application.Current.FindResource("WarningBrush"),
                Models.LogLevel.Success => Application.Current.FindResource("SuccessBrush"),
                _ => Application.Current.FindResource("TextSecondaryBrush"),
            };
        }
        return Application.Current.FindResource("TextSecondaryBrush");
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// Converts a TimeSpan to a human-readable uptime string.
/// </summary>
public class UptimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is TimeSpan ts)
        {
            if (ts.TotalDays >= 1)
                return $"{(int)ts.TotalDays}d {ts.Hours}h {ts.Minutes}m";
            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h {ts.Minutes}m";
            return $"{ts.Minutes}m";
        }
        return "N/A";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
