using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Controls;

public sealed partial class OperationTrackerControl : UserControl
{
    public OperationTrackerControl()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.Workflow.StateChanged += OnStateChanged;
        UpdateState(App.Workflow.State);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.Workflow.StateChanged -= OnStateChanged;
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => UpdateState(App.Workflow.State));
    }

    public void UpdateState(OnlyWingetState state)
    {
        var isBusy = state.BusyState != ApplicationBusyState.Idle;
        var progress = state.OperationProgress;

        if (!isBusy && progress == null)
        {
            TrackerRoot.Visibility = Visibility.Collapsed;
            return;
        }

        TrackerRoot.Visibility = Visibility.Visible;

        if (progress != null)
        {
            var formattedDetail = OperationProgressFormatter.FormatMessage(progress, TextResources.Get);
            StatusDetail.Text = string.IsNullOrWhiteSpace(formattedDetail)
                ? FormatBusyStateText(state.BusyState)
                : formattedDetail;

            if (progress.Percentage > 0)
            {
                OperationProgressBar.IsIndeterminate = false;
                OperationProgressBar.Value = Math.Clamp(progress.Percentage, 0, 100);
                PercentageText.Text = $"{progress.Percentage}%";
            }
            else
            {
                OperationProgressBar.IsIndeterminate = true;
                PercentageText.Text = string.Empty;
            }

            if (progress.TotalPackages > 1)
            {
                StatusTitle.Text = $"{TextResources.Get("Tracker_OperationInProgress")} ({progress.CompletedPackages}/{progress.TotalPackages})";
            }
            else
            {
                StatusTitle.Text = TextResources.Get("Tracker_OperationInProgress");
            }
        }
        else
        {
            StatusTitle.Text = TextResources.Get("Tracker_OperationInProgress");
            StatusDetail.Text = FormatBusyStateText(state.BusyState);
            OperationProgressBar.IsIndeterminate = true;
            PercentageText.Text = string.Empty;
        }
    }

    private static string FormatBusyStateText(ApplicationBusyState busyState) => busyState switch
    {
        ApplicationBusyState.RefreshingUpdates => TextResources.Get("State_RefreshingUpdates"),
        ApplicationBusyState.ScanningWindowsUpdates => TextResources.Get("State_ScanningWindowsUpdates"),
        ApplicationBusyState.InstallingWindowsUpdates => TextResources.Get("State_InstallingWindowsUpdates"),
        ApplicationBusyState.ExecutingOperation => TextResources.Get("State_ExecutingOperation"),
        ApplicationBusyState.Searching => TextResources.Get("State_Searching"),
        ApplicationBusyState.ManagingSources => TextResources.Get("State_ManagingSources"),
        ApplicationBusyState.ValidatingPackages => TextResources.Get("State_ValidatingPackages"),
        _ => TextResources.Get("Tracker_Working")
    };

    private void OnViewActivityClick(object sender, RoutedEventArgs e)
    {
        App.Navigate("activity");
    }
}
