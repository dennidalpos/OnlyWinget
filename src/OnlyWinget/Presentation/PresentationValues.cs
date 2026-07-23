using Microsoft.UI.Xaml;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Presentation;

public static class PresentationValues
{
    public static Visibility Visibility(bool value) => value ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

    public static bool? HeaderSelection(SelectionHeaderState state) => state switch
    {
        SelectionHeaderState.Checked => true,
        SelectionHeaderState.Mixed => null,
        _ => false
    };

    public static Visibility Selected(object? selectedItem, object item) =>
        Visibility(ReferenceEquals(selectedItem, item));

    public static Visibility NotSelected(object? selectedItem, object item) =>
        Visibility(!ReferenceEquals(selectedItem, item));

    public static Microsoft.UI.Xaml.Media.Brush StatusBrush(bool succeeded, bool isWarning)
    {
        var resourceKey = isWarning ? "SystemFillColorCautionBrush" : (succeeded ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush");
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var brush))
        {
            if (brush is Microsoft.UI.Xaml.Media.Brush b)
            {
                return b;
            }
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(isWarning ? Microsoft.UI.Colors.Orange : (succeeded ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Red));
    }

    public static Visibility HasErrorDetails(string? errorDetails) =>
        string.IsNullOrWhiteSpace(errorDetails) ? Microsoft.UI.Xaml.Visibility.Collapsed : Microsoft.UI.Xaml.Visibility.Visible;

    public static Microsoft.UI.Xaml.Media.Brush GetSeverityBrush(string resourceKey, Windows.UI.Color fallbackColor)
    {
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var brush))
        {
            if (brush is Microsoft.UI.Xaml.Media.Brush b)
            {
                return b;
            }
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(fallbackColor);
    }

    public static string OperationalGlyph(bool hasWarning, bool isBusy)
    {
        return hasWarning
            ? "\uE7BA"
            : (isBusy ? "\uE895" : "\uE930");
    }

    public static Microsoft.UI.Xaml.Media.Brush OperationalForeground(bool hasWarning, bool isBusy)
    {
        var resourceKey = hasWarning
            ? "SystemFillColorCautionBrush"
            : (isBusy ? "SystemFillColorAttentionBrush" : "SystemFillColorSuccessBrush");
        var fallback = hasWarning ? Microsoft.UI.Colors.Orange : (isBusy ? Microsoft.UI.Colors.Blue : Microsoft.UI.Colors.Green);
        return GetSeverityBrush(resourceKey, fallback);
    }

    public static string FormatFilterPlaceholder(string columnHeaderKey)
    {
        return string.Format(
            System.Globalization.CultureInfo.CurrentCulture,
            TextResources.Get("Filter_Column_Placeholder"),
            TextResources.Get(columnHeaderKey));
    }

    public static Microsoft.UI.Xaml.Media.Brush SeverityBrush(OnlyWinget.Application.Activity.ActivitySeverity severity) =>
        SeverityBrush(severity.ToString());

    public static Microsoft.UI.Xaml.Media.Brush SeverityBrush(string severity)
    {
        return severity switch
        {
            "Information" => GetSeverityBrush("SystemFillColorAttentionBrush", Microsoft.UI.Colors.DodgerBlue),
            "Success" => GetSeverityBrush("SystemFillColorSuccessBrush", Microsoft.UI.Colors.Green),
            "Warning" => GetSeverityBrush("SystemFillColorCautionBrush", Microsoft.UI.Colors.Orange),
            "Error" => GetSeverityBrush("SystemFillColorCriticalBrush", Microsoft.UI.Colors.Red),
            _ => GetSeverityBrush("TextFillColorSecondaryBrush", Microsoft.UI.Colors.Gray)
        };
    }
}

