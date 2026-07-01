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

    public SourcesPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        SourceList.ItemsSource = viewModel.Sources;
        viewModel.PropertyChanged += OnViewModelChanged;
        viewModel.Name.PropertyChanged += OnValidationChanged;
        viewModel.Argument.PropertyChanged += OnValidationChanged;
        SourceNameBox.TextChanged += OnSourceNameChanged;
        SourceArgumentBox.TextChanged += OnSourceArgumentChanged;
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

    private void OnSourceNameChanged(object sender, TextChangedEventArgs args) => viewModel.Name.Value = SourceNameBox.Text;
    private void OnSourceArgumentChanged(object sender, TextChangedEventArgs args) => viewModel.Argument.Value = SourceArgumentBox.Text;
    private void RefreshCommands() => CommandBar.SetCommands(viewModel.Commands.Values.Select(command =>
        command.Id == UiCommandId.AddSource ? command with { IsEnabled = viewModel.CanAdd } : command));

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        switch (args.Command.Id)
        {
            case UiCommandId.RefreshSources: await App.Workflow.RefreshSourcesAsync(CancellationToken.None); break;
            case UiCommandId.UpdateSources: await App.Workflow.UpdateSourcesAsync(CancellationToken.None); break;
            case UiCommandId.AddSource: await viewModel.AddAsync(CancellationToken.None); break;
            case UiCommandId.RemoveSource when SourceList.SelectedItem is SourceRow row &&
                await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Dialog_RemoveSource_Title", "Dialog_RemoveSource_Message"):
                await App.Workflow.RemoveSourceAsync(row.Name, CancellationToken.None); break;
            case UiCommandId.ResetSources when
                await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Dialog_ResetSources_Title", "Dialog_ResetSources_Message"):
                await App.Workflow.ResetSourcesAsync(CancellationToken.None); break;
        }
    }

    private async void OnSourceEnabledToggled(object sender, RoutedEventArgs args)
    {
        if (!viewModel.IsRefreshing && sender is ToggleSwitch { DataContext: SourceRow row } toggle)
        {
            await App.Workflow.SetSourceEnabledAsync(row.Name, toggle.IsOn, CancellationToken.None);
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
