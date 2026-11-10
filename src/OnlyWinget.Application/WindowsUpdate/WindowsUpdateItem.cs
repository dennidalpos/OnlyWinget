namespace OnlyWinget.Application.WindowsUpdate;

public sealed record WindowsUpdateItem(
    WindowsUpdateIdentity Identity,
    string Title,
    string? Description,
    string? Severity,
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> KnowledgeBaseArticles,
    ulong MaxDownloadSize,
    bool IsDownloaded,
    bool RebootRequired);
