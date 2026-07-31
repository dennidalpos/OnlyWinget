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

        var (bg, fg, border) = GetColorsForSeverity(Severity);
        BadgeBorder.Background = new SolidColorBrush(bg);
        BadgeBorder.BorderBrush = new SolidColorBrush(border);
        BadgeText.Foreground = new SolidColorBrush(fg);
        BadgeIcon.Foreground = new SolidColorBrush(fg);
    }

    private static (Color bg, Color fg, Color border) GetColorsForSeverity(BadgeSeverity severity) => severity switch
    {
        BadgeSeverity.Success => (Color.FromArgb(30, 16, 124, 65), Color.FromArgb(255, 16, 124, 65), Color.FromArgb(80, 16, 124, 65)),
        BadgeSeverity.Info => (Color.FromArgb(30, 0, 120, 212), Color.FromArgb(255, 0, 120, 212), Color.FromArgb(80, 0, 120, 212)),
        BadgeSeverity.Warning => (Color.FromArgb(35, 200, 140, 0), Color.FromArgb(255, 180, 125, 0), Color.FromArgb(90, 200, 140, 0)),
        BadgeSeverity.Error => (Color.FromArgb(30, 216, 59, 1), Color.FromArgb(255, 216, 59, 1), Color.FromArgb(80, 216, 59, 1)),
        _ => (Color.FromArgb(25, 128, 128, 128), Color.FromArgb(230, 140, 140, 140), Color.FromArgb(50, 128, 128, 128))
    };
}
