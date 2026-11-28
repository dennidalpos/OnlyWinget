using CommunityToolkit.Mvvm.ComponentModel;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;
using System.Globalization;

namespace OnlyWinget.Features.Updates;

public sealed record WindowsUpdateHistoryDisplayRow(
    string UpdateId,
    string Date,
    string Title,
    string KnowledgeBase,
    string Classification,
    string Result,
    string Error,
    bool IsSelected,
    bool CanSelect = false);

public sealed partial class WindowsUpdateHistoryViewModel(Action<Action> dispatch, OnlyWingetApplication? workflow = null)
    : FeatureViewModel(dispatch, workflow)
{
    private CancellationTokenSource? cancellation;
    private IReadOnlyList<WindowsUpdateHistoryRow> allRows = [];

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private FeatureState pageState = FeatureState.Ready;

    [ObservableProperty]
    private SelectionHeaderState headerState;

    [ObservableProperty]
    private string summary = string.Empty;

    /// <summary>Null shows every type; otherwise only that Windows Settings group.</summary>
    public WindowsUpdateClassification? GroupFilter { get; private set; }

    public ObservableCollection<WindowsUpdateHistoryDisplayRow> Rows { get; } = [];
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();
    public bool HasLoaded => Workflow.State.WindowsUpdateHistory is { Count: > 0 };

    public void SetGroupFilter(WindowsUpdateClassification? group)
    {
        GroupFilter = group;
        Refresh();
    }

    public void ToggleAll() => Workflow.ToggleAllHistoryUpdates();

    public void SetSelected(IEnumerable<WindowsUpdateHistoryDisplayRow> rows, bool isSelected) =>
        Workflow.SetHistoryUpdatesSelection(
            rows.Where(row => row.UpdateId.Length > 0).Select(row => new WindowsUpdateIdentity(row.UpdateId, 0)), isSelected);

    public void Cancel()
    {
        try { cancellation?.Cancel(); } catch (ObjectDisposedException) { }
    }

    public Task LoadAsync() => RunAsync(token => Workflow.LoadWindowsUpdateHistoryAsync(token));

    public Task UninstallAsync() => RunAsync(async token =>
    {
        await Workflow.UninstallHistoryUpdatesAsync(token);
        Workflow.RefreshRestartPending();
        if (!token.IsCancellationRequested)
        {
            await Workflow.LoadWindowsUpdateHistoryAsync(token);
        }
    });

    public Task RestartAsync() => RunAsync(token => Workflow.ScheduleRestartAsync(TimeSpan.FromSeconds(30), token));

    protected override void Refresh()
    {
        var state = PresentationStateMapper.ToWindowsUpdateHistoryState(Workflow.State);
        allRows = state.Rows;
        var visible = state.Rows
            .Where(row => GroupFilter is null || row.Classification == GroupFilter)
            .Select(ToDisplayRow)
            .ToArray();
        Rows.SynchronizeWith(visible, row => $"{row.UpdateId}|{row.Date}|{row.Title}");
        Commands = state.Commands.ToDictionary(command => command.Id);
        HeaderState = state.HeaderState;
        IsBusy = state.IsLoading || state.IsManaging;
        Summary = string.Format(CultureInfo.CurrentCulture, TextResources.Get("History_Summary"), visible.Length);
        PageState = !Workflow.State.Capabilities.CanUseWindowsUpdate
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WindowsUpdateUnavailableMessage)
            : state.Error is not null
                ? FeatureState.Error(state.Error)
                : state.Rows.Count == 0 && !IsBusy
                    ? FeatureState.Empty(TextResources.Get("Empty_WindowsUpdateHistory"))
                    : FeatureState.Ready;
        OnPropertyChanged(nameof(Commands));
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (cancellation is not null) return;
        var current = new CancellationTokenSource();
        cancellation = current;
        try { await action(current.Token); }
        finally
        {
            if (ReferenceEquals(cancellation, current)) cancellation = null;
            current.Dispose();
        }
    }

    private static WindowsUpdateHistoryDisplayRow ToDisplayRow(WindowsUpdateHistoryRow row)
    {
        var result = row.Operation == WindowsUpdateHistoryOperation.Uninstall && row.Result == WindowsUpdateHistoryResult.Succeeded
            ? TextResources.Get("History_Operation_Uninstall")
            : TextResources.Get($"History_Result_{row.Result}");
        return new WindowsUpdateHistoryDisplayRow(
            row.UpdateId,
            row.Date.ToLocalTime().ToString("g", CultureInfo.CurrentCulture),
            row.Title,
            string.IsNullOrEmpty(row.KnowledgeBase) ? "-" : row.KnowledgeBase,
            TextResources.Get(row.ClassificationKey),
            result,
            row.ErrorDetail ?? "-",
            row.IsSelected,
            row.CanSelect);
    }
}
