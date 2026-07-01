using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System.ComponentModel;

namespace OnlyWinget.Features.Home;

public sealed partial class DashboardPage : Page
{
    private readonly DashboardViewModel viewModel;

    public DashboardPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        MetricRepeater.ItemsSource = viewModel.Metrics;
        ActivityList.ItemsSource = viewModel.RecentActivity;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => viewModel.Activate();
    private void OnUnloaded(object sender, RoutedEventArgs args) => viewModel.Deactivate();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        RefreshOverview();
        if (args.PropertyName == nameof(DashboardViewModel.PageState))
        {
            PageState.Present(viewModel.PageState);
        }
    }

    private void ApplyText()
    {
        Scaffold.Title = TextResources.Get("Dashboard_Title");
        Scaffold.Subtitle = "OnlyWinget";
        viewModel.Metrics[0].Label = TextResources.Get("Dashboard_Winget");
        viewModel.Metrics[1].Label = TextResources.Get("Dashboard_Presets");
        viewModel.Metrics[2].Label = TextResources.Get("Dashboard_SearchResults");
        viewModel.Metrics[3].Label = TextResources.Get("Dashboard_Updates");
        viewModel.Metrics[4].Label = TextResources.Get("Dashboard_Sources");
        viewModel.Metrics[5].Label = TextResources.Get("Dashboard_WindowsUpdates");
        RecentActivityText.Text = TextResources.Get("Dashboard_RecentActivity");
        OpenPackagesButton.Content = TextResources.Get("Dashboard_OpenPackages");
        OpenUpdatesButton.Content = TextResources.Get("Dashboard_OpenUpdates");
        RefreshOverview();
    }

    private void RefreshOverview()
    {
        ActivePresetText.Text = $"{TextResources.Get("Dashboard_ActivePreset")}: {viewModel.ActivePreset}";
        OperationalStatus.Message = viewModel.OperationalStatus;
        OperationalStatus.Severity = viewModel.HasWarning ? InfoBarSeverity.Warning : InfoBarSeverity.Success;
    }

    private void OnOpenPackages(object sender, RoutedEventArgs args) => App.Navigate("packages");
    private void OnOpenUpdates(object sender, RoutedEventArgs args) => App.Navigate("updates");

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(() => action());
        }
    }
}
