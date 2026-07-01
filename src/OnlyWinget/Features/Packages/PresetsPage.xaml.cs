using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using OnlyWinget.Presentation;
using System.ComponentModel;

namespace OnlyWinget.Features.Packages;

public sealed partial class PresetsPage : UserControl
{
    private bool wasExecuting;
    private bool isRefreshing;
    private readonly PresetsViewModel viewModel;

    public PresetsPage()
    {
        InitializeComponent();
        viewModel = new(Dispatch);
        viewModel.PresetName.PropertyChanged += OnFieldValidationChanged;
        viewModel.PackageId.PropertyChanged += OnFieldValidationChanged;
        viewModel.PropertyChanged += OnViewModelChanged;
        PresetNameBox.TextChanged += OnPresetNameChanged;
        PackageIdBox.TextChanged += OnPackageIdChanged;
        PresetSelector.ItemsSource = viewModel.PresetNames;
        PackageList.ItemsSource = viewModel.Packages;
        OperationResultList.ItemsSource = viewModel.OperationResults;
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
        PresetSelector.SelectedItem = viewModel.ActivePresetName;
        PresetNameBox.Text = viewModel.ActivePresetName ?? string.Empty;
        PageState.Present(viewModel.PageState);
        OperationResultList.Visibility = viewModel.OperationResults.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
        OperationResultsText.Visibility = OperationResultList.Visibility;
        ApplyOperationProgress(viewModel.IsExecuting);

        PackageList.HeaderSelection = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };

        ApplyValidationToCommands();
        isRefreshing = false;
    }

    private void ApplyText()
    {
        PresetManagementText.Text = TextResources.Get("Section_PresetManagement");
        PackageManagementText.Text = TextResources.Get("Section_PackageManagement");
        PackagesSectionText.Text = TextResources.Get("Section_Packages");
        OperationResultsText.Text = TextResources.Get("Section_OperationResults");
        PresetNameBox.Header = TextResources.Get("Preset_Name");
        PackageIdBox.Header = TextResources.Get("Package_Id");
        PackageSourceBox.Header = TextResources.Get("Package_Source");
        ImportExportText.Text = TextResources.Get("Section_ImportExport");
        PackageList.SelectionLabel = TextResources.Get("Command_Select_All");
        PackageList.SetHeaders(new[] { "Header_Name", "Header_PackageId", "Header_Source", "Header_Version", "Header_Architecture" }.Select(TextResources.Get).ToArray());
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        await viewModel.ExecuteAsync(args.Command, PackageSourceBox.Text);
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isRefreshing || PresetSelector.SelectedItem is not string presetName)
        {
            return;
        }

        viewModel.SetActivePreset(presetName);
    }

    private void OnToggleAllPackages(object? sender, EventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        viewModel.ToggleAll();
    }

    private void OnPresetNameChanged(object sender, TextChangedEventArgs args) => viewModel.PresetName.Value = PresetNameBox.Text;
    private void OnPackageIdChanged(object sender, TextChangedEventArgs args) => viewModel.PackageId.Value = PackageIdBox.Text;

    private void OnFieldValidationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        PresetNameBox.Description = viewModel.PresetName.Error;
        PackageIdBox.Description = viewModel.PackageId.Error;
        AutomationProperties.SetHelpText(PresetNameBox, viewModel.PresetName.Error ?? string.Empty);
        AutomationProperties.SetHelpText(PackageIdBox, viewModel.PackageId.Error ?? string.Empty);
        ApplyValidationToCommands();
    }

    private void OnPackageSelectionToggled(object? sender, OnlyWingetTableSelectionEventArgs args)
    {
        if (args.Item is PresetPackageRow row)
            viewModel.Toggle(row);
    }

    private void ApplyValidationToCommands()
    {
        CommandBar.SetCommands(viewModel.Commands.Values.Select(command => command.Id switch
        {
            UiCommandId.AddPreset or UiCommandId.RenamePreset => command with { IsEnabled = command.IsEnabled && viewModel.PresetName.IsValid && viewModel.PresetName.Value.Trim().Length > 0 },
            UiCommandId.AddPresetPackage => command with { IsEnabled = command.IsEnabled && viewModel.PackageId.IsValid && viewModel.PackageId.Value.Trim().Length > 0 },
            _ => command
        }));
    }

    private void ApplyOperationProgress(bool isExecuting)
    {
        var progress = viewModel.Progress;
        if (isExecuting)
        {
            OperationStatus.Show(TextResources.Get("Operation_Preset_Title"), TextResources.Get(progress is null ? "Progress_Starting" : $"Progress_{progress.Phase}"), progress?.PackageId, progress?.Percentage, true);
        }
        else if (wasExecuting)
        {
            var error = viewModel.Error;
            OperationStatus.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasExecuting = isExecuting;
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => viewModel.Cancel();

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
