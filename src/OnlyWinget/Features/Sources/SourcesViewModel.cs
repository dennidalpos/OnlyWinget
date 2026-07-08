using OnlyWinget.Application.Presentation;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Sources;

public sealed class SourcesViewModel : FeatureViewModel
{
    private CancellationTokenSource? cancellation;
    private IReadOnlyList<SourceRow> allSources = [];
    private string nameFilter = string.Empty;
    private string argumentFilter = string.Empty;
    private string typeFilter = string.Empty;
    private string statusFilter = string.Empty;
    private bool isRefreshing;
    private FeatureState pageState = FeatureState.Ready;
    private SourceRow? selectedSource;

    public SourcesViewModel(Action<Action> dispatch) : base(App.Workflow, dispatch)
    {
        Name = new(ValidateName);
    }

    public ObservableCollection<SourceRow> Sources { get; } = [];
    public ValidatedField Name { get; }
    public ValidatedField Argument { get; } = new(ValidateArgument);
    public IReadOnlyDictionary<UiCommandId, UiCommand> Commands { get; private set; } = new Dictionary<UiCommandId, UiCommand>();
    public bool IsRefreshing { get => isRefreshing; private set => SetProperty(ref isRefreshing, value); }
    public FeatureState PageState { get => pageState; private set => SetProperty(ref pageState, value); }
    public SourceRow? SelectedSource { get => selectedSource; set { if (SetProperty(ref selectedSource, value)) OnPropertyChanged(nameof(Commands)); } }
    public bool CanAdd => Name.IsValid && Argument.IsValid && Name.Value.Trim().Length > 0 && Argument.Value.Trim().Length > 0 && IsEnabled(UiCommandId.AddSource);

    public bool IsEnabled(UiCommandId id) => Commands.TryGetValue(id, out var command) && command.IsEnabled;
    public void Cancel() => cancellation?.Cancel();

    private async Task RunAsync(Func<CancellationToken, Task> action)
    {
        if (cancellation is not null) return;
        using var current = new CancellationTokenSource();
        cancellation = current;
        try { await action(current.Token); }
        finally { if (ReferenceEquals(cancellation, current)) cancellation = null; }
    }

    public async Task AddAsync()
    {
        Name.Validate();
        Argument.Validate();
        if (!CanAdd)
        {
            OnPropertyChanged(nameof(CanAdd));
            return;
        }

        await RunAsync(token => Workflow.AddSourceAsync(Name.Value.Trim(), Argument.Value.Trim(), token));
        Name.Clear();
        Argument.Clear();
    }

    public async Task ExecuteAsync(UiCommandId command)
    {
        switch (command)
        {
            case UiCommandId.RefreshSources: await RunAsync(token => Workflow.RefreshSourcesAsync(token)); break;
            case UiCommandId.UpdateSources: await RunAsync(token => Workflow.UpdateSourcesAsync(token)); break;
            case UiCommandId.AddSource: await AddAsync(); break;
            case UiCommandId.RemoveSource when SelectedSource is not null && await ConfirmAsync("Dialog_RemoveSource_Title", "Dialog_RemoveSource_Message"):
                await RunAsync(token => Workflow.RemoveSourceAsync(SelectedSource.Name, token)); break;
            case UiCommandId.ResetSources when await ConfirmAsync("Dialog_ResetSources_Title", "Dialog_ResetSources_Message"):
                await RunAsync(token => Workflow.ResetSourcesAsync(token)); break;
        }
    }

    public Task SetEnabledAsync(SourceRow source, bool enabled) =>
        RunAsync(token => Workflow.SetSourceEnabledAsync(source.Name, enabled, token));

    public void SetListFilters(string name, string argument, string type, string status)
    {
        nameFilter = name.Trim();
        argumentFilter = argument.Trim();
        typeFilter = type.Trim();
        statusFilter = status.Trim();
        ApplyFilters();
    }

    private static Task<bool> ConfirmAsync(string title, string message) => App.XamlRoot is { } root
        ? App.UiServices.Confirmation.ConfirmAsync(root, title, message)
        : Task.FromResult(false);

    protected override void Refresh()
    {
        IsRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Sources;
        Commands = state.Commands.ToDictionary(command => command.Id);
        allSources = state.Sources.Select(source => new SourceRow(
            source.Name,
            source.Argument,
            source.IsExplicit,
            TextResources.Get(source.Type),
            TextResources.Get($"Source_Status_{source.Status}"),
            source.IsEnabled
        )).ToArray();
        ApplyFilters();
        if (SelectedSource is not null)
            SelectedSource = Sources.FirstOrDefault(source => string.Equals(source.Name, SelectedSource.Name, StringComparison.OrdinalIgnoreCase));
        PageState = !Workflow.State.Capabilities.CanUseWinget
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WingetUnavailableMessage)
            : state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.Sources.Count == 0
                ? FeatureState.Empty(TextResources.Get("Empty_Sources"))
                : FeatureState.Ready;
        Name.Validate();
        Argument.Validate();
        IsRefreshing = false;
        OnPropertyChanged(nameof(Commands));
        OnPropertyChanged(nameof(CanAdd));
    }

    private void ApplyFilters()
    {
        Sources.SynchronizeWith(allSources.Where(source =>
            Matches(source.Name, nameFilter) &&
            Matches(source.Argument, argumentFilter) &&
            Matches(source.Type, typeFilter) &&
            Matches(source.Status, statusFilter)),
            source => source.Name.ToUpperInvariant(),
            (existing, updated) =>
            {
                existing.Argument = updated.Argument;
                existing.IsExplicit = updated.IsExplicit;
                existing.Type = updated.Type;
                existing.Status = updated.Status;
                existing.IsEnabled = updated.IsEnabled;
            });
    }

    private static bool Matches(string value, string filter) =>
        filter.Length == 0 || value.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    private static string? ValidateArgument(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TextResources.Get("Validation_Required");
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https"
            ? null
            : TextResources.Get("Validation_SourceArgument");
    }

    private string? ValidateName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TextResources.Get("Validation_Required");
        }

        return Sources.Any(source => string.Equals(source.Name, value, StringComparison.OrdinalIgnoreCase))
            ? TextResources.Get("Validation_DuplicateSource")
            : null;
    }
}
