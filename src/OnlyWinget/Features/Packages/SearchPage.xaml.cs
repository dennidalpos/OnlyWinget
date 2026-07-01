using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using System.ComponentModel;

namespace OnlyWinget.Features.Packages;

public sealed partial class SearchPage : UserControl
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
        ResultList.HeaderSelection = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };

        CommandBar.SetCommands(viewModel.Commands.Values);
        isRefreshing = false;
    }

    private void ApplyText()
    {
        QueryBox.PlaceholderText = TextResources.Get("Search_Query");
        QueryBox.Header = TextResources.Get("Search_Query");
        ResultList.SelectionLabel = TextResources.Get("Command_Select_All");
        ResultList.SetHeaders(new[] { "Header_Name", "Header_PackageId", "Header_Source", "Header_Version", "Header_Architecture", "Header_Match" }.Select(TextResources.Get).ToArray());
    }

    private async void OnQuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        await SearchAsync();
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        switch (args.Command.Id)
        {
            case UiCommandId.SearchPackages: await SearchAsync(); break;
            case UiCommandId.AddSearchResults: await viewModel.AddSelectedAsync(CancellationToken.None); break;
        }
    }

    private Task SearchAsync()
    {
        return viewModel.SearchAsync(QueryBox.Text, CancellationToken.None);
    }

    private void OnToggleAllResults(object? sender, EventArgs args)
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

    private void OnResultSelectionToggled(object? sender, OnlyWingetTableSelectionEventArgs args)
    {
        if (args.Item is SearchResultRow row) viewModel.Toggle(row);
    }
}
