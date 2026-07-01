using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using System.ComponentModel;

namespace OnlyWinget.Features.Activity;

public sealed partial class ActivityPage : Page
{
    private readonly ActivityViewModel viewModel;

    public ActivityPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        ActivityList.ItemsSource = viewModel.Entries;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => viewModel.Activate();
    private void OnUnloaded(object sender, RoutedEventArgs args) => viewModel.Deactivate();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ActivityViewModel.Commands))
        {
            CommandBar.SetCommands(viewModel.Commands);
        }

        if (args.PropertyName == nameof(ActivityViewModel.IsEmpty))
        {
            if (viewModel.IsEmpty)
            {
                PageState.ShowEmpty(TextResources.Get("Empty_Activity"));
            }
            else
            {
                PageState.Hide();
            }
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
            item.Content = TextResources.Get($"Activity_Severity_{item.Tag}");
        }
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        if (args.Command.Id == UiCommandId.ClearActivity &&
            await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Command_Activity_Clear", args.Command.ConfirmationResourceKey ?? "Dialog_ClearActivity_Message"))
        {
            App.Workflow.ClearActivity();
        }
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyFilter();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        if (SearchBox is not null && SeverityFilter?.SelectedItem is ComboBoxItem selected)
        {
            viewModel.SetFilter(SearchBox.Text, selected.Tag?.ToString() ?? "all");
        }
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess)
        {
            action();
        }
        else
        {
            _ = DispatcherQueue.TryEnqueue(() => action());
        }
    }
}
