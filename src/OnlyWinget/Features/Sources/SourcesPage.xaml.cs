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
        ApplyText();
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

    private void ApplyText()
    {
        Scaffold.Title = TextResources.Get("Sources_Title");
        AddSourceSectionText.Text = TextResources.Get("Section_AddSource");
        ManageSourcesSectionText.Text = TextResources.Get("Section_ManageSources");
        SourceNameBox.Header = TextResources.Get("Source_Name");
        SourceArgumentBox.Header = TextResources.Get("Source_Argument");
    }


    private void RefreshCommands() => CommandBar.SetCommands(viewModel.Commands.Values.Select(command => command.Id switch
    {
        UiCommandId.AddSource => command with { IsEnabled = viewModel.CanAdd },
        UiCommandId.RemoveSource => command with { IsEnabled = command.IsEnabled && viewModel.SelectedSource?.IsExplicit == true },
        _ => command
    }));

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        await viewModel.ExecuteAsync(args.Command.Id);
    }

    private void OnSourceToggleDataContextChanged(FrameworkElement sender, DataContextChangedEventArgs args)
    {
        if (sender is ToggleSwitch toggle)
        {
            toggle.Toggled -= OnSourceEnabledToggled;
            if (toggle.DataContext is SourceRow row)
            {
                toggle.IsOn = row.IsEnabled;
            }
            else
            {
                toggle.IsOn = false;
            }
            toggle.Toggled += OnSourceEnabledToggled;
        }
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
