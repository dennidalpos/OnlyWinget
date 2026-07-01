using OnlyWinget.Application.Presentation;
using OnlyWinget.Application.WindowsUpdate;
using OnlyWinget.Domain.Selection;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Updates;

public sealed class WindowsUpdatesViewModel(Action<Action> dispatch) : FeatureViewModel(App.Workflow, dispatch)
{
    private CancellationTokenSource? cancellation;
    private bool isScanning;
    private bool isInstalling;
    private string status = string.Empty;
    private SelectionHeaderState headerState;

    public ObservableCollection<WindowsUpdateRow> Updates { get; } = [];
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();
    public bool IsScanning { get => isScanning; private set => SetProperty(ref isScanning, value); }
    public bool IsInstalling { get => isInstalling; private set => SetProperty(ref isInstalling, value); }
    public bool IsBusy => IsScanning || IsInstalling;
    public string Status { get => status; private set => SetProperty(ref status, value); }
    public SelectionHeaderState HeaderState { get => headerState; private set => SetProperty(ref headerState, value); }

    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public void ToggleAll() => Workflow.ToggleAllWindowsUpdates();
    public void Toggle(WindowsUpdateRow row) => Workflow.ToggleWindowsUpdate(new WindowsUpdateIdentity(row.UpdateId, row.RevisionNumber));
    public void Cancel() => cancellation?.Cancel();

    public Task ScanAsync(WindowsUpdateOptions options) => RunAsync(token => Workflow.ScanWindowsUpdatesAsync(options, token));
    public Task InstallAsync(WindowsUpdateOptions options) => RunAsync(token => Workflow.InstallSelectedWindowsUpdatesAsync(options, token));

    protected override void Refresh()
    {
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).WindowsUpdates;
        Updates.SynchronizeWith(state.Updates.Select(row => row with
        {
            Severity = Empty(row.Severity),
            Categories = Empty(row.Categories),
            KnowledgeBaseArticles = Empty(row.KnowledgeBaseArticles),
            Status = Empty(row.Status)
        }), Key);
        Commands = state.Commands.ToDictionary(command => command.Id);
        IsScanning = state.IsScanning;
        IsInstalling = state.IsInstalling;
        HeaderState = state.HeaderState;
        Status = state.Error ?? (state.IsScanning || state.Updates.Count > 0 ? string.Empty : TextResources.Get("Empty_WindowsUpdates"));
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

    private static string Empty(string? value) => string.IsNullOrWhiteSpace(value) ? TextResources.Get("Value_Unknown") : value;
    private static string Key(WindowsUpdateRow row) => $"{row.UpdateId.ToUpperInvariant()}|{row.RevisionNumber}";
}
