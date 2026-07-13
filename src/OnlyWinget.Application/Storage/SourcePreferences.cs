namespace OnlyWinget.Application.Storage;

public sealed record SourcePreferences(IReadOnlyList<string> DisabledSources, bool DefaultSourcesConfigured = false)
{
    public static SourcePreferences Empty { get; } = new([], false);
}
