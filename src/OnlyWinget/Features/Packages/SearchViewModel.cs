using OnlyWinget.Application.Presentation;
using OnlyWinget.Domain.Packages;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Packages;

public sealed class SearchViewModel(Action<Action> dispatch) : FeatureViewModel(App.Workflow, dispatch)
{
    private bool isLoading;
    private FeatureState pageState = FeatureState.Ready;
    private SelectionHeaderState headerState;

    public ObservableCollection<SearchResultRow> Results { get; } = [];
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();
    public bool IsLoading { get => isLoading; private set => SetProperty(ref isLoading, value); }
    public FeatureState PageState { get => pageState; private set => SetProperty(ref pageState, value); }
    public SelectionHeaderState HeaderState { get => headerState; private set => SetProperty(ref headerState, value); }

    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public Task SearchAsync(string query, CancellationToken token) => Workflow.SearchAsync(query, token);
    public Task AddSelectedAsync(CancellationToken token) => Workflow.AddSelectedSearchResultsToActivePresetAsync(token);
    public void ToggleAll() => Workflow.ToggleAllSearchResults();
    public void Toggle(SearchResultRow row) => Workflow.ToggleSearchResult(new PackageIdentity(row.PackageId, row.Source));

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Search;
        Results.SynchronizeWith(state.Results.Select(row => row with
        {
            Architecture = TextResources.Get(row.Architecture),
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
