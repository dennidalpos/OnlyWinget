using OnlyWinget.Application.Presentation;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OnlyWinget.Features.Home;

public sealed class DashboardViewModel : FeatureViewModel
{
    private FeatureState pageState = FeatureState.Ready;
    private string activePreset = string.Empty;
    private string operationalStatus = string.Empty;
    private bool hasWarning;

    public DashboardViewModel(Action<Action> dispatch) : base(App.Workflow, dispatch)
    {
    }

    public ObservableCollection<DashboardMetric> Metrics { get; } = [new(), new(), new(), new(), new(), new()];
    public ObservableCollection<ActivityRow> RecentActivity { get; } = [];
    public FeatureState PageState { get => pageState; private set => SetProperty(ref pageState, value); }
    public string ActivePreset { get => activePreset; private set => SetProperty(ref activePreset, value); }
    public string OperationalStatus { get => operationalStatus; private set => SetProperty(ref operationalStatus, value); }
    public bool HasWarning { get => hasWarning; private set => SetProperty(ref hasWarning, value); }
    public bool IsBusy => OperationalStatus == TextResources.Get("Dashboard_Busy");
    public string ActivePresetDisplay => $"{TextResources.Get("Dashboard_ActivePreset")}: {ActivePreset}";

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Dashboard;
        Metrics[0].AccentKey = "AreaHomeBrush";
        Metrics[1].AccentKey = "AreaPackagesBrush";
        Metrics[2].AccentKey = "AreaHomeBrush";
        Metrics[3].AccentKey = "AreaUpdatesBrush";
        Metrics[4].AccentKey = "AreaSourcesBrush";
        Metrics[5].AccentKey = "AreaActivityBrush";

        Metrics[0].Label = TextResources.Get("Dashboard_Winget");
        Metrics[1].Label = TextResources.Get("Dashboard_Presets");
        Metrics[2].Label = TextResources.Get("Dashboard_SearchResults");
        Metrics[3].Label = TextResources.Get("Dashboard_Updates");
        Metrics[4].Label = TextResources.Get("Dashboard_Sources");
        Metrics[5].Label = TextResources.Get("Dashboard_WindowsUpdates");

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
        Metrics[5].Value = state.WindowsUpdateCount.ToString(CultureInfo.CurrentCulture);
        ActivePreset = state.ActivePresetName ?? TextResources.Get("Dashboard_NoActivePreset");
        HasWarning = state.RebootRequired || state.IsWingetAvailable == false || state.IsWindowsUpdateAvailable == false;
        OperationalStatus = state.RebootRequired
            ? TextResources.Get("Dashboard_RebootWarning")
            : state.IsBusy ? TextResources.Get("Dashboard_Busy") : TextResources.Get("Dashboard_Ready");
        RecentActivity.ReplaceWith(state.RecentActivity);
        PageState = state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.IsWingetAvailable == false
                ? FeatureState.Unavailable(TextResources.Get("Dashboard_Winget_Unavailable"))
                : state.RecentActivity.Count == 0
                    ? FeatureState.Empty(TextResources.Get("Empty_Activity"))
                    : FeatureState.Ready;

        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(ActivePresetDisplay));
    }
}

public sealed class DashboardMetric : ObservableObject
{
    private string value = string.Empty;
    private string label = string.Empty;
    private string accentKey = "AreaHomeBrush";

    public string Value { get => value; set => SetProperty(ref this.value, value); }
    public string Label { get => label; set => SetProperty(ref label, value); }
    public string AccentKey { get => accentKey; set => SetProperty(ref accentKey, value); }
}
