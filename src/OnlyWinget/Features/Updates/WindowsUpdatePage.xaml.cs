using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using System.ComponentModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class WindowsUpdatePage : UserControl
{
    private bool isRefreshing;
    private bool wasBusy;
    private readonly WindowsUpdatesViewModel viewModel;

    public WindowsUpdatePage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        WindowsUpdateList.ItemsSource = viewModel.Updates;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        viewModel.Activate();
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
        LoadingRing.IsActive = viewModel.IsBusy;
        LoadingRing.Visibility = viewModel.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        ApplyOperationStatus();
        WindowsUpdateList.HeaderSelection = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };

        CommandBar.SetCommands(viewModel.Commands.Values);
        isRefreshing = false;
    }

    private void ApplyText()
    {
        SoftwareUpdatesBox.Content = TextResources.Get("WindowsUpdates_IncludeSoftware");
        DriverUpdatesBox.Content = TextResources.Get("WindowsUpdates_IncludeDrivers");
        MicrosoftUpdatesBox.Content = TextResources.Get("WindowsUpdates_IncludeMicrosoft");
        WindowsUpdateList.SelectionLabel = TextResources.Get("Command_Select_All");
        WindowsUpdateList.SetHeaders(new[] { "Header_Title", "Header_KnowledgeBase", "Header_Severity", "Header_Categories", "Header_Size", "Header_Downloaded", "Header_Reboot", "Header_Revision", "Header_Status" }.Select(TextResources.Get).ToArray());
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        switch (args.Command.Id)
        {
            case UiCommandId.ScanWindowsUpdates: await viewModel.ScanAsync(CreateOptions()); break;
            case UiCommandId.InstallWindowsUpdates: await viewModel.InstallAsync(CreateOptions()); break;
            case UiCommandId.CancelOperation: viewModel.Cancel(); break;
        }
    }

    private void OnToggleAllWindowsUpdates(object? sender, EventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        viewModel.ToggleAll();
    }

    private void OnWindowsUpdateSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not WindowsUpdateRow row)
        {
            return;
        }

        viewModel.Toggle(row);
    }

    private WindowsUpdateOptions CreateOptions() => new(
        SoftwareUpdatesBox.IsChecked == true,
        DriverUpdatesBox.IsChecked == true,
        MicrosoftUpdatesBox.IsChecked == true);

    private void ApplyOperationStatus()
    {
        if (viewModel.IsBusy)
        {
            OperationStatus.Show(TextResources.Get("Operation_WindowsUpdate_Title"), TextResources.Get(viewModel.IsInstalling ? "Progress_InstallingWindowsUpdates" : "Progress_ScanningWindowsUpdates"), canCancel: true);
        }
        else if (wasBusy)
        {
            var error = viewModel.Error;
            var reboot = viewModel.RebootRequired;
            OperationStatus.Complete(error ?? TextResources.Get(reboot ? "WindowsUpdates_RebootRequired" : "Progress_Completed"), error is not null);
        }
        wasBusy = viewModel.IsBusy;
    }

    private void OnWindowsUpdateSelectionToggled(object? sender, OnlyWingetTableSelectionEventArgs args)
    {
        if (args.Item is WindowsUpdateRow row) viewModel.Toggle(row);
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => viewModel.Cancel();

    public static string FormatBoolean(bool value) =>
        TextResources.Get(value ? "Value_Yes" : "Value_No");

    public static string FormatSize(ulong bytes)
    {
        if (bytes == 0)
        {
            return TextResources.Get("Value_Unknown");
        }

        const double megabyte = 1024d * 1024d;
        const double gigabyte = megabyte * 1024d;
        return bytes >= gigabyte
            ? $"{bytes / gigabyte:0.##} GB"
            : $"{bytes / megabyte:0.##} MB";
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
