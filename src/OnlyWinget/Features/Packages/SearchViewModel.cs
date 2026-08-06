using CommunityToolkit.Mvvm.ComponentModel;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Packages;

public sealed partial class SearchViewModel(Action<Action> dispatch) : FeatureViewModel(App.Workflow, dispatch)
{
    private CancellationTokenSource? cancellation;

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private FeatureState pageState = FeatureState.Ready;

    [ObservableProperty]
    private SelectionHeaderState headerState;

    public ObservableCollection<SearchResultRow> Results { get; } = [];
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();

    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public void ToggleAll() => Workflow.ToggleAllSearchResults();
    public void Toggle(SearchResultRow row) => Workflow.ToggleSearchResult(new PackageIdentity(row.PackageId, row.Source));
    public void SetSelected(IEnumerable<SearchResultRow> rows, bool isSelected) =>
        Workflow.SetSearchResultsSelection(rows.Select(row => new PackageIdentity(row.PackageId, row.Source)), isSelected);
    public void Cancel() => cancellation?.Cancel();

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (cancellation is not null) return;
        using var current = new CancellationTokenSource();
        cancellation = current;
        try { await action(current.Token); }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; }
    }

    public Task SearchAsync(string query) => RunAsync(token => Workflow.SearchAsync(query, token));
    public async Task AddSelectedAsync()
    {
        var added = false;
        await RunAsync(async token =>
        {
            added = (await Workflow.AddSelectedSearchResultsToActivePresetAsync(token)).Succeeded;
        });
        if (added)
        {
            await RunAsync(token => Workflow.SaveWorkspaceAsync(token));
        }
    }

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Search;
        Results.SynchronizeWith(state.Results.Select(row => row with
        {
            Publisher = TextResources.Get(row.Publisher),
            Version = string.IsNullOrWhiteSpace(row.Version) ? TextResources.Get("Value_Unknown") : row.Version,
            Match = string.IsNullOrWhiteSpace(row.Match) ? TextResources.Get("Value_Unknown") : row.Match
        }), PackageKey);
        Commands = state.Commands.ToDictionary(command => command.Id);
        IsLoading = state.IsLoading;
        HeaderState = state.HeaderState;
        PageState = !Workflow.State.Capabilities.CanUseWinget
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WingetUnavailableMessage)
            : state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.IsLoading
                ? FeatureState.Loading(TextResources.Get("Progress_Searching"))
                : state.Results.Count == 0
                    ? FeatureState.Empty(TextResources.Get("Empty_Search"))
                    : FeatureState.Ready;
        OnPropertyChanged(nameof(Commands));
    }

    private static string PackageKey(SearchResultRow row) => $"{row.Source?.ToUpperInvariant() ?? string.Empty}|{row.PackageId.ToUpperInvariant()}";
}
