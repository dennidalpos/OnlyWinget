namespace OnlyWinget.Application.Storage;

public sealed record SourcePreferences(IReadOnlyList<string> DisabledSources)
{
    public static SourcePreferences Empty { get; } = new([]);
}
