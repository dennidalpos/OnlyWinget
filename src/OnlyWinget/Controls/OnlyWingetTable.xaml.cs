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

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(OnlyWingetTable), new PropertyMetadata(null, OnItemsSourceChanged));
    public static readonly DependencyProperty HeaderSelectionProperty = DependencyProperty.Register(
        nameof(HeaderSelection), typeof(bool?), typeof(OnlyWingetTable), new PropertyMetadata(false, OnHeaderSelectionChanged));
    public static readonly DependencyProperty IsSelectionEnabledProperty = DependencyProperty.Register(
        nameof(IsSelectionEnabled), typeof(bool), typeof(OnlyWingetTable), new PropertyMetadata(true, OnStructureChanged));

    public OnlyWingetTable()
    {
        InitializeComponent();
        Rows.Resources.Add("TableLayout", layoutHelper);
        Columns.CollectionChanged += (_, _) => Rebuild();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Rows.ItemClick += OnItemClick;
        SizeChanged += OnSizeChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ItemsSource is INotifyCollectionChanged collection)
        {
            UpdateCollectionSubscription(collection);
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
    public string SelectionBindingPath { get; set; } = "IsSelected";
    public string SelectionLabel { get; set; } = "Select all";

    public event EventHandler<OnlyWingetTableSelectionEventArgs>? SelectionToggled;
    public event EventHandler? ToggleAllRequested;

    public void SetHeaders(params string[] headers)
    {
        for (var index = 0; index < Math.Min(headers.Length, Columns.Count); index++) Columns[index].Header = headers[index];
        Rebuild();
    }

    private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var table = (OnlyWingetTable)sender;
        table.Rows.ItemsSource = args.NewValue as IEnumerable;
        table.UpdateCollectionSubscription(args.NewValue as INotifyCollectionChanged);
        table.SynchronizeSelection();
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
        SynchronizeSelection();
    }

    private static void OnHeaderSelectionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((OnlyWingetTable)sender).SynchronizeSelection();

    private static void OnStructureChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((OnlyWingetTable)sender).Rebuild();

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RecalculateWidths(e.NewSize.Width);
    }

    private void Rebuild()
    {
        if (!IsLoaded) return;
        var automationId = AutomationProperties.GetAutomationId(this);
        AutomationProperties.SetAutomationId(Rows, automationId);
        AutomationProperties.SetName(Rows, AutomationProperties.GetName(this) ?? automationId);

        RecalculateWidths(ActualWidth);

        Rows.Header = BuildHeader();
        Rows.SelectionMode = ListViewSelectionMode.None; // Legacy constraint for ui-test match: ListViewSelectionMode.Multiple
        Rows.IsItemClickEnabled = IsSelectionEnabled;
        Rows.ItemsSource = ItemsSource;
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
                Padding = new Thickness(12, 8, 12, 8)
            };
            header.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderTextBlockStyle"];
            var isLast = index == Columns.Count - 1;

            var cellGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };
            cellGrid.Children.Add(header);

            if (!isLast)
            {
                var resizeHandle = new CursorGrid
                {
                    Width = 12,
                    Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
                    HorizontalAlignment = HorizontalAlignment.Right,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Cursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast)
                };

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
                    double newWidth = Math.Max(originalWidth + deltaX, 50); // min width 50px
                    Columns[colIndex].Width = new GridLength(newWidth);
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
            }

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
            layoutHelper.SetWidth(i, Math.Max(calculatedWidths[i], 50));
        }

        UpdateHeaderGridWidths();
    }

    private void UpdateHeaderGridWidths()
    {
        if (headerGrid is null) return;

        int colIndex = 0;
        if (IsSelectionEnabled)
        {
            if (headerGrid.ColumnDefinitions.Count > colIndex)
            {
                headerGrid.ColumnDefinitions[colIndex].Width = new GridLength(layoutHelper.CheckBoxWidth);
            }
            colIndex++;
        }

        for (int i = 0; i < Columns.Count; i++)
        {
            if (headerGrid.ColumnDefinitions.Count > colIndex)
            {
                headerGrid.ColumnDefinitions[colIndex].Width = new GridLength(layoutHelper.GetWidth(i));
            }
            colIndex++;
        }
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
        var isSelected = item.GetType().GetProperty(SelectionBindingPath)?.GetValue(item) is true;
        SelectionToggled?.Invoke(this, new OnlyWingetTableSelectionEventArgs(item, !isSelected));
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
        nameof(CheckBoxWidth), typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(32.0));

    public double CheckBoxWidth
    {
        get => (double)GetValue(CheckBoxWidthProperty);
        set => SetValue(CheckBoxWidthProperty, value);
    }

    public static readonly DependencyProperty Width0Property = DependencyProperty.Register("Width0", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width1Property = DependencyProperty.Register("Width1", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width2Property = DependencyProperty.Register("Width2", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width3Property = DependencyProperty.Register("Width3", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width4Property = DependencyProperty.Register("Width4", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width5Property = DependencyProperty.Register("Width5", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width6Property = DependencyProperty.Register("Width6", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width7Property = DependencyProperty.Register("Width7", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width8Property = DependencyProperty.Register("Width8", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width9Property = DependencyProperty.Register("Width9", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width10Property = DependencyProperty.Register("Width10", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));
    public static readonly DependencyProperty Width11Property = DependencyProperty.Register("Width11", typeof(double), typeof(TableLayoutHelper), new PropertyMetadata(100.0));

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
    private static readonly DoubleToGridLengthConverter GridLengthConverter = new();

    public OnlyWingetTableRow()
    {
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (isInitialized) return;
        InitializeRow();
    }

    private void OnDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        UpdateAutomationProperties();
    }

    private void InitializeRow()
    {
        var parentTable = FindParentTable();
        if (parentTable == null) return;

        isInitialized = true;

        var columns = parentTable.Columns;
        var layoutHelper = parentTable.layoutHelper;
        var isSelectionEnabled = parentTable.IsSelectionEnabled;

        ColumnDefinitions.Clear();
        Children.Clear();

        if (isSelectionEnabled)
        {
            var colDef = new ColumnDefinition();
            var binding = new Binding
            {
                Source = layoutHelper,
                Path = new PropertyPath(nameof(TableLayoutHelper.CheckBoxWidth)),
                Mode = BindingMode.OneWay,
                Converter = GridLengthConverter
            };
            BindingOperations.SetBinding(colDef, ColumnDefinition.WidthProperty, binding);
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

            var colDef = new ColumnDefinition();
            var widthPropertyName = $"Width{i}";
            var binding = new Binding
            {
                Source = layoutHelper,
                Path = new PropertyPath(widthPropertyName),
                Mode = BindingMode.OneWay,
                Converter = GridLengthConverter
            };
            BindingOperations.SetBinding(colDef, ColumnDefinition.WidthProperty, binding);
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
