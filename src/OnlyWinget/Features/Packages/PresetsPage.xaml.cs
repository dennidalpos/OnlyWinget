using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using OnlyWinget.Application.Presentation;
using OnlyWinget.DesignSystem.Commands;
using OnlyWinget.Controls;
using OnlyWinget.Presentation;
using System.ComponentModel;
using System.Linq;
using OnlyWinget.Domain.Packages;

namespace OnlyWinget.Features.Packages;

public sealed partial class PresetsPage : UserControl, IPendingNavigationGuard
{
    private bool wasExecuting;
    private bool isRefreshing;
    private Flyout? pendingFlyout;
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

        // Wire events for flyouts opening
        EditPackageFlyout.Opened += OnEditPackageFlyoutOpened;

        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
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



    private async void OnCommandInvoked(object? sender, UiCommandInvokedEventArgs args)
    {
        await ViewModel.ExecuteAsync(args.Command, string.Empty);
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
        SavePresetBtn.IsEnabled = ViewModel.PresetName.IsValid && ViewModel.PresetName.Value.Trim().Length > 0;

        RenamePresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RenamePreset);
        SaveRenamePresetBtn.IsEnabled = ViewModel.PresetName.IsValid && ViewModel.PresetName.Value.Trim().Length > 0;

        RemovePresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RemovePreset);
        ImportPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.ImportPreset);
        ExportPresetBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.ExportPreset);

        AddPackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.AddPresetPackage);
        SavePackageBtn.IsEnabled = ViewModel.PackageId.IsValid && ViewModel.PackageId.Value.Trim().Length > 0;

        EditPackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.EditPresetPackage);
        SaveEditPackageBtn.IsEnabled = ViewModel.PackageId.IsValid && ViewModel.PackageId.Value.Trim().Length > 0;

        RemovePackageBtn.IsEnabled = ViewModel.IsEnabled(UiCommandId.RemovePresetPackages);
    }

    private void ApplyOperationProgress(bool isExecuting)
    {
        var progress = ViewModel.Progress;
        if (isExecuting)
        {
            var message = OperationProgressFormatter.FormatMessage(progress, TextResources.Get);
            PageState.Show(TextResources.Get("Operation_Preset_Title"), message, progress?.PackageId, progress?.PackagePercentage, true);
        }
        else if (wasExecuting)
        {
            var error = ViewModel.Error;
            PageState.Complete(error ?? TextResources.Get("Progress_Completed"), error is not null);
        }
        wasExecuting = isExecuting;
    }

    private void OnOperationCancelRequested(object? sender, EventArgs args) => ViewModel.Cancel();

    public async Task<bool> ConfirmNavigationAsync()
    {
        if (!HasPendingEdit())
        {
            return true;
        }

        var isEditValid = IsPendingEditValid();

        var dialog = new ContentDialog
        {
            Title = TextResources.Get("Dialog_UnsavedChanges_Title"),
            Content = TextResources.Get("Dialog_UnsavedChanges_Message"),
            PrimaryButtonText = TextResources.Get("Dialog_UnsavedChanges_Apply"),
            IsPrimaryButtonEnabled = isEditValid,
            SecondaryButtonText = TextResources.Get("Dialog_UnsavedChanges_Discard"),
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
            pendingFlyout?.Hide();
            ClearPendingFields();
            return true;
        }

        return false;
    }

    private bool IsPendingEditValid()
    {
        if (pendingFlyout == AddPresetFlyout)
        {
            var text = PresetNameBox.Text.Trim();
            return !string.IsNullOrWhiteSpace(text) &&
                   !ViewModel.PresetNames.Any(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
        }

        if (pendingFlyout == RenamePresetFlyout)
        {
            var text = RenamePresetNameBox.Text.Trim();
            return !string.IsNullOrWhiteSpace(text) &&
                   !ViewModel.PresetNames.Any(name => string.Equals(name, text, StringComparison.OrdinalIgnoreCase));
        }

        if (pendingFlyout == AddPackageFlyout)
        {
            var id = PackageIdBox.Text.Trim();
            return !string.IsNullOrWhiteSpace(id) &&
                   !ViewModel.Packages.Any(p => string.Equals(p.PackageId, id, StringComparison.OrdinalIgnoreCase));
        }

        if (pendingFlyout == EditPackageFlyout)
        {
            var id = EditPackageIdBox.Text.Trim();
            var selected = ViewModel.Workflow.State.SelectedPresetPackages.SingleOrDefault();
            if (selected != null && string.Equals(selected.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return !string.IsNullOrWhiteSpace(id);
            }
            return !string.IsNullOrWhiteSpace(id) &&
                   !ViewModel.Packages.Any(p => string.Equals(p.PackageId, id, StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private bool HasPendingEdit() =>
        pendingFlyout == AddPresetFlyout && !string.IsNullOrWhiteSpace(PresetNameBox.Text) ||
        pendingFlyout == RenamePresetFlyout && !string.Equals(RenamePresetNameBox.Text.Trim(), ViewModel.ActivePresetName ?? string.Empty, StringComparison.Ordinal) ||
        pendingFlyout == AddPackageFlyout && (!string.IsNullOrWhiteSpace(PackageIdBox.Text) || !string.IsNullOrWhiteSpace(PackageSourceBox.Text)) ||
        pendingFlyout == EditPackageFlyout && (!string.IsNullOrWhiteSpace(EditPackageIdBox.Text) || !string.IsNullOrWhiteSpace(EditPackageSourceBox.Text));

    private async Task<bool> ApplyPendingEditAsync()
    {
        if (pendingFlyout == AddPresetFlyout)
        {
            await ExecuteCommandAsync(UiCommandId.AddPreset, PresetNameBox.Text);
            AddPresetFlyout.Hide();
            return true;
        }

        if (pendingFlyout == RenamePresetFlyout)
        {
            await ExecuteCommandAsync(UiCommandId.RenamePreset, RenamePresetNameBox.Text);
            RenamePresetFlyout.Hide();
            return true;
        }

        if (pendingFlyout == AddPackageFlyout)
        {
            await ExecuteCommandAsync(UiCommandId.AddPresetPackage, PackageSourceBox.Text);
            AddPackageFlyout.Hide();
            return true;
        }

        if (pendingFlyout == EditPackageFlyout)
        {
            await ExecuteCommandAsync(UiCommandId.EditPresetPackage, EditPackageSourceBox.Text);
            EditPackageFlyout.Hide();
            return true;
        }

        return true;
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
        if (ReferenceEquals(sender, pendingFlyout))
        {
            pendingFlyout = null;
        }
    }

    private async void OnAddPresetClick(object sender, RoutedEventArgs e)
    {
        await ExecuteCommandAsync(UiCommandId.AddPreset, PresetNameBox.Text);
        AddPresetFlyout.Hide();
    }

    private async void OnRenamePresetClick(object sender, RoutedEventArgs e)
    {
        await ExecuteCommandAsync(UiCommandId.RenamePreset, RenamePresetNameBox.Text);
        RenamePresetFlyout.Hide();
    }

    private async void OnRemovePresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RemovePreset, string.Empty);
    private async void OnImportPresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.ImportPreset, string.Empty);
    private async void OnExportPresetClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.ExportPreset, string.Empty);

    private async void OnAddPackageClick(object sender, RoutedEventArgs e)
    {
        await ExecuteCommandAsync(UiCommandId.AddPresetPackage, PackageSourceBox.Text);
        AddPackageFlyout.Hide();
    }

    private async void OnEditPackageClick(object sender, RoutedEventArgs e)
    {
        await ExecuteCommandAsync(UiCommandId.EditPresetPackage, EditPackageSourceBox.Text);
        EditPackageFlyout.Hide();
    }

    private async void OnRemovePackageClick(object sender, RoutedEventArgs e) => await ExecuteCommandAsync(UiCommandId.RemovePresetPackages, string.Empty);

    private void OnEditPackageFlyoutOpened(object? sender, object e)
    {
        OnPendingFlyoutOpened(sender, e);
        ViewModel.PrepareEditFields(source => EditPackageSourceBox.Text = source);
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
