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

        if (args.PropertyName == nameof(ActivityViewModel.PageState))
        {
            PageState.Present(viewModel.PageState);
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
        var categories = new[] { "AllCategories", "Packages", "WindowsUpdate", "Sources", "Presets", "System" };
        for (var index = 0; index < CategoryFilter.Items.Count; index++)
        {
            ((ComboBoxItem)CategoryFilter.Items[index]).Content = TextResources.Get($"Activity_Category_{categories[index]}");
        }
        UndoBar.Message = TextResources.Get("Activity_Cleared");
        UndoButton.Content = TextResources.Get("Command_Undo");
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        if (args.Command.Id == UiCommandId.ExportActivity)
        {
            await App.UiServices.FilePicker.PickAndWriteTextAsync(App.WindowId, "OnlyWinget-activity.log", ".log", "FileType_Log", viewModel.ExportText(), CancellationToken.None);
        }
        else if (args.Command.Id == UiCommandId.ClearActivity &&
            await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Command_Activity_Clear", args.Command.ConfirmationResourceKey ?? "Dialog_ClearActivity_Message"))
        {
            clearedEntries = App.Workflow.State.Activity.ToArray();
            App.Workflow.ClearActivity();
            UndoBar.IsOpen = true;
        }
    }

    private void OnSearchChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args) => ApplyFilter();
    private void OnFilterChanged(object sender, SelectionChangedEventArgs args) => ApplyFilter();

    private void ApplyFilter()
    {
        if (SearchBox is not null && SeverityFilter?.SelectedItem is ComboBoxItem selected)
        {
            var selectedCategory = CategoryFilter?.SelectedItem as ComboBoxItem;
            viewModel.SetFilter(SearchBox.Text, selected.Tag?.ToString() ?? "all", selectedCategory?.Tag?.ToString() ?? "all");
        }
    }

    private void OnCopyDetails(object sender, RoutedEventArgs args)
    {
        if (sender is Button { Tag: ActivityRow entry }) App.UiServices.Clipboard.CopyText(ActivityViewModel.Format(entry));
    }

    private void OnUndoClear(object sender, RoutedEventArgs args)
    {
        if (clearedEntries is null) return;
        App.Workflow.RestoreActivity(clearedEntries);
        clearedEntries = null;
        UndoBar.IsOpen = false;
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
