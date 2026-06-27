using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;

namespace OnlyWinget.Pages;

public sealed partial class WindowsUpdatePage : Page
{
    private CancellationTokenSource? operationCancellation;
    private bool isRefreshing;

    public WindowsUpdatePage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.WorkflowChanged += OnWorkflowChanged;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        App.WorkflowChanged -= OnWorkflowChanged;
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => Refresh();

    private void Refresh()
    {
        isRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).WindowsUpdates;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        WindowsUpdateList.ItemsSource = state.Updates;
        PageUi.ApplyStatus(StatusText, state.Error, TextResources.Get("Empty_WindowsUpdates"), state.Updates.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsScanning || state.IsInstalling);
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

            var install = App.Workflow.InstallSelectedWindowsUpdatesAsync(CreateOptions(), operationCancellation.Token);
            Notify();
            await install;
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            Notify();
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
        Notify();
    }

    private void OnWindowsUpdateSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not WindowsUpdateRow row)
        {
            return;
        }

        App.Workflow.ToggleWindowsUpdate(new WindowsUpdateIdentity(row.UpdateId, row.RevisionNumber));
        Notify();
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

    private static void Notify()
    {
        App.NotifyWorkflowChanged();
    }
}
