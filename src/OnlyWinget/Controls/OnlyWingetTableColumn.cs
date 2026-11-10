using Microsoft.UI.Xaml;

namespace OnlyWinget.Controls;

public sealed class OnlyWingetTableColumn : DependencyObject
{
    public string Header { get; set; } = string.Empty;
    public string BindingPath { get; set; } = string.Empty;
    public GridLength Width { get; set; } = new(160);
    public bool IsPrimary { get; set; }
    public bool IsTextSelectable { get; set; }
    public bool IsManuallyResized { get; set; }
}
