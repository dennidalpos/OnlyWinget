using OnlyWinget.Application.Presentation;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OnlyWinget.Features.Home;

public sealed class DashboardViewModel(Action<Action> dispatch) : FeatureViewModel(App.Workflow, dispatch)
{
    private string status = string.Empty;

    public ObservableCollection<DashboardMetric> Metrics { get; } = [new(), new(), new(), new(), new()];
    public ObservableCollection<ActivityRow> RecentActivity { get; } = [];
    public string Status { get => status; private set => SetProperty(ref status, value); }

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Dashboard;
        Metrics[0].Value = state.IsWingetAvailable switch
        {
            true => TextResources.Get("Dashboard_Winget_Available"),
            false => TextResources.Get("Dashboard_Winget_Unavailable"),
            _ => TextResources.Get("Dashboard_Winget_Unknown")
        };
        Metrics[1].Value = state.PresetCount.ToString(CultureInfo.CurrentCulture);
        Metrics[2].Value = state.SearchResultCount.ToString(CultureInfo.CurrentCulture);
        Metrics[3].Value = state.UpdateCount.ToString(CultureInfo.CurrentCulture);
        Metrics[4].Value = state.SourceCount.ToString(CultureInfo.CurrentCulture);
        RecentActivity.ReplaceWith(state.RecentActivity);
        Status = state.Error ?? (state.RecentActivity.Count == 0 ? TextResources.Get("Empty_Activity") : string.Empty);
    }
}

public sealed class DashboardMetric : ObservableObject
{
    private string value = string.Empty;
    private string label = string.Empty;

    public string Value { get => value; set => SetProperty(ref this.value, value); }
    public string Label { get => label; set => SetProperty(ref label, value); }
}
