using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Pages;

public sealed partial class SearchPage : Page
{
    private bool isRefreshing;

    public SearchPage()
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
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Search;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        ResultList.ItemsSource = state.Results;
        StatusText.Text = state.Error ?? (state.Results.Count == 0 ? TextResources.Get("Empty_Search") : string.Empty);
        LoadingRing.IsActive = state.IsLoading;
        LoadingRing.Visibility = state.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        SelectAllResultsBox.IsThreeState = true;
        SelectAllResultsBox.IsChecked = state.HeaderState switch
        {
            SelectionHeaderState.Checked => true,
            SelectionHeaderState.Mixed => null,
            _ => false
        };

        SetEnabled(SearchButton, commands, "search.execute");
        SetEnabled(AddSelectedButton, commands, "search.addSelected");
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Nav_Search");
        QueryBox.PlaceholderText = TextResources.Get("Search_Query");
        SourceBox.Header = TextResources.Get("Package_Source");
        SearchButton.Content = TextResources.Get("Command_Search_Execute");
        AddSelectedButton.Content = TextResources.Get("Command_Search_AddSelected");
    }

    private async void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await SearchAsync();
    }

    private async void OnSearch(object sender, RoutedEventArgs args)
    {
        await SearchAsync();
    }

    private async Task SearchAsync()
    {
        var search = App.Workflow.SearchAsync(QueryBox.Text, SourceBox.Text, CancellationToken.None);
        Notify();
        await search;
        Notify();
    }

    private async void OnAddSelected(object sender, RoutedEventArgs args)
    {
        var addSelected = App.Workflow.AddSelectedSearchResultsToActivePresetAsync(CancellationToken.None);
        Notify();
        await addSelected;
        Notify();
    }

    private void OnToggleAllResults(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        App.Workflow.ToggleAllSearchResults();
        Notify();
    }

    private void OnResultSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not SearchResultRow row)
        {
            return;
        }

        App.Workflow.ToggleSearchResult(new PackageIdentity(row.PackageId, row.Source));
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
