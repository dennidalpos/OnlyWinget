using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using System.ComponentModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class UpdatesPage : UserControl
{
    private bool initialRefreshStarted;
    private bool isRefreshing;
    private bool wasBusy;
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
        PageState.Present(viewModel.PageState);
        LoadingRing.IsActive = viewModel.IsLoading || viewModel.IsExecuting;
        LoadingRing.Visibility = viewModel.IsLoading || viewModel.IsExecuting ? Visibility.Visible : Visibility.Collapsed;
        ApplyOperationProgress();
        SelectAllUpdatesBox.IsThreeState = true;
        SelectAllUpdatesBox.IsChecked = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };
        CommandBar.SetCommands(viewModel.Commands.Values);
        isRefreshing = false;
    }

    private void ApplyText()
    {
        SelectAllUpdatesBox.Content = TextResources.Get("Command_Select_All");
        UpdatesNameHeader.Text = TextResources.Get("Header_Name");
        UpdatesPackageIdHeader.Text = TextResources.Get("Header_PackageId");
        UpdatesSourceHeader.Text = TextResources.Get("Header_Source");
        UpdatesInstalledHeader.Text = TextResources.Get("Header_Installed");
        UpdatesAvailableHeader.Text = TextResources.Get("Header_Available");
        UpdatesArchitectureHeader.Text = TextResources.Get("Header_Architecture");
        UpdatesStatusHeader.Text = TextResources.Get("Header_Status");
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        switch (args.Command.Id)
        {
            case UiCommandId.RefreshUpdates: await RefreshUpdatesAsync(); break;
            case UiCommandId.ApplyUpdates: await viewModel.ApplyAsync(); break;
            case UiCommandId.CancelOperation: viewModel.Cancel(); break;
        }
    }

    private Task RefreshUpdatesAsync()
    {
        return viewModel.RefreshAsync(CancellationToken.None);
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
        var busy = viewModel.IsLoading || viewModel.IsExecuting;
        if (busy)
        {
            OperationStatus.Show(TextResources.Get("Operation_Updates_Title"), viewModel.IsLoading ? TextResources.Get("Progress_LoadingUpdates") : TextResources.Get(viewModel.Progress is null ? "Progress_Starting" : $"Progress_{viewModel.Progress.Phase}"), viewModel.Progress?.PackageId, viewModel.Progress?.Percentage, viewModel.IsExecuting);
        }
        else if (wasBusy)
        {
            var error = App.Workflow.State.UserVisibleError;
            OperationStatus.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasBusy = busy;
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => viewModel.Cancel();

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
