using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;

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
        PageUi.ApplyStatus(StatusText, state.Error, TextResources.Get("Empty_Search"), state.Results.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsLoading);
        PageUi.ApplySelectionHeader(SelectAllResultsBox, state.HeaderState);

        PageUi.SetEnabled(SearchButton, commands, "search.execute");
        PageUi.SetEnabled(AddSelectedButton, commands, "search.addSelected");
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Nav_Search");
        SearchSectionText.Text = TextResources.Get("Section_Search");
        ResultsSectionText.Text = TextResources.Get("Section_SearchResults");
        QueryBox.PlaceholderText = TextResources.Get("Search_Query");
        QueryBox.Header = TextResources.Get("Search_Query");
        SourceBox.Header = TextResources.Get("Package_Source");
        SelectAllResultsBox.Content = TextResources.Get("Command_Select_All");
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

    private Task SearchAsync()
    {
        return PageUi.RunWorkflowAsync(() => App.Workflow.SearchAsync(QueryBox.Text, SourceBox.Text, CancellationToken.None));
    }

    private async void OnAddSelected(object sender, RoutedEventArgs args)
    {
        await PageUi.RunWorkflowAsync(() => App.Workflow.AddSelectedSearchResultsToActivePresetAsync(CancellationToken.None));
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

    private static void Notify()
    {
        App.NotifyWorkflowChanged();
    }
}
