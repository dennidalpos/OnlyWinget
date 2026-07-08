using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace OnlyWinget.Controls;

[ContentProperty(Name = nameof(Columns))]
public sealed partial class OnlyWingetTable : UserControl
{
    private INotifyCollectionChanged? subscribedCollection;
    internal readonly TableLayoutHelper layoutHelper = new();
    private Grid? headerGrid;
    private bool hasAutoFitDone;
    private readonly Dictionary<string, string> columnFilters = new(StringComparer.OrdinalIgnoreCase);
    private readonly ObservableCollection<object> filteredItems = [];

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(OnlyWingetTable), new PropertyMetadata(null, OnItemsSourceChanged));
    public static readonly DependencyProperty HeaderSelectionProperty = DependencyProperty.Register(
        nameof(HeaderSelection), typeof(bool?), typeof(OnlyWingetTable), new PropertyMetadata(false, OnHeaderSelectionChanged));
    public static readonly DependencyProperty IsSelectionEnabledProperty = DependencyProperty.Register(
        nameof(IsSelectionEnabled), typeof(bool), typeof(OnlyWingetTable), new PropertyMetadata(true, OnStructureChanged));
    public static readonly DependencyProperty ToggleOnRowClickProperty = DependencyProperty.Register(
        nameof(ToggleOnRowClick), typeof(bool), typeof(OnlyWingetTable), new PropertyMetadata(true));

    public OnlyWingetTable()
    {
        InitializeComponent();
        Rows.Resources.Add("TableLayout", layoutHelper);
        Columns.CollectionChanged += (_, _) => Rebuild();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Rows.ItemClick += OnItemClick;
        SizeChanged += OnSizeChanged;
        Rows.PointerWheelChanged += OnRowsPointerWheelChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ItemsSource is INotifyCollectionChanged collection)
        {
            UpdateCollectionSubscription(collection);
        }

        var parentScrollViewer = FindAncestorScrollViewer();
        if (parentScrollViewer != null)
        {
            Rows.SetValue(ScrollViewer.VerticalScrollModeProperty, ScrollMode.Disabled);
            Rows.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        }
        else
        {
            Rows.SetValue(ScrollViewer.VerticalScrollModeProperty, ScrollMode.Enabled);
            Rows.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        }

        Rebuild();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UpdateCollectionSubscription(null);
    }

    public ObservableCollection<OnlyWingetTableColumn> Columns { get; } = [];
    public IEnumerable? ItemsSource { get => (IEnumerable?)GetValue(ItemsSourceProperty); set => SetValue(ItemsSourceProperty, value); }
    public bool? HeaderSelection { get => (bool?)GetValue(HeaderSelectionProperty); set => SetValue(HeaderSelectionProperty, value); }
    public bool IsSelectionEnabled { get => (bool)GetValue(IsSelectionEnabledProperty); set => SetValue(IsSelectionEnabledProperty, value); }
    public bool ToggleOnRowClick { get => (bool)GetValue(ToggleOnRowClickProperty); set => SetValue(ToggleOnRowClickProperty, value); }
    public string SelectionBindingPath { get; set; } = "IsSelected";
    public string SelectionLabel { get; set; } = "Select all";

    public event EventHandler<OnlyWingetTableSelectionEventArgs>? SelectionToggled;
    public event EventHandler? ToggleAllRequested;
    public event EventHandler<OnlyWingetTableRowEventArgs>? RowInvoked;

    internal void RaiseSelectionToggled(object item, bool isSelected)
    {
        SelectionToggled?.Invoke(this, new OnlyWingetTableSelectionEventArgs(item, isSelected));
    }

    public void SetHeaders(params string[] headers)
    {
        for (var index = 0; index < Math.Min(headers.Length, Columns.Count); index++) Columns[index].Header = headers[index];
        Rebuild();
    }

    private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var table = (OnlyWingetTable)sender;
        table.UpdateCollectionSubscription(args.NewValue as INotifyCollectionChanged);
        table.ApplyFilters();
        table.SynchronizeSelection();
        table.hasAutoFitDone = false;
        table.AutoFitColumns();
    }

    private void UpdateCollectionSubscription(INotifyCollectionChanged? newCollection)
    {
        if (subscribedCollection is not null)
        {
            subscribedCollection.CollectionChanged -= OnItemsSourceCollectionChanged;
        }
        subscribedCollection = newCollection;
        if (subscribedCollection is not null && IsLoaded)
        {
            subscribedCollection.CollectionChanged += OnItemsSourceCollectionChanged;
        }
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplyFilters();
        SynchronizeSelection();
        if (e.Action == NotifyCollectionChangedAction.Add || e.Action == NotifyCollectionChangedAction.Reset)
        {
            hasAutoFitDone = false;
            AutoFitColumns();
        }
    }

    private static void OnHeaderSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((OnlyWingetTable)sender).SynchronizeSelection();

    private static void OnStructureChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((OnlyWingetTable)sender).Rebuild();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RecalculateWidths(e.NewSize.Width);
        if (!hasAutoFitDone && e.NewSize.Width > 0 && ItemsSource != null)
        {
            AutoFitColumns();
        }
    }

    public void AutoFitColumns()
    {
        if (Columns.Count == 0 || ItemsSource == null) return;

        var items = new List<object>();
        var enumerator = ItemsSource.GetEnumerator();
        while (enumerator.MoveNext())
        {
            if (enumerator.Current != null)
            {
                items.Add(enumerator.Current);
            }
        }

        if (items.Count == 0) return;

        for (int i = 0; i < Columns.Count; i++)
        {
            var col = Columns[i];
            if (col.IsManuallyResized) continue;

            // Start with header width
            double maxTextWidth = 0;
            if (!string.IsNullOrEmpty(col.Header))
            {
                maxTextWidth = col.Header.Length * 8.2 + 28;
            }

            // Inspect up to first 100 items for performance
            var sampleCount = Math.Min(items.Count, 100);
            for (int j = 0; j < sampleCount; j++)
            {
                var item = items[j];
                if (item == null) continue;

                var prop = item.GetType().GetProperty(col.BindingPath);
                if (prop != null)
                {
                    var val = prop.GetValue(item)?.ToString();
                    if (!string.IsNullOrEmpty(val))
                    {
                        double estimatedWidth = val.Length * 7.5 + 26;
                        if (estimatedWidth > maxTextWidth)
                        {
                            maxTextWidth = estimatedWidth;
                        }
                    }
                }
            }

            // Constrain between 60px and 450px
            // Minimum 90px: the header filter TextBox needs ~88px (64px min + 12px margin each side)
            double finalWidth = Math.Max(90, Math.Min(maxTextWidth, 450));

            col.Width = new GridLength(finalWidth);
        }

        if (ActualWidth > 0)
        {
            RecalculateWidths(ActualWidth);
            hasAutoFitDone = true;
        }
    }

    private void Rebuild()
    {
        if (!IsLoaded) return;
        var automationId = AutomationProperties.GetAutomationId(this);
        AutomationProperties.SetAutomationId(Rows, automationId);
        AutomationProperties.SetName(Rows, AutomationProperties.GetName(this) ?? automationId);

        RecalculateWidths(ActualWidth);

        Rows.Header = BuildHeader();
        Rows.SelectionMode = ListViewSelectionMode.None;
        Rows.IsItemClickEnabled = IsSelectionEnabled;
        ApplyFilters();
        Rows.ItemsSource = filteredItems;
        SynchronizeSelection();
    }

    private FrameworkElement BuildHeader()
    {
        headerGrid = CreateGrid();
        if (IsSelectionEnabled)
        {
            var selectAll = new CheckBox { IsThreeState = true, IsChecked = HeaderSelection, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Tag = "Header", MinWidth = 0, MinHeight = 0, Padding = new Thickness(0) };
            AutomationProperties.SetAutomationId(selectAll, $"{AutomationProperties.GetAutomationId(this)}SelectAll");
            AutomationProperties.SetName(selectAll, SelectionLabel);
            selectAll.Click += (_, _) => ToggleAllRequested?.Invoke(this, EventArgs.Empty);

            var checkBoxBorder = new Border
            {
                Padding = new Thickness(0, 8, 0, 8),
                BorderThickness = new Thickness(0, 0, 1, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = selectAll
            };
            checkBoxBorder.Style = (Style)global::Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderCellBorderStyle"];
            Grid.SetColumn(checkBoxBorder, 0);
            headerGrid.Children.Add(checkBoxBorder);
        }
        for (var index = 0; index < Columns.Count; index++)
        {
            var header = new TextBlock
            {
                Text = Columns[index].Header,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(12, 0, 12, 0)
            };
            header.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderTextBlockStyle"];
            var isLast = index == Columns.Count - 1;

            var cellGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                RowSpacing = 6,
                Padding = new Thickness(0, 8, 0, 8),
                // Prevent content (filter TextBox) from overflowing the column boundary
                MaxWidth = layoutHelper.GetWidth(index)
            };
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cellGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            cellGrid.Children.Add(header);

            var filterBox = new TextBox
            {
                PlaceholderText = string.Format(
                    global::System.Globalization.CultureInfo.CurrentCulture,
                    global::OnlyWinget.TextResources.Get("Filter_Column_Placeholder"),
                    Columns[index].Header),
                Margin = new Thickness(12, 0, 12, 0),
                MinWidth = 0,
                MaxWidth = Math.Max(0, layoutHelper.GetWidth(index) - 24),
                Tag = Columns[index].BindingPath,
                Text = columnFilters.GetValueOrDefault(Columns[index].BindingPath) ?? string.Empty
            };
            AutomationProperties.SetName(filterBox, filterBox.PlaceholderText);
            filterBox.TextChanged += OnColumnFilterChanged;
            Grid.SetRow(filterBox, 1);
            cellGrid.Children.Add(filterBox);

            var resizeHandle = new CursorGrid
            {
                Width = 12,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Stretch,
                Cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast)
            };
            Grid.SetRowSpan(resizeHandle, 2);

            int colIndex = index;
            bool isDragging = false;
            double originalWidth = 0;
            double startPointerX = 0;

            resizeHandle.PointerPressed += (s, args) =>
            {
                var pointerPoint = args.GetCurrentPoint(this);
                startPointerX = pointerPoint.Position.X;
                originalWidth = layoutHelper.GetWidth(colIndex);
                isDragging = resizeHandle.CapturePointer(args.Pointer);
                args.Handled = true;
            };

            resizeHandle.PointerMoved += (s, args) =>
            {
                if (!isDragging) return;
                var pointerPoint = args.GetCurrentPoint(this);
                double deltaX = pointerPoint.Position.X - startPointerX;
                double newWidth = Math.Max(originalWidth + deltaX, 90); // min width matches filter TextBox minimum
                Columns[colIndex].Width = new GridLength(newWidth);
                Columns[colIndex].IsManuallyResized = true;
                RecalculateWidths(ActualWidth);
                args.Handled = true;
            };

            resizeHandle.PointerReleased += (s, args) =>
            {
                if (isDragging)
                {
                    resizeHandle.ReleasePointerCapture(args.Pointer);
                    isDragging = false;
                    args.Handled = true;
                }
            };

            resizeHandle.PointerCaptureLost += (s, args) =>
            {
                isDragging = false;
            };

            cellGrid.Children.Add(resizeHandle);

            var cell = new Border
            {
                Padding = new Thickness(0),
                BorderThickness = isLast ? new Thickness(0) : new Thickness(0, 0, 1, 0),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = cellGrid
            };
            cell.Style = (Style)global::Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderCellBorderStyle"];

            Grid.SetColumn(cell, index + (IsSelectionEnabled ? 1 : 0));
            headerGrid.Children.Add(cell);
        }
        double total = (IsSelectionEnabled ? layoutHelper.CheckBoxWidth : 0);
        for (int i = 0; i < Columns.Count; i++)
        {
            total += layoutHelper.GetWidth(i);
        }
        headerGrid.Width = total;
        var headerBorder = new Border { Padding = new Thickness(0), CornerRadius = new CornerRadius(8, 8, 0, 0), Child = headerGrid };
        headerBorder.Style = (Style)global::Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderSurfaceStyle"];
        return headerBorder;
    }



    private Grid CreateGrid()
    {
        var grid = new Grid { MinHeight = 40 };
        if (IsSelectionEnabled) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(layoutHelper.CheckBoxWidth) });
        for (int i = 0; i < Columns.Count; i++)
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(layoutHelper.GetWidth(i)) });
        }
        return grid;
    }

    private void RecalculateWidths(double availableWidth)
    {
        if (Columns.Count == 0 || availableWidth <= 0) return;

        double checkboxWidth = IsSelectionEnabled ? 32 : 0;
        double remainingWidth = availableWidth - checkboxWidth;

        // Sum of all fixed/absolute columns
        double fixedWidthSum = 0;
        double totalStarShares = 0;

        var isStar = new bool[Columns.Count];
        var designWidths = new double[Columns.Count];

        for (int i = 0; i < Columns.Count; i++)
        {
            var col = Columns[i];
            if (col.Width.IsStar)
            {
                isStar[i] = true;
                totalStarShares += col.Width.Value;
            }
            else if (col.Width.IsAbsolute)
            {
                designWidths[i] = col.Width.Value;
                fixedWidthSum += col.Width.Value;
            }
            else
            {
                designWidths[i] = 120;
                fixedWidthSum += 120;
            }
        }

        double[] calculatedWidths = new double[Columns.Count];

        if (totalStarShares > 0)
        {
            double minStarWidthNeeded = totalStarShares * 80;
            if (remainingWidth >= fixedWidthSum + minStarWidthNeeded)
            {
                double starSpace = remainingWidth - fixedWidthSum;
                for (int i = 0; i < Columns.Count; i++)
                {
                    if (isStar[i])
                    {
                        calculatedWidths[i] = (Columns[i].Width.Value / totalStarShares) * starSpace;
                    }
                    else
                    {
                        calculatedWidths[i] = designWidths[i];
                    }
                }
            }
            else
            {
                for (int i = 0; i < Columns.Count; i++)
                {
                    if (isStar[i])
                    {
                        calculatedWidths[i] = 80 * Columns[i].Width.Value;
                    }
                    else
                    {
                        calculatedWidths[i] = designWidths[i];
                    }
                }
            }
        }
        else
        {
            if (remainingWidth > fixedWidthSum && Columns.Count > 0)
            {
                for (int i = 0; i < Columns.Count; i++)
                {
                    calculatedWidths[i] = designWidths[i];
                }
                calculatedWidths[^1] += (remainingWidth - fixedWidthSum);
            }
            else
            {
                for (int i = 0; i < Columns.Count; i++)
                {
                    calculatedWidths[i] = designWidths[i];
                }
            }
        }

        layoutHelper.CheckBoxWidth = checkboxWidth;
        for (int i = 0; i < Columns.Count; i++)
        {
            layoutHelper.SetWidth(i, Math.Max(calculatedWidths[i], 90));
        }

        UpdateHeaderGridWidths();
    }

    private void UpdateHeaderGridWidths()
    {
        if (headerGrid is null) return;

        double total = 0;
        int colIndex = 0;
        if (IsSelectionEnabled)
        {
            if (headerGrid.ColumnDefinitions.Count > colIndex)
            {
                var w = layoutHelper.CheckBoxWidth;
                headerGrid.ColumnDefinitions[colIndex].Width = new GridLength(w);
                total += w;
            }
            colIndex++;
        }

        for (int i = 0; i < Columns.Count; i++)
        {
            if (headerGrid.ColumnDefinitions.Count > colIndex)
            {
                var w = layoutHelper.GetWidth(i);
                headerGrid.ColumnDefinitions[colIndex].Width = new GridLength(w);
                total += w;

                // Update the MaxWidth on the inner cellGrid so the filter TextBox
                // does not overflow the column boundary
                foreach (var child in headerGrid.Children)
                {
                    if (child is Border border && Grid.GetColumn(border) == colIndex && border.Child is Grid cellGrid)
                    {
                        cellGrid.MaxWidth = w;
                        foreach (var innerChild in cellGrid.Children)
                        {
                            if (innerChild is TextBox textBox)
                            {
                                textBox.MaxWidth = Math.Max(0, w - 24);
                            }
                        }
                    }
                }
            }
            colIndex++;
        }
        headerGrid.Width = total;
    }

    private void SynchronizeSelection()
    {
        if (Rows.Header is Border { Child: Grid grid })
        {
            var firstChild = grid.Children.FirstOrDefault();
            if (firstChild is CheckBox cb)
            {
                cb.IsChecked = HeaderSelection;
            }
            else if (firstChild is Border border && border.Child is CheckBox cbBorder)
            {
                cbBorder.IsChecked = HeaderSelection;
            }
        }
    }

    private void OnItemClick(object sender, ItemClickEventArgs e)
    {
        var item = e.ClickedItem;
        if (item is null) return;
        if (!ToggleOnRowClick)
        {
            RowInvoked?.Invoke(this, new OnlyWingetTableRowEventArgs(item));
            return;
        }
        var isSelected = item.GetType().GetProperty(SelectionBindingPath)?.GetValue(item) is true;
        SelectionToggled?.Invoke(this, new OnlyWingetTableSelectionEventArgs(item, !isSelected));
    }

    private ScrollViewer? FindAncestorScrollViewer()
    {
        DependencyObject curr = VisualTreeHelper.GetParent(this);
        while (curr != null)
        {
            if (curr is ScrollViewer sv) return sv;
            curr = VisualTreeHelper.GetParent(curr);
        }
        return null;
    }

    private void OnRowsPointerWheelChanged(object sender, Microsoft.UI.Xaml.Input.PointerRoutedEventArgs e)
    {
        var parentScrollViewer = FindAncestorScrollViewer();
        if (parentScrollViewer is null) return;

        var pointerPoint = e.GetCurrentPoint(Rows);
        var properties = pointerPoint.Properties;
        if (!properties.IsHorizontalMouseWheel)
        {
            var delta = properties.MouseWheelDelta;
            if (delta != 0)
            {
                parentScrollViewer.ChangeView(null, parentScrollViewer.VerticalOffset - delta, null, false);
                e.Handled = true;
            }
        }
    }

    private void OnColumnFilterChanged(object sender, TextChangedEventArgs args)
    {
        if (sender is not TextBox { Tag: string bindingPath } box)
        {
            return;
        }

        var value = box.Text.Trim();
        if (value.Length == 0)
        {
            columnFilters.Remove(bindingPath);
        }
        else
        {
            columnFilters[bindingPath] = value;
        }

        ApplyFilters();
    }

    private void ApplyFilters()
    {
        filteredItems.Clear();
        if (ItemsSource is null)
        {
            Rows.ItemsSource = filteredItems;
            return;
        }

        foreach (var item in ItemsSource)
        {
            if (item is not null && MatchesFilters(item))
            {
                filteredItems.Add(item);
            }
        }

        Rows.ItemsSource = filteredItems;
    }

    private bool MatchesFilters(object item)
    {
        foreach (var filter in columnFilters)
        {
            var value = item.GetType().GetProperty(filter.Key)?.GetValue(item)?.ToString() ?? string.Empty;
            if (!value.Contains(filter.Value, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}

public sealed class OnlyWingetTableSelectionEventArgs(object item, bool isSelected) : EventArgs
{
    public object Item { get; } = item;
    public bool IsSelected { get; } = isSelected;
}

public sealed class CursorGrid : Grid
{
    public Microsoft.UI.Input.InputCursor Cursor
    {
        get => ProtectedCursor;
        set => ProtectedCursor = value;
    }
}

public sealed class TableLayoutHelper : DependencyObject
{
    public static readonly DependencyProperty CheckBoxWidthProperty = DependencyProperty.Register(
        nameof(CheckBoxWidth), typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(32.0, OnCheckBoxWidthPropertyChanged));

    private static void OnCheckBoxWidthPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is TableLayoutHelper helper && e.NewValue is double newWidth)
        {
            helper.CheckBoxWidthChanged?.Invoke(newWidth);
        }
    }

    public event Action<double>? CheckBoxWidthChanged;
    public event Action<int, double>? WidthChanged;

    private static void OnWidthChanged(DependencyObject d, DependencyPropertyChangedEventArgs e, int index)
    {
        if (d is TableLayoutHelper helper && e.NewValue is double newWidth)
        {
            helper.WidthChanged?.Invoke(index, newWidth);
        }
    }

    public static readonly DependencyProperty Width0Property = DependencyProperty.Register("Width0", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 0)));
    public static readonly DependencyProperty Width1Property = DependencyProperty.Register("Width1", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 1)));
    public static readonly DependencyProperty Width2Property = DependencyProperty.Register("Width2", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 2)));
    public static readonly DependencyProperty Width3Property = DependencyProperty.Register("Width3", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 3)));
    public static readonly DependencyProperty Width4Property = DependencyProperty.Register("Width4", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 4)));
    public static readonly DependencyProperty Width5Property = DependencyProperty.Register("Width5", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 5)));
    public static readonly DependencyProperty Width6Property = DependencyProperty.Register("Width6", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 6)));
    public static readonly DependencyProperty Width7Property = DependencyProperty.Register("Width7", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 7)));
    public static readonly DependencyProperty Width8Property = DependencyProperty.Register("Width8", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 8)));
    public static readonly DependencyProperty Width9Property = DependencyProperty.Register("Width9", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 9)));
    public static readonly DependencyProperty Width10Property = DependencyProperty.Register("Width10", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 10)));
    public static readonly DependencyProperty Width11Property = DependencyProperty.Register("Width11", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0, (d, e) => OnWidthChanged(d, e, 11)));

    public double CheckBoxWidth
    {
        get => (double)GetValue(CheckBoxWidthProperty);
        set => SetValue(CheckBoxWidthProperty, value);
    }

    public double Width0 { get => (double)GetValue(Width0Property); set => SetValue(Width0Property, value); }
    public double Width1 { get => (double)GetValue(Width1Property); set => SetValue(Width1Property, value); }
    public double Width2 { get => (double)GetValue(Width2Property); set => SetValue(Width2Property, value); }
    public double Width3 { get => (double)GetValue(Width3Property); set => SetValue(Width3Property, value); }
    public double Width4 { get => (double)GetValue(Width4Property); set => SetValue(Width4Property, value); }
    public double Width5 { get => (double)GetValue(Width5Property); set => SetValue(Width5Property, value); }
    public double Width6 { get => (double)GetValue(Width6Property); set => SetValue(Width6Property, value); }
    public double Width7 { get => (double)GetValue(Width7Property); set => SetValue(Width7Property, value); }
    public double Width8 { get => (double)GetValue(Width8Property); set => SetValue(Width8Property, value); }
    public double Width9 { get => (double)GetValue(Width9Property); set => SetValue(Width9Property, value); }
    public double Width10 { get => (double)GetValue(Width10Property); set => SetValue(Width10Property, value); }
    public double Width11 { get => (double)GetValue(Width11Property); set => SetValue(Width11Property, value); }

    public void SetWidth(int index, double value)
    {
        switch (index)
        {
            case 0: Width0 = value; break;
            case 1: Width1 = value; break;
            case 2: Width2 = value; break;
            case 3: Width3 = value; break;
            case 4: Width4 = value; break;
            case 5: Width5 = value; break;
            case 6: Width6 = value; break;
            case 7: Width7 = value; break;
            case 8: Width8 = value; break;
            case 9: Width9 = value; break;
            case 10: Width10 = value; break;
            case 11: Width11 = value; break;
        }
    }

    public double GetWidth(int index)
    {
        return index switch
        {
            0 => Width0,
            1 => Width1,
            2 => Width2,
            3 => Width3,
            4 => Width4,
            5 => Width5,
            6 => Width6,
            7 => Width7,
            8 => Width8,
            9 => Width9,
            10 => Width10,
            11 => Width11,
            _ => 100.0
        };
    }
}

