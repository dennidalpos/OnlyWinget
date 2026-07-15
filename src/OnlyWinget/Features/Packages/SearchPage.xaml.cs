using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using System.ComponentModel;
using System.Linq;
using OnlyWinget.Domain.Packages;

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

    private void OnResultBatchSelectionChanged(object? sender, OnlyWingetTableBatchSelectionEventArgs args)
    {
        var rows = args.Items.OfType<SearchResultRow>();
        ViewModel.SetSelected(rows, args.IsSelected);
    }
}
