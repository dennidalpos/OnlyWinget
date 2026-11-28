namespace OnlyWinget.Application.WindowsUpdate;

public enum WindowsUpdateHistoryOperation
{
    Install,
    Uninstall,
    Other
}

public enum WindowsUpdateHistoryResult
{
    NotStarted,
    InProgress,
    Succeeded,
    SucceededWithErrors,
    Failed,
    Aborted
}

/// <summary>One row of the Windows Update history (IUpdateHistoryEntry).</summary>
public sealed record WindowsUpdateHistoryEntry(
    DateTimeOffset Date,
    string Title,
    WindowsUpdateHistoryOperation Operation,
    WindowsUpdateHistoryResult Result,
    int HResult,
    WindowsUpdateClassification Classification,
    string? KnowledgeBaseArticle,
    string? UpdateId);
