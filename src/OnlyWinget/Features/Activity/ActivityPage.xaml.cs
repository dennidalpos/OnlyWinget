using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.DesignSystem;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Activity;

public sealed partial class ActivityPage : Page
{
    private readonly ObservableCollection<ActivityRow> entries = [];
    private IReadOnlyList<ActivityRow> allEntries = [];

    public ActivityPage()
    {
        InitializeComponent();
        ActivityList.ItemsSource = entries;
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
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Activity;
        CommandBar.SetCommands(state.Commands);
        allEntries = state.Entries;
        ApplyFilter();
        if (state.Entries.Count == 0)
        {
            PageState.ShowEmpty(TextResources.Get("Empty_Activity"));
        }
        else
        {
            PageState.Hide();
        }
    }

    private void ApplyText()
    {
        Scaffold.Title = TextResources.Get("Activity_Title");
        Scaffold.Subtitle = TextResources.Get("Activity_Subtitle");
        SearchBox.PlaceholderText = TextResources.Get("Activity_Search");
        ((ComboBoxItem)SeverityFilter.Items[0]).Content = TextResources.Get("Activity_AllSeverities");
        for (var index = 1; index < SeverityFilter.Items.Count; index++)
        {
            var item = (ComboBoxItem)SeverityFilter.Items[index];
            item.Content = item.Tag;
        }
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        if (args.Command.Id != UiCommandId.ClearActivity)
        {
            return;
        }

        if (!await App.UiServices.Confirmation.ConfirmAsync(
                XamlRoot,
                "Command_Activity_Clear",
                args.Command.ConfirmationResourceKey ?? "Dialog_ClearActivity_Message"))
        {
            return;
        }

        App.Workflow.ClearActivity();
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyFilter();

    private void OnFilterChanged(object sender, SelectionChangedEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        if (SearchBox is null || SeverityFilter is null)
        {
            return;
        }

        var query = SearchBox.Text.Trim();
        var severity = (SeverityFilter.SelectedItem as ComboBoxItem)?.Tag?.ToString();
        var filtered = allEntries.Where(entry =>
            (string.IsNullOrEmpty(query) || entry.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || entry.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase)) &&
            (severity is null or "all" || string.Equals(entry.Severity.ToString(), severity, StringComparison.Ordinal)));
        PageUi.ReplaceItems(entries, filtered.ToArray());
    }

}
