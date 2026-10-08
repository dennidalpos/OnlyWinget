using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using OnlyWinget.Presentation;
using System.ComponentModel;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Features.Packages;

public sealed partial class PresetsPage : UserControl, IPendingNavigationGuard
{
    private bool isRefreshing;
    private Flyout? pendingFlyout;
    private string originalName = string.Empty;
    private string originalPackageId = string.Empty;
    private string originalPackageSource = string.Empty;
    private bool isConfirmingNavigation;
    private bool isOpeningEditor;
    private bool isSwitchingPreset;
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
    }

    private void OnLoaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Activate();
        Refresh();
    }

    private void OnUnloaded(object sender, RoutedEventArgs args)
    {
        ViewModel.Deactivate();
    }

    private void OnViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (string.IsNullOrEmpty(args.PropertyName))
        {
            Refresh();
            return;
        }

        if (args.PropertyName == nameof(PresetsViewModel.ActivePresetName))
        {
            isRefreshing = true;
            PresetSelector.SelectedItem = ViewModel.ActivePresetName;
            if (pendingFlyout is null) ViewModel.PresetName.Value = ViewModel.ActivePresetName ?? string.Empty;
            isRefreshing = false;
        }

        if (args.PropertyName == nameof(PresetsViewModel.PageState))
        {
            PageState.Present(ViewModel.PageState);
        }

        if (args.PropertyName is nameof(PresetsViewModel.Commands) or nameof(PresetsViewModel.HasUnsavedEdit) or nameof(PresetsViewModel.IsApplyingEdit))
        {
            ApplyValidationToCommands();
        }
    }

    private void Refresh()
    {
        isRefreshing = true;
        PresetSelector.SelectedItem = ViewModel.ActivePresetName;
        if (pendingFlyout is null) ViewModel.PresetName.Value = ViewModel.ActivePresetName ?? string.Empty;
        PageState.Present(ViewModel.PageState);

        ApplyValidationToCommands();
        isRefreshing = false;
    }



    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        if (args.Command.Id == UiCommandId.SaveWorkspace && HasPendingEdit()) await ApplyPendingEditAsync();
        else await ViewModel.ExecuteAsync(args.Command, string.Empty);
    }

    private async void OnPresetChanged(object sender, SelectionChangedEventArgs args)
    {
        if (isRefreshing || isSwitchingPreset || PresetSelector.SelectedItem is not string presetName)
        {
            return;
        }

        isSwitchingPreset = true;
        try
        {
            RestorePresetSelection();
            if (await ConfirmNavigationAsync()) ViewModel.SetActivePreset(presetName);
        }
        catch (Exception exception) { AppDiagnostics.WriteException("PresetsPage.OnPresetChanged", exception); }
        finally
        {
            RestorePresetSelection();
            isSwitchingPreset = false;
        }
    }

    private void RestorePresetSelection()
    {
        isRefreshing = true;
        try { PresetSelector.SelectedItem = ViewModel.ActivePresetName; }
        finally { isRefreshing = false; }
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
        RenamePresetNameBox.Description = ViewModel.PresetName.Error;
        PackageIdBox.Description = ViewModel.PackageId.Error;
        EditPackageIdBox.Description = ViewModel.PackageId.Error;
        AutomationProperties.SetHelpText(PresetNameBox, ViewModel.PresetName.Error ?? string.Empty);
        AutomationProperties.SetHelpText(RenamePresetNameBox, ViewModel.PresetName.Error ?? string.Empty);
        AutomationProperties.SetHelpText(PackageIdBox, ViewModel.PackageId.Error ?? string.Empty);
        AutomationProperties.SetHelpText(EditPackageIdBox, ViewModel.PackageId.Error ?? string.Empty);
        ApplyValidationToCommands();
    }

    private void OnPackageBatchSelectionChanged(object? sender, OnlyWingetTableBatchSelectionEventArgs args)
    {
        if (isRefreshing) return;
        var rows = args.Items.OfType<PresetPackageRow>();
        ViewModel.SetSelected(rows, args.IsSelected);
    }

    private static IEnumerable<PackageIdentity> ParsePackageIdentities(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;

        var lines = text.Split(["\r\n", "\r", "\n"], StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0) continue;

            var tabs = trimmed.Split('\t');
            if (tabs.Length >= 3)
            {
                var id = tabs[1].Trim();
                var src = tabs[2].Trim();
                if (!string.IsNullOrEmpty(id))
                {
                    yield return new PackageIdentity(id, string.IsNullOrEmpty(src) ? null : src);
                    continue;
                }
            }

            if (trimmed.Contains('|'))
            {
                var parts = trimmed.Split('|');
                if (parts.Length == 2)
                {
                    var part0 = parts[0].Trim();
                    var part1 = parts[1].Trim();
                    if (!string.IsNullOrEmpty(part1))
                    {
                        yield return new PackageIdentity(part1, string.IsNullOrEmpty(part0) ? null : part0);
                        continue;
                    }
                }
            }

            yield return new PackageIdentity(trimmed, null);
        }
    }

    private async void OnPackageListPasteRequested(object? sender, OnlyWingetTablePasteEventArgs args)
    {
        if (isRefreshing) return;
        var packages = ParsePackageIdentities(args.Text).ToList();
        if (packages.Count == 0) return;

        await ViewModel.AddPackagesAsync(packages);
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

        AddPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.AddPreset);
        SavePresetBtn.IsEnabled = CanSaveEdit(UiCommandId.AddPreset, ViewModel.PresetName);

        RenamePresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RenamePreset);
        SaveRenamePresetBtn.IsEnabled = CanSaveEdit(UiCommandId.RenamePreset, ViewModel.PresetName);

        RemovePresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RemovePreset);
        ImportPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.ImportPreset);
        ExportPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.ExportPreset);

        AddPackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.AddPresetPackage);
        SavePackageBtn.IsEnabled = CanSaveEdit(UiCommandId.AddPresetPackage, ViewModel.PackageId);

        EditPackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.EditPresetPackage);
        SaveEditPackageBtn.IsEnabled = CanSaveEdit(UiCommandId.EditPresetPackage, ViewModel.PackageId);

        RemovePackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RemovePresetPackages);
        foreach (var field in new[] { PresetNameBox, RenamePresetNameBox, PackageIdBox, PackageSourceBox, EditPackageIdBox, EditPackageSourceBox })
        {
            field.IsEnabled = !ViewModel.HasUnsavedEdit && !ViewModel.IsApplyingEdit;
        }
    }

    private bool CanSaveEdit(UiCommandId id, ValidatedField field) =>
        !ViewModel.IsApplyingEdit && !ViewModel.IsExecuting && (ViewModel.HasUnsavedEdit || ViewModel.IsEnabled(id) && field.IsValid && field.Value.Trim().Length > 0);

    private void OnOperationCancelRequested(object? sender, EventArgs args) => ViewModel.Cancel();

    public async Task<bool> ConfirmNavigationAsync()
    {
        if (ViewModel.IsApplyingEdit || isConfirmingNavigation) return false;
        if (!HasPendingEdit())
        {
            return true;
        }

        if (XamlRoot is null) return false;

        isConfirmingNavigation = true;
        try
        {
            var isEditValid = IsPendingEditValid();

            var dialog = new ContentDialog
            {
                Title = TextResources.Get("Dialog_UnsavedChanges_Title"),
                Content = TextResources.Get(ViewModel.HasUnsavedEdit ? "Dialog_UnsavedChanges_SaveFailed" : "Dialog_UnsavedChanges_Message"),
                PrimaryButtonText = TextResources.Get("Dialog_UnsavedChanges_Apply"),
                IsPrimaryButtonEnabled = isEditValid,
                SecondaryButtonText = ViewModel.HasUnsavedEdit ? string.Empty : TextResources.Get("Dialog_UnsavedChanges_Discard"),
                CloseButtonText = TextResources.Get("Dialog_Cancel"),
                DefaultButton = isEditValid ? ContentDialogButton.Primary : ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };

            var result = await dialog.ShowAsync();
            if (result == ContentDialogResult.Primary)
            {
                return await ApplyPendingEditAsync();
            }

            if (result == ContentDialogResult.Secondary)
            {
                CompletePendingEdit();
                return true;
            }

            return false;
        }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("PresetsPage.ConfirmNavigationAsync", exception);
            return false;
        }
        finally { isConfirmingNavigation = false; }
    }

    private bool IsPendingEditValid()
    {
        if (ViewModel.HasUnsavedEdit) return true;
        if (pendingFlyout == AddPresetFlyout)
        {
            ViewModel.PresetName.Validate();
            return ViewModel.PresetName.IsValid;
        }

        if (pendingFlyout == RenamePresetFlyout)
        {
            ViewModel.PresetName.Validate();
            return ViewModel.PresetName.IsValid;
        }

        if (pendingFlyout == AddPackageFlyout)
        {
            ViewModel.PackageId.Validate();
            return ViewModel.PackageId.IsValid;
        }

        if (pendingFlyout == EditPackageFlyout)
        {
            ViewModel.PackageId.Validate();
            return ViewModel.PackageId.IsValid;
        }

        return false;
    }

    private bool HasPendingEdit() => ViewModel.HasUnsavedEdit || pendingFlyout is not null &&
        (pendingFlyout == AddPresetFlyout || pendingFlyout == RenamePresetFlyout
            ? !string.Equals(ViewModel.PresetName.Value.Trim(), originalName, StringComparison.Ordinal)
            : !string.Equals(ViewModel.PackageId.Value.Trim(), originalPackageId, StringComparison.Ordinal) ||
              !string.Equals(ViewModel.PackageSource.Trim(), originalPackageSource, StringComparison.Ordinal));

    private async Task<bool> ApplyPendingEditAsync()
    {
        var id = pendingFlyout == AddPresetFlyout ? UiCommandId.AddPreset
            : pendingFlyout == RenamePresetFlyout ? UiCommandId.RenamePreset
            : pendingFlyout == AddPackageFlyout ? UiCommandId.AddPresetPackage
            : pendingFlyout == EditPackageFlyout ? UiCommandId.EditPresetPackage
            : (UiCommandId?)null;
        try
        {
            if (id is null || !await ViewModel.ExecuteEditAsync(id.Value, ViewModel.PackageSource)) return false;
            CompletePendingEdit();
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception exception)
        {
            AppDiagnostics.WriteException("PresetsPage.ApplyPendingEditAsync", exception);
            return false;
        }
    }

    private void CompletePendingEdit()
    {
        var flyout = pendingFlyout;
        pendingFlyout = null;
        flyout?.Hide();
        ClearPendingFields();
    }

    private void ClearPendingFields()
    {
        PresetNameBox.Text = string.Empty;
        PackageIdBox.Text = string.Empty;
        PackageSourceBox.Text = string.Empty;
        EditPackageIdBox.Text = string.Empty;
        EditPackageSourceBox.Text = string.Empty;
        ViewModel.PresetName.Clear();
        ViewModel.PackageId.Clear();
        ViewModel.PackageSource = string.Empty;
    }

    private void OnPendingFlyoutOpened(object? sender, object e)
    {
        if (sender is Flyout flyout)
        {
            pendingFlyout = flyout;
        }
    }

    private void OnPendingFlyoutClosed(object? sender, object e)
    {
        if (ReferenceEquals(sender, pendingFlyout) && !ViewModel.IsApplyingEdit && !ViewModel.IsExecuting && !HasPendingEdit())
        {
            pendingFlyout = null;
        }
    }

    private async void OnAddPresetClick(object sender, RoutedEventArgs e) => await ApplyPendingEditAsync();
    private async void OnRenamePresetClick(object sender, RoutedEventArgs e) => await ApplyPendingEditAsync();

    private async void OnRemovePresetClick(object sender, RoutedEventArgs e)
    {
        if (await ConfirmNavigationAsync()) await ExecuteCommandAsync(UiCommandId.RemovePreset, string.Empty);
    }
    private async void OnImportPresetClick(object sender, RoutedEventArgs e)
    {
        if (await ConfirmNavigationAsync()) await ExecuteCommandAsync(UiCommandId.ImportPreset, string.Empty);
    }
    private async void OnExportPresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.ExportPreset, string.Empty);

    private async void OnAddPackageClick(object sender, RoutedEventArgs e) => await ApplyPendingEditAsync();
    private async void OnEditPackageClick(object sender, RoutedEventArgs e) => await ApplyPendingEditAsync();

    private async void OnRemovePackageClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RemovePresetPackages, string.Empty);

    private async void OnOpenEditorClick(object sender, RoutedEventArgs e)
    {
        if (isOpeningEditor || isConfirmingNavigation || sender is not FrameworkElement button ||
            FlyoutBase.GetAttachedFlyout(button) is not Flyout flyout) return;
        isOpeningEditor = true;
        try
        {
            if (pendingFlyout != flyout)
            {
                if (!await ConfirmNavigationAsync()) return;
                pendingFlyout = flyout;
                if (flyout == AddPackageFlyout) ViewModel.PrepareAddPackage();
                else if (flyout == EditPackageFlyout)
                {
                    if (!ViewModel.PrepareEditFields())
                    {
                        pendingFlyout = null;
                        return;
                    }
                }
                else ViewModel.PreparePresetName(flyout == RenamePresetFlyout);
                originalName = ViewModel.PresetName.Value.Trim();
                originalPackageId = ViewModel.PackageId.Value.Trim();
                originalPackageSource = ViewModel.PackageSource.Trim();
            }
            flyout.ShowAt(button);
        }
        catch (Exception exception) { AppDiagnostics.WriteException("PresetsPage.OnOpenEditorClick", exception); }
        finally { isOpeningEditor = false; }
    }

    private async System.Threading.Tasks.Task ExecuteCommandAsync(UiCommandId id, string source)
    {
        if (ViewModel.Commands.TryGetValue(id, out var command))
        {
            await ViewModel.ExecuteAsync(command, source);
        }
    }

    private void Dispatch(Action action)
    {
        if (DispatcherQueue.HasThreadAccess) action();
        else _ = DispatcherQueue.TryEnqueue(() => action());
    }
}
