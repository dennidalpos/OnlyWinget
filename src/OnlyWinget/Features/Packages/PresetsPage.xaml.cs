using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Presets;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Presentation;
using System.ComponentModel;

namespace OnlyWinget.Features.Packages;

public sealed partial class PresetsPage : UserControl
{
    private CancellationTokenSource? operationCancellation;
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
        ApplyOperationProgress(viewModel.IsExecuting);

        SelectAllPackagesBox.IsThreeState = true;
        SelectAllPackagesBox.IsChecked = viewModel.HeaderState switch { OnlyWinget.Domain.Selection.SelectionHeaderState.Checked => true, OnlyWinget.Domain.Selection.SelectionHeaderState.Mixed => null, _ => false };

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
        SelectAllPackagesBox.Content = TextResources.Get("Command_Select_All");
        PresetNameHeader.Text = TextResources.Get("Header_Name");
        PresetPackageIdHeader.Text = TextResources.Get("Header_PackageId");
        PresetSourceHeader.Text = TextResources.Get("Header_Source");
        PresetVersionHeader.Text = TextResources.Get("Header_Version");
        PresetArchitectureHeader.Text = TextResources.Get("Header_Architecture");
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        if (args.Command.ConfirmationResourceKey is { } confirmation &&
            !await App.UiServices.Confirmation.ConfirmAsync(XamlRoot, args.Command.LabelResourceKey, confirmation))
        {
            return;
        }

        switch (args.Command.Id)
        {
            case UiCommandId.AddPreset: OnAddPreset(this, new RoutedEventArgs()); break;
            case UiCommandId.RenamePreset: OnRenamePreset(this, new RoutedEventArgs()); break;
            case UiCommandId.RemovePreset: OnRemovePreset(this, new RoutedEventArgs()); break;
            case UiCommandId.AddPresetPackage: OnAddPackage(this, new RoutedEventArgs()); break;
            case UiCommandId.EditPresetPackage: OnEditPackage(this, new RoutedEventArgs()); break;
            case UiCommandId.RemovePresetPackages: OnRemovePackages(this, new RoutedEventArgs()); break;
            case UiCommandId.ImportPreset: OnImportPreset(this, new RoutedEventArgs()); break;
            case UiCommandId.ExportPreset: OnExportPreset(this, new RoutedEventArgs()); break;
            case UiCommandId.SaveWorkspace: OnSaveWorkspace(this, new RoutedEventArgs()); break;
            case UiCommandId.InstallPreset: OnInstallPreset(this, new RoutedEventArgs()); break;
            case UiCommandId.UninstallPreset: OnUninstallPreset(this, new RoutedEventArgs()); break;
            case UiCommandId.CancelOperation: OnCancelPresetOperation(this, new RoutedEventArgs()); break;
        }
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isRefreshing || PresetSelector.SelectedItem is not string presetName)
        {
            return;
        }

        App.Workflow.SetActivePreset(presetName);
    }

    private void OnAddPreset(object sender, RoutedEventArgs args)
    {
        viewModel.PresetName.Validate();
        if (viewModel.PresetName.IsValid)
        {
            App.Workflow.AddPreset(viewModel.PresetName.Value.Trim());
        }
    }

    private void OnRenamePreset(object sender, RoutedEventArgs args)
    {
        viewModel.PresetName.Validate();
        if (viewModel.PresetName.IsValid)
        {
            App.Workflow.RenameActivePreset(viewModel.PresetName.Value.Trim());
        }
    }

    private void OnRemovePreset(object sender, RoutedEventArgs args)
    {
        App.Workflow.RemoveActivePreset();
    }

    private async void OnAddPackage(object sender, RoutedEventArgs args)
    {
        viewModel.PackageId.Validate();
        if (viewModel.PackageId.IsValid)
        {
            await App.Workflow.AddPackageToActivePresetAsync(CreatePackageFromInputs(), CancellationToken.None);
        }
    }

    private async void OnEditPackage(object sender, RoutedEventArgs args)
    {
        var selected = App.Workflow.State.SelectedPresetPackages.SingleOrDefault();
        if (selected is null)
        {
            return;
        }

        await App.Workflow.ReplacePackageInActivePresetAsync(selected, CreatePackageFromInputs(), CancellationToken.None);
    }

    private void OnRemovePackages(object sender, RoutedEventArgs args)
    {
        App.Workflow.RemoveSelectedPackagesFromActivePreset();
    }

    private void OnToggleAllPackages(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        App.Workflow.ToggleAllPresetPackages();
    }

    private void OnPackageSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not PresetPackageRow row)
        {
            return;
        }

        App.Workflow.TogglePresetPackage(new PackageIdentity(row.PackageId, row.Source));
    }

    private async void OnInstallPreset(object sender, RoutedEventArgs args)
    {
        await ApplyPresetAsync(PackageAction.Install);
    }

    private async void OnUninstallPreset(object sender, RoutedEventArgs args)
    {
        await ApplyPresetAsync(PackageAction.Uninstall);
    }

    private async Task ApplyPresetAsync(PackageAction action)
    {
        operationCancellation?.Dispose();
        operationCancellation = new CancellationTokenSource();
        try
        {
            await App.Workflow.ApplyActivePresetAsync(action, operationCancellation.Token);
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
        }
    }

    private void OnCancelPresetOperation(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Cancel();
    }

    private async void OnSaveWorkspace(object sender, RoutedEventArgs args)
    {
        await App.Workflow.SaveWorkspaceAsync(CancellationToken.None);
    }

    private async void OnImportPreset(object sender, RoutedEventArgs args)
    {
        try
        {
            var json = await App.UiServices.FilePicker.PickAndReadTextAsync(
                App.WindowId,
                ".json",
                CancellationToken.None);
            if (json is null)
            {
                return;
            }

            await App.Workflow.ImportPresetAsync(json, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            App.Workflow.ReportExternalFailure(TextResources.Get("Error_PresetImportRead"));
        }
    }

    private async void OnExportPreset(object sender, RoutedEventArgs args)
    {
        var active = App.Workflow.State.ActivePreset;
        if (active is null)
        {
            return;
        }

        try
        {
            await App.UiServices.FilePicker.PickAndWriteTextAsync(
                App.WindowId,
                PresetDocumentService.GetExportFileName(active.Name),
                ".json",
                "Preset_FileType",
                App.Workflow.ExportActivePreset(),
                CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            App.Workflow.ReportExternalFailure(TextResources.Get("Error_PresetExportWrite"));
        }
    }

    private PackageIdentity CreatePackageFromInputs() => new(viewModel.PackageId.Value.Trim(), PackageSourceBox.Text.Trim());

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
        var progress = App.Workflow.State.OperationProgress;
        if (isExecuting)
        {
            OperationStatus.Show(TextResources.Get("Operation_Preset_Title"), TextResources.Get(progress is null ? "Progress_Starting" : $"Progress_{progress.Phase}"), progress?.PackageId, progress?.Percentage, operationCancellation is not null);
        }
        else if (wasExecuting)
        {
            var error = App.Workflow.State.UserVisibleError;
            OperationStatus.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasExecuting = isExecuting;
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => operationCancellation?.Cancel();

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
