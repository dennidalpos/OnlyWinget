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
    private bool wasBusy;
    public WingetUpdatesViewModel ViewModel { get; }

    public UpdatesPage()
    {
        ViewModel = new(Dispatch);
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
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
            OperationStatus.Show(TextResources.Get("Operation_Updates_Title"), ViewModel.IsLoading ? TextResources.Get("Progress_LoadingUpdates") : TextResources.Get(ViewModel.Progress is null ? "Progress_Starting" : $"Progress_{ViewModel.Progress.Phase}"), ViewModel.Progress?.PackageId, ViewModel.Progress?.Percentage, ViewModel.IsExecuting);
        }
        else if (wasBusy)
        {
            var error = ViewModel.Error;
            OperationStatus.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasBusy = busy;
    }

    private void OnUpdateSelectionToggled(object? sender, OnlyWingetTableSelectionEventArgs args)
    {
        if (args.Item is UpdateRow row) ViewModel.Toggle(row);
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => ViewModel.Cancel();

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
