using OnlyWinget.Application.Activity;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Activity;

public sealed class ActivityViewModel(Action<Action> dispatch) : FeatureViewModel(App.Workflow, dispatch)
{
    private IReadOnlyList<ActivityRow> allEntries = [];
    private string query = string.Empty;
    private string severity = "all";

    public ObservableCollection<ActivityRow> Entries { get; } = [];
    public IReadOnlyList<UiCommand> Commands { get; private set; } = [];
    public bool IsEmpty => allEntries.Count == 0;

    public void SetFilter(string search, string selectedSeverity)
    {
        query = search.Trim();
        severity = selectedSeverity;
        ApplyFilter();
    }

    public static string LocalizeSeverity(ActivitySeverity value) => TextResources.Get($"Activity_Severity_{value}");

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Activity;
        allEntries = state.Entries;
        Commands = state.Commands;
        OnPropertyChanged(nameof(Commands));
        OnPropertyChanged(nameof(IsEmpty));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Entries.ReplaceWith(allEntries.Where(entry =>
            (query.Length == 0 || entry.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || entry.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase)) &&
            (severity == "all" || string.Equals(entry.Severity.ToString(), severity, StringComparison.Ordinal))));
    }
}
