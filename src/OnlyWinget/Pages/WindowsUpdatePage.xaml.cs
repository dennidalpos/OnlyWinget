using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using System.Collections.ObjectModel;

namespace OnlyWinget.Pages;

public sealed partial class WindowsUpdatePage : Page
{
    private CancellationTokenSource? operationCancellation;
    private bool isRefreshing;
    private readonly ObservableCollection<WindowsUpdateRow> updates = [];

    public WindowsUpdatePage()
    {
        InitializeComponent();
        WindowsUpdateList.ItemsSource = updates;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.Workflow.StateChanged += OnWorkflowChanged;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        App.Workflow.StateChanged -= OnWorkflowChanged;
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => PageUi.RefreshOnUiThread(this, Refresh);

    private void Refresh()
    {
        isRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).WindowsUpdates;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        var localizedUpdates = state.Updates.Select(row => row with
        {
            Severity = string.IsNullOrWhiteSpace(row.Severity) ? TextResources.Get("Value_Unknown") : row.Severity,
            Categories = string.IsNullOrWhiteSpace(row.Categories) ? TextResources.Get("Value_Unknown") : row.Categories,
            KnowledgeBaseArticles = string.IsNullOrWhiteSpace(row.KnowledgeBaseArticles)
                ? TextResources.Get("Value_Unknown")
                : row.KnowledgeBaseArticles,
            Status = string.IsNullOrWhiteSpace(row.Status) ? TextResources.Get("Value_Unknown") : row.Status
        });
        PageUi.SynchronizeItems(updates, localizedUpdates, UpdateKey);
        PageUi.ApplyStatus(
            StatusText,
            state.Error,
            state.IsScanning ? string.Empty : TextResources.Get("Empty_WindowsUpdates"),
            state.Updates.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsScanning || state.IsInstalling);
        PageUi.SetVisible(WindowsUpdateProgressBar, state.IsScanning || state.IsInstalling);
        PageUi.SetVisible(LoadingStatusText, state.IsScanning || state.IsInstalling);
        LoadingStatusText.Text = state.IsInstalling
            ? TextResources.Get("Progress_InstallingWindowsUpdates")
            : state.IsScanning
                ? TextResources.Get("Progress_ScanningWindowsUpdates")
                : string.Empty;
        PageUi.ApplySelectionHeader(SelectAllWindowsUpdatesBox, state.HeaderState);

        PageUi.SetEnabled(ScanButton, commands, "windowsUpdates.scan");
        PageUi.SetEnabled(InstallSelectedButton, commands, "windowsUpdates.installSelected");
        PageUi.SetEnabled(CancelButton, commands, "operation.cancel");
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
        SupersededUpdatesBox.Content = TextResources.Get("WindowsUpdates_IncludeSuperseded");
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
        if (!await ConfirmSupersededAsync())
        {
            return;
        }

        await ScanWindowsUpdatesAsync(CreateOptions(), CancellationToken.None);
    }

    private async void OnInstallSelected(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        try
        {
            if (!await ConfirmSupersededAsync())
            {
                return;
            }

            await App.Workflow.InstallSelectedWindowsUpdatesAsync(CreateOptions(), operationCancellation.Token);
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
        }
    }

    private void OnCancel(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Cancel();
    }

    private void OnToggleAllWindowsUpdates(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        App.Workflow.ToggleAllWindowsUpdates();
    }

    private void OnWindowsUpdateSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not WindowsUpdateRow row)
        {
            return;
        }

        App.Workflow.ToggleWindowsUpdate(new WindowsUpdateIdentity(row.UpdateId, row.RevisionNumber));
    }

    private static Task ScanWindowsUpdatesAsync(WindowsUpdateOptions options, CancellationToken cancellationToken)
    {
        return PageUi.RunWorkflowAsync(() => App.Workflow.ScanWindowsUpdatesAsync(options, cancellationToken));
    }

    private WindowsUpdateOptions CreateOptions() => new(
        SoftwareUpdatesBox.IsChecked == true,
        DriverUpdatesBox.IsChecked == true,
        MicrosoftUpdatesBox.IsChecked == true,
        SupersededUpdatesBox.IsChecked == true);

    private async Task<bool> ConfirmSupersededAsync()
    {
        if (SupersededUpdatesBox.IsChecked != true)
        {
            return true;
        }

        var dialog = new ContentDialog
        {
            Title = TextResources.Get("Dialog_SupersededUpdates_Title"),
            Content = TextResources.Get("Dialog_SupersededUpdates_Message"),
            PrimaryButtonText = TextResources.Get("Dialog_Confirm"),
            CloseButtonText = TextResources.Get("Dialog_Cancel"),
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = XamlRoot
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

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

    private static string UpdateKey(WindowsUpdateRow row) =>
        $"{row.UpdateId.ToUpperInvariant()}|{row.RevisionNumber}";
}
