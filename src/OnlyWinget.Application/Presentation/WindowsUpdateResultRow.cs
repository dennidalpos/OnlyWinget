namespace OnlyWinget.Application.Presentation;

public sealed record WindowsUpdateResultRow(
    string UpdateId,
    int RevisionNumber,
    string Title,
    bool Succeeded,
    bool RebootRequired,
    string Status,
    string? Message);
