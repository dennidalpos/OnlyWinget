using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using System.ComponentModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class WindowsUpdatePage : Page
{
    private bool isRefreshing;
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
        WindowsUpdateProgressBar.Visibility = viewModel.IsBusy ? Visibility.Visible : Visibility.Collapsed;
        SelectAllWindowsUpdatesBox.IsThreeState = true;
        SelectAllWindowsUpdatesBox.IsChecked = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };

        ScanButton.IsEnabled = viewModel.IsEnabled(UiCommandId.ScanWindowsUpdates);
        InstallSelectedButton.IsEnabled = viewModel.IsEnabled(UiCommandId.InstallWindowsUpdates);
        CancelButton.IsEnabled = viewModel.IsEnabled(UiCommandId.CancelOperation);
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("WindowsUpdates_Title");
        SelectAllWindowsUpdatesBox.Content = TextResources.Get("Command_Select_All");
        ScanButton.Content = TextResources.Get("Command_WindowsUpdates_Scan");
        InstallSelectedButton.Content = TextResources.Get("Command_WindowsUpdates_InstallSelected");
        CancelButton.Content = TextResources.Get("Command_Operation_Cancel");
        SoftwareUpdatesBox.Content = TextResources.Get("WindowsUpdates_IncludeSoftware");
        DriverUpdatesBox.Content = TextResources.Get("WindowsUpdates_IncludeDrivers");
        MicrosoftUpdatesBox.Content = TextResources.Get("WindowsUpdates_IncludeMicrosoft");
        WindowsTitleHeader.Text = TextResources.Get("Header_Title");
        WindowsKbHeader.Text = TextResources.Get("Header_KnowledgeBase");
        WindowsSeverityHeader.Text = TextResources.Get("Header_Severity");
        WindowsCategoriesHeader.Text = TextResources.Get("Header_Categories");
        WindowsSizeHeader.Text = TextResources.Get("Header_Size");
        WindowsDownloadedHeader.Text = TextResources.Get("Header_Downloaded");
        WindowsRebootHeader.Text = TextResources.Get("Header_Reboot");
        WindowsRevisionHeader.Text = TextResources.Get("Header_Revision");
        WindowsStatusHeader.Text = TextResources.Get("Header_Status");
    }

    private async void OnScanWindowsUpdates(object sender, RoutedEventArgs args)
    {
        await viewModel.ScanAsync(CreateOptions());
    }

    private async void OnInstallSelected(object sender, RoutedEventArgs args)
    {
        await viewModel.InstallAsync(CreateOptions());
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        viewModel.Cancel();
    }

    private void OnToggleAllWindowsUpdates(object sender, RoutedEventArgs args)
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
