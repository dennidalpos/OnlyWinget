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
}
