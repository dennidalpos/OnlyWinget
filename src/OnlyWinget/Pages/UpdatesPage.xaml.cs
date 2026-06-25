using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;

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
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Updates;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        UpdateList.ItemsSource = state.Updates;
        StatusText.Text = state.Error ?? (state.Updates.Count == 0 ? TextResources.Get("Empty_Updates") : string.Empty);
        LoadingRing.IsActive = state.IsLoading;
        LoadingRing.Visibility = state.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        SelectAllUpdatesBox.IsThreeState = true;
        SelectAllUpdatesBox.IsChecked = state.HeaderState switch
        {
            SelectionHeaderState.Checked => true,
            SelectionHeaderState.Mixed => null,
            _ => false
        };

        SetEnabled(RefreshButton, commands, "updates.refresh");
        SetEnabled(ApplySelectedButton, commands, "updates.applySelected");
        SetEnabled(CancelButton, commands, "operation.cancel");
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Updates_Title");
        RefreshButton.Content = TextResources.Get("Command_Updates_Refresh");
        ApplySelectedButton.Content = TextResources.Get("Command_Updates_ApplySelected");
        CancelButton.Content = TextResources.Get("Command_Operation_Cancel");
    }

    private async void OnRefreshUpdates(object sender, RoutedEventArgs args)
    {
        var refresh = App.Workflow.RefreshUpdatesAsync(CancellationToken.None);
        Notify();
        await refresh;
        Notify();
    }

    private async void OnApplySelected(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        var apply = App.Workflow.ApplySelectedUpdatesAsync(operationCancellation.Token);
        Notify();
        await apply;
        operationCancellation.Dispose();
        operationCancellation = null;
        Notify();
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

    private static void SetEnabled(Control control, IReadOnlyDictionary<string, PresentationCommand> commands, string id)
    {
        if (commands.TryGetValue(id, out var command))
        {
            control.IsEnabled = command.IsEnabled;
        }
    }

    private static void Notify()
    {
        App.NotifyWorkflowChanged();
    }
}
