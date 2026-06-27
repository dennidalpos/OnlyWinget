using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Presets;
using OnlyWinget.Domain.Packages;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace OnlyWinget.Pages;

public sealed partial class PresetsPage : Page
{
    private CancellationTokenSource? operationCancellation;
    private bool isRefreshing;

    public PresetsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        ApplyText();
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        App.WorkflowChanged += OnWorkflowChanged;
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        App.WorkflowChanged -= OnWorkflowChanged;
        operationCancellation?.Cancel();
        operationCancellation?.Dispose();
        operationCancellation = null;
    }

    private void OnWorkflowChanged(object? sender, EventArgs args) => Refresh();

    private void Refresh()
    {
        isRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(App.Workflow.State).Presets;
        var commands = state.Commands.ToDictionary(command => command.Id, StringComparer.Ordinal);

        PresetSelector.ItemsSource = state.PresetNames;
        PresetSelector.SelectedItem = state.ActivePresetName;
        PresetNameBox.Text = state.ActivePresetName ?? string.Empty;
        PackageList.ItemsSource = state.Packages;
        OperationResultList.ItemsSource = state.OperationResults;
        StatusText.Text = state.Error ?? GetEmptyText(state);
        PageUi.SetVisible(OperationResultList, state.OperationResults.Count > 0);
        ApplyOperationProgress(state.IsExecuting);

        PageUi.ApplySelectionHeader(SelectAllPackagesBox, state.HeaderState);

        PageUi.SetEnabled(AddPresetButton, commands, "preset.add");
        PageUi.SetEnabled(RenamePresetButton, commands, "preset.rename");
        PageUi.SetEnabled(RemovePresetButton, commands, "preset.remove");
        PageUi.SetEnabled(AddPackageButton, commands, "preset.package.add");
        PageUi.SetEnabled(EditPackageButton, commands, "preset.package.edit");
        PageUi.SetEnabled(RemovePackageButton, commands, "preset.package.remove");
        PageUi.SetEnabled(ImportPresetButton, commands, "preset.import");
        PageUi.SetEnabled(ExportPresetButton, commands, "preset.export");
        PageUi.SetEnabled(SaveWorkspaceButton, commands, "preset.save");
        PageUi.SetEnabled(InstallPresetButton, commands, "preset.apply.install");
        PageUi.SetEnabled(UninstallPresetButton, commands, "preset.apply.uninstall");
        PageUi.SetEnabled(CancelPresetOperationButton, commands, "operation.cancel");
        ExportPresetButton.Content = string.Format(
            global::System.Globalization.CultureInfo.CurrentCulture,
            TextResources.Get("Command_Preset_ExportNamed"),
            state.ActivePresetName ?? TextResources.Get("Preset_DefaultFileName"));
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Presets_Title");
        PresetManagementText.Text = TextResources.Get("Section_PresetManagement");
        PackageManagementText.Text = TextResources.Get("Section_PackageManagement");
        PackagesSectionText.Text = TextResources.Get("Section_Packages");
        OperationResultsText.Text = TextResources.Get("Section_OperationResults");
        PresetNameBox.Header = TextResources.Get("Preset_Name");
        PackageIdBox.Header = TextResources.Get("Package_Id");
        PackageSourceBox.Header = TextResources.Get("Package_Source");
        ImportExportText.Text = TextResources.Get("Section_ImportExport");
        AddPresetButton.Content = TextResources.Get("Command_Preset_Add");
        RenamePresetButton.Content = TextResources.Get("Command_Preset_Rename");
        RemovePresetButton.Content = TextResources.Get("Command_Preset_Remove");
        AddPackageButton.Content = TextResources.Get("Command_PresetPackage_Add");
        EditPackageButton.Content = TextResources.Get("Command_PresetPackage_Edit");
        RemovePackageButton.Content = TextResources.Get("Command_PresetPackage_Remove");
        ImportPresetButton.Content = TextResources.Get("Command_Preset_Import");
        SaveWorkspaceButton.Content = TextResources.Get("Command_Workspace_Save");
        InstallPresetButton.Content = TextResources.Get("Command_Preset_ApplyInstall");
        UninstallPresetButton.Content = TextResources.Get("Command_Preset_ApplyUninstall");
        CancelPresetOperationButton.Content = TextResources.Get("Command_Operation_Cancel");
        SelectAllPackagesBox.Content = TextResources.Get("Command_Select_All");
    }

    private void OnPresetChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isRefreshing || PresetSelector.SelectedItem is not string presetName)
        {
            return;
        }

        App.Workflow.SetActivePreset(presetName);
        Notify();
    }

    private void OnAddPreset(object sender, RoutedEventArgs args)
    {
        App.Workflow.AddPreset(PresetNameBox.Text);
        Notify();
    }

    private void OnRenamePreset(object sender, RoutedEventArgs args)
    {
        App.Workflow.RenameActivePreset(PresetNameBox.Text);
        Notify();
    }

    private void OnRemovePreset(object sender, RoutedEventArgs args)
    {
        App.Workflow.RemoveActivePreset();
        Notify();
    }

    private async void OnAddPackage(object sender, RoutedEventArgs args)
    {
        await PageUi.RunWorkflowAsync(() =>
            App.Workflow.AddPackageToActivePresetAsync(CreatePackageFromInputs(), CancellationToken.None));
    }

    private async void OnEditPackage(object sender, RoutedEventArgs args)
    {
        var selected = App.Workflow.State.SelectedPresetPackages.SingleOrDefault();
        if (selected is null)
        {
            return;
        }

        await PageUi.RunWorkflowAsync(() =>
            App.Workflow.ReplacePackageInActivePresetAsync(selected, CreatePackageFromInputs(), CancellationToken.None));
    }

    private void OnRemovePackages(object sender, RoutedEventArgs args)
    {
        App.Workflow.RemoveSelectedPackagesFromActivePreset();
        Notify();
    }

    private void OnToggleAllPackages(object sender, RoutedEventArgs args)
    {
        if (isRefreshing)
        {
            return;
        }

        App.Workflow.ToggleAllPresetPackages();
        Notify();
    }

    private void OnPackageSelectionClick(object sender, RoutedEventArgs args)
    {
        if ((sender as FrameworkElement)?.DataContext is not PresetPackageRow row)
        {
            return;
        }

        App.Workflow.TogglePresetPackage(new PackageIdentity(row.PackageId, row.Source));
        Notify();
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
            var progress = new Progress<OnlyWinget.Application.Winget.OperationProgress>(_ => Notify());
            var operation = App.Workflow.ApplyActivePresetAsync(action, operationCancellation.Token, progress);
            Notify();
            await operation;
        }
        finally
        {
            operationCancellation.Dispose();
            operationCancellation = null;
            Notify();
        }
    }

    private void OnCancelPresetOperation(object sender, RoutedEventArgs args)
    {
        operationCancellation?.Cancel();
    }

    private async void OnSaveWorkspace(object sender, RoutedEventArgs args)
    {
        var save = App.Workflow.SaveWorkspaceAsync(CancellationToken.None);
        Notify();
        await save;
        Notify();
    }

    private async void OnImportPreset(object sender, RoutedEventArgs args)
    {
        var picker = new FileOpenPicker();
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        picker.FileTypeFilter.Add(".json");
        var file = await picker.PickSingleFileAsync();
        if (file is null)
        {
            return;
        }

        var json = await FileIO.ReadTextAsync(file);
        await PageUi.RunWorkflowAsync(() => App.Workflow.ImportPresetAsync(json, CancellationToken.None));
    }

    private async void OnExportPreset(object sender, RoutedEventArgs args)
    {
        var active = App.Workflow.State.ActivePreset;
        if (active is null)
        {
            return;
        }

        var picker = new FileSavePicker
        {
            SuggestedFileName = PresetDocumentService.GetExportFileName(active.Name)
        };
        picker.FileTypeChoices.Add(TextResources.Get("Preset_FileType"), [".json"]);
        WinRT.Interop.InitializeWithWindow.Initialize(picker, App.WindowHandle);
        var file = await picker.PickSaveFileAsync();
        if (file is null)
        {
            return;
        }

        await FileIO.WriteTextAsync(file, App.Workflow.ExportActivePreset());
    }

    private PackageIdentity CreatePackageFromInputs() => new(PackageIdBox.Text, PackageSourceBox.Text);

    private void ApplyOperationProgress(bool isExecuting)
    {
        var progress = App.Workflow.State.OperationProgress;
        PageUi.SetVisible(OperationProgressBar, isExecuting);
        PageUi.SetVisible(OperationProgressText, isExecuting);
        OperationProgressBar.Value = progress?.Percentage ?? 0;
        OperationProgressText.Text = progress is null
            ? TextResources.Get("Progress_Starting")
            : $"{TextResources.Get($"Progress_{progress.Phase}")} · {progress.Percentage}% · {progress.PackageId}";
    }

    private static string GetEmptyText(PresetsPresentationState state)
    {
        if (state.PresetNames.Count == 0)
        {
            return TextResources.Get("Empty_Presets");
        }

        return state.Packages.Count == 0 ? TextResources.Get("Empty_Packages") : string.Empty;
    }

    private static void Notify()
    {
        App.NotifyWorkflowChanged();
    }
}
