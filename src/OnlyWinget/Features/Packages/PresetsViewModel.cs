using CommunityToolkit.Mvvm.ComponentModel;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.App;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Application.Presets;
using OnlyWinget.Application.Winget;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Packages;

public sealed partial class PresetsViewModel : FeatureViewModel
{
    [ObservableProperty]
    private bool isExecuting;

    [ObservableProperty]
    private FeatureState pageState = FeatureState.Ready;

    [ObservableProperty]
    private string? activePresetName;

    [ObservableProperty]
    private SelectionHeaderState headerState;

    private CancellationTokenSource? cancellation;

    public PresetsViewModel(Action<Action> dispatch) : base(App.Workflow, dispatch)
    {
        PresetName = new(ValidatePresetName);
        PackageId = new(ValidatePackageId);
    }

    public ObservableCollection<string> PresetNames { get; } = [];
    public ObservableCollection<PresetPackageRow> Packages { get; } = [];
    public ObservableCollection<OperationResultRow> OperationResults { get; } = [];
    public ValidatedField PresetName { get; }
    public ValidatedField PackageId { get; }
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();
    public bool HasOperationResults => OperationResults.Count > 0;
    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public OperationProgress? Progress => Workflow.State.OperationProgress;
    public string? Error => Workflow.State.UserVisibleError;

    public void SetActivePreset(string name) => Workflow.SetActivePreset(name);
    public void ToggleAll() => Workflow.ToggleAllPresetPackages();
    public void Toggle(PresetPackageRow row) => Workflow.TogglePresetPackageInclusion(new PackageIdentity(row.PackageId, row.Source));
    public void SetSelected(IEnumerable<PresetPackageRow> rows, bool isSelected) =>
        Workflow.SetPresetPackagesInclusion(rows.Select(row => new PackageIdentity(row.PackageId, row.Source)), isSelected);
    public void Select(PresetPackageRow row) => Workflow.SelectPresetPackage(new PackageIdentity(row.PackageId, row.Source));
    public void Cancel() => cancellation?.Cancel();

    public async Task AddPackagesAsync(IEnumerable<PackageIdentity> packages)
    {
        var succeeded = false;
        await RunAsync(async token =>
        {
            succeeded = (await Workflow.AddPackagesToActivePresetAsync(packages, token)).Succeeded;
        });
        if (succeeded)
        {
            await AutoSaveWorkspaceAsync();
        }
    }

    public async Task ExecuteAsync(UiCommand command, string source)
    {
        if (command.ConfirmationResourceKey is { } confirmation && !await ConfirmAsync(command.LabelResourceKey, confirmation)) return;
        switch (command.Id)
        {
            case UiCommandId.AddPreset:
                if (Validate(PresetName))
                {
                    if (Workflow.AddPreset(PresetName.Value.Trim()).Succeeded)
                    {
                        await AutoSaveWorkspaceAsync();
                    }
                    PresetName.Clear();
                }
                break;
            case UiCommandId.RenamePreset:
                if (Validate(PresetName))
                {
                    if (Workflow.RenameActivePreset(PresetName.Value.Trim()).Succeeded)
                    {
                        await AutoSaveWorkspaceAsync();
                    }
                    PresetName.Clear();
                }
                break;
            case UiCommandId.RemovePreset:
                if (Workflow.RemoveActivePreset().Succeeded)
                {
                    await AutoSaveWorkspaceAsync();
                }
                break;
            case UiCommandId.AddPresetPackage:
                if (Validate(PackageId))
                {
                    if (await RunResultAsync(token => Workflow.AddPackageToActivePresetAsync(Package(source), token)))
                    {
                        await AutoSaveWorkspaceAsync();
                    }
                    PackageId.Clear();
                }
                break;
            case UiCommandId.EditPresetPackage when Workflow.State.SelectedPresetPackages.SingleOrDefault() is { } selected:
                if (Validate(PackageId))
                {
                    if (await RunResultAsync(token => Workflow.ReplacePackageInActivePresetAsync(selected, Package(source), token)))
                    {
                        await AutoSaveWorkspaceAsync();
                    }
                    PackageId.Clear();
                }
                break;
            case UiCommandId.RemovePresetPackages:
                if (Workflow.RemoveSelectedPackagesFromActivePreset().Succeeded)
                {
                    await AutoSaveWorkspaceAsync();
                }
                break;
            case UiCommandId.ImportPreset: await ImportAsync(); break;
            case UiCommandId.ExportPreset: await ExportAsync(); break;
            case UiCommandId.SaveWorkspace: await RunAsync(token => Workflow.SaveWorkspaceAsync(token)); break;
            case UiCommandId.InstallPreset: await ApplyAsync(PackageAction.Install); break;
            case UiCommandId.UninstallPreset: await ApplyAsync(PackageAction.Uninstall); break;
            case UiCommandId.CancelOperation: Cancel(); break;
        }
    }

