namespace OnlyWinget.Application.Winget;

public sealed record PackageSearchRequest(string Query, string? Source = null)
{
    public string Query { get; } = string.IsNullOrWhiteSpace(Query)
        ? throw new ArgumentException("Search query is required.", nameof(Query))
        : Query.Trim();

    public string? Source { get; } = string.IsNullOrWhiteSpace(Source) ? null : Source.Trim();
}
