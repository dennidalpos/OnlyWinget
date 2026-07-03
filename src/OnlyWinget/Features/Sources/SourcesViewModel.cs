using OnlyWinget.Application.Presentation;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Sources;

public sealed class SourcesViewModel : FeatureViewModel
{
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

    public async Task AddAsync(CancellationToken cancellationToken)
    {
        Name.Validate();
        Argument.Validate();
        if (!CanAdd)
        {
            OnPropertyChanged(nameof(CanAdd));
            return;
        }

        await Workflow.AddSourceAsync(Name.Value.Trim(), Argument.Value.Trim(), cancellationToken);
        Name.Clear();
        Argument.Clear();
    }

    public async Task ExecuteAsync(UiCommandId command, CancellationToken cancellationToken)
    {
        switch (command)
        {
            case UiCommandId.RefreshSources: await Workflow.RefreshSourcesAsync(cancellationToken); break;
            case UiCommandId.UpdateSources: await Workflow.UpdateSourcesAsync(cancellationToken); break;
            case UiCommandId.AddSource: await AddAsync(cancellationToken); break;
            case UiCommandId.RemoveSource when SelectedSource is not null && await ConfirmAsync("Dialog_RemoveSource_Title", "Dialog_RemoveSource_Message"):
                await Workflow.RemoveSourceAsync(SelectedSource.Name, cancellationToken); break;
            case UiCommandId.ResetSources when await ConfirmAsync("Dialog_ResetSources_Title", "Dialog_ResetSources_Message"):
                await Workflow.ResetSourcesAsync(cancellationToken); break;
        }
    }

    public Task SetEnabledAsync(SourceRow source, bool enabled, CancellationToken cancellationToken) =>
        Workflow.SetSourceEnabledAsync(source.Name, enabled, cancellationToken);

    private static Task<bool> ConfirmAsync(string title, string message) => App.XamlRoot is { } root
        ? App.UiServices.Confirmation.ConfirmAsync(root, title, message)
        : Task.FromResult(false);

    protected override void Refresh()
    {
        IsRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Sources;
        Commands = state.Commands.ToDictionary(command => command.Id);
        if (SelectedSource is not null)
            SelectedSource = Sources.FirstOrDefault(source => string.Equals(source.Name, SelectedSource.Name, StringComparison.OrdinalIgnoreCase));
        Sources.ReplaceWith(state.Sources.Select(source => source with
        {
            Type = TextResources.Get(source.Type),
            Status = TextResources.Get($"Source_Status_{source.Status}")
        }));
        PageState = !Workflow.State.Capabilities.CanUseWinget
            ? FeatureState.Unavailable(Workflow.State.Capabilities.WingetUnavailableMessage)
            : state.Error is not null
            ? FeatureState.Error(state.Error)
            : state.Sources.Count == 0
                ? FeatureState.Empty(TextResources.Get("Empty_Sources"))
                : FeatureState.Ready;
        Name.Validate();
        IsRefreshing = false;
        OnPropertyChanged(nameof(Commands));
        OnPropertyChanged(nameof(CanAdd));
    }

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
