using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Packages;

public sealed class PresetsViewModel : FeatureViewModel
{
    private bool isExecuting;
    private FeatureState pageState = FeatureState.Ready;
    private string? activePresetName;
    private SelectionHeaderState headerState;

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
