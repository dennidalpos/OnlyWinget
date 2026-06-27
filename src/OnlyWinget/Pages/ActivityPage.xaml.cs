using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Pages;

public sealed partial class ActivityPage : Page
{
    private readonly ObservableCollection<ActivityRow> entries = [];

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
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);
        PageUi.ReplaceItems(entries, state.Entries);
        StatusText.Text = state.Entries.Count == 0 ? TextResources.Get("Empty_Activity") : string.Empty;
        PageUi.SetEnabled(ClearButton, commands, "activity.clear");
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Activity_Title");
        ClearButton.Content = TextResources.Get("Command_Activity_Clear");
    }

    private void OnClearActivity(object sender, RoutedEventArgs args)
    {
        App.Workflow.ClearActivity();
    }

}
