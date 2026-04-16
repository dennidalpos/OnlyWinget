// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Windows;
using System.Windows.Controls;

namespace OnlyWinget.Helpers;

public static class GridViewAutoSizeBehavior
{
    public static readonly DependencyProperty MinWidthProperty =
        DependencyProperty.RegisterAttached(
            "MinWidth",
            typeof(double),
            typeof(GridViewAutoSizeBehavior),
            new PropertyMetadata(0.0));

    public static double GetMinWidth(DependencyObject d) => (double)d.GetValue(MinWidthProperty);
    public static void SetMinWidth(DependencyObject d, double value) => d.SetValue(MinWidthProperty, value);

    public static readonly DependencyProperty StarWidthProperty =
        DependencyProperty.RegisterAttached(
            "StarWidth",
            typeof(double),
            typeof(GridViewAutoSizeBehavior),
            new PropertyMetadata(0.0));

    public static double GetStarWidth(DependencyObject d) => (double)d.GetValue(StarWidthProperty);
    public static void SetStarWidth(DependencyObject d, double value) => d.SetValue(StarWidthProperty, value);

    public static readonly DependencyProperty EnabledProperty =
        DependencyProperty.RegisterAttached(
            "Enabled",
            typeof(bool),
            typeof(GridViewAutoSizeBehavior),
            new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject d) => (bool)d.GetValue(EnabledProperty);
    public static void SetEnabled(DependencyObject d, bool value) => d.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ListView listView) return;

        if ((bool)e.NewValue)
        {
            listView.Loaded += OnLoaded;
            listView.SizeChanged += OnSizeChanged;
        }
        else
        {
            listView.Loaded -= OnLoaded;
            listView.SizeChanged -= OnSizeChanged;
        }
    }

    private static void OnLoaded(object sender, RoutedEventArgs e) => Resize((ListView)sender);
    private static void OnSizeChanged(object sender, SizeChangedEventArgs e) => Resize((ListView)sender);

    private static void Resize(ListView listView)
    {
        if (listView.View is not GridView gridView) return;

        var totalStars = 0.0;
        var fixedWidth = 0.0;
        var starMinWidth = 0.0;

        foreach (var col in gridView.Columns)
        {
            var star = GetStarWidth(col);
            var minWidth = GetMinWidth(col);
            if (star > 0)
            {
                totalStars += star;
                starMinWidth += minWidth;
            }
            else
            {
                var width = double.IsNaN(col.Width) ? col.ActualWidth : col.Width;
                width = Math.Max(width, minWidth);
                col.Width = width;
                fixedWidth += width;
            }
        }

        if (totalStars <= 0) return;

        var available = listView.ActualWidth - fixedWidth - SystemParameters.VerticalScrollBarWidth - 4;
        if (available <= 0) return;

        var distributableWidth = Math.Max(0, available - starMinWidth);

        foreach (var col in gridView.Columns)
        {
            var star = GetStarWidth(col);
            if (star > 0)
            {
                var minWidth = GetMinWidth(col);
                col.Width = minWidth + (distributableWidth * (star / totalStars));
            }
        }
    }
}
