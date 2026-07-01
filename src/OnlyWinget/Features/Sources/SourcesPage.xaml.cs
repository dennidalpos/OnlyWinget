using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
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
        AddButton.IsEnabled = viewModel.CanAdd;
    }

    private void RefreshControls()
    {
        PageState.Present(viewModel.PageState);
        LoadingRing.IsActive = viewModel.IsRefreshing;
        LoadingRing.Visibility = viewModel.IsRefreshing ? Visibility.Visible : Visibility.Collapsed;
        RefreshButton.IsEnabled = viewModel.IsEnabled(UiCommandId.RefreshSources);
        UpdateButton.IsEnabled = viewModel.IsEnabled(UiCommandId.UpdateSources);
        AddButton.IsEnabled = viewModel.CanAdd;
        RemoveButton.IsEnabled = viewModel.IsEnabled(UiCommandId.RemoveSource);
        ResetButton.IsEnabled = viewModel.IsEnabled(UiCommandId.ResetSources);
    }

    private void ApplyText()
    {
        Scaffold.Title = TextResources.Get("Sources_Title");
        AddSourceSectionText.Text = TextResources.Get("Section_AddSource");
        ManageSourcesSectionText.Text = TextResources.Get("Section_ManageSources");
        SourceNameBox.Header = TextResources.Get("Source_Name");
        SourceArgumentBox.Header = TextResources.Get("Source_Argument");
        AddButton.Content = TextResources.Get("Command_Sources_Add");
        RefreshButton.Content = TextResources.Get("Command_Sources_Refresh");
        UpdateButton.Content = TextResources.Get("Command_Sources_Update");
        RemoveButton.Content = TextResources.Get("Command_Sources_Remove");
        ResetButton.Content = TextResources.Get("Command_Sources_Reset");
    }

    private void OnSourceNameChanged(object sender, TextChangedEventArgs args) => viewModel.Name.Value = SourceNameBox.Text;
    private void OnSourceArgumentChanged(object sender, TextChangedEventArgs args) => viewModel.Argument.Value = SourceArgumentBox.Text;
    private async void OnRefreshSources(object sender, RoutedEventArgs args) => await App.Workflow.RefreshSourcesAsync(CancellationToken.None);
    private async void OnUpdateSources(object sender, RoutedEventArgs args) => await App.Workflow.UpdateSourcesAsync(CancellationToken.None);
    private async void OnAddSource(object sender, RoutedEventArgs args) => await viewModel.AddAsync(CancellationToken.None);

    private async void OnRemoveSource(object sender, RoutedEventArgs args)
    {
        if (SourceList.SelectedItem is SourceRow row &&
            await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Dialog_RemoveSource_Title", "Dialog_RemoveSource_Message"))
        {
            await App.Workflow.RemoveSourceAsync(row.Name, CancellationToken.None);
        }
    }

    private async void OnResetSources(object sender, RoutedEventArgs args)
    {
        if (await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, "Dialog_ResetSources_Title", "Dialog_ResetSources_Message"))
        {
            await App.Workflow.ResetSourcesAsync(CancellationToken.None);
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
