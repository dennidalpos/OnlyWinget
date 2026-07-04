using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Presentation;

namespace OnlyWinget.DesignSystem.States;

public sealed partial class StatePresenter : UserControl
{
    public event EventHandler? ActionRequested;
    public event EventHandler? CancelRequested;

    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(nameof(IsOpen), typeof(bool), typeof(StatePresenter), new PropertyMetadata(false));
    public static readonly DependencyProperty IsLoadingProperty = DependencyProperty.Register(nameof(IsLoading), typeof(bool), typeof(StatePresenter), new PropertyMetadata(false));
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(nameof(Title), typeof(string), typeof(StatePresenter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty MessageProperty = DependencyProperty.Register(nameof(Message), typeof(string), typeof(StatePresenter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty DetailsProperty = DependencyProperty.Register(nameof(Details), typeof(string), typeof(StatePresenter), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SeverityProperty = DependencyProperty.Register(nameof(Severity), typeof(InfoBarSeverity), typeof(StatePresenter), new PropertyMetadata(InfoBarSeverity.Informational));
    public static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(nameof(Progress), typeof(double), typeof(StatePresenter), new PropertyMetadata(0.0));
    public static readonly DependencyProperty IsIndeterminateProperty = DependencyProperty.Register(nameof(IsIndeterminate), typeof(bool), typeof(StatePresenter), new PropertyMetadata(true));
    public static readonly DependencyProperty CanCancelProperty = DependencyProperty.Register(nameof(CanCancel), typeof(bool), typeof(StatePresenter), new PropertyMetadata(false));

    public StatePresenter()
    {
        InitializeComponent();
        CancelButton.Content = TextResources.Get("Command_Operation_Cancel");
    }

    public bool IsOpen { get => (bool)GetValue(IsOpenProperty); set => SetValue(IsOpenProperty, value); }
    public bool IsLoading { get => (bool)GetValue(IsLoadingProperty); set => SetValue(IsLoadingProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public string Details { get => (string)GetValue(DetailsProperty); set => SetValue(DetailsProperty, value); }
    public InfoBarSeverity Severity { get => (InfoBarSeverity)GetValue(SeverityProperty); set => SetValue(SeverityProperty, value); }
    public double Progress { get => (double)GetValue(ProgressProperty); set => SetValue(ProgressProperty, value); }
    public bool IsIndeterminate { get => (bool)GetValue(IsIndeterminateProperty); set => SetValue(IsIndeterminateProperty, value); }
    public bool CanCancel { get => (bool)GetValue(CanCancelProperty); set => SetValue(CanCancelProperty, value); }

    public Visibility CardVisibility => IsOpen ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ProgressRingVisibility => (IsLoading && IsIndeterminate) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility FontIconVisibility => !(IsLoading && IsIndeterminate) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility TitleVisibility => string.IsNullOrWhiteSpace(Title) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility ProgressBarVisibility => IsLoading ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActionsVisibility => (CanCancel || ActionButtonVisibility == Visibility.Visible) ? Visibility.Visible : Visibility.Collapsed;
    public Visibility ActionButtonVisibility => string.IsNullOrWhiteSpace(ActionButton?.Content?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    public Visibility CancelButtonVisibility => CanCancel ? Visibility.Visible : Visibility.Collapsed;
    public Visibility DetailsVisibility => string.IsNullOrWhiteSpace(Details) ? Visibility.Collapsed : Visibility.Visible;

    public string IconGlyph => Severity switch
    {
        InfoBarSeverity.Error => "\uE783",
        InfoBarSeverity.Warning => "\uE7BA",
        InfoBarSeverity.Success => "\uE930",
        _ => "\uE946"
    };

    public Microsoft.UI.Xaml.Media.Brush IconForeground => Severity switch
    {
        InfoBarSeverity.Error => GetSeverityBrush("SystemFillColorCriticalBrush", Microsoft.UI.Colors.Red),
        InfoBarSeverity.Warning => GetSeverityBrush("SystemFillColorCautionBrush", Microsoft.UI.Colors.Orange),
        InfoBarSeverity.Success => GetSeverityBrush("SystemFillColorSuccessBrush", Microsoft.UI.Colors.Green),
        _ => GetSeverityBrush("SystemFillColorAttentionBrush", Microsoft.UI.Colors.Blue)
    };

    private Microsoft.UI.Xaml.Media.Brush GetSeverityBrush(string resourceKey, Windows.UI.Color fallbackColor)
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

    public void Present(FeatureState state)
    {
        IsOpen = state.Kind != FeatureStateKind.Ready;
        IsLoading = state.Kind is FeatureStateKind.Loading or FeatureStateKind.Executing;
        IsIndeterminate = true;
        Progress = 0;
        CanCancel = false;
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

    public void Show(string title, string message, string? detail = null, double? progress = null, bool canCancel = false)
    {
        IsOpen = true;
        IsLoading = true;
        Title = title;
        Message = message;
        Details = detail ?? string.Empty;
        Progress = progress ?? 0;
        IsIndeterminate = progress is null;
        CanCancel = canCancel;
        Severity = InfoBarSeverity.Informational;
        ActionButton.Content = string.Empty;
        DetailsExpander.Header = TextResources.Get("State_TechnicalDetails");
        Bindings.Update();
    }

    public void Complete(string message, bool failed = false)
    {
        IsOpen = true;
        IsLoading = false;
        Title = string.Empty;
        Message = message;
        Details = string.Empty;
        Progress = failed ? 0 : 100;
        IsIndeterminate = false;
        CanCancel = false;
        Severity = failed ? InfoBarSeverity.Error : InfoBarSeverity.Success;
        ActionButton.Content = string.Empty;
        Bindings.Update();
    }

    public void Hide()
    {
        IsOpen = false;
        Bindings.Update();
    }

    public void ShowUndo(string message, string actionText)
    {
        IsOpen = true;
        IsLoading = false;
        Title = string.Empty;
        Message = message;
        Details = string.Empty;
        Progress = 0;
        IsIndeterminate = false;
        CanCancel = false;
        Severity = InfoBarSeverity.Informational;
        ActionButton.Content = actionText;
        Bindings.Update();
    }

    private void OnAction(object sender, RoutedEventArgs args) => ActionRequested?.Invoke(this, EventArgs.Empty);
    private void OnCancel(object sender, RoutedEventArgs args) => CancelRequested?.Invoke(this, EventArgs.Empty);
}
