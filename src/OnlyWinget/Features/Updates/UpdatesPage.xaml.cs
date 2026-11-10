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
        PageState.Present(ViewModel.PageState);
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

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WingetUpdatesViewModel.PageState))
        {
            PageState.Present(ViewModel.PageState);
        }
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
