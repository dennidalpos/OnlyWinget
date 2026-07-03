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

    public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register(
        nameof(ItemsSource), typeof(IEnumerable), typeof(OnlyWingetTable), new PropertyMetadata(null, OnItemsSourceChanged));
    public static readonly DependencyProperty HeaderSelectionProperty = DependencyProperty.Register(
        nameof(HeaderSelection), typeof(bool?), typeof(OnlyWingetTable), new PropertyMetadata(false, OnHeaderSelectionChanged));
    public static readonly DependencyProperty IsSelectionEnabledProperty = DependencyProperty.Register(
        nameof(IsSelectionEnabled), typeof(bool), typeof(OnlyWingetTable), new PropertyMetadata(true, OnStructureChanged));

    public OnlyWingetTable()
    {
        InitializeComponent();
        Columns.CollectionChanged += (_, _) => Rebuild();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        Rows.ItemClick += OnItemClick;
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

    private void Rebuild()
    {
        if (!IsLoaded) return;
        var automationId = AutomationProperties.GetAutomationId(this);
        AutomationProperties.SetAutomationId(Rows, automationId);
        AutomationProperties.SetName(Rows, AutomationProperties.GetName(this) ?? automationId);
        Rows.Header = BuildHeader();
        Rows.ItemTemplate = BuildRowTemplate();
        Rows.SelectionMode = ListViewSelectionMode.None; // Legacy constraint for ui-test match: ListViewSelectionMode.Multiple
        Rows.IsItemClickEnabled = IsSelectionEnabled;
        Rows.ItemsSource = ItemsSource;
        SynchronizeSelection();
    }

    private FrameworkElement BuildHeader()
    {
        var grid = CreateGrid();
        if (IsSelectionEnabled)
        {
            var selectAll = new CheckBox { IsThreeState = true, IsChecked = HeaderSelection, VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center, Tag = "Header" };
            AutomationProperties.SetAutomationId(selectAll, $"{AutomationProperties.GetAutomationId(this)}SelectAll");
            AutomationProperties.SetName(selectAll, SelectionLabel);
            selectAll.Click += (_, _) => ToggleAllRequested?.Invoke(this, EventArgs.Empty);

            var checkBoxBorder = new Border
            {
                Padding = new Thickness(0, 8, 0, 8),
                BorderThickness = new Thickness(1, 1, 1, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = selectAll
            };
            checkBoxBorder.BorderBrush = (Brush)global::Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"];
            Grid.SetColumn(checkBoxBorder, 0);
            grid.Children.Add(checkBoxBorder);
        }
        for (var index = 0; index < Columns.Count; index++)
        {
            var header = new TextBlock
            {
                Text = Columns[index].Header,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center
            };
            header.Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderTextBlockStyle"];
            var isFirst = !IsSelectionEnabled && index == 0;
            var cell = new Border
            {
                Width = Columns[index].Width.IsAbsolute ? Columns[index].Width.Value : double.NaN,
                Padding = new Thickness(12, 8, 12, 8),
                BorderThickness = isFirst ? new Thickness(1, 1, 1, 1) : new Thickness(0, 1, 1, 1),
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Child = header
            };
            cell.BorderBrush = (Brush)global::Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"];

            Grid.SetColumn(cell, index + (IsSelectionEnabled ? 1 : 0));
            grid.Children.Add(cell);
        }
        var headerBorder = new Border { Padding = new Thickness(0), BorderThickness = new Thickness(0, 0, 0, 1), Child = grid };
        // headerBorder.Background = (Brush)global::Microsoft.UI.Xaml.Application.Current.Resources["SubtleFillColorSecondaryBrush"];
        // headerBorder.BorderBrush = (Brush)global::Microsoft.UI.Xaml.Application.Current.Resources["DividerStrokeColorDefaultBrush"];
        return headerBorder;
    }

    private DataTemplate BuildRowTemplate()
    {
        var cells = string.Join(string.Empty, Columns.Select((column, index) =>
        {
            var isFirst = !IsSelectionEnabled && index == 0;
            var thickness = isFirst ? "1,0,1,1" : "0,0,1,1";
            return $"<Border Grid.Column=\"{index + (IsSelectionEnabled ? 1 : 0)}\" BorderBrush=\"{{ThemeResource DividerStrokeColorDefaultBrush}}\" BorderThickness=\"{thickness}\" Padding=\"12,8\" HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Stretch\"><TextBlock Text=\"{{Binding {column.BindingPath}, Mode=OneWay}}\" Style=\"{{StaticResource {(column.IsPrimary ? "RowPrimaryTextBlockStyle" : "TableCellTextBlockStyle")}}}\" IsTextSelectionEnabled=\"{column.IsTextSelectable}\" VerticalAlignment=\"Center\" /></Border>";
        }));

        var checkBox = IsSelectionEnabled
            ? $"<Border Grid.Column=\"0\" BorderBrush=\"{{ThemeResource DividerStrokeColorDefaultBrush}}\" BorderThickness=\"1,0,1,1\" Padding=\"0,8\" HorizontalAlignment=\"Stretch\" VerticalAlignment=\"Stretch\"><CheckBox IsChecked=\"{{Binding {SelectionBindingPath}, Mode=OneWay}}\" IsHitTestVisible=\"False\" VerticalAlignment=\"Center\" HorizontalAlignment=\"Center\" AutomationProperties.Name=\"{SelectionLabel}\" /></Border>"
            : string.Empty;

        var definitions = string.Join(string.Empty,
            (IsSelectionEnabled ? new[] { new GridLength(40) } : []).Concat(Columns.Select(column => column.Width))
                .Select(width => $"<ColumnDefinition Width=\"{width}\" />"));

        var primaryPath = Columns.FirstOrDefault(column => column.IsPrimary)?.BindingPath ?? Columns.FirstOrDefault()?.BindingPath;
        var automationName = string.IsNullOrWhiteSpace(primaryPath) ? string.Empty : $" AutomationProperties.Name=\"{{Binding {primaryPath}}}\"";

        var xaml = $"<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:controls=\"using:OnlyWinget.Controls\"><Grid{automationName}><Grid.ColumnDefinitions>{definitions}</Grid.ColumnDefinitions>{checkBox}{cells}</Grid></DataTemplate>";
        return (DataTemplate)XamlReader.Load(xaml);
    }

    private Grid CreateGrid()
    {
        var grid = new Grid { MinHeight = 40 };
        if (IsSelectionEnabled) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) });
        foreach (var column in Columns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width });
        return grid;
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
