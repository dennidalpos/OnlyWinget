using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.DesignSystem.States;

public sealed partial class StatePresenter : UserControl
{
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(StatePresenter), new PropertyMetadata(false));
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(StatePresenter), new PropertyMetadata(false));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatePresenter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(StatePresenter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DetailsProperty = DependencyProperty.Register(nameof(Details), typeof(string), typeof(StatePresenter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(nameof(Severity), typeof(InfoBarSeverity), typeof(StatePresenter), new PropertyMetadata(InfoBarSeverity.Informational));

    public StatePresenter() => InitializeComponent();

    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public string Details { get => (string)GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public InfoBarSeverity Severity { get => (InfoBarSeverity)GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }
    public Visibility DetailsVisibility => string.IsNullOrWhiteSpace(Details) ? Visibility.Collapsed : Visibility.Visible;

    public void ShowEmpty(string message)
    {
        Title = string.Empty;
        Message = message;
        Details = string.Empty;
        Severity = InfoBarSeverity.Informational;
        IsLoading = false;
        IsOpen = true;
        Bindings.Update();
    }

    public void ShowError(string message, string? details = null)
    {
        Title = TextResources.Get("State_Error");
        Message = message;
        Details = details ?? string.Empty;
        Severity = InfoBarSeverity.Error;
        IsLoading = false;
        IsOpen = true;
        Bindings.Update();
    }

    public void Hide()
    {
        IsOpen = false;
        Bindings.Update();
    }
}
