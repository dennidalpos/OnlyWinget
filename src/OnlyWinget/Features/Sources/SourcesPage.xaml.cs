using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using System.ComponentModel;

namespace OnlyWinget.Features.Sources;

public sealed partial class SourcesPage : Page
{
    private readonly SourcesViewModel viewModel;
    public SourcesViewModel ViewModel => viewModel;

    public SourcesPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        SourceList.ItemsSource = viewModel.Sources;
        viewModel.PropertyChanged += OnViewModelChanged;

        viewModel.Name.PropertyChanged += OnValidationChanged;
        viewModel.Argument.PropertyChanged += OnValidationChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs args) => viewModel.Activate();
    private void OnUnloaded(object sender, RoutedEventArgs args) => viewModel.Deactivate();

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args) => RefreshControls();

    private void OnValidationChanged(object? sender, PropertyChangedEventArgs args)
    {
        SourceNameBox.Description = viewModel.Name.Error;
        SourceArgumentBox.Description = viewModel.Argument.Error;
        AutomationProperties.SetHelpText(SourceNameBox, viewModel.Name.Error ?? string.Empty);
        AutomationProperties.SetHelpText(SourceArgumentBox, viewModel.Argument.Error ?? string.Empty);
        RefreshCommands();
    }

    private void RefreshControls()
    {
        PageState.Present(viewModel.PageState);
        LoadingRing.IsActive = viewModel.IsRefreshing;
        LoadingRing.Visibility = viewModel.IsRefreshing ? Visibility.Visible : Visibility.Collapsed;
        RefreshCommands();
    }

    private void RefreshCommands()
    {
        UiCommandId[] topLevelCommandIds = [UiCommandId.CancelOperation];

        CommandBar.SetCommands(viewModel.Commands.Values
            .Where(c => topLevelCommandIds.Contains(c.Id)));

        AddSourceBtn.IsEnabled = viewModel.CanAdd;
        RefreshSourcesBtn.IsEnabled = viewModel.IsEnabled(UiCommandId.RefreshSources);
        UpdateSourcesBtn.IsEnabled = viewModel.IsEnabled(UiCommandId.UpdateSources);
        RemoveSourceBtn.IsEnabled = viewModel.IsEnabled(UiCommandId.RemoveSource) && viewModel.SelectedSource?.IsExplicit == true;
        ResetSourcesBtn.IsEnabled = viewModel.IsEnabled(UiCommandId.ResetSources);
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        await viewModel.ExecuteAsync(args.Command.Id);
    }

    private void OnSourceSearchBoxChanged(AutoSuggestBox sender, AutoSuggestBoxTextChangedEventArgs args)
    {
        viewModel.SetSearchFilter(sender.Text);
    }

    private void OnSourceEnabledToggled(object sender, RoutedEventArgs args)
    {
        if (sender is ToggleSwitch { DataContext: SourceRow row } toggle)
        {
            if (toggle.IsOn != row.IsEnabled)
            {
                _ = DispatcherQueue.TryEnqueue(async () =>
                {
                    await viewModel.SetEnabledAsync(row, toggle.IsOn);
                });
            }
        }
    }

    private void OnSourceSelectionChanged(object sender, SelectionChangedEventArgs args) =>
        viewModel.SelectedSource = SourceList.SelectedItem as SourceRow;

    private async void OnAddSourceClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.AddSource);
    private async void OnRefreshSourcesClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RefreshSources);
    private async void OnUpdateSourcesClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.UpdateSources);
    private async void OnRemoveSourceClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RemoveSource);
    private async void OnResetSourcesClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.ResetSources);

    private async System.Threading.Tasks.Task ExecuteCommandAsync(UiCommandId id)
    {
        if (viewModel.Commands.TryGetValue(id, out var command) && command.IsEnabled)
        {
            await viewModel.ExecuteAsync(id);
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
