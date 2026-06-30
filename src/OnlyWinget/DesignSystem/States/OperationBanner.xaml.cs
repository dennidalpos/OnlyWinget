using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.DesignSystem.States;

public sealed partial class OperationBanner : UserControl
{
    public event EventHandler? CancelRequested;

    public OperationBanner()
    {
        InitializeComponent();
        CancelButton.Content = TextResources.Get("Command_Operation_Cancel");
    }

    public bool IsOpen { get; private set; }
    public bool IsIndeterminate { get; private set; } = true;
    public bool CanCancel { get; private set; }
    public double Progress { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string Detail { get; private set; } = string.Empty;
    public InfoBarSeverity Severity { get; private set; } = InfoBarSeverity.Informational;
    public Visibility CancelVisibility => CanCancel ? Visibility.Visible : Visibility.Collapsed;

    public void Show(string title, string message, string? detail = null, double? progress = null, bool canCancel = false)
    {
        IsOpen = true;
        Title = title;
        Message = message;
        Detail = detail ?? string.Empty;
        Progress = progress ?? 0;
        IsIndeterminate = progress is null;
        CanCancel = canCancel;
        Severity = InfoBarSeverity.Informational;
        Bindings.Update();
    }

    public void Complete(string message, bool failed = false)
    {
        IsOpen = true;
        Message = message;
        IsIndeterminate = false;
        Progress = failed ? 0 : 100;
        CanCancel = false;
        Severity = failed ? InfoBarSeverity.Error : InfoBarSeverity.Success;
        Bindings.Update();
    }

    public void Hide()
    {
        IsOpen = false;
        Bindings.Update();
    }

    private void OnCancel(object sender, RoutedEventArgs args) => CancelRequested?.Invoke(this, EventArgs.Empty);
}
