using System;
using Windows.Foundation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace OnlyWinget.DesignSystem.Controls;

public class OnlyWingetResponsivePanel : Panel
{
    public static readonly DependencyProperty ItemMinWidthProperty =
        DependencyProperty.Register(nameof(ItemMinWidth), typeof(double), typeof(OnlyWingetResponsivePanel), new PropertyMetadata(300.0, OnPropertyChanged));

    public static readonly DependencyProperty HorizontalSpacingProperty =
        DependencyProperty.Register(nameof(HorizontalSpacing), typeof(double), typeof(OnlyWingetResponsivePanel), new PropertyMetadata(12.0, OnPropertyChanged));

    public static readonly DependencyProperty VerticalSpacingProperty =
        DependencyProperty.Register(nameof(VerticalSpacing), typeof(double), typeof(OnlyWingetResponsivePanel), new PropertyMetadata(12.0, OnPropertyChanged));

    public double ItemMinWidth
    {
        get => (double)GetValue(ItemMinWidthProperty);
        set => SetValue(ItemMinWidthProperty, value);
    }

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

    private static void OnPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is OnlyWingetResponsivePanel panel)
        {
            panel.InvalidateMeasure();
            panel.InvalidateArrange();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var visibleChildren = new System.Collections.Generic.List<UIElement>();
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                visibleChildren.Add(child);
            }
        }

        if (visibleChildren.Count == 0)
        {
            return new Size(0, 0);
        }

        double width = availableSize.Width;
        double hSpacing = HorizontalSpacing;
        double vSpacing = VerticalSpacing;

        if (double.IsInfinity(width) || width <= 0)
        {
            double totalWidth = 0;
            double maxHeight = 0;
            foreach (var child in visibleChildren)
            {
                child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                totalWidth += child.DesiredSize.Width + hSpacing;
                maxHeight = Math.Max(maxHeight, child.DesiredSize.Height);
            }
            totalWidth -= hSpacing;
            return new Size(totalWidth, maxHeight);
        }

        double itemMinWidth = ItemMinWidth;
        double colWidth = Math.Max(1.0, itemMinWidth + hSpacing);
        int maxColumns = (int)Math.Floor((width + hSpacing) / colWidth);
        int columns = Math.Max(1, Math.Min(maxColumns, visibleChildren.Count));

        double childWidth = Math.Max(0.0, (width - (columns - 1) * hSpacing) / columns);

        double totalHeight = 0;
        double currentHeight = 0;
        int colIndex = 0;

        foreach (var child in visibleChildren)
        {
            child.Measure(new Size(childWidth, double.PositiveInfinity));
            currentHeight = Math.Max(currentHeight, child.DesiredSize.Height);

            colIndex++;
            if (colIndex >= columns)
            {
                totalHeight += currentHeight + vSpacing;
                currentHeight = 0;
                colIndex = 0;
            }
        }

        if (colIndex > 0)
        {
            totalHeight += currentHeight + vSpacing;
        }

        totalHeight -= vSpacing;

        return new Size(width, totalHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var visibleChildren = new System.Collections.Generic.List<UIElement>();
        foreach (var child in Children)
        {
            if (child.Visibility != Visibility.Collapsed)
            {
                visibleChildren.Add(child);
            }
        }

        if (visibleChildren.Count == 0)
        {
            return finalSize;
        }

        double width = finalSize.Width;
        double itemMinWidth = ItemMinWidth;
        double hSpacing = HorizontalSpacing;
        double vSpacing = VerticalSpacing;

        double colWidth = Math.Max(1.0, itemMinWidth + hSpacing);
        int maxColumns = (int)Math.Floor((width + hSpacing) / colWidth);
        int columns = Math.Max(1, Math.Min(maxColumns, visibleChildren.Count));

        double childWidth = Math.Max(0.0, (width - (columns - 1) * hSpacing) / columns);

        double currentY = 0;
        double rowHeight = 0;
        int colIndex = 0;

        for (int i = 0; i < visibleChildren.Count; i++)
        {
            var child = visibleChildren[i];
            double currentX = colIndex * (childWidth + hSpacing);

            if (colIndex == 0)
            {
                rowHeight = 0;
                for (int j = i; j < Math.Min(i + columns, visibleChildren.Count); j++)
                {
                    rowHeight = Math.Max(rowHeight, visibleChildren[j].DesiredSize.Height);
                }
            }

            child.Arrange(new Rect(currentX, currentY, childWidth, rowHeight));

            colIndex++;
            if (colIndex >= columns)
            {
                currentY += rowHeight + vSpacing;
                colIndex = 0;
            }
        }

        return finalSize;
    }
}
