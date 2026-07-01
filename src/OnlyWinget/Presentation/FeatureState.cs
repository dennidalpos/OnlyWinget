namespace OnlyWinget.Presentation;

public enum FeatureStateKind
{
    Ready,
    Empty,
    Loading,
    Executing,
    Error,
    Unavailable
}

public sealed record FeatureState(
    FeatureStateKind Kind,
    string Message,
    string? Details = null,
    string? ActionResourceKey = null)
{
    public static FeatureState Ready { get; } = new(FeatureStateKind.Ready, string.Empty);

    public static FeatureState Empty(string message, string? actionResourceKey = null) =>
        new(FeatureStateKind.Empty, message, ActionResourceKey: actionResourceKey);

    public static FeatureState Loading(string message) => new(FeatureStateKind.Loading, message);
    public static FeatureState Executing(string message) => new(FeatureStateKind.Executing, message);
    public static FeatureState Error(string message, string? details = null) => new(FeatureStateKind.Error, message, details);
    public static FeatureState Unavailable(string message, string? details = null) => new(FeatureStateKind.Unavailable, message, details);
}
