using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using System.ComponentModel;

namespace OnlyWinget.Features.Packages;

public sealed partial class SearchPage : UserControl
{
    public SearchViewModel ViewModel { get; }

    public SearchPage()
    {
        ViewModel = new(Dispatch);
        InitializeComponent();
        ViewModel.PropertyChanged += OnViewModelChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Activate();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Deactivate();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args) => Refresh();

    private void Refresh()
    {
        PageState.Present(ViewModel.PageState);
    }

    private void ApplyText()
    {
        QueryBox.PlaceholderText = TextResources.Get("Search_Query");
        QueryBox.Header = TextResources.Get("Search_Query");
        ResultList.SelectionLabel = TextResources.Get("Command_Select_All");
        ResultList.SetHeaders(new[] { "Header_Name", "Header_PackageId", "Header_Source", "Header_Version", "Header_Publisher", "Header_Match" }.Select(TextResources.Get).ToArray());
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
            case UiCommandId.AddSearchResults: await ViewModel.AddSelectedAsync(); break;
        }
    }

    private Task SearchAsync()
    {
        return ViewModel.SearchAsync(QueryBox.Text);
    }

    private void OnToggleAllResults(object? sender, EventArgs args)
    {
        ViewModel.ToggleAll();
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }

    private void OnResultSelectionToggled(object? sender, OnlyWingetTableSelectionEventArgs args)
    {
        if (args.Item is SearchResultRow row) ViewModel.Toggle(row);
    }
}
