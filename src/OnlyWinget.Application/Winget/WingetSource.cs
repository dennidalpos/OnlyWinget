namespace OnlyWinget.Application.Winget;

public sealed record WingetSource(
    string Name,
    string Argument,
    bool IsExplicit,
    WingetSourceStatus Status,
    bool IsEnabled = true);