public sealed class DoubleToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        return value is double d ? new GridLength(d) : GridLength.Auto;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        throw new NotImplementedException();
    }
}

public sealed class OnlyWingetTableRow : Grid
{
    private bool isInitialized;
    private TableLayoutHelper? subscribedLayoutHelper;

    public OnlyWingetTableRow()
    {
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!isInitialized)
        {
            InitializeRow();
        }
        else
        {
            var parentTable = FindParentTable();
            if (parentTable != null)
            {
                SubscribeLayoutHelper(parentTable.layoutHelper);
                SyncWidths(parentTable);
            }
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        UnsubscribeLayoutHelper();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        UpdateAutomationProperties();
    }

    private void SubscribeLayoutHelper(TableLayoutHelper layoutHelper)
    {
        if (subscribedLayoutHelper == layoutHelper) return;

        UnsubscribeLayoutHelper();

        subscribedLayoutHelper = layoutHelper;
        subscribedLayoutHelper.WidthChanged += OnColumnWidthChanged;
        subscribedLayoutHelper.CheckBoxWidthChanged += OnCheckBoxWidthChanged;
    }

    private void UnsubscribeLayoutHelper()
    {
        if (subscribedLayoutHelper != null)
        {
            subscribedLayoutHelper.WidthChanged -= OnColumnWidthChanged;
            subscribedLayoutHelper.CheckBoxWidthChanged -= OnCheckBoxWidthChanged;
            subscribedLayoutHelper = null;
        }
    }

