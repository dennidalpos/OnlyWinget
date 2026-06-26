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

    private async void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.WorkflowChanged += OnWorkflowChanged;
        Refresh();
        await ScanWindowsUpdatesAsync(CancellationToken.None);
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
        ScanButton.Label = TextResources.Get("Command_WindowsUpdates_Scan");
        InstallSelectedButton.Label = TextResources.Get("Command_WindowsUpdates_InstallSelected");
        CancelButton.Label = TextResources.Get("Command_Operation_Cancel");
    }

    private async void OnScanWindowsUpdates(object sender, RoutedEventArgs args)
    {
        await ScanWindowsUpdatesAsync(CancellationToken.None);
    }

    private async void OnInstallSelected(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        try
        {
            var install = App.Workflow.InstallSelectedWindowsUpdatesAsync(operationCancellation.Token);
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

    private static async Task ScanWindowsUpdatesAsync(CancellationToken cancellationToken)
    {
        var scan = App.Workflow.ScanWindowsUpdatesAsync(cancellationToken);
        Notify();
        await scan;
        Notify();
    }

    private static void Notify()
    {
        App.NotifyWorkflowChanged();
    }
}
