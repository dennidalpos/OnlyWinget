using System;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.DesignSystem.Controls;

public class OnlyWingetWrapPanel : Panel
{
    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double), typeof(OnlyWingetWrapPanel), new PropertyMetadata(8.0, OnSpacingChanged));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(nameof(VerticalSpacing), typeof(double), typeof(OnlyWingetWrapPanel), new PropertyMetadata(8.0, OnSpacingChanged));

    public double HorizontalSpacing
    {
        get => (double)GetValue(HorizontalSpacingProperty);
        set => SetValue(HorizontalSpacingProperty, value);
    }

    public double VerticalSpacing
    {
        get => (double)GetValue(VerticalSpacingProperty);
        set => SetValue(VerticalSpacingProperty, value);
    }

    private static void OnSpacingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OnlyWingetWrapPanel panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double horizontalSpacing = HorizontalSpacing;
        double verticalSpacing = VerticalSpacing;

        double currentX = 0;
        double currentY = 0;
        double rowHeight = 0;
        double maxWidth = 0;

        foreach (var child in Children)
        {
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            Size childSize = child.DesiredSize;

            if (currentX + childSize.Width > availableSize.Width && currentX > 0)
            {
                // Wrap to next row
                currentX = 0;
                currentY += rowHeight + verticalSpacing;
                rowHeight = 0;
            }

            rowHeight = Math.Max(rowHeight, childSize.Height);
            currentX += childSize.Width + horizontalSpacing;
            maxWidth = Math.Max(maxWidth, currentX - horizontalSpacing);
        }

        double finalHeight = currentY + rowHeight;
        return new Size(
            double.IsInfinity(availableSize.Width) ? maxWidth : Math.Min(availableSize.Width, maxWidth),
            double.IsInfinity(availableSize.Height) ? finalHeight : Math.Min(availableSize.Height, finalHeight));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double horizontalSpacing = HorizontalSpacing;
        double verticalSpacing = VerticalSpacing;

        double currentX = 0;
        double currentY = 0;
        double rowHeight = 0;

        foreach (var child in Children)
        {
            Size childSize = child.DesiredSize;

            if (currentX + childSize.Width > finalSize.Width && currentX > 0)
            {
                // Wrap to next row
                currentX = 0;
                currentY += rowHeight + verticalSpacing;
                rowHeight = 0;
            }

            child.Arrange(new Rect(currentX, currentY, childSize.Width, childSize.Height));
            rowHeight = Math.Max(rowHeight, childSize.Height);
            currentX += childSize.Width + horizontalSpacing;
        }

        return finalSize;
    }
}
