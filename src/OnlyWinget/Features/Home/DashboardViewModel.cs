using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.System;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Home;

public sealed partial class DashboardViewModel : FeatureViewModel
{
    private readonly IPcMetricsService metricsService;

    [ObservableProperty]
    private FeatureState pageState = FeatureState.Ready;

    [ObservableProperty]
    private string activePreset = string.Empty;

    [ObservableProperty]
    private string operationalStatus = string.Empty;

    [ObservableProperty]
    private bool hasWarning;

    public DashboardViewModel(Action<Action> dispatch, IPcMetricsService? pcMetricsService = null)
        : base(App.Workflow, dispatch)
    {
        metricsService = pcMetricsService ?? AppComposition.Host.Services.GetRequiredService<IPcMetricsService>();
    }

    public ObservableCollection<DashboardMetric> Metrics { get; } = [new(), new(), new(), new(), new(), new()];
    public bool IsBusy => OperationalStatus == TextResources.Get("Dashboard_Busy");
    public string ActivePresetDisplay => $"{TextResources.Get("Dashboard_ActivePreset")}: {ActivePreset}";

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Dashboard;
        var pcMetrics = metricsService.GetCurrentMetrics();

        Metrics[0].AccentKey = "AreaHomeBrush";
        Metrics[1].AccentKey = "AreaPackagesBrush";
        Metrics[2].AccentKey = "AreaHomeBrush";
        Metrics[3].AccentKey = "AreaUpdatesBrush";
        Metrics[4].AccentKey = "AreaSourcesBrush";
        Metrics[5].AccentKey = "AreaActivityBrush";

        Metrics[0].Label = TextResources.Get("Dashboard_Cpu");
        Metrics[0].Value = $"{pcMetrics.CpuUsagePercent:F1}%";

        Metrics[1].Label = TextResources.Get("Dashboard_Ram");
        Metrics[1].Value = pcMetrics.RamUsageText;

        Metrics[2].Label = TextResources.Get("Dashboard_Disk");
        Metrics[2].Value = pcMetrics.DiskUsageText;

        Metrics[3].Label = TextResources.Get("Dashboard_Uptime");
        Metrics[3].Value = pcMetrics.UptimeText;

        Metrics[4].Label = TextResources.Get("Dashboard_OsVersion");
        Metrics[4].Value = pcMetrics.OsVersionText;

        Metrics[5].Label = TextResources.Get("Dashboard_Network");
        Metrics[5].Value = pcMetrics.NetworkStatusText;

        ActivePreset = state.ActivePresetName ?? TextResources.Get("Dashboard_NoActivePreset");
        HasWarning = state.RebootRequired || state.IsWingetAvailable == false || state.IsWindowsUpdateAvailable == false;
        OperationalStatus = state.RebootRequired
            ? TextResources.Get("Dashboard_RebootWarning")
            : state.IsBusy ? TextResources.Get("Dashboard_Busy") : TextResources.Get("Dashboard_Ready");

        PageState = state.Error is not null
            ? FeatureState.Error(state.Error)
            : FeatureState.Ready;

        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(ActivePresetDisplay));
    }
}

public sealed partial class DashboardMetric : ObservableObject
{
    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string label = string.Empty;

    [ObservableProperty]
    private string accentKey = "AreaHomeBrush";
}
