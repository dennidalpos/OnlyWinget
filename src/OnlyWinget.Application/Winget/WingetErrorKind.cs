namespace OnlyWinget.Application.Winget;

public enum WingetErrorKind
{
    None,
    NotFound,
    NoUpdates,
    SourceUnavailable,
    Cancelled,
    Unknown
}
