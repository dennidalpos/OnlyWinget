using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;

namespace OnlyWinget.Pages;

public sealed partial class PresetsPage : Page
{
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
        StatusText.Text = state.Error ?? GetEmptyText(state);

        SelectAllPackagesBox.IsThreeState = true;
        SelectAllPackagesBox.IsChecked = state.HeaderState switch
        {
            SelectionHeaderState.Checked => true,
            SelectionHeaderState.Mixed => null,
            _ => false
        };

        SetEnabled(AddPresetButton, commands, "preset.add");
        SetEnabled(RenamePresetButton, commands, "preset.rename");
        SetEnabled(RemovePresetButton, commands, "preset.remove");
        SetEnabled(AddPackageButton, commands, "preset.package.add");
        SetEnabled(EditPackageButton, commands, "preset.package.edit");
        SetEnabled(RemovePackageButton, commands, "preset.package.remove");
        SetEnabled(ImportPresetButton, commands, "preset.import");
        SetEnabled(ExportPresetButton, commands, "preset.export");
        SetEnabled(SaveWorkspaceButton, commands, "preset.save");
        SetEnabled(InstallPresetButton, commands, "preset.apply.install");
        SetEnabled(UninstallPresetButton, commands, "preset.apply.uninstall");
        isRefreshing = false;
    }

    private void ApplyText()
    {
        TitleText.Text = TextResources.Get("Presets_Title");
        PresetNameBox.Header = TextResources.Get("Preset_Name");
        PackageIdBox.Header = TextResources.Get("Package_Id");
        PackageSourceBox.Header = TextResources.Get("Package_Source");
        ImportJsonBox.Header = TextResources.Get("Import_Json");
        AddPresetButton.Content = TextResources.Get("Command_Preset_Add");
        RenamePresetButton.Content = TextResources.Get("Command_Preset_Rename");
        RemovePresetButton.Content = TextResources.Get("Command_Preset_Remove");
        AddPackageButton.Content = TextResources.Get("Command_PresetPackage_Add");
        EditPackageButton.Content = TextResources.Get("Command_PresetPackage_Edit");
        RemovePackageButton.Content = TextResources.Get("Command_PresetPackage_Remove");
        ImportPresetButton.Content = TextResources.Get("Command_Preset_Import");
        ExportPresetButton.Content = TextResources.Get("Command_Preset_Export");
        SaveWorkspaceButton.Content = TextResources.Get("Command_Workspace_Save");
        InstallPresetButton.Content = TextResources.Get("Command_Preset_ApplyInstall");
        UninstallPresetButton.Content = TextResources.Get("Command_Preset_ApplyUninstall");
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

    private void OnAddPackage(object sender, RoutedEventArgs args)
    {
        App.Workflow.AddPackageToActivePreset(CreatePackageFromInputs());
        Notify();
    }

    private void OnEditPackage(object sender, RoutedEventArgs args)
    {
        var selected = App.Workflow.State.SelectedPresetPackages.SingleOrDefault();
        if (selected is null)
        {
            return;
        }

        App.Workflow.ReplacePackageInActivePreset(selected, CreatePackageFromInputs());
        Notify();
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
        var install = App.Workflow.ApplyActivePresetAsync(PackageAction.Install, CancellationToken.None);
        Notify();
        await install;
        Notify();
    }

    private async void OnUninstallPreset(object sender, RoutedEventArgs args)
    {
        var uninstall = App.Workflow.ApplyActivePresetAsync(PackageAction.Uninstall, CancellationToken.None);
        Notify();
        await uninstall;
        Notify();
    }

    private async void OnSaveWorkspace(object sender, RoutedEventArgs args)
    {
        var save = App.Workflow.SaveWorkspaceAsync(CancellationToken.None);
        Notify();
        await save;
        Notify();
    }

    private void OnImportPreset(object sender, RoutedEventArgs args)
    {
        App.Workflow.ImportPreset(ImportJsonBox.Text);
        Notify();
    }

    private void OnExportPreset(object sender, RoutedEventArgs args)
    {
        ImportJsonBox.Text = App.Workflow.ExportActivePreset();
        Notify();
    }

    private PackageIdentity CreatePackageFromInputs() => new(PackageIdBox.Text, PackageSourceBox.Text);

    private static void SetEnabled(Control control, IReadOnlyDictionary<string, PresentationCommand> commands, string id)
    {
        if (commands.TryGetValue(id, out var command))
        {
            control.IsEnabled = command.IsEnabled;
        }
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
