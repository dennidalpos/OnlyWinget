using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Presentation;

namespace OnlyWinget.DesignSystem.States;

public sealed partial class StatePresenter : UserControl
{
    public event EventHandler? ActionRequested;

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
    public Visibility ActionVisibility => string.IsNullOrWhiteSpace(ActionButton.Content?.ToString()) ? Visibility.Collapsed : Visibility.Visible;

    public void Present(FeatureState state)
    {
        IsOpen = state.Kind != FeatureStateKind.Ready;
        IsLoading = state.Kind is FeatureStateKind.Loading or FeatureStateKind.Executing;
        Title = state.Kind switch
        {
            FeatureStateKind.Error => TextResources.Get("State_Error"),
            FeatureStateKind.Unavailable => TextResources.Get("State_Unavailable"),
            _ => string.Empty
        };
        Message = state.Message;
        Details = state.Details ?? string.Empty;
        Severity = state.Kind switch
        {
            FeatureStateKind.Error => InfoBarSeverity.Error,
            FeatureStateKind.Unavailable => InfoBarSeverity.Warning,
            _ => InfoBarSeverity.Informational
        };
        ActionButton.Content = string.IsNullOrWhiteSpace(state.ActionResourceKey)
            ? string.Empty
            : TextResources.Get(state.ActionResourceKey);
        DetailsExpander.Header = TextResources.Get("State_TechnicalDetails");
        Bindings.Update();
    }

    private void OnAction(object sender, RoutedEventArgs args) => ActionRequested?.Invoke(this, EventArgs.Empty);
}
