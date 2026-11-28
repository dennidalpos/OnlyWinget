using CommunityToolkit.Mvvm.ComponentModel;
using OnlyWinget.Application.Activity;
using OnlyWinget.Application.App;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Activity;

public sealed record ActivityDay(string Label, IReadOnlyList<ActivityRow> Entries);

public sealed partial class ActivityViewModel(Action<Action> dispatch, OnlyWingetApplication? workflow = null)
    : FeatureViewModel(dispatch, workflow)
{
    private IReadOnlyList<ActivityRow> allEntries = [];
    private string query = string.Empty;
    private string severity = "all";
    private string category = "all";
    private string timeFilter = string.Empty;
    private string severityFilter = string.Empty;
    private string titleFilter = string.Empty;
    private string messageFilter = string.Empty;

    [ObservableProperty]
    private FeatureState pageState = FeatureState.Ready;

    public ObservableCollection<ActivityDay> Days { get; } = [];
    public IReadOnlyList<UiCommand> Commands { get; private set; } = [];

    public void SetFilter(string search, string selectedSeverity, string selectedCategory)
    {
        query = search.Trim();
        severity = selectedSeverity;
        category = selectedCategory;
        ApplyFilter();
    }

    public void SetColumnFilters(string time, string severityValue, string title, string message)
    {
        timeFilter = time.Trim();
        severityFilter = severityValue.Trim();
        titleFilter = title.Trim();
        messageFilter = message.Trim();
        ApplyFilter();
    }

    public static string LocalizeSeverity(ActivitySeverity value) => TextResources.Get($"Activity_Severity_{value}");
    public static string CopyLabel => TextResources.Get("Command_CopyDetails");

    public static string LocalizeCategory(ActivityCategory value) => TextResources.Get($"Activity_Category_{value}");

    public static string Format(ActivityRow entry) =>
        $"{entry.Timestamp:O}\r\n{entry.Severity} · {ExportCategory(entry.Category)}\r\n{entry.Title}\r\n{entry.Message}";

    private static string ExportCategory(ActivityCategory value) =>
        value == ActivityCategory.WindowsUpdate ? "windows" : value.ToString().ToLowerInvariant();

    public string ExportText() => string.Join("\r\n\r\n", allEntries.Select(Format));

    public IReadOnlyList<ActivityEntry> ClearActivity()
    {
        var snapshot = Workflow.State.Activity.ToArray();
        Workflow.ClearActivity();
        return snapshot;
    }

    public void RestoreActivity(IEnumerable<ActivityEntry> entries)
    {
        Workflow.RestoreActivity(entries);
    }

    protected override void Refresh()
    {
        var state = PresentationStateMapper.ToActivityState(Workflow.State);
        allEntries = state.Entries;
        Commands = state.Commands;
        OnPropertyChanged(nameof(Commands));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var matchingEntries = allEntries.Where(entry =>
            (query.Length == 0 || entry.Title.Contains(query, StringComparison.CurrentCultureIgnoreCase) || entry.Message.Contains(query, StringComparison.CurrentCultureIgnoreCase)) &&
            (string.Equals(severity, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(entry.Severity.ToString(), severity, StringComparison.OrdinalIgnoreCase)) &&
            (string.Equals(category, "all", StringComparison.OrdinalIgnoreCase) || string.Equals(entry.Category.ToString(), category, StringComparison.OrdinalIgnoreCase)) &&
            Matches(entry.TimestampDisplay, timeFilter) &&
            Matches(LocalizeSeverity(entry.Severity), severityFilter) &&
            Matches(entry.Title, titleFilter) &&
            Matches(entry.Message, messageFilter));
        var days = matchingEntries
            .GroupBy(entry => entry.Timestamp.ToLocalTime().Date)
            .OrderByDescending(group => group.Key)
            .Select(group => new ActivityDay(group.Key.ToString("D", TextResources.OverrideCulture ?? global::System.Globalization.CultureInfo.CurrentCulture), group.ToArray()))
            .ToArray();
        Days.ReplaceWith(days);
        PageState = days.Length != 0
            ? FeatureState.Ready
            : FeatureState.Empty(TextResources.Get(allEntries.Count == 0 ? "Empty_Activity" : "Empty_ActivityFiltered"));
    }

    private static bool Matches(string value, string filter) =>
        filter.Length == 0 || value.Contains(filter, StringComparison.CurrentCultureIgnoreCase);
}
