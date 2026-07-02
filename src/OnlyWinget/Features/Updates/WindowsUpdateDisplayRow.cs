namespace OnlyWinget.Features.Updates;

public sealed record WindowsUpdateDisplayRow(
    string UpdateId,
    int RevisionNumber,
    string Revision,
    string Title,
    string KnowledgeBaseArticles,
    string Severity,
    string Categories,
    string MaxDownloadSize,
    string IsDownloaded,
    string RebootRequired,
    bool IsSelected,
    string Status);
