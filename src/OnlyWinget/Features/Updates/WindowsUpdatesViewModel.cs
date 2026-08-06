using CommunityToolkit.Mvvm.ComponentModel;
using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Updates;

public sealed partial class WindowsUpdatesViewModel(Action<Action> dispatch) : FeatureViewModel(App.Workflow, dispatch)
{
    private CancellationTokenSource? cancellation;

    [ObservableProperty]
    private bool isScanning;

    [ObservableProperty]
    private bool isInstalling;

    [ObservableProperty]
    private FeatureState pageState = FeatureState.Ready;

    [ObservableProperty]
    private SelectionHeaderState headerState;

    public ObservableCollection<WindowsUpdateDisplayRow> Updates { get; } = [];
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();
    public bool IsBusy => IsScanning || IsInstalling;
    public string? Error => Workflow.State.UserVisibleError;
    public bool RebootRequired => Workflow.State.LastWindowsUpdateResults.Any(result => result.RebootRequired);

    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public void ToggleAll() => Workflow.ToggleAllWindowsUpdates();
    public void Toggle(WindowsUpdateDisplayRow row) => Workflow.ToggleWindowsUpdate(new WindowsUpdateIdentity(row.UpdateId, row.RevisionNumber));
    public void SetSelected(IEnumerable<WindowsUpdateDisplayRow> rows, bool isSelected) =>
        Workflow.SetWindowsUpdatesSelection(rows.Select(row => new WindowsUpdateIdentity(row.UpdateId, row.RevisionNumber)), isSelected);
    public void Cancel() => cancellation?.Cancel();

    public Task ScanAsync(WindowsUpdateOptions options) => RunAsync(token => Workflow.ScanWindowsUpdatesAsync(options, token));
    public Task InstallAsync(WindowsUpdateOptions options) => RunAsync(token => Workflow.InstallSelectedWindowsUpdatesAsync(options, token));

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).WindowsUpdates;
        Updates.SynchronizeWith(state.Updates.Select(ToDisplayRow), Key);
        Commands = state.Commands.ToDictionary(command => command.Id);
        IsScanning = state.IsScanning;
        IsInstalling = state.IsInstalling;
        HeaderState = state.HeaderState;
        PageState = !Workflow.State.Capabilities.CanUseWindowsUpdate
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WindowsUpdateUnavailableMessage)
            : state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.Updates.Count == 0 && !state.IsScanning && !state.IsInstalling
                ? FeatureState.Empty(TextResources.Get("Empty_WindowsUpdates"))
                : FeatureState.Ready;
        OnPropertyChanged(nameof(IsBusy));
        OnPropertyChanged(nameof(Commands));
    }

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (cancellation is not null) return;
        using var current = new CancellationTokenSource();
        cancellation = current;
        try { await action(current.Token); }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; }
    }

    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? "-" : value;
    private static WindowsUpdateDisplayRow ToDisplayRow(WindowsUpdateRow row)
    {
        string statusText = "-";
        if (!string.IsNullOrWhiteSpace(row.Status))
        {
            if (row.Status.StartsWith("Operation_Status_"))
            {
                var spaceIndex = row.Status.IndexOf(' ');
                if (spaceIndex > 0)
                {
                    var key = row.Status.Substring(0, spaceIndex);
                    var suffix = row.Status.Substring(spaceIndex);
                    statusText = TextResources.Get(key) + suffix;
                }
                else
                {
                    statusText = TextResources.Get(row.Status);
                }
            }
            else
            {
                statusText = TextResources.Get(row.Status);
            }
        }
        return new(
            row.UpdateId,
            row.RevisionNumber,
            row.RevisionNumber.ToString(System.Globalization.CultureInfo.CurrentCulture),
            row.Title,
            Empty(row.KnowledgeBaseArticles),
            Empty(row.Severity),
            Empty(row.Categories),
            FormatSize(row.MaxDownloadSize),
            FormatBoolean(row.IsDownloaded),
            FormatBoolean(row.RebootRequired),
            row.IsSelected,
            statusText);
    }

    private static string FormatBoolean(bool value) => TextResources.Get(value ? "Value_Yes" : "Value_No");

    private static string FormatSize(ulong bytes)
    {
        if (bytes == 0) return "-";
        const double megabyte = 1024d * 1024d;
        const double gigabyte = megabyte * 1024d;
        return bytes >= gigabyte
            ? $"{bytes / gigabyte:0.##} GB"
            : $"{bytes / megabyte:0.##} MB";
    }

    private static string Key(WindowsUpdateDisplayRow row) => $"{row.UpdateId.ToUpperInvariant()}|{row.RevisionNumber}";
}
