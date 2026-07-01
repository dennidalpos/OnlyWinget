using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using System.ComponentModel;

namespace OnlyWinget.Features.Packages;

public sealed partial class SearchPage : Page
{
    private bool isRefreshing;
    private readonly SearchViewModel viewModel;

    public SearchPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        ResultList.ItemsSource = viewModel.Results;
        viewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        viewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        viewModel.Deactivate();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        isRefreshing = true;
        PageState.Present(viewModel.PageState);
        LoadingRing.IsActive = viewModel.IsLoading;
        LoadingRing.Visibility = viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        SearchProgressBar.Visibility = viewModel.IsLoading ? Visibility.Visible : Visibility.Collapsed;
        SelectAllResultsBox.IsThreeState = true;
        SelectAllResultsBox.IsChecked = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };

        SearchButton.IsEnabled = viewModel.IsEnabled(UiCommandId.SearchPackages);
        AddSelectedButton.IsEnabled = viewModel.IsEnabled(UiCommandId.AddSearchResults);
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
        return viewModel.SearchAsync(QueryBox.Text, CancellationToken.None);
    }

    private async void OnAddSelected(object sender, RoutedEventArgs args)
    {
        await viewModel.AddSelectedAsync(CancellationToken.None);
    }

    private void OnToggleAllResults(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        viewModel.ToggleAll();
    }

    private void OnResultSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not SearchResultRow row)
        {
            return;
        }

        viewModel.Toggle(row);
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
