using OnlyWinget.Application.Presentation;
using OnlyWinget.Presentation;
using System.Collections.ObjectModel;

namespace OnlyWinget.Features.Sources;

public sealed class SourcesViewModel : FeatureViewModel
{
    private bool isRefreshing;
    private FeatureState pageState = FeatureState.Ready;

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
    }

    protected override void Refresh()
    {
        IsRefreshing = true;
        var state = PresentationStateMapper.FromApplicationState(Workflow.State).Sources;
        Commands = state.Commands.ToDictionary(command => command.Id);
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
