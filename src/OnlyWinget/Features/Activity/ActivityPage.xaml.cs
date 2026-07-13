using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Activity;
using OnlyWinget.DesignSystem.Commands;
using System.ComponentModel;

namespace OnlyWinget.Features.Activity;

public sealed partial class ActivityPage : Page
{
    private readonly ActivityViewModel viewModel;
    private IReadOnlyList<ActivityEntry>? clearedEntries;

    public ActivityPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        ActivityList.ItemsSource = viewModel.Entries;
        viewModel.PropertyChanged += OnViewModelChanged;
        PageState.ActionRequested += OnUndoClear;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => viewModel.Activate();
    private void OnUnloaded(object sender, RoutedEventArgs args) => viewModel.Deactivate();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ActivityViewModel.Commands))
        {
            CommandBar.SetCommands(viewModel.Commands);
        }

        if (args.PropertyName == nameof(ActivityViewModel.PageState))
        {
            PageState.Present(viewModel.PageState);
        }
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        if (args.Command.Id == UiCommandId.ExportActivity)
        {
            using var cancellation = new CancellationTokenSource();
            await App.UiServices.FilePicker.PickAndWriteTextAsync(App.WindowId, "OnlyWinget-activity.log", ".log", "FileType_Log", viewModel.ExportText(), cancellation.Token);
        }
        else if (args.Command.Id == UiCommandId.ClearActivity &&
            await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Command_Activity_Clear", args.Command.ConfirmationResourceKey ?? "Dialog_ClearActivity_Message"))
        {
            clearedEntries = App.Workflow.State.Activity.ToArray();
            App.Workflow.ClearActivity();
            PageState.ShowUndo(TextResources.Get("Activity_Cleared"), TextResources.Get("Command_Undo"));
        }
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyFilter();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs args) => ApplyFilter();
    private void OnActivityColumnFilterChanged(object sender, TextChangedEventArgs args)
    {
        if (sender is TextBox box)
        {
            var caretIndex = box.SelectionStart;
            var selectionLength = box.SelectionLength;

            ApplyColumnFilter();

            DispatcherQueue.TryEnqueue(() =>
            {
                box.Focus(FocusState.Programmatic);
                box.SelectionStart = caretIndex;
                box.SelectionLength = selectionLength;
            });
        }
    }

    private void ApplyFilter()
    {
        if (viewModel is not null && SearchBox is not null && SeverityFilter?.SelectedItem is ComboBoxItem selected)
        {
            var selectedCategory = CategoryFilter?.SelectedItem as ComboBoxItem;
            viewModel.SetFilter(SearchBox.Text, selected.Tag?.ToString() ?? "all", selectedCategory?.Tag?.ToString() ?? "all");
        }
    }

    private void ApplyColumnFilter()
    {
        if (viewModel is not null && ActivityTimeFilterBox is not null)
        {
            viewModel.SetColumnFilters(
                ActivityTimeFilterBox.Text,
                ActivitySeverityFilterBox.Text,
                ActivityTitleFilterBox.Text,
                ActivityMessageFilterBox.Text);
        }
    }

    private void OnCopyDetails(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ActivityRow entry }) App.UiServices.Clipboard.CopyText(ActivityViewModel.Format(entry));
    }

    private void OnUndoClear(object? sender, EventArgs args)
    {
        if (clearedEntries is null) return;
        App.Workflow.RestoreActivity(clearedEntries);
        clearedEntries = null;
        PageState.Hide();
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
