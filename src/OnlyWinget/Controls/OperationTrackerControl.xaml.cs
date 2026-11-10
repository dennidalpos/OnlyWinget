using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Winget;

namespace OnlyWinget.Controls;

public sealed partial class OperationTrackerControl : UserControl
{
    private readonly OnlyWingetApplication workflow;
    private DispatcherTimer? autoDismissTimer;

    public OperationTrackerControl() : this(null) { }

    public OperationTrackerControl(OnlyWingetApplication? workflow = null)
    {
        this.workflow = workflow ?? App.Workflow;
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        workflow.StateChanged += OnStateChanged;
        UpdateState(workflow.State);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        workflow.StateChanged -= OnStateChanged;
        StopAutoDismissTimer();
    }

    private void OnStateChanged(object? sender, EventArgs e)
    {
        DispatcherQueue.TryEnqueue(() => UpdateState(workflow.State));
    }

    public void UpdateState(OnlyWingetState state)
    {
        var isBusy = state.BusyState != ApplicationBusyState.Idle;
        var progress = state.OperationProgress;

        if (!isBusy && progress == null)
        {
            if (autoDismissTimer is null || !autoDismissTimer.IsEnabled)
            {
                TrackerRoot.Visibility = Visibility.Collapsed;
            }
            return;
        }

        // Active operation or busy state -> ensure visible and cancel pending hide timers
        StopAutoDismissTimer();
        TrackerRoot.Visibility = Visibility.Visible;

        if (progress != null)
        {
            var formattedDetail = OperationProgressFormatter.FormatMessage(progress, TextResources.Get);
            StatusDetail.Text = string.IsNullOrWhiteSpace(formattedDetail)
                ? FormatBusyStateText(state.BusyState)
                : formattedDetail;

            if (progress.Phase == WingetProgressPhase.Completed)
            {
                CancelOperationButton.Visibility = Visibility.Collapsed;
                StatusRing.IsActive = false;
                StatusRing.Visibility = Visibility.Collapsed;
                StatusIcon.Glyph = "\uE73E"; // Checkmark
                StatusIcon.Foreground = GetThemeBrush("SystemFillColorSuccessBrush");
                StatusIcon.Visibility = Visibility.Visible;

                StatusTitle.Text = TextResources.Get("Progress_Completed");
                OperationProgressBar.IsIndeterminate = false;
                OperationProgressBar.Value = 100;
                PercentageText.Text = "100%";

                StartAutoDismissTimer(2500);
                return;
            }

            if (progress.Phase == WingetProgressPhase.Failed)
            {
                CancelOperationButton.Visibility = Visibility.Collapsed;
                StatusRing.IsActive = false;
                StatusRing.Visibility = Visibility.Collapsed;
                StatusIcon.Glyph = "\uE783"; // Error badge
                StatusIcon.Foreground = GetThemeBrush("SystemFillColorCriticalBrush");
                StatusIcon.Visibility = Visibility.Visible;

                StatusTitle.Text = TextResources.Get("Progress_Failed");
                OperationProgressBar.IsIndeterminate = false;
                PercentageText.Text = string.Empty;

                StartAutoDismissTimer(4000);
                return;
            }

            // Normal active operation
            CancelOperationButton.Visibility = Visibility.Visible;
            StatusIcon.Visibility = Visibility.Collapsed;
            StatusRing.Visibility = Visibility.Visible;
            StatusRing.IsActive = true;

            var total = progress.TotalPackages;
            if (total > 1)
            {
                var current = (progress.CompletedPackages >= total)
                    ? total
                    : Math.Clamp(progress.CompletedPackages + 1, 1, total);
                StatusTitle.Text = $"{TextResources.Get("Tracker_OperationInProgress")} ({current}/{total})";
            }
            else
            {
                StatusTitle.Text = TextResources.Get("Tracker_OperationInProgress");
            }

            if (progress.Percentage > 0)
            {
                OperationProgressBar.IsIndeterminate = false;
                var clampedPct = Math.Clamp(progress.Percentage, 0, 100);
                OperationProgressBar.Value = clampedPct;
                PercentageText.Text = $"{clampedPct}%";
            }
            else
            {
                OperationProgressBar.IsIndeterminate = true;
                PercentageText.Text = string.Empty;
            }
        }
        else
        {
            // Busy state without detailed progress (e.g., searching, loading updates, managing sources)
            CancelOperationButton.Visibility = Visibility.Visible;
            StatusIcon.Visibility = Visibility.Collapsed;
            StatusRing.Visibility = Visibility.Visible;
            StatusRing.IsActive = true;

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

    private static Brush GetThemeBrush(string key) =>
        Microsoft.UI.Xaml.Application.Current.Resources.TryGetValue(key, out var res) && res is Brush brush
            ? brush
            : new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    private void StartAutoDismissTimer(int milliseconds)
    {
        StopAutoDismissTimer();
        autoDismissTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(milliseconds)
        };
        autoDismissTimer.Tick += (s, e) =>
        {
            StopAutoDismissTimer();
            TrackerRoot.Visibility = Visibility.Collapsed;
        };
        autoDismissTimer.Start();
    }

    private void StopAutoDismissTimer()
    {
        if (autoDismissTimer != null)
        {
            autoDismissTimer.Stop();
            autoDismissTimer = null;
        }
    }

    private void OnCancelOperationClick(object sender, RoutedEventArgs e)
    {
        workflow.CancelCurrentOperation();
    }

    private void OnViewActivityClick(object sender, RoutedEventArgs e)
    {
        App.Navigate("activity");
    }
}