    private void OnColumnWidthChanged(int index, double newWidth)
    {
        var parentTable = FindParentTable();
        if (parentTable == null) return;

        var isSelectionEnabled = parentTable.IsSelectionEnabled;
        var colIndex = index + (isSelectionEnabled ? 1 : 0);

        if (colIndex < ColumnDefinitions.Count)
        {
            ColumnDefinitions[colIndex].Width = new GridLength(newWidth);
        }
        UpdateRowWidth();
    }

    private void OnCheckBoxWidthChanged(double newWidth)
    {
        if (ColumnDefinitions.Count > 0)
        {
            ColumnDefinitions[0].Width = new GridLength(newWidth);
        }
        UpdateRowWidth();
    }

    private void SyncWidths(OnlyWingetTable parentTable)
    {
        var layoutHelper = parentTable.layoutHelper;
        var isSelectionEnabled = parentTable.IsSelectionEnabled;

        if (isSelectionEnabled && ColumnDefinitions.Count > 0)
        {
            ColumnDefinitions[0].Width = new GridLength(layoutHelper.CheckBoxWidth);
        }

        for (int i = 0; i < parentTable.Columns.Count; i++)
        {
            var colIndex = i + (isSelectionEnabled ? 1 : 0);
            if (colIndex < ColumnDefinitions.Count)
            {
                ColumnDefinitions[colIndex].Width = new GridLength(layoutHelper.GetWidth(i));
            }
        }
        UpdateRowWidth();
    }

