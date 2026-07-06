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
    private string category = "all";
    private FeatureState pageState = FeatureState.Ready;

    public ObservableCollection<ActivityRow> Entries { get; } = [];
    public IReadOnlyList<UiCommand> Commands { get; private set; } = [];
    public FeatureState PageState { get => pageState; private set => SetProperty(ref pageState, value); }

    public void SetFilter(string search, string selectedSeverity, string selectedCategory)
    {
        query = search.Trim();
        severity = selectedSeverity;
        category = selectedCategory;
        ApplyFilter();
    }

    public static string LocalizeSeverity(ActivitySeverity value) => TextResources.Get($"Activity_Severity_{value}");
    public static string CopyLabel => TextResources.Get("Command_CopyDetails");

    public static string Category(ActivityRow entry)
    {
        var value = $"{entry.Title} {entry.Message}";
        if (value.Contains("Windows Update", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("restart", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("riavvio", StringComparison.OrdinalIgnoreCase)) return "windows";
        if (value.Contains("preset", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("workspace", StringComparison.OrdinalIgnoreCase)) return "presets";
        if (value.Contains("package", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("winget", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("update", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("pacchetto", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("aggiornamento", StringComparison.OrdinalIgnoreCase)) return "packages";
        if (value.Contains("source", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("origine", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("origini", StringComparison.OrdinalIgnoreCase)) return "sources";
        return "system";
    }

    public static string Format(ActivityRow entry) =>
        $"{entry.Timestamp:O}\r\n{entry.Severity} · {Category(entry)}\r\n{entry.Title}\r\n{entry.Message}";

    public string ExportText() => string.Join("\r\n\r\n", allEntries.Select(Format));

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Activity;
        allEntries = state.Entries;
        Commands = state.Commands;
        OnPropertyChanged(nameof(Commands));
        PageState = state.Entries.Count == 0
            ? FeatureState.Empty(TextResources.Get("Empty_Activity"))
            : FeatureState.Ready;
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        Entries.ReplaceWith(allEntries.Where(entry =>
            (query.Length == 0 || entry.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || entry.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase)) &&
            (severity == "all" || string.Equals(entry.Severity.ToString(), severity, StringComparison.Ordinal)) &&
            (category == "all" || Category(entry) == category)));
    }
}
