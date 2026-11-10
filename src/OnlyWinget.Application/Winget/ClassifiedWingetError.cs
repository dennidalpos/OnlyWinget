namespace OnlyWinget.Application.Winget;

public sealed record ClassifiedWingetError(WingetErrorKind Kind, string Message);
