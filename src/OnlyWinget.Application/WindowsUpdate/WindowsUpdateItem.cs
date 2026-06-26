namespace OnlyWinget.Application.WindowsUpdate;

public sealed record WindowsUpdateItem(
    WindowsUpdateIdentity Identity,
    string Title,
    string? Description,
    string? Severity,
    IReadOnlyList<string> Categories,
    bool IsDownloaded,
    bool RebootRequired);
