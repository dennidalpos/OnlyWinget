using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Controls;
using OnlyWinget.DesignSystem.Commands;
using System.ComponentModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class WindowsUpdateHistoryPage : UserControl
{
    private bool initialLoadStarted;

    public WindowsUpdateHistoryViewModel ViewModel { get; }

    public WindowsUpdateHistoryPage()
    {
        ViewModel = new(Dispatch);
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        PageState.CancelRequested += (_, _) => ViewModel.Cancel();
        Loaded += OnLoaded;
        Unloaded += (_, _) => ViewModel.Deactivate();
        // The hub creates every tab up front, so Loaded fires while this tab is hidden and the app may still be
        // checking capabilities or refreshing winget. Load when the tab is actually shown and the app is idle.
        RegisterPropertyChangedCallback(VisibilityProperty, (_, _) => _ = TryAutoLoadAsync());
    }

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Activate();
        PageState.Present(ViewModel.PageState);
        ViewModel.Workflow.RefreshRestartPending();
        await TryAutoLoadAsync();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(WindowsUpdateHistoryViewModel.PageState))
        {
            PageState.Present(ViewModel.PageState);
            // PageState changes whenever the shared app state does, including when it becomes idle.
            _ = TryAutoLoadAsync();
        }
    }

    private async Task TryAutoLoadAsync()
    {
        if (initialLoadStarted || Visibility != Visibility.Visible || !IsLoaded || ViewModel.HasLoaded)
        {
            return;
        }

        var state = ViewModel.Workflow.State;
        if (!state.Capabilities.CanUseWindowsUpdate || state.BusyState != OnlyWinget.Application.App.ApplicationBusyState.Idle)
        {
            return;
        }

        initialLoadStarted = true;
        await ViewModel.LoadAsync();
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        var command = args.Command;
        if (command.ConfirmationResourceKey is { } confirmation && XamlRoot is { } root &&
            !await App.UiServices.Confirmation.ConfirmAsync(root, command.LabelResourceKey, confirmation))
        {
            return;
        }

        switch (command.Id)
        {
            case UiCommandId.LoadWindowsUpdateHistory: await ViewModel.LoadAsync(); break;
            case UiCommandId.UninstallWindowsUpdates: await ViewModel.UninstallAsync(); break;
            case UiCommandId.RestartWindows: await ViewModel.RestartAsync(); break;
            case UiCommandId.CancelOperation: ViewModel.Cancel(); break;
        }
    }

    private void OnGroupFilterChanged(object sender, SelectionChangedEventArgs args)
    {
        var tag = (GroupFilter.SelectedItem as ComboBoxItem)?.Tag as string;
        WindowsUpdateClassification? group = Enum.TryParse<WindowsUpdateClassification>(tag, out var parsed) ? parsed : null;
        ViewModel.SetGroupFilter(group);
    }

    private void OnToggleAll(object? sender, EventArgs args) => ViewModel.ToggleAll();

    private void OnBatchSelectionChanged(object? sender, OnlyWingetTableBatchSelectionEventArgs args) =>
        ViewModel.SetSelected(args.Items.OfType<WindowsUpdateHistoryDisplayRow>(), args.IsSelected);

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
