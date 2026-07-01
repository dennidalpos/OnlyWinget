using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Application.Presets;
using OnlyWinget.Application.Winget;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Packages;

public sealed class PresetsViewModel : FeatureViewModel
{
    private bool isExecuting;
    private FeatureState pageState = FeatureState.Ready;
    private string? activePresetName;
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
    public string? ActivePresetName { get => activePresetName; private set => SetProperty(ref activePresetName, value); }
    public bool IsExecuting { get => isExecuting; private set => SetProperty(ref isExecuting, value); }
    public FeatureState PageState { get => pageState; private set => SetProperty(ref pageState, value); }
    public SelectionHeaderState HeaderState { get => headerState; private set => SetProperty(ref headerState, value); }
    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public OperationProgress? Progress => Workflow.State.OperationProgress;
    public string? Error => Workflow.State.UserVisibleError;

    public void SetActivePreset(string name) => Workflow.SetActivePreset(name);
    public void ToggleAll() => Workflow.ToggleAllPresetPackages();
    public void Toggle(PresetPackageRow row) => Workflow.TogglePresetPackage(new PackageIdentity(row.PackageId, row.Source));
    public void Cancel() => cancellation?.Cancel();

    public async Task ExecuteAsync(UiCommand command, string source)
    {
        if (command.ConfirmationResourceKey is { } confirmation && !await ConfirmAsync(command.LabelResourceKey, confirmation)) return;
        switch (command.Id)
        {
            case UiCommandId.AddPreset: if (Validate(PresetName)) Workflow.AddPreset(PresetName.Value.Trim()); break;
            case UiCommandId.RenamePreset: if (Validate(PresetName)) Workflow.RenameActivePreset(PresetName.Value.Trim()); break;
            case UiCommandId.RemovePreset: Workflow.RemoveActivePreset(); break;
            case UiCommandId.AddPresetPackage: if (Validate(PackageId)) await Workflow.AddPackageToActivePresetAsync(Package(source), CancellationToken.None); break;
            case UiCommandId.EditPresetPackage when Workflow.State.SelectedPresetPackages.SingleOrDefault() is { } selected:
                if (Validate(PackageId)) await Workflow.ReplacePackageInActivePresetAsync(selected, Package(source), CancellationToken.None); break;
            case UiCommandId.RemovePresetPackages: Workflow.RemoveSelectedPackagesFromActivePreset(); break;
            case UiCommandId.ImportPreset: await ImportAsync(); break;
            case UiCommandId.ExportPreset: await ExportAsync(); break;
            case UiCommandId.SaveWorkspace: await Workflow.SaveWorkspaceAsync(CancellationToken.None); break;
            case UiCommandId.InstallPreset: await ApplyAsync(PackageAction.Install); break;
            case UiCommandId.UninstallPreset: await ApplyAsync(PackageAction.Uninstall); break;
            case UiCommandId.CancelOperation: Cancel(); break;
        }
    }

    private PackageIdentity Package(string source) => new(PackageId.Value.Trim(), source.Trim());
    private static bool Validate(ValidatedField field) { field.Validate(); return field.IsValid; }
    private static Task<bool> ConfirmAsync(string title, string message) => App.XamlRoot is { } root
        ? App.UiServices.Confirmation.ConfirmAsync(root, title, message) : Task.FromResult(false);

    private async Task ApplyAsync(PackageAction action)
    {
        if (cancellation is not null) return;
        using var current = new CancellationTokenSource();
        cancellation = current;
        try { await Workflow.ApplyActivePresetAsync(action, current.Token); }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; }
    }

    private async Task ImportAsync()
    {
        try
        {
            var json = await App.UiServices.FilePicker.PickAndReadTextAsync(App.WindowId, ".json", CancellationToken.None);
            if (json is not null) await Workflow.ImportPresetAsync(json, CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { Workflow.ReportExternalFailure(TextResources.Get("Error_PresetImportRead")); }
    }

    private async Task ExportAsync()
    {
        if (Workflow.State.ActivePreset is not { } active) return;
        try
        {
            await App.UiServices.FilePicker.PickAndWriteTextAsync(App.WindowId, PresetDocumentService.GetExportFileName(active.Name), ".json", "Preset_FileType", Workflow.ExportActivePreset(), CancellationToken.None);
        }
        catch (Exception exception) when (exception is not OperationCanceledException) { Workflow.ReportExternalFailure(TextResources.Get("Error_PresetExportWrite")); }
    }

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Presets;
        PresetNames.SynchronizeWith(state.PresetNames, name => name);
        Packages.SynchronizeWith(state.Packages.Select(row => row with
        {
            Architecture = TextResources.Get(row.Architecture),
            Name = string.IsNullOrWhiteSpace(row.Name) ? TextResources.Get("Value_Unknown") : row.Name,
            Version = string.IsNullOrWhiteSpace(row.Version) ? TextResources.Get("Value_Unknown") : row.Version
        }), PackageKey);
        OperationResults.ReplaceWith(state.OperationResults);
        Commands = state.Commands.ToDictionary(command => command.Id);
        ActivePresetName = state.ActivePresetName;
        HeaderState = state.HeaderState;
        IsExecuting = state.IsExecuting;
        PageState = !Workflow.State.Capabilities.CanUseWinget
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WingetUnavailableMessage)
            : state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.IsExecuting
                ? FeatureState.Executing(TextResources.Get("Progress_Starting"))
                : EmptyState(state);
        PresetName.Validate();
        PackageId.Validate();
        OnPropertyChanged(nameof(Commands));
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
