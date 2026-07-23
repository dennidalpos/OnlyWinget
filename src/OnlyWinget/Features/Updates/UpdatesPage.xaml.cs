using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using OnlyWinget.Presentation;
using System.ComponentModel;
using System.Linq;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Features.Updates;

public sealed partial class UpdatesPage : UserControl
{
    private bool initialRefreshStarted;
    private bool wasBusy;
    public WingetUpdatesViewModel ViewModel { get; }

    public UpdatesPage()
    {
        ViewModel = new(Dispatch);
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        PageState.CancelRequested += OnOperationCancelRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Activate();
        if (!initialRefreshStarted &&
            ViewModel.ShouldInitialRefresh)
        {
            initialRefreshStarted = true;
            await RefreshUpdatesAsync();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Deactivate();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        PageState.Present(ViewModel.PageState);
        ApplyOperationProgress();
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        switch (args.Command.Id)
        {
            case UiCommandId.RefreshUpdates: await RefreshUpdatesAsync(); break;
            case UiCommandId.ApplyUpdates: await ViewModel.ApplyAsync(); break;
            case UiCommandId.CancelOperation: ViewModel.Cancel(); break;
        }
    }

    private Task RefreshUpdatesAsync()
    {
        return ViewModel.RefreshAsync();
    }

    private void OnToggleAllUpdates(object? sender, EventArgs args)
    {
        ViewModel.ToggleAll();
    }

    private void ApplyOperationProgress()
    {
        var busy = ViewModel.IsBusy;
        if (busy)
        {
            var message = ViewModel.IsLoading
                ? TextResources.Get("Progress_LoadingUpdates")
                : OperationProgressFormatter.FormatMessage(ViewModel.Progress, TextResources.Get);

            PageState.Show(TextResources.Get("Operation_Updates_Title"), message, ViewModel.Progress?.PackageId, ViewModel.Progress?.PackagePercentage, ViewModel.IsExecuting);
        }
        else if (wasBusy)
        {
            var error = ViewModel.Error;
            PageState.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasBusy = busy;
    }

    private void OnUpdateBatchSelectionChanged(object? sender, OnlyWingetTableBatchSelectionEventArgs args)
    {
        var rows = args.Items.OfType<UpdateRow>();
        ViewModel.SetSelected(rows, args.IsSelected);
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => ViewModel.Cancel();

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
