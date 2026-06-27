namespace OnlyWinget.Application.Presentation;

public sealed record SearchResultRow(
    string PackageId,
    string Name,
    string? Source,
    string? Version,
    string Architecture,
    string? Match,
    bool IsSelected);
