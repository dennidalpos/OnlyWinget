using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Markup;
using System.Collections;
using System.Collections.ObjectModel;

namespace OnlyWinget.Controls;

[ContentProperty(Name = nameof(Columns))]
public sealed partial class OnlyWingetTable : UserControl
{
    private bool synchronizingSelection;
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
        Loaded += (_, _) => Rebuild();
        Rows.SelectionChanged += OnRowsSelectionChanged;
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

    private static void OnItemsSourceChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args) =>
        ((OnlyWingetTable)sender).Rows.ItemsSource = args.NewValue as IEnumerable;

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
        Rows.SelectionMode = IsSelectionEnabled ? ListViewSelectionMode.Multiple : ListViewSelectionMode.None;
        Rows.ItemsSource = ItemsSource;
        SynchronizeSelection();
    }

    private FrameworkElement BuildHeader()
    {
        var grid = CreateGrid();
        if (IsSelectionEnabled)
        {
            var selectAll = new CheckBox { IsThreeState = true, IsChecked = HeaderSelection, VerticalAlignment = VerticalAlignment.Center, Tag = "Header" };
            AutomationProperties.SetAutomationId(selectAll, $"{AutomationProperties.GetAutomationId(this)}SelectAll");
            AutomationProperties.SetName(selectAll, SelectionLabel);
            selectAll.Click += (_, _) => ToggleAllRequested?.Invoke(this, EventArgs.Empty);
            grid.Children.Add(selectAll);
        }
        for (var index = 0; index < Columns.Count; index++)
        {
            var header = new TextBlock
            {
                Text = Columns[index].Header,
                Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderTextBlockStyle"],
                HorizontalAlignment = HorizontalAlignment.Left,
                TextAlignment = TextAlignment.Left
            };
            var cell = new Border
            {
                Width = Columns[index].Width.IsAbsolute ? Columns[index].Width.Value : double.NaN,
                Margin = IsSelectionEnabled ? new Thickness(-44, 0, 44, 0) : new Thickness(0),
                Padding = new Thickness(0, 0, 16, 0),
                HorizontalAlignment = HorizontalAlignment.Left,
                Child = header
            };
            Grid.SetColumn(cell, index + (IsSelectionEnabled ? 1 : 0));
            grid.Children.Add(cell);
        }
        return new Border { Padding = new Thickness(12, 8, 12, 8), Style = (Style)Microsoft.UI.Xaml.Application.Current.Resources["TableHeaderSurfaceStyle"], Child = grid };
    }

    private DataTemplate BuildRowTemplate()
    {
        var cells = string.Join(string.Empty, Columns.Select((column, index) =>
            $"<TextBlock Grid.Column=\"{index + (IsSelectionEnabled ? 1 : 0)}\" Text=\"{{Binding {column.BindingPath}, Mode=OneWay}}\" Style=\"{{StaticResource {(column.IsPrimary ? "RowPrimaryTextBlockStyle" : "TableCellTextBlockStyle")}}}\" Margin=\"0,0,16,0\" IsTextSelectionEnabled=\"{column.IsTextSelectable}\" />"));
        var definitions = string.Join(string.Empty,
            (IsSelectionEnabled ? new[] { new GridLength(44) } : []).Concat(Columns.Select(column => column.Width))
                .Select(width => $"<ColumnDefinition Width=\"{width}\" />"));
        var primaryPath = Columns.FirstOrDefault(column => column.IsPrimary)?.BindingPath ?? Columns.FirstOrDefault()?.BindingPath;
        var automationName = string.IsNullOrWhiteSpace(primaryPath) ? string.Empty : $" AutomationProperties.Name=\"{{Binding {primaryPath}}}\"";
        var xaml = $"<DataTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\" xmlns:controls=\"using:OnlyWinget.Controls\"><Grid Padding=\"12,0\"{automationName}><Grid.ColumnDefinitions>{definitions}</Grid.ColumnDefinitions>{cells}<Border Grid.ColumnSpan=\"{Columns.Count + (IsSelectionEnabled ? 1 : 0)}\" Style=\"{{StaticResource TableRowDividerStyle}}\" /></Grid></DataTemplate>";
        return (DataTemplate)XamlReader.Load(xaml);
    }

    private Grid CreateGrid()
    {
        var grid = new Grid { MinHeight = 40 };
        if (IsSelectionEnabled) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(44) });
        foreach (var column in Columns) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = column.Width });
        return grid;
    }

    private void SynchronizeSelection()
    {
        if (Rows.Header is Border { Child: Grid grid } && grid.Children.FirstOrDefault() is CheckBox checkBox)
            checkBox.IsChecked = HeaderSelection;

        if (!IsSelectionEnabled || ItemsSource is null) return;
        synchronizingSelection = true;
        try
        {
            Rows.SelectedItems.Clear();
            foreach (var item in ItemsSource)
            {
                if (item?.GetType().GetProperty(SelectionBindingPath)?.GetValue(item) is true) Rows.SelectedItems.Add(item);
            }
        }
        finally { synchronizingSelection = false; }
    }

    private void OnRowsSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (synchronizingSelection) return;
        foreach (var item in args.AddedItems) SelectionToggled?.Invoke(this, new OnlyWingetTableSelectionEventArgs(item, true));
        foreach (var item in args.RemovedItems) SelectionToggled?.Invoke(this, new OnlyWingetTableSelectionEventArgs(item, false));
    }
}

public sealed class OnlyWingetTableSelectionEventArgs(object item, bool isSelected) : EventArgs
{
    public object Item { get; } = item;
    public bool IsSelected { get; } = isSelected;
}
