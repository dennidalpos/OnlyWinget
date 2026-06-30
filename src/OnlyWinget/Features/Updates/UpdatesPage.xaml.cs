using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.DesignSystem;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class UpdatesPage : Page
{
    private CancellationTokenSource? operationCancellation;
    private bool initialRefreshStarted;
    private bool isRefreshing;
    private readonly ObservableCollection<UpdateRow> updates = [];

    public UpdatesPage()
    {
        InitializeComponent();
        UpdateList.ItemsSource = updates;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.Workflow.StateChanged += OnWorkflowChanged;
        Refresh();
        if (!initialRefreshStarted &&
            App.Workflow.State.Updates.Count == 0 &&
            App.Workflow.State.BusyState == OnlyWinget.Application.App.ApplicationBusyState.Idle)
        {
            initialRefreshStarted = true;
            await RefreshUpdatesAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        App.Workflow.StateChanged -= OnWorkflowChanged;
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => PageUi.RefreshOnUiThread(this, Refresh);

    private void Refresh()
    {
        isRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Updates;
        var commands = state.Commands.ToDictionary(command => command.Id);

        var localizedUpdates = state.Updates.Select(row => row with
        {
            Architecture = TextResources.Get(row.Architecture),
            Status = TextResources.Get(row.Status ?? "Update_Status_Available")
        });
        PageUi.SynchronizeItems(updates, localizedUpdates, PackageKey);
        PageUi.ApplyStatus(
            StatusText,
            state.Error,
            state.IsLoading ? string.Empty : TextResources.Get("Empty_Updates"),
            state.Updates.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsLoading || state.IsExecuting);
        PageUi.SetVisible(DiscoveryProgressBar, state.IsLoading);
        PageUi.SetVisible(LoadingStatusText, state.IsLoading);
        LoadingStatusText.Text = state.IsLoading ? TextResources.Get("Progress_LoadingUpdates") : string.Empty;
        ApplyOperationProgress(state.IsExecuting);
        PageUi.ApplySelectionHeader(SelectAllUpdatesBox, state.HeaderState);

        PageUi.SetEnabled(RefreshButton, commands, UiCommandId.RefreshUpdates);
        PageUi.SetEnabled(ApplySelectedButton, commands, UiCommandId.ApplyUpdates);
        PageUi.SetEnabled(CancelButton, commands, UiCommandId.CancelOperation);
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Updates_Title");
        SelectAllUpdatesBox.Content = TextResources.Get("Command_Select_All");
        RefreshButton.Content = TextResources.Get("Command_Updates_Refresh");
        ApplySelectedButton.Content = TextResources.Get("Command_Updates_ApplySelected");
        CancelButton.Content = TextResources.Get("Command_Operation_Cancel");
        UpdatesNameHeader.Text = TextResources.Get("Header_Name");
        UpdatesPackageIdHeader.Text = TextResources.Get("Header_PackageId");
        UpdatesSourceHeader.Text = TextResources.Get("Header_Source");
        UpdatesInstalledHeader.Text = TextResources.Get("Header_Installed");
        UpdatesAvailableHeader.Text = TextResources.Get("Header_Available");
        UpdatesArchitectureHeader.Text = TextResources.Get("Header_Architecture");
        UpdatesStatusHeader.Text = TextResources.Get("Header_Status");
    }

    private async void OnRefreshUpdates(object sender, RoutedEventArgs args)
    {
        await RefreshUpdatesAsync();
    }

    private static Task RefreshUpdatesAsync()
    {
        return PageUi.RunWorkflowAsync(() => App.Workflow.RefreshUpdatesAsync(CancellationToken.None));
    }

    private async void OnApplySelected(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        try
        {
            await App.Workflow.ApplySelectedUpdatesAsync(operationCancellation.Token);
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Cancel();
    }

    private void OnToggleAllUpdates(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        App.Workflow.ToggleAllUpdates();
    }

    private void OnUpdateSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not UpdateRow row)
        {
            return;
        }

        App.Workflow.ToggleUpdate(new PackageIdentity(row.PackageId, row.Source));
    }

    private void ApplyOperationProgress(bool isExecuting)
    {
        var progress = App.Workflow.State.OperationProgress;
        PageUi.SetVisible(OperationProgressBar, isExecuting);
        PageUi.SetVisible(OperationProgressText, isExecuting);
        OperationProgressBar.IsIndeterminate = isExecuting &&
            (progress is null || progress.Phase == OnlyWinget.Application.Winget.WingetProgressPhase.Starting);
        OperationProgressBar.Value = progress?.Percentage ?? 0;
        OperationProgressText.Text = progress is null
            ? TextResources.Get("Progress_Starting")
            : $"{TextResources.Get($"Progress_{progress.Phase}")} · {progress.Percentage}% · {progress.PackageId}";
    }

    private static string PackageKey(UpdateRow row) =>
        $"{row.Source?.ToUpperInvariant() ?? string.Empty}|{row.PackageId.ToUpperInvariant()}";
}
