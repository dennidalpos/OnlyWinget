using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace OnlyWinget.DesignSystem.Controls;

public sealed partial class PageScaffold : UserControl
{
    public static readonly DependencyProperty TitleProperty = DependencyProperty.Register(
        nameof(Title), typeof(string), typeof(PageScaffold), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty SubtitleProperty = DependencyProperty.Register(
        nameof(Subtitle), typeof(string), typeof(PageScaffold), new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty AreaAccentBrushProperty = DependencyProperty.Register(
        nameof(AreaAccentBrush), typeof(Brush), typeof(PageScaffold), new PropertyMetadata(null, OnAreaAccentBrushChanged));
    public static readonly DependencyProperty CommandsProperty = DependencyProperty.Register(
        nameof(Commands), typeof(UIElement), typeof(PageScaffold), new PropertyMetadata(null));
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(
        nameof(State), typeof(UIElement), typeof(PageScaffold), new PropertyMetadata(null));
    public static readonly DependencyProperty BodyProperty = DependencyProperty.Register(
        nameof(Body), typeof(UIElement), typeof(PageScaffold), new PropertyMetadata(null));
    public static readonly DependencyProperty FooterProperty = DependencyProperty.Register(
        nameof(Footer), typeof(UIElement), typeof(PageScaffold), new PropertyMetadata(null));

    public PageScaffold()
    {
        InitializeComponent();
        Loaded += (_, _) => UpdateAccentBar();
    }

    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Subtitle { get => (string)GetValue(SubtitleProperty); set => SetValue(SubtitleProperty, value); }
    public Brush? AreaAccentBrush { get => (Brush?)GetValue(AreaAccentBrushProperty); set => SetValue(AreaAccentBrushProperty, value); }
    public UIElement? Commands { get => (UIElement?)GetValue(CommandsProperty); set => SetValue(CommandsProperty, value); }
    public UIElement? State { get => (UIElement?)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public UIElement? Body { get => (UIElement?)GetValue(BodyProperty); set => SetValue(BodyProperty, value); }
    public UIElement? Footer { get => (UIElement?)GetValue(FooterProperty); set => SetValue(FooterProperty, value); }

    private static void OnAreaAccentBrushChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        ((PageScaffold)d).UpdateAccentBar();
    }

    private void UpdateAccentBar()
    {
        if (AccentBar is not null)
        {
            AccentBar.Visibility = AreaAccentBrush is not null ? Visibility.Visible : Visibility.Collapsed;
        }
    }
}
