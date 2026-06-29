using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using System.Collections.ObjectModel;

namespace OnlyWinget.Pages;

public sealed partial class SearchPage : Page
{
    private bool isRefreshing;
    private readonly ObservableCollection<SearchResultRow> results = [];

    public SearchPage()
    {
        InitializeComponent();
        ResultList.ItemsSource = results;
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
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Search;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        var localizedResults = state.Results.Select(row => row with
        {
            Architecture = TextResources.Get(row.Architecture),
            Version = string.IsNullOrWhiteSpace(row.Version) ? TextResources.Get("Value_Unknown") : row.Version,
            Match = string.IsNullOrWhiteSpace(row.Match) ? TextResources.Get("Value_Unknown") : row.Match
        });
        PageUi.SynchronizeItems(results, localizedResults, PackageKey);
        PageUi.ApplyStatus(
            StatusText,
            state.Error,
            state.IsLoading ? string.Empty : TextResources.Get("Empty_Search"),
            state.Results.Count > 0);
        PageUi.ApplyLoading(LoadingRing, state.IsLoading);
        PageUi.SetVisible(SearchProgressBar, state.IsLoading);
        PageUi.SetVisible(LoadingStatusText, state.IsLoading);
        LoadingStatusText.Text = state.IsLoading ? TextResources.Get("Progress_Searching") : string.Empty;
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
        SelectAllResultsBox.Content = TextResources.Get("Command_Select_All");
        SearchButton.Content = TextResources.Get("Command_Search_Execute");
        AddSelectedButton.Content = TextResources.Get("Command_Search_AddSelected");
        SearchNameHeader.Text = TextResources.Get("Header_Name");
        SearchPackageIdHeader.Text = TextResources.Get("Header_PackageId");
        SearchSourceHeader.Text = TextResources.Get("Header_Source");
        SearchVersionHeader.Text = TextResources.Get("Header_Version");
        SearchArchitectureHeader.Text = TextResources.Get("Header_Architecture");
        SearchMatchHeader.Text = TextResources.Get("Header_Match");
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
        return PageUi.RunWorkflowAsync(() => App.Workflow.SearchAsync(QueryBox.Text, CancellationToken.None));
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
    }

    private void OnResultSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not SearchResultRow row)
        {
            return;
        }

        App.Workflow.ToggleSearchResult(new PackageIdentity(row.PackageId, row.Source));
    }

    private static string PackageKey(SearchResultRow row) =>
        $"{row.Source?.ToUpperInvariant() ?? string.Empty}|{row.PackageId.ToUpperInvariant()}";
}
