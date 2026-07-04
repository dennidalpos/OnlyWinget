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

    public static Microsoft.UI.Xaml.Media.Brush StatusBrush(bool succeeded)
    {
        var resourceKey = succeeded ? "SystemFillColorSuccessBrush" : "SystemFillColorCriticalBrush";
        if (Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(resourceKey, out var brush))
        {
            if (brush is Microsoft.UI.Xaml.Media.Brush b)
            {
                return b;
            }
        }
        return new Microsoft.UI.Xaml.Media.SolidColorBrush(succeeded ? Microsoft.UI.Colors.Green : Microsoft.UI.Colors.Red);
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
}
