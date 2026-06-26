namespace OnlyWinget.Application.Presentation;

public sealed record SourceRow(
    string Name,
    string Argument,
    bool IsExplicit,
    string Status);
