using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Pages;

public sealed partial class UpdatesPage : Page
{
    private CancellationTokenSource? operationCancellation;
    private bool isRefreshing;

    public UpdatesPage()
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
        await RefreshUpdatesAsync();
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
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Updates;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        UpdateList.ItemsSource = state.Updates;
        PageUi.ApplyStatus(StatusText, state.Error, TextResources.Get("Empty_Updates"), state.Updates.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsLoading || state.IsExecuting);
        PageUi.ApplySelectionHeader(SelectAllUpdatesBox, state.HeaderState);

        PageUi.SetEnabled(RefreshButton, commands, "updates.refresh");
        PageUi.SetEnabled(ApplySelectedButton, commands, "updates.applySelected");
        PageUi.SetEnabled(CancelButton, commands, "operation.cancel");
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Updates_Title");
        SelectAllUpdatesBox.Content = TextResources.Get("Command_Select_All");
        RefreshButton.Label = TextResources.Get("Command_Updates_Refresh");
        ApplySelectedButton.Label = TextResources.Get("Command_Updates_ApplySelected");
        CancelButton.Label = TextResources.Get("Command_Operation_Cancel");
        UpdateNameHeaderText.Text = TextResources.Get("Header_Name");
        UpdatePackageHeaderText.Text = TextResources.Get("Header_PackageId");
        UpdateInstalledHeaderText.Text = TextResources.Get("Header_Installed");
        UpdateAvailableHeaderText.Text = TextResources.Get("Header_Available");
        UpdateSourceHeaderText.Text = TextResources.Get("Header_Source");
        UpdateStatusHeaderText.Text = TextResources.Get("Header_Status");
    }

    private async void OnRefreshUpdates(object sender, RoutedEventArgs args)
    {
        await RefreshUpdatesAsync();
    }

    private static Task RefreshUpdatesAsync()
    {
        return PageUi.RunWorkflowAsync(() => App.Workflow.RefreshUpdatesAsync(CancellationToken.None));
    }

    private async void OnApplySelected(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        try
        {
            var apply = App.Workflow.ApplySelectedUpdatesAsync(operationCancellation.Token);
            Notify();
            await apply;
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

    private void OnToggleAllUpdates(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        App.Workflow.ToggleAllUpdates();
        Notify();
    }

    private void OnUpdateSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not UpdateRow row)
        {
            return;
        }

        App.Workflow.ToggleUpdate(new PackageIdentity(row.PackageId, row.Source));
        Notify();
    }

    private static void Notify()
    {
        App.NotifyWorkflowChanged();
    }
}
