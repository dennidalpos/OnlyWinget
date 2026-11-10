using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace OnlyWinget.Controls;

public enum BadgeSeverity
{
    Neutral,
    Info,
    Success,
    Warning,
    Error
}

public sealed partial class StatusBadgeControl : UserControl
{
    public static readonly DependencyProperty TextProperty =
        DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadgeControl), new PropertyMetadata(string.Empty, OnPropertyChanged));

    public static readonly DependencyProperty GlyphProperty =
        DependencyProperty.Register(nameof(Glyph), typeof(string), typeof(StatusBadgeControl), new PropertyMetadata(string.Empty, OnPropertyChanged));

    public static readonly DependencyProperty SeverityProperty =
        DependencyProperty.Register(nameof(Severity), typeof(BadgeSeverity), typeof(StatusBadgeControl), new PropertyMetadata(BadgeSeverity.Neutral, OnPropertyChanged));

    public string Text
    {
        get => (string)GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public BadgeSeverity Severity
    {
        get => (BadgeSeverity)GetValue(SeverityProperty);
        set => SetValue(SeverityProperty, value);
    }

    public StatusBadgeControl()
    {
        InitializeComponent();
        UpdateVisuals();
    }

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is StatusBadgeControl badge)
        {
            badge.UpdateVisuals();
        }
    }

    private void UpdateVisuals()
    {
        BadgeText.Text = Text;
        if (!string.IsNullOrWhiteSpace(Glyph))
        {
            BadgeIcon.Glyph = Glyph;
            BadgeIcon.Visibility = Visibility.Visible;
        }
        else
        {
            BadgeIcon.Visibility = Visibility.Collapsed;
        }

        ApplySeverityTheme(Severity);
    }

    private void ApplySeverityTheme(BadgeSeverity severity)
    {
        switch (severity)
        {
            case BadgeSeverity.Error:
                var criticalBrush = GetThemeBrush("SystemFillColorCriticalBrush");
                BadgeBorder.ClearValue(Border.BackgroundProperty);
                BadgeBorder.BorderBrush = criticalBrush;
                BadgeText.Foreground = criticalBrush;
                BadgeIcon.Foreground = criticalBrush;
                break;
            case BadgeSeverity.Warning:
                var attentionBrush = GetThemeBrush("SystemFillColorAttentionBrush");
                BadgeBorder.ClearValue(Border.BackgroundProperty);
                BadgeBorder.BorderBrush = attentionBrush;
                BadgeText.Foreground = attentionBrush;
                BadgeIcon.Foreground = attentionBrush;
                break;
            case BadgeSeverity.Success:
            case BadgeSeverity.Info:
            case BadgeSeverity.Neutral:
            default:
                BadgeBorder.ClearValue(Border.BackgroundProperty);
                BadgeBorder.ClearValue(Border.BorderBrushProperty);
                BadgeText.ClearValue(TextBlock.ForegroundProperty);
                BadgeIcon.ClearValue(FontIcon.ForegroundProperty);
                break;
        }
    }

    private static Brush GetThemeBrush(string key)
    {
        if (global::Microsoft.UI.Xaml.Application.Current?.Resources != null &&
            global::Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var resource) &&
            resource is Brush brush)
        {
            return brush;
        }
        return new SolidColorBrush(Microsoft.UI.Colors.Transparent);
    }
}
