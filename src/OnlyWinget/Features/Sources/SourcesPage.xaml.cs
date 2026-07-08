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
        SourceNameFilterBox.PlaceholderText = string.Format(System.Globalization.CultureInfo.CurrentCulture, TextResources.Get("Filter_Column_Placeholder"), TextResources.Get("Header_Name"));
        SourceArgumentFilterBox.PlaceholderText = string.Format(System.Globalization.CultureInfo.CurrentCulture, TextResources.Get("Filter_Column_Placeholder"), TextResources.Get("Header_Argument"));
        SourceTypeFilterBox.PlaceholderText = string.Format(System.Globalization.CultureInfo.CurrentCulture, TextResources.Get("Filter_Column_Placeholder"), TextResources.Get("Header_Type"));
        SourceStatusFilterBox.PlaceholderText = string.Format(System.Globalization.CultureInfo.CurrentCulture, TextResources.Get("Filter_Column_Placeholder"), TextResources.Get("Header_Status"));

        AddSourceBtn.Content = TextResources.Get("Command_Sources_Add");

        ToolTipService.SetToolTip(RefreshSourcesBtn, TextResources.Get("Command_Sources_Refresh"));
        AutomationProperties.SetName(RefreshSourcesBtn, TextResources.Get("Command_Sources_Refresh"));

        ToolTipService.SetToolTip(UpdateSourcesBtn, TextResources.Get("Command_Sources_Update"));
        AutomationProperties.SetName(UpdateSourcesBtn, TextResources.Get("Command_Sources_Update"));

        ToolTipService.SetToolTip(RemoveSourceBtn, TextResources.Get("Command_Sources_Remove"));
        AutomationProperties.SetName(RemoveSourceBtn, TextResources.Get("Command_Sources_Remove"));

        ToolTipService.SetToolTip(ResetSourcesBtn, TextResources.Get("Command_Sources_Reset"));
        AutomationProperties.SetName(ResetSourcesBtn, TextResources.Get("Command_Sources_Reset"));
    }


    private void RefreshCommands()
    {
        var topLevelCommandIds = new[] { UiCommandId.CancelOperation };

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

    private void OnSourceFilterChanged(object sender, TextChangedEventArgs args)
    {
        viewModel.SetListFilters(
            SourceNameFilterBox.Text,
            SourceArgumentFilterBox.Text,
            SourceTypeFilterBox.Text,
            SourceStatusFilterBox.Text);
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