    private void InitializeRow()
    {
        var parentTable = FindParentTable();
        if (parentTable == null) return;

        isInitialized = true;

        var columns = parentTable.Columns;
        var layoutHelper = parentTable.layoutHelper;
        var isSelectionEnabled = parentTable.IsSelectionEnabled;

        SubscribeLayoutHelper(layoutHelper);

        ColumnDefinitions.Clear();
        Children.Clear();

        if (isSelectionEnabled)
        {
            var colDef = new ColumnDefinition { Width = new GridLength(layoutHelper.CheckBoxWidth) };
            ColumnDefinitions.Add(colDef);

            var checkBox = new CheckBox
            {
                IsHitTestVisible = false,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center,
                MinWidth = 0,
                MinHeight = 0,
                Padding = new Thickness(0)
            };
            AutomationProperties.SetName(checkBox, parentTable.SelectionLabel);

            var cbBinding = new Binding
            {
                Path = new PropertyPath(parentTable.SelectionBindingPath),
                Mode = BindingMode.OneWay
            };
            checkBox.SetBinding(CheckBox.IsCheckedProperty, cbBinding);

            var border = new Border
            {
                BorderBrush = (Brush)global::Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(0, 0, 1, 1),
                Padding = new Thickness(0, 8, 0, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = checkBox
            };
            Grid.SetColumn(border, 0);
            Children.Add(border);
        }

        for (int i = 0; i < columns.Count; i++)
        {
            var col = columns[i];
            var colIndex = i + (isSelectionEnabled ? 1 : 0);

            var colDef = new ColumnDefinition { Width = new GridLength(layoutHelper.GetWidth(i)) };
            ColumnDefinitions.Add(colDef);

            var textBlock = new TextBlock
            {
                VerticalAlignment = VerticalAlignment.Center,
                IsTextSelectionEnabled = col.IsTextSelectable
            };

            var styleKey = col.IsPrimary ? "RowPrimaryTextBlockStyle" : "TableCellTextBlockStyle";
            if (global::Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(styleKey, out var styleObj))
            {
                if (styleObj is Style style)
                {
                    textBlock.Style = style;
                }
            }

            var textBinding = new Binding
            {
                Path = new PropertyPath(col.BindingPath),
                Mode = BindingMode.OneWay
            };
            textBlock.SetBinding(TextBlock.TextProperty, textBinding);

            var isLast = i == columns.Count - 1;
            var border = new Border
            {
                BorderBrush = (Brush)global::Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"],
                BorderThickness = isLast ? new Thickness(0, 0, 0, 1) : new Thickness(0, 0, 1, 1),
                Padding = new Thickness(12, 8, 12, 8),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = textBlock
            };
            Grid.SetColumn(border, colIndex);
            Children.Add(border);
        }

        UpdateAutomationProperties();
        UpdateRowWidth();
    }

    private void UpdateRowWidth()
    {
        double total = 0;
        foreach (var colDef in ColumnDefinitions)
        {
            total += colDef.Width.Value;
        }
        Width = total;
    }

    private void UpdateAutomationProperties()
    {
        var parentTable = FindParentTable();
        if (parentTable == null) return;

        var primaryCol = parentTable.Columns.FirstOrDefault(c => c.IsPrimary) ?? parentTable.Columns.FirstOrDefault();
        if (primaryCol != null && DataContext != null)
        {
            var propName = primaryCol.BindingPath;
            var val = DataContext.GetType().GetProperty(propName)?.GetValue(DataContext)?.ToString();
            if (val != null)
            {
                AutomationProperties.SetName(this, val);
            }
        }
    }

    private OnlyWingetTable? FindParentTable()
    {
        DependencyObject curr = this;
        while (curr != null)
        {
            if (curr is OnlyWingetTable table) return table;
            curr = VisualTreeHelper.GetParent(curr);
        }
        return null;
    }
}

public sealed class OnlyWingetTableRowEventArgs(object item) : EventArgs
{
    public object Item { get; } = item;
}
