using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.DesignSystem;
using OnlyWinget.Application.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Sources;

public sealed partial class SourcesPage : Page
{
    private bool isRefreshing;
    private readonly ObservableCollection<SourceRow> sources = [];

    public SourcesPage()
    {
        InitializeComponent();
        SourceList.ItemsSource = sources;
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
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => PageUi.RefreshOnUiThread(this, Refresh);

    private void Refresh()
    {
        isRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Sources;
        var commands = state.Commands.ToDictionary(command => command.Id);

        PageUi.ReplaceItems(sources, state.Sources
            .Select(source => source with
            {
                Type = TextResources.Get(source.Type),
                Status = TextResources.Get($"Source_Status_{source.Status}")
            })
            .ToArray());
        PageUi.ApplyStatus(StatusText, state.Error, TextResources.Get("Empty_Sources"), state.Sources.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsLoading);
        PageUi.SetEnabled(RefreshButton, commands, UiCommandId.RefreshSources);
        PageUi.SetEnabled(UpdateButton, commands, UiCommandId.UpdateSources);
        PageUi.SetEnabled(AddButton, commands, UiCommandId.AddSource);
        PageUi.SetEnabled(RemoveButton, commands, UiCommandId.RemoveSource);
        PageUi.SetEnabled(ResetButton, commands, UiCommandId.ResetSources);
        isRefreshing = false;
    }

    private void ApplyText()
    {
        Scaffold.Title = TextResources.Get("Sources_Title");
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
            !await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Dialog_RemoveSource_Title", "Dialog_RemoveSource_Message"))
        {
            return;
        }

        await PageUi.RunWorkflowAsync(() => App.Workflow.RemoveSourceAsync(row.Name, CancellationToken.None));
    }

    private async void OnResetSources(object sender, RoutedEventArgs args)
    {
        if (!await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Dialog_ResetSources_Title", "Dialog_ResetSources_Message"))
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

}
