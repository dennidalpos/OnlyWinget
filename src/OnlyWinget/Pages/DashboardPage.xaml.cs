using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace OnlyWinget.Pages;

public sealed partial class DashboardPage : Page
{
    private readonly ObservableCollection<ActivityRow> recentActivity = [];
    private readonly ObservableCollection<DashboardMetric> metrics = [new(), new(), new(), new(), new()];

    public DashboardPage()
    {
        InitializeComponent();
        MetricRepeater.ItemsSource = metrics;
        ActivityList.ItemsSource = recentActivity;
        PageUi.RouteVerticalMouseWheel(PageScroller);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.Workflow.StateChanged += OnWorkflowChanged;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        App.Workflow.StateChanged -= OnWorkflowChanged;
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => PageUi.RefreshOnUiThread(this, Refresh);

    private void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Dashboard;
        metrics[0].Value = state.IsWingetAvailable switch
        {
            true => TextResources.Get("Dashboard_Winget_Available"),
            false => TextResources.Get("Dashboard_Winget_Unavailable"),
            _ => TextResources.Get("Dashboard_Winget_Unknown")
        };
        metrics[1].Value = state.PresetCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        metrics[2].Value = state.SearchResultCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        metrics[3].Value = state.UpdateCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        metrics[4].Value = state.SourceCount.ToString(System.Globalization.CultureInfo.CurrentCulture);
        PageUi.ReplaceItems(recentActivity, state.RecentActivity);
        StatusText.Text = state.Error ?? (state.RecentActivity.Count == 0 ? TextResources.Get("Empty_Activity") : string.Empty);
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Dashboard_Title");
        SummaryText.Text = "OnlyWinget";
        metrics[0].Label = TextResources.Get("Dashboard_Winget");
        metrics[1].Label = TextResources.Get("Dashboard_Presets");
        metrics[2].Label = TextResources.Get("Dashboard_SearchResults");
        metrics[3].Label = TextResources.Get("Dashboard_Updates");
        metrics[4].Label = TextResources.Get("Dashboard_Sources");
        RecentActivityText.Text = TextResources.Get("Dashboard_RecentActivity");
    }
}

public sealed class DashboardMetric : INotifyPropertyChanged
{
    private string value = string.Empty;
    private string label = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Value { get => value; set => Set(ref this.value, value); }
    public string Label { get => label; set => Set(ref label, value); }

    private void Set(ref string field, string value, [CallerMemberName] string? propertyName = null)
    {
        if (field == value)
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
