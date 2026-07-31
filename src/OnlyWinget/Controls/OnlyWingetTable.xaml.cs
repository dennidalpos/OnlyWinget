using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using OnlyWinget.Presentation;

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
    private readonly Dictionary<Type, Dictionary<string, System.Reflection.PropertyInfo?>> propertyCache = [];
    private readonly Dictionary<Type, Dictionary<string, Func<object, object?>?>> getterCache = [];

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
        Rows.SelectionChanged += OnRowsSelectionChanged;
        Rows.KeyDown += OnRowsKeyDown;
        SizeChanged += OnSizeChanged;
        // Stop BringIntoView events from bubbling past the ListView's own ScrollViewer.
        // Without this, clicking/selecting a row makes the outer page ScrollViewer
        // jump back to the top because the focused item requests to be brought into view.
        Rows.BringIntoViewRequested += OnRowsBringIntoViewRequested;
    }

    private static void OnRowsBringIntoViewRequested(UIElement sender, BringIntoViewRequestedEventArgs args)
    {
        // Mark as handled so the event does not bubble further to the outer page
        // ScrollViewer (MainPageScrollViewer), which would scroll the whole page to top.
        // The ListView's own internal ScrollViewer still handles keyboard scrolling correctly.
        args.Handled = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ItemsSource is INotifyCollectionChanged collection)
        {
            UpdateCollectionSubscription(collection);
        }

        Rows.SetValue(ScrollViewer.VerticalScrollModeProperty, ScrollMode.Enabled);
        Rows.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        Rows.SetValue(ScrollViewer.HorizontalScrollModeProperty, ScrollMode.Enabled);
        Rows.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);

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

    public event EventHandler<OnlyWingetTableBatchSelectionEventArgs>? BatchSelectionChanged;
    public event EventHandler? ToggleAllRequested;
    public event EventHandler<OnlyWingetTablePasteEventArgs>? PasteRequested;

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
        table.SyncListViewSelectionWithItems();
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
        SyncListViewSelectionWithItems();
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

                var getter = GetCachedGetter(item.GetType(), col.BindingPath);
                if (getter != null)
                {
                    var val = getter(item)?.ToString();
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
        Rows.SelectionMode = IsSelectionEnabled ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
        Rows.IsItemClickEnabled = false;
        ApplyFilters();
        Rows.ItemsSource = filteredItems;
        SyncListViewSelectionWithItems();
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
                Padding = new Thickness(0, 6, 0, 6),
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
                Padding = new Thickness(0, 6, 0, 6),
                MaxWidth = layoutHelper.GetWidth(index)
            };
            cellGrid.Children.Add(header);

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
                double newWidth = Math.Max(originalWidth + deltaX, 60);
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

                foreach (var child in headerGrid.Children)
                {
                    if (child is Border border && Grid.GetColumn(border) == colIndex && border.Child is Grid cellGrid)
                    {
                        cellGrid.MaxWidth = w;
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

    private bool isSyncingSelection = false;
    private object? lastClickedItem;

    private void SyncListViewSelectionWithItems()
    {
        if (!IsSelectionEnabled || isSyncingSelection) return;
        isSyncingSelection = true;
        try
        {
            Rows.SelectionChanged -= OnRowsSelectionChanged;

            // Build the desired selected set from the source data model.
            var desiredSelected = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var item in filteredItems)
            {
                var getter = GetCachedGetter(item.GetType(), SelectionBindingPath);
                if (getter != null && getter(item) is true)
                {
                    desiredSelected.Add(item);
                }
            }

            // Remove items that should no longer be selected (iterate backwards to allow safe removal).
            for (int i = Rows.SelectedItems.Count - 1; i >= 0; i--)
            {
                if (!desiredSelected.Contains(Rows.SelectedItems[i]))
                {
                    Rows.SelectedItems.RemoveAt(i);
                }
            }

            // Add items that should be selected but aren't yet.
            var currentSelected = new HashSet<object>(ReferenceEqualityComparer.Instance);
            foreach (var item in Rows.SelectedItems) currentSelected.Add(item);
            foreach (var item in desiredSelected)
            {
                if (!currentSelected.Contains(item))
                {
                    Rows.SelectedItems.Add(item);
                }
            }
        }
        finally
        {
            Rows.SelectionChanged += OnRowsSelectionChanged;
            isSyncingSelection = false;
        }
        SynchronizeSelection();
    }

    private void OnRowsSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (isSyncingSelection || !IsSelectionEnabled) return;

        var addedList = new List<object>();
        foreach (var item in e.AddedItems)
        {
            var getter = GetCachedGetter(item.GetType(), SelectionBindingPath);
            if (getter != null && getter(item) is not true)
            {
                addedList.Add(item);
            }
        }

        var removedList = new List<object>();
        foreach (var item in e.RemovedItems)
        {
            var getter = GetCachedGetter(item.GetType(), SelectionBindingPath);
            if (getter != null && getter(item) is true)
            {
                removedList.Add(item);
            }
        }

        if (addedList.Count > 0 || removedList.Count > 0)
        {
            DispatcherQueue.TryEnqueue(() =>
            {
                if (addedList.Count > 0)
                {
                    BatchSelectionChanged?.Invoke(this, new OnlyWingetTableBatchSelectionEventArgs(addedList, true));
                }
                if (removedList.Count > 0)
                {
                    BatchSelectionChanged?.Invoke(this, new OnlyWingetTableBatchSelectionEventArgs(removedList, false));
                }
            });
        }
    }

    private static bool IsKeyDown(Windows.System.VirtualKey key)
    {
        var state = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(key);
        return state.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);
    }

    private void OnRowsKeyDown(object sender, Microsoft.UI.Xaml.Input.KeyRoutedEventArgs e)
    {
        var isCtrlPressed = IsKeyDown(Windows.System.VirtualKey.Control);

        if (isCtrlPressed && e.Key == Windows.System.VirtualKey.C)
        {
            CopySelectedToClipboard();
            e.Handled = true;
        }
        else if (isCtrlPressed && e.Key == Windows.System.VirtualKey.V)
        {
            PasteFromClipboard();
            e.Handled = true;
        }
    }

    private void CopySelectedToClipboard()
    {
        var text = GetSelectedRowsClipboardText();
        if (string.IsNullOrEmpty(text)) return;
        var package = new Windows.ApplicationModel.DataTransfer.DataPackage();
        package.SetText(text);
        Windows.ApplicationModel.DataTransfer.Clipboard.SetContent(package);
    }

    private async void PasteFromClipboard()
    {
        var dataPackageView = Windows.ApplicationModel.DataTransfer.Clipboard.GetContent();
        if (dataPackageView.Contains(Windows.ApplicationModel.DataTransfer.StandardDataFormats.Text))
        {
            try
            {
                var text = await dataPackageView.GetTextAsync();
                if (!string.IsNullOrEmpty(text))
                {
                    PasteRequested?.Invoke(this, new OnlyWingetTablePasteEventArgs(text));
                }
            }
            catch
            {
                // Ignore clipboard errors
            }
        }
    }

    private string GetSelectedRowsClipboardText()
    {
        var sb = new System.Text.StringBuilder();
        foreach (var item in Rows.SelectedItems)
        {
            var rowValues = new List<string>();
            foreach (var col in Columns)
            {
                var getter = GetCachedGetter(item.GetType(), col.BindingPath);
                var val = getter?.Invoke(item)?.ToString() ?? string.Empty;
                rowValues.Add(val);
            }
            sb.AppendLine(string.Join("\t", rowValues));
        }
        return sb.ToString();
    }

    internal void ToggleItemSelection(object item)
    {
        var isShiftPressed = IsKeyDown(Windows.System.VirtualKey.Shift);
        var isCtrlPressed = IsKeyDown(Windows.System.VirtualKey.Control);

        if (isShiftPressed)
        {
            var index = filteredItems.IndexOf(item);
            if (index >= 0)
            {
                var anchor = lastClickedItem;
                if (anchor == null && Rows.SelectedItems.Count > 0) anchor = Rows.SelectedItems[^1];
                var anchorIndex = anchor != null ? filteredItems.IndexOf(anchor) : 0;

                if (anchorIndex >= 0)
                {
                    int start = Math.Min(anchorIndex, index);
                    int end = Math.Max(anchorIndex, index);

                    var itemsToSelect = new List<object>();
                    for (int i = start; i <= end; i++)
                    {
                        itemsToSelect.Add(filteredItems[i]);
                    }

                    isSyncingSelection = true;
                    try
                    {
                        if (!isCtrlPressed)
                        {
                            Rows.SelectedItems.Clear();
                        }
                        foreach (var it in itemsToSelect)
                        {
                            if (!Rows.SelectedItems.Contains(it))
                            {
                                Rows.SelectedItems.Add(it);
                            }
                        }
                    }
                    finally
                    {
                        isSyncingSelection = false;
                    }

                    DispatcherQueue.TryEnqueue(() =>
                    {
                        BatchSelectionChanged?.Invoke(this, new OnlyWingetTableBatchSelectionEventArgs(itemsToSelect, true));
                    });
                    lastClickedItem = item;
                    return;
                }
            }
        }

        if (Rows.SelectedItems.Contains(item))
        {
            isSyncingSelection = true;
            try
            {
                Rows.SelectedItems.Remove(item);
            }
            finally
            {
                isSyncingSelection = false;
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                BatchSelectionChanged?.Invoke(this, new OnlyWingetTableBatchSelectionEventArgs([item], false));
            });
        }
        else
        {
            isSyncingSelection = true;
            try
            {
                Rows.SelectedItems.Add(item);
            }
            finally
            {
                isSyncingSelection = false;
            }
            DispatcherQueue.TryEnqueue(() =>
            {
                BatchSelectionChanged?.Invoke(this, new OnlyWingetTableBatchSelectionEventArgs([item], true));
            });
        }
        lastClickedItem = item;
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

        var caretIndex = box.SelectionStart;
        var selectionLength = box.SelectionLength;

        ApplyFilters();

        DispatcherQueue.TryEnqueue(() =>
        {
            box.Focus(FocusState.Programmatic);
            box.SelectionStart = caretIndex;
            box.SelectionLength = selectionLength;
        });
    }

    private object GetItemKey(object item)
    {
        if (item == null) return string.Empty;
        var type = item.GetType();

        var packageIdGetter = GetCachedGetter(type, "PackageId");
        if (packageIdGetter != null)
        {
            var packageId = packageIdGetter(item)?.ToString() ?? string.Empty;
            var sourceGetter = GetCachedGetter(type, "Source");
            var source = sourceGetter?.Invoke(item)?.ToString() ?? string.Empty;
            return $"{source}|{packageId}";
        }

        var updateIdGetter = GetCachedGetter(type, "UpdateId");
        if (updateIdGetter != null)
        {
            var updateId = updateIdGetter(item)?.ToString() ?? string.Empty;
            var revisionGetter = GetCachedGetter(type, "RevisionNumber");
            var revision = revisionGetter?.Invoke(item)?.ToString() ?? string.Empty;
            return $"{updateId}|{revision}";
        }

        var nameGetter = GetCachedGetter(type, "Name");
        if (nameGetter != null)
        {
            return nameGetter(item)?.ToString() ?? string.Empty;
        }

        return item;
    }

    private void ApplyFilters()
    {
        if (ItemsSource is null)
        {
            filteredItems.Clear();
            if (Rows.ItemsSource != filteredItems)
            {
                Rows.ItemsSource = filteredItems;
            }
            return;
        }

        var desiredItems = new List<object>();
        foreach (var item in ItemsSource)
        {
            if (item is not null && MatchesFilters(item))
            {
                desiredItems.Add(item);
            }
        }

        filteredItems.SynchronizeWith(desiredItems, GetItemKey);

        if (Rows.ItemsSource != filteredItems)
        {
            Rows.ItemsSource = filteredItems;
        }
        SyncListViewSelectionWithItems();
    }

    private System.Reflection.PropertyInfo? GetCachedProperty(Type type, string propertyName)
    {
        if (!propertyCache.TryGetValue(type, out var typeCache))
        {
            typeCache = [];
            propertyCache[type] = typeCache;
        }

        if (!typeCache.TryGetValue(propertyName, out var propInfo))
        {
            propInfo = type.GetProperty(propertyName);
            typeCache[propertyName] = propInfo;
        }

        return propInfo;
    }

    private Func<object, object?>? GetCachedGetter(Type type, string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName)) return null;

        if (!getterCache.TryGetValue(type, out var typeCache))
        {
            typeCache = new Dictionary<string, Func<object, object?>?>(StringComparer.Ordinal);
            getterCache[type] = typeCache;
        }

        if (!typeCache.TryGetValue(propertyName, out var getter))
        {
            var propInfo = GetCachedProperty(type, propertyName);
            if (propInfo != null && propInfo.CanRead)
            {
                var param = System.Linq.Expressions.Expression.Parameter(typeof(object), "item");
                var castParam = System.Linq.Expressions.Expression.Convert(param, type);
                var propertyAccess = System.Linq.Expressions.Expression.Property(castParam, propInfo);
                var castResult = System.Linq.Expressions.Expression.Convert(propertyAccess, typeof(object));
                getter = System.Linq.Expressions.Expression.Lambda<Func<object, object?>>(castResult, param).Compile();
            }
            else
            {
                getter = null;
            }
            typeCache[propertyName] = getter;
        }

        return getter;
    }

    private bool MatchesFilters(object item)
    {
        var type = item.GetType();
        foreach (var filter in columnFilters)
        {
            var getter = GetCachedGetter(type, filter.Key);
            var value = getter?.Invoke(item)?.ToString() ?? string.Empty;
            if (!value.Contains(filter.Value, StringComparison.CurrentCultureIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }

    internal string? GetItemPropertyValue(object item, string propertyName)
    {
        if (item == null || string.IsNullOrEmpty(propertyName)) return null;
        var getter = GetCachedGetter(item.GetType(), propertyName);
        return getter?.Invoke(item)?.ToString();
    }
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


