using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;

namespace OnlyWinget.Pages;

public sealed partial class SourcesPage : Page
{
    private bool isRefreshing;

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
        isRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Sources;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        SourceList.ItemsSource = state.Sources
            .Select(source => source with
            {
                Type = TextResources.Get(source.Type),
                Status = TextResources.Get($"Source_Status_{source.Status}")
            })
            .ToArray();
        PageUi.ApplyStatus(StatusText, state.Error, TextResources.Get("Empty_Sources"), state.Sources.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsLoading);
        PageUi.SetEnabled(RefreshButton, commands, "sources.refresh");
        PageUi.SetEnabled(UpdateButton, commands, "sources.update");
        PageUi.SetEnabled(AddButton, commands, "sources.add");
        PageUi.SetEnabled(RemoveButton, commands, "sources.remove");
        PageUi.SetEnabled(ResetButton, commands, "sources.reset");
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Sources_Title");
        AddSourceSectionText.Text = TextResources.Get("Section_AddSource");
        ManageSourcesSectionText.Text = TextResources.Get("Section_ManageSources");
        SourceNameBox.Header = TextResources.Get("Source_Name");
        SourceArgumentBox.Header = TextResources.Get("Source_Argument");
        AddButton.Content = TextResources.Get("Command_Sources_Add");
        RefreshButton.Content = TextResources.Get("Command_Sources_Refresh");
        UpdateButton.Content = TextResources.Get("Command_Sources_Update");
        RemoveButton.Content = TextResources.Get("Command_Sources_Remove");
        ResetButton.Content = TextResources.Get("Command_Sources_Reset");
    }

    private async void OnRefreshSources(object sender, RoutedEventArgs args)
    {
        await PageUi.RunWorkflowAsync(() => App.Workflow.RefreshSourcesAsync(CancellationToken.None));
    }

    private async void OnUpdateSources(object sender, RoutedEventArgs args)
    {
        await PageUi.RunWorkflowAsync(() => App.Workflow.UpdateSourcesAsync(CancellationToken.None));
    }

    private async void OnAddSource(object sender, RoutedEventArgs args)
    {
        await PageUi.RunWorkflowAsync(() => App.Workflow.AddSourceAsync(SourceNameBox.Text, SourceArgumentBox.Text, CancellationToken.None));
    }

    private async void OnRemoveSource(object sender, RoutedEventArgs args)
    {
        if (SourceList.SelectedItem is not SourceRow row ||
            !await ConfirmAsync(TextResources.Get("Dialog_RemoveSource_Title"), TextResources.Get("Dialog_RemoveSource_Message")))
        {
            return;
        }

        await PageUi.RunWorkflowAsync(() => App.Workflow.RemoveSourceAsync(row.Name, CancellationToken.None));
    }

    private async void OnResetSources(object sender, RoutedEventArgs args)
    {
        if (!await ConfirmAsync(TextResources.Get("Dialog_ResetSources_Title"), TextResources.Get("Dialog_ResetSources_Message")))
        {
            return;
        }

        await PageUi.RunWorkflowAsync(() => App.Workflow.ResetSourcesAsync(CancellationToken.None));
    }

    private async void OnSourceEnabledToggled(object sender, RoutedEventArgs args)
    {
        if (isRefreshing || sender is not ToggleSwitch { DataContext: SourceRow row } toggle)
        {
            return;
        }

        await PageUi.RunWorkflowAsync(() =>
            App.Workflow.SetSourceEnabledAsync(row.Name, toggle.IsOn, CancellationToken.None));
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

}
