using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;

namespace OnlyWinget.Pages;

public sealed partial class ActivityPage : Page
{
    public ActivityPage()
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
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Activity;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);
        ActivityList.ItemsSource = state.Entries;
        StatusText.Text = state.Entries.Count == 0 ? TextResources.Get("Empty_Activity") : string.Empty;
        PageUi.SetEnabled(ClearButton, commands, "activity.clear");
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Activity_Title");
        ClearButton.Content = TextResources.Get("Command_Activity_Clear");
        ActivityTimeHeaderText.Text = TextResources.Get("Header_Time");
        ActivitySeverityHeaderText.Text = TextResources.Get("Header_Severity");
        ActivityTitleHeaderText.Text = TextResources.Get("Header_Title");
        ActivityMessageHeaderText.Text = TextResources.Get("Header_Message");
    }

    private void OnClearActivity(object sender, RoutedEventArgs args)
    {
        App.Workflow.ClearActivity();
        App.NotifyWorkflowChanged();
    }

}
