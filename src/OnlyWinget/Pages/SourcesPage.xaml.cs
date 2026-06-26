using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;

namespace OnlyWinget.Pages;

public sealed partial class SourcesPage : Page
{
    public SourcesPage()
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
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => Refresh();

    private void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Sources;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        SourceList.ItemsSource = state.Sources
            .Select(source => source with { Status = TextResources.Get($"Source_Status_{source.Status}") })
            .ToArray();
        PageUi.ApplyStatus(StatusText, state.Error, TextResources.Get("Empty_Sources"), state.Sources.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsLoading);
        PageUi.SetEnabled(RefreshButton, commands, "sources.refresh");
        PageUi.SetEnabled(UpdateButton, commands, "sources.update");
        PageUi.SetEnabled(AddButton, commands, "sources.add");
        PageUi.SetEnabled(RemoveButton, commands, "sources.remove");
        PageUi.SetEnabled(ResetButton, commands, "sources.reset");
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Sources_Title");
        SourceNameBox.Header = TextResources.Get("Source_Name");
        SourceArgumentBox.Header = TextResources.Get("Source_Argument");
        AddButton.Content = TextResources.Get("Command_Sources_Add");
        RefreshButton.Label = TextResources.Get("Command_Sources_Refresh");
        UpdateButton.Label = TextResources.Get("Command_Sources_Update");
        RemoveButton.Label = TextResources.Get("Command_Sources_Remove");
        ResetButton.Label = TextResources.Get("Command_Sources_Reset");
    }

    private async void OnRefreshSources(object sender, RoutedEventArgs args)
    {
        var operation = App.Workflow.RefreshSourcesAsync(CancellationToken.None);
        Notify();
        await operation;
        Notify();
    }

    private async void OnUpdateSources(object sender, RoutedEventArgs args)
    {
        var operation = App.Workflow.UpdateSourcesAsync(CancellationToken.None);
        Notify();
        await operation;
        Notify();
    }

    private async void OnAddSource(object sender, RoutedEventArgs args)
    {
        var operation = App.Workflow.AddSourceAsync(SourceNameBox.Text, SourceArgumentBox.Text, CancellationToken.None);
        Notify();
        await operation;
        Notify();
    }

    private async void OnRemoveSource(object sender, RoutedEventArgs args)
    {
        if (SourceList.SelectedItem is not SourceRow row ||
            !await ConfirmAsync(TextResources.Get("Dialog_RemoveSource_Title"), TextResources.Get("Dialog_RemoveSource_Message")))
        {
            return;
        }

        var operation = App.Workflow.RemoveSourceAsync(row.Name, CancellationToken.None);
        Notify();
        await operation;
        Notify();
    }

    private async void OnResetSources(object sender, RoutedEventArgs args)
    {
        if (!await ConfirmAsync(TextResources.Get("Dialog_ResetSources_Title"), TextResources.Get("Dialog_ResetSources_Message")))
        {
            return;
        }

        var operation = App.Workflow.ResetSourcesAsync(CancellationToken.None);
        Notify();
        await operation;
        Notify();
    }

    private async Task<bool> ConfirmAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
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
