using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using System.ComponentModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class UpdatesPage : Page
{
    private bool initialRefreshStarted;
    private bool isRefreshing;
    private readonly WingetUpdatesViewModel viewModel;

    public UpdatesPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        UpdateList.ItemsSource = viewModel.Updates;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        viewModel.Activate();
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
        viewModel.Deactivate();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        isRefreshing = true;
        StatusText.Text = viewModel.Status;
        LoadingRing.IsActive = viewModel.IsLoading || viewModel.IsExecuting;
        LoadingRing.Visibility = viewModel.IsLoading || viewModel.IsExecuting ? Visibility.Visible : Visibility.Collapsed;
        DiscoveryProgressBar.Visibility = viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingStatusText.Visibility = viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        LoadingStatusText.Text = viewModel.IsLoading ? TextResources.Get("Progress_LoadingUpdates") : string.Empty;
        ApplyOperationProgress();
        SelectAllUpdatesBox.IsThreeState = true;
        SelectAllUpdatesBox.IsChecked = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };

        RefreshButton.IsEnabled = viewModel.IsEnabled(UiCommandId.RefreshUpdates);
        ApplySelectedButton.IsEnabled = viewModel.IsEnabled(UiCommandId.ApplyUpdates);
        CancelButton.IsEnabled = viewModel.IsEnabled(UiCommandId.CancelOperation);
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

    private Task RefreshUpdatesAsync()
    {
        return viewModel.RefreshAsync(CancellationToken.None);
    }

    private async void OnApplySelected(object sender, RoutedEventArgs args)
    {
        await viewModel.ApplyAsync();
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        viewModel.Cancel();
    }

    private void OnToggleAllUpdates(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        viewModel.ToggleAll();
    }

    private void OnUpdateSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not UpdateRow row)
        {
            return;
        }

        viewModel.Toggle(row);
    }

    private void ApplyOperationProgress()
    {
        OperationProgressBar.Visibility = viewModel.IsExecuting ? Visibility.Visible : Visibility.Collapsed;
        OperationProgressText.Visibility = viewModel.IsExecuting ? Visibility.Visible : Visibility.Collapsed;
        OperationProgressBar.IsIndeterminate = viewModel.IsExecuting && viewModel.Progress is null;
        OperationProgressBar.Value = viewModel.Progress?.Percentage ?? 0;
        OperationProgressText.Text = viewModel.ProgressText;
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
