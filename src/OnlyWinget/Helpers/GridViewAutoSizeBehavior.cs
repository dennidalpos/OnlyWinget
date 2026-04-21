// OnlyWinget
// Copyright (c) 2026 Danny Perondi. All rights reserved.
// Proprietary and confidential. Unauthorized copying, modification,
// distribution, sublicensing, or commercial use is prohibited.

using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace OnlyWinget.Helpers;

public static class GridViewAutoSizeBehavior
{
    private static readonly ConditionalWeakTable<ListView, SubscriptionState> States = new();
    private static readonly DependencyPropertyDescriptor? ItemsSourceDescriptor =
        DependencyPropertyDescriptor.FromProperty(ItemsControl.ItemsSourceProperty, typeof(ListView));

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
        if (d is not ListView listView)
        {
            return;
        }

        if ((bool)e.NewValue)
        {
            Attach(listView);
        }
        else
        {
            Detach(listView);
        }
    }

    private static void Attach(ListView listView)
    {
        var state = States.GetValue(listView, static _ => new SubscriptionState());
        state.Owner = listView;
        if (state.IsAttached)
        {
            ScheduleResize(listView);
            return;
        }

        state.IsAttached = true;
        listView.Loaded += OnLoaded;
        listView.SizeChanged += OnSizeChanged;
        listView.Unloaded += OnUnloaded;
        listView.ItemContainerGenerator.StatusChanged += state.ItemContainerStatusChangedHandler;
        ItemsSourceDescriptor?.AddValueChanged(listView, OnItemsSourceChanged);
        AttachColumnResizeHandlers(listView);
        AttachItemSubscriptions(listView);
        ScheduleResize(listView);
    }

    private static void Detach(ListView listView)
    {
        if (!States.TryGetValue(listView, out var state) || !state.IsAttached)
        {
            return;
        }

        state.IsAttached = false;
        listView.Loaded -= OnLoaded;
        listView.SizeChanged -= OnSizeChanged;
        listView.Unloaded -= OnUnloaded;
        listView.ItemContainerGenerator.StatusChanged -= state.ItemContainerStatusChangedHandler;
        ItemsSourceDescriptor?.RemoveValueChanged(listView, OnItemsSourceChanged);
        DetachColumnResizeHandlers(listView);
        DetachItemSubscriptions(listView);
    }

    private static void OnLoaded(object sender, RoutedEventArgs e)
    {
        var listView = (ListView)sender;
        AttachItemSubscriptions(listView);
        ScheduleResize(listView);
    }

    private static void OnUnloaded(object sender, RoutedEventArgs e)
    {
        Detach((ListView)sender);
    }

    private static void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (e.WidthChanged)
        {
            ScheduleResize((ListView)sender);
        }
    }

    private static void OnItemsSourceChanged(object? sender, EventArgs e)
    {
        if (sender is not ListView listView)
        {
            return;
        }

        DetachItemSubscriptions(listView);
        AttachItemSubscriptions(listView);
        ScheduleResize(listView);
    }

    private static void AttachColumnResizeHandlers(ListView listView)
    {
        if (listView.View is not GridView gridView)
        {
            return;
        }

        if (!States.TryGetValue(listView, out var state) || state.ColumnWidthDescriptor != null)
        {
            return;
        }

        state.ColumnWidthDescriptor = DependencyPropertyDescriptor.FromProperty(GridViewColumn.WidthProperty, typeof(GridViewColumn));
        foreach (var column in gridView.Columns)
        {
            state.ColumnWidthDescriptor?.AddValueChanged(column, state.ColumnWidthChangedHandler);
        }
    }

    private static void DetachColumnResizeHandlers(ListView listView)
    {
        if (listView.View is not GridView gridView || !States.TryGetValue(listView, out var state))
        {
            return;
        }

        foreach (var column in gridView.Columns)
        {
            state.ColumnWidthDescriptor?.RemoveValueChanged(column, state.ColumnWidthChangedHandler);
        }

        state.ColumnWidthDescriptor = null;
    }

    private static void AttachItemSubscriptions(ListView listView)
    {
        if (!States.TryGetValue(listView, out var state))
        {
            return;
        }

        if (listView.ItemsSource is INotifyCollectionChanged collectionChanged)
        {
            collectionChanged.CollectionChanged += state.CollectionChangedHandler;
            state.ObservedCollection = collectionChanged;
        }

        foreach (var item in listView.Items.OfType<INotifyPropertyChanged>())
        {
            item.PropertyChanged += state.ItemPropertyChangedHandler;
            state.ObservedItems.Add(item);
        }
    }

    private static void DetachItemSubscriptions(ListView listView)
    {
        if (!States.TryGetValue(listView, out var state))
        {
            return;
        }

        if (state.ObservedCollection != null)
        {
            state.ObservedCollection.CollectionChanged -= state.CollectionChangedHandler;
            state.ObservedCollection = null;
        }

        foreach (var item in state.ObservedItems)
        {
            item.PropertyChanged -= state.ItemPropertyChangedHandler;
        }

        state.ObservedItems.Clear();
    }

    private static void OnItemsCollectionChanged(ListView listView, NotifyCollectionChangedEventArgs e)
    {
        if (!States.TryGetValue(listView, out var state))
        {
            return;
        }

        if (e.OldItems != null)
        {
            foreach (var item in e.OldItems.OfType<INotifyPropertyChanged>())
            {
                item.PropertyChanged -= state.ItemPropertyChangedHandler;
                state.ObservedItems.Remove(item);
            }
        }

        if (e.NewItems != null)
        {
            foreach (var item in e.NewItems.OfType<INotifyPropertyChanged>())
            {
                item.PropertyChanged += state.ItemPropertyChangedHandler;
                state.ObservedItems.Add(item);
            }
        }

        ScheduleResize(listView);
    }

    private static void ScheduleResize(ListView listView)
    {
        if (!States.TryGetValue(listView, out var state) || state.ResizeScheduled)
        {
            return;
        }

        state.ResizeScheduled = true;
        listView.Dispatcher.BeginInvoke(() =>
        {
            if (!States.TryGetValue(listView, out var currentState))
            {
                return;
            }

            currentState.ResizeScheduled = false;
            Resize(listView);
        }, DispatcherPriority.Background);
    }

    private static void Resize(ListView listView)
    {
        if (listView.View is not GridView gridView)
        {
            return;
        }

        var totalStars = 0.0;
        var fixedWidth = 0.0;
        var starMinWidth = 0.0;

        foreach (var column in gridView.Columns)
        {
            var star = GetStarWidth(column);
            var minWidth = GetMinWidth(column);
            if (star > 0)
            {
                totalStars += star;
                starMinWidth += minWidth;
                continue;
            }

            var width = double.IsNaN(column.Width) ? column.ActualWidth : column.Width;
            width = Math.Max(width, minWidth);
            column.Width = width;
            fixedWidth += width;
        }

        if (totalStars <= 0)
        {
            return;
        }

        var borderWidth = listView.BorderThickness.Left + listView.BorderThickness.Right;
        var available = listView.ActualWidth - borderWidth - fixedWidth - SystemParameters.VerticalScrollBarWidth - 4;
        if (available <= 0)
        {
            return;
        }

        var distributableWidth = Math.Max(0, available - starMinWidth);

        foreach (var column in gridView.Columns)
        {
            var star = GetStarWidth(column);
            if (star <= 0)
            {
                continue;
            }

            var minWidth = GetMinWidth(column);
            column.Width = minWidth + (distributableWidth * (star / totalStars));
        }
    }

    private sealed class SubscriptionState
    {
        public SubscriptionState()
        {
            ColumnWidthChangedHandler = (_, _) =>
            {
                if (Owner != null)
                {
                    ScheduleResize(Owner);
                }
            };

            CollectionChangedHandler = (_, e) =>
            {
                if (Owner != null)
                {
                    OnItemsCollectionChanged(Owner, e);
                }
            };

            ItemPropertyChangedHandler = (_, _) =>
            {
                if (Owner != null)
                {
                    ScheduleResize(Owner);
                }
            };

            ItemContainerStatusChangedHandler = (_, _) =>
            {
                if (Owner != null)
                {
                    ScheduleResize(Owner);
                }
            };
        }

        public bool IsAttached { get; set; }
        public bool ResizeScheduled { get; set; }
        public DependencyPropertyDescriptor? ColumnWidthDescriptor { get; set; }
        public INotifyCollectionChanged? ObservedCollection { get; set; }
        public HashSet<INotifyPropertyChanged> ObservedItems { get; } = new();
        public EventHandler ColumnWidthChangedHandler { get; }
        public NotifyCollectionChangedEventHandler CollectionChangedHandler { get; }
        public PropertyChangedEventHandler ItemPropertyChangedHandler { get; }
        public EventHandler ItemContainerStatusChangedHandler { get; }
        public ListView? Owner { get; set; }
    }

}
