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
    public PresetsViewModel ViewModel { get; }

    public PresetsPage()
    {
        ViewModel = new(Dispatch);
        InitializeComponent();
        ViewModel.PresetName.PropertyChanged += OnFieldValidationChanged;
        ViewModel.PackageId.PropertyChanged += OnFieldValidationChanged;
        ViewModel.PropertyChanged += OnViewModelChanged;
        PresetSelector.ItemsSource = ViewModel.PresetNames;
        PageState.CancelRequested += OnOperationCancelRequested;
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
        isRefreshing = true;
        PresetSelector.SelectedItem = ViewModel.ActivePresetName;
        ViewModel.PresetName.Value = ViewModel.ActivePresetName ?? string.Empty;
        PageState.Present(ViewModel.PageState);
        ApplyOperationProgress(ViewModel.IsExecuting);

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

        AddPresetBtn.Content = TextResources.Get("Command_Preset_Add");
        RenamePresetBtn.Content = TextResources.Get("Command_Preset_Rename");
        RemovePresetBtn.Content = TextResources.Get("Command_Preset_Remove");
        ImportPresetBtn.Content = TextResources.Get("Command_Preset_Import");
        ExportPresetBtn.Content = TextResources.Get("Command_Preset_Export");
        AddPackageBtn.Content = TextResources.Get("Command_PresetPackage_Add");
        EditPackageBtn.Content = TextResources.Get("Command_PresetPackage_Edit");
        RemovePackageBtn.Content = TextResources.Get("Command_PresetPackage_Remove");
    }

    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        await ViewModel.ExecuteAsync(args.Command, PackageSourceBox.Text);
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isRefreshing || PresetSelector.SelectedItem is not string presetName)
        {
            return;
        }

        ViewModel.SetActivePreset(presetName);
    }

    private void OnToggleAllPackages(object? sender, EventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        ViewModel.ToggleAll();
    }



    private void OnFieldValidationChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        PresetNameBox.Description = ViewModel.PresetName.Error;
        PackageIdBox.Description = ViewModel.PackageId.Error;
        AutomationProperties.SetHelpText(PresetNameBox, ViewModel.PresetName.Error ?? string.Empty);
        AutomationProperties.SetHelpText(PackageIdBox, ViewModel.PackageId.Error ?? string.Empty);
        ApplyValidationToCommands();
    }

    private void OnPackageSelectionToggled(object? sender, OnlyWingetTableSelectionEventArgs args)
    {
        if (args.Item is PresetPackageRow row)
            ViewModel.Toggle(row);
    }

    private void ApplyValidationToCommands()
    {
        var topLevelCommandIds = new[]
        {
            UiCommandId.SaveWorkspace,
            UiCommandId.InstallPreset,
            UiCommandId.UninstallPreset,
            UiCommandId.CancelOperation
        };

        CommandBar.SetCommands(ViewModel.Commands.Values
            .Where(c => topLevelCommandIds.Contains(c.Id)));

        AddPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.AddPreset) && ViewModel.PresetName.IsValid && ViewModel.PresetName.Value.Trim().Length > 0;
        RenamePresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RenamePreset) && ViewModel.PresetName.IsValid && ViewModel.PresetName.Value.Trim().Length > 0;
        RemovePresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RemovePreset);
        ImportPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.ImportPreset);
        ExportPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.ExportPreset);

        AddPackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.AddPresetPackage) && ViewModel.PackageId.IsValid && ViewModel.PackageId.Value.Trim().Length > 0;
        EditPackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.EditPresetPackage) && ViewModel.PackageId.IsValid && ViewModel.PackageId.Value.Trim().Length > 0;
        RemovePackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RemovePresetPackages);
    }

    private void ApplyOperationProgress(bool isExecuting)
    {
        var progress = ViewModel.Progress;
        if (isExecuting)
        {
            PageState.Show(TextResources.Get("Operation_Preset_Title"), TextResources.Get(progress is null ? "Progress_Starting" : $"Progress_{progress.Phase}"), progress?.PackageId, progress?.Percentage, true);
        }
        else if (wasExecuting)
        {
            var error = ViewModel.Error;
            PageState.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasExecuting = isExecuting;
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => ViewModel.Cancel();

    private async void OnAddPresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.AddPreset);
    private async void OnRenamePresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RenamePreset);
    private async void OnRemovePresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RemovePreset);
    private async void OnImportPresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.ImportPreset);
    private async void OnExportPresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.ExportPreset);
    private async void OnAddPackageClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.AddPresetPackage);
    private async void OnEditPackageClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.EditPresetPackage);
    private async void OnRemovePackageClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RemovePresetPackages);

    private async System.Threading.Tasks.Task ExecuteCommandAsync(UiCommandId id)
    {
        if (ViewModel.Commands.TryGetValue(id, out var command))
        {
            await ViewModel.ExecuteAsync(command, PackageSourceBox.Text);
        }
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