    private PackageIdentity Package(string source) => new(PackageId.Value.Trim(), source.Trim());
    private static bool Validate(ValidatedField field) { field.Validate(); return field.IsValid; }
    private static Task<bool> ConfirmAsync(string title, string message) => App.XamlRoot is { } root
        ? App.UiServices.Confirmation.ConfirmAsync(root, title, message) : Task.FromResult(false);

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (cancellation is not null) return;
        using var current = new CancellationTokenSource();
        cancellation = current;
        try { await action(current.Token); }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; }
    }

    private async Task<bool> RunResultAsync(Func<CancellationToken, Task<ApplicationActionResult>> action)
    {
        var succeeded = false;
        await RunAsync(async token =>
        {
            succeeded = (await action(token)).Succeeded;
        });
        return succeeded;
    }

    private Task AutoSaveWorkspaceAsync() =>
        RunAsync(token => Workflow.SaveWorkspaceAsync(token));

    private Task ApplyAsync(PackageAction action) =>
        RunAsync(token => Workflow.ApplyActivePresetAsync(action, token));

    private async Task ImportAsync()
    {
        try
        {
            var imported = false;
            await RunAsync(async token =>
            {
                var json = await App.UiServices.FilePicker.PickAndReadTextAsync(App.WindowId, ".json", token);
                if (json is not null)
                {
                    imported = (await Workflow.ImportPresetAsync(json, token)).Succeeded;
                }
            });
            if (imported)
            {
                await AutoSaveWorkspaceAsync();
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { Workflow.ReportExternalFailure(TextResources.Get("Error_PresetImportRead")); }
    }

    private async Task ExportAsync()
    {
        if (Workflow.State.ActivePreset is not { } active) return;
        try
        {
            await RunAsync(token => App.UiServices.FilePicker.PickAndWriteTextAsync(App.WindowId, PresetDocumentService.GetExportFileName(active.Name), ".json", "Preset_FileType", Workflow.ExportActivePreset(), token));
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { Workflow.ReportExternalFailure(TextResources.Get("Error_PresetExportWrite")); }
    }

    public void PrepareEditFields(Action<string> setSourceText)
    {
        var selected = Workflow.State.SelectedPresetPackages.SingleOrDefault();
        if (selected is not null)
        {
            PackageId.Value = selected.Id;
            setSourceText(selected.Source ?? string.Empty);
        }
    }

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Presets;
        PresetNames.SynchronizeWith(state.PresetNames, name => name);
        Packages.SynchronizeWith(state.Packages.Select(row => row with
        {
            Publisher = TextResources.Get(row.Publisher),
            Name = string.IsNullOrWhiteSpace(row.Name) ? TextResources.Get("Value_Unknown") : row.Name,
            Version = string.IsNullOrWhiteSpace(row.Version) ? TextResources.Get("Value_Unknown") : row.Version
        }), PackageKey);
        OperationResults.ReplaceWith(state.OperationResults.Select(row => row with
        {
            Status = TextResources.Get(row.Status)
        }));
        Commands = state.Commands.ToDictionary(command => command.Id);
        ActivePresetName = state.ActivePresetName;
        HeaderState = state.HeaderState;
        IsExecuting = state.IsExecuting;
        PageState = !Workflow.State.Capabilities.CanUseWinget
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WingetUnavailableMessage)
            : state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.IsExecuting
                ? FeatureState.Ready
                : EmptyState(state);
        PresetName.Validate();
        PackageId.Validate();
        OnPropertyChanged(nameof(Commands));
        OnPropertyChanged(nameof(HasOperationResults));
    }

    private string? ValidatePresetName(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TextResources.Get("Validation_Required");
        return PresetNames.Any(name => string.Equals(name, value, StringComparison.OrdinalIgnoreCase))
            ? TextResources.Get("Validation_DuplicatePreset") : null;
    }

    private string? ValidatePackageId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return TextResources.Get("Validation_Required");
        return Packages.Any(package => string.Equals(package.PackageId, value, StringComparison.OrdinalIgnoreCase))
            ? TextResources.Get("Validation_DuplicatePackage") : null;
    }

    private static FeatureState EmptyState(PresetsPresentationState state) => state.PresetNames.Count == 0
        ? FeatureState.Empty(TextResources.Get("Empty_Presets"))
        : state.Packages.Count == 0
            ? FeatureState.Empty(TextResources.Get("Empty_Packages"))
            : FeatureState.Ready;

    private static string PackageKey(PresetPackageRow row) => $"{row.Source?.ToUpperInvariant() ?? string.Empty}|{row.PackageId.ToUpperInvariant()}";
}
