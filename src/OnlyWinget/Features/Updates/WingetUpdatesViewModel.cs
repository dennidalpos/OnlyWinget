using CommunityToolkit.Mvvm.ComponentModel;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.Winget;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class WingetUpdatesViewModel(Action<Action> dispatch) : FeatureViewModel(App.Workflow, dispatch)
{
    private CancellationTokenSource? cancellation;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private bool isExecuting;

    [ObservableProperty]
    private FeatureState pageState = FeatureState.Ready;

    [ObservableProperty]
    private SelectionHeaderState headerState;

    [ObservableProperty]
    private OperationProgress? progress;

    public ObservableCollection<UpdateRow> Updates { get; } = [];
    public ObservableCollection<OperationResultRow> OperationResults { get; } = [];
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();
    public bool IsBusy => IsLoading || IsExecuting;
    public bool HasOperationResults => OperationResults.Count > 0;
    public string ProgressText => OperationProgressFormatter.FormatProgressText(Progress, TextResources.Get);
    public bool ShouldInitialRefresh => Workflow.State.Updates.Count == 0 && Workflow.State.BusyState == ApplicationBusyState.Idle;
    public string? Error => Workflow.State.UserVisibleError;
    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public void ToggleAll() => Workflow.ToggleAllUpdates();
    public void Toggle(UpdateRow row) => Workflow.ToggleUpdate(new PackageIdentity(row.PackageId, row.Source));
    public void SetSelected(IEnumerable<UpdateRow> rows, bool isSelected) =>
        Workflow.SetUpdatesSelection(rows.Select(row => new PackageIdentity(row.PackageId, row.Source)), isSelected);
    public void Cancel() => cancellation?.Cancel();

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (cancellation is not null) return;
        using var current = new CancellationTokenSource();
        cancellation = current;
        try { await action(current.Token); }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; }
    }

    public Task RefreshAsync() => RunAsync(token => Workflow.RefreshUpdatesAsync(token));
    public Task ApplyAsync() => RunAsync(async token =>
    {
        await Workflow.ApplySelectedUpdatesAsync(token);
        if (!token.IsCancellationRequested)
        {
            await Workflow.RefreshUpdatesAsync(token);
        }
    });
    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Updates;
        Updates.SynchronizeWith(state.Updates.Select(row => row with
        {
            Publisher = TextResources.Get(row.Publisher),
            Status = TextResources.Get(row.Status ?? "Update_Status_Available")
        }), PackageKey);
        OperationResults.ReplaceWith(state.OperationResults.Select(row => row with
        {
            Status = TextResources.Get(row.Status)
        }));
        Commands = state.Commands.ToDictionary(command => command.Id);
        IsLoading = state.IsLoading;
        IsExecuting = state.IsExecuting;
        HeaderState = state.HeaderState;
        Progress = Workflow.State.OperationProgress;
        PageState = !Workflow.State.Capabilities.CanUseWinget
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WingetUnavailableMessage)
            : state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.Updates.Count == 0 && !state.IsLoading && !state.IsExecuting
                ? FeatureState.Empty(TextResources.Get("Empty_Updates"))
                : FeatureState.Ready;
        OnPropertyChanged(nameof(Commands));
        OnPropertyChanged(nameof(ProgressText));
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(HasOperationResults));
    }

    private static string PackageKey(UpdateRow row) => $"{row.Source?.ToUpperInvariant() ?? string.Empty}|{row.PackageId.ToUpperInvariant()}";
}
