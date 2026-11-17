using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using System.ComponentModel;
using System.Linq;

namespace OnlyWinget.Features.Updates;

public sealed partial class WindowsUpdatePage : UserControl
{
    public WindowsUpdatesViewModel ViewModel { get; }

    public WindowsUpdatePage()
    {
        ViewModel = new(Dispatch);
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        PageState.CancelRequested += OnOperationCancelRequested;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Activate();
        ViewModel.Workflow.RefreshRestartPending();
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Deactivate();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName is nameof(WindowsUpdatesViewModel.PageState) or
                                nameof(WindowsUpdatesViewModel.IsBusy))
        {
            Refresh();
        }
    }

    private void Refresh()
    {
        PageState.Present(ViewModel.PageState);
        SoftwareUpdatesBox.IsEnabled = !ViewModel.IsBusy;
        DriverUpdatesBox.IsEnabled = !ViewModel.IsBusy;
        MicrosoftUpdatesBox.IsEnabled = !ViewModel.IsBusy;
        OptionalUpdatesBox.IsEnabled = !ViewModel.IsBusy;
        ShowHiddenBox.IsEnabled = !ViewModel.IsBusy;
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
            case UiCommandId.ScanWindowsUpdates: await ViewModel.ScanAsync(CreateOptions()); break;
            case UiCommandId.InstallWindowsUpdates: await ViewModel.InstallAsync(CreateOptions()); break;
            case UiCommandId.DownloadWindowsUpdates: await ViewModel.InstallAsync(CreateOptions() with { DownloadOnly = true }); break;
            case UiCommandId.HideWindowsUpdates: await ViewModel.SetHiddenAsync(true, CreateOptions()); break;
            case UiCommandId.ShowWindowsUpdates: await ViewModel.SetHiddenAsync(false, CreateOptions()); break;
            case UiCommandId.RestartWindows: await ViewModel.RestartAsync(); break;
            case UiCommandId.CancelRestart: await ViewModel.CancelRestartAsync(); break;
            case UiCommandId.CancelOperation: ViewModel.Cancel(); break;
        }
    }

    private void OnToggleAllWindowsUpdates(object? sender, EventArgs args)
    {
        ViewModel.ToggleAll();
    }

    private WindowsUpdateOptions CreateOptions() => new(
        SoftwareUpdatesBox.IsChecked == true,
        DriverUpdatesBox.IsChecked == true,
        MicrosoftUpdatesBox.IsChecked == true,
        OptionalUpdatesBox.IsChecked == true,
        ShowHiddenBox.IsChecked == true);

    private void OnWindowsUpdateBatchSelectionChanged(object? sender, OnlyWingetTableBatchSelectionEventArgs args)
    {
        var rows = args.Items.OfType<WindowsUpdateDisplayRow>();
        ViewModel.SetSelected(rows, args.IsSelected);
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => ViewModel.Cancel();

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
