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
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Deactivate();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        PageState.Present(ViewModel.PageState);
        SoftwareUpdatesBox.IsEnabled = !ViewModel.IsBusy;
        DriverUpdatesBox.IsEnabled = !ViewModel.IsBusy;
        MicrosoftUpdatesBox.IsEnabled = !ViewModel.IsBusy;
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        switch (args.Command.Id)
        {
            case UiCommandId.ScanWindowsUpdates: await ViewModel.ScanAsync(CreateOptions()); break;
            case UiCommandId.InstallWindowsUpdates: await ViewModel.InstallAsync(CreateOptions()); break;
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
        MicrosoftUpdatesBox.IsChecked == true);

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
