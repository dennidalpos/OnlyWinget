using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
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
        OperationResultList.ItemsSource = viewModel.OperationResults;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        viewModel.Activate();
        if (!initialRefreshStarted &&
            viewModel.ShouldInitialRefresh)
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
        OperationResultList.Visibility = viewModel.OperationResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        ApplyOperationProgress();
        UpdateList.HeaderSelection = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };
        CommandBar.SetCommands(viewModel.Commands.Values);
        isRefreshing = false;
    }

    private void ApplyText()
    {
        UpdateList.SelectionLabel = TextResources.Get("Command_Select_All");
        UpdateList.SetHeaders(new[] { "Header_Name", "Header_PackageId", "Header_Source", "Header_Installed", "Header_Available", "Header_Architecture", "Header_Status" }.Select(TextResources.Get).ToArray());
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

    private void OnToggleAllUpdates(object? sender, EventArgs args)
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
            var error = viewModel.Error;
            OperationStatus.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasBusy = busy;
    }

    private void OnUpdateSelectionToggled(object? sender, OnlyWingetTableSelectionEventArgs args)
    {
        if (args.Item is UpdateRow row) viewModel.Toggle(row);
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => viewModel.Cancel();

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
