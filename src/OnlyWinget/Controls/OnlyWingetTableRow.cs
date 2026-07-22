using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace OnlyWinget.Controls;

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
                Background = new SolidColorBrush(Microsoft.UI.Colors.Transparent),
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

            if (col.IsTextSelectable)
            {
                border.AddHandler(UIElement.PointerPressedEvent, new PointerEventHandler((sender, args) =>
                {
                    var parent = FindParentTable();
                    if (parent != null && DataContext != null)
                    {
                        var pointerPoint = args.GetCurrentPoint(border);
                        if (pointerPoint.Properties.IsLeftButtonPressed)
                        {
                            parent.ToggleItemSelection(DataContext);
                        }
                    }
                }), true);
            }

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
            var val = parentTable.GetItemPropertyValue(DataContext, propName);
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

public sealed class OnlyWingetTableBatchSelectionEventArgs(IReadOnlyList<object> items, bool isSelected) : EventArgs
{
    public IReadOnlyList<object> Items { get; } = items;
    public bool IsSelected { get; } = isSelected;
}

public sealed class OnlyWingetTablePasteEventArgs(string text) : EventArgs
{
    public string Text { get; } = text;
}
