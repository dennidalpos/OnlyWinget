namespace OnlyWinget.Application.Presentation;

public sealed record WindowsUpdateRow(
    string UpdateId,
    int RevisionNumber,
    string Title,
    string? Description,
    string? Severity,
    string Categories,
    bool IsDownloaded,
    bool RebootRequired,
    bool IsSelected,
    string? Status,
    string? Message);
